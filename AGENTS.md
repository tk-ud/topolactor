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

`.agent/reports/` and `.agent/tasks/todo.md` are persistent surfaces for routine / scheduled / automatically executed agents.

Use `.agent/reports/` for:

- routine inspection reports
- scheduled maintenance reports
- automated agent run outputs
- periodic structure / policy / dependency audit results

Use `.agent/tasks/todo.md` for:

- unresolved tasks discovered by routine or automated agents
- residual tasks that must survive beyond the current PR or conversation

Do not use `.agent/reports/` as the default place for PR summaries, PR audit notes, or normal implementation logs.
Do not update `.agent/tasks/todo.md` during normal PR work unless a real remaining task must be carried forward after merge.

PR audit results should normally stay in the review / conversation / PR comment surface.
Implementation summaries should normally stay in the PR description or completion message.

## Runtime Policy Checklist Gate

For any task that touches runtime behavior, data-defined topology, canonical
route steps, or frontend projection: complete `.agent/checklists/runtime-policy.md`
and run the local check before reporting completion.

```sh
# 1. Copy the checklist template and fill in all Q1–Q10 answers
cp .agent/checklists/runtime-policy.md /tmp/my-task-checklist.md
# edit /tmp/my-task-checklist.md — set each Answer: to yes / no / n/a

# 2. Validate locally
bash .agent/tests/check-runtime-policy-local.sh /tmp/my-task-checklist.md
```

The check is **local only** — it is not wired to any GitHub Actions workflow.
Fixtures in `.agent/tests/fixtures/runtime-policy/` define the expected PASS /
FAIL outcomes and serve as a reference for how to fill in the checklist.

The check script fails on:
- any answer that is not `yes`, `no`, or `n/a`
- Q5=yes (silent fallback introduced)
- Q6=yes (unexplained production runtime constant)
- Q2=yes AND Q3=no (fallback present, no explicit-error replacement)
- Q1=yes AND Q4=no (runtime value not from a policy surface)
- Q7=yes (canonical route bypassed)
- Q8=yes (business logic in frontend projection layer)
- Q9=yes (broken reference swallowed)
- Q10=no (structure check not run or not passing)

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
Agent     = local structure guard / routine report / residual task surface
```
