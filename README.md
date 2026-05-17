# topolactor

**Topology + Attractor runtime for vector-driven business systems.**

topolactor is a vector-driven topology runtime for business management systems.

It treats frontend as a physical interaction space, backend as an abstract vector runtime, and database as a semantic topology space.

ユーザー操作を `vector` として解釈し、`attractor` に収束させ、`structure_map / package / schema / components` を通じて業務データと UI を展開するための実験的フレームワークです。

---

## Concept

topolactor is not a conventional CRUD / MVC / layered architecture.

The core flow is:

```text
user_operation_vector
→ attractor_resolve
→ structure_map_resolve
→ package_resolve
→ schema_resolve
→ component_expand
→ emission_or_projection
```

The system does not start from fixed tables or fixed screens.

Instead, it stores topology, registries, schemas, packages, and function parameters, then expands only the converged data required by the current operation.

---

## Architecture Philosophy

### Frontend = Physical Interaction Space

Frontend is the physical projection layer.

User operations, payloads, and responses rotate the interaction axes like a quaternion space.

UI is not treated as a fixed screen, but as a projection generated from component packages, schemas, agenda, and resume context.

### Backend = Abstract Vector Runtime

Backend is the abstract vector execution space.

It receives a user operation vector, resolves the attractor, binds parameters stored in the database, validates the runtime state, and emits a response.

### Database = Semantic Topology Space

Database is the semantic topology space.

It stores registries, relations, schemas, packages, function parameters, converged entity data, diff logs, usage metrics, and promotion policies.

---

## Runtime Terms

- **Vector**: compressed direction of operation.
- **Attractor**: semantic convergence point of a vector.
- **Structure Map**: runtime topology map connecting vector, attractor, package, schema, components, relation, and state policy.
- **Package**: bundle of executable frontend or backend components.
- **Schema**: definition of elements and wiring.
- **Component**: atomic executable unit.
- **Agenda**: execution plan.
- **Resume**: restoration context.
- **Emission**: validated runtime output.

---

## Directory Structure

### Frontend

```text
frontend/
  components/      # Atomic UI components
  package/         # UI component package groups
  schema/          # UI element and wiring schemas
  registry/        # Frontend runtime registries
  runtime/         # Frontend runtime executor
  state/           # Client runtime state
  api/             # Backend contract clients
  pages/           # Route shells
  util/            # Pure abstract utility functions
  guard/           # Client-side schema/component guards
  structure_map.ts # Vector to package/schema/component map
  layout.tsx       # Global physical shell
```

### Backend

```text
backend/
  endpoint/        # Thin HTTP boundaries
  components/      # Atomic backend runtime components
  package/         # Backend component package groups
  schema/          # Data and runtime contract schemas
  registry/        # Runtime registries and registrar surface
  mapper/          # Semantic mapping layer
  runtime/         # Backend vector runtime executor
  repository/      # Persistence boundary
  state/           # State policy layer
  guard/           # Permission and runtime safety guards
  util/            # Pure abstract utility functions
  jobs/            # Async maintenance jobs
```

---

## Data Extension Model

topolactor follows a registrar-driven extension model.

```text
raw/entity_jsonb
→ registrar
→ registry
→ relation_registry
→ structure_map
→ package
→ schema
→ UI/runtime projection
```

When usage value becomes clear, data can be promoted.

```text
jsonb
→ index
→ generated column
→ registry
→ physical table
```

Promotion is controlled by external manifests and admin approval.

---

## DTO Position

DTOs are required, but they are not the subject of the architecture.

DTOs are endpoint contracts — essential nutrients for safe communication between frontend, endpoint, and backend runtime.

The subject is:

```text
vector
→ attractor
→ topology
→ package
→ schema
→ components
→ emission
```

---

## Runtime Rules

- Vector is the runtime entry subject.
- Attractor is the semantic convergence point.
- Frontend projects physical interaction space.
- Backend executes abstract vector functions.
- Database stores semantic topology and function parameters.
- Package groups components.
- Schema defines elements and wiring.
- Mapper maps meaning, not ORM.
- Repository persists without business branching.
- Registry is the registrar surface.
- Structure map is the runtime navigation map.
- Agenda executes.
- Resume restores.
- Broken refs are errors, not silent no-ops.

---

## Public Design Documents

This repository currently publishes design documents only.

- `docs/framework-core.yaml` — core framework philosophy, topology model, and layer definitions.
- `docs/framework-policy.yaml` — registry, state, search, manifest, data, log, and promotion policies.
- `docs/file-structure.yaml` — vector-driven runtime directory structure and wiring routes.
- `NOTICE.md` — documentation-only publication notice.

---

## Status

Experimental design phase.

This repository starts as a structural and runtime design reference for a vector-driven business management system.

The reference implementation, production runtime, database schema, admin UI, and private business application code are developed outside this repository.

---

## License / Notice

This repository contains public design documents only.

All rights reserved unless explicitly stated otherwise.

No source code license is granted by this repository at this time.

See `NOTICE.md`.
