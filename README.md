# topolactor

Topolactor is a data-driven topology runtime scaffold.

Use this repository as a seed for a separate product repository.

Core flow:

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

Start here:

- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`
- `docs/file-structure.yaml`
- `AGENTS.md`
- `.agent/`

Agent workflow:

```text
READ_RULES
→ INSPECT_TARGET
→ DEFINE_SCOPE
→ RUN_LOCAL_CI
→ FIX_IF_RED
→ COMMIT_OR_PR
```

See `NOTICE.md`.
