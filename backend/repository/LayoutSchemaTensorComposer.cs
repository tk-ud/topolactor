using System.Text.Json;

namespace Topolactor.Repository;

/// <summary>
/// Composes LayoutNodeRecords from components_layout_design.layout_schema_json.records[] —
/// the structural authority tree (Category/Section/Form/Workflow/Validation/Field/Table/Action/
/// WorkflowStep/Unresolved — the full
/// react-schema-topology-seed-translator-ssot.yaml layoutAdoptionCandidates.source_record_types
/// vocabulary) — for layouts where that tree is populated. Structural record types become
/// "structural_node" leaves with no componentId/componentKind; Field/Table/Action/WorkflowStep
/// record types become "catalog_component" leaves whose componentId/componentKind are resolved
/// from the existing topology.ui_component_registry preset catalog
/// (db/ui_component_registry_preset_catalog_bootstrap.sql) via the caller's existing
/// LoadComponentIdsByKeysAsync/LoadComponentKindsByIdsAsync batch lookups — no new registry rows or
/// query shapes; Unresolved records become "unresolved_gap" leaves that never resolve to a
/// component and always fail explicit at frontend render time, carrying their authored
/// knownGapRefs.
///
/// Tensor runtimeInteractions merge: authored tensor nodes (layout_patch_json.nodes[]) key their
/// runtimeInteractions array at the FORM level, with each entry individually tagged by
/// sourceActionKey identifying which specific Action/Field leaf it belongs to — the merge groups
/// tensor interaction entries by sourceActionKey (BuildInteractionsBySourceActionKey) and attaches
/// each leaf's own matching entries by leaf key == sourceActionKey, not by a naive
/// tensor-nodeId == record-key match (the tensor nodeId is the FORM's key, a structural node, which
/// never receives runtimeInteractions).
///
/// Layouts whose layout_schema_json has no records[] (most UI-Builder-authored layouts, where
/// componentId/componentKind already live directly on the tensor nodes) are unaffected — callers
/// must keep the existing tensor-only path when ParseRecords returns empty.
/// </summary>
public static class LayoutSchemaTensorComposer
{
    // SSOT: docs/design/runtime-orchestration-ssot.yaml ui_projection_render_reachability_contract
    // layout_schema_structural_render_contract — Category/Section/Form/Workflow/Validation are
    // structural nodes; Field/Action/Table/WorkflowStep are catalog components; Unresolved is
    // neither (an explicit unresolved_gap render-time failure).
    private static readonly HashSet<string> StructuralRecordTypes = new(StringComparer.Ordinal)
    {
        "topology_ui_category",
        "topology_ui_section",
        "topology_ui_form",
        "topology_ui_workflow",
        "topology_ui_validation",
    };

    private const string ActionRecordType = "topology_ui_action";
    private const string TableRecordType = "topology_ui_table";
    private const string WorkflowStepRecordType = "topology_ui_workflow_step";
    private const string UnresolvedRecordType = "topology_ui_unresolved";

    // Canonical control -> ui_component_registry.component_key convention for Field leaves.
    // Reuses the existing preset catalog rows; does not invent new registry entries.
    private static readonly IReadOnlyDictionary<string, string> FieldControlToComponentKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["form_input/select"] = "select.template",
            ["form_input/form_field"] = "form_field.template",
            ["form_input/input"] = "input.primitive",
            ["form_input/textarea"] = "textarea.alias",
        };

    // Canonical display -> ui_component_registry.component_key convention for Table leaves.
    // Reuses the existing preset catalog rows declared for table-shaped surfaces
    // (ui-builder-preset-ecosystem-ssot.yaml) — does not invent new registry entries.
    private static readonly IReadOnlyDictionary<string, string> TableDisplayToComponentKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["card_list"] = "card_list.primitive",
            ["data_grid"] = "data_grid.alias",
            ["list"] = "list.alias",
        };

    // Action and WorkflowStep leaves resolve to the existing generic button primitive — both
    // share the identical required-field shape (actionRef/eventBinding/authorityMarker) and are
    // differentiated by runtimeInteractions/eventBinding, not by componentKind. See
    // docs/design/react-schema-topology-seed-translator-ssot.yaml
    // storage_adoption_contract.adoption_candidate_separation_contract, which already treats the
    // two identically across wiringAdoptionCandidates/tensorAdoptionCandidates.
    private const string ActionComponentKey = "button.primitive";

    public record SchemaRecordRow(
        string RecordType,
        string Key,
        string? ParentKey,
        string? Label,
        string? Control,
        string? Display = null,
        IReadOnlyList<string>? KnownGapRefs = null);

    // Full recognized recordType vocabulary (structural + catalog + unresolved-gap) — matches
    // docs/design/react-schema-topology-seed-translator-ssot.yaml
    // storage_adoption_contract.adoption_candidate_separation_contract.candidate_buckets
    // .layoutAdoptionCandidates.source_record_types exactly. An entry whose recordType is outside
    // this set is a malformed/unrecognized shape, never silently skipped.
    private static readonly HashSet<string> RecognizedRecordTypes =
        new(StructuralRecordTypes, StringComparer.Ordinal)
        {
            "topology_ui_field", ActionRecordType, TableRecordType, WorkflowStepRecordType, UnresolvedRecordType,
        };

    /// <summary>
    /// Discriminates "no records[] to compose from" (NoRecords — the existing tensor-only path
    /// applies unchanged) from "records[] exists and is well-formed" (Valid) from "records[]
    /// exists but is malformed" (Invalid — a real authoring defect that must surface as an
    /// explicit failure, never be silently dropped to the tensor-only path or partially composed
    /// by skipping the bad entries). See docs/design/runtime-orchestration-ssot.yaml
    /// ui_projection_render_reachability_contract.layout_schema_structural_render_contract.
    /// absent_vs_invalid_records.
    /// </summary>
    public abstract record RecordsParseResult
    {
        private RecordsParseResult() { }
        public sealed record NoRecords : RecordsParseResult;
        public sealed record Valid(IReadOnlyList<SchemaRecordRow> Rows) : RecordsParseResult;
        public sealed record Invalid(string Reason) : RecordsParseResult;
    }

    /// <summary>
    /// Parses layout_schema_json's records[] array (topology_ui_seed_record entries) into a
    /// RecordsParseResult. NoRecords: layout_schema_json is absent/blank, has no "records" key, or
    /// "records" is an empty array (the default '{}'::jsonb shape most UI-Builder-authored layouts
    /// have). Invalid: the top-level JSON fails to parse, "records" exists but is not an array, or
    /// any entry is malformed (not an object; missing "record"; missing/blank recordType or key;
    /// or an unrecognized recordType) — the WHOLE records[] is rejected, never a partial tree built
    /// by skipping the bad entries. Valid: records[] is a non-empty array where every entry is
    /// well-formed.
    /// </summary>
    public static RecordsParseResult ParseRecords(string? layoutSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(layoutSchemaJson))
            return new RecordsParseResult.NoRecords();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(layoutSchemaJson);
        }
        catch (JsonException ex)
        {
            return new RecordsParseResult.Invalid($"layout_schema_json is not valid JSON: {ex.Message}");
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("records", out var recordsEl))
                return new RecordsParseResult.NoRecords();

            if (recordsEl.ValueKind != JsonValueKind.Array)
                return new RecordsParseResult.Invalid(
                    $"layout_schema_json.records is present but is not a JSON array (found {recordsEl.ValueKind}).");

            if (recordsEl.GetArrayLength() == 0)
                return new RecordsParseResult.NoRecords();

            var rows = new List<SchemaRecordRow>();
            var index = 0;
            foreach (var entry in recordsEl.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    return new RecordsParseResult.Invalid($"records[{index}] is not a JSON object.");
                if (!entry.TryGetProperty("record", out var record) || record.ValueKind != JsonValueKind.Object)
                    return new RecordsParseResult.Invalid($"records[{index}] is missing a \"record\" object.");

                var recordType = record.TryGetProperty("recordType", out var rt) && rt.ValueKind == JsonValueKind.String
                    ? rt.GetString() : null;
                var key = record.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String
                    ? k.GetString() : null;
                if (string.IsNullOrWhiteSpace(recordType))
                    return new RecordsParseResult.Invalid($"records[{index}].record is missing a non-empty recordType.");
                if (string.IsNullOrWhiteSpace(key))
                    return new RecordsParseResult.Invalid($"records[{index}].record is missing a non-empty key.");
                if (!RecognizedRecordTypes.Contains(recordType))
                    return new RecordsParseResult.Invalid(
                        $"records[{index}].record.recordType \"{recordType}\" is not a recognized structural/catalog record type.");

                var parentKey = entry.TryGetProperty("parentKey", out var pk) && pk.ValueKind == JsonValueKind.String
                    ? pk.GetString() : null;
                var label = record.TryGetProperty("label", out var lb) && lb.ValueKind == JsonValueKind.String
                    ? lb.GetString() : null;
                var control = record.TryGetProperty("control", out var ctl) && ctl.ValueKind == JsonValueKind.String
                    ? ctl.GetString() : null;
                var display = record.TryGetProperty("display", out var dsp) && dsp.ValueKind == JsonValueKind.String
                    ? dsp.GetString() : null;

                IReadOnlyList<string>? knownGapRefs = null;
                if (record.TryGetProperty("knownGapRefs", out var kgr) && kgr.ValueKind == JsonValueKind.Array)
                {
                    var refs = new List<string>();
                    foreach (var refEl in kgr.EnumerateArray())
                    {
                        if (refEl.ValueKind == JsonValueKind.String && refEl.GetString() is { } refValue)
                            refs.Add(refValue);
                    }
                    knownGapRefs = refs;
                }
                if (recordType == UnresolvedRecordType && (knownGapRefs is null || knownGapRefs.Count == 0))
                    return new RecordsParseResult.Invalid(
                        $"records[{index}].record.recordType \"{UnresolvedRecordType}\" is missing a non-empty knownGapRefs array.");

                rows.Add(new SchemaRecordRow(recordType!, key!, parentKey, label, control, display, knownGapRefs));
                index++;
            }
            return new RecordsParseResult.Valid(rows);
        }
    }

    /// <summary>
    /// Returns the distinct ui_component_registry.component_key values a caller must resolve
    /// (via LoadComponentIdsByKeysAsync) to compose the given schema records — Field records
    /// resolve by their control convention, Table records by their display convention, Action/
    /// WorkflowStep records resolve to the shared button primitive. Structural and Unresolved
    /// records never need a component_key (Unresolved never resolves to a component at all).
    /// Records with an unrecognized/absent control or display are omitted (componentId stays
    /// null — an explicit CATALOG_COMPONENT_KIND_REQUIRED error surfaces at render time, never a
    /// silent guess).
    /// </summary>
    public static IReadOnlyList<string> RequiredComponentKeys(IReadOnlyList<SchemaRecordRow> schemaRecords)
    {
        var keys = new List<string>();
        foreach (var row in schemaRecords)
        {
            if (StructuralRecordTypes.Contains(row.RecordType)) continue;
            if (row.RecordType == UnresolvedRecordType) continue;
            var key = ResolveComponentKey(row);
            if (key is not null) keys.Add(key);
        }
        return keys.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string? ResolveComponentKey(SchemaRecordRow row)
    {
        if (row.RecordType == ActionRecordType || row.RecordType == WorkflowStepRecordType)
            return ActionComponentKey;
        if (row.RecordType == TableRecordType)
        {
            return row.Display is not null && TableDisplayToComponentKey.TryGetValue(row.Display, out var tableMapped)
                ? tableMapped
                : null;
        }
        return row.Control is not null && FieldControlToComponentKey.TryGetValue(row.Control, out var mapped)
            ? mapped
            : null;
    }

    /// <summary>
    /// Groups tensor nodes' (layout_patch_json.nodes[]) runtimeInteractions entries by their
    /// sourceActionKey field — each tensor node carries its child Action/Field leaves' entries at
    /// the FORM level, individually tagged by which leaf they belong to. Entries without a
    /// sourceActionKey are omitted (nothing to attribute them to — never guessed). Returns a map
    /// from sourceActionKey to the JSON array text of that key's own entries (usually one).
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildInteractionsBySourceActionKey(
        IReadOnlyList<LayoutNodeRecord> tensorNodes)
    {
        var bySourceActionKey = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);

        foreach (var node in tensorNodes)
        {
            if (string.IsNullOrWhiteSpace(node.RuntimeInteractionsJson)) continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(node.RuntimeInteractionsJson);
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                foreach (var entry in doc.RootElement.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object) continue;
                    if (!entry.TryGetProperty("sourceActionKey", out var sak) ||
                        sak.ValueKind != JsonValueKind.String)
                        continue;
                    var sourceActionKey = sak.GetString();
                    if (string.IsNullOrWhiteSpace(sourceActionKey)) continue;

                    if (!bySourceActionKey.TryGetValue(sourceActionKey, out var list))
                    {
                        list = new List<JsonElement>();
                        bySourceActionKey[sourceActionKey] = list;
                    }
                    list.Add(entry.Clone());
                }
            }
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (sourceActionKey, entries) in bySourceActionKey)
            result[sourceActionKey] = JsonSerializer.Serialize(entries);
        return result;
    }

    /// <summary>
    /// Composes structural + catalog-component + unresolved-gap LayoutNodeRecords from the
    /// authored schema records tree. Structural record types (Category/Section/Form/Workflow/
    /// Validation) become "structural_node" entries carrying RecordType/Label only — never a
    /// componentId/componentKind. Field/Table/Action/WorkflowStep record types become
    /// "catalog_component" entries whose componentId/componentKind are resolved via the
    /// caller-supplied registry lookup dictionaries (see RequiredComponentKeys). Unresolved
    /// records become "unresolved_gap" entries carrying RecordType/Label/KnownGapRefs — never a
    /// componentId/componentKind, never merged with runtimeInteractions. Each catalog_component
    /// leaf's runtimeInteractions are merged from interactionsBySourceActionKey (see
    /// BuildInteractionsBySourceActionKey) by leaf key == sourceActionKey. Order follows the
    /// authored document order (already parent-before-child).
    ///
    /// NodeId is normally the record's own authored key; when two records anywhere in the tree
    /// share the same key (an authoring collision — the record tree's key is only guaranteed
    /// unique within its own branch, not globally), each is namespaced as
    /// "{parentKey}::{key}" instead so the composed list never violates the LAYOUT_PATCH_DUPLICATE_NODE_ID
    /// invariant StructureMapResolver.ValidateLayoutNodes already enforces. ParentNodeId is nulled
    /// (root) when a record's authored parentKey does not match any record in this tree — the
    /// implicit/virtual tree root the translator emits ($.root) is never itself a record.
    /// </summary>
    public static IReadOnlyList<LayoutNodeRecord> Compose(
        IReadOnlyList<SchemaRecordRow> schemaRecords,
        IReadOnlyDictionary<string, string> interactionsBySourceActionKey,
        IReadOnlyDictionary<string, string> componentKeyToId,
        IReadOnlyDictionary<string, string> componentIdToKind)
    {
        var definedKeys = new HashSet<string>(schemaRecords.Select(r => r.Key), StringComparer.Ordinal);
        var duplicateKeys = new HashSet<string>(
            schemaRecords.GroupBy(r => r.Key, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key),
            StringComparer.Ordinal);

        string ResolveNodeId(SchemaRecordRow row) =>
            duplicateKeys.Contains(row.Key) ? $"{row.ParentKey}::{row.Key}" : row.Key;

        var result = new List<LayoutNodeRecord>(schemaRecords.Count);
        var orderIndex = 0;

        foreach (var row in schemaRecords)
        {
            var isStructural = StructuralRecordTypes.Contains(row.RecordType);
            var isUnresolved = row.RecordType == UnresolvedRecordType;
            var isCatalogLeaf = !isStructural && !isUnresolved;
            string? componentId = null;
            string? componentKind = null;

            if (isCatalogLeaf)
            {
                var componentKey = ResolveComponentKey(row);
                if (componentKey is not null && componentKeyToId.TryGetValue(componentKey, out var resolvedId))
                {
                    componentId = resolvedId;
                    componentIdToKind.TryGetValue(resolvedId, out componentKind);
                }
            }

            // Merge target is a catalog_component leaf only, keyed by the leaf's own authored
            // key (== tensor entry sourceActionKey) — never by NodeId, which may be namespaced.
            // structural_node and unresolved_gap nodes never receive runtimeInteractions.
            string? runtimeInteractionsJson = null;
            if (isCatalogLeaf)
                interactionsBySourceActionKey.TryGetValue(row.Key, out runtimeInteractionsJson);

            var parentNodeId = row.ParentKey is not null && definedKeys.Contains(row.ParentKey)
                ? row.ParentKey
                : null;

            var nodeKind = isUnresolved ? "unresolved_gap" : isStructural ? "structural_node" : "catalog_component";

            result.Add(new LayoutNodeRecord(
                NodeId: ResolveNodeId(row),
                NodeKind: nodeKind,
                HtmlTag: null,
                ComponentKey: null,
                ComponentId: componentId,
                ParentNodeId: parentNodeId,
                SlotKey: null,
                OrderIndex: orderIndex++,
                X: 0.0,
                Y: 0.0,
                Width: null,
                Height: null,
                LayoutClassRefs: null,
                ComponentKind: componentKind,
                RuntimeInteractionsJson: runtimeInteractionsJson,
                RecordType: (isStructural || isUnresolved) ? row.RecordType : null,
                Label: (isStructural || isUnresolved) ? row.Label : null,
                KnownGapRefs: isUnresolved ? row.KnownGapRefs : null
            ));
        }

        return result;
    }
}
