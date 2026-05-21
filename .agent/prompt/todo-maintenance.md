# Todo Maintenance Prompt Router

## Purpose

Route loose `todo整理` / `.agent/tasks/todo.md` cleanup requests into the correct minimum surfaces.

This router absorbs vague task-material prompts such as:

- `todo整理`
- `.agent/tasks/todo.md の点検と整理`
- checked TODO cleanup
- reflect open issues into todo
- remove completed TODO work logs

This is a router, not a protocol. It selects required surfaces and triggered protocols; final judgment remains owned by `.agent/protocols/completion.md` and `.agent/protocols/reports-and-todos.md`.

## Trigger condition

Open this router when the task asks to inspect, clean, reconcile, or rewrite `.agent/tasks/todo.md`, especially when it mentions:

- checked TODO items
- open GitHub issues
- completed TODO removal
- todo reflection / synchronization
- todo surface cleanup

Do not open this router for ordinary feature implementation unless the task explicitly includes todo maintenance.

## Required reads

Minimum required surfaces:

- `.agent/tasks/todo.md`
- `.agent/protocols/reports-and-todos.md`
- `.agent/protocols/completion.md`
- `docs/system-roadmap.yaml`

When open issues are mentioned, inspect open GitHub issues and map only unresolved implementation / design / SSOT / test-authoring work into `.agent/tasks/todo.md`.

When open issue inspection cannot be executed because the required tool or remote access is unavailable, classify the issue-inspection check as `NOT_EXECUTED` / `REMOTE_REQUIRED`. Do not claim there are no unreflected open issues from local references alone.

When a TODO references a roadmap component, known gap, or implementation status, inspect the relevant `docs/system-roadmap.yaml` entry before deleting or completing the TODO.

When a TODO references target files or functions, inspect only the referenced target surfaces required to verify whether the TODO is complete.

## Scope

In scope:

- remove completed `[x]` TODO work-log entries after verifying they are actually completed
- rewrite partial / skeleton / known_gap_ref / unconnected / untested items into concrete `[ ]` TODOs
- add missing open-issue work to `.agent/tasks/todo.md` only when it represents unresolved implementation / design / SSOT / test-authoring work
- avoid duplicate TODO entries for issues already represented
- keep `.agent/tasks/todo.md` as a task queue, not a changelog

Out of scope unless explicitly requested:

- implementing the TODOs
- closing GitHub issues
- editing unrelated docs or code
- turning CI waiting, remote CI confirmation, local tool absence, or verification bookkeeping into TODO items
- copying full issue bodies into `.agent/tasks/todo.md`

## Classification rules

- `[x]` plus residual work is invalid. Remove the completed work log and preserve only remaining work as `[ ]`.
- `partial`, `skeleton`, `known_gap_ref`, `not implemented`, `unconnected`, `untested`, or unmet completion condition means the item remains `[ ]`.
- Closed issues are not added to `.agent/tasks/todo.md` unless they expose a still-unresolved concrete follow-up.
- Open issues are not automatically TODOs. Add only the actionable unresolved work that must survive beyond the current conversation.
- If an open issue is already represented by an existing TODO, do not duplicate it; refine the existing TODO only when needed.
- If open issue listing is `NOT_EXECUTED` / `REMOTE_REQUIRED`, separate the claim scope:
  - local-reference inspection result
  - remote open-issue inspection status
  - final issue-reflection judgment pending remote verification
- Do not convert `tool unavailable`, `gh missing`, remote access failure, or API failure into a clean "no unreflected open issues" claim.
- If evidence is insufficient to delete a TODO, keep it open and narrow it to the concrete verification or implementation gap.

## Completion expectations

Before push / PR update:

- run `bash .agent/tests/check-structure.sh` last
- if this is an existing PR update, follow `.agent/protocols/reports-and-todos.md` and add a follow-up PR comment after push
- completion summary must include PASS / FAIL / NOT_EXECUTED / REMOTE_REQUIRED checks and remaining TODOs

## Expected output shape

Summaries should separate:

- removed completed TODOs
- rewritten remaining TODOs
- open issues added to TODO
- open issues intentionally not added and why
- open issue inspection status, including NOT_EXECUTED / REMOTE_REQUIRED when applicable
- checks executed / not executed
- remaining TODOs
