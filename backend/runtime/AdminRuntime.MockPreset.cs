using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — Mock Preset Intake Compiler actions.
// SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml
// Entry: AdminRuntime.ExecuteDataAsync layer=mock_preset actions:
//   create | list | get | compile | bind
//
// Boundary invariants:
//   - bind returns layout_patch_json as data payload ONLY (tmp canvas draft)
//   - bind does NOT write to topology.ui_topology_tensor or any canonical topology table
//   - All failures are explicit errors; no silent fallback
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    private static readonly HashSet<string> ValidMockPresetSourceKinds =
        new(StringComparer.OrdinalIgnoreCase) { "svg_xml", "generic_xml", "figma_json", "ui_builder_canvas" };

    // ─── create ─────────────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataMockPresetCreateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_mockPresetRepository is null)
            return (null, new ValidationError("MOCK_PRESET_NOT_CONFIGURED", "Mock preset repository is not registered"));

        if (vector.Payload is null)
            return (null, new ValidationError("PRESET_PAYLOAD_REQUIRED", "payload is required for mock_preset:create"));

        var payload = vector.Payload.Value;

        var presetKey = payload.TryGetProperty("presetKey", out var pk) ? pk.GetString() : null;
        var presetLabel = payload.TryGetProperty("presetLabel", out var pl) ? pl.GetString() : null;
        var sourceKind = payload.TryGetProperty("sourceKind", out var sk) ? sk.GetString() : null;

        if (string.IsNullOrWhiteSpace(presetKey))
            return (null, new ValidationError("PRESET_KEY_REQUIRED", "presetKey is required"));
        if (string.IsNullOrWhiteSpace(presetLabel))
            return (null, new ValidationError("PRESET_LABEL_REQUIRED", "presetLabel is required"));
        if (string.IsNullOrWhiteSpace(sourceKind) || !ValidMockPresetSourceKinds.Contains(sourceKind!))
            return (null, new ValidationError("SOURCE_KIND_INVALID",
                $"sourceKind must be one of: {string.Join(", ", ValidMockPresetSourceKinds)}"));

        var sourceHash = payload.TryGetProperty("sourceHash", out var sh) ? sh.GetString() ?? "" : "";
        var sourceSnapshotJson = payload.TryGetProperty("sourceSnapshotJson", out var ssj)
            ? ssj : JsonSerializer.SerializeToElement(new { });
        var visualTreeJson = payload.TryGetProperty("visualTreeJson", out var vtj)
            ? vtj : JsonSerializer.SerializeToElement(new { nodes = Array.Empty<object>() });

        var request = new MockPresetCreateRequest(
            PresetKey: presetKey!,
            PresetLabel: presetLabel!,
            SourceKind: sourceKind!,
            SourceHash: sourceHash,
            SourceSnapshotJson: sourceSnapshotJson,
            VisualTreeJson: visualTreeJson
        );

        var (presetId, errorCode, message) = await _mockPresetRepository.CreatePresetAsync(request, ct);

        if (errorCode is not null)
            return (null, new ValidationError(errorCode, message ?? errorCode));

        _logger.LogInformation("AdminRuntime.MockPreset.Create: presetId={PresetId} key={Key}", presetId, presetKey);
        return (JsonSerializer.SerializeToElement(new { ok = true, presetId, presetKey }), null);
    }

    // ─── list ────────────────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataMockPresetListAsync(OperationVector vector, CancellationToken ct)
    {
        if (_mockPresetRepository is null)
            return (null, new ValidationError("MOCK_PRESET_NOT_CONFIGURED", "Mock preset repository is not registered"));

        var status = "active";
        if (vector.Payload.HasValue &&
            vector.Payload.Value.TryGetProperty("status", out var st) &&
            st.GetString() is string statusStr)
        {
            status = statusStr;
        }

        var presets = await _mockPresetRepository.ListPresetsAsync(status, ct);
        return (JsonSerializer.SerializeToElement(presets), null);
    }

    // ─── get ─────────────────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataMockPresetGetAsync(OperationVector vector, CancellationToken ct)
    {
        if (_mockPresetRepository is null)
            return (null, new ValidationError("MOCK_PRESET_NOT_CONFIGURED", "Mock preset repository is not registered"));

        if (vector.IdOrHubId is null)
            return (null, new ValidationError("PRESET_ID_REQUIRED", "idOrHubId (presetId) is required for mock_preset:get"));

        var preset = await _mockPresetRepository.GetPresetAsync(vector.IdOrHubId.Value, ct);
        if (preset is null)
            return (null, new ValidationError("PRESET_NOT_FOUND", $"Preset {vector.IdOrHubId} not found"));

        return (JsonSerializer.SerializeToElement(preset), null);
    }

    // ─── compile ─────────────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataMockPresetCompileAsync(OperationVector vector, CancellationToken ct)
    {
        if (_mockPresetRepository is null)
            return (null, new ValidationError("MOCK_PRESET_NOT_CONFIGURED", "Mock preset repository is not registered"));

        if (vector.IdOrHubId is null)
            return (null, new ValidationError("PRESET_ID_REQUIRED", "idOrHubId (presetId) is required for mock_preset:compile"));

        var preset = await _mockPresetRepository.GetPresetAsync(vector.IdOrHubId.Value, ct);
        if (preset is null)
            return (null, new ValidationError("PRESET_NOT_FOUND", $"Preset {vector.IdOrHubId} not found"));

        // Compile: produce layout_patch_json from object mappings.
        // Deterministic; no AI inference.
        var compileResult = CompilePresetFromMappings(preset);
        if (compileResult.ErrorCode is not null)
            return (null, new ValidationError(compileResult.ErrorCode, compileResult.Message ?? compileResult.ErrorCode));

        // Persist compile snapshot.
        var (snapshotId, saveError) = await _mockPresetRepository.SaveCompileSnapshotAsync(
            vector.IdOrHubId.Value, compileResult, ct);

        if (saveError is not null)
            return (null, new ValidationError(saveError, $"Failed to save compile snapshot: {saveError}"));

        _logger.LogInformation(
            "AdminRuntime.MockPreset.Compile: presetId={PresetId} snapshotId={SnapshotId}",
            vector.IdOrHubId, snapshotId);

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            compileSnapshotId = snapshotId,
            layoutPatchJson = compileResult.LayoutPatchJson,
            unresolvedJson = compileResult.UnresolvedJson,
        }), null);
    }

    // ─── save_mappings ───────────────────────────────────────────────────────
    // Persists object mappings from the drawer to topology.mock_preset_object_mapping.
    // Called immediately after preset create, before compile.

    private async Task<(JsonElement? data, ValidationError? error)>
        DataMockPresetSaveMappingsAsync(OperationVector vector, CancellationToken ct)
    {
        if (_mockPresetRepository is null)
            return (null, new ValidationError("MOCK_PRESET_NOT_CONFIGURED", "Mock preset repository is not registered"));

        if (vector.Payload is null)
            return (null, new ValidationError("PRESET_PAYLOAD_REQUIRED", "payload is required for mock_preset:save_mappings"));

        var payload = vector.Payload.Value;
        var presetIdStr = payload.TryGetProperty("presetId", out var pi) ? pi.GetString() : null;
        if (string.IsNullOrWhiteSpace(presetIdStr) || !Guid.TryParse(presetIdStr, out var presetId))
            return (null, new ValidationError("PRESET_ID_REQUIRED", "payload.presetId (uuid) is required for mock_preset:save_mappings"));

        if (!payload.TryGetProperty("mappings", out var mappingsEl) ||
            mappingsEl.ValueKind != JsonValueKind.Array)
            return (null, new ValidationError("MAPPINGS_REQUIRED", "payload.mappings (array) is required for mock_preset:save_mappings"));

        var saved = 0;
        var errors = new List<string>();

        foreach (var m in mappingsEl.EnumerateArray())
        {
            var sourceObjectId = m.TryGetProperty("sourceObjectId", out var soi) ? soi.GetString() : null;
            var nodeId = m.TryGetProperty("nodeId", out var ni) ? ni.GetString() : null;
            var nodeKind = m.TryGetProperty("nodeKind", out var nk) ? nk.GetString() : null;
            var mappingStatus = m.TryGetProperty("mappingStatus", out var ms) ? ms.GetString() : "mapped";
            var componentKey = m.TryGetProperty("componentKey", out var ck) && ck.ValueKind != JsonValueKind.Null ? ck.GetString() : null;
            var componentKind = m.TryGetProperty("componentKind", out var ckk) && ckk.ValueKind != JsonValueKind.Null ? ckk.GetString() : null;
            var htmlTag = m.TryGetProperty("htmlTag", out var ht) && ht.ValueKind != JsonValueKind.Null ? ht.GetString() : null;
            var parentId = m.TryGetProperty("parentSourceObjectId", out var par) && par.ValueKind != JsonValueKind.Null ? par.GetString() : null;
            var slotKey = m.TryGetProperty("slotKey", out var sk) && sk.ValueKind != JsonValueKind.Null ? sk.GetString() : null;
            var orderIndex = m.TryGetProperty("orderIndex", out var oi) && oi.TryGetInt32(out var oidx) ? oidx : 0;
            var bbox = m.TryGetProperty("bboxJson", out var bj) ? bj : JsonSerializer.SerializeToElement(new { });
            var text = m.TryGetProperty("textJson", out var tj) ? tj : JsonSerializer.SerializeToElement(new { });
            var style = m.TryGetProperty("styleCandidateJson", out var scj) ? scj : JsonSerializer.SerializeToElement(Array.Empty<object>());

            if (string.IsNullOrWhiteSpace(sourceObjectId) || string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(nodeKind))
            {
                errors.Add($"Skipped malformed mapping (missing sourceObjectId/nodeId/nodeKind)");
                continue;
            }

            // mappingId not used here (required by record constructor but irrelevant for upsert)
            var record = new MockPresetObjectMappingRecord(
                MappingId: "",
                SourceObjectId: sourceObjectId!,
                NodeId: nodeId!,
                NodeKind: nodeKind!,
                ComponentKey: componentKey,
                ComponentKind: componentKind,
                HtmlTag: htmlTag,
                ParentSourceObjectId: parentId,
                SlotKey: slotKey,
                OrderIndex: orderIndex,
                BboxJson: bbox,
                TextJson: text,
                StyleCandidateJson: style,
                MappingStatus: mappingStatus ?? "mapped"
            );

            var (_, errorCode) = await _mockPresetRepository.UpsertObjectMappingAsync(presetId, record, ct);
            if (errorCode is not null)
                errors.Add($"Failed to upsert {sourceObjectId}: {errorCode}");
            else
                saved++;
        }

        // Optionally persist wiring candidates (payload.wiringCandidates array).
        var wiringCandidatesSaved = 0;
        if (payload.TryGetProperty("wiringCandidates", out var wcEl) && wcEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var wc in wcEl.EnumerateArray())
            {
                var wcSourceObjectId = wc.TryGetProperty("sourceObjectId", out var wsoi) ? wsoi.GetString() : null;
                var wcNodeId = wc.TryGetProperty("nodeId", out var wni) ? wni.GetString() : null;
                var wcCapabilityTag = wc.TryGetProperty("capabilityTag", out var wct) ? wct.GetString() : null;
                var wcWiringKind = wc.TryGetProperty("wiringKind", out var wwk) && wwk.ValueKind != JsonValueKind.Null ? wwk.GetString() : null;
                var wcTargetSurface = wc.TryGetProperty("targetSurface", out var wts) && wts.ValueKind != JsonValueKind.Null ? wts.GetString() : null;
                var wcTargetRef = wc.TryGetProperty("targetRef", out var wtr) && wtr.ValueKind != JsonValueKind.Null ? wtr.GetString() : null;
                var wcBinding = wc.TryGetProperty("bindingJson", out var wbj) ? wbj : JsonSerializer.SerializeToElement(new { });
                var wcStatus = wc.TryGetProperty("status", out var ws) ? ws.GetString() ?? "pending" : "pending";

                if (string.IsNullOrWhiteSpace(wcSourceObjectId) || string.IsNullOrWhiteSpace(wcNodeId) || string.IsNullOrWhiteSpace(wcCapabilityTag))
                    continue;

                var wcRecord = new MockPresetWiringCandidateRecord(
                    WiringCandidateId: "",
                    SourceObjectId: wcSourceObjectId!,
                    NodeId: wcNodeId!,
                    CapabilityTag: wcCapabilityTag!,
                    WiringKind: wcWiringKind,
                    TargetSurface: wcTargetSurface,
                    TargetRef: wcTargetRef,
                    BindingJson: wcBinding,
                    Status: wcStatus
                );

                var (_, wcError) = await _mockPresetRepository.UpsertWiringCandidateAsync(presetId, wcRecord, ct);
                if (wcError is null) wiringCandidatesSaved++;
                else errors.Add($"WiringCandidate {wcSourceObjectId}/{wcCapabilityTag}: {wcError}");
            }
        }

        _logger.LogInformation(
            "AdminRuntime.MockPreset.SaveMappings: presetId={PresetId} saved={Saved} wirings={Wirings} errors={Errors}",
            presetId, saved, wiringCandidatesSaved, errors.Count);

        return (JsonSerializer.SerializeToElement(new
        {
            ok = errors.Count == 0,
            presetId = presetIdStr,
            savedCount = saved,
            wiringCandidatesSavedCount = wiringCandidatesSaved,
            errors,
        }), null);
    }

    // ─── bind ────────────────────────────────────────────────────────────────
    // bind returns compile_snapshot layout_patch_json as a tmp canvas draft payload.
    // It does NOT write to topology.ui_topology_tensor or canonical topology tables.

    private async Task<(JsonElement? data, ValidationError? error)>
        DataMockPresetBindAsync(OperationVector vector, CancellationToken ct)
    {
        if (_mockPresetRepository is null)
            return (null, new ValidationError("MOCK_PRESET_NOT_CONFIGURED", "Mock preset repository is not registered"));

        if (vector.Payload is null)
            return (null, new ValidationError("PRESET_BIND_PAYLOAD_REQUIRED", "payload is required for mock_preset:bind"));

        var payload = vector.Payload.Value;
        var presetIdStr = payload.TryGetProperty("presetId", out var pi) ? pi.GetString() : null;
        var routeKey = payload.TryGetProperty("routeKey", out var rk) ? rk.GetString() : null;

        if (string.IsNullOrWhiteSpace(presetIdStr) || !Guid.TryParse(presetIdStr, out var presetId))
            return (null, new ValidationError("PRESET_ID_REQUIRED", "payload.presetId (uuid) is required for mock_preset:bind"));

        if (string.IsNullOrWhiteSpace(routeKey))
            return (null, new ValidationError("ROUTE_KEY_NOT_SELECTED", "payload.routeKey is required for mock_preset:bind"));

        var snapshot = await _mockPresetRepository.GetLatestCompileSnapshotAsync(presetId, ct);
        if (snapshot is null)
        {
            // No compile snapshot; attempt on-demand compile.
            var preset = await _mockPresetRepository.GetPresetAsync(presetId, ct);
            if (preset is null)
                return (null, new ValidationError("PRESET_NOT_FOUND", $"Preset {presetId} not found"));

            var compiled = CompilePresetFromMappings(preset);
            if (compiled.ErrorCode is not null)
                return (null, new ValidationError(compiled.ErrorCode, compiled.Message ?? compiled.ErrorCode));

            await _mockPresetRepository.SaveCompileSnapshotAsync(presetId, compiled, ct);
            snapshot = new MockPresetBindResult(
                Ok: true,
                LayoutPatchJson: compiled.LayoutPatchJson,
                PackageMembershipCandidateJson: JsonSerializer.SerializeToElement(new { }),
                WiringCandidateJson: JsonSerializer.SerializeToElement(Array.Empty<object>()),
                StyleCandidateJson: JsonSerializer.SerializeToElement(Array.Empty<object>()),
                UnresolvedJson: compiled.UnresolvedJson
            );
        }

        _logger.LogInformation(
            "AdminRuntime.MockPreset.Bind: presetId={PresetId} routeKey={RouteKey} (tmp canvas draft only)",
            presetId, routeKey);

        // Return as data payload only. Frontend holds this in local draftNodes state.
        // This path does NOT write to topology.ui_topology_tensor or canonical topology tables.
        return (JsonSerializer.SerializeToElement(snapshot), null);
    }

    // ─── deterministic compiler ──────────────────────────────────────────────
    // Converts preset object mappings → layout_patch_json compatible shape.
    // No AI inference. Parser rules per SSOT §compiler_contract.

    private static MockPresetCompileResult CompilePresetFromMappings(MockPresetDetail preset)
    {
        var nodes = new List<object>();
        var unresolved = new List<object>();

        foreach (var mapping in preset.ObjectMappings.Where(m => m.MappingStatus != "ignored"))
        {
            if (mapping.MappingStatus == "unassigned" || mapping.MappingStatus == "unresolved")
            {
                unresolved.Add(new
                {
                    sourceObjectId = mapping.SourceObjectId,
                    nodeId = mapping.NodeId,
                    reason = "UNRESOLVED_REQUIRED_OBJECT",
                    mappingStatus = mapping.MappingStatus,
                });
                continue;
            }

            if (mapping.NodeKind == "catalog_component" && string.IsNullOrWhiteSpace(mapping.ComponentKey))
            {
                unresolved.Add(new
                {
                    sourceObjectId = mapping.SourceObjectId,
                    nodeId = mapping.NodeId,
                    reason = "COMPONENT_KEY_NOT_IN_BUCKET_OR_CATALOG",
                });
                continue;
            }

            if (mapping.NodeKind == "structural_html" && string.IsNullOrWhiteSpace(mapping.HtmlTag))
            {
                unresolved.Add(new
                {
                    sourceObjectId = mapping.SourceObjectId,
                    nodeId = mapping.NodeId,
                    reason = "STRUCTURAL_HTML_TAG_NOT_ALLOWLISTED",
                });
                continue;
            }

            // Build VisualNodePayload-compatible shape (see frontend/runtime/visualLayoutUtils.ts).
            var bbox = mapping.BboxJson;
            var x = TryGetInt(bbox, "x", 0);
            var y = TryGetInt(bbox, "y", 0);
            var width = TryGetInt(bbox, "width", 140);
            var height = TryGetInt(bbox, "height", 60);

            nodes.Add(new
            {
                nodeId = mapping.NodeId,
                nodeKind = mapping.NodeKind,
                componentKey = mapping.NodeKind == "structural_html"
                    ? "layout/structural_html"
                    : mapping.ComponentKey,
                htmlTag = mapping.HtmlTag,
                componentKind = mapping.ComponentKind,
                isDraftOnly = false,
                slotKey = mapping.SlotKey ?? "",
                orderIndex = mapping.OrderIndex,
                parentNodeId = mapping.ParentSourceObjectId,
                gridCol = Math.Max(1, x / 50 + 1),
                gridRow = Math.Max(1, y / 40 + 1),
                x,
                y,
                width,
                height,
            });
        }

        var layoutPatchJson = JsonSerializer.SerializeToElement(new
        {
            nodes,
            layoutClassRefs = Array.Empty<string>(),
        });

        var unresolvedJson = JsonSerializer.SerializeToElement(unresolved);

        return new MockPresetCompileResult(
            Ok: true,
            CompileSnapshotId: null,
            LayoutPatchJson: layoutPatchJson,
            UnresolvedJson: unresolvedJson
        );
    }

    private static int TryGetInt(JsonElement el, string property, int fallback)
    {
        if (el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(property, out var v) &&
            v.TryGetInt32(out var i))
            return i;
        return fallback;
    }
}
