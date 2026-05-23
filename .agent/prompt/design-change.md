# design_change prompt router

## purpose
SSOT/docs/external contract change.

## trigger_condition
Worktype is `design_change`.

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
changed contracts, impact map, required check scope

## out_of_scope
- implementation-only edits without design delta
- treating `.agent/docs` as always-read bundle
