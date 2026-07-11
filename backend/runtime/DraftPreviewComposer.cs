using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Composes /draft-preview/preview responses from layout tensor rows, manifest screen_data_shape
/// topology intent (initialDataRows), and package-scoped component style designs.
/// Explicit failure — no silent fallback.
/// </summary>
public static class DraftPreviewComposer
{
    public record DraftPreviewSuccess(
        string LayoutId,
        string PreviewMode,
        string? RouteKey,
        string? PackageId,
        IReadOnlyList<object> LayoutNodes,
        IReadOnlyList<string> RootLayoutClassRefs,
        IReadOnlyDictionary<string, object> DesignByNodeId,
        string? ManifestId,
        string? ManifestStatus,
        string? TopologySystemName,
        IReadOnlyList<Dictionary<string, string>> InitialDataRows);

    public static async Task<(DraftPreviewSuccess? Success, ValidationError? Error, int StatusCode)>
        ComposeAsync(
            string layoutIdRaw,
            TopologyRepository topoRepo,
            ManifestRepository manifestRepo,
            UiTopologyRepository uiRepo,
            CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(layoutIdRaw) || !Guid.TryParse(layoutIdRaw, out var layoutId))
        {
            return (null, new ValidationError(
                "MALFORMED_LAYOUT_ID", "layoutId must be a non-empty valid UUID."), 422);
        }

        LayoutTensorContextDto? tensorContext;
        try
        {
            tensorContext = await uiRepo.ResolveLayoutTensorContextAsync(layoutId, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("LAYOUT_NODES_AMBIGUOUS_SELECTOR", StringComparison.Ordinal))
        {
            return (null, new ValidationError(
                "LAYOUT_NODES_AMBIGUOUS_SELECTOR",
                ex.Message), 422);
        }

        IReadOnlyList<LayoutNodeRecord> tensorRows;
        try
        {
            tensorRows = await topoRepo.LoadLayoutNodesAsync(layoutId, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("LAYOUT_NODES_AMBIGUOUS_SELECTOR", StringComparison.Ordinal))
        {
            return (null, new ValidationError(
                "LAYOUT_NODES_AMBIGUOUS_SELECTOR",
                ex.Message), 422);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("LAYOUT_SCHEMA_RECORDS_INVALID", StringComparison.Ordinal))
        {
            return (null, new ValidationError(
                "LAYOUT_SCHEMA_RECORDS_INVALID",
                ex.Message), 422);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("LAYOUT_SCHEMA_RUNTIME_INTERACTIONS_INVALID", StringComparison.Ordinal))
        {
            return (null, new ValidationError(
                "LAYOUT_SCHEMA_RUNTIME_INTERACTIONS_INVALID",
                ex.Message), 422);
        }

        if (tensorContext is null && tensorRows.Count == 0)
        {
            return (null, new ValidationError(
                "LAYOUT_NODES_NOT_FOUND",
                $"layout_id '{layoutId}' has no tensor rows in ui_topology_tensor. " +
                "Broken layout configuration — no fallback."), 422);
        }

        if (tensorRows.Count == 0)
        {
            return (null, new ValidationError(
                "LAYOUT_HAS_NO_NODES",
                $"layout_id '{layoutId}' has a tensor row but no placement nodes. " +
                "Apply a layout patch with at least one node in UI Builder."), 422);
        }

        IReadOnlyList<Dictionary<string, string>> initialDataRows = [];
        string? manifestId = null;
        string? manifestStatus = null;
        string? topologySystemName = null;

        if (!string.IsNullOrWhiteSpace(tensorContext?.RouteKey))
        {
            var (manifest, manifestError) = await DraftPreviewManifestResolver.ResolveDraftByTopologySystemNameAsync(
                manifestRepo,
                tensorContext.RouteKey,
                ct);
            if (manifestError is not null)
                return (null, manifestError, 422);

            if (manifest is not null)
            {
                manifestId = manifest.ManifestId.ToString();
                manifestStatus = manifest.Status;
                var shapeEntry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntry(manifest.Topology);
                if (shapeEntry is not null)
                {
                    if (shapeEntry.Value.TryGetProperty("topologySystemName", out var tsnEl) &&
                        tsnEl.ValueKind == JsonValueKind.String)
                    {
                        topologySystemName = tsnEl.GetString();
                    }
                    initialDataRows = ScreenDataShapeQueryEvaluator.ParseInitialDataRows(shapeEntry.Value);
                }
            }
        }

        IReadOnlyList<ComponentStyleDesignListItemDto> designs = [];
        if (tensorContext?.PackageId is Guid packageId)
        {
            designs = await uiRepo.ListComponentStyleDesignsAsync(packageId, ct);
        }

        var designByNodeId = BuildDesignByNodeId(designs);

        var orderedNodes = tensorRows
            .OrderBy(r => r.OrderIndex)
            .Select(r => (object)new
            {
                nodeId = r.NodeId,
                nodeKind = r.NodeKind,
                htmlTag = r.HtmlTag,
                componentKey = r.ComponentKey,
                componentId = r.ComponentId,
                componentKind = r.ComponentKind,
                parentNodeId = r.ParentNodeId,
                slotKey = r.SlotKey,
                orderIndex = r.OrderIndex,
                x = r.X,
                y = r.Y,
                width = r.Width,
                height = r.Height,
                widthMode = r.WidthMode,
                heightMode = r.HeightMode,
                layoutClassRefs = r.LayoutClassRefs,
                design = designByNodeId.TryGetValue(r.NodeId, out var nodeDesign) ? nodeDesign : null,
            })
            .ToList();

        var previewMode = initialDataRows.Count > 0
            ? "layout_and_topology_intent"
            : "layout_only";

        return (new DraftPreviewSuccess(
            LayoutId: layoutId.ToString(),
            PreviewMode: previewMode,
            RouteKey: tensorContext?.RouteKey,
            PackageId: tensorContext?.PackageId.ToString(),
            LayoutNodes: orderedNodes,
            RootLayoutClassRefs: tensorContext?.RootLayoutClassRefs ?? [],
            DesignByNodeId: designByNodeId,
            ManifestId: manifestId,
            ManifestStatus: manifestStatus,
            TopologySystemName: topologySystemName,
            InitialDataRows: initialDataRows), null, 200);
    }

    internal static Dictionary<string, object> BuildDesignByNodeId(
        IReadOnlyList<ComponentStyleDesignListItemDto> designs)
    {
        var map = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var design in designs)
        {
            var nodeId = design.LayoutNodeId?.Trim();
            if (string.IsNullOrEmpty(nodeId)) continue;
            map[nodeId] = new
            {
                designId = design.DesignId,
                inlineText = design.InlineText,
                linkHref = design.LinkHref,
                linkTarget = design.LinkTarget,
                cssTokenRefs = design.CssTokenRefs,
                responsiveTokenRefs = design.ResponsiveTokenRefs,
                classname = design.Classname,
                tailwind = design.Tailwind,
                hasDesignTmpDraft = design.HasDesignTmpDraft,
            };
        }
        return map;
    }
}
