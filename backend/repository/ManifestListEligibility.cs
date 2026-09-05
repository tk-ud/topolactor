using System.Text.Json;
using Topolactor.Runtime;

namespace Topolactor.Repository;

public record ManifestListSummary(
    string? ContentsType,
    string? TopologySystemName,
    string? UserFacingTopologyLabel,
    string? TableRef,
    bool PhysicalBound,
    string? AuthoringProgressStep,
    string? DraftOrigin,
    string? CloneMode,
    string? SourceActiveManifestId,
    int LogicalTableCount);

public record ManifestListFilter(
    string? Status,
    IReadOnlyList<string> ContentsTypes,
    bool? Physical,
    int LogicalTablesMin);

/// <summary>
/// Server-side manifest:list eligibility and summary projection for admin contents surfaces.
/// </summary>
public static class ManifestListEligibility
{
    public static ManifestListFilter ParseFilter(JsonElement? payload)
    {
        if (payload is null or { ValueKind: not JsonValueKind.Object })
            return new ManifestListFilter(null, Array.Empty<string>(), null, 0);

        string? status = null;
        if (payload.Value.TryGetProperty("status", out var statusEl) &&
            statusEl.ValueKind == JsonValueKind.String)
        {
            status = statusEl.GetString();
        }

        var contentsTypes = ParseContentsTypeFilter(payload.Value);

        bool? physical = null;
        if (payload.Value.TryGetProperty("physical", out var physicalEl) &&
            (physicalEl.ValueKind == JsonValueKind.True || physicalEl.ValueKind == JsonValueKind.False))
        {
            physical = physicalEl.GetBoolean();
        }

        var logicalTablesMin = 0;
        if (payload.Value.TryGetProperty("logicalTablesMin", out var minEl) &&
            minEl.ValueKind == JsonValueKind.Number &&
            minEl.TryGetInt32(out var parsedMin))
        {
            logicalTablesMin = Math.Max(0, parsedMin);
        }

        return new ManifestListFilter(status, contentsTypes, physical, logicalTablesMin);
    }

    public static ManifestListSummary Summarize(
        IReadOnlyList<JsonElement> topology,
        IReadOnlySet<string> activePhysicalTableRefs)
    {
        var shapeEntry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntry(topology);
        var cloneMeta = CloneDraftMetadata.Extract(topology);

        if (shapeEntry is null)
        {
            return new ManifestListSummary(
                null, null, null, null, false, null,
                cloneMeta?.DraftOrigin, cloneMeta?.CloneMode,
                cloneMeta?.SourceActiveManifestId?.ToString(), 0);
        }

        var shape = shapeEntry.Value;
        var contentsType = ScreenDataShapeTopologyReader.ExtractStringProperty(shape, "contentsType");
        var topologySystemName = ScreenDataShapeTopologyReader.ExtractStringProperty(shape, "topologySystemName");
        var userFacingLabel = ScreenDataShapeTopologyReader.ExtractStringProperty(shape, "userFacingTopologyLabel");
        var tableRef = ManifestCanonicalProjection.ExtractTableRef(shape);
        var logicalTables = ManifestRelationIntentValidator.ExtractLogicalTables(topology);
        var physicalBound = !string.IsNullOrWhiteSpace(tableRef) &&
                            activePhysicalTableRefs.Contains(tableRef!);
        var progressStep = ComputeAuthoringProgressStep(shape, logicalTables.Count);

        return new ManifestListSummary(
            contentsType,
            topologySystemName,
            userFacingLabel,
            tableRef,
            physicalBound,
            progressStep,
            cloneMeta?.DraftOrigin,
            cloneMeta?.CloneMode,
            cloneMeta?.SourceActiveManifestId?.ToString(),
            logicalTables.Count);
    }

    public static bool MatchesFilter(ManifestListSummary summary, ManifestListFilter filter)
    {
        if (filter.ContentsTypes.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(summary.ContentsType) ||
                !filter.ContentsTypes.Contains(summary.ContentsType, StringComparer.Ordinal))
            {
                return false;
            }
        }

        if (filter.Physical == true && !summary.PhysicalBound)
            return false;

        if (summary.LogicalTableCount < filter.LogicalTablesMin)
            return false;

        return true;
    }

    public static IReadOnlyList<JsonElement> EnsureContentsTypeOnTopology(
        IReadOnlyList<JsonElement> topology,
        string contentsType)
    {
        var existing = ScreenDataShapeTopologyReader.FindScreenDataShapeEntry(topology);
        if (existing is null)
        {
            var initial = JsonSerializer.SerializeToElement(new
            {
                type = ManifestCanonicalProjection.ScreenDataShapeEntryType,
                contentsType,
            });
            return ManifestCanonicalProjection.MergeTopologyEntry(
                topology,
                ManifestCanonicalProjection.ScreenDataShapeEntryType,
                initial);
        }

        if (string.Equals(ScreenDataShapeTopologyReader.ExtractStringProperty(existing.Value, "contentsType"), contentsType, StringComparison.Ordinal))
            return topology.ToList();

        var updated = StampContentsType(existing.Value, contentsType);
        return ManifestCanonicalProjection.MergeTopologyEntry(
            topology,
            ManifestCanonicalProjection.ScreenDataShapeEntryType,
            updated);
    }

    private static JsonElement StampContentsType(JsonElement shapeEntry, string contentsType)
    {
        using var doc = JsonDocument.Parse(shapeEntry.GetRawText());
        var root = doc.RootElement;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, "contentsType", StringComparison.Ordinal)) continue;
                writer.WritePropertyName(prop.Name);
                prop.Value.WriteTo(writer);
            }
            writer.WriteString("contentsType", contentsType);
            writer.WriteEndObject();
        }
        stream.Position = 0;
        using var updated = JsonDocument.Parse(stream);
        return updated.RootElement.Clone();
    }

    private static string? ComputeAuthoringProgressStep(JsonElement shape, int logicalTableCount)
    {
        var topologySystemName = ScreenDataShapeTopologyReader.ExtractStringProperty(shape, "topologySystemName");
        if (string.IsNullOrWhiteSpace(topologySystemName))
            return null;

        if (logicalTableCount < 1)
            return "1";

        var tableRef = ManifestCanonicalProjection.ExtractTableRef(shape);
        var operationKinds = ManifestCanonicalProjection.ExtractScreenOperationKinds(new[] { shape });
        var hasOps = operationKinds.Count > 0;
        if (!string.IsNullOrWhiteSpace(tableRef) && hasOps)
            return "3";

        if (CountRelationIntents(shape) > 0)
            return "2.5";

        return "2";
    }

    private static int CountRelationIntents(JsonElement shape)
    {
        if (!shape.TryGetProperty("relationIntents", out var relEl) ||
            relEl.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var item in relEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            count++;
        }
        return count;
    }

    private static IReadOnlyList<string> ParseContentsTypeFilter(JsonElement payload)
    {
        if (!payload.TryGetProperty("contentsType", out var el))
            return Array.Empty<string>();

        if (el.ValueKind == JsonValueKind.String)
        {
            var single = el.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(single) ? Array.Empty<string>() : new[] { single! };
        }

        if (el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(item.GetString()))
            {
                list.Add(item.GetString()!.Trim());
            }
        }
        return list;
    }
}
