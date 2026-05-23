# .agent Directory Map

## Purpose

`.agent/` is the execution surface for repository agent governance that is subordinate to:

- `docs/governance/agent-governance-routing-ssot.yaml`
- `docs/governance/agent-governance-routing-ssot.md`

## Route Position

Canonical route:

`AGENTS.md -> .agent/rules/rule.md -> worktype decision -> .agent/README.md -> .agent/prompt/<worktype>.md -> .agent/protocols/<worktype or triggered protocol>.md -> checklist/tests`

## Directory Responsibilities

- `rules/`: always-on prohibitions, minimal workflow invariant, worktype branch.
- `routes/`: executable map between worktype and required prompt/protocol/check references.
- `prompt/`: worktype routers (purpose/trigger/reads/protocol triggers/output shape/out-of-scope).
- `protocols/`: judgment gates (blocking/pass semantics only).
- `checklists/`: viewpoint signature templates.
- `tests/`: executable route/vocabulary/structure checks.
- `docs/`: agent-side required path and ssot mapping indexes.
- `reports/`: persistent inspection reports.
- `tasks/`: unresolved work queue.
- `tmp/`: temporary artifacts only.
- `skills/`: task procedures.
- `scripts/`: helper scripts.

## Boundary Notes

- `.agent/README.md` is a directory map only.
- Token estimates, protocol body details, and completion judgment logic are out of scope here.
