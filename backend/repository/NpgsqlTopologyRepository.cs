using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Runtime;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of TopologyRepository.
/// Replaces in-memory placeholder reads with real SQL queries against the topology store.
///
/// Canonical tables:
///   structure_maps, package_registry, schema_registry, function_parameters
///
/// Wiring: inject NpgsqlTopologyRepository wherever TopologyRepository is required in
/// production DI registration. Tests continue to use the in-memory base class directly.
/// </summary>
public class NpgsqlTopologyRepository : TopologyRepository
{
    private readonly ILogger<NpgsqlTopologyRepository> _npgsqlLogger;

    public NpgsqlTopologyRepository(
        ILogger<NpgsqlTopologyRepository> logger,
        string connectionString)
        : base(NullLogger<TopologyRepository>.Instance, connectionString)
    {
        _npgsqlLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads a structure map by attractor_key or structure_map_id (UUID string).
    /// Returns null when not found — caller must treat as broken reference.
    /// SQL: SELECT ... FROM topology.structure_maps WHERE (attractor_key = @key OR structure_map_id::text = @key) AND active = true LIMIT 1
    /// </summary>
    public override async Task<StructureMapRecord?> LoadStructureMapAsync(
        string key, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT structure_map_id::text, attractor_key, package_id, schema_id, " +
            "       component_ids, state_policy::text, layout_id " +
            "FROM topology.structure_maps " +
            "WHERE (attractor_key = @key OR structure_map_id::text = @key) " +
            "  AND active = true " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("key", key);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadStructureMapAsync: no record for key='{Key}'.", key);
            return null;
        }

        var componentIdsRaw = reader.GetValue(4) as Guid[];
        var componentIds = componentIdsRaw?.Select(g => g.ToString()).ToList()
            ?? new List<string>();

        return new StructureMapRecord(
            StructureMapId: reader.GetString(0),
            AttractorKey:   reader.GetString(1),
            PackageId:      reader.GetGuid(2),
            SchemaId:       reader.GetGuid(3),
            ComponentIds:   componentIds,
            StatePolicyJson: reader.IsDBNull(5) ? null : reader.GetString(5),
            LayoutId: reader.IsDBNull(6) ? null : reader.GetGuid(6)
        );
    }

    /// <summary>
    /// Loads a package record from package_registry by package_id.
    /// Returns null when not found — caller must treat as broken reference.
    /// </summary>
    public override async Task<PackageRecord?> LoadPackageAsync(
        Guid packageId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT package_id, name, package_def::text " +
            "FROM topology.package_registry " +
            "WHERE package_id = @id AND active = true " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("id", packageId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadPackageAsync: no record for packageId='{Id}'.", packageId);
            return null;
        }

        return new PackageRecord(
            PackageId:     reader.GetGuid(0),
            PackageName:   reader.GetString(1),
            Version:       null,
            RawDefinition: reader.IsDBNull(2) ? null : reader.GetString(2)
        );
    }

    /// <summary>
    /// Loads a schema record from schema_registry by schema_id.
    /// Returns null when not found — caller must treat as broken reference.
    /// </summary>
    public override async Task<SchemaRecord?> LoadSchemaAsync(
        Guid schemaId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT schema_id, name, schema_def::text " +
            "FROM topology.schema_registry " +
            "WHERE schema_id = @id AND active = true " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("id", schemaId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadSchemaAsync: no record for schemaId='{Id}'.", schemaId);
            return null;
        }

        return new SchemaRecord(
            SchemaId:      reader.GetGuid(0),
            SchemaName:    reader.GetString(1),
            Version:       null,
            RawDefinition: reader.IsDBNull(2) ? null : reader.GetString(2)
        );
    }

    /// <inheritdoc/>
    public override async Task<ComponentRecord?> LoadComponentAsync(
        Guid componentId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT component_id, name, component_type, component_def::text " +
            "FROM topology.component_registry " +
            "WHERE component_id = @id AND active = true " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("id", componentId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadComponentAsync: no record for componentId='{Id}'.", componentId);
            return null;
        }

        return new ComponentRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    /// <summary>
    /// Loads a function_parameter value from the topology store.
    /// Returns null when no active row is found — caller must treat as policy-missing.
    /// SQL: SELECT parameter_value FROM topology.function_parameters
    ///   WHERE function_name = @fn AND parameter_key = @key AND active = true LIMIT 1
    /// </summary>
    public override async Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT parameter_value::text FROM topology.function_parameters " +
            "WHERE function_name = @fn AND parameter_key = @key AND active = true " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("fn", functionName);
        cmd.Parameters.AddWithValue("key", parameterKey);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadFunctionParameterAsync: no parameter for '{Fn}/{Key}'.",
                functionName, parameterKey);
            return null;
        }

        return (string)result;
    }

    /// <summary>
    /// Loads layout nodes for the given layout_id. Two independent sources are consulted:
    /// components_layout_design.layout_schema_json.records[] (the structural authority — read
    /// directly by the layout_id primary key, never gated on a tensor row existing) and
    /// topology.ui_topology_tensor.layout_patch_json.nodes[] (the flat tensor carrier — a
    /// read-only schema-authored layout with zero tensor rows is a valid, real shape, not a
    /// broken one). componentKind is enriched from ui_component_registry; runtimeDispatchAction
    /// from ui_wiring_registry.wiring_kind via tensor JOIN. Tensor reads at most 2 rows to detect
    /// selector ambiguity. Returns nodes sorted by orderIndex. Returns empty list only when
    /// NEITHER source has content for layout_id (no schema records[] and no tensor row).
    /// Throws InvalidOperationException("LAYOUT_NODES_AMBIGUOUS_SELECTOR:...") when multiple
    /// tensor rows exist for the same layout_id — caller must convert to an explicit ValidationError.
    /// </summary>
    public override async Task<IReadOnlyList<LayoutNodeRecord>> LoadLayoutNodesAsync(
        Guid layoutId, CancellationToken ct = default)
    {
        string? layoutSchemaJson;
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            // layout_id is components_layout_design's primary key — at most one row, no
            // ambiguity check needed here (unlike the tensor read below).
            cmd.CommandText =
                "SELECT layout_schema_json::text FROM topology.components_layout_design WHERE layout_id = @layoutId";
            cmd.Parameters.AddWithValue("layoutId", layoutId);
            var scalar = await cmd.ExecuteScalarAsync(ct);
            layoutSchemaJson = scalar is null or DBNull ? null : (string)scalar;
        }

        string? firstJson = null;
        string? wiringKind = null;
        string? wiringId = null;
        string? wiringKey = null;
        string? targetSurface = null;
        string? targetRef = null;

        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            // LIMIT 2: detect ambiguity without silent "latest wins" ORDER BY.
            // 0 rows → no tensor carrier for this layout_id (schema records[] may still supply
            // the layout below); 1 row → parse; 2+ rows → explicit error.
            cmd.CommandText =
                "SELECT t.layout_patch_json::text, w.wiring_kind, " +
                "       w.wiring_id::text, w.wiring_key, w.target_surface, w.target_ref " +
                "FROM topology.ui_topology_tensor t " +
                "LEFT JOIN topology.ui_wiring_registry w ON w.wiring_id = t.wiring_id " +
                "WHERE t.layout_id = @layoutId " +
                "LIMIT 2";
            cmd.Parameters.AddWithValue("layoutId", layoutId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var rowCount = 0;

            while (await reader.ReadAsync(ct))
            {
                rowCount++;
                if (rowCount == 1)
                {
                    firstJson = reader.IsDBNull(0) ? null : reader.GetString(0);
                    wiringKind = reader.IsDBNull(1) ? null : reader.GetString(1);
                    wiringId = reader.IsDBNull(2) ? null : reader.GetString(2);
                    wiringKey = reader.IsDBNull(3) ? null : reader.GetString(3);
                    targetSurface = reader.IsDBNull(4) ? null : reader.GetString(4);
                    targetRef = reader.IsDBNull(5) ? null : reader.GetString(5);
                }
                if (rowCount == 2)
                    throw new InvalidOperationException(
                        $"LAYOUT_NODES_AMBIGUOUS_SELECTOR: multiple tensor rows for layout_id='{layoutId}'. " +
                        "Cannot safely resolve layout nodes without a disambiguating selector (route_key or package_id). " +
                        "Ensure a unique (layout_id, route_key) or (layout_id, package_id) selector is used at apply time.");
            }
        }

        var runtimeDispatchAction = MapWiringKindToDispatchAction(wiringKind);
        var baseNodes = firstJson is null
            ? Array.Empty<LayoutNodeRecord>()
            : ParseNodesFromLayoutPatchJson(firstJson, layoutId);

        // Structural authority path: components_layout_design.layout_schema_json.records[] — when
        // populated — is the render-completion source, not the flat tensor nodes[]. It is read
        // independently of whether a tensor row exists at all: a read-only schema-authored layout
        // with zero tensor rows is a valid shape (interactionsBySourceActionKey is simply empty —
        // BuildInteractionsBySourceActionKey tolerates an empty tensor node list). Most
        // UI-Builder-authored layouts have no records[] (componentId/componentKind already sit
        // directly on tensor nodes); those layouts keep the existing tensor-only path below
        // unchanged. SSOT: docs/design/runtime-orchestration-ssot.yaml
        // ui_projection_render_reachability_contract layout_schema_structural_render_contract.
        var parseResult = LayoutSchemaTensorComposer.ParseRecords(layoutSchemaJson);
        if (parseResult is LayoutSchemaTensorComposer.RecordsParseResult.Invalid invalid)
        {
            // A present-but-malformed layout_schema_json.records[] is a real authoring defect,
            // never equivalent to "no records[]" — it must never be silently dropped to the
            // tensor-only path below, nor partially composed by skipping the bad entries. This
            // check is independent of tensor-row existence — a schema-only layout with malformed
            // records[] must fail the same way as one that also has a tensor row.
            throw new InvalidOperationException(
                $"LAYOUT_SCHEMA_RECORDS_INVALID: layout_id '{layoutId}' components_layout_design.layout_schema_json.records[] is malformed: {invalid.Reason}");
        }

        if (parseResult is LayoutSchemaTensorComposer.RecordsParseResult.Valid { Rows: var schemaRecords })
        {
            // Tensor nodes key their runtimeInteractions array at the FORM level; each entry is
            // individually tagged by sourceActionKey identifying the Action/Field leaf it belongs
            // to — grouping by that key is the correct merge join, not a tensor-nodeId match.
            // baseNodes is empty when no tensor row exists — BuildInteractionsBySourceActionKey
            // returns an empty Valid map in that case, which is correct: a schema-only layout has
            // no runtimeInteractions to merge, not a broken one.
            var interactionsParseResult = LayoutSchemaTensorComposer.BuildInteractionsBySourceActionKey(baseNodes);
            if (interactionsParseResult is LayoutSchemaTensorComposer.InteractionsParseResult.Invalid invalidInteractions)
            {
                // A malformed/unattributable tensor runtimeInteractions shape is a real authoring
                // defect — never silently skipped, never composed as if the interaction simply
                // didn't exist.
                throw new InvalidOperationException(
                    $"LAYOUT_SCHEMA_RUNTIME_INTERACTIONS_INVALID: layout_id '{layoutId}' ui_topology_tensor.layout_patch_json runtimeInteractions is malformed: {invalidInteractions.Reason}");
            }
            var interactionsBySourceActionKey =
                ((LayoutSchemaTensorComposer.InteractionsParseResult.Valid)interactionsParseResult).BySourceActionKey;

            var requiredComponentKeys = LayoutSchemaTensorComposer.RequiredComponentKeys(schemaRecords);
            var componentKeyToId = await LoadComponentIdsByKeysAsync(requiredComponentKeys, ct);
            var componentIdToKind = await LoadComponentKindsByIdsAsync(componentKeyToId.Values.ToList(), ct);
            var nodeLocalDataByNodeId = LayoutSchemaTensorComposer.BuildNodeLocalDataByNodeId(baseNodes);

            var composed = LayoutSchemaTensorComposer.Compose(
                schemaRecords,
                interactionsBySourceActionKey,
                componentKeyToId,
                componentIdToKind,
                nodeLocalDataByNodeId);

            return composed.Select(n =>
                n.NodeKind == "structural_node" || n.NodeKind == "unresolved_gap"
                    ? n
                    : n with
                    {
                        RuntimeDispatchAction = runtimeDispatchAction,
                        WiringId = wiringId,
                        WiringKey = wiringKey,
                        WiringKind = wiringKind,
                        TargetSurface = targetSurface,
                        TargetRef = targetRef,
                    }).ToList();
        }

        // No schema records[] and no tensor row: neither source has content for this layout_id.
        if (firstJson is null)
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadLayoutNodesAsync: no schema records[] and no tensor row for layoutId='{LayoutId}'.",
                layoutId);
            return Array.Empty<LayoutNodeRecord>();
        }

        if (baseNodes.Count == 0)
            return baseNodes;

        var enrichedNodes = await EnrichCatalogComponentIdsFromRegistryAsync(baseNodes, ct);

        // Batch-fetch componentKind for catalog_component nodes.
        var catalogComponentIds = enrichedNodes
            .Where(n => n.NodeKind is not "structural_html" && n.ComponentId is not null)
            .Select(n => n.ComponentId!)
            .Distinct()
            .ToList();

        var componentKindMap = await LoadComponentKindsByIdsAsync(catalogComponentIds, ct);

        var hasWiring = runtimeDispatchAction is not null || wiringId is not null;
        if (!hasWiring && componentKindMap.Count == 0)
            return enrichedNodes;

        return enrichedNodes.Select(n =>
        {
            if (n.NodeKind is "structural_html")
                return n;
            var kind = n.ComponentId is not null && componentKindMap.TryGetValue(n.ComponentId, out var k) ? k : null;
            return n with
            {
                ComponentKind = kind,
                RuntimeDispatchAction = runtimeDispatchAction,
                WiringId = wiringId,
                WiringKey = wiringKey,
                WiringKind = wiringKind,
                TargetSurface = targetSurface,
                TargetRef = targetRef,
            };
        }).ToList();
    }

    /// <inheritdoc/>
    public override async Task<string?> LoadLayoutCalcBindingsJsonAsync(
        Guid layoutId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT (COALESCE(layout_draft_tmp_json, layout_patch_json)->'calculationBindings')::text " +
            "FROM topology.ui_topology_tensor " +
            "WHERE layout_id = @layoutId " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("layoutId", layoutId);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        if (scalar is null || scalar is DBNull) return null;
        var json = scalar as string;
        if (string.IsNullOrWhiteSpace(json) || json == "null") return null;
        return json;
    }

    /// <summary>
    /// Maps ui_wiring_registry.wiring_kind to the canonical RuntimeDispatchAction.
    /// search/aggregate → Search, create → Create, update → diffUpdate, delete → logicalDelete.
    /// Returns null for unknown or null wiring_kind.
    /// </summary>
    private static string? MapWiringKindToDispatchAction(string? wiringKind) => wiringKind switch
    {
        "search" => "Search",
        "aggregate" => "Search",
        "create" => "Create",
        "update" => "diffUpdate",
        "delete" => "logicalDelete",
        _ => null,
    };

    /// <inheritdoc/>
    public override async Task<IReadOnlyDictionary<string, string>> LoadComponentIdsByKeysAsync(
        IReadOnlyList<string> componentKeys, CancellationToken ct = default)
    {
        if (componentKeys.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var keys = componentKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (keys.Length == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT component_key, component_id::text " +
            "FROM topology.ui_component_registry " +
            "WHERE component_key = ANY(@keys)";
        cmd.Parameters.AddWithValue("keys", keys);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                result[reader.GetString(0)] = reader.GetString(1);
        }
        return result;
    }

    private async Task<IReadOnlyList<LayoutNodeRecord>> EnrichCatalogComponentIdsFromRegistryAsync(
        IReadOnlyList<LayoutNodeRecord> nodes,
        CancellationToken ct)
    {
        var keysNeedingId = nodes
            .Where(n => n.NodeKind is not "structural_html"
                && string.IsNullOrWhiteSpace(n.ComponentId)
                && !string.IsNullOrWhiteSpace(n.ComponentKey))
            .Select(n => n.ComponentKey!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (keysNeedingId.Count == 0)
            return nodes;

        var idByKey = await LoadComponentIdsByKeysAsync(keysNeedingId, ct);
        if (idByKey.Count == 0)
            return nodes;

        return nodes
            .Select(n => LayoutNodeComponentIdEnrichment.Enrich(n, idByKey))
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task<IReadOnlyDictionary<string, string>> LoadComponentKindsByIdsAsync(
        IReadOnlyList<string> componentIds, CancellationToken ct = default)
    {
        if (componentIds.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var ids = componentIds
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT component_id::text, component_kind " +
            "FROM topology.ui_component_registry " +
            "WHERE component_id = ANY(@ids)";
        cmd.Parameters.AddWithValue("ids", ids);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                result[reader.GetString(0)] = reader.GetString(1);
        }
        return result;
    }

    private IReadOnlyList<LayoutNodeRecord> ParseNodesFromLayoutPatchJson(string json, Guid layoutId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("nodes", out var nodesEl) ||
                nodesEl.ValueKind != JsonValueKind.Array)
                return Array.Empty<LayoutNodeRecord>();

            var result = new List<LayoutNodeRecord>();
            foreach (var node in nodesEl.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object) continue;

                var nodeId = node.TryGetProperty("nodeId", out var nid) && nid.ValueKind == JsonValueKind.String
                    ? nid.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(nodeId)) continue;

                var nodeKind = node.TryGetProperty("nodeKind", out var nk) && nk.ValueKind == JsonValueKind.String
                    ? nk.GetString() : null;
                var htmlTag = node.TryGetProperty("htmlTag", out var ht) && ht.ValueKind == JsonValueKind.String
                    ? ht.GetString() : null;
                var componentKey = node.TryGetProperty("componentKey", out var ck) && ck.ValueKind == JsonValueKind.String
                    ? ck.GetString() : null;
                var componentId = node.TryGetProperty("componentId", out var ci) && ci.ValueKind == JsonValueKind.String
                    ? ci.GetString() : null;
                var parentNodeId = node.TryGetProperty("parentNodeId", out var pni) && pni.ValueKind == JsonValueKind.String
                    ? pni.GetString() : null;
                var slotKey = node.TryGetProperty("slotKey", out var sk) && sk.ValueKind == JsonValueKind.String
                    ? sk.GetString() : null;
                var orderIndex = node.TryGetProperty("orderIndex", out var oi) && oi.ValueKind == JsonValueKind.Number
                    ? oi.GetInt32() : 0;
                var x = node.TryGetProperty("x", out var xv) && xv.ValueKind == JsonValueKind.Number
                    ? xv.GetDouble() : 0.0;
                var y = node.TryGetProperty("y", out var yv) && yv.ValueKind == JsonValueKind.Number
                    ? yv.GetDouble() : 0.0;
                object? width = null;
                if (node.TryGetProperty("width", out var wv))
                {
                    width = wv.ValueKind switch
                    {
                        JsonValueKind.Number => wv.GetDouble(),
                        JsonValueKind.String => wv.GetString(),
                        _ => null
                    };
                }
                object? height = null;
                if (node.TryGetProperty("height", out var hv))
                {
                    height = hv.ValueKind switch
                    {
                        JsonValueKind.Number => hv.GetDouble(),
                        JsonValueKind.String => hv.GetString(),
                        _ => null
                    };
                }

                IReadOnlyList<string>? layoutClassRefs = null;
                if (node.TryGetProperty("layoutClassRefs", out var lcrEl) && lcrEl.ValueKind == JsonValueKind.Array)
                {
                    var refs = lcrEl.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToList();
                    if (refs.Count > 0) layoutClassRefs = refs;
                }

                var propsJson = node.TryGetProperty("propsJson", out var pj) && pj.ValueKind == JsonValueKind.String
                    ? pj.GetString() : null;
                var stateJson = node.TryGetProperty("stateJson", out var sj) && sj.ValueKind == JsonValueKind.String
                    ? sj.GetString() : null;
                var widthMode = node.TryGetProperty("widthMode", out var wm) && wm.ValueKind == JsonValueKind.String
                    ? wm.GetString() : null;
                var heightMode = node.TryGetProperty("heightMode", out var hm) && hm.ValueKind == JsonValueKind.String
                    ? hm.GetString() : null;
                string? propBindingsJson = null;
                if (node.TryGetProperty("propBindings", out var pb) && pb.ValueKind == JsonValueKind.Object)
                    propBindingsJson = pb.GetRawText();
                string? runtimeInteractionsJson = null;
                if (node.TryGetProperty("runtimeInteractions", out var ri) && ri.ValueKind == JsonValueKind.Array)
                    runtimeInteractionsJson = ri.GetRawText();
                string? dispatchPayloadFromByTriggerJson = null;
                if (node.TryGetProperty("dispatchPayloadFromByTrigger", out var dpfbt) && dpfbt.ValueKind == JsonValueKind.Object)
                    dispatchPayloadFromByTriggerJson = dpfbt.GetRawText();

                result.Add(new LayoutNodeRecord(
                    NodeId: nodeId!,
                    NodeKind: nodeKind,
                    HtmlTag: htmlTag,
                    ComponentKey: componentKey,
                    ComponentId: componentId,
                    ParentNodeId: parentNodeId,
                    SlotKey: slotKey,
                    OrderIndex: orderIndex,
                    X: x,
                    Y: y,
                    Width: width,
                    Height: height,
                    LayoutClassRefs: layoutClassRefs,
                    PropsJson: propsJson,
                    StateJson: stateJson,
                    PropBindingsJson: propBindingsJson,
                    RuntimeInteractionsJson: runtimeInteractionsJson,
                    DispatchPayloadFromByTriggerJson: dispatchPayloadFromByTriggerJson,
                    WidthMode: widthMode,
                    HeightMode: heightMode
                ));
            }

            result.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
            return result;
        }
        catch (JsonException ex)
        {
            _npgsqlLogger.LogWarning(
                ex,
                "NpgsqlTopologyRepository.ParseNodesFromLayoutPatchJson: JSON parse error for layoutId='{LayoutId}'. Returning empty.",
                layoutId);
            return Array.Empty<LayoutNodeRecord>();
        }
    }

}
