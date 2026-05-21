# Agent Rules

## Always-Read Baseline

Every task starts by reading:

1. `AGENTS.md`
2. `.agent/rules/rule.md`
3. `.agent/README.md`

These are always-on governance sources. Prompt bundle, protocol bundle, docs bundle, skills bundle, and rule core bundle are not always-read; open them only when their trigger or task scope applies.

## Read Route

Use this lightweight read route:

1. `AGENTS.md`
2. `.agent/rules/rule.md`
3. `.agent/README.md`
4. `.agent/skills/agent-workflow.md`
5. when applicable, open matching `.agent/prompt/<work-type>.md`
6. when target surface needs SSOT mapping, open `.agent/docs/ssot-map.yaml` and relevant `.agent/docs/` resume/index
7. only when needed, open mapped `docs/` SSOT source pages
8. only when needed, open condition-triggered `.agent/rules/core/`, `.agent/protocols/`, checklists, scripts, tests, and corresponding task skills

Do not treat protocol bundle, docs bundle, skills bundle, prompt bundle, or rule core bundle as always-read scope.

Work execution order follows `.agent/skills/agent-workflow.md`.

## Always-On Prohibitions

- Do not bypass Workflow Order Invariant.
- Do not treat structure check as a judgment substitute.
- Do not push, PR update, TODO `[x]`, or completion summary before JUDGMENT and STRUCTURE_CHECK.
- Do not use silent fallback. Broken refs are explicit errors.
- Do not treat DTO, CRUD, layered MVC, or OperationVector as the architecture subject.
- Do not hide runtime policy in magic numbers or private constants.
- Do not read whole protocol/docs/skills/prompt/core bundles by default.

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
- Prompt routers may select required rule core, docs/SSOT, protocols, checklists, and skills.
- Read corresponding `.agent/docs/` resume/index for changed target surfaces only when prompt, ssot-map, or task materials require it.
- When `.agent/docs/ssot-map.yaml` maps the change surface to `docs/` SSOT, read the mapped `docs/` SSOT before implementation or audit.
- This requirement does not make the entire `docs/` bundle always-read scope.

## Rule Core Index

Open these files only when the trigger applies:

- `.agent/rules/core/architecture-subject.md`
  - Trigger: architecture explanation, runtime ownership, DTO/entity boundary, DB topology, frontend projection, explicit-failure behavior.
  - Index terms: Data-defined topology is the architecture subject; OperationVector is internal runtime representation; Broken refs are explicit errors.
- `.agent/rules/core/runtime-policy-magic-number.md`
  - Trigger: runtime behavior, scoring, threshold, routing, validation, persistence scope, emission, projection, registry policy, manifest policy, parameter defaults.
  - Index terms: Runtime Policy / Magic Number Rules; production fallback constants are prohibited.
- `.agent/rules/core/canonical-runtime-route.md`
  - Trigger: runtime route, dispatch, persistence, projection, emission, resolver order, fallback behavior.
  - Index terms: Canonical Runtime Route; silent fallback prohibited.
- `.agent/rules/core/boundary-identity.md`
  - Trigger: endpoint, frontend API proxy, repository write, admin operation, persistence mutation, DB-backed registry operation, append log, current table, frontend projection, or UI action.
  - Index terms: End-to-End Boundary Identity; Multi-instance leakage; repository mutation identity; Frontend projection identity; UI action identity.

## Checklist Anti-Bloat Rule

Policy Judgment Checklist must remain a lightweight compliance-signature gate.
Do not expand checklist questions for incident-specific one-off cases by default.

When a new policy judgment viewpoint is needed, first evaluate whether it should be captured in:
1. AGENTS.md scope / required-check definitions, or
2. this rule index as durable judgment order and architecture rule reference, with detailed body in `.agent/rules/core/`.

Checker script responsibility should stay focused on answer-format validation and critical-violation detection, not detailed policy rule proliferation.

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

Protocol body routing:
- After Protocol Agenda Map / Protocol Trigger Map / prompt router / ssot-map selects protocol targets, use `.agent/protocols/index.yaml` as a lightweight section-level grep route for targeted section markers.
- `.agent/protocols/index.yaml` is not protocol body and not a judgment gate SSOT.
- grep hits are read-route hints only; PASS/FAIL judgment remains in each protocol body.

- Completion report / TODO `[x]` update / completion eligibility decision:
  - `.agent/protocols/completion.md`
  - `.agent/protocols/reports-and-todos.md`
- Runtime claim / route / persistence / projection changes:
  - `.agent/protocols/scenario-contract.md` (Temporary Scenario Contract)
- Endpoint / frontend API proxy / repository write / admin operation / persistence mutation / DB-backed registry operation:
  - `.agent/rules/core/boundary-identity.md`
  - `.agent/protocols/runtime-boundary-matrix.md` (Runtime Boundary Failure Matrix)
- Policy value / scoring / threshold / routing / validation / projection behavior changes:
  - `.agent/rules/core/runtime-policy-magic-number.md`
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
