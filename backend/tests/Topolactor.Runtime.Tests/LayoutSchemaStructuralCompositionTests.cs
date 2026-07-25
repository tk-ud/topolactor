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
        {"type":"topology_ui_seed_record","parentKey":"implicit_virtual_root","record":{"recordType":"topology_ui_category","key":"cat1","label":"Category One","sourceReactPath":"$.test.cat1","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
        {"type":"topology_ui_seed_record","parentKey":"cat1","record":{"recordType":"topology_ui_section","key":"sec1","label":"Section One","sourceReactPath":"$.test.sec1","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
        {"type":"topology_ui_seed_record","parentKey":"sec1","record":{"recordType":"topology_ui_field","key":"field1","label":"Field One","control":"form_input/select","sourceReactPath":"$.test.field1","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
        {"type":"topology_ui_seed_record","parentKey":"sec1","record":{"recordType":"topology_ui_action","key":"action1","label":"Action One","sourceReactPath":"$.test.action1","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
        {"type":"topology_ui_seed_record","parentKey":"sec1","record":{"recordType":"topology_ui_validation","key":"val1","label":"Validation One","sourceReactPath":"$.test.val1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}
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
        const string json = """{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"key":"f1","label":"f1","sourceReactPath":"$.test.f1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}""";
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_EntryMissingKey_ReturnsInvalid_NeverSkipped()
    {
        const string json = """{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","label":"unknown_key","sourceReactPath":"$.test.unknown_key","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}""";
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_UnrecognizedRecordType_ReturnsInvalid_NeverSkipped()
    {
        const string json = """{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_unknown_thing","key":"x","label":"x","sourceReactPath":"$.test.x","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}""";
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    // record_common_required_fields (docs/design/react-schema-topology-seed-translator-ssot.yaml
    // topology_ui_seed_contract.record_common_required_fields): the runtime parser validates the
    // SAME common fields the translator itself requires at generation time, so a persisted
    // layout_schema_json that drifted from that shape (hand-edited, corrupted, or authored by a
    // future producer that skips the translator) still fails close here instead of silently
    // composing with a null/fallback label, node id, or path. label, sourceReactPath, and
    // sourceYamlRefs must be non-empty; knownGapRefs must be present as an array (may be empty).
    [Theory]
    [InlineData("""{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","sourceReactPath":"$.test.f1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}""")]
    [InlineData("""{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","label":"","sourceReactPath":"$.test.f1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}""")]
    public void ParseRecords_MissingOrEmptyLabel_ReturnsInvalid_NeverFallsBackToKeyOrNodeId(string json)
    {
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Theory]
    [InlineData("""{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","label":"F1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}""")]
    [InlineData("""{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","label":"F1","sourceReactPath":"","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}""")]
    public void ParseRecords_MissingOrEmptySourceReactPath_ReturnsInvalid(string json)
    {
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Theory]
    [InlineData("""{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","label":"F1","sourceReactPath":"$.test.f1","knownGapRefs":[]}}]}""")]
    [InlineData("""{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","label":"F1","sourceReactPath":"$.test.f1","sourceYamlRefs":"not-an-array","knownGapRefs":[]}}]}""")]
    // Empty sourceYamlRefs is ALSO invalid — the translator's own validate_seed_record_tree
    // (SEED_RECORD_EMPTY_SOURCE_YAML_REFS) rejects an empty sourceYamlRefs the same way it
    // rejects a missing one; the runtime check mirrors that exactly.
    [InlineData("""{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","label":"F1","sourceReactPath":"$.test.f1","sourceYamlRefs":[],"knownGapRefs":[]}}]}""")]
    public void ParseRecords_MissingOrWrongShapedOrEmptySourceYamlRefs_ReturnsInvalid(string json)
    {
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_MissingKnownGapRefs_ReturnsInvalid_EvenForNonUnresolvedRecordType()
    {
        const string json = """{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","label":"F1","sourceReactPath":"$.test.f1","sourceYamlRefs":["a"]}}]}""";
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_KnownGapRefsEmptyListAllowed_ReturnsValid()
    {
        // An empty knownGapRefs list is not itself invalid for a non-Unresolved record type —
        // only a missing or wrong-shaped one is. sourceYamlRefs, unlike knownGapRefs, must be
        // NON-empty (see ParseRecords_MissingOrWrongShapedOrEmptySourceYamlRefs_ReturnsInvalid).
        const string json = """{"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","label":"F1","sourceReactPath":"$.test.f1","sourceYamlRefs":["a"],"knownGapRefs":[]}}]}""";
        var result = LayoutSchemaTensorComposer.ParseRecords(json);
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Valid>(result);
    }

    [Fact]
    public void ParseRecords_OneInvalidEntryAmongOtherwiseValidEntries_RejectsTheWholeList_NoPartialTree()
    {
        // Two well-formed entries plus one malformed entry — the WHOLE list must be rejected,
        // never a partial tree built by silently dropping just the bad entry.
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_category","key":"cat1","label":"cat1","sourceReactPath":"$.test.cat1","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"cat1","record":{"recordType":"topology_ui_unknown_thing","key":"bad","label":"bad","sourceReactPath":"$.test.bad","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"cat1","record":{"recordType":"topology_ui_section","key":"sec1","label":"sec1","sourceReactPath":"$.test.sec1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}
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
        // A catalog_component leaf's authored label survives composition exactly like a
        // structural_node's — the frontend needs it for honest production default props, not a
        // hardcoded placeholder caption or the bare NodeId.
        Assert.Equal("Field One", field.Label);

        var action = Assert.Single(composed, n => n.NodeId == "action1");
        Assert.Equal("catalog_component", action.NodeKind);
        Assert.Equal("00000000-0000-0000-0001-000000000010", action.ComponentId);
        Assert.Equal("action/button", action.ComponentKind);
        Assert.Equal("Action One", action.Label);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_FieldWithUnresolvableControl_LeavesComponentIdNull_NoSilentFallback()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"f1","control":"form_input/unknown_widget","label":"f1","sourceReactPath":"$.test.f1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}
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

        var result = LayoutSchemaTensorComposer.BuildInteractionsBySourceActionKey([formNode]);
        var grouped = Assert.IsType<LayoutSchemaTensorComposer.InteractionsParseResult.Valid>(result).BySourceActionKey;

        // Keys are scoped by the owning FORM's own tensor NodeId ("some_form") — never
        // sourceActionKey alone — so two different Forms authoring the same leaf key can never
        // cross-contaminate each other's interactions.
        Assert.True(grouped.ContainsKey("some_form::validate"));
        Assert.True(grouped.ContainsKey("some_form::preview"));
        Assert.DoesNotContain("validate", grouped.Keys);
        Assert.Contains("\"sourceActionKey\":\"validate\"", grouped["some_form::validate"]);
    }

    [Theory]
    [InlineData("not-json", "is not valid JSON")]
    [InlineData("{}", "is not a JSON array")]
    [InlineData("[\"not-an-object\"]", "is not a JSON object")]
    [InlineData("[{\"trigger\":\"click\"}]", "is missing a non-empty sourceActionKey")]
    [InlineData("[{\"trigger\":\"click\",\"sourceActionKey\":\"\"}]", "is missing a non-empty sourceActionKey")]
    public void BuildInteractionsBySourceActionKey_MalformedRuntimeInteractions_ReturnsInvalid_NeverSilentlySkipped(
        string runtimeInteractionsJson, string expectedReasonSubstring)
    {
        var formNode = new LayoutNodeRecord(
            NodeId: "some_form", NodeKind: "catalog_component", HtmlTag: null, ComponentKey: null,
            ComponentId: null, ParentNodeId: null, SlotKey: null, OrderIndex: 0, X: 0, Y: 0,
            Width: null, Height: null, LayoutClassRefs: null,
            RuntimeInteractionsJson: runtimeInteractionsJson);

        var result = LayoutSchemaTensorComposer.BuildInteractionsBySourceActionKey([formNode]);
        var invalid = Assert.IsType<LayoutSchemaTensorComposer.InteractionsParseResult.Invalid>(result);
        Assert.Contains(expectedReasonSubstring, invalid.Reason);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_TensorRuntimeInteractions_MergeOntoMatchingLeafBySourceActionKey()
    {
        var records = ParseValidRows(CategoryRecordsJson);
        const string interactionsJson = """[{"trigger":"click","actionType":"dispatchInstanceOperation","sourceActionKey":"action1"}]""";

        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            // Keys are scoped by "{parentKey}::{key}" ("sec1::action1" — action1's owning
            // Section) — never leaf key alone.
            interactionsBySourceActionKey: new Dictionary<string, string>
            {
                ["sec1::action1"] = interactionsJson,
                // Coincidental key collision with a structural node — must NOT be merged there.
                ["cat1::cat1"] = interactionsJson,
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
    public void ComposeLayoutSchemaWithTensor_DuplicateActionKeyAcrossTwoForms_InteractionsDoNotCrossContaminate()
    {
        // Two different Forms both author an Action with the SAME key ("submit") — a real
        // authoring possibility (Action keys are only guaranteed unique within their own Form).
        // Each Form's tensor node carries its OWN sourceActionKey-tagged interaction; scoping the
        // merge by "{parentKey}::{key}" (owning Form) must keep them from mixing.
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_form","key":"form_a","label":"Form A","sourceReactPath":"$.test.form_a","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"form_a","record":{"recordType":"topology_ui_action","key":"submit","label":"Submit A","sourceReactPath":"$.test.submit","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_form","key":"form_b","label":"Form B","sourceReactPath":"$.test.form_b","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"form_b","record":{"recordType":"topology_ui_action","key":"submit","label":"Submit B","sourceReactPath":"$.test.submit","sourceYamlRefs":["ref"],"knownGapRefs":[]}}
          ]
        }
        """;
        var records = ParseValidRows(json);

        const string interactionsA = """[{"trigger":"click","actionType":"dispatchInstanceOperation","sourceActionKey":"submit","targetRef":"A"}]""";
        const string interactionsB = """[{"trigger":"click","actionType":"dispatchInstanceOperation","sourceActionKey":"submit","targetRef":"B"}]""";

        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            interactionsBySourceActionKey: new Dictionary<string, string>
            {
                ["form_a::submit"] = interactionsA,
                ["form_b::submit"] = interactionsB,
            },
            componentKeyToId: new Dictionary<string, string>(),
            componentIdToKind: new Dictionary<string, string>());

        var submitA = Assert.Single(composed, n => n.NodeId == "form_a::submit");
        var submitB = Assert.Single(composed, n => n.NodeId == "form_b::submit");
        Assert.Equal(interactionsA, submitA.RuntimeInteractionsJson);
        Assert.Equal(interactionsB, submitB.RuntimeInteractionsJson);
        Assert.NotEqual(submitA.RuntimeInteractionsJson, submitB.RuntimeInteractionsJson);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_DuplicateContainerKey_ChildAttachesToTheSameInstanceItWasNestedUnder_NotWhicheverDuplicateMatchesByKey()
    {
        // Two Sections share the SAME key ("shared_section") under two different Categories —
        // each with its own distinct child. ParentNodeId resolution must attach each child to
        // the instance it was actually nested under (by document order), never to whichever
        // duplicate-keyed Section happens to match by raw key.
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_category","key":"cat_a","label":"Category A","sourceReactPath":"$.test.cat_a","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"cat_a","record":{"recordType":"topology_ui_section","key":"shared_section","label":"Shared Section A","sourceReactPath":"$.test.shared_section","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"shared_section","record":{"recordType":"topology_ui_field","key":"field_a","label":"Field A","control":"form_input/form_field","sourceReactPath":"$.test.field_a","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_category","key":"cat_b","label":"Category B","sourceReactPath":"$.test.cat_b","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"cat_b","record":{"recordType":"topology_ui_section","key":"shared_section","label":"Shared Section B","sourceReactPath":"$.test.shared_section","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"shared_section","record":{"recordType":"topology_ui_field","key":"field_b","label":"Field B","control":"form_input/form_field","sourceReactPath":"$.test.field_b","sourceYamlRefs":["ref"],"knownGapRefs":[]}}
          ]
        }
        """;
        var records = ParseValidRows(json);
        var componentKeyToId = new Dictionary<string, string> { ["form_field.template"] = "00000000-0000-0000-0001-000000000013" };
        var componentIdToKind = new Dictionary<string, string> { ["00000000-0000-0000-0001-000000000013"] = "form_input/form_field" };

        var composed = LayoutSchemaTensorComposer.Compose(
            records, new Dictionary<string, string>(), componentKeyToId, componentIdToKind);

        var sectionA = Assert.Single(composed, n => n.NodeId == "cat_a::shared_section");
        var sectionB = Assert.Single(composed, n => n.NodeId == "cat_b::shared_section");
        var fieldA = Assert.Single(composed, n => n.NodeId == "field_a");
        var fieldB = Assert.Single(composed, n => n.NodeId == "field_b");

        Assert.Equal(sectionA.NodeId, fieldA.ParentNodeId);
        Assert.Equal(sectionB.NodeId, fieldB.ParentNodeId);
        Assert.NotEqual(fieldA.ParentNodeId, fieldB.ParentNodeId);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_DuplicateKeyAcrossBranches_NamespacesNodeId_NoDuplicateNodeIdCollision()
    {
        // Same authored key ("approval_status") in two different branches — a real seed shape
        // (manifest 092), not a synthetic edge case. Each occurrence must resolve to a distinct,
        // deterministic NodeId, never silently collide or silently drop one.
        // Parent-before-child document order, matching the real translator's
        // flatten_topology_ui_seed_tree contract (parent-scoped identity reconstruction depends
        // on this order — see ParentNodeId resolution above).
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_section","key":"section_a","label":"Section A","sourceReactPath":"$.test.section_a","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"section_a","record":{"recordType":"topology_ui_field","key":"approval_status","label":"A","sourceReactPath":"$.test.approval_status","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_section","key":"section_b","label":"Section B","sourceReactPath":"$.test.section_b","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"section_b","record":{"recordType":"topology_ui_field","key":"approval_status","label":"B","sourceReactPath":"$.test.approval_status","sourceYamlRefs":["ref"],"knownGapRefs":[]}}
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
    public void ComposeLayoutSchemaWithTensor_DuplicateParentKeyAndDuplicateChildKeySimultaneously_ResolvedIdentityStaysConsistent_NoNodeIdCollision_NoCrossContamination()
    {
        // Both the CONTAINER key ("shared_section") and the LEAF key ("shared_field"/
        // "shared_action") are duplicated across two branches at the same time — the combined
        // case neither ComposeLayoutSchemaWithTensor_DuplicateContainerKey_... (duplicate parent,
        // distinct children) nor ComposeLayoutSchemaWithTensor_DuplicateKeyAcrossBranches_...
        // (distinct parents, duplicate child) covers alone. The resolved parent identity used for
        // a child's NodeId must be the SAME resolved identity used for that child's tensor
        // interaction merge key, and neither may cross into the other branch.
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_category","key":"cat_a","label":"Category A","sourceReactPath":"$.test.cat_a","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"cat_a","record":{"recordType":"topology_ui_section","key":"shared_section","label":"Shared Section A","sourceReactPath":"$.test.shared_section","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"shared_section","record":{"recordType":"topology_ui_field","key":"shared_field","label":"Field A","control":"form_input/form_field","sourceReactPath":"$.test.shared_field","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"shared_section","record":{"recordType":"topology_ui_action","key":"shared_action","label":"Action A","sourceReactPath":"$.test.shared_action","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_category","key":"cat_b","label":"Category B","sourceReactPath":"$.test.cat_b","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"cat_b","record":{"recordType":"topology_ui_section","key":"shared_section","label":"Shared Section B","sourceReactPath":"$.test.shared_section","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"shared_section","record":{"recordType":"topology_ui_field","key":"shared_field","label":"Field B","control":"form_input/form_field","sourceReactPath":"$.test.shared_field","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":"shared_section","record":{"recordType":"topology_ui_action","key":"shared_action","label":"Action B","sourceReactPath":"$.test.shared_action","sourceYamlRefs":["ref"],"knownGapRefs":[]}}
          ]
        }
        """;
        var records = ParseValidRows(json);
        // Distinguishable evidence per branch (different instanceTargetRef) — if the merge ever
        // grabbed the WRONG branch's dictionary entry, identical-content evidence would still
        // pass a same-shape assertion; only a per-branch-distinct value can prove exact
        // attribution, not just "some entry merged".
        const string interactionsA = """[{"trigger":"click","actionType":"dispatchInstanceOperation","sourceActionKey":"shared_action","instanceTargetRef":"A"}]""";
        const string interactionsB = """[{"trigger":"click","actionType":"dispatchInstanceOperation","sourceActionKey":"shared_action","instanceTargetRef":"B"}]""";

        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            interactionsBySourceActionKey: new Dictionary<string, string>
            {
                ["cat_a::shared_section::shared_action"] = interactionsA,
                ["cat_b::shared_section::shared_action"] = interactionsB,
            },
            componentKeyToId: new Dictionary<string, string>(),
            componentIdToKind: new Dictionary<string, string>());

        // No duplicate NodeId anywhere in the composed tree.
        var nodeIds = composed.Select(n => n.NodeId).ToList();
        Assert.Equal(nodeIds.Count, nodeIds.Distinct().Count());

        var sectionA = Assert.Single(composed, n => n.NodeId == "cat_a::shared_section");
        var sectionB = Assert.Single(composed, n => n.NodeId == "cat_b::shared_section");
        var fieldA = Assert.Single(composed, n => n.NodeId == "cat_a::shared_section::shared_field");
        var fieldB = Assert.Single(composed, n => n.NodeId == "cat_b::shared_section::shared_field");
        var actionA = Assert.Single(composed, n => n.NodeId == "cat_a::shared_section::shared_action");
        var actionB = Assert.Single(composed, n => n.NodeId == "cat_b::shared_section::shared_action");

        // Each child attaches to the SAME resolved parent identity it was actually nested under.
        Assert.Equal(sectionA.NodeId, fieldA.ParentNodeId);
        Assert.Equal(sectionA.NodeId, actionA.ParentNodeId);
        Assert.Equal(sectionB.NodeId, fieldB.ParentNodeId);
        Assert.Equal(sectionB.NodeId, actionB.ParentNodeId);
        Assert.NotEqual(fieldA.ParentNodeId, fieldB.ParentNodeId);
        Assert.NotEqual(actionA.ParentNodeId, actionB.ParentNodeId);

        // Each duplicate Action's runtimeInteractions come from its OWN branch's merge key —
        // never cross-contaminated with the other branch's entry for the same leaf key. Asserting
        // the branch-DISTINCT instanceTargetRef (not just the shared sourceActionKey both entries
        // carry) proves exact attribution, not merely "some entry merged".
        Assert.NotNull(actionA.RuntimeInteractionsJson);
        Assert.NotNull(actionB.RuntimeInteractionsJson);
        Assert.Contains("\"instanceTargetRef\":\"A\"", actionA.RuntimeInteractionsJson);
        Assert.DoesNotContain("\"instanceTargetRef\":\"B\"", actionA.RuntimeInteractionsJson);
        Assert.Contains("\"instanceTargetRef\":\"B\"", actionB.RuntimeInteractionsJson);
        Assert.DoesNotContain("\"instanceTargetRef\":\"A\"", actionB.RuntimeInteractionsJson);
    }

    [Fact]
    public void ParseRecords_TopologyUiTable_TopologyUiWorkflowStep_AreRecognized_NeverRejectedAsUnknown()
    {
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_table","key":"tbl1","label":"Table One","display":"card_list","sourceReactPath":"$.test.tbl1","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_workflow_step","key":"step1","label":"Step One","sourceReactPath":"$.test.step1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}
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
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_table","key":"tbl1","display":"card_list","label":"tbl1","sourceReactPath":"$.test.tbl1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}
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
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_table","key":"tbl1","display":"unknown_display_kind","label":"tbl1","sourceReactPath":"$.test.tbl1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}
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
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_workflow_step","key":"step1","label":"step1","sourceReactPath":"$.test.step1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}
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
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_unresolved","key":"u1","label":"Unresolved","sourceReactPath":"$.test.u1","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}
        """;
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ParseRecords_UnresolvedRecordWithEmptyKnownGapRefsArray_ReturnsInvalid_NeverSkipped()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_unresolved","key":"u1","knownGapRefs":[],"label":"u1","sourceReactPath":"$.test.u1","sourceYamlRefs":["ref"]}}]}
        """;
        Assert.IsType<LayoutSchemaTensorComposer.RecordsParseResult.Invalid>(LayoutSchemaTensorComposer.ParseRecords(json));
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_UnresolvedRecord_ComposesToUnresolvedGapNodeKind_CarryingKnownGapRefs_NeverAComponent()
    {
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_unresolved","key":"u1","label":"Unresolved Fragment","knownGapRefs":["table_item_click_wiring_not_yet_expressible"],"sourceReactPath":"$.test.u1","sourceYamlRefs":["ref"]}}]}
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
    // ------------------------------------------------------------------------------------------
    // Checked-in LayoutNode[] fixture proofs for frontend/tests/layoutSchemaStructuralRender.test.ts
    // ------------------------------------------------------------------------------------------
    // The frontend common/generic renderEmission() contract tests previously hand-typed their
    // LayoutNode[] input directly (a hand-authored Emission-facing literal that happened to satisfy
    // renderEmission's own expectations by construction, never independently verified against real
    // LayoutSchemaTensorComposer.Compose() output). These test methods close that gap: each one
    // parses seed-shaped records[] JSON (the exact wire shape LayoutSchemaTensorComposer.ParseRecords
    // consumes — the same non-seed literal-JSON pattern the rest of this file already uses, NOT a
    // db/seed_empty.sql row; no manifest/route/UI is added by this), runs it through the REAL
    // LayoutSchemaTensorComposer.Compose() + StructureMapResolver.ToLayoutNode() + the exact
    // camelCase/WhenWritingNull JSON options backend/Program.cs serializes Emission over HTTP with,
    // and asserts the result is byte-exact against a checked-in fixture under
    // frontend/tests/fixtures/layout_schema_composed_scenarios/ — the same "checked-in fixture with a
    // byte-exact companion backend proof" discipline manifest 092's own fixture uses (see
    // CredentialManagementHubRelationUiProjectionLiveDbTests.cs
    // DispatchAsync_BareDefaultEntry_NoTargetRef_ResolvesManifest0092ViaCanonicalDefaultEntryRelation),
    // just applied to small synthetic scenarios instead of a live-DB-dispatched real manifest. A
    // future composer change that alters any of these scenarios' output breaks this test, not just
    // the frontend one — closing the "hand-authored LayoutNode[] -> renderEmission only" gap the
    // common test substrate previously had.
    private static readonly System.Text.Json.JsonSerializerOptions EmissionLayoutNodeJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private static string FixturePath(string fileName) => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../../../frontend/tests/fixtures/layout_schema_composed_scenarios",
        fileName));

    private static async Task AssertComposedLayoutNodesMatchFixtureAsync(
        string fixtureFileName,
        IReadOnlyList<LayoutSchemaTensorComposer.SchemaRecordRow> records,
        IReadOnlyDictionary<string, string> interactionsBySourceActionKey,
        IReadOnlyDictionary<string, string> componentKeyToId,
        IReadOnlyDictionary<string, string> componentIdToKind)
    {
        var composed = LayoutSchemaTensorComposer.Compose(
            records, interactionsBySourceActionKey, componentKeyToId, componentIdToKind);
        var layoutNodes = composed.Select(Topolactor.Runtime.StructureMapResolver.ToLayoutNode).ToList();
        var actualJson = System.Text.Json.JsonSerializer.Serialize(layoutNodes, EmissionLayoutNodeJsonOptions);
        var expectedJson = await File.ReadAllTextAsync(FixturePath(fixtureFileName));
        Assert.Equal(expectedJson.Trim(), actualJson.Trim());
    }

    [Fact]
    public async Task ComposeAndMapToLayoutNode_StructuralCategorySectionFormFieldAction_MatchesCheckedInFrontendFixture()
    {
        // Same records[] scenario as ComposeLayoutSchemaWithTensor_CategorySectionFormRecords_...
        // and ComposeLayoutSchemaWithTensor_FieldAndActionRecords_... above, mapped to the
        // Emission-facing LayoutNode shape.
        await AssertComposedLayoutNodesMatchFixtureAsync(
            "scenario_structural_catalog_mix.json",
            ParseValidRows(CategoryRecordsJson),
            interactionsBySourceActionKey: new Dictionary<string, string>(),
            componentKeyToId: new Dictionary<string, string>
            {
                ["select.template"] = "00000000-0000-0000-0001-000000000012",
                ["button.primitive"] = "00000000-0000-0000-0001-000000000010",
            },
            componentIdToKind: new Dictionary<string, string>
            {
                ["00000000-0000-0000-0001-000000000012"] = "form_input/select",
                ["00000000-0000-0000-0001-000000000010"] = "action/button",
            });
    }

    [Fact]
    public async Task ComposeAndMapToLayoutNode_FieldWithUnresolvableControl_MatchesCheckedInFrontendFixture()
    {
        // Same scenario as ComposeLayoutSchemaWithTensor_FieldWithUnresolvableControl_... above —
        // componentId stays null, no silent fallback.
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_field","key":"unresolvable_field","control":"form_input/unknown_widget","label":"Unresolvable field","sourceReactPath":"$.test.unresolvable_field","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}
        """;
        await AssertComposedLayoutNodesMatchFixtureAsync(
            "scenario_unresolvable_componentid.json",
            ParseValidRows(json),
            interactionsBySourceActionKey: new Dictionary<string, string>(),
            componentKeyToId: new Dictionary<string, string>(),
            componentIdToKind: new Dictionary<string, string>());
    }

    [Fact]
    public async Task ComposeAndMapToLayoutNode_ActionWithUnattributableRuntimeInteraction_MatchesCheckedInFrontendFixture()
    {
        // actionType "localStateMutation" carries no target (portTargetRef/instanceTargetRef/
        // targetNodeId) — renderEmission.ts's event binding builder cannot attribute this to any
        // recognized trigger, so it must fail explicit as RUNTIME_INTERACTION_UNATTRIBUTABLE. This
        // proves that failure mode against REAL composer-merged runtimeInteractions, not a
        // hand-typed LayoutNode.runtimeInteractions literal.
        const string recordsJson = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_action","key":"sample_unattributable_action","label":"Sample unattributable action","sourceReactPath":"$.test.sample_unattributable_action","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}
        """;
        const string interactionsJson = """[{"trigger":"click","actionType":"localStateMutation","sourceActionKey":"sample_unattributable_action"}]""";
        await AssertComposedLayoutNodesMatchFixtureAsync(
            "scenario_unattributable_interaction.json",
            ParseValidRows(recordsJson),
            // Top-level record (no parentKey) -> resolved parentNodeId is null -> merge key is
            // "::{key}" (see LayoutSchemaTensorComposer.Compose's ResolveInteractionsMergeKey note).
            interactionsBySourceActionKey: new Dictionary<string, string>
            {
                ["::sample_unattributable_action"] = interactionsJson,
            },
            componentKeyToId: new Dictionary<string, string> { ["button.primitive"] = "00000000-0000-0000-0001-000000000010" },
            componentIdToKind: new Dictionary<string, string> { ["00000000-0000-0000-0001-000000000010"] = "action/button" });
    }

    [Fact]
    public async Task ComposeAndMapToLayoutNode_ActionWithAttributableRuntimeInteraction_MatchesCheckedInFrontendFixture()
    {
        // actionType "dispatchInstanceOperation" with an instanceTargetRef IS a recognized,
        // attributable event binding — the counterpart proof to the unattributable scenario above,
        // again against real composer-merged runtimeInteractions.
        const string recordsJson = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_action","key":"sample_action","label":"Sample action","sourceReactPath":"$.test.sample_action","sourceYamlRefs":["ref"],"knownGapRefs":[]}}]}
        """;
        const string interactionsJson = """[{"trigger":"click","actionType":"dispatchInstanceOperation","instanceTargetRef":"instance-port:sample_instance_port:sample_field:operation_binding_key","sourceActionKey":"sample_action"}]""";
        await AssertComposedLayoutNodesMatchFixtureAsync(
            "scenario_attributable_interaction.json",
            ParseValidRows(recordsJson),
            interactionsBySourceActionKey: new Dictionary<string, string>
            {
                ["::sample_action"] = interactionsJson,
            },
            componentKeyToId: new Dictionary<string, string> { ["button.primitive"] = "00000000-0000-0000-0001-000000000010" },
            componentIdToKind: new Dictionary<string, string> { ["00000000-0000-0000-0001-000000000010"] = "action/button" });
    }

    [Fact]
    public async Task ComposeAndMapToLayoutNode_TableAndWorkflowStep_MatchesCheckedInFrontendFixture()
    {
        // Combines the same records[] scenarios as
        // ComposeLayoutSchemaWithTensor_TableRecord_ResolvesComponentIdViaDisplayConvention and
        // ComposeLayoutSchemaWithTensor_WorkflowStepRecord_ResolvesToSameComponentAsAction above.
        const string json = """
        {
          "records": [
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_table","key":"results_table","display":"card_list","label":"Results table","sourceReactPath":"$.test.results_table","sourceYamlRefs":["ref"],"knownGapRefs":[]}},
            {"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_workflow_step","key":"approval_step","label":"Approval step","sourceReactPath":"$.test.approval_step","sourceYamlRefs":["ref"],"knownGapRefs":[]}}
          ]
        }
        """;
        await AssertComposedLayoutNodesMatchFixtureAsync(
            "scenario_table_workflow_step.json",
            ParseValidRows(json),
            interactionsBySourceActionKey: new Dictionary<string, string>(),
            componentKeyToId: new Dictionary<string, string>
            {
                ["card_list.primitive"] = "00000000-0000-0000-0001-000000000014",
                ["button.primitive"] = "00000000-0000-0000-0001-000000000010",
            },
            componentIdToKind: new Dictionary<string, string>
            {
                ["00000000-0000-0000-0001-000000000014"] = "display/card_list",
                ["00000000-0000-0000-0001-000000000010"] = "action/button",
            });
    }

    [Fact]
    public async Task ComposeAndMapToLayoutNode_UnresolvedGap_MatchesCheckedInFrontendFixture()
    {
        // Same scenario as ComposeLayoutSchemaWithTensor_UnresolvedRecord_... above.
        const string json = """
        {"records":[{"type":"topology_ui_seed_record","parentKey":null,"record":{"recordType":"topology_ui_unresolved","key":"unresolved_fragment","label":"Unresolved fragment","knownGapRefs":["table_item_click_wiring_not_yet_expressible"],"sourceReactPath":"$.test.unresolved_fragment","sourceYamlRefs":["ref"]}}]}
        """;
        await AssertComposedLayoutNodesMatchFixtureAsync(
            "scenario_unresolved_gap.json",
            ParseValidRows(json),
            interactionsBySourceActionKey: new Dictionary<string, string>(),
            componentKeyToId: new Dictionary<string, string>(),
            componentIdToKind: new Dictionary<string, string>());
    }

    // ─── BuildNodeLocalDataByNodeId / PropsJson/StateJson/PropBindingsJson merge ───
    // (added 2026-07-23: admin-runtime-operation-dispatch-lane-determination read-circuit
    // consumption — previously only RuntimeInteractions merged through from tensor nodes for
    // schema-composed layouts; a tensor node's own propsJson/stateJson/propBindings were parsed
    // by NpgsqlTopologyRepository but silently discarded for this layout shape.)

    [Fact]
    public void BuildNodeLocalDataByNodeId_TensorNodeWithNoLocalFields_IsAbsentFromResult()
    {
        var tensorNodes = new List<LayoutNodeRecord>
        {
            new(NodeId: "n1", NodeKind: "catalog_component", HtmlTag: null,
                ComponentKey: null, ComponentId: null, ParentNodeId: null,
                SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: null, Height: null,
                LayoutClassRefs: null),
        };
        var result = LayoutSchemaTensorComposer.BuildNodeLocalDataByNodeId(tensorNodes);
        Assert.False(result.ContainsKey("n1"));
    }

    [Fact]
    public void BuildNodeLocalDataByNodeId_TensorNodeWithPropsAndPropBindings_IsKeptByExactNodeId()
    {
        var tensorNodes = new List<LayoutNodeRecord>
        {
            new(NodeId: "enum_table", NodeKind: "catalog_component", HtmlTag: null,
                ComponentKey: null, ComponentId: null, ParentNodeId: null,
                SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: null, Height: null,
                LayoutClassRefs: null,
                PropsJson: """{"table":null,"columns":[{"key":"groupName","header":"Group name"}]}""",
                PropBindingsJson: """{"rows":{"source":"emission.data"}}"""),
        };
        var result = LayoutSchemaTensorComposer.BuildNodeLocalDataByNodeId(tensorNodes);
        Assert.True(result.TryGetValue("enum_table", out var data));
        Assert.Contains("groupName", data.PropsJson);
        Assert.Contains("emission.data", data.PropBindingsJson);
        Assert.Null(data.StateJson);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_CatalogLeafMatchingTensorNodeId_MergesPropsAndPropBindings()
    {
        var records = ParseValidRows(CategoryRecordsJson);
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
        var nodeLocalDataByNodeId = new Dictionary<string, LayoutSchemaTensorComposer.NodeLocalData>
        {
            ["field1"] = new(
                PropsJson: """{"data":{"label":"Overridden"}}""",
                StateJson: null,
                PropBindingsJson: """{"options":{"source":"emission.data"}}"""),
        };

        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            interactionsBySourceActionKey: new Dictionary<string, string>(),
            componentKeyToId,
            componentIdToKind,
            nodeLocalDataByNodeId);

        var field = Assert.Single(composed, n => n.NodeId == "field1");
        Assert.Equal("""{"data":{"label":"Overridden"}}""", field.PropsJson);
        Assert.Equal("""{"options":{"source":"emission.data"}}""", field.PropBindingsJson);
        Assert.Null(field.StateJson);

        // A structural_node never receives node-local data even when a tensor entry happens to
        // share its NodeId — only catalog_component leaves are eligible.
        var section = Assert.Single(composed, n => n.NodeId == "sec1");
        Assert.Null(section.PropsJson);
        Assert.Null(section.PropBindingsJson);

        // A catalog_component leaf with no matching tensor NodeId composes with null local data,
        // exactly as before this feature existed — purely additive, no behavior change for
        // unrelated leaves.
        var action = Assert.Single(composed, n => n.NodeId == "action1");
        Assert.Null(action.PropsJson);
        Assert.Null(action.PropBindingsJson);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_NoNodeLocalDataMapProvided_ComposesAsBeforeThisFeature()
    {
        // Compose's nodeLocalDataByNodeId parameter is optional (null default) — every existing
        // caller/fixture that predates this feature must keep composing identically.
        var records = ParseValidRows(CategoryRecordsJson);
        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            interactionsBySourceActionKey: new Dictionary<string, string>(),
            componentKeyToId: new Dictionary<string, string>(),
            componentIdToKind: new Dictionary<string, string>());

        Assert.All(composed, n =>
        {
            Assert.Null(n.PropsJson);
            Assert.Null(n.StateJson);
            Assert.Null(n.PropBindingsJson);
            Assert.Null(n.DispatchPayloadFromByTriggerJson);
        });
    }

    // ─── dispatchPayloadFromByTrigger (PR #599 review round 6): a node-level
    // admin_runtime payload binding field, merged by exact tensor NodeId through the
    // SAME NodeLocalData mechanism as propsJson/stateJson/propBindings ────────────

    [Fact]
    public void BuildNodeLocalDataByNodeId_TensorNodeWithDispatchPayloadFromByTrigger_IsKeptByExactNodeId()
    {
        var tensorNodes = new List<LayoutNodeRecord>
        {
            new(NodeId: "enum_confirm_button", NodeKind: "catalog_component", HtmlTag: null,
                ComponentKey: null, ComponentId: null, ParentNodeId: null,
                SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: null, Height: null,
                LayoutClassRefs: null,
                DispatchPayloadFromByTriggerJson: """{"click":{"groupName":"node:name_input.value"}}"""),
        };
        var result = LayoutSchemaTensorComposer.BuildNodeLocalDataByNodeId(tensorNodes);
        Assert.True(result.TryGetValue("enum_confirm_button", out var data));
        Assert.Contains("groupName", data.DispatchPayloadFromByTriggerJson);
        Assert.Null(data.PropsJson);
    }

    [Fact]
    public void ComposeLayoutSchemaWithTensor_CatalogLeafMatchingTensorNodeId_MergesDispatchPayloadFromByTrigger()
    {
        var records = ParseValidRows(CategoryRecordsJson);
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
        var nodeLocalDataByNodeId = new Dictionary<string, LayoutSchemaTensorComposer.NodeLocalData>
        {
            ["action1"] = new(
                PropsJson: null,
                StateJson: null,
                PropBindingsJson: null,
                DispatchPayloadFromByTriggerJson: """{"click":{"groupName":"node:field1.value"}}"""),
        };

        var composed = LayoutSchemaTensorComposer.Compose(
            records,
            interactionsBySourceActionKey: new Dictionary<string, string>(),
            componentKeyToId,
            componentIdToKind,
            nodeLocalDataByNodeId);

        var action = Assert.Single(composed, n => n.NodeId == "action1");
        Assert.Equal(
            """{"click":{"groupName":"node:field1.value"}}""",
            action.DispatchPayloadFromByTriggerJson);

        // A structural_node never receives node-local data even when a tensor entry happens to
        // share its NodeId — only catalog_component leaves are eligible.
        var section = Assert.Single(composed, n => n.NodeId == "sec1");
        Assert.Null(section.DispatchPayloadFromByTriggerJson);
    }
}
