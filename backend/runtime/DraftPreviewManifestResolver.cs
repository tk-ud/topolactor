using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves draft manifest screen_data_shape by topologySystemName (routeKey SSOT).
/// Contents Step 3 persists initialDataRows as topology intent — not content_entity_drafts.
/// </summary>
public static class DraftPreviewManifestResolver
{
    public static async Task<(ManifestRecord? Manifest, ValidationError? Error)> ResolveDraftByTopologySystemNameAsync(
        ManifestRepository manifestRepository,
        string topologySystemName,
        CancellationToken ct = default)
    {
        var normalized = topologySystemName?.Trim() ?? "";
        if (normalized.Length == 0)
            return (null, null);

        var draftItems = await manifestRepository.ListManifestsAsync("draft", ct);
        ManifestRecord? match = null;

        foreach (var item in draftItems)
        {
            var detail = await manifestRepository.LoadDetailByIdAsync(item.ManifestId, ct);
            if (detail is null) continue;

            var shapeEntry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntry(detail.Topology);
            if (shapeEntry is null) continue;

            if (!shapeEntry.Value.TryGetProperty("topologySystemName", out var tsnEl) ||
                tsnEl.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                continue;
            }

            if (!string.Equals(tsnEl.GetString(), normalized, StringComparison.Ordinal))
                continue;

            if (match is not null)
            {
                return (null, new ValidationError(
                    "MANIFEST_TOPOLOGY_AMBIGUOUS",
                    $"複数の draft manifest が topologySystemName '{normalized}' に一致しています。" +
                    "曖昧さは許可されません。"));
            }

            match = new ManifestRecord(
                detail.ManifestId,
                detail.RelationRegistryId,
                detail.Topology,
                detail.Status);
        }

        return (match, null);
    }
}
