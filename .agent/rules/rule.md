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

## Runtime Policy / Magic Number Rules

Runtime behavior must be data-defined whenever the value can change by topology, hub, domain, role, operation, package, schema, deployment, or projection context.

Do not hide runtime behavior in magic numbers or private constants.

The following value categories must first be considered as Registry / Manifest / function_parameters / structure_map policy / package-schema parameters:

- topology behavior
- recommendation behavior
- selection behavior
- promotion behavior
- validation behavior
- scoring behavior
- threshold behavior
- retention behavior
- routing behavior
- UI projection behavior

Inline values are allowed only when they are not runtime policy:

- loop counters
- local collection limits used only to protect iteration mechanics
- protocol constants
- harmless display-only values
- test fixtures
- deterministic placeholder IDs in skeleton topology

Allowed inline values must stay local and must not become hidden business or runtime policy.

If a value affects Runtime output, candidate ranking, validation result, persistence scope, emission shape, routing, retention, or projection behavior, it must not be introduced as an unexplained constant.

Required decision order:

1. Can this value be stored in an existing Registry / Manifest / function_parameters / structure_map policy surface?
2. Can this value be scoped by hub / relation / domain / role / operation / package / schema?
3. If yes, keep Runtime as executor and resolve the value from stored topology data.
4. If policy storage is not implemented yet, return an explicit missing-policy / missing-parameter status rather than inventing a production fallback.
5. If the value is truly mechanical, document why inline is acceptable.

Production fallback constants are prohibited.

Test fixtures may contain representative policy values, but they must be isolated under tests and must not be referenced by production Runtime or Repository code.

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

## Checklist Anti-Bloat Rule

Policy Judgment Checklist must remain a lightweight compliance-signature gate.
Do not expand checklist questions for incident-specific one-off cases by default.

When a new policy judgment viewpoint is needed, first evaluate whether it should be captured in:
1. AGENTS.md scope / required-check definitions, or
2. this rule.md as durable judgment order and architecture rule.

Checker script responsibility should stay focused on answer-format validation and critical-violation detection, not detailed policy rule proliferation.

## Protocol References

Detailed procedures are split under `.agent/protocols/` and scenario contract verification must follow those protocols:

- Temporary Scenario Contract: `.agent/protocols/scenario-contract.md`
- Runtime Boundary Failure Matrix: `.agent/protocols/runtime-boundary-matrix.md`
- Policy Judgment Gate and V1〜V16 table: `.agent/protocols/policy-judgment.md`
- Completion Sequence and local gate order: `.agent/protocols/completion.md`
- Reports and TODO operation surfaces: `.agent/protocols/reports-and-todos.md`

Recursive Verification Gate: Any blocking audit failure across CI/local checks, scenario contract, boundary matrix, full diff verification, or policy judgment blocks completion and requires fix-and-reverify within scope before completion.

Related executables:

- `bash .agent/checklists/check-policy-judgment.sh`
- `bash .agent/tests/check-structure.sh` (run last)
