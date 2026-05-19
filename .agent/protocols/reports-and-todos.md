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



## Required Check Scope Declaration Gate Reporting Requirements

Before Completion Eligibility judgment in any completion-facing report or governance audit summary, declare required check scope from changed files and changed behavior surfaces.

Per-check status must use exactly one label:

- REQUIRED_EXECUTED
- REQUIRED_NOT_EXECUTED
- NOT_REQUIRED
- OUT_OF_SCOPE

Rules:

1. REQUIRED_NOT_EXECUTED requires remote CI equivalence evidence or explicit Remaining TODO preservation.
2. NOT_REQUIRED and OUT_OF_SCOPE require explicit scope rationale.
3. Missing declarations or ambiguous status assignments are BLOCKING.
4. For doc-only / governance-only scope, still evaluate and declare at minimum: policy judgment need, structure check need, checklist self-test need, and report/todo/completion-protocol consistency checks.
5. Policy judgment need classification is mandatory and must use one allowed value; ambiguous declarations such as `NOT_REQUIRED` are prohibited.
5. If any declared check failed, pass control to Failure Triage Self-Recursion Gate before completion eligibility decision.
6. If changed scope touches `infra/docker-compose.yml`, `.agent/scripts/bootstrap-local-postgres.sh`, or DB init path, required check scope must include docker-compose/bootstrap verification with one of: REQUIRED_EXECUTED, or REQUIRED_NOT_EXECUTED + remote CI equivalent success, or REQUIRED_NOT_EXECUTED + incomplete TODO.

## Failure Triage Self-Recursion Gate Reporting Requirements

Before Completion Eligibility judgment in any audit/completion-facing report, triage all executed-command failures.

Required per-failure classification:

- required check failure
- exploratory / usage-confirmation failure
- expected negative test
- out-of-scope failure

Rules:

1. required check failure => BLOCKING.
2. unclassified failure => BLOCKING.
3. exploratory / usage-confirmation failure requires rationale for non-required status.
4. expected negative test requires expected-failure intent and success condition evidence.
5. failure triage must be completed before TODO `[x]` updates.
6. if triage finds blocking conditions, recurse without waiting for user instruction (fix phase, report reclassification, TODO rollback, or Remaining TODO preservation).

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

Registry/topology semantics audits must classify drift/GAP using `.agent/protocols/registry-tensor-policy.md` when the change touches SSOT, recommendation runtime design, registrar UI spec, or topology governance policy text.

Conditional pass, caution, and non-fatal findings must not be treated as unconditional PASS.


## Registry Tensor Projection Continuity Lightweight Gate

For registry tensor continuity routine/periodic audits, use:

- checklist template: `.agent/checklists/registry-tensor-projection-continuity.md`
- static validator: `bash .agent/checklists/check-registry-tensor-projection-continuity.sh <checklist-file>`

Gate intent is intentionally lightweight: validate 6-surface presence (runtime/endpoint/scheduler/function/UI/DB), write/read surface visibility, explicit unimplemented boundaries, and remaining TODO preservation.


## Negative Consistency Gate Reporting Requirements

Negative Consistency is executed at checklist/verification time.

Use:

- checklist template: `.agent/checklists/negative-consistency.md`
- static validator: `bash .agent/checklists/check-negative-consistency.sh <checklist-file>`

Completion summaries must report:

- checklist execution result (PASS/FAIL),
- blocking decision,
- failure reasons,
- remaining TODO / out-of-scope linkage for unresolved risk.

Do not require summary-time re-printing of Q1/Q2/Q3 question text.

Rules:

1. `Answer: 問題なし` with empty `Evidence` is BLOCKING.
2. Evidence must mention at least one of: diff / test / CI / contract / checklist / TODO.
3. Any remaining risk must be connected to explicit remaining TODO or explicit out-of-scope reason.
4. If required checks are NOT EXECUTED without equivalent remote CI PASS, if CI is not PASS, or if blocking failures remain unresolved, Q3 must be `問題あり` and completion eligibility is BLOCKING.
5. REQUIRED_NOT_EXECUTED is never PASS-equivalent; preserve as unresolved until remote CI equivalent success or explicit incomplete TODO.
6. backend-tests CI success is not equivalent evidence for docker-compose/bootstrap/db-init verification.
