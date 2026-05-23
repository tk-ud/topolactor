# audit prompt router

## purpose
PR/diff semantic audit.

## trigger_condition
Worktype is `audit`.

## required_reads
- docs/governance/agent-governance-routing-ssot.yaml
- docs/governance/agent-governance-routing-ssot.md
- .agent/protocols/audit.md

## optional_reads
- .agent/docs/ssot-map.yaml when PR diff touches runtime/db/frontend/governance surfaces and SSOT selection is needed
- .agent/docs/required-paths.yaml only when auditing `.agent` structure, required paths, required content terms, or check-structure expectations
- touched-surface SSOT docs only

## protocol_triggers
- always: .agent/protocols/audit.md
- conditional: policy/scenario/runtime protocols only when touched

## output_shape
problem, purpose, improvement_policy, reference_materials, target_files, target_functions, todo, remaining_todo

## out_of_scope
- full-bundle reading by default
- treating `.agent/docs` as always-read bundle
- replacing semantic audit with structure check
