# Draft: Agent Development OS PR Review Loop

This is a draft operating memo for the future PR-centered Agent Development OS loop.

It is not an execution rule source. `AGENTS.md` and `.agent/` remain authoritative for agent behavior.
This document captures the target shape: agent work happens in pull requests, semantic review happens as review/comment feedback, and merge remains a human decision.

## Goal

Move the current chat-driven review loop into a repository-native PR loop.

Current manual loop:

```text
user prompt
-> agent implementation
-> chat-based GPT audit
-> prompt/comment returned to agent
-> agent follow-up fix
-> human merge decision
```

Target repository loop:

```text
agent draft PR
-> CI structural/type/runtime checks
-> GPT semantic PR review/comment
-> webhook returns review feedback to agent
-> agent follow-up commit/comment
-> CI + semantic review repeat
-> human marks ready/merges
```

The goal is not full autonomous merge. The goal is a governed feedback loop where agents can repair their own PRs while humans retain the final design and merge authority.

## Roles

### Implementation Agent

Examples: Claude Code, Codex, or another code agent.

Responsibilities:

- create or update a draft PR;
- read `AGENTS.md` and triggered `.agent` surfaces;
- implement within the requested lane;
- preserve remaining TODOs instead of over-claiming completion;
- respond to PR review/comment feedback in the same PR;
- post follow-up comments after push when the PR already exists.

### CI Gates

Responsibilities:

- detect structural, type, runtime, and pipeline-continuity failures;
- expose failures as indexes to the relevant surface;
- prevent merge-ready claims while required checks fail;
- avoid replacing semantic judgment.

CI is a gate and an index. It is not the final meaning authority.

### Semantic Reviewer

Example: GPT PR review agent.

Responsibilities:

- audit semantic consistency, not just structural diff scope;
- verify roadmap / TODO / completion-claim alignment;
- classify failures into implementation fixes, SSOT gaps, protocol gaps, or remaining TODOs;
- leave PR review/comment feedback that can be routed back to the implementation agent;
- avoid merging.

### Human Maintainer

Responsibilities:

- decide whether the design direction is acceptable;
- resolve unclear design subject or product direction;
- decide whether to merge;
- decide when a gap should become a new issue, prompt, or lane.

The human maintainer is the merge gate.

## PR State Model

```text
DRAFT_CREATED
-> AGENT_IMPLEMENTING
-> CI_RUNNING
-> SEMANTIC_REVIEW_REQUESTED
-> REVIEW_FEEDBACK_POSTED
-> AGENT_FOLLOW_UP
-> READY_FOR_HUMAN_REVIEW
-> HUMAN_MERGED | HUMAN_REJECTED | HUMAN_SPLIT_REQUIRED
```

A PR should not be treated as merge-ready just because an agent completed a local summary.
Merge readiness requires CI status, semantic review status, and remaining TODO classification to agree.

## Feedback Transport

The future loop should treat PR reviews and PR comments as the durable transport surface.

```text
GPT review/comment
-> GitHub webhook
-> dispatcher
-> selected agent runner
-> same PR follow-up commit
-> follow-up PR comment
```

Comments should carry enough state for continuation:

- reviewed commit SHA;
- blocking findings;
- target files/functions;
- required SSOT/protocol references;
- checks PASS / FAIL / NOT_EXECUTED / REMOTE_REQUIRED;
- remaining TODOs;
- whether the PR body is intentionally thin.

Chat-only summaries are useful during experimentation, but they are not durable enough for the final loop.

## Lane Lock Policy

Parallel agent PRs are useful only when their lanes do not collide.

Recommended lock rules:

- one active PR per `known_gap_ref`;
- one active PR touching `.agent/tasks/todo.md` at a time;
- one active PR touching `docs/system-roadmap.yaml` at a time;
- one active PR per runtime surface when the same target functions are involved;
- follow-up fixes should stay inside the same PR whenever possible;
- after merge, the next agent task must re-read `main`.

Safe parallel lanes:

- backend runtime implementation that does not touch the same roadmap/TODO entries;
- frontend-only projection work isolated from backend runtime lanes;
- public documentation draft work that does not change execution protocols;
- `.agent` protocol work when no implementation PR depends on the same protocol surface.

High-conflict lanes:

- `.agent/tasks/todo.md` cleanup;
- `docs/system-roadmap.yaml` status changes;
- shared runtime routing files;
- protocol changes that affect current active PR behavior.

## Interruption Handling

Agent runs may stop because of output-token limits, context-window limits, quota limits, or tooling failures.

Interrupted runs must not claim completion.

Suggested interruption statuses:

```text
COMPLETED
INCOMPLETE_MAX_TOKENS
INCOMPLETE_CONTEXT_WINDOW
INCOMPLETE_QUOTA_LIMIT
FAILED_TOOLING
```

When interrupted, the agent should leave a checkpoint comment instead of a completion claim.

Checkpoint comments should include:

- current status;
- files changed so far;
- checks executed / not executed;
- last known blocking issue;
- next suggested step;
- whether a commit was pushed;
- remaining TODOs.

`INCOMPLETE_*` is not completion. It must not trigger TODO `[x]`, implemented claims, production-ready claims, or merge-ready claims.

## PR Body Policy

The PR body is a thin entry summary.

It should contain:

- purpose;
- high-level scope;
- durable references;
- known remaining gaps if they define the PR boundary.

It should not become:

- a rolling implementation log;
- a CI status ledger;
- a full audit thread;
- a replacement for roadmap / TODO surfaces.

Follow-up details belong in PR comments or PR reviews.

## Merge Rule

Agents may propose, implement, repair, and explain.

CI may block.

Semantic review may request changes.

Only humans merge.

This preserves the central invariant of the Agent Development OS: agents can operate inside a governed repository loop, but the final design-subject decision remains human-owned.

## Open Design Questions

- Should the semantic reviewer post a GitHub review (`REQUEST_CHANGES` / `COMMENT`) or a normal PR comment?
- Should webhook dispatch choose Claude, Codex, or another agent based on changed files and failure type?
- Should lane locks be represented as labels, issue links, or a repository-local lock file?
- Should interrupted agent runs create a checkpoint comment automatically before termination?
- Should `.agent` expose a dedicated `agent-run-interruption` protocol, or keep interruption handling inside completion governance?
- How much of the PR review loop should be public documentation versus repository-local `.agent` protocol?
