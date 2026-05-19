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


## Audit Gap Response Gate Reporting Requirements

When producing any governance audit report (including completion-facing audit summaries), include all of the following sections:

- Governance Gaps
- Proposed Governance Improvements
- Remaining TODOs
- Completion Eligibility

Section rules:

1. Governance Gaps
   - List findings across protocol, checklist, task design, report surface, completion behavior, and agent behavior.
   - If no gaps are found, state short evidence-based rationale.
   - Do not classify log-only output, PR-body-only narrative, or static-document confirmation as explicit behavior evidence.

2. Proposed Governance Improvements
   - Mandatory when one or more governance gaps are present.
   - Must include at least one concrete protocol/checklist/feature improvement per relevant gap cluster.

3. Remaining TODOs
   - Record unresolved implementation, verification, or governance follow-up.
   - If follow-up is needed beyond the current PR/conversation, preserve it as incomplete work in `.agent/tasks/todo.md`.

4. Completion Eligibility
   - State whether the audited item is static protocol coverage audit or behavior execution audit.
   - Static protocol coverage audit may be completion-eligible from protocol/checklist/docs evidence.
   - Behavior execution audit is not completion-eligible without observed behavior evidence artifacts.
   - If governance gaps remain, completion is blocked unless the source task is static coverage scope and unresolved gaps are explicitly preserved as remaining TODOs.

Classification policy for audit conclusions:

- PASS: evidence-backed and no remaining work for audited scope.
- GAP: governance deficiency exists and requires proposed improvement(s).
- BLOCKING: completion/merge/[x] update prohibited until resolved in-scope or explicitly deferred under gate rules.
- TODO: out-of-scope remaining work preserved as incomplete follow-up.

Conditional pass, caution, and non-fatal findings must not be treated as unconditional PASS.

