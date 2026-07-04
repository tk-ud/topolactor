# Agent Contract

## Role

Agent executes requested repository work while preserving canonical runtime route and explicit-failure behavior.

## Entry Route

- Start by reading `.agent/rules/rule.md` for always-read operating rules and trigger map.
- If the executing agent is Claude Code on the web / remote execution environment, read `.agent/protocols/claude.md` at READ_ENTRY as an environment prerequisite route before further task checks.
- Then read `.agent/README.md` for the `.agent` directory map and operating route.
- Then read `.agent/skills/agent-workflow.md` for workflow procedure order.
- Tool-first: when `.agent/tools/agent-ui-initial-contract` is usable, run it to resolve worktype/prompt routing and target SSOT sections (see `.agent/tools/README.md`) instead of manually opening every prompt/protocol surface.
- Fallback: when the tool is not usable, read matching `.agent/prompt/<work-type>.md` as the lightweight router.

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

## Top-Level Directory Agenda

- `.agent/`
  - Agent-facing documentation, rules, prompts, protocols, checklists, tests, tasks, reports, and helper scripts.
  - Repository work starts from `AGENTS.md`, then follows `.agent/rules/rule.md`, `.agent/README.md`, `.agent/skills/agent-workflow.md`, and the matching worktype route.
  - Do not treat all `.agent/` files as always-read scope.

- `docs/`
  - Canonical design / governance SSOT surface.
  - Architecture, runtime boundaries, data model, and implementation judgment must be checked against the matching docs.

- `db/`
  - Semantic topology space.
  - Owns database schema, topology tables, promotion tables, SQL attention logs, seeds, and persistence-side topology authority.

- `backend/`
  - Abstract runtime.
  - Owns thin HTTP endpoint boundary, runtime executor, operation vector / attractor resolution, semantic mapper, repository boundary, guards, and runtime contract schemas.

- `frontend/`
  - Physical projection space.
  - Fresh / Deno / Preact frontend.
  - Owns routes, islands, UI components, package projection, frontend schema, registry, runtime executor, and backend API clients.

- `infra/`
  - Local/demo/bootstrap hosting boundary.
  - Owns compose wiring, nginx reverse proxy route boundary, and local compose environment template.
  - Does not own runtime policy, topology semantics, database schema authority, or frontend projection judgment.

- `articles/`
  - Public article / explanatory surface.
  - Not authoritative implementation SSOT.
