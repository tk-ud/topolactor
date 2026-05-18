# topolactor

Topolactor is a data-driven topology runtime scaffold.

Use this repository as a seed for a separate product repository.

**Tech stack:** PostgreSQL / C# / Deno Fresh / Preact.

## What You Can Build

Topolactor is designed for applications where UI, data shape, and runtime behavior are expanded from stored definitions instead of hard-coded screens.

With this scaffold, you can build:

- business systems whose screens are driven by registry and schema definitions
- admin tools that can grow from JSONB data into promoted tables
- UI projections assembled from packages and components
- composite business views centered around a resolved hub
- operation-to-runtime dispatch flows
- context-aware recommendation surfaces based on accumulated operation history
- agent-assisted scaffold repositories with local CI guardrails

In this project, a **hub** is a resolved grouping point in the topology space. It lets related business data be assembled as a view without making a fixed physical table or screen the architecture subject.

The intended extension model is:

```text
add topology data
→ define attractor / structure_map
→ bind package / schema / components
→ runtime emits validated output
→ frontend projects UI
```

This makes it possible to add new business surfaces by extending runtime definitions, packages, components, schemas, and structure maps instead of creating a fixed CRUD screen for every table.

## Repository Surfaces

Topolactor is organized as three public-facing surfaces:

- **Runtime scaffold** — backend, frontend, database, and demo topology definitions.
- **Demo walkthrough** — fake-data scenarios showing how topology and policy changes propagate.
- **Agent governance layer** — `.agent/` and `AGENTS.md`, which define how coding agents should inspect, modify, verify, and report changes.

The `.agent/` layer is not application runtime code. It is a repository governance layer for preserving the data-defined topology architecture during agent-assisted development.

## Self-Learning DB and Recommendation Runtime

Topolactor includes a lightweight self-learning DB pattern for recommendation.

It does not train a neural network and does not backpropagate model weights.
Instead, it records append-only context events and derives recommendations from stored topology data, token registries, vector caches, and transition aggregates.

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
Runtime thresholds, scoring policy, retention policy, and recommendation behavior should be resolved from Registry / Manifest / function_parameters / structure_map policy surfaces rather than hidden constants.

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

A public scaffold demo is available at the `/demo` route after starting the frontend.

The `/demo` route exercises the **frontend-side** canonical flow only:

```
UserOperation → resolveOperationVector → attractorKey
→ lookupStructureMap → Emission → renderEmission → ComponentSpec[]
```

Changing `defaultStructureMap` (`frontend/structure_map.ts`) or `defaultComponentRegistry` (`frontend/registry/componentRegistry.ts`) changes what `/demo` resolves and renders — no DB required.

Backend resolution (DB attractor_resolve, live entity data, live recommendations) is exercised at `/` via the dispatch panel.

The walkthrough (`docs/demo-walkthrough.md`) covers 4 scenarios:

- **Scenario A** — token `value` change → recommendation score change (backend/DB, via dispatch panel)
- **Scenario B** — `context_route_policy_ref` change → different policy loads (backend/DB, via dispatch panel)
- **Scenario C** — `aggregation_limit` change → windowed transition stats scope changes (backend/DB, via dispatch panel)
- **Scenario D** — `defaultStructureMap` or `defaultComponentRegistry` change → `/demo` projection changes (frontend only, no DB)

No real business data is used.

## Demo Status

The public demo uses fake data only.

- `/demo` shows the frontend-side canonical projection flow.
- `/` dispatch panel exercises backend-side runtime resolution when backend and DB services are available.
- `docs/demo-walkthrough.md` documents observable scenarios for token value changes, policy reference changes, aggregation policy changes, and frontend projection changes.

Known remaining demo/runtime gaps are tracked in `.agent/tasks/todo.md`.

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
- `AGENTS.md` — entrypoint instructions for coding agents.
- `.agent/` — agent rules, structure checks, skills, tasks, and routine agent surfaces.

## Agent Governance

Agent-assisted changes are governed by `AGENTS.md`.

The agent flow separates:

- local CI checks for structure, backend, frontend, and DB verification
- Policy Judgment Gate checks for architecture and policy decisions
- residual task tracking in `.agent/tasks/todo.md`

This helps prevent accidental conversion into CRUD/MVC code, silent runtime fallbacks, frontend business logic, or hidden production policy constants.

## Agent Workflow

```text
READ_AGENTS
→ INSPECT_FULL_BRANCH_DIFF
→ APPLY_POLICY_JUDGMENT
→ AUDIT_SCOPE_AND_CLAIMS
→ RUN_RELEVANT_LOCAL_CI
→ RUN_STRUCTURE_CHECK_LAST
→ REPORT_PASS_FAIL_NOT_EXECUTED_AND_TODOS
```

For detailed agent instructions, see `AGENTS.md`.

See `NOTICE.md`.