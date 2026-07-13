# design_change prompt router

## purpose
SSOT/docs/external contract change.

## trigger_condition
Worktype is `design_change`.

## tool_first_entry
When `.agent/tools/agent-ui-initial-contract` is usable, follow `worktypes` → `start` → `resolve-ssot` → `sections` → `end` per [`docs/governance/agent-ui-protocol-ssot.yaml`](../../docs/governance/agent-ui-protocol-ssot.yaml). `start` inlines the routed prompt as `prompt_content` (full text) and the routed required/triggered protocol as `protocol_obligations[]` (normalized structured fields extracted from each protocol file's own headings -- trigger_condition/judgment_scope/foundation_ssot_read_gate/blocking_conditions/pass_conditions/required_fields/classification_vocab/output_boundary -- not that file's full text); SSOT sections are read in `resolve-ssot` / `sections`, not in `start` alone. Protocol file paths listed below are fallback-route/manual verification references only when the tool is unusable, when checking tool-output absence/routing inconsistency, or when reading beyond `protocol_obligations[]`'s 8 canonical fields is needed (each entry's `fallback_protocol_ref` points back to the same path); under tool-first they are already represented by `protocol_obligations[]` and are not an extra mandatory manual read. After implementation or audit work, close with the full `agent-ui-local-test` chain through `summary`. This file remains the fallback router when the tool is not usable.
## required_reads
- docs/governance/agent-governance-routing-ssot.yaml
- docs/governance/agent-governance-routing-ssot.md
- .agent/docs/ssot-map.yaml for SSOT impact-surface selection
- .agent/protocols/design-change.md
- foundation SSOT read gate judgment (when applicable):
  1. docs/framework-core.yaml
  2. docs/framework-policy.yaml
  3. docs/design/runtime-orchestration-ssot.yaml
  4. docs/design/pipeline-continuity-ssot.yaml
  5. docs/design/db-schema.yaml -- additionally mandatory when the SSOT change touches DB / manifest / seed SQL / UI topology / package / layout / design / wiring / tensor persistence or translator adoption targets (see `.agent/protocols/design-change.md` `foundation_ssot_read_gate`)

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
- framework_policy_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
- db_schema_read: yes/no/not_required
- reason_if_not_required:
- target_ssot_read_after_foundation:

## out_of_scope
- implementation-only edits without design delta
- treating `.agent/docs` as always-read bundle
