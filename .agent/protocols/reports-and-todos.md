# Reports and TODO Surfaces

`.agent/reports/` is the persistent surface for:

- routine inspection reports
- scheduled maintenance reports
- periodic audit reports

Do not place normal PR audit logs, implementation summaries, or scenario verification detail logs here.

PR-level audit results belong in PR body, PR comments, or conversation summary.

Only remaining work that must survive beyond the PR/conversation should be copied to `.agent/tasks/todo.md`.

Recursive Verification Gate and TODO state:

- Mark `.agent/tasks/todo.md` entries as `[x]` only after the Recursive Verification Gate passes.
- If audit detects a blocking failure, keep TODO as incomplete (or revert to incomplete) and record explicit remaining TODO for the required fix.
- Do not represent incomplete verification as completion.
