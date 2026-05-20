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
