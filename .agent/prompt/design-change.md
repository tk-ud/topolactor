# design_change prompt router

## purpose
SSOT/docs/external contract change.

## trigger_condition
Worktype is `design_change`.

## tool_first_entry
When `.agent/tools/agent-ui-initial-contract` is usable, run `start --task-name <name> --worktype design_change` first to get the routed prompt excerpt and protocol trigger hints; this file remains the fallback router when the tool is not usable.

## required_reads
- docs/governance/agent-governance-routing-ssot.yaml
- docs/governance/agent-governance-routing-ssot.md
- .agent/docs/ssot-map.yaml for SSOT impact-surface selection
- .agent/protocols/design-change.md

## optional_reads
- .agent/docs/required-paths.yaml when changing `.agent` structure, required paths, or required content terms
- .agent/protocols/ssot-change-impact.md

## protocol_triggers
- always: design-change protocol
- ssot change: ssot-change-impact protocol (must stay consistent with ssot-change-impact requirements)

## output_shape
changed contracts, impact map, foundation_ssot_read_judgment, required check scope

## foundation_ssot_read_judgment
- framework_core_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
- reason_if_not_required:
- target_ssot_read_after_foundation:

## out_of_scope
- implementation-only edits without design delta
- treating `.agent/docs` as always-read bundle
