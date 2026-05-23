# implementation_change prompt router

## purpose
Implementation change under existing SSOT.

## trigger_condition
Worktype is `implementation_change`.

## required_reads
- .agent/docs/ssot-map.yaml
- mapped SSOT for touched runtime surface selected through `.agent/docs/ssot-map.yaml`
- .agent/protocols/implementation-change.md

## optional_reads
- .agent/docs/required-paths.yaml only when touching `.agent/tests/check-structure.sh`, required paths, required terms, or check-structure expectation vocabulary
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
- treating `.agent/docs` as full-read bundle
