# topolactor

Topolactor is a data-driven topology runtime scaffold.

Use this repository as a seed for a separate product repository.

## What You Can Build

Topolactor is designed for applications where UI, data shape, and runtime behavior should be expanded from stored definitions instead of hard-coded screens.

With this scaffold, you can build:

- registry-driven business systems
- schema-driven admin tools
- package/component-driven UI projection
- hub-centered composite data views
- operation-to-runtime dispatch flows
- agent-assisted scaffold repositories

The intended extension model is:

```text
add topology data
→ define attractor / structure_map
→ bind package / schema / components
→ runtime emits validated output
→ frontend projects UI
```

This makes it possible to add new business surfaces by extending runtime definitions, packages, components, schemas, and structure maps instead of creating a fixed CRUD screen for every table.

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

- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`
- `docs/file-structure.yaml`
- `docs/registrar-admin-ui-specification.md`
- `docs/promotion-manifest-editor-specification.md`
- `AGENTS.md`
- `.agent/`

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