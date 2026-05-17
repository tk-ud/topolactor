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

Frontend owns:

- user interaction
- visual projection
- component package expansion
- agenda execution
- resume restoration
- response projection

Frontend must not own:

- business meaning decisions
- database topology authority
- backend internal runtime state

---

### Backend = Abstract Vector Runtime

Backend is the abstract vector execution space.

It receives a user operation vector, resolves the attractor, binds parameters stored in the database, validates the runtime state, and emits a response.

Backend owns:

- vector resolution
- attractor resolution
- abstract function parameter binding
- runtime validation
- mapper execution
- repository command generation
- emission generation

Backend must not own:

- physical UI rendering
- unvalidated schema mutation
- silent fallback

---

### Database = Semantic Topology Space

Database is the semantic topology space.

It stores registries, relations, schemas, packages, function parameters, and converged entity data.

The database is not merely a table store. It is the space where business meaning, relations, and runtime parameters are accumulated.

Database stores:

- registrar
- registry
- relation registry
- structure map
- schema
- package
- component refs
- abstract function parameters
- converged entity data
- diff logs
- usage metrics
- promotion policy

---

## Runtime Terms

### Vector

A compressed direction of operation.

It contains user action, target, context, role, payload, current hub/entity, and requested projection.

### Attractor

The semantic convergence point of a vector.

It is resolved through hub, relation registry, manifest, package, schema, and state context.

### Structure Map

A runtime topology map.

It connects:

```text
vector
→ attractor
→ package
→ schema
→ components
→ relation
→ state policy
```

### Package

A bundle of executable components.

Frontend packages contain UI components.

Backend packages contain resolvers, mappers, validators, and repository commands.

### Schema

The definition of elements and wiring.

Schema defines shape, validation, fields, relations, component refs, API contracts, and repository command shape.

### Component

An atomic executable unit.

Frontend components are UI parts.

Backend components are runtime execution units such as resolvers, validators, mappers, diff appenders, and schedulers.

### Agenda

Execution plan.

It defines which vector, package, schema, and components should run in which order.

### Resume

Restoration context.

It restores current vector, attractor, hub, entity, selected package, selected schema, and relation context.

### Emission

Validated runtime output.

Frontend receives emissions and expands them into UI projection.

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

New data structures do not require immediate physical DDL.

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

DTOs exist to support endpoint shape, validation, and transport boundaries.

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

## Initial Goal

The first goal of topolactor is to build a business management runtime where:

- admins can define logical tables through registrars
- relations can be added as data
- UI can be projected from schemas and packages
- backend behavior can be expanded through runtime packages
- frequently used jsonb fields can be promoted safely
- audit logs remain append-only
- business data evolves from usage, not from premature schema fixation

---

## Status

Experimental design phase.

This repository starts as a structural and runtime design reference for a vector-driven business management system.

---

## License

TBD
