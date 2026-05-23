# Completion Summary Protocol (Agenda: completion-summary)

## Trigger condition

Read this protocol only when writing final completion summary output,
or when handling existing-PR follow-up comment output sink requirements.

## Scope boundary

- `## Completion Summary Template` defines final completion summary body shape.
- Initial PR body is out of template scope and remains thin entry summary.
- PR body / `make_pr` output does not replace final completion summary.

## WorkEvent Output Sink Contract

| WorkEvent.type | required_sink | optional_sink | non-substitution rule |
|---|---|---|---|
| `new_pr` | thin PR body | none | final summary does not replace thin PR body creation/update |
| `existing_pr_update` | PR follow-up comment | PR body update only if materially misleading | PR body update never replaces required PR follow-up comment |
| `local_only` | final summary | none | no remote sink is required when no remote PR update exists |

Existing PR updates require a follow-up PR comment after push unless there is no remote PR update.

For `existing_pr_update`, completion requires either:
- `POSTED + VERIFIED`, or
- `PR_COMMENT_NOT_POSTED` + exact paste-ready comment body.

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
- `optional_sink`:
  - existing_pr_update: PR body update only if materially misleading
- `state`:
  - new_pr: POSTED + VERIFIED / NOT_POSTED
  - existing_pr_update: POSTED + VERIFIED / PR_COMMENT_NOT_POSTED
  - local_only: EMITTED
- 理由:
- posted 先 (PR URL or NOT_REQUIRED):
- paste-ready comment body (existing_pr_update かつ PR_COMMENT_NOT_POSTED の場合のみ必須):

### 残タスク引き継ぎ指示

This section is Auditor TODO input, not canonical TODO closure.

- Section body details and classification rules are governed by `.agent/protocols/todo-carry-over.md`.
- Do not duplicate TODO carry-over detail rules in this protocol.
