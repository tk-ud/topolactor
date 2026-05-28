# Agent Contract

## Role

Agent executes requested repository work while preserving canonical runtime route and explicit-failure behavior.

## Entry Route

- Start by reading `.agent/rules/rule.md` for always-read operating rules and trigger map.
- If the executing agent is Claude Code on the web / remote execution environment, read `.agent/protocols/claude.md` at READ_ENTRY as an environment prerequisite route before further task checks.
- Then read `.agent/README.md` for the `.agent` directory map and operating route.
- When applicable to the work type, read matching `.agent/prompt/<work-type>.md` as the lightweight router.

## Triggered Governance References

- Runtime Boundary Failure Matrix is handled through condition-triggered protocol references under `.agent/protocols/`.
- Policy Judgment Gate is handled through `.agent/protocols/policy-judgment.md` and `.agent/checklists/check-policy-judgment.sh` when triggered.
- Temporary Scenario Contract is handled through `.agent/protocols/scenario-contract.md` when triggered.
- Recursive Verification Gate completion-governance handling is defined in `.agent/protocols/completion.md`.

## Work Posture

- Do not treat all protocols as always-on workflow.
- Do not treat all docs as always-read scope.
- Do not treat all skills as always-read scope.
- Open and apply protocol/checklist/test surfaces only when their trigger condition matches the current change.
- Keep verification explicit and run required local checks, with `bash .agent/tests/check-structure.sh` run last.
