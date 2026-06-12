using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies that backend propBindings capability table and transform allowlist match
/// the SSOT canonical values defined in admin-console-workflow-ssot.yaml.
/// Also verifies that ValidateLayoutNodes enforces propBindings validate_contract
/// (source prefix, capability, transform allowlist, path field shape).
///
/// Frontend equivalent: frontend/tests/propBindingContractSync.test.ts
/// </summary>
public class PropBindingContractSyncTests
{
    // ── SSOT canonical values ────────────────────────────────────────────────
    // Canonical source: admin-console-workflow-ssot.yaml layout_node_props_contract
    // Must match frontend/runtime/propBindingResolver.ts COMPONENT_ARRAY_PROP_CAPABILITIES

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> SsotCapabilities =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["data_display/table"]               = new HashSet<string> { "rows", "columns" },
            ["data_display/data_grid"]           = new HashSet<string> { "rows", "columns" },
            ["data_display/list"]                = new HashSet<string> { "rows", "items" },
            ["data_display/tree"]                = new HashSet<string> { "nodes", "items" },
            ["data_display/json"]                = new HashSet<string> { "data" },
            ["display/card_list"]                = new HashSet<string> { "items" },
            ["disclosure/accordion"]             = new HashSet<string> { "items" },
            ["form_input/select"]                = new HashSet<string> { "options" },
            ["form_input/checkbox"]              = new HashSet<string> { "options" },
            ["form_input/radio_group"]           = new HashSet<string> { "options" },
            ["form_input/checkbox_group"]        = new HashSet<string> { "options" },
            ["table_op/faceted_filter_bar"]      = new HashSet<string> { "filters" },
            ["table_op/column_filter"]           = new HashSet<string> { "options" },
            ["table_op/column_visibility_editor"] = new HashSet<string> { "columns" },
            ["table_op/sort_control"]            = new HashSet<string> { "columns" },
            ["table_op/group_by_control"]        = new HashSet<string> { "columns" },
            ["table_op/bulk_action_panel"]       = new HashSet<string> { "actions" },
            ["table_op/virtualized_data_table"]  = new HashSet<string> { "rows", "columns" },
            ["inline_edit/audit_diff_drawer"]     = new HashSet<string> { "entries" },
            ["calc_topology/aggregation_preview_table"] = new HashSet<string> { "data" },
            ["calc_topology/hub_statistics_panel"] = new HashSet<string> { "data" },
        };

    private static readonly IReadOnlySet<string> SsotTransforms =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "activeColumnsToTableColumns",
            "rowsToOptions",
        };

    // ── Capability table sync ────────────────────────────────────────────────

    [Fact]
    public void ComponentPropBindingCapabilities_MatchesSsotCanonicalTable()
    {
        var impl = StructureMapResolver.ComponentArrayPropCapabilities;

        var ssotKinds = SsotCapabilities.Keys.OrderBy(x => x).ToList();
        var implKinds = impl.Keys.OrderBy(x => x).ToList();
        Assert.Equal(ssotKinds, implKinds);

        foreach (var kind in ssotKinds)
        {
            var ssotProps = SsotCapabilities[kind].OrderBy(x => x).ToList();
            var implProps = impl[kind].OrderBy(x => x).ToList();
            Assert.Equal(ssotProps, implProps);
        }
    }

    [Fact]
    public void AllowedPropBindingTransforms_MatchesSsotCanonicalAllowlist()
    {
        var impl = StructureMapResolver.AllowedPropBindingTransforms;
        var ssotSorted = SsotTransforms.OrderBy(x => x).ToList();
        var implSorted = impl.OrderBy(x => x).ToList();
        Assert.Equal(ssotSorted, implSorted);
    }

    // ── ValidateLayoutNodes: propBindings validate_contract ──────────────────

    [Fact]
    public void ValidateLayoutNodes_AcceptsValidPropBindings()
    {
        var nodes = MakeNodes(
            componentKind: "data_display/table",
            propBindingsJson: """{"rows":{"source":"emission.data.rows"},"columns":{"source":"emission.data.activeColumns","transform":"activeColumnsToTableColumns"}}"""
        );
        var errors = InvokeValidate(nodes);
        Assert.Null(errors);
    }

    [Fact]
    public void ValidateLayoutNodes_AcceptsJsonViewerFullEmissionDataBinding()
    {
        var nodes = MakeNodes(
            componentKind: "data_display/json",
            propBindingsJson: """{"data":{"source":"emission.data"}}"""
        );
        var errors = InvokeValidate(nodes);
        Assert.Null(errors);
    }

    [Fact]
    public void ValidateLayoutNodes_RejectsNonObjectPropBindings()
    {
        var nodes = MakeNodes(
            componentKind: "data_display/table",
            propBindingsJson: """["not","an","object"]"""
        );
        var errors = InvokeValidate(nodes);
        Assert.NotNull(errors);
        Assert.Contains(errors!, e => e.Code == "LAYOUT_NODE_PROP_BINDING_INVALID");
    }

    [Fact]
    public void ValidateLayoutNodes_RejectsNonCapabilityComponent()
    {
        var nodes = MakeNodes(
            componentKind: "action/button",
            propBindingsJson: """{"items":{"source":"emission.data.rows"}}"""
        );
        var errors = InvokeValidate(nodes);
        Assert.NotNull(errors);
        Assert.Contains(errors!, e => e.Code == "LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT");
    }

    [Fact]
    public void ValidateLayoutNodes_RejectsUnknownPropForCapabilityComponent()
    {
        // data_display/table accepts "rows" and "columns" only, not "items"
        var nodes = MakeNodes(
            componentKind: "data_display/table",
            propBindingsJson: """{"items":{"source":"emission.data.rows"}}"""
        );
        var errors = InvokeValidate(nodes);
        Assert.NotNull(errors);
        Assert.Contains(errors!, e => e.Code == "LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_PROP");
    }

    [Fact]
    public void ValidateLayoutNodes_RejectsSourceWithoutEmissionDataPrefix()
    {
        var nodes = MakeNodes(
            componentKind: "data_display/table",
            propBindingsJson: """{"rows":{"source":"data.rows"}}"""
        );
        var errors = InvokeValidate(nodes);
        Assert.NotNull(errors);
        Assert.Contains(errors!, e => e.Code == "LAYOUT_NODE_PROP_BINDING_INVALID_SOURCE");
    }

    [Fact]
    public void ValidateLayoutNodes_RejectsTransformNotInAllowlist()
    {
        var nodes = MakeNodes(
            componentKind: "data_display/table",
            propBindingsJson: """{"rows":{"source":"emission.data.rows","transform":"evilEval"}}"""
        );
        var errors = InvokeValidate(nodes);
        Assert.NotNull(errors);
        Assert.Contains(errors!, e => e.Code == "LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM");
    }

    [Fact]
    public void ValidateLayoutNodes_RejectsNonStringLabelPath()
    {
        var nodes = MakeNodes(
            componentKind: "form_input/select",
            propBindingsJson: """{"options":{"source":"emission.data.rows","labelPath":123}}"""
        );
        var errors = InvokeValidate(nodes);
        Assert.NotNull(errors);
        Assert.Contains(errors!, e => e.Code == "LAYOUT_NODE_PROP_BINDING_INVALID_PATH");
    }

    [Fact]
    public void ValidateLayoutNodes_RejectsMissingComponentKindWithPropBindings()
    {
        // catalog_component without componentKind but with propBindings
        var nodes = new List<LayoutNodeRecord>
        {
            new(
                NodeId: "n-pb-nokind",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: "some-key",
                ComponentId: "comp-nokind",
                ParentNodeId: null,
                SlotKey: null,
                OrderIndex: 0,
                X: 0, Y: 0, Width: 0, Height: 0,
                LayoutClassRefs: null,
                ComponentKind: null,
                RuntimeDispatchAction: null,
                PropBindingsJson: """{"items":{"source":"emission.data.rows"}}"""
            )
        };
        var errors = InvokeValidate(nodes);
        Assert.NotNull(errors);
        Assert.Contains(errors!, e => e.Code == "LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT");
    }

    [Fact]
    public void ValidateLayoutNodes_AcceptsNodeWithoutPropBindings()
    {
        var nodes = MakeNodes(componentKind: "action/button", propBindingsJson: null);
        var errors = InvokeValidate(nodes);
        Assert.Null(errors);
    }

    [Fact]
    public void ValidateLayoutNodes_AcceptsValidRowsToOptionsWithPathFields()
    {
        var nodes = MakeNodes(
            componentKind: "form_input/select",
            propBindingsJson: """{"options":{"source":"emission.data.rows","transform":"rowsToOptions","labelPath":"name","valuePath":"id","keyPath":"id"}}"""
        );
        var errors = InvokeValidate(nodes);
        Assert.Null(errors);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<LayoutNodeRecord> MakeNodes(string? componentKind, string? propBindingsJson)
    {
        return
        [
            new LayoutNodeRecord(
                NodeId: "n-pb-test",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: "test-key",
                ComponentId: "comp-pb-test",
                ParentNodeId: null,
                SlotKey: null,
                OrderIndex: 0,
                X: 0, Y: 0, Width: 0, Height: 0,
                LayoutClassRefs: null,
                ComponentKind: componentKind,
                RuntimeDispatchAction: null,
                PropBindingsJson: propBindingsJson
            )
        ];
    }

    private static IReadOnlyList<ValidationError>? InvokeValidate(IReadOnlyList<LayoutNodeRecord> nodes)
    {
        // ValidateLayoutNodes is private — use reflection to access it for testing.
        var method = typeof(StructureMapResolver)
            .GetMethod("ValidateLayoutNodes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (IReadOnlyList<ValidationError>?)method!.Invoke(null, [nodes]);
    }
}
