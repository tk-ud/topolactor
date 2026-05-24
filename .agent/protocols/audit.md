# audit protocol

## workflow_guard
Use in JUDGMENT for worktype `audit`.

## trigger_condition
Semantic PR/diff audit, merge judgment, or summary-truth verification requested.

## required_alignment_surfaces
- PR diff or patch
- changed file list
- .agent/tasks/todo.md
- docs/system-roadmap.yaml
- roadmap target milestone/unlocks and implementation_registry entries
- diff-target implementation files
- main-vs-target diff reality (or target PR head state)
- README/public docs only when needed for externally claimed behavior verification

## judgment_scope
Implementation meaning consistency against stated intent and roadmap/todo status.

## approve_judgment_axis
- Approve requires semantic consistency between PR diff, TODO, roadmap, and relevant SSOT completion_condition classification.
- PR scope が partial 実装であっても、未達SSOT条件が TODO / roadmap / `known_gap_ref` / `remaining_todo` に明示維持されていれば Approve 可能。
- relevant SSOT completion_condition が未達のまま implemented / complete / closed を示す、または示唆する PR は Approve 禁止。
- representative route、ACK-only intake、skeleton wiring、partial wiring は、SSOT completion_condition が許容しない限り implemented 根拠にしない。
- Remote CI / tests passing は証拠の一部であり、単体では semantic completion 根拠にしない。

## required_output_contract
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

## forbidden_shortcuts
- Summaryだけで判断しない
- PR metadata / mergeability だけで判断しない
- ファイル存在だけで partial / implemented 判定しない
- todo未実装scopeを見ずに roadmap status を判断しない
- implementation_registry key 名だけで実装意味を判断しない
- completion_condition 未達のまま implemented 判定しない
- representative route / skeleton / ACK-only / partial wiring を implemented 根拠にしない


## todo_roadmap_finalization_gate
- PR Approve requires TODO/Roadmap Finalization Judgment.
- If implementation meaning satisfies or changes any TODO / roadmap `implementation_registry` entry, auditor must either:
  1. update canonical TODO/roadmap in the same audit/follow-up maintenance task, or
  2. if canonical TODO/roadmap cannot be updated in the same task, emit a single explicit follow-up prompt for `todo_maintenance` as a blocked-state output obligation (not as an approval-unblock condition).
- Approve is blocked when roadmap/TODO status remains materially stale.
- When same-task canonical update is not possible, auditor must hold approval until stale status is resolved or explicitly reclassified as out-of-scope, after emitting the required follow-up prompt.
- Remote CI unavailable to implementation agent is not a TODO item; it is Auditor evidence input for final closure.

## blocking_conditions
- Missing required audit output fields.
- Replacing semantic audit with structure-only result.
- Required alignment surfaces not checked.

## pass_conditions
- Required output contract produced.
- Required output contract includes semantic audit fields:
  - problem
  - purpose
  - improvement_policy
  - reference_materials
  - target_files
  - target_functions
  - todo
  - remaining_todo
- Required alignment surfaces explicitly cross-checked.
- Semantic findings grounded in diff + implementation reality.
- `implemented` 判定時、roadmap/TODO/SSOT completion_condition 充足を明示できる。
- SSOT未達が残る場合、`known_gap_ref` と `remaining_todo` に未達条件が明示される。
