# AGENTS.md — Repository Agent Entrypoint

This is the instruction entrypoint for all agents operating on this repository.

## Read First

Before taking any action, read:

- `.agent/rules/rule.md` — architecture rules and constraints
- `.agent/docs/structure-map.yaml` — repository structure overview
- `.agent/docs/required-paths.yaml` — required paths and content terms (SSOT)

## Run Before Reporting Completion

Always run the local structure check before reporting that a task is complete:

```sh
.agent/tests/check-structure.sh
```

The check must pass with zero failures. Do not report completion if the check fails.

## Agent Local CI Gate

`.agent/tests/*.sh` are local CI gates for agents.
GitHub Actions workflows are audit wrappers for PR verification.

Required checks:

- Always run `bash .agent/tests/check-structure.sh`.
- For DB or SQL changes, run `bash .agent/tests/check-db-schema.sh`.
- For backend or C# runtime changes, run `bash .agent/tests/check-backend-tests.sh`.
- For frontend or Fresh/Deno/Preact changes, run `bash .agent/tests/check-frontend-types.sh`.

Local CI policy:

- CI red means no commit and no push.
- If local CI is red, fix the error first.
- After fixing, rerun the relevant local CI.
- Only green local CI may proceed to commit and push.
- A missing required tool means the check was not executed, not that it passed.
- Completion reports must distinguish actual passes from environment-limited non-execution.

## Report and Task Surface

- Write reports and issue drafts under `.agent/reports/`
- Update `.agent/tasks/todo.md` to reflect remaining work

## Architecture Constraints

- Do not convert topolactor into a CRUD or MVC application.
- Do not bypass the canonical runtime route: `operation → vector → attractor → structure_map → package → schema → emission`.
- Do not add silent fallbacks. Broken references are explicit errors.
- Do not require DB credentials, real business data, or build tools for structure checks.

## Layer Summary

```text
DB        = semantic topology space
Backend   = abstract runtime / function execution space
Frontend  = Fresh / Deno / Preact physical projection space
Agent     = local structure guard / report / task surface
```