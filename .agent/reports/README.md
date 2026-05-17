# Reports

This directory is the persistent report surface for routine or automatically executed agents.

Use this directory for:

- routine inspection reports
- scheduled maintenance reports
- automated agent run outputs
- periodic structure / policy / dependency check results

Do not use this directory as the default place for normal PR summaries, implementation logs, or temporary review notes.

PR review results should normally stay in the review, conversation, or PR comment surface.
Implementation summaries should normally stay in the PR description or completion message.

If a routine or automated report creates residual work that must survive beyond the current run, place that work in `.agent/tasks/todo.md`.

## Naming Convention

Use descriptive filenames with a date prefix for routine or scheduled reports:

```text
YYYY-MM-DD-<topic>.md
```

Example:

```text
2026-05-17-structure-check-pass.md
2026-05-17-policy-check.md
```
