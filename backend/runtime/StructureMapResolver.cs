using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves a full structure map from the repository using the attractor result.
/// Returns a RuntimeWorkingShape populated with structure map fields.
/// When layout_id is set, parses layout nodes from layout_patch_json.nodes[] and validates them.
/// Missing or invalid nodes when layout_id is set yields an explicit validation error —
/// no silent fallback to flat componentIds rendering.
/// </summary>
public class StructureMapResolver
{
    // SSOT: admin-console-workflow-ssot.yaml layout_editor.node_kind_contract.structural_html.allowlist
    private static readonly HashSet<string> StructuralHtmlTagAllowlist = new(StringComparer.Ordinal)
    {
        // block
        "div", "section", "article", "aside", "header", "footer", "main", "nav",
        // heading
        "h1", "h2", "h3", "h4", "h5", "h6",
        // text
        "p", "span", "strong", "em", "blockquote", "pre", "code",
        // link
        "a",
        // form
        "form", "fieldset", "legend", "label", "button", "input", "textarea", "select", "option",
        // media
        "img", "picture", "figure", "figcaption", "video", "audio",
        // list
        "ul", "ol", "li", "dl", "dt", "dd",
        // table
        "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption",
    };

    private readonly TopologyRepository _topologyRepository;

    public StructureMapResolver(TopologyRepository topologyRepository)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    /// <summary>
    /// Loads the full structure map and constructs the initial RuntimeWorkingShape.
    /// Throws if the structure map record cannot be loaded by its ID.
    /// When layout_id is set, parses layout_patch_json.nodes[] and validates the result.
    /// Returns LAYOUT_NODES_NOT_FOUND when no nodes are parsed (empty layout_patch_json).
    /// Returns LAYOUT_PATCH_DUPLICATE_NODE_ID, LAYOUT_PATCH_MISSING_PARENT,
    /// LAYOUT_PATCH_PARENT_CYCLE, or LAYOUT_PATCH_STRUCTURAL_HTML_TAG_UNKNOWN on invalid nodes.
    /// </summary>
    public async Task<RuntimeWorkingShape> Resolve(AttractorResult attractor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(attractor);

        var record = await _topologyRepository.LoadStructureMapAsync(attractor.StructureMapId, ct);

        if (record is null)
            throw new InvalidOperationException(
                $"Structure map '{attractor.StructureMapId}' not found. Broken reference — no fallback.");

        IReadOnlyList<LayoutNode>? layoutNodes = null;
        IReadOnlyList<ValidationError>? layoutErrors = null;

        if (record.LayoutId.HasValue)
        {
            IReadOnlyList<LayoutNodeRecord> parsedNodes;
            try
            {
                parsedNodes = await _topologyRepository.LoadLayoutNodesAsync(record.LayoutId.Value, ct);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("LAYOUT_NODES_AMBIGUOUS_SELECTOR"))
            {
                layoutErrors =
                [
                    new ValidationError("LAYOUT_NODES_AMBIGUOUS_SELECTOR", ex.Message)
                ];
                goto buildShape;
            }

            if (parsedNodes.Count == 0)
            {
                layoutErrors =
                [
                    new ValidationError(
                        "LAYOUT_NODES_NOT_FOUND",
                        $"layout_id '{record.LayoutId.Value}' has no nodes in layout_patch_json. " +
                        "Broken layout configuration — no fallback to flat component rendering.")
                ];
            }
            else
            {
                var validationErrors = ValidateLayoutNodes(parsedNodes);
                if (validationErrors is not null)
                {
                    layoutErrors = validationErrors;
                }
                else
                {
                    // componentId comes from nodes[].componentId — not positional from structure_maps.component_ids.
                    layoutNodes = parsedNodes.Select(row => new LayoutNode(
                        NodeId: row.NodeId,
                        NodeKind: row.NodeKind,
                        HtmlTag: row.HtmlTag,
                        ComponentKey: row.ComponentKey,
                        ComponentId: row.ComponentId,
                        ParentNodeId: row.ParentNodeId,
                        SlotKey: row.SlotKey,
                        OrderIndex: row.OrderIndex,
                        X: row.X,
                        Y: row.Y,
                        Width: row.Width,
                        Height: row.Height,
                        LayoutClassRefs: row.LayoutClassRefs,
                        ComponentKind: row.ComponentKind,
                        RuntimeDispatchAction: row.RuntimeDispatchAction,
                        WiringId: row.WiringId,
                        WiringKey: row.WiringKey,
                        WiringKind: row.WiringKind,
                        TargetSurface: row.TargetSurface,
                        TargetRef: row.TargetRef,
                        PropsJson: row.PropsJson,
                        StateJson: row.StateJson,
                        PropBindings: row.PropBindingsJson != null
                            ? JsonSerializer.Deserialize<JsonElement>(row.PropBindingsJson)
                            : null
                    )).ToList();
                }
            }
        }

        buildShape:

        return new RuntimeWorkingShape(
            Vector: null,
            StructureMapId: record.StructureMapId,
            PackageId: record.PackageId,
            SchemaId: record.SchemaId,
            ComponentIds: record.ComponentIds,
            PackageDef: null,
            SchemaDef: null,
            ResolvedData: null,
            Errors: layoutErrors,
            StructureMapStatePolicyJson: record.StatePolicyJson,
            LayoutId: record.LayoutId?.ToString(),
            LayoutNodes: layoutNodes
        );
    }

    /// <summary>
    /// Validates the parsed layout nodes. Returns the first validation error found, or null if valid.
    /// Checks: duplicate nodeIds, missing parents, parent cycles, structural_html tag allowlist.
    /// </summary>
    private static IReadOnlyList<ValidationError>? ValidateLayoutNodes(IReadOnlyList<LayoutNodeRecord> nodes)
    {
        var nodeIdSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            if (!nodeIdSet.Add(node.NodeId))
            {
                return
                [
                    new ValidationError(
                        "LAYOUT_PATCH_DUPLICATE_NODE_ID",
                        $"Duplicate nodeId '{node.NodeId}' in layout_patch_json.nodes[]. Each node must have a unique ID.")
                ];
            }
        }

        foreach (var node in nodes)
        {
            if (node.ParentNodeId is not null && !nodeIdSet.Contains(node.ParentNodeId))
            {
                return
                [
                    new ValidationError(
                        "LAYOUT_PATCH_MISSING_PARENT",
                        $"Node '{node.NodeId}' references parentNodeId '{node.ParentNodeId}' which does not exist in layout_patch_json.nodes[].")
                ];
            }
        }

        // Cycle detection via ancestor-chain traversal.
        var parentMap = nodes
            .Where(n => n.ParentNodeId is not null)
            .ToDictionary(n => n.NodeId, n => n.ParentNodeId!, StringComparer.Ordinal);
        var globalVisited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            if (globalVisited.Contains(node.NodeId)) continue;

            var path = new HashSet<string>(StringComparer.Ordinal);
            var current = node.NodeId;

            while (current is not null && !globalVisited.Contains(current))
            {
                if (!path.Add(current))
                {
                    return
                    [
                        new ValidationError(
                            "LAYOUT_PATCH_PARENT_CYCLE",
                            $"Cycle detected in parentNodeId references involving nodeId '{current}'.")
                    ];
                }
                parentMap.TryGetValue(current, out current);
            }

            foreach (var id in path) globalVisited.Add(id);
        }

        foreach (var node in nodes)
        {
            if (node.NodeKind is "structural_html")
            {
                if (string.IsNullOrWhiteSpace(node.HtmlTag))
                {
                    return
                    [
                        new ValidationError(
                            "LAYOUT_PATCH_STRUCTURAL_HTML_TAG_REQUIRED",
                            $"Node '{node.NodeId}' is nodeKind='structural_html' but has no htmlTag.")
                    ];
                }

                if (!StructuralHtmlTagAllowlist.Contains(node.HtmlTag))
                {
                    return
                    [
                        new ValidationError(
                            "LAYOUT_PATCH_STRUCTURAL_HTML_TAG_UNKNOWN",
                            $"Node '{node.NodeId}' has htmlTag '{node.HtmlTag}' which is not in the SSOT structural HTML tag allowlist.")
                    ];
                }
            }
        }

        return null;
    }
}
