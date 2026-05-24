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
- foundation SSOT read gate judgment (when applicable):
  1. docs/framework-core.yaml
  2. docs/design/runtime-orchestration-ssot.yaml
  3. docs/design/pipeline-continuity-ssot.yaml

## optional_reads
- .agent/docs/ssot-map.yaml when touched surface exists and relevant SSOT/protocol selection is needed
- .agent/docs/required-paths.yaml only when updating `.agent` structure or check expectation vocabulary
- relevant protocol(s) for touched surface

## protocol_triggers
- completion-summary protocol

## output_shape
follow-up delta, foundation_ssot_read_judgment, checks, output sink state

## foundation_ssot_read_judgment
- framework_core_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
- reason_if_not_required:
- target_ssot_read_after_foundation:

## out_of_scope
- replacing required PR follow-up comment sink with PR body-only edits
- breaking manual-paste-unit expectations for Completion Summary
