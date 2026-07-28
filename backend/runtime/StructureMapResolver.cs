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

    // SSOT: admin-console-workflow-ssot.yaml layout_node_props_contract.component_array_prop_capabilities
    // Must stay in sync with frontend/runtime/propBindingResolver.ts COMPONENT_ARRAY_PROP_CAPABILITIES.
    internal static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ComponentArrayPropCapabilities =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["data_display/table"]              = new HashSet<string>(StringComparer.Ordinal) { "rows", "columns" },
            ["data_display/data_grid"]          = new HashSet<string>(StringComparer.Ordinal) { "rows", "columns" },
            ["data_display/list"]               = new HashSet<string>(StringComparer.Ordinal) { "rows", "items" },
            ["data_display/tree"]               = new HashSet<string>(StringComparer.Ordinal) { "nodes", "items" },
            ["data_display/json"]               = new HashSet<string>(StringComparer.Ordinal) { "data" },
            ["display/card_list"]               = new HashSet<string>(StringComparer.Ordinal) { "items" },
            ["disclosure/accordion"]            = new HashSet<string>(StringComparer.Ordinal) { "items" },
            ["form_input/select"]               = new HashSet<string>(StringComparer.Ordinal) { "options" },
            ["form_input/checkbox"]             = new HashSet<string>(StringComparer.Ordinal) { "options" },
            ["form_input/radio_group"]          = new HashSet<string>(StringComparer.Ordinal) { "options" },
            ["form_input/checkbox_group"]       = new HashSet<string>(StringComparer.Ordinal) { "options" },
            // "value" is a scalar, not an array, despite this dict's name -- ComponentArrayPropCapabilities
            // is the universal propBindings capability whitelist (see the validator below), not
            // array-specific. Added so an existing read/get action's emission.data can pre-fill a
            // search_input.alias field's displayed value (admin-write-surface-selection-context-and-
            // mode-composition-gap Bundle, .agent/tasks/todo.md) -- the same generic propBindings
            // mechanism already used for table/card_list/json_viewer, extended to one more component
            // kind, not a new mechanism.
            ["form_input/search_input"]         = new HashSet<string>(StringComparer.Ordinal) { "value" },
            ["table_op/faceted_filter_bar"]     = new HashSet<string>(StringComparer.Ordinal) { "filters" },
            ["table_op/column_filter"]          = new HashSet<string>(StringComparer.Ordinal) { "options" },
            ["table_op/column_visibility_editor"] = new HashSet<string>(StringComparer.Ordinal) { "columns" },
            ["table_op/sort_control"]           = new HashSet<string>(StringComparer.Ordinal) { "columns" },
            ["table_op/group_by_control"]       = new HashSet<string>(StringComparer.Ordinal) { "columns" },
            ["table_op/bulk_action_panel"]      = new HashSet<string>(StringComparer.Ordinal) { "actions" },
            ["table_op/virtualized_data_table"] = new HashSet<string>(StringComparer.Ordinal) { "rows", "columns" },
            ["inline_edit/audit_diff_drawer"]     = new HashSet<string>(StringComparer.Ordinal) { "entries" },
            ["calc_topology/aggregation_preview_table"] = new HashSet<string>(StringComparer.Ordinal) { "data" },
            ["calc_topology/hub_statistics_panel"] = new HashSet<string>(StringComparer.Ordinal) { "data" },
        };

    // Canonical hub_relations navigation sequence propBinding source — the ONLY recognized
    // source outside the "emission.data"/"emission.data." family. Never
    // emission.recommendNavigationProjection (SQL Attention / attention_recommendation_tab
    // stays out of reach of propBindings — docs/framework-core.yaml runtime_route_attention_boundary).
    // Must stay in sync with frontend/runtime/propBindingResolver.ts EMISSION_NAVIGATION_SEQUENCE_SOURCE.
    internal const string EmissionNavigationSequenceSource = "emission.navigationSequence";

    // SSOT: admin-console-workflow-ssot.yaml layout_node_props_contract.propBindings.transform.allowlist
    // Must stay in sync with frontend/runtime/propBindingResolver.ts ALLOWED_PROP_BINDING_TRANSFORMS.
    internal static readonly IReadOnlySet<string> AllowedPropBindingTransforms =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "activeColumnsToTableColumns",
            "rowsToOptions",
            "navigationLinksToCardItems",
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
        JsonElement? calculationBindings = null;

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
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("LAYOUT_SCHEMA_RECORDS_INVALID"))
            {
                // A present-but-malformed layout_schema_json.records[] is a real authoring
                // defect, never equivalent to "no records[]" — it must never be silently
                // dropped to the tensor-only path or partially composed by skipping bad entries.
                layoutErrors =
                [
                    new ValidationError("LAYOUT_SCHEMA_RECORDS_INVALID", ex.Message)
                ];
                goto buildShape;
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("LAYOUT_SCHEMA_RUNTIME_INTERACTIONS_INVALID"))
            {
                // A malformed/unattributable tensor runtimeInteractions shape is a real
                // authoring defect — never silently skipped during the schema-authority merge.
                layoutErrors =
                [
                    new ValidationError("LAYOUT_SCHEMA_RUNTIME_INTERACTIONS_INVALID", ex.Message)
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
                    layoutNodes = parsedNodes.Select(ToLayoutNode).ToList();
                }
            }

            // Load calculationBindings regardless of node validation outcome — they are forwarded raw.
            var calcBindingsJson = await _topologyRepository.LoadLayoutCalcBindingsJsonAsync(record.LayoutId.Value, ct);
            if (!string.IsNullOrWhiteSpace(calcBindingsJson))
            {
                try
                {
                    calculationBindings = JsonSerializer.Deserialize<JsonElement>(calcBindingsJson);
                }
                catch (JsonException ex)
                {
                    // Malformed JSON is an explicit failure — silent omit is prohibited.
                    // Broken calculationBindings authoring must surface in Emission.Errors.
                    var calcError = new ValidationError(
                        "CALC_BINDINGS_JSON_INVALID",
                        $"layout_id '{record.LayoutId.Value}' has malformed calculationBindings JSON in layout_patch_json. " +
                        $"Frontend calculation bindings cannot be applied. Parse error: {ex.Message}");
                    var merged = new List<ValidationError>(layoutErrors ?? []) { calcError };
                    layoutErrors = merged;
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
            LayoutNodes: layoutNodes,
            CalculationBindings: calculationBindings
        );
    }

    /// <summary>
    /// Converts a repository-loaded layout node record to the Emission-facing LayoutNode shape.
    /// Shared by StructureMapResolver (structure-map-driven layout resolution) and
    /// ManifestDispatcher (manifest.topology[ui_projection].layoutId-driven resolution) so both
    /// paths stay identical rather than duplicating the field mapping.
    /// </summary>
    internal static LayoutNode ToLayoutNode(LayoutNodeRecord row) => new(
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
            : null,
        RuntimeInteractions: row.RuntimeInteractionsJson != null
            ? JsonSerializer.Deserialize<JsonElement>(row.RuntimeInteractionsJson)
            : null,
        DispatchPayloadFromByTrigger: row.DispatchPayloadFromByTriggerJson != null
            ? JsonSerializer.Deserialize<JsonElement>(row.DispatchPayloadFromByTriggerJson)
            : null,
        RecordType: row.RecordType,
        Label: row.Label,
        KnownGapRefs: row.KnownGapRefs
    );

    /// <summary>
    /// Validates the parsed layout nodes. Returns the first validation error found, or null if valid.
    /// Checks: duplicate nodeIds, missing parents, parent cycles, structural_html tag allowlist.
    /// Internal (not private): reused by ManifestDispatcher's ui_projection.layoutId resolution
    /// path so both layout-node sources apply the same structural validation.
    /// </summary>
    internal static IReadOnlyList<ValidationError>? ValidateLayoutNodes(IReadOnlyList<LayoutNodeRecord> nodes)
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

        // propBindings validate_contract (SSOT: admin-console-workflow-ssot.yaml layout_node_props_contract)
        foreach (var node in nodes)
        {
            if (node.PropBindingsJson is null) continue;

            JsonElement pb;
            try
            {
                pb = JsonSerializer.Deserialize<JsonElement>(node.PropBindingsJson);
            }
            catch (JsonException)
            {
                return
                [
                    new ValidationError(
                        "LAYOUT_NODE_PROP_BINDING_INVALID",
                        $"Node '{node.NodeId}': propBindings is not valid JSON.")
                ];
            }

            if (pb.ValueKind != JsonValueKind.Object)
            {
                return
                [
                    new ValidationError(
                        "LAYOUT_NODE_PROP_BINDING_INVALID",
                        $"Node '{node.NodeId}': propBindings must be a JSON object.")
                ];
            }

            // component kind required for capability check when propBindings present
            var componentKind = node.ComponentKind;
            if (string.IsNullOrWhiteSpace(componentKind))
            {
                return
                [
                    new ValidationError(
                        "LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT",
                        $"Node '{node.NodeId}': propBindings present but componentKind is missing.")
                ];
            }

            if (!ComponentArrayPropCapabilities.TryGetValue(componentKind, out var acceptedProps))
            {
                return
                [
                    new ValidationError(
                        "LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT",
                        $"Node '{node.NodeId}': component kind '{componentKind}' does not accept prop bindings.")
                ];
            }

            foreach (var bindingProp in pb.EnumerateObject())
            {
                var propName = bindingProp.Name;

                if (!acceptedProps.Contains(propName))
                {
                    return
                    [
                        new ValidationError(
                            "LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_PROP",
                            $"Node '{node.NodeId}': prop '{propName}' is not an accepted propBinding target for '{componentKind}'.")
                    ];
                }

                if (bindingProp.Value.ValueKind != JsonValueKind.Object)
                {
                    return
                    [
                        new ValidationError(
                            "LAYOUT_NODE_PROP_BINDING_INVALID_ENTRY",
                            $"Node '{node.NodeId}': binding for prop '{propName}' must be an object.")
                    ];
                }

                // source: required, must be "emission.data", start with "emission.data.", or be
                // exactly EmissionNavigationSequenceSource.
                if (!bindingProp.Value.TryGetProperty("source", out var sourceProp) ||
                    sourceProp.ValueKind != JsonValueKind.String)
                {
                    return
                    [
                        new ValidationError(
                            "LAYOUT_NODE_PROP_BINDING_INVALID_SOURCE",
                            $"Node '{node.NodeId}', prop '{propName}': source must be a string.")
                    ];
                }
                var source = sourceProp.GetString()!;
                if (!(source == "emission.data" ||
                      source.StartsWith("emission.data.", StringComparison.Ordinal) ||
                      source == EmissionNavigationSequenceSource))
                {
                    return
                    [
                        new ValidationError(
                            "LAYOUT_NODE_PROP_BINDING_INVALID_SOURCE",
                            $"Node '{node.NodeId}', prop '{propName}': source \"{source}\" must be \"emission.data\", start with \"emission.data.\", or be \"{EmissionNavigationSequenceSource}\".")
                    ];
                }

                // transform: optional, must be in allowlist when present
                if (bindingProp.Value.TryGetProperty("transform", out var transformProp))
                {
                    if (transformProp.ValueKind != JsonValueKind.String)
                    {
                        return
                        [
                            new ValidationError(
                                "LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM",
                                $"Node '{node.NodeId}', prop '{propName}': transform must be a string.")
                        ];
                    }
                    var transform = transformProp.GetString()!;
                    if (!AllowedPropBindingTransforms.Contains(transform))
                    {
                        return
                        [
                            new ValidationError(
                                "LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM",
                                $"Node '{node.NodeId}', prop '{propName}': transform \"{transform}\" is not in the allowlist.")
                        ];
                    }
                }

                // path fields: keyPath, labelPath, valuePath, childrenPath — must be string when present
                foreach (var pathField in new[] { "keyPath", "labelPath", "valuePath", "childrenPath" })
                {
                    if (bindingProp.Value.TryGetProperty(pathField, out var pathProp) &&
                        pathProp.ValueKind != JsonValueKind.String)
                    {
                        return
                        [
                            new ValidationError(
                                "LAYOUT_NODE_PROP_BINDING_INVALID_PATH",
                                $"Node '{node.NodeId}', prop '{propName}': {pathField} must be a string.")
                        ];
                    }
                }
            }
        }

        return null;
    }
}
