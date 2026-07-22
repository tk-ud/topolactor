# Seed Data Authoring Guide and CRUD UI Reference

## Document Status

- Classification: authoring guide / reference template
- Authority: non-SSOT
- Applies to: seed data authoring, translator output adoption, canonical persistence comparison, and future UI Builder preset design
- Does not define:
  - persistence schema
  - runtime lane
  - dispatch protocol
  - operation authority
  - component catalog identity
  - backend function or table authority
  - Bundle scope or completion status

Applicable SSOT documents remain the design authority. This document explains how to compare and adopt seed-generated data without treating translator output, existing seed precedent, or implementation behavior as design authority.

---

## 1. Purpose

Seed-generated UI topology must preserve the same persisted shape and semantics as the corresponding live authoring carrier when that carrier exists.

The comparison target is the data written by `/admin/contents` or `/admin/ui-builder`, not the React implementation, hook, API helper, or authoring interaction sequence used to produce it.

```text
/admin/contents authoring
        ↓
contents-owned persisted data

/admin/ui-builder authoring
        ↓
layout-patch / node-interaction persisted data

translator
        ↓
draft adoption candidates
        ↓
validated adoption into the applicable persisted carriers
```

A translator candidate is not active topology and is not execution authority by itself.

---

## 2. Core Judgment Rule

Do not make a single repository-wide claim that every seed artifact must be byte-identical to one UI Builder output object.

Judge each persisted carrier independently.

### 2.1 Live-authoring canonical carrier

When Contents or UI Builder writes and later reads the same carrier, seed adoption must conform to that carrier's field shape, reference boundary, and semantics.

Examples:

- Contents-owned logical table and operation binding data
- `ui_topology_tensor.layout_patch_json.nodes[]`
- node-level `runtimeInteractions[]`
- node props, state, prop bindings, calculation bindings, placement, and layout class references

### 2.2 Translator canonical carrier

A translator-specific structure may be valid when:

- its owning SSOT defines the exchange shape;
- a persistence owner is identified;
- a validator exists;
- an actual reader or composer consumes it; and
- it is not falsely described as UI Builder-produced data.

`components_layout_design.layout_schema_json.records[]` is an example of a translator/seed structural tree that may be consumed independently of the ordinary tensor-only UI Builder path.

### 2.3 Persistence-only or consumer-unverified data

A field being present in a table does not prove runtime authority.

When no reader or runtime consumer is proven:

- classify it as `persistence_only` or `consumer_unverified`;
- do not use it as implementation-completion evidence;
- do not infer dispatch reachability from its presence; and
- do not delete or repurpose it without an owning design decision.

`ui_wiring_registry.wiring_schema_json` must not be treated as runtime wiring completion evidence unless a concrete consumer is proven.

---

## 3. Required Carrier Classification

For every seed or translator adoption, classify each relevant structure using one or more of the following labels.

| Classification | Meaning |
|---|---|
| `live_authoring_canonical` | Written by a live Contents/UI Builder flow and consumed through the same product contract |
| `translator_canonical` | Defined by translator SSOT and consumed by an identified adoption/runtime component |
| `runtime_consumed` | Read by runtime, composer, validator, dispatcher, or projection code |
| `persistence_only` | Stored but not used as runtime authority |
| `consumer_unverified` | A reader or runtime effect has not been proven |
| `unresolved_contract` | Required semantics are known, but canonical field/action/target vocabulary is not yet defined |

One structure may carry multiple labels. For example, `runtimeInteractions[]` can be both `live_authoring_canonical` and `runtime_consumed`.

---

## 4. Canonical Conformance and Runtime Reachability

These are independent audit axes.

### Axis A: Canonical JSON Conformance

Confirm:

- persistence target
- JSON field shape
- identity ownership
- reference direction
- node and Action granularity
- payload binding shape
- output/projection binding shape
- mutation authority source
- confirmation and idempotency fields
- fail-close behavior

### Axis B: Runtime Reachability

Confirm:

- event emission
- payload resolution
- dispatch selection
- manifest or operation authorization
- backend execution
- mutation evidence
- response projection
- component state or prop update
- audit evidence

### Judgment Rules

- Runtime failure does not prove JSON nonconformance.
- Runtime success does not prove canonical conformance.
- A stored object with no consumer is not a runtime wiring proof.
- A translator candidate passing translator validation is not active-topology proof.
- Report both axes independently as `pass`, `partial`, `fail`, or `unverified`.

---

## 5. Translator and Adoption Boundary

The translator is an exchange translator that generates canonical adoption candidates according to its own SSOT.

It is not the authority for:

- active topology writes
- backend runtime routes
- manifest authorization
- database identity issuance
- final component registry resolution
- final persistence normalization

### Required

- Keep input authoring notation separate from persisted output.
- Resolve component and operation identities from applicable authority surfaces.
- Separate package, layout, style, wiring, tensor, and manifest-reference responsibilities.
- Preserve node-level Action granularity.
- Preserve canonical payload and output binding shapes.
- Keep unresolved contracts explicit and fail closed.
- Verify adopted data against the actual storage consumer, not only translator validation.
- Provide regeneration evidence or deterministic comparison evidence where practical.

### Prohibited

- Treating translator output as active topology by itself
- Treating an existing seed as design authority
- Introducing a seed-only runtime schema
- Embedding UI payload trees directly into manifest reference topology
- Deriving table or function authority from frontend payload
- Generating per-surface backend handlers or switch branches
- Treating `wiring_schema_json` presence as dispatch completion
- Treating runtime reachability failure as proof that the seed shape is invalid

---

## 6. Runtime Interaction Identity

`runtimeInteractionId` is persistence-boundary identity.

- Do not generate it in translator input, translator templates, or seed candidates unless the owning SSOT is explicitly changed.
- Live UI Builder apply may assign it at the backend persistence boundary.
- An id-less seed interaction may use a documented positional fallback, but that fallback does not make the stored result structurally identical to a freshly applied live-authoring result.
- When raw SQL adoption bypasses the identity-assignment boundary, separately determine whether normalization, migration, or backfill is required.

The absence of `runtimeInteractionId` in a translator candidate is not by itself a translator defect.

---

## 7. Contents Comparison

For Contents-owned data, compare seed-persisted data directly with an actual `/admin/contents`-produced object for the same semantic screen.

At minimum, verify the applicable equivalents of:

- topology manifest identity and references
- topology system identity
- logical tables and columns
- physical table references
- operation kinds
- operation/entity bindings
- search keys and display columns
- relation intents
- aggregation definitions
- screen data shape
- initial data and lineage

Do not persist seed-only authorities such as `crudMode`, `targetTable`, or `buttonOperations` when the same meaning belongs to an existing Contents-owned structure.

This document intentionally does not invent a replacement Contents schema. The exact comparison sample must come from the live authoring output or the owning SSOT.

---

## 8. UI Builder Comparison

### 8.1 Layout / Design

Compare:

- `nodeId`
- `nodeKind`
- component reference
- `parentNodeId`
- `slotKey`
- `orderIndex`
- size and placement fields
- layout class references
- props
- state
- prop bindings
- calculation bindings

### 8.2 Wiring / Interaction

Compare node-level `runtimeInteractions[]` fields such as:

- `trigger`
- `actionType`
- target node or typed target reference
- `statePath`
- `payloadFrom`
- `outputProp`
- side-effect policy
- debounce policy
- lifecycle confirmation
- idempotency policy
- backend-assigned runtime interaction identity

The Wiring Canvas graph is an authoring projection over node interactions. The graph itself is not an independent persistence authority.

---

## 9. CRUD Semantic Reference

The YAML below is a non-persistable semantic reference. Known UI-local interactions use existing UI Builder-shaped `runtimeInteractions`. Backend CRUD interactions remain explicit unresolved contracts until their canonical action and target vocabulary are defined.

```yaml
crudUiReference:
  metadata:
    templateKey: crud-ui-reference
    classification: reference
    authority: non-SSOT
    futureUse:
      - seed authoring example
      - translator fixture design
      - future UI Builder CRUD preset convergence

  authoringIntent:
    operations:
      - list
      - create
      - update
      - delete
    logicalEntityRef: "<resolve-from-contents-authority>"
    fields:
      - key: id
        role: identity
      - key: name
        role: editable_value
      - key: category
        role: candidate_selection
      - key: enabled
        role: boolean_value

  uiBuilderLayoutPatchReference:
    nodes:
      - nodeId: crud-root
        nodeKind: structural_node
        parentNodeId: null
        slotKey: root
        orderIndex: 0
        runtimeInteractions: []

      - nodeId: create-open-button
        nodeKind: catalog_component
        componentKey: "<resolve-existing-button-component-key>"
        componentKind: form_input/button
        parentNodeId: crud-root
        slotKey: actions
        orderIndex: 0
        propsJson: '{"label":"Create"}'
        runtimeInteractions:
          - trigger: click
            actionType: openModal
            targetNodeId: create-modal
            statePath: open
            sideEffectNone: false

      - nodeId: create-modal
        nodeKind: catalog_component
        componentKey: "<resolve-existing-modal-component-key>"
        componentKind: disclosure/modal
        parentNodeId: crud-root
        slotKey: overlays
        orderIndex: 0
        propsJson: '{"title":"Create record"}'
        stateJson: '{"open":false}'
        runtimeInteractions: []

      - nodeId: create-name-input
        nodeKind: catalog_component
        componentKey: "<resolve-existing-text-input-component-key>"
        componentKind: "<resolve-existing-text-input-component-kind>"
        parentNodeId: create-modal
        slotKey: form
        orderIndex: 0
        propsJson: '{"label":"Name","autocomplete":true}'
        stateJson: '{"value":""}'
        runtimeInteractions: []

      - nodeId: create-category-select
        nodeKind: catalog_component
        componentKey: "<resolve-existing-select-component-key>"
        componentKind: "<resolve-existing-select-component-kind>"
        parentNodeId: create-modal
        slotKey: form
        orderIndex: 1
        propsJson: '{"label":"Category"}'
        stateJson: '{"value":null}'
        propBindings:
          options: "<resolve-canonical-candidate-projection-path>"
        runtimeInteractions: []

      - nodeId: create-enabled-checkbox
        nodeKind: catalog_component
        componentKey: "<resolve-existing-checkbox-component-key>"
        componentKind: "<resolve-existing-checkbox-component-kind>"
        parentNodeId: create-modal
        slotKey: form
        orderIndex: 2
        propsJson: '{"label":"Enabled"}'
        stateJson: '{"value":false}'
        runtimeInteractions: []

      - nodeId: create-submit-button
        nodeKind: catalog_component
        componentKey: "<resolve-existing-button-component-key>"
        componentKind: form_input/button
        parentNodeId: create-modal
        slotKey: actions
        orderIndex: 0
        propsJson: '{"label":"Add"}'
        runtimeInteractions:
          - trigger: click
            actionType: openDialog
            targetNodeId: create-confirm-dialog
            statePath: open
            sideEffectNone: false

      - nodeId: create-confirm-dialog
        nodeKind: catalog_component
        componentKey: "<resolve-existing-dialog-component-key>"
        componentKind: disclosure/dialog
        parentNodeId: crud-root
        slotKey: overlays
        orderIndex: 1
        propsJson: '{"title":"Confirm create"}'
        stateJson: '{"open":false}'
        runtimeInteractions: []

      - nodeId: search-input
        nodeKind: catalog_component
        componentKey: "<resolve-existing-search-input-component-key>"
        componentKind: "<resolve-existing-search-input-component-kind>"
        parentNodeId: crud-root
        slotKey: search
        orderIndex: 0
        propsJson: '{"label":"Search"}'
        stateJson: '{"value":""}'
        runtimeInteractions: []

      - nodeId: result-list
        nodeKind: catalog_component
        componentKey: "<resolve-existing-list-or-table-component-key>"
        componentKind: "<resolve-existing-list-or-table-component-kind>"
        parentNodeId: crud-root
        slotKey: results
        orderIndex: 0
        propsJson: '{"emptyLabel":"No records"}'
        stateJson: '{"items":[],"editingId":null}'
        propBindings:
          items: "<resolve-canonical-result-projection-path>"
        runtimeInteractions: []

      - nodeId: row-edit-action
        nodeKind: catalog_component
        componentKey: "<resolve-existing-edit-action-component-key>"
        componentKind: "<resolve-existing-action-component-kind>"
        parentNodeId: result-list
        slotKey: rowActions
        orderIndex: 0
        propsJson: '{"icon":"edit","label":"Edit"}'
        runtimeInteractions:
          - trigger: click
            actionType: setState
            targetNodeId: result-list
            statePath: editingId
            payloadFrom:
              value: event.row.id
            sideEffectNone: false

      - nodeId: row-delete-action
        nodeKind: catalog_component
        componentKey: "<resolve-existing-delete-action-component-key>"
        componentKind: "<resolve-existing-action-component-kind>"
        parentNodeId: result-list
        slotKey: rowActions
        orderIndex: 1
        propsJson: '{"icon":"delete","label":"Delete"}'
        runtimeInteractions:
          - trigger: click
            actionType: setState
            targetNodeId: delete-confirm-dialog
            statePath: candidateId
            payloadFrom:
              value: event.row.id
            sideEffectNone: false
          - trigger: click
            actionType: openDialog
            targetNodeId: delete-confirm-dialog
            statePath: open
            sideEffectNone: false

      - nodeId: delete-confirm-dialog
        nodeKind: catalog_component
        componentKey: "<resolve-existing-dialog-component-key>"
        componentKind: disclosure/dialog
        parentNodeId: crud-root
        slotKey: overlays
        orderIndex: 2
        propsJson: '{"title":"Confirm delete"}'
        stateJson: '{"open":false,"candidateId":null}'
        runtimeInteractions: []

  unresolvedBackendOperationContracts:
    list:
      triggerNodeId: search-button
      operationKind: list
      payloadIntent:
        query: node:search-input.value
      outputIntent:
        targetNodeId: result-list
        targetProp: items

    create:
      triggerNodeId: create-confirm-button
      operationKind: create
      payloadIntent:
        name: node:create-name-input.value
        category: node:create-category-select.value
        enabled: node:create-enabled-checkbox.value
      outputIntent:
        targetNodeId: result-list
        targetProp: items

    update:
      triggerNodeId: inline-edit-input
      operationKind: update
      payloadIntent:
        id: event.row.id
        column: event.column
        data: node:inline-edit-input.value
      outputIntent:
        targetNodeId: result-list
        targetProp: items

    delete:
      triggerNodeId: delete-confirm-button
      operationKind: delete
      payloadIntent:
        id: node:delete-confirm-dialog.candidateId
      outputIntent:
        targetNodeId: result-list
        targetProp: items

    unresolvedFields:
      - canonical backend dispatch actionType
      - manifest-authorized operation target reference
      - operation selector carrier
      - canonical response projection contract
      - preview / validate / explicit-confirm / write evidence contract
```

`unresolvedBackendOperationContracts` is authoring intent only. It must not be copied into persistence as a new runtime schema.

---

## 10. Seed Authoring Checklist

### Before authoring

- [ ] Read the applicable SSOT sections.
- [ ] Identify the owning Bundle.
- [ ] Capture an actual Contents-generated comparison object when Contents owns the semantics.
- [ ] Capture an actual UI Builder-generated layout patch when UI Builder owns the carrier.
- [ ] Identify package, layout, style, wiring, tensor, and manifest-reference owners.
- [ ] Identify every writer, validator, reader, composer, and runtime consumer.

### During authoring

- [ ] Keep translator input distinct from adoption output.
- [ ] Use existing component and operation identities.
- [ ] Preserve Action-level granularity.
- [ ] Preserve canonical payload sources.
- [ ] Keep mutation authority out of frontend payload.
- [ ] Record unresolved contracts instead of inventing vocabulary.
- [ ] Do not treat persistence-only data as runtime completion.

### After authoring

- [ ] Compare each persisted carrier independently.
- [ ] Classify each carrier using Section 3.
- [ ] Compare `runtimeInteractions[]` node by node.
- [ ] Verify identity assignment boundaries.
- [ ] Verify runtime reachability separately.
- [ ] Verify tests do not conflate shape conformance and execution.
- [ ] Record deviations and unresolved contracts explicitly.

---

## 11. Todo and Translator References

A todo involving seed or translator adoption should reference this guide and include:

- problem
- purpose
- improvement policy
- applicable SSOT and reference documents
- target files
- target functions
- target seed and translator fixture
- actual Contents comparison artifact
- actual UI Builder comparison artifact
- carrier classification
- canonical conformance proof
- runtime reachability proof
- unresolved contracts

Do not define completion as only "the seed SQL executed" or "translator validation passed."

---

## 12. Future UI Builder CRUD Preset Convergence

The CRUD semantic reference may become the design input for a future Bundle that revises UI Builder CRUD presets.

That future Bundle should:

1. inventory current CRUD preset outputs;
2. capture the exact live-authored persisted JSON;
3. compare current presets with this CRUD semantic reference;
4. define a reusable CRUD preset at Bundle scope rather than per-screen hardcoding;
5. preserve Contents-owned operation/entity authority;
6. generate node-level create, search, update, delete, confirmation, and result-projection interactions;
7. prove that preset-authored and manually UI-Builder-authored results converge to the same canonical carriers; and
8. test preview, validate, apply, runtime dispatch, response projection, and audit evidence.

This section records future direction only. It does not expand the current PR or Bundle scope.

---

## 13. Review Output

Report at minimum:

### Canonical JSON Conformance

- comparison artifacts
- carrier-by-carrier classification
- Contents-owned structure comparison
- UI Builder layout-patch comparison
- node / Action granularity comparison
- reference and authority comparison
- deviations

### Runtime Reachability

- event emission
- payload resolution
- dispatch and authorization
- execution
- response projection
- UI update
- mutation and audit evidence

### Classification

- `Implemented`
- `partial`
- `not_started`
- `blocked_by_design`
- `unverified`

Never collapse canonical conformance and runtime reachability into one unsupported judgment.
