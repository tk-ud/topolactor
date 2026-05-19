# Agent Rules

## Always-Read Baseline

Every task starts by reading:

1. `AGENTS.md`
2. `.agent/README.md`
3. `.agent/rules/rule.md`

These are always-on governance sources. Protocols and skills are conditional and must be opened only when their trigger applies.


## Read Route

Use this lightweight read route:

1. `AGENTS.md`
2. `.agent/README.md`
3. `.agent/rules/rule.md`
4. `.agent/skills/agent-workflow.md`
5. only when needed, open relevant `.agent/docs/` resume/index
6. only when needed, open corresponding `.agent/skills/structure-check.md` and other task skills
7. only when needed, open `docs/` source pages and condition-triggered `.agent/protocols/` / checklists / scripts / tests

Do not treat protocol bundle, docs bundle, or skills bundle as always-read scope.

Work execution order follows `.agent/skills/agent-workflow.md`.




## Workflow Order Invariant

The following workflow order is a mandatory invariant for all work routes:

```text
READ_ENTRY
→ READ_TASK_MATERIALS
→ READ_TARGET_SURFACES
→ DEFINE_SCOPE
→ SCENARIO_CONTRACT
→ IMPLEMENT
→ FILL_CHECKLISTS
→ VERIFY_SCENARIO_DIFF
→ JUDGMENT
→ STRUCTURE_CHECK
→ PUSH_OR_PR
```

- This order must be followed across all work entry routes.
- Opening protocol / checklist / script / test surfaces directly must not bypass this order.
- If work starts from a mid-step, return to incomplete prerequisite steps first.
- structure check is not a judgment substitute.
- push / PR update / TODO `[x]` / completion summary are allowed only after JUDGMENT and STRUCTURE_CHECK.

## Task-Required Reading Rules

- Issue / prompt explicit materials and required-read lists are task-required input.
- Read corresponding `.agent/docs/` resume/index for changed target surfaces, including `.agent/docs/ssot-map.yaml`.
- When `.agent/docs/ssot-map.yaml` maps the change surface to `docs/` SSOT, read the mapped `docs/` SSOT before implementation or audit.
- This requirement does not make the entire `docs/` bundle always-read scope.

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

## Protocol Agenda Map

Protocol agenda map (condition-triggered):

1. completion-governance: `.agent/protocols/completion.md`
2. scenario-contract: `.agent/protocols/scenario-contract.md`
3. boundary-identity: `.agent/protocols/runtime-boundary-matrix.md`
4. policy-judgment: `.agent/protocols/policy-judgment.md`
5. registry-topology-semantics: `.agent/protocols/registry-tensor-policy.md`
6. reports-and-todo-surfaces: `.agent/protocols/reports-and-todos.md`

## Protocol Trigger Map

Protocols are not always-on reading. Use each protocol only when its trigger condition applies.

- Completion report / TODO `[x]` update / completion eligibility decision:
  - `.agent/protocols/completion.md`
  - `.agent/protocols/reports-and-todos.md`
- Runtime claim / route / persistence / projection changes:
  - `.agent/protocols/scenario-contract.md` (Temporary Scenario Contract)
- Endpoint / frontend API proxy / repository write / admin operation / persistence mutation / DB-backed registry operation:
  - `.agent/protocols/runtime-boundary-matrix.md` (Runtime Boundary Failure Matrix)
- Policy value / scoring / threshold / routing / validation / projection behavior changes:
  - `.agent/protocols/policy-judgment.md`
- Registry tensor / topology semantics changes:
  - `.agent/protocols/registry-tensor-policy.md`

## Gate Naming (Reference Only)

Recursive Verification Gate, Required Check Scope Declaration Gate, Failure Triage Self-Recursion Gate, and Audit Gap Response Gate are executed through completion-governance protocol.

Details:
- `.agent/protocols/completion.md`
- `.agent/protocols/reports-and-todos.md`

Skills are operation procedures and are not governance protocols. Read `.agent/skills/*.md` only when executing the corresponding task/check.


Temporary Scenario Contract and scenario contract use are trigger-based and not always-on.
