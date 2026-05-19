# Registrar admin UI specification

## 1. Scope

The Registrar admin UI is a topology registration management surface.

It allows authorized administrators to manage registry-backed topology data
within the Topolactor architecture boundary.

The Registrar admin UI is not:

- generic CRUD admin
- direct DB editor
- ORM editor
- runtime execution screen
- production operations console

It is a controlled registration boundary for topology data and is not CRUD.
Administrators submit registration intent. The backend validates and persists
valid states. The runtime resolves topology through the canonical route — not
through the Registrar UI directly.

## 2. Architecture Position

The Registrar UI occupies a controlled registration boundary in the layer stack:

```text
DB        = semantic topology space
Backend   = abstract runtime / function execution space
Frontend  = physical projection space
Registrar = controlled registration boundary for topology data
```

The Registrar UI submits registration intent. It does not execute runtime directly.

Registry tensor interpretation:
- registry table is semantic matrix topology vocabulary basis (not a mere dictionary / metadata table / component catalog)
- registry ids compose sparse vector / tensor coordinates
- Registrar UI is a UI projection surface of registry tensor, not an authority for topology meaning decisions
- packageId / layoutId / wiringId are UI tensor axes
- CRUD wiring / CanDI wiring are wiring-axis projections over the same UI topology tensor (semantic matrix UI projection surface)

The canonical runtime route remains:

```text
stored_topology_data
→ user_operation
→ operation_vector
→ attractor_resolve
→ structure_map_resolve
→ package_resolve
→ schema_resolve
→ component_expand
→ emission_or_projection
```

The Registrar surface feeds into `stored_topology_data` only after successful
draft → validate → promote flow. It does not short-circuit or bypass any step.

## 2.5 Components Bucket → Package Generator → DB Save

The UI topology registration flow is strict and immediate:

```text
components bucket
→ package generator
→ ID issuance (componentId / packageId / layoutId / wiringId)
→ UI topology DB save
→ frontend projection target
```

- Components bucket is an unpackaged-component holding surface; bucket entries are not UI topology tensor entities yet.
- Components without issued `componentId` must not be connected to CRUD/CanDI/route/layout/wiring projection surfaces.
- Package generator promotes bucket entries into packageable topology units, issues required IDs, wires layout/wiring axes, and persists the result.
- Persisted rows in UI topology DB are the source of truth for projection; frontend projects DB topology definitions and does not judge topology meaning.
- Code-only component or code-only package is drift/GAP because it is detached from registry tensor / semantic matrix persistence.

## 3. Managed Objects

The Registrar admin UI manages the following topology registration objects at a
conceptual level:

- attractor definitions
- structure maps
- package registry entries
- schema registry entries
- component registry entries
- relation declarations
- validation rules / required refs

This list is conceptual. Final UI components and screen implementations are out
of scope for this specification.

## 4. Object Boundaries

### Attractor definitions

- **Purpose**: Define the key that anchors the canonical runtime route.
- **Editable**: attractor key, description, active flag, associated structure map reference.
- **Immutable/guarded**: attractor key after promotion (key changes invalidate runtime routing).
- **Required references**: at least one structure map reference.
- **Validation conditions**: key format must follow `namespace:domain:operation` pattern;
  referenced structure map must exist.
- **Promotion conditions**: structure map reference resolved; no duplicate key in active registry.

### Structure maps

- **Purpose**: Define how an operation vector resolves to packages, schemas, and components.
- **Editable**: map name, package ref, schema ref, component ref list, emission contract ref.
- **Immutable/guarded**: map key after promotion.
- **Required references**: at least one package ref, at least one schema ref.
- **Validation conditions**: all referenced packages, schemas, and components must exist in registry.
- **Promotion conditions**: all refs resolved; no broken structure map ref.

### Package registry entries

- **Purpose**: Group UI component packages for projection.
- **Editable**: package key, component list, display metadata.
- **Immutable/guarded**: package key after promotion.
- **Required references**: at least one component registry entry.
- **Validation conditions**: referenced components exist.
- **Promotion conditions**: all component refs resolved.

### Schema registry entries

- **Purpose**: Define the data and runtime contract shape for a topology operation.
- **Editable**: schema key, field definitions, required fields, type constraints.
- **Immutable/guarded**: schema key after promotion; field removal is guarded.
- **Required references**: none mandatory beyond the schema definition itself.
- **Validation conditions**: field types are valid; no conflicting field names.
- **Promotion conditions**: schema is structurally consistent.

### Component registry entries

- **Purpose**: Map a logical component key to a physical frontend component.
- **Editable**: component key, projection target, display metadata.
- **Immutable/guarded**: component key after promotion.
- **Required references**: referenced projection target must exist in frontend registry.
- **Validation conditions**: component key format valid; projection target resolvable.
- **Promotion conditions**: projection target confirmed resolvable.

### Relation declarations

- **Purpose**: Declare abstract relation axes that connect entities in topology space.
- **Editable**: relation name, axis type, ordering metadata, weight, active flag.
- **Immutable/guarded**: relation registry id after promotion.
- **Required references**: master ids must reference valid master entries.
- **Validation conditions**: no duplicate relation axis; referenced master ids exist.
- **Promotion conditions**: all master id refs resolved; relation axis is unique.

### Validation rules / required refs

- **Purpose**: Define structural constraints applied during draft validation.
- **Editable**: rule key, target object type, required field list, ref conditions.
- **Immutable/guarded**: rule key after promotion.
- **Required references**: target object type must exist in managed object set.
- **Validation conditions**: rule conditions are syntactically valid.
- **Promotion conditions**: rule does not conflict with existing promoted rules.

## 5. Draft / Validate / Promote Flow

Administrators follow a safe, explicit workflow:

```text
Draft registration
→ Validate refs
→ Preview projection/emission contract
→ Promote to active registry state
→ Runtime can resolve through normal route
```

Each stage is explicit. There is no automatic promotion or silent repair.

**Draft**: The admin creates or edits registration objects. The system stores
them in draft state. Draft objects are not visible to the runtime.

**Validate refs**: The backend validates all references in the draft — package
refs, schema refs, component refs, attractor key format, structure map links.
Broken refs are explicit validation errors. The backend returns a structured
validation result. Validation does not auto-repair missing refs.

**Preview projection/emission contract**: The admin can preview what the
projection and emission contract would look like if this draft were promoted.
The preview is read-only and does not modify registry state.

**Promote to active registry state**: After successful validation, the admin
explicitly triggers promotion. The backend persists the validated state to the
active registry. The runtime can then resolve through the canonical route.

**Runtime resolution**: Once promoted, the runtime resolves through the
normal canonical route. The Registrar UI has no further involvement.

## 6. Validation Model

The following validation classes apply during the validate-refs stage:

- **missing required field**: a required field on the registration object is absent.
- **duplicate key**: an attractor key, structure map key, package key, schema key,
  or component key already exists in the active registry.
- **missing referenced package/schema/component**: a reference to a package,
  schema, or component cannot be resolved in the registry.
- **broken structure map ref**: a structure map references an attractor, package,
  schema, or component that does not exist.
- **invalid attractor key format**: the attractor key does not follow the required
  `namespace:domain:operation` format.
- **unsafe promotion**: promotion is attempted while validation errors remain unresolved.

Validation errors are surfaced as structured errors. They are not silent UI
warnings. The admin must resolve all errors before promotion is permitted.

Broken refs are explicit validation errors. No silent fallback or auto-repair.

### Registry Vector Validation

In addition to structural validation, the Registrar backend applies vector neighbor
validation when an ID-array-based registry entry is drafted or validated:

- **duplicate_vector**: cosine >= duplicate_threshold (from policy) — blocking; reuse existing.
- **near_duplicate_vector**: cosine >= near_duplicate_threshold (from policy) — blocking or confirm.
- **related_existing_registry**: cosine >= related_threshold (from policy) — warning; consider reuse.
- **zero_vector**: empty ID array (zero norm) — explicit result; not a silent pass.
- **pass**: cosine < related_threshold — proceed.

Thresholds are read from `function_parameters` (`topology_vector_runtime.registry_validation`).
No threshold is hardcoded in runtime code.

The backend returns a `RegistryVectorValidationResult` containing:
- validation class
- whether the result is blocking
- nearest neighbor candidates (registry id, name, cosine score, matched ids, reason)

The Registrar UI projects this structured result. The UI does not compute cosine similarity.
Broken refs, malformed IDs, and DB unavailability return explicit errors — not silent fallback.

Duplicate key check (string equality) and duplicate vector check (cosine similarity) are
separate validation classes. Both may apply to the same registration attempt.

## 7. Frontend Projection Policy

The Registrar admin UI frontend is a projection of topology registration state.

```text
- frontend displays registry/draft state
- frontend may hold local edit vectors/hooks
- frontend must not become source of truth
- DOM may carry stable ids/data attributes as physical projection references
- backend/DB remains source of truth for topology registration
```

The admin UI frontend:
- reads draft and active registry state from the backend.
- submits registration intent (create, edit, promote) to the backend.
- displays structured validation results returned by the backend.
- does not infer, compute, or derive topology state independently.
- does not cache or hold authoritative topology state locally.

The backend and DB remain the source of truth for all topology registration data.

## 8. Backend Boundary Policy

The backend Registrar service is responsible for:

```text
- accept registration intent
- validate topology refs
- produce structured validation result
- persist only valid draft/promoted states
```

The backend:
- accepts registration intent submitted by the frontend.
- validates all topology references and constraints.
- returns structured validation results — not UI-level warnings.
- persists only states that pass validation (draft) or full promotion check (promoted).
- does not auto-repair broken refs.
- does not bypass the canonical runtime route.

Endpoint design and implementation are future issues. This specification does
not define production endpoint contracts.


### Boundary failure handling requirements

For admin UI and registrar backend boundaries, success-path handling alone is insufficient.
Backend responses must return explicit structured validation results for both validation and
persistence failures.

Required explicit-result handling includes:
- request validation failures (missing/invalid fields, malformed id/payload)
- authorization/authentication failures
- not found conditions
- persistence constraint failures (for example UNIQUE constraints such as `(label, group)`)
- backend/repository unavailable failures

Frontend API proxy and UI must propagate backend status classes without silent rewrites, and
must present UI-visible error states. Post-write reads must remain consistent with the persisted
state (or return explicit inconsistency errors).

## 9. Out of Scope

The following are explicitly out of scope for this specification:

- UI implementation
- backend endpoint implementation
- DB migration
- auth/permission implementation
- browser E2E tests
- real domain data
- production admin console
- promotion manifest editor implementation

## 10. Future Implementation Issues

The following implementation work should follow after this specification is accepted:

- Registrar draft schema / DB tables
- Registrar validation service
- Registrar admin route skeleton
- Registrar preview projection
- Registrar promotion operation

These are separate issues. Do not implement them in this specification issue.
