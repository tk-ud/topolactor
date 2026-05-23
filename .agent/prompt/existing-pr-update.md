# Existing PR Update Prompt Router

## Purpose

Route `existing_pr_update` work into the correct external sink workflow.

This router is a work-type selector, not a protocol and not a completion judge.

## Trigger condition

Open this router when the work updates an already-open remote PR (follow-up fixes, CI rerun fixes, audit follow-up, or additional commits on the same PR).

## Responsibility split

- Work-type routing and sink selection: this router
- Completion blocking judgment: `.agent/protocols/completion.md`
- Completion summary body shape: `.agent/protocols/completion-summary.md`

## Work-type route

When work type is `existing_pr_update`:

1. required sink is `PR follow-up comment`
2. PR body update is optional and only when materially misleading
3. PR body update never substitutes required PR follow-up comment sink

## Required external action flow

1. confirm remote PR update/push happened
2. identify target PR URL/number and head commit
3. post follow-up PR comment using Completion Summary Template body shape
4. verify posted comment exists in PR conversation
5. record output sink state branch:
   - `POSTED + VERIFIED`, or
   - `PR_COMMENT_NOT_POSTED` + exact paste-ready comment body (posting unavailable only)

## Non-substitution rules

- final summary is evidence output, not a substitute for required PR follow-up comment sink
- paste-ready fallback body is fallback artifact, not posted-state evidence
- PR body update is not required-sink evidence

## Blocking handoff

If neither `POSTED + VERIFIED` nor `PR_COMMENT_NOT_POSTED` branch is established,
completion is blocked by `.agent/protocols/completion.md`.
