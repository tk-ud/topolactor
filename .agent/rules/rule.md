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


## Temporary Scenario Contract

`.agent/tmp/tmp.txt` is not a free-form planning memo. It is a temporary scenario contract used to bind implementation work to a concrete runtime / policy / projection claim before code changes begin.

When a task touches runtime behavior, recommendation, routing, persistence, append-only logs, cache rebuilds, policy resolution, demo observability, or frontend projection claims, agents must create `.agent/tmp/tmp.txt` before implementation via `bash .agent/scripts/create-tmp.sh`.

The scenario contract must define:

1. User-visible scenario or runtime claim.
2. Entry operation / request shape.
3. Expected canonical runtime route.
4. Expected read / write / append / cache / return order.
5. Seed / fixture / policy data involved.
6. Expected emission / projection / status.
7. Required side effects, including failure / cold-start / insufficient paths.
8. Runtime Boundary Failure Matrix coverage and any intentional out-of-scope reasons.
9. Known non-goals / out-of-scope paths.

After implementation, full branch diff inspection must compare the actual diff against `.agent/tmp/tmp.txt`.

Full branch diff inspection is incomplete unless the agent verifies whether the implemented code, docs, seed data, tests, and completion report satisfy the scenario contract.

If the full diff contradicts the tmp scenario contract, the task is not complete. The agent must either fix the implementation or explicitly update the scenario contract before completing work.

Rules:
- `.agent/tmp/tmp.txt` is temporary and must not be committed.
- Create it only with `bash .agent/scripts/create-tmp.sh`.
- Verify the full branch diff against the scenario contract before Policy Judgment Checklist and before local CI.
- If the implemented diff changes the intended scenario, update the scenario contract first, then re-verify the full diff against the updated contract.
- After verification, delete it via `bash .agent/scripts/delete-tmp.sh`.
- `bash .agent/tests/check-structure.sh` must fail when `.agent/tmp/tmp.txt` remains.
- Keep `bash .agent/tests/check-structure.sh` as the final check before completion report.
- Completion report must state whether tmp was used, the scenario contract verification result, and tmp deletion status.


## Runtime Boundary Failure Matrix

For any change that adds or wires an endpoint, frontend API proxy, repository write,
admin operation, persistence mutation, or DB-backed registry operation, success tests
are not sufficient.

The temporary scenario contract and PR audit must verify the full runtime boundary matrix:

1. success path
2. authentication / authorization failure
3. request validation failure
4. malformed id / malformed payload
5. not found
6. persistence constraint failure
7. repository / backend unavailable
8. frontend proxy status propagation
9. UI-visible error state
10. post-write read consistency

If a matrix item is intentionally out of scope, completion reporting must state why.

## Agent Judgment Gate — Policy Judgment Checklist

The Policy Judgment Gate is a local-only agent self-check. It is **not CI** and
is **not connected to any GitHub Actions workflow**. It is separate from the
`.agent/tests/*.sh` local CI gate.

Scope: not limited to Runtime. Covers any change involving Runtime, validation,
promotion, disclosure, routing, projection claim, or any other policy-boundary
decision — anywhere the agent's judgment determines whether a design choice
complies with the data-defined topology architecture.

Checklist template: `.agent/checklists/policy-judgment.md`
Validation script: `.agent/checklists/check-policy-judgment.sh`

Required before completion report on any change that:
- introduces or modifies a runtime or policy-affecting value
- touches recommendation, scoring, threshold, retention, routing, validation,
  promotion, disclosure, emission, or projection behavior
- modifies Registry, Manifest, function_parameters, structure_map, package, schema
- defines or changes relation_registry FK audit / abstract migration policy
- makes a runtime or policy behavior claim in docs / README / PR summary

Judgment gate rules:

- **Checklist must be answered from the full branch diff.** Use `git status --short`, `git diff -- . ':(exclude).git'`, and `git diff --cached -- . ':(exclude).git'`.
  Answering from only the latest commit or only edited files is a violation (V10).
- **Partial diff judgment is prohibited.** If the branch diff was not inspected,
  Q12 must be `no` — which triggers V10 automatically.
- **Delegated or split work does not inherit checklist verification.** When work is
  delegated, split, or continued by another agent, every agent that makes
  implementation, policy, summary, or completion decisions must independently use
  the Policy Judgment Checklist if the task falls within its trigger scope. An
  agent must not treat another agent's checklist answer or summary as its own
  verified judgment.
- **Workflow non-connection.** This gate must not be called from `.github/workflows/`.
- **NOT EXECUTED ≠ PASS.** Missing tools → report NOT EXECUTED, not yes.
- **All green before completion report.** Gate red → no completion report.

Checklist gate violations (any → FAIL):

| Rule | Condition |
|---|---|
| V1 | Q5 = yes — silent fallback |
| V2 | Q6 = yes — unexplained production policy constant |
| V3 | Q2 = yes AND Q3 = no — fallback present, no explicit-error replacement |
| V4 | Q1 = yes AND Q4 = no — policy/runtime value not from a policy surface |
| V5 | Q7 = yes — canonical route bypassed |
| V6 | Q8 = yes — business logic in frontend projection layer |
| V7 | Q9 = yes — broken reference swallowed |
| V8 | Q10 = no — policy fields not consumed by runtime/policy executor |
| V9 | Q11 = no — demo/mock/static values not isolated |
| V10 | Q12 = no — full branch diff not inspected |
| V11 | Q14 = no — required local checks not passed |
| V12 | Q13 = no — checklist answers not based on full branch diff |
| V13 | Q15 = no — remaining TODOs not listed in completion report or PR summary |
| V14 | Any answer not in {yes, no, n/a} |
| V15 | Missing answer |
| V16 | Fewer or more than 15 answers |

## Local CI Gate

`.agent/tests/*.sh` are local CI gates for agents.

GitHub Actions workflows are audit wrappers for PR verification. They must not be treated as the agent's primary debug loop.

Required local checks:

- Always run `bash .agent/tests/check-structure.sh`.
- For DB or SQL changes, run `bash .agent/tests/check-db-schema.sh`.
- For backend or C# runtime changes, run `bash .agent/tests/check-backend-tests.sh`.
- For frontend or Fresh/Deno/Preact changes, run `bash .agent/tests/check-frontend-types.sh`.

Local CI policy:

- CI red means no commit and no push.
- If local CI is red, fix the error first.
- After fixing, rerun the relevant local CI.
- Only green local CI may proceed to commit and push.
- A missing required tool means the check was not executed, not that it passed.
- Completion reports must distinguish actual passes from environment-limited non-execution.

## Agent Report and Task Surfaces

`.agent/reports/` is for routine, scheduled, or automatically executed agent reports.

`.agent/tasks/todo.md` is for unresolved tasks discovered by routine automation, or residual tasks that must survive beyond the current PR or conversation.

Normal PR work must not use `.agent/reports/` as a summary/log output surface.

Normal PR work must not update `.agent/tasks/todo.md` unless a real remaining task must be carried forward after merge.

When no residual task exists, keep `.agent/tasks/todo.md` present but empty of task items.

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
- Run `.agent/tests/check-structure.sh` last before reporting task completion, after policy judgment, scope/claim audits, and relevant local CI.
- Use `.agent/reports/` only for routine / scheduled / automated agent reports.
- Update `.agent/tasks/todo.md` only for residual tasks that must survive beyond the current PR or conversation.
- Do not convert topolactor to CRUD or MVC.
- Do not add build steps, DB execution, or integration tests under the structure check surface.

## Checklist Anti-Bloat Rule

Policy Judgment Checklist must remain a lightweight compliance-signature gate.
Do not expand checklist questions for incident-specific one-off cases by default.

When a new policy judgment viewpoint is needed, first evaluate whether it should be captured in:
1. `AGENTS.md` scope / required-check definitions, or
2. this `rule.md` as durable judgment order and architecture rule.

Only keep the checklist focused on:
- yes/no/n/a answer validation
- major architecture/policy violation detection
- completion-readiness signature

Checker script responsibility should stay focused on answer-format validation and critical-violation detection, not detailed policy rule proliferation.

## Completion Sequence (Mandatory)

Before completion report, execute in this order:

1. Create `.agent/tmp/tmp.txt` before implementation when the task affects runtime / policy / projection behavior.
2. Implement the change.
3. Inspect full branch diff (`git status --short`, `git diff -- . ':(exclude).git'`, and `git diff --cached -- . ':(exclude).git'`).
4. Verify the full branch diff against `.agent/tmp/tmp.txt` scenario contract and Runtime Boundary Failure Matrix when tmp is required.
5. Perform Policy Judgment Checklist.
6. Audit AGENTS.md / rule.md scope applicability and required checks.
7. Audit prompt / PR summary / docs claims for runtime or policy assertions.
8. Run relevant local CI checks.
9. Delete `.agent/tmp/tmp.txt` when it was used.
10. Run `bash .agent/tests/check-structure.sh` **last**.
11. In completion report, list commands, results (`PASS` / `FAIL` / `NOT EXECUTED`), scenario contract verification result, tmp deletion status, and remaining TODOs.
