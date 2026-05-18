# topolactor

Topolactor is a **DB-defined application runtime and agent-assisted development OS** built as a data-driven topology runtime.

It is a public scaffold for building runtime-defined applications where topology, dispatch, projection, and policy are resolved from stored definitions instead of fixed CRUD screens.

**Tech stack:** PostgreSQL / C# / Deno Fresh / Preact.

## Application Runtime OS / Agent Development OS

Topolactor is intentionally organized as a two-layer system:

- **Application Runtime OS**
  - Stores topology semantics in DB surfaces (registries, schemas, packages, relations, structure maps, function parameters).
  - Resolves user operations through the canonical runtime route.
  - Emits runtime output and UI projection inputs from data-defined topology.
- **Agent Development OS**
  - Governs how AI agents inspect, modify, verify, and report repository changes.
  - Preserves semantic boundaries (runtime vs projection vs policy) during agent-assisted evolution.
  - Uses explicit rule, protocol, checklist, script, and test surfaces.

This repository is **not** a CRUD generator. It is a runtime-defined application scaffold where AI agents can safely modify topology-driven behavior while preserving semantic architecture boundaries.

## What You Can Build

Topolactor is designed for applications where UI, data shape, and runtime behavior are expanded from stored definitions.

With this scaffold, you can build:

- DB-backed applications whose screens are driven by registry and schema definitions
- admin tools that can grow from JSONB data into promoted tables
- UI projections assembled from packages and components
- composite domain projections centered around a resolved hub
- operation-to-runtime dispatch flows
- context-aware recommendation surfaces based on accumulated operation history
- runtime-defined applications that remain agent-editable under governance constraints

In this project, a **hub** is a resolved grouping point in topology space. It lets related domain data be assembled as a view without making a fixed physical table or screen the architecture subject.

The intended extension model is:

```text
add topology data
→ define attractor / structure_map
→ bind package / schema / components
→ runtime emits validated output
→ frontend projects UI
```

## Repository Surfaces

Topolactor exposes the following surfaces:

- **Runtime scaffold** — backend, frontend, database, and topology definitions.
- **Documentation surface** — architecture, policy, flow, and operating guides.
- **Agent Development OS surface** — governance assets for AI-agent operation (`AGENTS.md`, `.agent/rules/`, `.agent/protocols/`, `.agent/scripts/`, `.agent/checklists/`, `.agent/tests/`).

The Agent Development OS layer is governance infrastructure, not application runtime logic.

## Self-Learning DB and Recommendation Runtime

Topolactor includes a lightweight self-learning DB pattern for recommendation.

It does not train a neural network and does not backpropagate model weights. Instead, it records append-only context events and derives recommendations from stored topology data, token registries, vector caches, and transition aggregates.

The basic loop is:

```text
append context event
→ resolve active context tokens
→ build sparse event / prefix vectors
→ search nearest historical prefixes
→ aggregate transition counts
→ emit next operation / token candidates
```

Transition probability is calculated from observed counts in SQL:

```text
P(next_operation | prev_operation)
= count(next_operation) / sum(count(next_operation) in the same prev_operation scope)
```

This keeps learning behavior explainable, auditable, and data-defined.

## Core Flow

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

## Demo

A public scaffold demo is available after starting the frontend runtime.

- `/demo` is the **runtime dispatch entrypoint** used for interactive runtime flow access.
- `/demo-static` is a **frontend-only static structure diagram** and **not a runtime result**.
- `/` can also be used to exercise runtime dispatch and related backend-connected behavior depending on environment wiring.

The walkthrough (`docs/demo-walkthrough.md`) covers representative scenarios around recommendation and policy behavior using fake data.

## Demo Status

The public demo uses fake data only.

- Runtime-connected behavior is exercised through dispatch-capable runtime entry surfaces.
- `/demo-static` is documentation-style static structure output only, not runtime execution output.
- Known remaining demo/runtime gaps are tracked in `.agent/tasks/todo.md`.

## Start Here

- `docs/framework-core.yaml` — core concepts, topology model, and layer responsibilities.
- `docs/framework-policy.yaml` — registry, state, search, manifest, data, log, and promotion policies.
- `docs/file-structure.yaml` — repository layout and canonical runtime wiring.
- `docs/design/context-route-recommendation.md` — self-learning DB and recommendation runtime SSOT.
- `docs/design/runtime-excitation-and-package-dispatch.md` — runtime excitation trigger and package dispatch SSOT.
- `docs/design/topology-recommendation-ci-runtime.md` — topology recommendation CI runtime SSOT.
- `docs/design/relation-registry-fk-audit-and-abstract-migration.md` — relation_registry authority FK audit / abstract migration SSOT.
- `docs/registrar-admin-ui-specification.md` — boundary for topology registration admin UI.
- `docs/promotion-manifest-editor-specification.md` — boundary for promotion manifest editing.
- `AGENTS.md` — top-level agent entrypoint instructions.

## Agent Governance

Agent governance is split across explicit surfaces:

- `AGENTS.md` — root contract and required execution order.
- `.agent/rules/rule.md` — architecture and policy rules.
- `.agent/protocols/` — protocol definitions (scenario contract, boundary matrix, completion sequence, policy judgment).
- `.agent/scripts/` — helper scripts for repeatable protocol operations.
- `.agent/checklists/` — policy-judgment and related compliance checklist gates.
- `.agent/tests/` — structure and repository-level guard checks.

## Agent Workflow

```text
READ_AGENTS_AND_RULES
→ SELECT_RELEVANT_PROTOCOLS_AND_CHECKLISTS
→ (IF REQUIRED) CREATE_SCENARIO_CONTRACT
→ APPLY_POLICY_JUDGMENT
→ INSPECT_FULL_BRANCH_DIFF
→ VERIFY_DIFF_AGAINST_SCENARIO_CONTRACT_AND_BOUNDARY_MATRIX
→ RUN_REQUIRED_LOCAL_CHECKS
→ RUN bash .agent/tests/check-structure.sh LAST
→ REPORT_PASS_FAIL_NOT_EXECUTED_AND_REMAINING_TODOS
```

See `NOTICE.md`.

## Runtime Environment Routes

Use one of the three routes below. All configured routes must reach the same canonical runtime: frontend proxy (or nginx) → backend runtime → PostgreSQL.

### 1) Local dev (processes on host)

- Start PostgreSQL and apply `db/schema.sql`, `db/topology_tables.sql`, `db/promotion_tables.sql`, `db/context_route_tables.sql`, `db/seed_empty.sql`, `db/demo_seed.sql`.
- Set backend env: `DATABASE_URL`, `DEMO_JWT_SECRET`, `DEMO_JWT_EXPIRY_HOURS` (`DEMO_JWT_ISSUER` optional). (`BACKEND_PORT` is optional in local dev; default `5000`.)
- Start backend from `backend/`.
- Set frontend env: `DEMO_BACKEND_URL=http://localhost:<BACKEND_PORT>` and start Fresh.
- If `DEMO_BACKEND_URL` is missing in Fresh mode, `/api/*` proxies return 501 explicit configuration errors.

### 2) Docker Compose demo (`infra/docker-compose.yml`)

- Copy `infra/.env.example` to `infra/.env` and fill required values.
- Run `docker compose --env-file infra/.env -f infra/docker-compose.yml up -d`.
- nginx entrypoint: `http://localhost` (port 80).
- Internal service wiring is fixed to prevent drift: backend listens on `5000`, frontend on `8000`, nginx upstream is `backend:5000` and `frontend:8000`, and backend healthcheck probes `http://localhost:5000/health`.
- `/api/*` requests are routed by nginx to backend directly.

### 3) Production-like (reverse proxy + separate services)

- Keep same required backend env as local/compose (`DATABASE_URL`, JWT settings).
- Route `/api/*` through reverse proxy to backend runtime; route UI traffic to frontend runtime.
- Avoid mixed routing where some requests use Fresh proxy and others bypass it unintentionally.

### Explicit failure behavior (no silent fallback)

- Backend startup fails immediately when `DATABASE_URL` is missing.
- JWT-guarded backend routes (`/dispatch`, `/admin/*`) return 401 with explicit auth errors when token/secret is invalid or missing.
- Fresh proxy routes return 501 when `DEMO_BACKEND_URL` is unset and 502 when backend is unreachable.
- Backend validation failures remain explicit (400/404/409/422 depending on endpoint contract).
