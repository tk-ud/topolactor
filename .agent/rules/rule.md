# Agent Rules

## Architecture Rules

- Data-defined topology is the architecture subject. Not DTOs, not CRUD, not layered MVC.
- OperationVector is internal runtime representation. It is not the architecture subject.
- DTO is endpoint contract only. DTOs are not the subject of the architecture.
- DB is the semantic topology space. It stores registries, schemas, packages, relations, structure maps, and function parameters.
- Backend is the abstract runtime. It executes functions against stored topology data.
- Frontend is the physical projection space. It projects UI from packages, schemas, and component expansions.
- Broken refs are explicit errors, not silent fallback. Any unresolved reference must return a validation error.
- Real business data is out of scope for the public skeleton.
- Structure check must pass before completion report.
- Reports must include remaining TODO.

## Canonical Runtime Route

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

Do not bypass any step. Do not add silent fallbacks anywhere in this route.

## Agent Behavior

- Read `.agent/docs/required-paths.yaml` to understand required structure.
- Run `.agent/tests/check-structure.sh` before reporting task completion.
- Write outputs to `.agent/reports/`.
- Update `.agent/tasks/todo.md` with remaining tasks.
- Do not convert topolactor to CRUD or MVC.
- Do not add build steps, DB execution, or integration tests under the structure check surface.
