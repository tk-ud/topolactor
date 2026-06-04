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
