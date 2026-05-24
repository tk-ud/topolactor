# implementation_change prompt router

## purpose
Implementation change under existing SSOT.

## trigger_condition
Worktype is `implementation_change`.

## required_reads
- .agent/docs/ssot-map.yaml
- mapped SSOT for touched runtime surface selected through `.agent/docs/ssot-map.yaml`
- .agent/protocols/implementation-change.md
- foundation SSOT read gate judgment (when applicable):
  1. docs/framework-core.yaml
  2. docs/design/runtime-orchestration-ssot.yaml
  3. docs/design/pipeline-continuity-ssot.yaml

## optional_reads
- .agent/docs/required-paths.yaml only when touching `.agent/tests/check-structure.sh`, required paths, required terms, or check-structure expectation vocabulary
- .agent/protocols/scenario-contract.md
- .agent/protocols/policy-judgment.md

## protocol_triggers
- always: implementation-change protocol
- runtime/persistence/projection change -> scenario-contract
- policy/scoring/threshold change -> policy-judgment

## output_shape
scope, implementation delta, protocol decisions, foundation_ssot_read_judgment, todo_granularity_judgment, check results
legacy_minimum_shape: scope, implementation delta, protocol decisions, todo_granularity_judgment, check results

## foundation_ssot_read_judgment
- framework_core_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
- reason_if_not_required:
- target_ssot_read_after_foundation:

## out_of_scope
- SSOT design rewrite without design_change route
- treating `.agent/docs` as full-read bundle

## todo_granularity_judgment
- roadmap entry（`docs/system-roadmap.yaml`）
- target `completion_condition` / `known_gap_ref`
- carry-over が implementation atom ではなく completion bundle 単位か
- TODO追加 / follow-up prompt / no TODO の判断
- Issue closed 状態を implemented 根拠にしていないこと
