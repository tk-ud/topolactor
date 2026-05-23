# Reports

This directory is a persistent surface for routine inspection reports,
scheduled maintenance reports, and periodic audit reports.

Use this directory for:

- routine inspection reports
- scheduled maintenance reports
- automated agent run outputs
- periodic structure / policy / dependency audit reports

Do not use this directory as the default place for normal PR summaries,
PR audit logs, implementation summaries, or scenario contract verification logs.

PR-level audit results should be written to PR description, PR comments,
or conversation summaries.

Only when an inspection report creates residual work that must survive beyond
the current PR/conversation, transfer that residual item to `.agent/tasks/todo.md`.

## Naming Convention

Use descriptive filenames with a date prefix for routine or scheduled reports:

```text
YYYY-MM-DD-<topic>.md
```

See `.agent/protocols/report-surfaces.md` for report placement and `.agent/protocols/todo-carry-over.md` for TODO carry-over rules.
