# Report Surfaces Protocol (Agenda: report-surfaces)

## Trigger condition

Read this protocol only when deciding where inspection/maintenance/audit report outputs should live.
Normal implementation agents should usually not need this protocol.

## `.agent/reports/` usage

`.agent/reports/` is the persistent surface for:

- routine inspection reports
- scheduled maintenance reports
- periodic audit reports

Do not place the following in `.agent/reports/`:

- normal PR summary
- implementation summary
- scenario verification detail log

Those belong in PR body/PR comments/final completion summary surfaces according to completion governance.
