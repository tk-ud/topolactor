# Completion Sequence

Mandatory completion order:

1. Inspect full branch diff (`git status --short`, `git diff -- . ":(exclude).git"`, `git diff --cached -- . ":(exclude).git"`).
2. Verify full diff against Temporary Scenario Contract when required.
3. Verify Runtime Boundary Failure Matrix coverage when required.
4. Run Policy Judgment Checklist and keep 15 answers valid.
5. Run relevant local CI checks for touched scope.
6. Apply the Recursive Verification Gate: if any blocking failure exists (FAIL, required NOT EXECUTED, contract/diff mismatch, matrix gap, policy violation, or report/diff contradiction), do not complete; return to fix phase within scope or leave explicit remaining TODO when out of scope.
7. Delete `.agent/tmp/tmp.txt` via `bash .agent/scripts/delete-tmp.sh` when it was created and recursive verification is complete.
8. Run `bash .agent/tests/check-structure.sh` last.
9. Only after the Recursive Verification Gate passes may `.agent/tasks/todo.md` items be marked `[x]`.

Completion report must include:

- changed files
- scenario contract verification result
- boundary matrix verification result
- policy judgment result
- local check status (PASS / FAIL / NOT EXECUTED)
- tmp deletion status
- remaining TODOs

NOT EXECUTED ≠ PASS.

Environment-limited NOT EXECUTED can be completion-eligible only when all of the following are true:

- reason is explicit and verifiable,
- the skipped check is not mandatory for the changed scope or is already executed in CI with equivalent coverage,
- no blocking failure remains for the same risk surface.

If a mandatory scope-relevant check is NOT EXECUTED, completion is blocked.
