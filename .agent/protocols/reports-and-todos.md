# Reports and TODO Surfaces (Agenda: reports-and-todo-surfaces)

## Workflow Guard

- Treat this protocol as a report/TODO surface update after JUDGMENT.
- Do not treat report/TODO updates as a bypass for workflow order.

## Trigger condition

Read this protocol only when deciding where to store reports/summaries/TODO carry-over.

## Surface rules

`.agent/reports/` is the persistent surface for:

- routine inspection reports
- scheduled maintenance reports
- periodic audit reports

Do not place normal PR audit logs, implementation summaries, or scenario verification detail logs here.
PR-level audit results belong in PR body, PR comments, or conversation summary.

Only remaining implementation/design work that must survive beyond the PR/conversation should be copied to `.agent/tasks/todo.md`.

CI/check/remote-verification status is not a `.agent/tasks/todo.md` item by itself. Record it in the PR summary or completion report under verification/check-scope status.

If a failed or unexecuted check reveals concrete follow-up work, copy only that concrete implementation/design/SSOT task to `.agent/tasks/todo.md`; do not copy the check-running activity itself as the TODO.

## TODO carry-over rules

- `.agent/tasks/todo.md` is for unresolved implementation, design, SSOT, or test-authoring work that must survive beyond the current PR/conversation.
- `.agent/tasks/todo.md` is not for CI waiting, remote CI pass confirmation, local environment absence, or verification-only bookkeeping.
- `.agent/tasks/todo.md` is not an implementation report or PR changelog.
- Do not leave completed work logs under `[x]` items in `.agent/tasks/todo.md`.
- Do not mark a TODO `[x]` when the same item still contains concrete remaining work, partial/skeleton status, missing tests, unconnected runtime lanes, or unmet completion conditions.
- For batch PRs, each TODO item must be judged independently. A batch implementation may complete some items while leaving others open.
- If a batch PR creates a partial surface, skeleton boundary, or helper that still needs wiring/test/SSOT work, rewrite that residue as a smaller `[ ]` TODO instead of marking the parent item `[x]`.
- Completion decision and TODO `[x]` eligibility are governed by `.agent/protocols/completion.md`.
- Recursive Verification Gate, Required Check Scope Declaration Gate, Failure Triage Self-Recursion Gate, Audit Gap Response Gate, and Remote CI Equivalence Gate are defined in completion-governance SSOT.

## Required reporting sections

When producing governance audit style summaries, include:

- Governance Gaps
- Proposed Governance Improvements
- Remaining TODOs
- Completion Eligibility


Failure Triage Self-Recursion Gate Reporting Requirements are defined in `.agent/protocols/completion.md`.
Required Check Scope Declaration Gate Reporting Requirements are defined in `.agent/protocols/completion.md`.


## Roadmap Status vs TODO Responsibility

- `docs/system-roadmap.yaml` is the primary implementation status source.
- `.agent/tasks/todo.md` is a task queue, not an implementation status registry.
- Remaining TODO entries should reference roadmap `implementation_registry` entries or `known_gap_ref` where applicable.
- Do not duplicate the full roadmap status matrix into TODO.
- If TODO completion changes implementation status, update `docs/system-roadmap.yaml` in the same PR.
- CI waiting / remote pass confirmation / local tool absence remain verification-section records, not TODO queue items.
