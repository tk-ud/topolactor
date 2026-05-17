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

## Start Here

- `docs/framework-core.yaml` — core concepts, topology model, and layer responsibilities.
- `docs/framework-policy.yaml` — registry, state, search, manifest, data, log, and promotion policies.
- `docs/file-structure.yaml` — repository layout and canonical runtime wiring.
- `docs/design/context-route-recommendation.md` — self-learning DB and recommendation runtime SSOT.
- `docs/registrar-admin-ui-specification.md` — boundary for topology registration admin UI.
- `docs/promotion-manifest-editor-specification.md` — boundary for promotion manifest editing.
- `AGENTS.md` — entrypoint instructions for coding agents.
- `.agent/` — agent rules, structure checks, skills, tasks, and routine agent surfaces.

## Agent Workflow

```text
READ_RULES
→ INSPECT_TARGET
→ DEFINE_SCOPE
→ RUN_LOCAL_CI
→ FIX_IF_RED
→ COMMIT_OR_PR
```

See `NOTICE.md`.