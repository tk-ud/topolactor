using System.Text.Json;
using Topolactor.Repository;

namespace Topolactor.Runtime;

/// <summary>
/// Reads screen_data_shape and screen_read_query_wiring from manifest topology JSON.
/// </summary>
public static class ScreenDataShapeTopologyReader
{
    public static JsonElement? FindScreenDataShapeEntry(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                !string.Equals(typeEl.GetString(), ManifestCanonicalProjection.ScreenDataShapeEntryType, StringComparison.Ordinal))
            {
                continue;
            }
            return entry;
        }
        return null;
    }

    /// <summary>Trimmed string property value, or null when absent/blank/non-string.</summary>
    public static string? ExtractStringProperty(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return null;
        var value = el.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Finds the screen_data_shape entry inside a serialized hubs.topology_manifests.topology_jsonb
    /// value. That column's shape varies by writer -- all three are real, live shapes:
    /// 1. ManifestCanonicalProjection.UpsertTopologyManifestAsync (the authoring/promote path)
    ///    writes `{"manifest_id": ..., "entries": [...same shape as manifest.topology...]}`.
    /// 2. Several db/*_preset_seed.sql presets merge a top-level key directly, e.g.
    ///    `topology_jsonb || jsonb_build_object('screen_data_shape', '{...}'::jsonb)`, so the entry
    ///    is addressable as `topology_jsonb->'screen_data_shape'` with no "entries"/"type" wrapper
    ///    (see db/email_approval_form_preset_seed.sql).
    /// 3. Other seed rows write a bare entries array (e.g. via `to_jsonb(m.topology)`).
    /// An unrelated ad-hoc object (e.g. demo_seed.sql's `{"demo": true}`) matches none of these and
    /// returns null, same as a manifest with no screen_data_shape entry at all.
    /// </summary>
    public static JsonElement? FindScreenDataShapeEntryFromTopologyManifestJsonb(string? topologyJsonbText)
    {
        if (string.IsNullOrWhiteSpace(topologyJsonbText)) return null;
        try
        {
            using var doc = JsonDocument.Parse(topologyJsonbText);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("screen_data_shape", out var directShapeEl) &&
                directShapeEl.ValueKind == JsonValueKind.Object)
            {
                return directShapeEl.Clone();
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                return FindScreenDataShapeEntry(root.EnumerateArray().ToList())?.Clone();
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("entries", out var entriesEl) &&
                entriesEl.ValueKind == JsonValueKind.Array)
            {
                return FindScreenDataShapeEntry(entriesEl.EnumerateArray().ToList())?.Clone();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsScreenReadAction(string? layer, string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return false;
        var normalizedAction = action.Trim();
        if (!string.Equals(normalizedAction, "Read", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedAction, "Search", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var layerNorm = (layer ?? "").Trim().ToLowerInvariant();
        return layerNorm is "screen_list" or "screen_aggregation" or "screen_entity" or "screen_detail";
    }
}
