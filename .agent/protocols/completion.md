# Completion Sequence

Mandatory completion order:

1. Inspect full branch diff (`git status --short`, `git diff -- . ':(exclude).git'`, `git diff --cached -- . ':(exclude).git'`).
2. Verify full diff against Temporary Scenario Contract when required.
3. Verify Runtime Boundary Failure Matrix coverage when required.
4. Run Policy Judgment Checklist and keep 15 answers valid.
5. Run relevant local CI checks for touched scope.
6. Delete `.agent/tmp/tmp.txt` via `bash .agent/scripts/delete-tmp.sh` when it was created.
7. Run `bash .agent/tests/check-structure.sh` last.

Completion report must include:

- changed files
- scenario contract verification result
- boundary matrix verification result
- policy judgment result
- local check status (PASS / FAIL / NOT EXECUTED)
- tmp deletion status
- remaining TODOs

NOT EXECUTED ≠ PASS.
