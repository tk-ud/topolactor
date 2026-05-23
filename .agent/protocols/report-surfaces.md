# Report Surfaces Protocol (Agenda: report-surfaces)

## Workflow Guard

- Treat this protocol as report placement routing after JUDGMENT.
- Do not treat report placement as a substitute for completion judgment.

## Trigger condition

Read this protocol only when deciding whether output belongs to `.agent/reports/`.

## `.agent/reports/` usage

`.agent/reports/` is the persistent surface for:

- routine inspection reports
- scheduled maintenance reports
- periodic audit reports

Do not place normal PR summary, implementation summary, or scenario verification detail log in `.agent/reports/`.
Those belong to PR surfaces and completion-summary surfaces.

## Read posture

- Routine implementation agents should not read this protocol unless report-surface routing is actually needed.
- Keep this protocol independent from completion-summary composition and TODO carry-over detail.
