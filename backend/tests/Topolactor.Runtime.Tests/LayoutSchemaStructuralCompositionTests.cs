using Topolactor.Repository;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies LayoutSchemaTensorComposer — the layout-schema structural authority composition
/// path (docs/design/runtime-orchestration-ssot.yaml ui_projection_render_reachability_contract
/// layout_schema_structural_render_contract):
/// - components_layout_design.layout_schema_json.records[] is read as the structural authority.
/// - Category/Section/Form/Workflow/Validation record types become "structural_node" entries
///   that NEVER receive a componentId/componentKind.
/// - Field/Action/Table/WorkflowStep record types become "catalog_component" entries whose
///   componentId/componentKind are resolved via the caller-supplied ui_component_registry lookup
///   dictionaries (never invented, never left as a silent fallback).
/// - Unresolved record types become "unresolved_gap" entries that always carry their authored
///   knownGapRefs and never resolve to a componentId/componentKind.
/// - Tensor nodes' runtimeInteractions (keyed by sourceActionKey per entry, at the FORM level) are
///   merged onto the matching catalog_component leaf by leaf key == sourceActionKey.
/// - NodeId collisions (the same authored key reused in two branches — a real authoring
///   possibility, since a record's key is only guaranteed unique within its own branch) are
///   disambiguated by parent-scoping rather than silently colliding.
/// - A record's parentKey that does not match any record in the tree (the translator's implicit/
///   virtual $.root, never itself a record) resolves to a null (root) ParentNodeId.
/// - Absent/empty records[] (NoRecords) is never conflated with a present-but-malformed
///   records[] shape (Invalid) — the latter must surface as an explicit failure, never a
///   silent partial-tree or tensor-only fallback.
/// </summary>
public class LayoutSchemaStructuralCompositionTests
{
    private const string CategoryRecordsJson = """
    {
      "records": [
        {"type":"topology_ui_seed_record","parentKey":"implicit_virtual_root","record":{"recordType":"topology_ui_category","key":"cat1","label":"Category One"}},
        {"type":"topology_ui_seed_record","parentKey":"cat1","record":{"recordType":"topology_ui_section","key":"sec1","label":"Section One"}},
        {"type":"topology_ui_seed_record","parentKey":"sec1","record":{"recordType":"topology_ui_field","key":"field1","label":"Field One","control":"form_input/select"}},
        {"type":"topology_ui_seed_record","parentKey":"sec1","record":{"recordType":"topology_ui_action","key":"action1","label":"Action One"}},
        {"type":"topology_ui_seed_record","parentKey":"sec1","record":{"recordType":"topology_ui_validation","key":"val1","label":"Validation One"}}
      ]
    }
    """;

    private static IReadOnlyList<LayoutSchemaTensorComposer.SchemaRecordRow> ParseValidRows(string json)
    {
        var result = LayoutSchemaTensorComposer.ParseRecords(json);
        var valid = Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Valid>(result);
        return valid.Rows;
    }

    [Fact]
    public void ParseRecords_AbsentOrEmptyRecords_ReturnsNoRecords_NoSchemaDrivenComposition()
    {
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.NoRecords>(LayoutSchemaTensorComposer.ParseRecords(null));
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.NoRecords>(LayoutSchemaTensorComposer.ParseRecords(""));
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.NoRecords>(LayoutSchemaTensorComposer.ParseRecords("{}"));
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.NoRecords>(LayoutSchemaTensorComposer.ParseRecords("{\"records\":[]}"));
    }

    [Fact]
    public void ParseRecords_MalformedTopLevelJson_ReturnsInvalid_NeverTreatedAsAbsent()
    {
        var result = LayoutSchemaTensorComposer.ParseRecords("not json");
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(result);
    }

    [Theory]
    [InlineData("{\"records\":\"not-an-array\"}")]
    [InlineData("{\"records\":{}}")]
    [InlineData("{\"records\":42}")]
    public void ParseRecords_RecordsKeyPresentButNotAnArray_ReturnsInvalid_NeverTreatedAsAbsent(string json)
    {
        var result = LayoutSchemaTensorComposer.ParseRecords(json);
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(result);
    }

    [Fact]
    public void ParseRecords_EntryNotAnObject_ReturnsInvalid_NeverSkipped()
    {
        const string json = """{"records":["not-an-object"]}""";
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_EntryMissingRecordObject_ReturnsInvalid_NeverSkipped()
    {
        const string json = """{"records":[{"type":"topology_ui_seed_record","parentKey":null}]}""";
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_EntryMissingRecordType_ReturnsInvalid_NeverSkipped()
    {
        const string json = """{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"key":"f1"}}]}""";
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_EntryMissingKey_ReturnsInvalid_NeverSkipped()
    {
        const string json = """{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field"}}]}""";
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_UnrecognizedRecordType_ReturnsInvalid_NeverSkipped()
    {
        const string json = """{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_unknown_thing","key":"x"}}]}""";
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_OneInvalidEntryAmongOtherwiseValidEntries_RejectsTheWholeList_NoPartialTree()
    {
        // Two well-formed entries plus one malformed entry — the WHOLE list must be rejected,
        // never a partial tree built by silently dropping just the bad entry.
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_category","key":"cat1"}},
            {"type":"topology_ui_seed_record","parentKey":"cat1","record":{"recordType":"topology_ui_unknown_thing","key":"bad"}},
            {"type":"topology_ui_seed_record","parentKey":"cat1","record":{"recordType":"topology_ui_section","key":"sec1"}}
          ]
        }
        """;
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_CategorySectionFormRecords_EmitStructuralNodes_NotCatalogComponents()
    {
        var records = ParseValidRows(CategoryRecordsJson);
        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            interactionsBySourceActionKey: new Dictionary<string, string>(),
            componentKeyToId: new Dictionary<string, string>(),
            componentIdToKind: new Dictionary<string, string>());

        var category = Assert.Single(composed, n => n.NodeId == "cat1");
        Assert.Equal("structural_node", category.NodeKind);
        Assert.Equal("topology_ui_category", category.RecordType);
        Assert.Equal("Category One", category.Label);
        Assert.Null(category.ComponentId);
        Assert.Null(category.ComponentKind);
        // parentKey "implicit_virtual_root" matches no record in this tree (the translator's
        // implicit $.root) — resolves to a null (root) ParentNodeId, never a dangling reference.
        Assert.Null(category.ParentNodeId);

        var section = Assert.Single(composed, n => n.NodeId == "sec1");
        Assert.Equal("structural_node", section.NodeKind);
        Assert.Equal("cat1", section.ParentNodeId);
        Assert.Null(section.ComponentId);

        var validation = Assert.Single(composed, n => n.NodeId == "val1");
        Assert.Equal("structural_node", validation.NodeKind);
        Assert.Equal("topology_ui_validation", validation.RecordType);
        Assert.Null(validation.ComponentId);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_FieldAndActionRecords_ResolveComponentIdAndKind_FromExistingRegistryLookup()
    {
        var records = ParseValidRows(CategoryRecordsJson);
        var requiredKeys = LayoutSchemaTensorComposer.RequiredComponentKeys(records);

        // Field control="form_input/select" -> select.template; Action -> button.primitive.
        Assert.Contains("select.template", requiredKeys);
        Assert.Contains("button.primitive", requiredKeys);

        var componentKeyToId = new Dictionary<string, string>
        {
            ["select.template"] = "00000000-0000-0000-0001-000000000012",
            ["button.primitive"] = "00000000-0000-0000-0001-000000000010",
        };
        var componentIdToKind = new Dictionary<string, string>
        {
            ["00000000-0000-0000-0001-000000000012"] = "form_input/select",
            ["00000000-0000-0000-0001-000000000010"] = "action/button",
        };

        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            interactionsBySourceActionKey: new Dictionary<string, string>(),
            componentKeyToId,
            componentIdToKind);

        var field = Assert.Single(composed, n => n.NodeId == "field1");
        Assert.Equal("catalog_component", field.NodeKind);
        Assert.Equal("00000000-0000-0000-0001-000000000012", field.ComponentId);
        Assert.Equal("form_input/select", field.ComponentKind);
        Assert.Null(field.RecordType);
        Assert.Null(field.Label);

        var action = Assert.Single(composed, n => n.NodeId == "action1");
        Assert.Equal("catalog_component", action.NodeKind);
        Assert.Equal("00000000-0000-0000-0001-000000000010", action.ComponentId);
        Assert.Equal("action/button", action.ComponentKind);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_FieldWithUnresolvableControl_LeavesComponentIdNull_NoSilentFallback()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","control":"form_input/unknown_widget"}}]}
        """;
        var records = ParseValidRows(json);
        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        var field = Assert.Single(composed);
        Assert.Equal("catalog_component", field.NodeKind);
        Assert.Null(field.ComponentId);
        Assert.Null(field.ComponentKind);
    }

    [Fact]
    public void BuildInteractionsBySourceActionKey_GroupsFormLevelTensorEntries_ByLeafSourceActionKey()
    {
        // Real seed shape: a single FORM-keyed tensor node carries multiple runtimeInteractions
        // entries, each tagged with the specific Action/Field leaf key (sourceActionKey) it
        // belongs to — not one array per leaf.
        var formNode = new LayoutNodeRecord(
            NodeId: "some_form", NodeKind: "catalog_component", HtmlTag: null, ComponentKey: null,
            ComponentId: null, ParentNodeId: null, SlotKey: null, OrderIndex: 0, X: 0, Y: 0,
            Width: null, Height: null, LayoutClassRefs: null,
            RuntimeInteractionsJson: """
            [
              {"trigger":"click","actionType":"dispatchInstanceOperation","sourceActionKey":"validate"},
              {"trigger":"click","actionType":"dispatchInstanceOperation","sourceActionKey":"preview"}
            ]
            """);

        var grouped = LayoutSchemaTensorComposer.BuildInteractionsBySourceActionKey([formNode]);

        Assert.True(grouped.ContainsKey("validate"));
        Assert.True(grouped.ContainsKey("preview"));
        Assert.DoesNotContain("some_form", grouped.Keys);
        Assert.Contains("\"sourceActionKey\":\"validate\"", grouped["validate"]);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_TensorRuntimeInteractions_MergeOntoMatchingLeafBySourceActionKey()
    {
        var records = ParseValidRows(CategoryRecordsJson);
        const string interactionsJson = """[{"trigger":"click","actionType":"dispatchInstanceOperation","sourceActionKey":"action1"}]""";

        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            interactionsBySourceActionKey: new Dictionary<string, string>
            {
                ["action1"] = interactionsJson,
                // Coincidental key collision with a structural node — must NOT be merged there.
                ["cat1"] = interactionsJson,
            },
            componentKeyToId: new Dictionary<string, string>(),
            componentIdToKind: new Dictionary<string, string>());

        var action = Assert.Single(composed, n => n.NodeId == "action1");
        Assert.Equal(interactionsJson, action.RuntimeInteractionsJson);

        // A structural node is never a runtimeInteractions merge target even when a tensor entry
        // coincidentally shares its key — merge only applies to catalog_component leaves.
        var category = Assert.Single(composed, n => n.NodeId == "cat1");
        Assert.Null(category.RuntimeInteractionsJson);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_DuplicateKeyAcrossBranches_NamespacesNodeId_NoDuplicateNodeIdCollision()
    {
        // Same authored key ("approval_status") in two different branches — a real seed shape
        // (manifest 092), not a synthetic edge case. Each occurrence must resolve to a distinct,
        // deterministic NodeId, never silently collide or silently drop one.
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":"section_a","record":{"recordType":"topology_ui_field","key":"approval_status","label":"A"}},
            {"type":"topology_ui_seed_record","parentKey":"section_b","record":{"recordType":"topology_ui_field","key":"approval_status","label":"B"}},
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_section","key":"section_a","label":"Section A"}},
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_section","key":"section_b","label":"Section B"}}
          ]
        }
        """;
        var records = ParseValidRows(json);
        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        var nodeIds = composed.Select(n => n.NodeId).ToList();
        Assert.Equal(nodeIds.Count, nodeIds.Distinct().Count());
        Assert.Contains("section_a::approval_status", nodeIds);
        Assert.Contains("section_b::approval_status", nodeIds);
    }

    [Fact]
    public void ParseRecords_TopologyUiTable_TopologyUiWorkflowStep_AreRecognized_NeverRejectedAsUnknown()
    {
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_table","key":"tbl1","label":"Table One","display":"card_list"}},
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_workflow_step","key":"step1","label":"Step One"}}
          ]
        }
        """;
        var result = LayoutSchemaTensorComposer.ParseRecords(json);
        var valid = Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Valid>(result);
        Assert.Equal(2, valid.Rows.Count);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_TableRecord_ResolvesComponentIdViaDisplayConvention()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_table","key":"tbl1","display":"card_list"}}]}
        """;
        var records = ParseValidRows(json);
        var requiredKeys = LayoutSchemaTensorComposer.RequiredComponentKeys(records);
        Assert.Contains("card_list.primitive", requiredKeys);

        var componentKeyToId = new Dictionary<string, string> { ["card_list.primitive"] = "00000000-0000-0000-0001-000000000014" };
        var componentIdToKind = new Dictionary<string, string> { ["00000000-0000-0000-0001-000000000014"] = "display/card_list" };

        var composed = LayoutSchemaTensorComposer.Compose(
            records, new Dictionary<string, string>(), componentKeyToId, componentIdToKind);

        var table = Assert.Single(composed);
        Assert.Equal("catalog_component", table.NodeKind);
        Assert.Equal("00000000-0000-0000-0001-000000000014", table.ComponentId);
        Assert.Equal("display/card_list", table.ComponentKind);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_TableRecordWithUnresolvableDisplay_LeavesComponentIdNull_NoSilentFallback()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_table","key":"tbl1","display":"unknown_display_kind"}}]}
        """;
        var records = ParseValidRows(json);
        var composed = LayoutSchemaTensorComposer.Compose(
            records, new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>());

        var table = Assert.Single(composed);
        Assert.Equal("catalog_component", table.NodeKind);
        Assert.Null(table.ComponentId);
        Assert.Null(table.ComponentKind);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_WorkflowStepRecord_ResolvesToSameComponentAsAction()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_workflow_step","key":"step1"}}]}
        """;
        var records = ParseValidRows(json);
        var requiredKeys = LayoutSchemaTensorComposer.RequiredComponentKeys(records);
        Assert.Contains("button.primitive", requiredKeys);

        var componentKeyToId = new Dictionary<string, string> { ["button.primitive"] = "00000000-0000-0000-0001-000000000010" };
        var componentIdToKind = new Dictionary<string, string> { ["00000000-0000-0000-0001-000000000010"] = "action/button" };

        var composed = LayoutSchemaTensorComposer.Compose(
            records, new Dictionary<string, string>(), componentKeyToId, componentIdToKind);

        var step = Assert.Single(composed);
        Assert.Equal("catalog_component", step.NodeKind);
        Assert.Equal("00000000-0000-0000-0001-000000000010", step.ComponentId);
        Assert.Equal("action/button", step.ComponentKind);
    }

    [Fact]
    public void ParseRecords_UnresolvedRecordMissingKnownGapRefs_ReturnsInvalid_NeverSkipped()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_unresolved","key":"u1","label":"Unresolved"}}]}
        """;
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_UnresolvedRecordWithEmptyKnownGapRefsArray_ReturnsInvalid_NeverSkipped()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_unresolved","key":"u1","knownGapRefs":[]}}]}
        """;
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_UnresolvedRecord_ComposesToUnresolvedGapNodeKind_CarryingKnownGapRefs_NeverAComponent()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_unresolved","key":"u1","label":"Unresolved Fragment","knownGapRefs":["table_item_click_wiring_not_yet_expressible"]}}]}
        """;
        var records = ParseValidRows(json);

        // Unresolved records never require a component_key lookup.
        Assert.Empty(LayoutSchemaTensorComposer.RequiredComponentKeys(records));

        var composed = LayoutSchemaTensorComposer.Compose(
            records, new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>());

        var unresolved = Assert.Single(composed);
        Assert.Equal("unresolved_gap", unresolved.NodeKind);
        Assert.Equal("topology_ui_unresolved", unresolved.RecordType);
        Assert.Equal("Unresolved Fragment", unresolved.Label);
        Assert.NotNull(unresolved.KnownGapRefs);
        Assert.Contains("table_item_click_wiring_not_yet_expressible", unresolved.KnownGapRefs!);
        Assert.Null(unresolved.ComponentId);
        Assert.Null(unresolved.ComponentKind);
        Assert.Null(unresolved.RuntimeInteractionsJson);
    }
}
