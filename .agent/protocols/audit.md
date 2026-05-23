# audit protocol

## workflow_guard
Use in JUDGMENT for worktype `audit`.

## trigger_condition
Semantic PR/diff audit requested.

## judgment_scope
Implementation meaning consistency against stated intent.

## blocking_conditions
- Missing required audit output fields.
- Replacing semantic audit with structure-only result.

## pass_conditions
- Required fields produced:
  - problem
  - purpose
  - improvement_policy
  - reference_materials
  - target_files
  - target_functions
  - todo
  - remaining_todo
