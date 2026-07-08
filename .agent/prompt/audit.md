# audit prompt router

## purpose
PR/diff semantic audit and merge-readiness judgment with implementation-meaning alignment.
Approve判断は「完全実装済みか」だけではなく、PR scope の実装意味と SSOT状態分類（implemented/partial/remaining gaps）が整合しているかを必要条件として確認する。ただし、状態分類の正確性だけで同一Bundle未達PRの main merge を許可してはならない。main merge-readiness は completion Bundle として main に入れてよい整合境界を満たすことを要求する。Bundle途中状態の checkpoint clear is not main merge approval; checkpoint clear は同一PR内で次checkpointへ進む許可である。

## trigger_condition
Worktype is `audit`, including any of:
- PR監査
- 差分監査
- merge可否判断
- roadmap/todo/repo実態の整合確認
- Summary の真偽確認
- 「差分見て」「リポジトリ進捗見て」「マージしていいか」系依頼

## tool_first_entry
When `.agent/tools/agent-ui-initial-contract` is usable, follow `worktypes` → `start` → `resolve-ssot` → `sections` → `end` per [`docs/governance/agent-ui-protocol-ssot.yaml`](../../docs/governance/agent-ui-protocol-ssot.yaml). `start` inlines the routed prompt as `prompt_content` and the routed required/triggered protocol full text as `protocol_trigger_hints[].content`; SSOT sections are read in `resolve-ssot` / `sections`, not in `start` alone. Protocol file paths listed below are fallback-route/manual verification references only when the tool is unusable, or when checking tool-output absence/routing inconsistency; under tool-first they are already represented by `protocol_trigger_hints[].content` and are not an extra mandatory manual read. After implementation or audit work, close with the full `agent-ui-local-test` chain through `summary`. This file remains the fallback router when the tool is not usable.
## required_reads
- semantic audit top-level SSOT baseline（always for audit worktype）:
  1. docs/framework-core.yaml
  2. docs/framework-policy.yaml
  3. docs/design/runtime-orchestration-ssot.yaml
  4. docs/design/pipeline-continuity-ssot.yaml
- PR diff or patch
- changed file list
- PR上の記録 when the audit target is a PR:
  - PR本文 / scope claim
  - PR comments
  - PR reviews
  - review thread status
- tool証跡log when `.agent/tools` / Agent UI run evidence is claimed or required:
  - .agent/tools/logs/tool.log
  - referenced Agent UI uuid / datetime
  - senario-tmp.md or relevant tool summary when referenced
- .agent/tasks/todo.md
- docs/system-roadmap.yaml
- roadmap target milestone/unlocks and related implementation_registry entry
- diff-target implementation files
- .agent/protocols/audit.md
- .agent/protocols/audit/index.md
- Gate 0 architecture substrate / reusable abstraction conformance from `.agent/protocols/audit.md`
- target-specific SSOT discovery after top-level baseline:
  - .agent/docs/ssot-map.yaml (surface-specific discovery; do not replace top-level baseline reads)

## optional_reads
- README / docs/articles / design SSOT when diff claim validation requires public/docs context
- .agent/docs/ssot-map.yaml when PR diff touches runtime/db/frontend/governance surfaces and SSOT selection is needed
- .agent/docs/required-paths.yaml only when auditing `.agent` structure, required paths, required content terms, or check-structure expectations
- .agent/tools/README.md when tool log boundary or Agent UI log meaning must be verified

## protocol_triggers
- always: .agent/protocols/audit.md
- always: .agent/protocols/audit/index.md
- conditional: .agent/protocols/todo-carry-over.md when TODO/Roadmap Finalization Judgment updates, closes, reclassifies, or carries over canonical TODO/roadmap state
- conditional: policy/scenario/runtime protocols only when touched

## completion_judgment_axis
- audit の implemented / partial / carry-over / Request Changes の詳細判定は .agent/protocols/audit.md と .agent/protocols/audit/ section shards の approve_judgment_axis に従う。
- prompt 側では completion 判定本文を重複定義しない。
- `implemented` 判定可否、PR-internal checkpoint clear、main merge approval 条件、implemented 未達時の TODO細分化 / carry-over 必須条件は protocol 側を正本とする。
- PR merge unit is completion Bundle; Bundle途中状態の監査clearは同一PR内で次checkpointへ進む許可であり、main merge approval ではない。

## output_shape
- Diff reviewed: yes/no
- Changed files
- PR record checked: yes/no/not_applicable (yes の場合、PR本文/comments/reviews/thread status の確認範囲を列挙する; metadata-only の yes は無効)
- Tool log checked: yes/no/not_applicable (yes の場合、確認した tool.log / uuid / datetime / senario-tmp.md / tool summary surface を列挙する; tool log は観測記録であり SSOT/proof/completion 判定そのものではない)
- Todo checked: yes/no
- Roadmap checked: yes/no
- Implementation registry checked: yes/no
- Repo implementation checked: yes/no (yes は実際に読んだ実装ファイル・テストのリストを出力必須; リストなしの yes は無効)
- Architecture substrate judgment:
  - runtime port hardcode:
  - UI surface:
  - action wiring:
  - dispatch/entity circuit:
  - reusable abstraction usage:
  - new route/island/frontend API necessity:
  - SSOT update classification:
  - data_driven_projection_gate_judgment (when projection/dispatch/SSE/mutation surfaces touched):
  - admin_authoring_completion_gate_judgment (when admin/Contents/UIEvents/wiring surfaces touched):
  - external_integration_completion_gate_judgment (when external port bundle surfaces touched):
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
- top_level_ssot_checked

## out_of_scope
- full-bundle reading by default
- treating `.agent/docs` as always-read bundle
- replacing semantic audit with structure check
- summary-only judgment
- metadata-only (PR metadata/mergeability only) judgment
- treating PR record as a replacement for SSOT/code/test evidence
- treating tool log or tool output as SSOT authority, proof completion, completion judgment, or semantic audit judgment by itself

## todo_granularity_judgment
- roadmap entry（`docs/system-roadmap.yaml`）
- target `completion_condition` / `known_gap_ref`
- carry-over が implementation atom ではなく completion bundle 単位か
- TODO追加 / follow-up prompt / no TODO の判断
- Issue closed 状態を implemented 根拠にしていないこと

## foundation_ssot_read_judgment
- framework_core_read: yes/no
- framework_policy_read: yes/no
- runtime_orchestration_read: yes/no
- pipeline_continuity_read: yes/no
- target_ssot_read_after_foundation:

## top_level_ssot_checked
- docs/framework-core.yaml: yes/no
- docs/framework-policy.yaml: yes/no
- docs/design/runtime-orchestration-ssot.yaml: yes/no
- docs/design/pipeline-continuity-ssot.yaml: yes/no
