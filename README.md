# topolactor

Topolactor is a **data-driven topology runtime** and **Data-Driven OS** for building runtime-defined applications from registry-defined topology coordinates, and an **AI-Driven Development OS** for evolving that runtime safely with agent-readable governance.

Topolactor provides a data-driven topology runtime with explicit runtime-route and policy governance boundaries.

The core architecture treats the registry table as a **semantic matrix**. DB, UI, endpoint, runtime, scheduler, and function surfaces are projected or expanded from the same registry-defined topology coordinate space rather than implemented as disconnected CRUD screens.

**Tech stack:** PostgreSQL / C# / Deno Fresh / Preact.

Development started: 2026-05-17 (first repository commit: 79d71f1)

## 30-Second Overview

Topolactor has three main subjects:

1. **Data-Driven OS** — application behavior is resolved from stored topology data, registry coordinates, structure maps, packages, schemas, and runtime policies.
2. **AI-Driven Development OS** — agents modify the repository through explicit contracts, protocols, checklists, TODO surfaces, and CI gates instead of ad-hoc edits.
3. **SQL Attention** — PostgreSQL observes physical-current pressure and hub-current square-matrix continuity as separate current planes, then records hub-attractor attention evidence without collapsing statistics, Attention, and Phase Attention into one score. It is not SQL-side QK dot-product reproduction and is not the topology-side recommendation current.

Registry / hub / topology-coordinate shorthand:

- **registry = definitions and configuration** (target-dependent: hub registry / topology registry / ui registry)
- **registry semantic matrix = definition/configuration basis** for topology vocabulary, axes, coordinates, and wiring; it is not a hub, attractor, or Attention entity itself
- **hub = relation node** formed by resolved relations
- **relation-bearing hub = tensor/vector-readable coordinate surface**
- **vector convergence = attractor**
- **Attention = observation point/operation, not the attractor itself**
- **abstract function(coordinate/tensor surface) = runtime/projection surface expander**

## Architecture Overview

### Registry Semantic Matrix / Hub Tensor Surfaces

The registry is not a plain dictionary, config table, or metadata catalog. It stores definitions/configuration that resolve topology vocabulary and wiring axes. Registry itself is not a hub, attractor, or Attention entity. Hubs are relation nodes built from those definitions, and relation-bearing hubs become tensor/vector-readable coordinate surfaces.

### Projection / Expansion Surfaces

DB / UI / endpoint / runtime / scheduler / function / CI-diagnostic are treated as projection or expansion surfaces of one registry-defined topology coordinate space. Frontend projection renders resolved topology; it is not the meaning authority.

### SQL Attention

SQL Attention observes what the runtime should attend to by comparing two DB-native current planes:

- **physical current** — table, column / JSON path, candidate, operation, component, diff, and log pressure.
- **hub current** — hub / attractor continuity represented as a bounded square-matrix field.

The attention result is not the aggregate itself. SQL Attention observes logs-side physical time-axis pressure and reads hub/vector-indicated attractor evidence. Attractor is a vector convergence point, while Attention is the observation operation. Physical pressure and hub continuity are parallel observation planes; neither one is merely a derived view of the other.

The implemented context-route tables in `db/context_route_tables.sql` are projection and signal surfaces used by the topology runtime. `context_event`, rebuildable vector caches, transition statistics, and append-only feedback events provide observable signals and rebuildable projections. `context_hub_recommendation_current` is the topology-side recommendation current for context-route and topology-vector use; it is not the SQL Attention target and not the meaning authority.

`context_token_registry.value` is an ordering/display/audit reference, not a sparse-vector computation weight. Token presence is observed as multi-hot `token_id -> 1.0`; `vector_sparse` and `l2_norm` remain rebuildable projection caches, not meaning SoT.

### UI Topology Tensor

UI definitions become topology entities only after persistence and ID issuance. Code-only components remain staging artifacts until promoted by package-generation flow.

## What topolactor is **not**

- **Not** a normal CRUD auto-generator.
- **Not** a component catalog-only system.
- **Not** an external embedding cache / pgvector-centric architecture.
- **Not** a framework that recreates endpoint/UI/scheduler/function per spec.
- **Not** a recommendation-only or ranking-only subsystem.

## Agent Governance Context Cost

Agent governance work usually consumes more context than one-shot code generation because the baseline route is contract-first (`AGENTS.md` → `.agent/rules/rule.md` → `.agent/README.md` → selected worktype prompt → triggered protocols) before implementation detail reads.

### Measurement method (rough estimate)

- Measured repository-local file text size (character count) on governance route files.
- Token estimate uses `chars / 4` as the primary approximation and `chars / 3` as an upper-bound approximation.
- This is a rough guide, not tokenizer-exact accounting.
- Estimates below intentionally **exclude** target implementation files and PR diff payload size.

### Route-based context estimate (governance read-set only)

| Route | Typical read-set | Estimated tokens (chars/4 to chars/3) |
|---|---|---:|
| Baseline worktype route | `AGENTS.md` + `rule.md` + `.agent/README.md` + selected prompt | ~1,130–1,550 |
| audit | baseline + `protocols/audit.md` (+ optional `ssot-map`) | ~1,500–1,950 (without `ssot-map`) / ~3,640–4,800 (with `ssot-map`) |
| specific | baseline + `protocols/specific.md` | ~1,440–1,900 |
| implementation_change | baseline + implementation protocol + `ssot-map` + selected SSOT (+ triggered protocol) | ~3,940–5,230 before selected SSOT docs |
| design_change | baseline + design protocol + governance SSOT + `ssot-change-impact` | ~2,370–3,150 before additional impacted SSOT docs |
| existing_pr_update | baseline + `completion-summary` (+ triggered protocol if needed) | ~2,340–3,080 |
| todo_maintenance | baseline + `todo-carry-over` + `.agent/tasks/todo.md` | route-dependent (task backlog size dominates) |

Reference measured sizes used in the estimate (chars): `AGENTS.md` 1,345; `.agent/rules/rule.md` 1,122; `.agent/README.md` 1,329; prompts: ~736–966 each; `completion-summary.md` 4,083; `ssot-map.yaml` 8,558; `required-paths.yaml` 14,068.

- `.agent/docs/ssot-map.yaml` and `.agent/docs/required-paths.yaml` are **conditional indexes**, not always-read bundles.
- Full `.agent` governance bundle read is **avoid by default**; route-targeted reading is the intended policy.
- If full bundle loading happens, context cost can reach roughly **15k–30k+ tokens** depending on included protocol/docs surfaces.

## AI-Driven Development OS Boundary

This README keeps only the public positioning and context-cost estimate for AI governance. The execution contract, judgment rules, and detailed routing authority remain in `docs/agent-development-os.md`, `docs/governance/`, `AGENTS.md`, and `.agent/`; README is not the agent execution SSOT.

## Implementation Status

Topolactor is currently a **public design + scaffold reference** with a **canonical runtime-route skeleton** under active construction; it is **not production-ready** as a completed application platform.

- **Public status SSOT:** `docs/system-roadmap.yaml` is the canonical public status source for milestone and component state (`implemented` / `partial` / `skeleton` / `not_started` / `production_ready`).
- **Current repository state (high level):**
  - **Implemented surfaces:** selected boundaries such as frontend dispatch action/client, backend dispatch endpoint, default-entity-search vertical slice, SQL Attention DB observation tables, and governance/check surfaces.
  - **Partial surfaces:** runtime executor behavior and M1 runtime skeleton milestone progress.
  - **Skeleton surfaces:** manifest dispatcher, runtime timeline scheduler, and SSE projection lane wiring.
  - **Planned / not started:** most M2+ milestone scopes remain not started in roadmap SSOT.
- **Design-guarded policy posture:** tensor-first projection discipline, package-generator promotion semantics, fixed surface-adapter posture, and explicit no-silent-fallback policy.

For exact status/evidence and completion conditions, follow `docs/system-roadmap.yaml` first, then drill down into linked design SSOT files.

## Detail Entry Points

### Data-Driven OS / Runtime

- Core policy SSOT: `docs/framework-policy.yaml`
- Repository/runtime map: `docs/file-structure.yaml`
- Registry/topology tensor policy: `.agent/protocols/registry-tensor-policy.md`
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
- SQL observation surface: `db/context_route_tables.sql`

### DB Schema Surfaces

- `db/topology_tables.sql`
- `db/ui_topology_tables.sql`
- `db/context_route_tables.sql`


## Local Bootstrap for .agent Checks

To satisfy local prerequisites without mixing install and check logic:

```bash
bash .agent/scripts/bootstrap-local-tools.sh
```

This script installs missing dotnet SDK 8 and Deno only when absent, ensures `infra/.env` exists, starts `postgres` via Docker Compose, and writes `~/.topolactor-tools/env.sh` for PATH setup in the parent shell.

For DB bootstrap only:

```bash
bash .agent/scripts/bootstrap-local-postgres.sh
```

After bootstrap, load tool PATH in your current shell, then run the unified local CI entrypoint (check scripts still fail explicitly when prerequisites are missing):

```bash
source ~/.topolactor-tools/env.sh
bash .agent/tests/check-local-ci.sh
```

`check-local-ci.sh` executes checks in fixed order: unified test gate → runtime environment → structure check (always last). Even when earlier checks fail or are not executed, it still runs the final structure check and then exits non-zero.
