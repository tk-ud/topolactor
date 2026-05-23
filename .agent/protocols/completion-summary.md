# Completion Summary Protocol (Agenda: completion-summary)

## Workflow Guard

- Use this protocol for terminal completion summary / existing-PR follow-up output handling.
- Completion Summary Template defines body shape; it is not an action substitute.

## Trigger condition

Read this protocol when composing:

- final completion summary
- completion report output body
- existing PR update follow-up PR comment body

## PR body separation

- final completion summary must use this template.
- initial PR body is out of template scope and stays a thin entry summary.
- PR body or `make_pr` output does not replace final completion summary.

- Existing PR updates require a follow-up PR comment after push unless no remote PR update exists.
- Completion Summary Template is the single terminal reporting endpoint and must be emitted inside this template only.

## Prompt Type / Work Type Output Switch

| Work Type | PR body | PR comment | Final summary | Required action | Evidence |
|---|---|---|---|---|---|
| new PR | thin entry summary | NOT_REQUIRED | REQUIRED | create/update thin PR body | PR URL / head commit |
| existing PR update | update only if materially misleading | REQUIRED | REQUIRED | post follow-up PR comment after push and verify posted state | `POSTED + VERIFIED` or `PR_COMMENT_NOT_POSTED` + Completion Summary全文 (manual paste unit) |
| todo-maintenance existing PR | update only if materially misleading | REQUIRED | REQUIRED | post follow-up PR comment after push and verify posted state | `POSTED + VERIFIED` or `PR_COMMENT_NOT_POSTED` + Completion Summary全文 (manual paste unit) |
| local-only draft (no remote PR update) | NOT_REQUIRED | NOT_REQUIRED | REQUIRED | no remote action | local-only reason + no remote PR update fact |

## WorkEvent Output Sink Contract

| WorkEvent.type | required_sink | optional_sink | non-substitution rule |
|---|---|---|---|
| `new_pr` | thin PR body | none | final summary does not replace thin PR body creation/update |
| `existing_pr_update` | PR follow-up comment | PR body update only if materially misleading | PR body update never replaces required PR follow-up comment |
| `local_only` | final summary | none | no remote sink is required when no remote PR update exists |

Structural completion rule for `existing_pr_update`:
- completion requires either `POSTED + VERIFIED` or `PR_COMMENT_NOT_POSTED` + Completion Summary全文 (manual paste unit).
- final summary is required evidence output, not a substitute for PR comment posting.

## Completion Summary generation rules (not emitted section)

- `Summary`, `Testing`, `Output Sink State`, `残TODO` をテンプレート外の自由形式セクションで追加しない。
- `Testing` は必ず `### test結果` に統合する。
- PR follow-up comment posting state は `### output sink state` に記録する。
- local required check 未実行は `#### Required check scope` で `REQUIRED_NOT_EXECUTED` を記録する。
- `### 残タスク引き継ぎ指示` は Auditor TODO input であり canonical TODO closure ではない。
- Do not create a separate paste-ready body block; the full Completion Summary is the copy/paste unit.

## Completion Summary Template

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

### output sink state

- `WorkEvent.type`: new_pr / existing_pr_update / local_only
- `required_sink`:
  - new_pr: thin PR body
  - existing_pr_update: PR follow-up comment
  - local_only: final summary
- `state`:
  - new_pr: POSTED + VERIFIED / NOT_POSTED
  - existing_pr_update: POSTED + VERIFIED / PR_COMMENT_NOT_POSTED
  - local_only: EMITTED
- 理由:
- posted 先 (PR URL or NOT_REQUIRED):
- manual paste unit (existing_pr_update かつ PR_COMMENT_NOT_POSTED の場合のみ必須):

````
<Completion Summary全文>
````

### 残タスク引き継ぎ指示

This section is Auditor TODO input, not canonical TODO closure.
