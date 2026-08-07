using System.Text.Json;

namespace Topolactor.Repository;

/// <summary>
/// Composes LayoutNodeRecords from components_layout_design.layout_schema_json.records[] —
/// the structural authority tree (Category/Section/Form/Workflow/Validation/Field/Table/Action/
/// WorkflowStep/Modal/Unresolved — the full
/// react-schema-topology-seed-translator-ssot.yaml layoutAdoptionCandidates.source_record_types
/// vocabulary) — for layouts where that tree is populated. Structural record types become
/// "structural_node" leaves with no componentId/componentKind; Field/Table/Action/WorkflowStep
/// record types become "catalog_component" leaves whose componentId/componentKind are resolved
/// from the existing topology.ui_component_registry preset catalog
/// (db/ui_component_registry_preset_catalog_bootstrap.sql) via the caller's existing
/// LoadComponentIdsByKeysAsync/LoadComponentKindsByIdsAsync batch lookups — no new registry rows or
/// query shapes; Modal (round 24) also becomes a "catalog_component" leaf but its componentKind is
/// the record's own literal value (always "disclosure/modal" today), never a registry lookup, and
/// it carries no ComponentId (a built-in runtime primitive, not a registry-backed preset) —
/// unlike every other catalog leaf, a Modal can itself be a parent of further catalog leaves (its
/// authored Confirm/Cancel Action children); Unresolved records become "unresolved_gap" leaves
/// that never resolve to a component and always fail explicit at frontend render time, carrying
/// their authored knownGapRefs.
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
    // Round 24 (admin-enum subBundle, mutation-confirmation-workflow): a Modal record carries its
    // own componentKind directly (always "disclosure/modal" today) rather than resolving one via
    // a control/display convention table the way Field/Table do -- there is no "modal shape"
    // vocabulary to convention-map, and the translator already only ever emits one literal value.
    // Modal IS a catalog leaf (renders a real component, unlike Category/Section/Form/Workflow/
    // Validation's structural_node) but, uniquely among catalog leaves so far, can itself be a
    // PARENT of further catalog leaves (its Confirm/Cancel Action children) -- the generic
    // ParentNodeId/lastResolvedNodeIdByKey resolution below already supports any record type
    // acting as a parent, so this needs no separate tree-walk.
    private const string ModalRecordType = "topology_ui_modal";
    private const string ModalComponentKind = "disclosure/modal";

    // Canonical control -> ui_component_registry.component_key convention for Field leaves.
    // Reuses the existing preset catalog rows; does not invent new registry entries.
    private static readonly IReadOnlyDictionary<string, string> FieldControlToComponentKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["form_input/select"] = "select.template",
            ["form_input/form_field"] = "form_field.template",
            ["form_input/input"] = "input.primitive",
            ["form_input/textarea"] = "textarea.alias",
            ["form_input/search_input"] = "search_input.alias",
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
            ["table"] = "table.primitive",
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
        IReadOnlyList<string>? KnownGapRefs = null,
        string? ComponentKind = null);

    // Full recognized recordType vocabulary (structural + catalog + unresolved-gap) — matches
    // docs/design/react-schema-topology-seed-translator-ssot.yaml
    // storage_adoption_contract.adoption_candidate_separation_contract.candidate_buckets
    // .layoutAdoptionCandidates.source_record_types exactly. An entry whose recordType is outside
    // this set is a malformed/unrecognized shape, never silently skipped.
    private static readonly HashSet<string> RecognizedRecordTypes =
        new(StructuralRecordTypes, StringComparer.Ordinal)
        {
            "topology_ui_field", ActionRecordType, TableRecordType, WorkflowStepRecordType, UnresolvedRecordType,
            ModalRecordType,
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
                var componentKind = record.TryGetProperty("componentKind", out var ck) && ck.ValueKind == JsonValueKind.String
                    ? ck.GetString() : null;
                if (recordType == ModalRecordType && string.IsNullOrWhiteSpace(componentKind))
                    return new RecordsParseResult.Invalid($"records[{index}].record recordType \"{ModalRecordType}\" is missing a non-empty componentKind.");
                // Round 25: exact-match fail-close, not merely non-empty. docs/design/
                // react-schema-topology-seed-translator-ssot.yaml react_schema_contract
                // .allowed_node_kinds.Modal fixes componentKind to the single literal value
                // "disclosure/modal" -- this translator/composer emits no Drawer/Dialog/other
                // container kind today, so any other non-empty value (a typo, "disclosure/drawer"
                // ahead of that family's own real support landing, or an unrelated string) is a
                // real authoring defect, never silently accepted as if it were a registered kind.
                if (recordType == ModalRecordType && componentKind != ModalComponentKind)
                    return new RecordsParseResult.Invalid(
                        $"records[{index}].record recordType \"{ModalRecordType}\" has componentKind \"{componentKind}\", " +
                        $"only \"{ModalComponentKind}\" is supported.");

                // record_common_required_fields (docs/design/react-schema-topology-seed-translator-ssot.yaml
                // topology_ui_seed_contract.record_common_required_fields, mirrored at runtime so a
                // persisted layout_schema_json that drifted from the translator-validated shape
                // still fails close here rather than silently composing with fallback content):
                // label and sourceReactPath are required non-empty strings. sourceYamlRefs must be
                // a non-empty array — the translator's own validate_seed_record_tree
                // (SEED_RECORD_EMPTY_SOURCE_YAML_REFS) rejects an empty sourceYamlRefs the same
                // way it rejects a missing one, so the runtime check matches that exactly rather
                // than being more permissive than the translator it mirrors.
                if (string.IsNullOrWhiteSpace(label))
                    return new RecordsParseResult.Invalid($"records[{index}].record is missing a non-empty label.");
                if (!record.TryGetProperty("sourceReactPath", out var srp) || srp.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(srp.GetString()))
                    return new RecordsParseResult.Invalid($"records[{index}].record is missing a non-empty sourceReactPath.");
                if (!record.TryGetProperty("sourceYamlRefs", out var syr) || syr.ValueKind != JsonValueKind.Array ||
                    syr.GetArrayLength() == 0)
                    return new RecordsParseResult.Invalid($"records[{index}].record is missing a non-empty sourceYamlRefs array.");

                if (!record.TryGetProperty("knownGapRefs", out var kgr) || kgr.ValueKind != JsonValueKind.Array)
                    return new RecordsParseResult.Invalid($"records[{index}].record is missing a knownGapRefs array.");
                var knownGapRefs = new List<string>();
                foreach (var refEl in kgr.EnumerateArray())
                {
                    if (refEl.ValueKind == JsonValueKind.String && refEl.GetString() is { } refValue)
                        knownGapRefs.Add(refValue);
                }
                if (recordType == UnresolvedRecordType && (knownGapRefs is null || knownGapRefs.Count == 0))
                    return new RecordsParseResult.Invalid(
                        $"records[{index}].record.recordType \"{UnresolvedRecordType}\" is missing a non-empty knownGapRefs array.");

                rows.Add(new SchemaRecordRow(recordType!, key!, parentKey, label, control, display, knownGapRefs, componentKind));
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
            if (row.RecordType == ModalRecordType) continue;
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
    /// Discriminates a successfully-grouped interactions map (Valid) from a malformed/
    /// unattributable tensor runtimeInteractions shape (Invalid — a real authoring defect that
    /// must surface as an explicit failure, never be silently skipped). See
    /// docs/design/runtime-orchestration-ssot.yaml
    /// ui_projection_render_reachability_contract.layout_schema_structural_render_contract
    /// tensor_runtime_interactions_merge.
    /// </summary>
    public abstract record InteractionsParseResult
    {
        private InteractionsParseResult() { }
        public sealed record Valid(IReadOnlyDictionary<string, string> BySourceActionKey) : InteractionsParseResult;
        public sealed record Invalid(string Reason) : InteractionsParseResult;
    }

    /// <summary>
    /// Groups tensor nodes' (layout_patch_json.nodes[]) runtimeInteractions entries by
    /// "{formTensorNodeId}::{sourceActionKey}" — each tensor node carries its child Action/Field
    /// leaves' entries at the FORM level, individually tagged by which leaf they belong to.
    /// Scoping the map key by the OWNING FORM's own tensor NodeId (not sourceActionKey alone)
    /// prevents cross-contamination when two different Forms happen to author the same leaf key
    /// (e.g. two Forms both authoring an Action named "validate") — each Form's entries stay
    /// attributed only to its own children, never merged across Forms. Malformed JSON, a
    /// non-array runtimeInteractions value, a non-object entry, or an entry missing a non-empty
    /// sourceActionKey is a real authoring defect — never silently skipped — and returns Invalid.
    /// Valid returns a map from "{formTensorNodeId}::{sourceActionKey}" to the JSON array text of
    /// that key's own entries (usually one) — see Compose's ResolveInteractionsMergeKey for the
    /// matching leaf-side key construction.
    /// </summary>
    public static InteractionsParseResult BuildInteractionsBySourceActionKey(
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
            catch (JsonException ex)
            {
                return new InteractionsParseResult.Invalid(
                    $"node '{node.NodeId}' runtimeInteractions is not valid JSON: {ex.Message}");
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return new InteractionsParseResult.Invalid(
                        $"node '{node.NodeId}' runtimeInteractions is present but is not a JSON array (found {doc.RootElement.ValueKind}).");

                var entries = doc.RootElement.EnumerateArray().ToList();
                for (var index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    if (entry.ValueKind != JsonValueKind.Object)
                        return new InteractionsParseResult.Invalid(
                            $"node '{node.NodeId}' runtimeInteractions[{index}] is not a JSON object.");
                    if (!entry.TryGetProperty("sourceActionKey", out var sak) ||
                        sak.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(sak.GetString()))
                        return new InteractionsParseResult.Invalid(
                            $"node '{node.NodeId}' runtimeInteractions[{index}] is missing a non-empty sourceActionKey.");
                    var sourceActionKey = sak.GetString()!;
                    var scopedKey = $"{node.NodeId}::{sourceActionKey}";

                    if (!bySourceActionKey.TryGetValue(scopedKey, out var list))
                    {
                        list = new List<JsonElement>();
                        bySourceActionKey[scopedKey] = list;
                    }
                    list.Add(entry.Clone());
                }
            }
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (scopedKey, entries) in bySourceActionKey)
            result[scopedKey] = JsonSerializer.Serialize(entries);
        return new InteractionsParseResult.Valid(result);
    }

    /// <summary>
    /// Node-local propsJson/stateJson/propBindings/dispatchPayloadFromByTrigger JSON string
    /// quadruple, keyed by the exact tensor nodeId. Distinct from
    /// BuildInteractionsBySourceActionKey's sourceActionKey-scoped map: these fields describe a
    /// single node's own static/bound configuration (table columns, propBindings reading
    /// emission.data, this SAME node's own admin_runtime dispatch payload binding, etc.) rather
    /// than a form's collection of child leaves' interaction entries, so the match key is the
    /// tensor node's own NodeId directly, not "{formNodeId}::{sourceActionKey}".
    /// </summary>
    public readonly record struct NodeLocalData(
        string? PropsJson,
        string? StateJson,
        string? PropBindingsJson,
        string? DispatchPayloadFromByTriggerJson = null,
        string? DispatchTargetRefByTriggerJson = null,
        /// <summary>
        /// round 37 (search_filter_input_contract debounce_policy): this node's own declared
        /// debounceMs from the tensor's layout_patch_json.nodes[].debounceMs. Null when not
        /// authored.
        /// </summary>
        int? DebounceMs = null);

    /// <summary>
    /// Builds a NodeId -> NodeLocalData map from the tensor's own layout_patch_json.nodes[]
    /// entries, for schema-composed leaves to merge onto by exact NodeId match (see Compose).
    /// A tensor node with none of the fields set is simply absent from the result — there
    /// is nothing to attach. This is purely additive seed authoring: a schema-composed layout
    /// with no matching tensor node entries composes exactly as before (all fields null on
    /// every leaf), so this never changes behavior for existing layouts that only use tensor
    /// nodes for runtimeInteractions.
    /// </summary>
    public static IReadOnlyDictionary<string, NodeLocalData> BuildNodeLocalDataByNodeId(
        IReadOnlyList<LayoutNodeRecord> tensorNodes)
    {
        var result = new Dictionary<string, NodeLocalData>(StringComparer.Ordinal);
        foreach (var node in tensorNodes)
        {
            if (node.PropsJson is null && node.StateJson is null && node.PropBindingsJson is null &&
                node.DispatchPayloadFromByTriggerJson is null && node.DispatchTargetRefByTriggerJson is null &&
                node.DebounceMs is null)
                continue;
            result[node.NodeId] = new NodeLocalData(
                node.PropsJson, node.StateJson, node.PropBindingsJson, node.DispatchPayloadFromByTriggerJson,
                node.DispatchTargetRefByTriggerJson, node.DebounceMs);
        }
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
    /// BuildInteractionsBySourceActionKey) by "{parentKey}::{key}" — scoped to the leaf's OWNING
    /// FORM, never by leaf key alone, so two different Forms authoring the same leaf key never
    /// cross-contaminate each other's interactions. Order follows the authored document order
    /// (already parent-before-child).
    ///
    /// NodeId is normally the record's own authored key; when two records anywhere in the tree
    /// share the same key (an authoring collision — the record tree's key is only guaranteed
    /// unique within its own branch, not globally), each is namespaced as
    /// "{parentKey}::{key}" instead so the composed list never violates the LAYOUT_PATCH_DUPLICATE_NODE_ID
    /// invariant StructureMapResolver.ValidateLayoutNodes already enforces.
    ///
    /// ParentNodeId resolution walks records in authored document order and tracks each key's
    /// MOST RECENTLY composed NodeId so far — never a static whole-tree key lookup — so a child
    /// whose parentKey happens to collide with another branch's container (a duplicated
    /// container key) still attaches to the SAME physical container instance it was actually
    /// nested under, not to whichever duplicate happens to match by key. ParentNodeId is nulled
    /// (root) when a record's authored parentKey has not been composed yet at this point in the
    /// document — the implicit/virtual tree root the translator emits ($.root) is never itself a
    /// record.
    /// </summary>
    /// <summary>
    /// One schema record's resolved identity: its NodeId (duplicate-key-namespaced exactly as
    /// Compose composes it), its resolved ParentNodeId, its structural/unresolved/catalog-leaf
    /// classification, and — for a catalog leaf other than Modal — its canonical control/display
    /// convention componentKey (ResolveComponentKey), all computed by the single walk in
    /// ResolveNodeIdentities. Row is carried through so callers needing the full record (Compose)
    /// don't need a second lookup back into schemaRecords.
    /// </summary>
    private readonly record struct ResolvedNodeIdentity(
        SchemaRecordRow Row,
        string ResolvedNodeId,
        string? ParentNodeId,
        bool IsStructural,
        bool IsUnresolved,
        bool IsCatalogLeaf,
        string? ComponentKey);

    /// <summary>
    /// Single authority for schema-tree NodeId/ParentNodeId resolution (including the
    /// duplicate-key "{resolvedParentNodeId}::{key}" namespacing) and each catalog leaf's own
    /// canonical control/display -&gt; ui_component_registry.component_key convention lookup
    /// (ResolveComponentKey) — shared by Compose (render-time full composition, which
    /// additionally resolves componentId/componentKind via registry lookups and merges
    /// runtimeInteractions/local tensor data) and ResolveCatalogComponentKeysByNodeId
    /// (persistence-time identity resolution for a schema-composed layout's tensor override
    /// deltas — see NpgsqlUiTopologyRepository.ValidateLayoutPatchAsync). Walking this identically
    /// in both places — one implementation, not two mirrors kept in sync by hand — is what
    /// guarantees the two callers' NodeId/componentKey semantics can never drift apart.
    /// </summary>
    private static List<ResolvedNodeIdentity> ResolveNodeIdentities(IReadOnlyList<SchemaRecordRow> schemaRecords)
    {
        var duplicateKeys = new HashSet<string>(
            schemaRecords.GroupBy(r => r.Key, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key),
            StringComparer.Ordinal);

        // Namespaces a duplicated key by the child's RESOLVED parent identity (never the raw
        // authored parentKey) — a duplicated child key under a duplicated PARENT key would
        // otherwise namespace to the SAME "{rawParentKey}::{key}" string in every branch (e.g.
        // two Forms both keyed "shared_section" each having their own Field keyed "shared_field"
        // would both resolve to "shared_section::shared_field"), silently colliding instead of
        // staying attached to the actual instance each was nested under.
        string ResolveNodeId(SchemaRecordRow row, string? resolvedParentNodeId) =>
            duplicateKeys.Contains(row.Key) ? $"{resolvedParentNodeId}::{row.Key}" : row.Key;

        var result = new List<ResolvedNodeIdentity>(schemaRecords.Count);

        // Running map: authored key -> the resolved NodeId of the most recently composed record
        // with that key, in document order (parent-before-child). See ParentNodeId resolution
        // note above.
        var lastResolvedNodeIdByKey = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in schemaRecords)
        {
            var isStructural = StructuralRecordTypes.Contains(row.RecordType);
            var isUnresolved = row.RecordType == UnresolvedRecordType;
            var isCatalogLeaf = !isStructural && !isUnresolved;

            var parentNodeId = row.ParentKey is not null && lastResolvedNodeIdByKey.TryGetValue(row.ParentKey, out var resolvedParentId)
                ? resolvedParentId
                : null;

            var resolvedNodeId = ResolveNodeId(row, parentNodeId);
            lastResolvedNodeIdByKey[row.Key] = resolvedNodeId;

            // Modal never resolves via the control/display convention table (it carries its own
            // literal componentKind, see Compose) — matches Compose's own branching, which never
            // calls ResolveComponentKey for a Modal row either.
            string? componentKey = isCatalogLeaf && row.RecordType != ModalRecordType
                ? ResolveComponentKey(row)
                : null;

            result.Add(new ResolvedNodeIdentity(row, resolvedNodeId, parentNodeId, isStructural, isUnresolved, isCatalogLeaf, componentKey));
        }
        return result;
    }

    /// <summary>
    /// Persistence-boundary identity resolution (round 40): maps each schema-composed catalog
    /// leaf's resolved NodeId -&gt; its canonical control/display convention componentKey, via the
    /// SAME ResolveNodeIdentities NodeId/duplicate-key/parent-tracking walk Compose itself uses —
    /// there is no second definition of either the NodeId or componentKey resolution rule to
    /// drift out of sync with Compose's own render-time resolution.
    ///
    /// Used by NpgsqlUiTopologyRepository.ValidateLayoutPatchAsync to resolve a schema-composed
    /// layout's ui_topology_tensor.layout_patch_json.nodes[] override-delta entries — which, per
    /// this file's own header doc, carry neither componentKind nor componentKey themselves,
    /// identity for them living only in components_layout_design.layout_schema_json.records[] —
    /// back to the SAME Field-family componentKey vocabulary (FieldFamilyComponentKeys /
    /// SearchInputFieldComponentKey) already checked for tensor-only-authored nodes, so the SAME
    /// two save-time validators (read-action allowlist, search debounceMs) apply with identical
    /// meaning to both authoring modes.
    ///
    /// Modal rows are never included (no control/display convention applies to them — see
    /// ResolveNodeIdentities). A catalog leaf whose control/display has no recognized convention
    /// mapping is simply absent from the result — Compose already fails that shape closed with
    /// CATALOG_COMPONENT_KIND_REQUIRED at render time; this method does not re-diagnose it.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ResolveCatalogComponentKeysByNodeId(
        IReadOnlyList<SchemaRecordRow> schemaRecords)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var identity in ResolveNodeIdentities(schemaRecords))
        {
            if (identity.IsCatalogLeaf && identity.ComponentKey is not null)
                result[identity.ResolvedNodeId] = identity.ComponentKey;
        }
        return result;
    }

    public static IReadOnlyList<LayoutNodeRecord> Compose(
        IReadOnlyList<SchemaRecordRow> schemaRecords,
        IReadOnlyDictionary<string, string> interactionsBySourceActionKey,
        IReadOnlyDictionary<string, string> componentKeyToId,
        IReadOnlyDictionary<string, string> componentIdToKind,
        IReadOnlyDictionary<string, NodeLocalData>? nodeLocalDataByNodeId = null)
    {
        var identities = ResolveNodeIdentities(schemaRecords);
        var result = new List<LayoutNodeRecord>(schemaRecords.Count);
        var orderIndex = 0;

        foreach (var identity in identities)
        {
            var row = identity.Row;
            var isStructural = identity.IsStructural;
            var isUnresolved = identity.IsUnresolved;
            var isCatalogLeaf = identity.IsCatalogLeaf;
            var parentNodeId = identity.ParentNodeId;
            var resolvedNodeId = identity.ResolvedNodeId;
            string? componentId = null;
            string? componentKind = null;

            if (row.RecordType == ModalRecordType)
            {
                // No ui_component_registry lookup: the record already carries its own literal
                // componentKind (validated non-empty in ParseRecords) -- there is no component_key
                // convention to resolve. ComponentId is still required (round 25 fix): frontend/
                // runtime/runtimeComponentAdapter.ts adaptComponentDataHub fails closed with
                // RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_ID on an empty/null componentId
                // BEFORE componentKind is even inspected -- a null ComponentId here would silently
                // turn every Modal node into an "error" ComponentSpec at render time (never
                // reaching modalFactory at all), caught by round 25's own DOM mount test. Assigned
                // below once resolvedNodeId exists; component_operation_event_log.component_id is
                // plain TEXT (no FK to ui_component_registry), so a non-registry-backed identifier
                // is a legitimate value, not a type mismatch.
                componentKind = row.ComponentKind;
            }
            else if (isCatalogLeaf)
            {
                var componentKey = identity.ComponentKey;
                if (componentKey is not null && componentKeyToId.TryGetValue(componentKey, out var resolvedId))
                {
                    componentId = resolvedId;
                    componentIdToKind.TryGetValue(resolvedId, out componentKind);
                }
            }

            // Merge target is a catalog_component leaf only, keyed by "{resolvedParentNodeId}::{key}"
            // — scoped to the leaf's owning FORM's RESOLVED identity (see
            // BuildInteractionsBySourceActionKey, whose tensor-side key uses the same resolved
            // owning_form_key the translator emits) — never the leaf's raw authored parentKey
            // alone. A raw-parentKey key would collide when the OWNING FORM's key is itself
            // duplicated across branches (e.g. two Forms both keyed "shared_section", each with
            // its own Action keyed "shared_action") — using the resolved parent identity keeps
            // each duplicate branch's interactions attributed only to its own leaf. structural_node
            // and unresolved_gap nodes never receive runtimeInteractions.
            string? runtimeInteractionsJson = null;
            if (isCatalogLeaf)
                interactionsBySourceActionKey.TryGetValue($"{parentNodeId}::{row.Key}", out runtimeInteractionsJson);

            var nodeKind = isUnresolved ? "unresolved_gap" : isStructural ? "structural_node" : "catalog_component";
            if (row.RecordType == ModalRecordType)
                componentId = resolvedNodeId;

            NodeLocalData? localData = isCatalogLeaf && nodeLocalDataByNodeId is not null &&
                nodeLocalDataByNodeId.TryGetValue(resolvedNodeId, out var matchedLocalData)
                ? matchedLocalData
                : null;

            result.Add(new LayoutNodeRecord(
                NodeId: resolvedNodeId,
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
                PropsJson: localData?.PropsJson,
                StateJson: localData?.StateJson,
                PropBindingsJson: localData?.PropBindingsJson,
                DispatchPayloadFromByTriggerJson: localData?.DispatchPayloadFromByTriggerJson,
                DispatchTargetRefByTriggerJson: localData?.DispatchTargetRefByTriggerJson,
                DebounceMs: localData?.DebounceMs,
                RecordType: (isStructural || isUnresolved) ? row.RecordType : null,
                // Every schema record carries an authored label (record_common_required_fields)
                // — a catalog_component leaf's own label must survive composition the same way a
                // structural_node's does, so the frontend can build real production default
                // props from it instead of a hardcoded placeholder caption or the bare NodeId.
                Label: row.Label,
                KnownGapRefs: isUnresolved ? row.KnownGapRefs : null
            ));
        }

        return result;
    }
}
