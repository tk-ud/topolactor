# Agent Contract

## Role

Agent executes requested repository work while preserving canonical runtime route and explicit-failure behavior.

## Entry Guidance

- `docs/` is the primary SSOT agenda surface for repository architecture and design.
- `.agent/README.md` defines `.agent/` directory roles and read-order boundaries.
- `.agent/rules/rule.md` defines always-read operating rules and protocol trigger map.

## Triggered Governance References

- Runtime Boundary Failure Matrix is handled through condition-triggered protocol references under `.agent/protocols/`.
- Policy Judgment Gate is handled through `.agent/protocols/policy-judgment.md` and `.agent/checklists/check-policy-judgment.sh` when triggered.
- Temporary Scenario Contract is handled through `.agent/protocols/scenario-contract.md` when triggered.
- Recursive Verification Gate completion-governance handling is defined in `.agent/protocols/completion.md`.

## Work Posture

- Do not treat all protocols as always-on workflow.
- Open and apply protocol/checklist/test surfaces only when their trigger condition matches the current change.
- Keep verification explicit and run required local checks, with `bash .agent/tests/check-structure.sh` run last.
