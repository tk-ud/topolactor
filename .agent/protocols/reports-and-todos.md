# Reports and TODO Surfaces (Agenda: reports-and-todo-surfaces)

## Workflow Guard

- Treat this protocol as a report/TODO surface update after JUDGMENT.
- Do not treat report/TODO updates as a bypass for workflow order.

## Trigger condition

Read this protocol only when deciding where to store reports/summaries/TODO carry-over.

Also read this protocol when updating an existing PR after follow-up fixes, CI reruns, audit findings, or remaining-risk classification.

## Surface rules

`.agent/reports/` is the persistent surface for:

- routine inspection reports
- scheduled maintenance reports
- periodic audit reports

Do not place normal PR audit logs, implementation summaries, or scenario verification detail logs here.
PR-level audit results belong in PR body, PR comments, or conversation summary.

Only remaining implementation/design work that must survive beyond the PR/conversation should be copied to `.agent/tasks/todo.md`.

CI/check/remote-verification status is not a `.agent/tasks/todo.md` item by itself. Record it in the PR summary or completion report under verification/check-scope status.

If a failed or unexecuted check reveals concrete follow-up work, copy only that concrete implementation/design/SSOT task to `.agent/tasks/todo.md`; do not copy the check-running activity itself as the TODO.

## PR body and follow-up comment policy

- Keep the PR body as a thin entry summary: purpose, high-level scope, and durable references only.
- Do not use the PR body as a rolling implementation log, CI status ledger, or follow-up audit thread.
- When a PR receives follow-up fixes, CI re-runs, audit findings, or remaining-risk notes, add a PR comment instead of continuously rewriting the PR body.
- Existing PR updates require a follow-up PR comment after push unless the only change is a purely local draft with no remote PR.
- For existing PR updates, treat follow-up comment state as mandatory completion evidence: (a) posted in PR conversation after push, or (b) `PR_COMMENT_NOT_POSTED` + exact paste-ready comment body in final summary when posting is unavailable.
- Follow-up PR comments for existing PR updates must follow the `## Completion Summary Template` structure.
- `## Completion Summary Template` scope is not limited to follow-up comments; initial Codex / Agent final completion summaries and completion reports also use this template.
- Initial PR body is out of template scope and must remain a thin entry summary.
- If the environment cannot post a PR comment, the final summary must include `PR_COMMENT_NOT_POSTED` and the exact comment body to paste.
- Chat/final summary by itself is never a substitute for required existing-PR follow-up comment handling.
- Passing local checks (including structure/completion checks) does not imply follow-up comment was posted.
- Do not claim the follow-up comment was posted unless it was actually posted to the PR conversation.
- If the PR body becomes materially false or misleading, update it only to restore a thin, accurate entry summary.
- Detailed pass/fail/not executed notes, CI failure indexes, audit responses, and residual TODO classification belong in PR comments or completion summaries, not in the PR body.
- This keeps the PR body stable while preserving follow-up traceability in chronological comments.

## Prompt Type / Work Type Output Switch

Use this switch to decide output surface, required external action, body template, and evidence branch.

| Work Type | PR body | PR comment | Final summary | Required action | Evidence |
|---|---|---|---|---|---|
| new PR | thin entry summary | NOT_REQUIRED | REQUIRED | create/update thin PR body | PR URL / head commit |
| existing PR update | update only if materially misleading | REQUIRED | REQUIRED | post follow-up PR comment after push and verify posted state | `POSTED + VERIFIED` or `PR_COMMENT_NOT_POSTED` + exact paste-ready body |
| todo-maintenance existing PR | update only if materially misleading | REQUIRED | REQUIRED | post follow-up PR comment after push and verify posted state | `POSTED + VERIFIED` or `PR_COMMENT_NOT_POSTED` + exact paste-ready body |
| local-only draft (no remote PR update) | NOT_REQUIRED | NOT_REQUIRED | REQUIRED | no remote action | local-only reason + no remote PR update fact |

Switch invariant:
- `## Completion Summary Template` decides body shape only.
- External actions (for example PR comment posting) are decided by this switch and must be executed/verified separately.



## Completion Summary Template scope mapping

Completion Summary Template is the terminal reporting endpoint for completion-governance output surfaces.
No prompt router or workflow note may replace it with an alternate final summary shape.
Completion Summary Template defines body shape.
It does not satisfy required external actions by itself.

- summary source:
  - `.agent/protocols/reports-and-todos.md` / `## Completion Summary Template`
- summary sinks:
  - Codex / Agent final completion summary (initial work completion included)
  - completion report
  - existing PR follow-up comment (required only for existing PR updates)
  - PR body is excluded from this template and stays under thin entry summary policy
- existing PR follow-up comment mapping (inside template, no extra appendix format):
  - changed summary → `### 作業内容`
  - checks as PASS / FAIL / NOT_EXECUTED / REMOTE_REQUIRED → `### test結果` (`#### Local` / `#### Remote CI` / `#### Required check scope`)
  - remaining TODOs → `### 残タスク引き継ぎ指示`
  - PR body thin state or update reason when materially misleading → `### 作業内容` または `### 変更ファイル`
  - `PR_COMMENT_NOT_POSTED` fallback (when posting unavailable) → same template body must be emitted as exact paste-ready comment content in final summary
- excluded surfaces:
  - initial PR body
  - thin PR body
  - PR metadata generated by `make_pr` tool

For existing PR updates, final summary without either `POSTED + VERIFIED` evidence or `PR_COMMENT_NOT_POSTED` paste-ready body is incomplete.


## Completion Summary Template

Implementation agent must write completion / follow-up summaries using this shape.

### 作業内容

- 対応 Issue / Gap:
- 目的:
- 実装したこと:
- 実装しなかったこと / OUT_OF_SCOPE:
- partial / skeleton / known_gap が残る場合の理由:

### 変更ファイル

- `path`
  - 変更内容:
  - 対象関数:
  - 意味境界:

### test結果

#### Local
- `command`: PASS / FAIL / NOT_EXECUTED
  - 理由:

#### Remote CI
- `workflow/job`: PASS / FAIL / PENDING / NOT_REQUIRED
  - 理由:

#### Required check scope
- `command`: REQUIRED_EXECUTED / REQUIRED_NOT_EXECUTED / NOT_REQUIRED / OUT_OF_SCOPE
  - remote CI 代替が必要か:

### 残タスク引き継ぎ指示

This section is Auditor TODO input, not canonical TODO closure.

#### 残すべき TODO 候補
- [ ] 内容:
      → 理由:
      → 対象ファイル:
      → 対象関数:
      → 完了条件:

#### 削除 / close 候補
- 内容:
  → 理由:
  → 監査役確認ポイント:

#### roadmap 更新候補
- component:
  - 現状:
  - 更新候補:
  - 根拠:
  - known_gap_ref:

#### TODOに入れてはいけない検証メモ
- CI待ち:
- local tool不足:
- remote CI確認待ち:

## TODO carry-over rules

- `.agent/tasks/todo.md` is for unresolved implementation, design, SSOT, or test-authoring work that must survive beyond the current PR/conversation.
- `.agent/tasks/todo.md` is not for CI waiting, remote CI pass confirmation, local environment absence, or verification-only bookkeeping.
- Implementation agent should normally propose canonical TODO/roadmap actions as Auditor TODO inputs, not finalize closure by self-assertion.
- Completion/follow-up summary `残タスク引き継ぎ指示` is Auditor TODO input, not canonical TODO closure.
- Final reflection into `.agent/tasks/todo.md` is auditor responsibility by default.
- Implementation agent may directly update canonical TODO/roadmap only when completion protocol direct-update conditions are all explicitly satisfied.
- If any condition is unmet/unknown, or partial/skeleton/known_gap remains, defer to Auditor TODO input instead of self-closing canonical TODO.
- Auditor is the default finalizer for canonical TODO/roadmap updates unless completion protocol direct-update conditions are explicitly satisfied.
- `.agent/tasks/todo.md` is not an implementation report or PR changelog.
- Do not leave completed work logs under `[x]` items in `.agent/tasks/todo.md`.
- Do not mark a TODO `[x]` when the same item still contains concrete remaining work, partial/skeleton status, missing tests, unconnected runtime lanes, or unmet completion conditions.
- For batch PRs, each TODO item must be judged independently. A batch implementation may complete some items while leaving others open.
- If a batch PR creates a partial surface, skeleton boundary, or helper that still needs wiring/test/SSOT work, rewrite that residue as a smaller `[ ]` TODO instead of marking the parent item `[x]`.
- Completion decision and TODO `[x]` eligibility are governed by `.agent/protocols/completion.md`.
- Recursive Verification Gate, Required Check Scope Declaration Gate, Failure Triage Self-Recursion Gate, Audit Gap Response Gate, and Remote CI Equivalence Gate are defined in completion-governance SSOT.

## Required reporting sections

When producing governance audit style summaries, include:

- Governance Gaps
- Proposed Governance Improvements
- Remaining TODOs
- Completion Eligibility


Failure Triage Self-Recursion Gate Reporting Requirements are defined in `.agent/protocols/completion.md`.
Required Check Scope Declaration Gate Reporting Requirements are defined in `.agent/protocols/completion.md`.


## Roadmap Status vs TODO Responsibility

- `docs/system-roadmap.yaml` is the primary implementation status source.
- `.agent/tasks/todo.md` is a task queue, not an implementation status registry.
- Remaining TODO entries should reference roadmap `implementation_registry` entries or `known_gap_ref` where applicable.
- Do not duplicate the full roadmap status matrix into TODO.
- If TODO completion changes implementation status, update `docs/system-roadmap.yaml` in the same PR.
- When implementation-side certainty is insufficient, keep roadmap/TODO closure as Auditor TODO and defer canonical status finalization to auditor judgment.
- CI waiting / remote pass confirmation / local tool absence remain verification-section records, not TODO queue items.
