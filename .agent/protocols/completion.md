# Completion Sequence

Mandatory completion order:

1. Inspect full branch diff (`git status --short`, `git diff -- . ":(exclude).git"`, `git diff --cached -- . ":(exclude).git"`).
2. Verify full diff against Temporary Scenario Contract when required.
3. Verify Runtime Boundary Failure Matrix coverage when required.
4. For boundary extension scope, run Boundary Identity Gate with a PR-specific temporary answer file (`bash .agent/checklists/check-boundary-identity.sh <pr-specific-temp-file>`), and record the result in the completion report.
5. Run Policy Judgment Checklist and keep 15 answers valid.
6. Run relevant local CI checks for touched scope.
7. Apply Remote CI Equivalence Gate for any scope-relevant local check that is NOT EXECUTED.
8. Apply Failure Triage Self-Recursion Gate over all executed commands and record failure triage result before any completion decision or TODO `[x]` update.
9. Apply Audit Gap Response Gate checks for any audit/completion/todo decision surface before completion or TODO `[x]` updates.
10. Apply the Recursive Verification Gate: if any blocking failure exists (FAIL, required-check failure in failure triage, unclassified failure in failure triage, required NOT EXECUTED without equivalent remote CI success, remote CI queued/in_progress, remote CI failure/cancelled/skipped-unjustified, contract/diff mismatch, matrix gap, policy violation, missing audit-gap response sections, unresolved governance GAP without required improvements/TODO handling, or report/diff contradiction), do not complete; return to fix phase within scope or leave explicit remaining TODO when out of scope.
11. Delete `.agent/tmp/tmp.txt` via `bash .agent/scripts/delete-tmp.sh` when it was created and recursive verification is complete.
12. Run `bash .agent/tests/check-structure.sh` last.
13. Only after the Recursive Verification Gate passes may `.agent/tasks/todo.md` items be marked `[x]`.

Completion report must include:

- changed files
- scenario contract verification result
- boundary matrix verification result
- boundary identity gate result (when boundary extension scope exists)
- policy judgment result
- failure triage result (failed-command inventory, classification per failure, blocking decision, and recursion action)
- audit gap response result (required sections, classification, and evidence eligibility check)
- local check status (PASS / FAIL / NOT EXECUTED)
- remote CI equivalence status for each required NOT EXECUTED local check
- tmp deletion status
- remaining TODOs

NOT EXECUTED ≠ PASS.

Environment-limited NOT EXECUTED can be completion-eligible only when all of the following are true:

- reason is explicit and verifiable,
- the skipped check is not mandatory for the changed scope or is already executed in CI with equivalent coverage,
- no blocking failure remains for the same risk surface.

Remote CI Equivalence Gate:

- NOT EXECUTED is never PASS.
- If a mandatory scope-relevant local check is NOT EXECUTED due to environment limits, completion is blocked until the equivalent GitHub Actions workflow run is completed with success.
- queued or in_progress equivalent CI is blocking (not PASS).
- failure, cancelled, or skipped-unjustified equivalent CI is blocking and requires recursion to fix phase when in-scope.
- scope-irrelevant CI must not be treated as required.

If a mandatory scope-relevant check is NOT EXECUTED and no equivalent remote CI success is verified, completion is blocked.


CI policy baseline:

- Structure Check is the always-on required gate.
- Heavy CI workflows are path-scoped.
- scope-irrelevant workflow-level skip is not blocking.
- scope-relevant workflow success is required when local equivalent is NOT EXECUTED.
- heavy CI should not be configured as always-required branch protection unless pending-skip behavior is explicitly handled.
