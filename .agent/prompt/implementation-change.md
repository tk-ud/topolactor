# implementation_change prompt router

## purpose
Implementation change under existing SSOT.

## trigger_condition
Worktype is `implementation_change`.

## tool_first_entry
When `.agent/tools/agent-ui-initial-contract` is usable, follow `worktypes` → `start` → `resolve-ssot` → `sections` → `end` per [`docs/governance/agent-ui-protocol-ssot.yaml`](../../docs/governance/agent-ui-protocol-ssot.yaml). `start` inlines the routed prompt as `prompt_content` (full text) and the routed required/triggered protocol as `protocol_obligations[]` (normalized structured fields extracted from each protocol file's own headings -- trigger_condition/judgment_scope/foundation_ssot_read_gate/blocking_conditions/pass_conditions/required_fields/classification_vocab/output_boundary -- not that file's full text); SSOT sections are read in `resolve-ssot` / `sections`, not in `start` alone. Protocol file paths listed below are fallback-route/manual verification references only when the tool is unusable, when checking tool-output absence/routing inconsistency, or when reading beyond `protocol_obligations[]`'s 8 canonical fields is needed (each entry's `fallback_protocol_ref` points back to the same path); under tool-first they are already represented by `protocol_obligations[]` and are not an extra mandatory manual read. After implementation or audit work, close with the full `agent-ui-local-test` chain through `summary`. This file remains the fallback router when the tool is not usable.
## required_reads
- .agent/docs/ssot-map.yaml
- mapped SSOT for touched runtime surface selected through `.agent/docs/ssot-map.yaml`
- .agent/protocols/implementation-change.md
- foundation SSOT read gate judgment (when applicable):
  1. docs/framework-core.yaml
  2. docs/framework-policy.yaml
  3. docs/design/runtime-orchestration-ssot.yaml
  4. docs/design/pipeline-continuity-ssot.yaml
  5. docs/design/db-schema.yaml -- additionally mandatory when the change touches DB / manifest / seed SQL / UI topology / package / layout / design / wiring / tensor persistence or translator adoption targets (see `.agent/protocols/implementation-change.md` `foundation_ssot_read_gate`)

## optional_reads
- .agent/docs/required-paths.yaml only when touching `.agent/tests/check-structure.sh`, required paths, required terms, or check-structure expectation vocabulary
- .agent/protocols/scenario-contract.md
- .agent/protocols/policy-judgment.md

## protocol_triggers
- always: implementation-change protocol
- runtime/persistence/projection change -> scenario-contract
- policy/scoring/threshold change -> policy-judgment


## Substrate Plan (required before implementation when substrate surfaces change)

When adding or changing any route / island / frontend API / action handler / helper / repository method / audit writer / validation flow / status transition flow, create this plan before implementation:

```markdown
## Substrate Plan

- hardcoded runtime substrate:
- seed-defined entity/projection/action/UI surface:
- data-defined mapping:
- reusable abstraction:
- runtime/admin data:
- external authority boundary:
- existing substrate reused:
- new substrate required:
- explicit SSOT exception, if any:
- ng_axis_gate_triggers (list triggered axes per gate, or "none"):
  - data_driven_projection_completion_gate: (ui_db_projection / dispatch_resolution / response_or_sse_queue_projection / abstract_function_or_db_driven_operation_boundary / seed_or_data_defined_surface — list which apply)
  - admin_authoring_completion_gate: (all_dispatch_kinds_configurable / contents_dispatch_ui_wiring_configurable / ui_events_trigger_and_target_configurable / external_integration_uses_credential_substrate / external_integration_trigger_and_target_wiring_configurable — list which apply)
  - external_integration_completion_gate: (credential_resolution_base — triggered or not)
- exception_declaration (if any gate axis exception applies — exception_name / reason / ssot_basis / why_abstract_function_impossible):
```

Before implementation, check whether the requested behavior can be expressed by existing substrate:

- existing seed/entity/projection/action surfaces
- existing dispatch -> entity -> runtime circuit
- existing frontend command lane
- existing admin_runtime action pattern
- existing repository pattern
- existing audit append pattern
- existing validation result persistence pattern
- existing status transition helper or lifecycle pattern
- existing enum/dictionary/select projection pattern

For any change touching runtime / projection / admin / external integration surfaces, additionally verify completion gate axes before coding:

- Identify which gate axes are triggered: `data_driven_projection_completion_gate`, `admin_authoring_completion_gate`, `external_integration_completion_gate`
- For each triggered axis, confirm the pass condition is achievable with the planned approach before writing code
- `ui_db_projection`: UI must be DB/seed/topology-manifest/projection-repository driven — test-local handwritten emission is not pass
- `dispatch_resolution`: full path client_trigger → frontend_scheduler → api_client → backend_endpoint → manifest_dispatcher → dispatchable_runtime must be present — direct backend handler / dedicated frontend wrapper is not pass
- `response_or_sse_queue_projection`: response must travel through existing queue/SSE/projection-response lane — returning EndpointResponseDto inline is not pass
- `abstract_function_or_db_driven_operation_boundary`: backend mutation must use topology_function_binder / execute_db_function / abstract function pattern — concrete dedicated mutation is not pass
- `seed_or_data_defined_surface`: surfaces expressible as seed/DB-defined must be seed/DB-defined — hardcoded frontend list / inline backend mapping is not pass
- `ui_events_trigger_and_target_configurable`: trigger UI and targetNodeId are independent concepts — satisfying one does not satisfy the other
- If bundle boundary is unclear: emit follow-up prompt or investigation item rather than adding a small TODO

Reusable substrate must be preferred before adding one-off implementation. If existing substrate is insufficient, add a reusable abstraction suitable for future bundles rather than a narrow one-off implementation, unless an explicit SSOT exception exists. This applies to frontend API wrappers, action handlers, dispatch payload mappers, projection builders, form/table renderers, repository methods, audit writers, validation flows, status transition flows, and provider compatibility checkers.

Use `docs/design/runtime-orchestration-ssot.yaml` boundaries before coding:

- hardcode allowed: runtime port, runtime handler, runtime skeleton, scheduler / dispatcher skeleton, endpoint shape, and abstract function shape when explicitly registered in SSOT.
- seed/data-defined required: UI schema, form/table projection, action buttons, action wiring, dispatch payload mapping, admin surface registration, entity operation binding, projection constructor mapping, function parameters, and runtime mapping where SSOT treats mapping as data-defined.

Forbidden for implementation agents:

- Do not add UI that can be expressed by existing seed/entity/projection/action substrate as a dedicated route or island.
- Do not add an action that can use the existing dispatch -> entity -> runtime circuit as a dedicated frontend API wrapper.
- Do not add one-off helpers for behavior covered by existing repository / audit / validation / status transition patterns.
- Do not create implementation-first shape and then add SSOT text to ratify it (deviation-ratification is not pass).
- Do not confuse hardcode-allowed runtime ports with UI/action/mapping surfaces that must be seed/data-defined.
- Do not proliferate narrow dedicated helpers that future bundles cannot reuse.
- Do not hardcode in frontend or backend any surface that can be expressed as seed/DB-defined projection.
- Do not wire UI Events trigger without independent targetNodeId wiring; do not wire targetNodeId without independent trigger wiring — they are separate concepts.
- Do not add a small TODO when bundle boundary is unclear — use follow-up prompt or investigation item instead.

## PR / Bundle / checkpoint boundary
- PR merge unit is completion Bundle; implementation scope definition must distinguish PR-internal checkpoints from main merge readiness.
- 小粒な実装進行や途中checkpointは、同一PR内でBundle completionへ進むための作業単位であり、Bundle未達PRを merge する根拠ではない。
- checkpoint clear is not main merge approval; audit clear during a partial Bundle state only permits the next checkpoint inside the same PR.
- implementation atom を別PR化して、同一Bundle未達を main へ分割投入してはならない。
- commit granularity is not a governance requirement; commit粒度を governance 主語にしない。

## output_shape
scope, implementation delta, protocol decisions, foundation_ssot_read_judgment, todo_granularity_judgment, check results
legacy_minimum_shape: scope, implementation delta, protocol decisions, todo_granularity_judgment, check results

## foundation_ssot_read_judgment
- framework_core_read: yes/no/not_required
- framework_policy_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
- db_schema_read: yes/no/not_required
- reason_if_not_required:
- target_ssot_read_after_foundation:

## out_of_scope
- SSOT design rewrite without design_change route
- treating `.agent/docs` as full-read bundle

## todo_granularity_judgment
- roadmap entry（`docs/system-roadmap.yaml`）
- target `completion_condition` / `known_gap_ref`
- carry-over が implementation atom ではなく completion bundle 単位か
- TODO追加 / follow-up prompt / no TODO の判断
- Issue closed 状態を implemented 根拠にしていないこと
