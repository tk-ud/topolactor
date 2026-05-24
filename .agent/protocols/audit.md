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
