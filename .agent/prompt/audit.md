# audit prompt router

## purpose
PR/diff semantic audit and merge-readiness judgment with implementation-meaning alignment.
Approve判断は「完全実装済みか」ではなく、PR scope の実装意味と SSOT状態分類（implemented/partial/remaining gaps）が整合しているかで行う。

## trigger_condition
Worktype is `audit`, including any of:
- PR監査
- 差分監査
- merge可否判断
- roadmap/todo/repo実態の整合確認
- Summary の真偽確認
- 「差分見て」「リポジトリ進捗見て」「マージしていいか」系依頼

## required_reads
- PR diff or patch
- changed file list
- .agent/tasks/todo.md
- docs/system-roadmap.yaml
- roadmap target milestone/unlocks and related implementation_registry entry
- diff-target implementation files
- .agent/protocols/audit.md
- foundation SSOT read gate judgment (when applicable):
  1. docs/framework-core.yaml
  2. docs/design/runtime-orchestration-ssot.yaml
  3. docs/design/pipeline-continuity-ssot.yaml

## optional_reads
- README / docs/articles / design SSOT when diff claim validation requires public/docs context
- .agent/docs/ssot-map.yaml when PR diff touches runtime/db/frontend/governance surfaces and SSOT selection is needed
- .agent/docs/required-paths.yaml only when auditing `.agent` structure, required paths, required content terms, or check-structure expectations

## protocol_triggers
- always: .agent/protocols/audit.md
- conditional: .agent/protocols/todo-carry-over.md when TODO/Roadmap Finalization Judgment updates, closes, reclassifies, or carries over canonical TODO/roadmap state
- conditional: policy/scenario/runtime protocols only when touched

## completion_judgment_axis
- audit の implemented / partial / carry-over / Request Changes の詳細判定は .agent/protocols/audit.md の approve_judgment_axis に従う。
- prompt 側では completion 判定本文を重複定義しない。
- `implemented` 判定可否、partial Approve 条件、implemented 未達時の TODO細分化 / carry-over 必須条件は protocol 側を正本とする。

## output_shape
- Diff reviewed: yes/no
- Changed files
- Todo checked: yes/no
- Roadmap checked: yes/no
- Implementation registry checked: yes/no
- Repo implementation checked: yes/no
- problem
- purpose
- improvement_policy
- reference_materials
- target_files
- target_functions
- todo
- remaining_todo
- Semantic findings
- Required follow-up
- Merge judgment
- todo_granularity_judgment
- foundation_ssot_read_judgment

## out_of_scope
- full-bundle reading by default
- treating `.agent/docs` as always-read bundle
- replacing semantic audit with structure check
- summary-only judgment
- metadata-only (PR metadata/mergeability only) judgment

## todo_granularity_judgment
- roadmap entry（`docs/system-roadmap.yaml`）
- target `completion_condition` / `known_gap_ref`
- carry-over が implementation atom ではなく completion bundle 単位か
- TODO追加 / follow-up prompt / no TODO の判断
- Issue closed 状態を implemented 根拠にしていないこと

## foundation_ssot_read_judgment
- framework_core_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
- reason_if_not_required:
- target_ssot_read_after_foundation:
