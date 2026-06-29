using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Clone draft metadata helper.
///
/// SSOT: docs/design/admin-console-workflow-ssot.yaml
///   canonical_sequential_authoring_pipeline.admin_contents_step1_entry_modes
///   .clone_metadata_rule / .draft_origin_vocabulary / .replacement_clone_merge_lifecycle
///
/// Clone is NOT a lifecycle/status enum. Manifest authoring state stays a two-value
/// draft/active toggle. Clone semantics live in a JSONB `clone_metadata` topology entry
/// (operation context / attribute), so replacement vs clone-as-new-topology can be
/// distinguished by draft_origin / clone_mode / source_active_manifest_id / source evidence
/// without ever superposing draft+active on one row or adding a clone status value.
/// </summary>
public static class CloneDraftMetadata
{
    public const string EntryType = "clone_metadata";

    // draft_origin vocabulary (admin-console-workflow-ssot draft_origin_vocabulary.values)
    public const string OriginManualNew = "manual_new";
    public const string OriginManualCloneReplacement = "manual_clone_replacement";
    public const string OriginManualCloneNewTopology = "manual_clone_new_topology";
    public const string OriginSqlAttentionCandidate = "sql_attention_candidate";

    // clone_mode vocabulary (clone_mode_values)
    public const string CloneModeNone = "none";
    public const string CloneModeReplacement = "replacement";
    public const string CloneModeNewTopology = "new_topology";

    public static readonly IReadOnlySet<string> DraftOrigins = new HashSet<string>(StringComparer.Ordinal)
    {
        OriginManualNew,
        OriginManualCloneReplacement,
        OriginManualCloneNewTopology,
        OriginSqlAttentionCandidate,
    };

    public static readonly IReadOnlySet<string> CloneModes = new HashSet<string>(StringComparer.Ordinal)
    {
        CloneModeNone,
        CloneModeReplacement,
        CloneModeNewTopology,
    };

    public record CloneMetadata(
        string DraftOrigin,
        string CloneMode,
        Guid? SourceActiveManifestId,
        CloneSourceEvidence? SourceEvidence);

    /// <summary>
    /// Builds a clone_metadata topology entry. clone_off (cloneMode=none) MUST NOT carry a
    /// source_active_manifest_id; clone_on (replacement / new_topology) requires it.
    /// </summary>
    public static JsonElement BuildEntry(
        string draftOrigin,
        string cloneMode,
        Guid? sourceActiveManifestId,
        CloneSourceEvidence? evidence)
    {
        object? evidenceObj = evidence is null
            ? null
            : new
            {
                sourceActiveManifestId = evidence.SourceActiveManifestId.ToString(),
                topologySystemName = evidence.TopologySystemName,
                routeKey = evidence.RouteKey,
                dispatcherAxes = evidence.DispatcherAxes,
                sourceUpdatedAtUnixMs = evidence.SourceUpdatedAt.ToUnixTimeMilliseconds(),
                sourceTopologyHash = evidence.SourceTopologyHash,
                status = evidence.Status,
            };

        return JsonSerializer.SerializeToElement(new
        {
            type = EntryType,
            draftOrigin,
            cloneMode,
            // clone_off keeps source_active_manifest_id null/absent (clone_metadata_rule).
            sourceActiveManifestId = sourceActiveManifestId?.ToString(),
            sourceEvidence = evidenceObj,
        });
    }

    public static CloneMetadata? Extract(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                !string.Equals(typeEl.GetString(), EntryType, StringComparison.Ordinal)) continue;

            var draftOrigin = ReadString(entry, "draftOrigin") ?? OriginManualNew;
            var cloneMode = ReadString(entry, "cloneMode") ?? CloneModeNone;

            Guid? sourceId = null;
            var sourceRaw = ReadString(entry, "sourceActiveManifestId");
            if (!string.IsNullOrWhiteSpace(sourceRaw) && Guid.TryParse(sourceRaw, out var parsed))
                sourceId = parsed;

            CloneSourceEvidence? evidence = null;
            if (entry.TryGetProperty("sourceEvidence", out var evEl) &&
                evEl.ValueKind == JsonValueKind.Object)
            {
                evidence = ExtractEvidence(evEl);
            }

            return new CloneMetadata(draftOrigin, cloneMode, sourceId, evidence);
        }

        return null;
    }

    private static CloneSourceEvidence? ExtractEvidence(JsonElement evEl)
    {
        var sourceRaw = ReadString(evEl, "sourceActiveManifestId");
        if (string.IsNullOrWhiteSpace(sourceRaw) || !Guid.TryParse(sourceRaw, out var sourceId))
            return null;

        AdminManifestDispatcherMappingDto? axes = null;
        if (evEl.TryGetProperty("dispatcherAxes", out var axesEl) &&
            axesEl.ValueKind == JsonValueKind.Object &&
            ReadString(axesEl, "role") is { } role &&
            ReadString(axesEl, "target") is { } target &&
            ReadString(axesEl, "layer") is { } layer &&
            ReadString(axesEl, "action") is { } action)
        {
            axes = new AdminManifestDispatcherMappingDto(role, target, layer, action);
        }

        long updatedMs = 0;
        if (evEl.TryGetProperty("sourceUpdatedAtUnixMs", out var msEl) &&
            msEl.ValueKind == JsonValueKind.Number)
        {
            updatedMs = msEl.GetInt64();
        }

        return new CloneSourceEvidence(
            sourceId,
            ReadString(evEl, "topologySystemName"),
            ReadString(evEl, "routeKey"),
            axes,
            DateTimeOffset.FromUnixTimeMilliseconds(updatedMs),
            ReadString(evEl, "sourceTopologyHash") ?? string.Empty,
            ReadString(evEl, "status") ?? "active");
    }

    /// <summary>
    /// Replaces the screen_data_shape identity for a clone-as-new-topology draft and strips
    /// the source identity bindings (hub_grouping, dispatcher_mapping) so the cloned draft
    /// CANNOT replace the source active and is forced onto the conventional register/promote
    /// path with a new topologySystemName. tableRef is cleared so the author re-binds in Step 3.
    /// </summary>
    public static IReadOnlyList<JsonElement> WithNewTopologyIdentity(
        IReadOnlyList<JsonElement> topology,
        string newTopologySystemName,
        string? userFacingLabel)
    {
        var result = new List<JsonElement>(topology.Count);
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String)
            {
                result.Add(entry);
                continue;
            }

            var type = typeEl.GetString();
            // Drop source identity bindings — new topology re-derives its own identity.
            if (string.Equals(type, ManifestCanonicalProjection.HubGroupingEntryType, StringComparison.Ordinal) ||
                string.Equals(type, "dispatcher_mapping", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(type, ManifestCanonicalProjection.ScreenDataShapeEntryType, StringComparison.Ordinal))
            {
                result.Add(RewriteScreenDataShapeIdentity(entry, newTopologySystemName, userFacingLabel));
                continue;
            }

            result.Add(entry);
        }

        return result;
    }

    private static JsonElement RewriteScreenDataShapeIdentity(
        JsonElement shape,
        string newTopologySystemName,
        string? userFacingLabel)
    {
        var map = new Dictionary<string, JsonElement>();
        foreach (var prop in shape.EnumerateObject())
            map[prop.Name] = prop.Value.Clone();

        map["topologySystemName"] = JsonSerializer.SerializeToElement(newTopologySystemName);
        map["userFacingTopologyLabel"] = JsonSerializer.SerializeToElement(userFacingLabel);
        // Clear physical binding so the new topology re-binds its own physical table in Step 3.
        map["tableRef"] = JsonSerializer.SerializeToElement((string?)null);
        map["dbTableName"] = JsonSerializer.SerializeToElement((string?)null);

        return JsonSerializer.SerializeToElement(map);
    }

    /// <summary>Reads the screen_data_shape topologySystemName for source evidence.</summary>
    public static string? ExtractTopologySystemName(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                !string.Equals(typeEl.GetString(), ManifestCanonicalProjection.ScreenDataShapeEntryType, StringComparison.Ordinal))
                continue;
            return ReadString(entry, "topologySystemName");
        }
        return null;
    }

    /// <summary>
    /// Stable hash over the topology entries, used as the replacement merge diff base and
    /// stale-source detection key. Recomputed at merge time against the live source active row.
    /// </summary>
    public static string ComputeTopologyHash(IReadOnlyList<JsonElement> topology)
    {
        var sb = new StringBuilder();
        foreach (var entry in topology)
        {
            // Re-serialize through JsonSerializer to normalize whitespace/ordering noise.
            sb.Append(JsonSerializer.Serialize(entry));
            sb.Append('\n');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Computes diff/log evidence for a replacement merge: which topology entry types changed
    /// between the source active row and the working draft. ChangeCount==0 means there is no
    /// evidence justifying a merge (no-op merges fail close).
    /// </summary>
    public static (string DiffJson, int ChangeCount) ComputeTopologyDiff(
        IReadOnlyList<JsonElement> before,
        IReadOnlyList<JsonElement> after)
    {
        var beforeByType = GroupByType(before);
        var afterByType = GroupByType(after);

        var changedTypes = new List<object>();
        var allTypes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var k in beforeByType.Keys) allTypes.Add(k);
        foreach (var k in afterByType.Keys) allTypes.Add(k);

        foreach (var type in allTypes)
        {
            // clone_metadata is authoring bookkeeping, not part of the merged production shape.
            if (string.Equals(type, EntryType, StringComparison.Ordinal)) continue;

            beforeByType.TryGetValue(type, out var b);
            afterByType.TryGetValue(type, out var a);
            if (!string.Equals(b, a, StringComparison.Ordinal))
            {
                changedTypes.Add(new
                {
                    type,
                    changeKind = b is null ? "added" : a is null ? "removed" : "modified",
                });
            }
        }

        var diff = new
        {
            changedEntryTypes = changedTypes,
            changeCount = changedTypes.Count,
        };
        return (JsonSerializer.Serialize(diff), changedTypes.Count);
    }

    private static Dictionary<string, string> GroupByType(IReadOnlyList<JsonElement> topology)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
            var type = typeEl.GetString() ?? string.Empty;
            // Last entry of a type wins (topology keeps one canonical entry per type).
            map[type] = JsonSerializer.Serialize(entry);
        }
        return map;
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
