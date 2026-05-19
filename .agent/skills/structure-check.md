# Skill: Structure Check

This is a task skill, not an always-on governance protocol.
Read and apply it only when running the structure check task.

## When to Run

Run `.agent/tests/check-structure.sh` before reporting any task as complete.
Run it after creating, moving, or deleting files to catch structural drift.
Run it after any refactor that touches directories listed in `.agent/docs/required-paths.yaml`.

## What Structure Check Means

Structure check verifies required directories, required files, and architecture-critical terms.
Passing this check confirms repository structure only; it is not behavioral correctness.
