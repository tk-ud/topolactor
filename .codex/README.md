# Codex Local Verification Contract

## Entry Contract

Start from the repository-level agent contract at `AGENTS.md`. The `.codex/`
instructions are local Codex additions and do not replace the top-level
workflow, routing, or verification requirements.

## Local Environment Assumption

This repository's local Codex environment is expected to have Docker, Deno, and
.NET available. Missing required tooling is an explicit verification failure,
not a pass or a reason to silently skip the matching gate.

## GitHub Workflow Equivalent

When working locally, run the repository's local CI wrapper as the default
verification route before completion:

```sh
bash .agent/tests/check-local-ci.sh
```

`check-local-ci.sh` is the local entrypoint for the GitHub workflow-equivalent
gate set. It runs:

- `bash .agent/tests/check-unified-test-gate.sh`
- `bash .agent/tests/check-runtime-environment.sh`
- `bash .agent/tests/check-structure.sh`

The structure check must remain last. Do not replace this local route with only
targeted checks unless the task is explicitly inspection-only or the result is
reported as partial verification.

## Targeted Checks

Targeted checks may be run during implementation for faster feedback, but they
do not replace the local CI wrapper when Docker, Deno, and .NET are available.
If a targeted check is the only verification performed, report that the GitHub
workflow-equivalent local gate was not run.
