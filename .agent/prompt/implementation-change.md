# implementation_change prompt router

## purpose
Implementation change under existing SSOT.

## trigger_condition
Worktype is `implementation_change`.

## required_reads
- mapped SSOT for touched runtime surface
- .agent/protocols/implementation-change.md

## optional_reads
- .agent/protocols/scenario-contract.md
- .agent/protocols/policy-judgment.md

## protocol_triggers
- always: implementation-change protocol
- runtime/persistence/projection change -> scenario-contract
- policy/scoring/threshold change -> policy-judgment

## output_shape
scope, implementation delta, protocol decisions, check results

## out_of_scope
- SSOT design rewrite without design_change route
