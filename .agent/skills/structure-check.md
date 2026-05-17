# Skill: Structure Check

## When to Run

Run `.agent/tests/check-structure.sh` before reporting any task as complete.
Run it after creating, moving, or deleting files to catch structural drift.
Run it after any refactor that touches directories listed in `.agent/docs/required-paths.yaml`.

## What Structure Check Means

Structure check verifies:

1. Required directories exist.
2. Required files exist.
3. Architecture-critical content terms are present in key files.

It confirms the repository has the minimum expected shape for the data-defined topology runtime skeleton.

## What Structure Check Does Not Mean

- It does not confirm that the code compiles.
- It does not confirm that SQL executes correctly.
- It does not confirm that the Fresh/Deno frontend builds.
- It does not confirm that the runtime produces correct emissions.
- It does not confirm that any business workflow is correct.
- Passing this check does not mean the implementation is complete.

## How to Interpret Failures

Each failure line is prefixed with `FAIL:` and describes either:

- `Directory missing: <path>` — the directory must be created.
- `File missing: <path>` — the file must be created.
- `Term not found in <file>: "<term>"` — the file exists but is missing a required architecture term. Add the term to the file content.

Fix all failures before reporting task completion.

## Avoiding False Confidence

Structure check passes when the skeleton is present. It does not guarantee correctness.

After structure check passes:

- Verify that file contents reflect the actual intended design.
- Check `.agent/tasks/todo.md` for remaining implementation work.
- Do not conflate structural presence with behavioral correctness.
