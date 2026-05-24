# existing_pr_update prompt router

## purpose
Follow-up work for an already-open PR based on reviewed diff and residual gaps.

## trigger_condition
Worktype is `existing_pr_update`, including:
- 既存PRへ追加修正
- PR差分レビュー後 follow-up 作成
- merge前後の残ズレ修正
- PR #xxx 差分に対する追加commit/追加prompt作成

## required_reads
- PR diff or patch
- changed files
- previous review findings or explicit user findings
- .agent/tasks/todo.md
- docs/system-roadmap.yaml
- target implementation files
- .agent/protocols/completion-summary.md

## optional_reads
- .agent/docs/ssot-map.yaml when touched surface exists and relevant SSOT/protocol selection is needed
- .agent/docs/required-paths.yaml only when updating `.agent` structure or check expectation vocabulary
- relevant protocol(s) for touched surface

## protocol_triggers
- completion-summary protocol

## output_shape
follow-up delta, checks, output sink state

## out_of_scope
- replacing required PR follow-up comment sink with PR body-only edits
- breaking manual-paste-unit expectations for Completion Summary
