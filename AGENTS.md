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

## Policy Judgment Gate

`.agent/checklists/` contains a local-only Agent judgment gate for policy-impacting
changes. It is **not** a GitHub Actions workflow. It is separate from the
`.agent/tests/*.sh` local CI gates — those verify structure and build correctness;
this verifies policy design decisions.

**When required:** any change involving runtime behavior, data-defined topology,
Registry, Manifest, function_parameters, structure_map policy, package / schema /
component expansion, recommendation, scoring, threshold, retention, routing,
validation, promotion, disclosure, frontend projection claim, or docs / README /
PR summary that asserts runtime or policy behavior.

**Not required** for documentation-only, comment-only, purely mechanical refactor,
display-only copy / style changes, or test-fixture-only changes — unless those
contain a runtime or policy behavior claim.

```sh
# 1. Inspect full branch diff first
git diff main...HEAD

# 2. Copy checklist template and fill in all Q1–Q15 answers
cp .agent/checklists/policy-judgment.md /tmp/my-task-checklist.md
# edit /tmp/my-task-checklist.md — set each Answer: to yes / no / n/a

# 3. Validate locally
bash .agent/checklists/check-policy-judgment.sh /tmp/my-task-checklist.md

# 4. Run self-test to verify fixtures
bash .agent/checklists/check-policy-judgment.sh --self-test
```

Policy:

- **Checklist answers must be based on the full branch diff**, not only the latest
  commit or edited files. Run `git diff main...HEAD` first.
- **All green before reporting completion.** If any local check or the judgment gate
  fails, fix first.
- **NOT EXECUTED ≠ PASS.** If a required tool is missing, report NOT EXECUTED in
  the completion summary. Never report a check as passing when it was not run.
- **Remaining TODOs must be listed** in the completion report or PR summary.
- **Delegated or split work does not inherit checklist verification.** When work is
  delegated, split, or continued by another agent, each agent that makes
  implementation, policy, summary, or completion decisions must independently use
  the Policy Judgment Checklist if the task falls within its trigger scope. An
  agent must not treat another agent's checklist answer or summary as its own
  verified judgment.

Fixtures in `.agent/checklists/fixtures/policy-judgment/` define expected PASS /
FAIL outcomes and serve as a reference for correct checklist completion.

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