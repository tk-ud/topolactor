# Reports

This directory is the output surface for agent-generated reports and issue drafts.

Agents may place here:

- **PR audit reports** — analysis of pull request changes against architecture rules.
- **Issue drafts** — proposed issues for structural gaps, missing implementations, or policy violations.
- **Remaining TODO summaries** — snapshots of `.agent/tasks/todo.md` at a point in time.
- **Local check reports** — output from `.agent/tests/check-structure.sh` captured for review.

## Naming Convention

Use descriptive filenames with a date prefix where relevant:

```text
YYYY-MM-DD-<topic>.md
```

Example:

```text
2026-05-17-structure-check-pass.md
2026-05-17-pr-audit-issue-3.md
```

## Not Required by This Issue

Generated reports are not required for issue #3.
This directory and README are created to establish the surface for future agent output.
