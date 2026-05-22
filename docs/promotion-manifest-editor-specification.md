# Promotion manifest editor specification

## 1. Scope

The Promotion manifest editor is a controlled manifest editing surface for promotion metadata.

It is not:

- a generic CRUD campaign editor
- a direct DB editor
- an ad operations console
- a runtime execution screen
- a production campaign manager

Its role is to allow admins to edit promotion manifest intent — drafting, validating, and promoting
manifests while respecting topology layer boundaries. No runtime is executed from this editor.
No ad serving is triggered. No production campaign data is managed here.

## 2. Architecture Position

Layer boundary:

```text
DB         = semantic topology space
Backend    = abstract runtime / function execution space
Frontend   = physical projection space
Registrar  = controlled topology registration boundary
Promotion  = controlled manifest editing boundary for promotion metadata
```

The Promotion manifest editor submits manifest editing intent to the backend.
It does not execute runtime directly and does not serve ads.
It consumes topology references (packages, schemas, components) from the DB layer via the backend.
It does not write to topology structures — it only references them.

## 3. Manifest Responsibility

Promotion manifests represent:

- promotion metadata
- campaign-like display intent
- disclosure text intent
- eligible topology targets
- package/schema/component references
- validation rules for promotion safety
- activation/promotion state

These are conceptual definitions. No UI components and no runtime screens are defined here.
Manifests are authored as structured intent — they do not directly produce runtime output.

## 4. Managed Objects

Admin-managed manifest objects:

- promotion manifest draft
- promotion manifest version
- target topology references
- disclosure metadata
- display placement intent
- activation policy
- validation result

These are the objects an admin can view or edit through the Promotion manifest editor.
Each object is subject to validation before it can be promoted to an active state.

## 5. Object Boundaries

### Promotion manifest draft

- purpose: capture initial manifest intent before validation
- editable fields: manifest key, version label, target topology refs, disclosure text intent, display placement intent, activation policy
- immutable or guarded: manifest id (system-assigned), creation timestamp, promoted state
- required references: at least one valid target topology ref
- validation conditions: all required fields present, no duplicate manifest key/version, all refs resolvable
- promotion/activation conditions: must pass full validation, disclosure metadata must be present and explicit

### Promotion manifest version

- purpose: represent a validated, versioned snapshot of a manifest draft
- editable fields: none (versions are immutable once created)
- immutable or guarded: all fields
- required references: same as draft at time of versioning
- validation conditions: inherited from draft validation
- promotion/activation conditions: version must exist and be valid; activation applies the version to runtime consumption

### Target topology references

- purpose: bind a manifest to topology packages, schemas, and components
- editable fields: package ref, schema ref, component ref
- immutable or guarded: resolved ref ids (assigned on validation)
- required references: valid package, schema, and component that exist in the DB topology layer
- validation conditions: each ref must resolve; broken refs are explicit validation errors
- promotion/activation conditions: all refs must resolve before promotion

### Disclosure metadata

- purpose: carry required disclosure text intent for the promotion
- editable fields: disclosure text, disclosure type/category label
- immutable or guarded: disclosure intent is required — it cannot be removed from an active manifest
- required references: none beyond the manifest itself
- validation conditions: disclosure text must be present and non-empty
- promotion/activation conditions: disclosure metadata must be present; promotion is blocked without it

### Display placement intent

- purpose: express where and how the promotion should be projected
- editable fields: placement key, projection surface type
- immutable or guarded: placement key once assigned to an active manifest version
- required references: placement key must match a valid topology target
- validation conditions: placement key must exist in topology
- promotion/activation conditions: valid placement key required

### Activation policy

- purpose: define when and how a manifest version becomes active
- editable fields: policy type, condition expressions (intent only, not runtime code)
- immutable or guarded: policy is locked after activation
- required references: none beyond the manifest
- validation conditions: policy type must be a known valid type
- promotion/activation conditions: policy must be valid and explicitly set

### Validation result

- purpose: carry structured validation output for a manifest draft
- editable fields: none (system-produced)
- immutable or guarded: all fields
- required references: manifest draft id
- validation conditions: N/A
- promotion/activation conditions: must be present and pass before promotion is allowed

## 6. Draft / Validate / Promote Flow

The safe editor workflow:

```text
Draft manifest
→ Validate refs and disclosure requirements
→ Preview projection contract
→ Promote manifest version
→ Runtime can consume only validated manifest metadata through normal route
```

Each step is explicit. No step is skipped automatically.

- Broken refs must remain explicit validation errors. No silent repair.
- No auto-promotion. Admin must explicitly trigger each state transition.
- No ad-serving side effect occurs during editing or validation.
- Preview shows disclosure surface and resolved topology refs — not live runtime output.

## 7. Validation Model

Validation classes:

- missing required field
- duplicate manifest key/version
- missing referenced package/schema/component
- missing disclosure metadata
- invalid activation policy
- unsafe promotion
- invalid target topology ref

Validation errors must be surfaced as structured errors, not silent UI warnings.
Each error must identify the field, the expected condition, and the actual state.
The backend produces the validation result; the frontend displays it.

## 8. Disclosure Policy

Disclosure metadata is required manifest intent, not optional UI decoration.

Required meaning:

- disclosure text must be explicit
- promotion state must not hide required disclosure
- preview must show disclosure surface
- runtime must not receive active promotion metadata without disclosure intent

Disclosure is a structural requirement of the manifest, not a compliance detail added later.
Manifests without explicit disclosure metadata cannot be promoted.

This specification does not implement legal or compliance logic.
That is a future implementation concern outside this boundary.

## 9. Frontend Projection Policy

The editor UI is a projection of promotion manifest draft/validated state.

Required meaning:

- frontend displays manifest draft/validation state
- frontend may hold local edit vectors/hooks
- frontend must not become source of truth
- DOM may carry stable ids/data attributes as physical projection references
- backend/DB remains source of truth for promotion manifests

The frontend submits editing intent to the backend.
It does not store the authoritative manifest state.
Local edit state is transient — it is not persisted without a backend round-trip.

## 10. Backend Boundary Policy

Backend responsibility:

- accept manifest editing intent
- validate topology refs and disclosure requirements
- produce structured validation result
- persist only valid draft/promoted manifest states

The backend does not execute runtime in response to manifest edits.
It does not serve ads or inject promotion data into emission directly.
Endpoint design is a future implementation issue.

## 11. Runtime Boundary Policy

Runtime relationship to promotion manifests:

- runtime consumes validated manifest metadata only
- editor does not execute runtime
- editor does not inject promotion data directly into emission
- broken manifest refs are explicit errors
- no fallback promotion manifest is created automatically

The canonical runtime route is not altered by this editor:

```text
stored_topology_data → user_operation → operation_vector → attractor_resolve
→ structure_map_resolve → package_resolve → schema_resolve → component_expand
→ emission_or_projection
```

Promotion manifest metadata enters only at the stored_topology_data layer,
via the backend persistence boundary, after validation and explicit promotion.

## 12. Out of Scope

The following are explicitly out of scope for this specification:

- editor UI implementation
- backend endpoint implementation
- DB migration
- auth/permission implementation
- browser E2E tests
- real domain data
- production campaign manager
- ad serving implementation
- payment/reward implementation
- registrar admin UI implementation

No implementation files are added by this specification.

## 13. Implementation Work Items

After this specification is accepted, the following implementation issues may follow:

- Promotion manifest draft schema / DB tables
- Promotion manifest validation service
- Promotion manifest editor route boundary
- Promotion preview projection
- Promotion activation operation
- Disclosure preview component

These are implementation work items tracked outside this static specification.
