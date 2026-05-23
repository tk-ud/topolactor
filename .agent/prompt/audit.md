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
- .agent/docs/ssot-map.yaml
- touched-surface SSOT docs only

## protocol_triggers
- always: .agent/protocols/audit.md
- conditional: policy/scenario/runtime protocols only when touched

## output_shape
problem, purpose, improvement_policy, reference_materials, target_files, target_functions, todo, remaining_todo

## out_of_scope
- full-bundle reading by default
- replacing semantic audit with structure check
