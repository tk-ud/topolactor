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


When a change extends an existing boundary with a new event, write path, append log, current table, repository mutation, API payload, frontend projection, or UI action, agents must verify End-to-End Boundary Identity.

The verification must compare DB identity, backend contract identity, API payload identity, repository mutation identity, frontend projection identity, and UI action identity.

Any omitted identity field must be explicitly justified. Multi-instance leakage scenarios must be checked before completion.


## Audit Gap Response Gate

Any audit report, completion report, or TODO-state decision that evaluates governance quality must pass the Audit Gap Response Gate.

Required audit sections:

- Governance Gaps
- Proposed Governance Improvements
- Remaining TODOs
- Completion Eligibility

Gate requirements:

1. Governance Gaps must explicitly evaluate protocol/checklist/task/report-surface/completion/agent-behavior risks.
2. If any governance gap exists, Proposed Governance Improvements is mandatory and must describe actionable protocol/feature/checklist improvements.
3. Remaining TODOs must preserve unresolved work in `.agent/tasks/todo.md` as incomplete items.
4. Completion Eligibility must separate static protocol coverage audit eligibility from behavior execution audit eligibility.
5. Behavior execution audit cannot be treated complete without observed behavior evidence (for example execution logs, trap-case results, historical PR evidence tied to artifacts, diff-linked runtime behavior proof, or persistent report evidence).
6. Log-only evidence, PR-body-only evidence, and static-doc-only evidence must not be misclassified as explicit behavior evidence or explicit result-surface proof.
7. Classification must use PASS / GAP / BLOCKING / TODO semantics; cautionary, conditional, or non-fatal findings are not unconditional PASS.

Recursive Verification Gate dependency:

- Any unresolved governance GAP or missing improvement/TODO response that should block completion is treated as blocking until fixed or explicitly preserved as remaining TODO within the defined audit scope.


## Failure Triage Self-Recursion Gate

Any completion report, audit report, or TODO `[x]` decision must run failure triage over all executed commands before eligibility judgment.

Failure triage classification (required for each failed command):

- required check failure
- exploratory / usage-confirmation failure
- expected negative test
- out-of-scope failure

Gate requirements:

1. If any required check failure exists, classification is BLOCKING.
2. If a failure cannot be classified, classification is BLOCKING.
3. exploratory / usage-confirmation failures must include explicit rationale for why the command is not a required check for audited scope.
4. expected negative tests must include expected-failure intent and success condition evidence.
5. out-of-scope failures must include explicit scope boundary rationale.
6. No completion report and no TODO `[x]` update is allowed before failure triage result is recorded.
7. Agent must not wait for user feedback to trigger triage recursion.

Self-recursion actions (at least one required when triage finds BLOCKING/GAP/TODO conditions):

- return to fix phase,
- revise report classification (for example PASS → GAP/BLOCKING/TODO),
- revert TODO `[x]` to `[ ]`,
- preserve unresolved work in Remaining TODOs.

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
