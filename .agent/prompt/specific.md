# specific prompt router

## purpose
Single-file/function/design-point local inspection or fix.

## trigger_condition
Worktype is `specific`.

## required_reads
- target files only
- .agent/protocols/specific.md

## optional_reads
- .agent/docs/ssot-map.yaml when target maps to SSOT

## protocol_triggers
- .agent/protocols/specific.md
- runtime/policy/scenario protocols only if trigger applies

## output_shape
local scope statement, touched targets, decisions, checks

## out_of_scope
- reading all docs/protocols by default
- scope expansion without explicit reason
