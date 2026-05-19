# topolactor

Topolactor is a **data-driven topology runtime** and **Data-Driven OS** for building runtime-defined applications from a registry tensor, and an **AI-Driven Development OS** for evolving that runtime safely with agent-readable governance.

The core architecture treats the registry table as a **semantic matrix**. DB, UI, endpoint, runtime, scheduler, and function surfaces are projected or expanded from the same registry tensor rather than implemented as disconnected CRUD screens.

**Tech stack:** PostgreSQL / C# / Deno Fresh / Preact.

## 30-Second Overview

Topolactor has three main subjects:

1. **Data-Driven OS** — application behavior is resolved from stored topology data, registry coordinates, structure maps, packages, schemas, and runtime policies.
2. **AI-Driven Development OS** — agents modify the repository through explicit contracts, protocols, checklists, TODO surfaces, and CI gates instead of ad-hoc edits.
3. **SQL Attention** — PostgreSQL observes topology continuity and attention-equivalent weight from relations, transitions, recency, frequency, aggregates, diffs, and logs. It is not SQL-side QK dot-product reproduction.

Registry tensor shorthand:

- **registry table = semantic matrix**
- **row = registryId / basis vocabulary**
- **column = semantic axis / projection axis / wiring axis**
- **value = weight / state / relation / coordinate / connection**
- **registryId combinations = sparse vector / tensor coordinate**
- **abstract function(tensor) = runtime/projection surface expander**

## Architecture Overview

### Registry Tensor / Semantic Matrix

The registry is not a plain dictionary, config table, or metadata catalog. It supplies the topology vocabulary and wiring axes used to resolve runtime behavior across surfaces.

### Projection / Expansion Surfaces

DB / UI / endpoint / runtime / scheduler / function / CI-diagnostic are treated as projection or expansion surfaces of one registry tensor. Frontend projection renders resolved topology; it is not the meaning authority.

### SQL Attention

SQL Attention observes what the runtime should attend to in DB topology space. It combines candidate narrowing from registry IDs and relations with attention-weight evidence from aggregation, transition, recency, frequency, diff, and log signals.

### UI Topology Tensor

UI definitions become topology entities only after persistence and ID issuance. Code-only components remain staging artifacts until promoted by package-generation flow.

## What topolactor is **not**

- **Not** a normal CRUD auto-generator.
- **Not** a component catalog-only system.
- **Not** an external embedding cache / pgvector-centric architecture.
- **Not** a framework that recreates endpoint/UI/scheduler/function per spec.
- **Not** a recommendation-only or ranking-only subsystem.

## Implementation Status

- **Implemented now:** registry semantic-matrix principle, topology registry surfaces, SQL-backed context/recommendation runtime surfaces, UI topology persistence surfaces, admin UI boundary docs, and agent governance surfaces.
- **Design-guarded:** tensor-first projection discipline, package-generator promotion semantics, fixed surface-adapter posture, and explicit no-silent-fallback policy.
- **Future / planned:** items explicitly marked as planned in SSOT docs and `.agent/tasks/todo.md`.

## Detail Entry Points

### Data-Driven OS / Runtime

- Core policy SSOT: `docs/framework-policy.yaml`
- Repository/runtime map: `docs/file-structure.yaml`
- Registry tensor policy: `.agent/protocols/registry-tensor-policy.md`
- Context-route recommendation SSOT: `docs/design/context-route-recommendation.md`
- Topology recommendation CI runtime SSOT: `docs/design/topology-recommendation-ci-runtime.md`
- Admin UI boundary: `docs/registrar-admin-ui-specification.md`
- Demo/setup guide: `docs/demo-walkthrough.md`

### AI-Driven Development OS / Agent Governance

- Agent contract: `AGENTS.md`
- External overview and agenda: `docs/agent-development-os.md`
- Rules: `.agent/rules/rule.md`
- Protocols: `.agent/protocols/`
- Checklists: `.agent/checklists/`
- Tests: `.agent/tests/`
- Remaining work: `.agent/tasks/todo.md`

### SQL Attention / Public Articles

- `docs/articles/registry-semantic-matrix.md`
- `docs/articles/sql-attention.md`
- `docs/articles/ui-topology-tensor.md`

### DB Schema Surfaces

- `db/topology_tables.sql`
- `db/ui_topology_tables.sql`
- `db/context_route_tables.sql`

## CI / Verification Notes

- Structure Check is the always-on required gate.
- Heavy CI workflows are path-scoped.
- Scope-irrelevant skipped heavy CI is not blocking.
- If a local required check is not executed, equivalent remote CI evidence or an explicit remaining TODO is required.
- IF_LOCAL_NOT_EXECUTED_VERIFY_REMOTE_CI_EQUIVALENT: agents must verify backend-tests and frontend-types CI pass when local equivalents are not executed.
