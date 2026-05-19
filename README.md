# topolactor

Topolactor is a **data-driven topology runtime architecture** that treats the registry table as a **semantic matrix**, then projects/expands DB, UI, endpoint, runtime, scheduler, and function surfaces from the same **registry tensor**.

> Short definition: topolactor treats the registry table as a semantic matrix and resolves runtime surfaces by applying abstract functions to registry tensor coordinates.

**Tech stack:** PostgreSQL / C# / Deno Fresh / Preact.

## 30-Second Overview

- **registry table = semantic matrix**
- **row = registryId / basis vocabulary**
- **column = semantic axis / projection axis / wiring axis**
- **value = weight / state / relation / coordinate / connection**
- **registryId combinations = sparse vector / tensor coordinate**
- **abstract function(tensor) = each runtime/projection surface expander**
- **SQL Attention = DB-topology attention-equivalent observation (not SQL QK dot-product reproduction)**
- **Admin UI = semantic matrix / UI topology tensor editor**

This is not a “put config in JSON and reload” model. The architecture subject is the registry tensor itself and its projection/expansion behavior across runtime boundaries.

## Architecture Overview

### 1) Registry Tensor / Semantic Matrix

The registry is not only metadata or dictionary storage. It is the semantic matrix that supplies basis vocabulary and wiring axes. Runtime behavior is resolved from matrix coordinates, and different surfaces observe the same tensor from different projections.

### 2) SQL Attention

SQL Attention is the runtime’s DB-native observation model for topology continuity and attention weight. It is used to observe what the system should attend to in topology space (context continuity, transitions, and weighted relevance), not as a recommendation-only UI ranking gimmick.

### 3) Projection / Expansion Surfaces

DB / UI / endpoint / runtime / scheduler / function / CI-diagnostic are treated as projection or expansion surfaces of one tensor. They are not independent architecture subjects.

### 4) UI Topology Tensor

UI topology is a tensor projection surface. UI definitions become first-class topology entities only after persistence and ID issuance; code-only component presence is intentionally insufficient.

### 5) Components Bucket + Package Generator

- **components bucket**: staging surface for unpackaged components.
- **package generator**: issues `componentId`, `packageId`, `layoutId`, `wiringId` and persists topology wiring.
- only persisted/issued topology entities are projected as runtime UI topology.

### 6) Stable Surface Adapters

Endpoint/projection adapters are fixed surfaces. New specs should be represented primarily by registry tensor / UI topology data updates, not by re-implementing per-spec adapters every time.

### 7) Governance / Audit Policy

The repository keeps explicit governance surfaces for runtime boundary checks, policy judgment, structure checks, and SSOT drift prevention.

## What topolactor is **not**

- **Not** a normal CRUD auto-generator.
- **Not** a component catalog-only system.
- **Not** an external embedding cache / pgvector-centric architecture.
- **Not** a framework that recreates endpoint/UI/scheduler/function per spec.
- **Not** a recommendation-only or ranking-only subsystem.

## Implementation Status (Now / Design / Future)

- **Implemented now (main branch):** registry semantic-matrix principle, topology registry surfaces, SQL-based context/recommendation runtime surfaces, UI topology persistence surfaces, admin UI boundary docs.
- **Design-guarded (SSOT-defined):** tensor-first projection discipline across runtime surfaces, package generator promotion semantics, fixed adapter posture.
- **Future / planned:** items explicitly called out as planned in SSOT docs (for example some audit/log-derived recommendation expansions).

## Developer Entry Points

- SSOT concepts/policies: `docs/framework-policy.yaml`
- Structure map of repository/runtime wiring: `docs/file-structure.yaml`
- Registry tensor policy: `.agent/protocols/registry-tensor-policy.md`
- Context-route recommendation SSOT: `docs/design/context-route-recommendation.md`
- Topology recommendation CI runtime SSOT: `docs/design/topology-recommendation-ci-runtime.md`
- Admin UI boundary: `docs/registrar-admin-ui-specification.md`
- DB schema surfaces:
  - `db/topology_tables.sql`
  - `db/ui_topology_tables.sql`
  - `db/context_route_tables.sql`
- Demo walkthrough: `docs/demo-walkthrough.md`

## Public-facing Articles

- `docs/articles/registry-semantic-matrix.md`
- `docs/articles/sql-attention.md`
- `docs/articles/ui-topology-tensor.md`


- External overview and agenda: `docs/agent-development-os.md`

## Operational Details

Runtime setup, explicit failure behavior, and agent workflow are intentionally kept in dedicated operational documents.

- Agent contract: `AGENTS.md`
- Repository/runtime map: `docs/file-structure.yaml`
- Runtime and policy SSOT: `docs/framework-policy.yaml`
- Demo/setup guide: `docs/demo-walkthrough.md`

## CI / Verification Notes

- `IF_LOCAL_NOT_EXECUTED_VERIFY_REMOTE_CI_EQUIVALENT`
- Structure Check is the always-on required gate.
- Heavy CI workflows are path-scoped.
- Scope-irrelevant skipped heavy CI is not blocking.
