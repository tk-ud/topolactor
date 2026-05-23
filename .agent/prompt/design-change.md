# design_change prompt router

## purpose
SSOT/docs/external contract change.

## trigger_condition
Worktype is `design_change`.

## required_reads
- docs/governance/agent-governance-routing-ssot.yaml
- docs/governance/agent-governance-routing-ssot.md
- .agent/protocols/design-change.md

## optional_reads
- .agent/protocols/ssot-change-impact.md

## protocol_triggers
- always: design-change protocol
- ssot change: ssot-change-impact protocol

## output_shape
changed contracts, impact map, required check scope

## out_of_scope
- implementation-only edits without design delta
