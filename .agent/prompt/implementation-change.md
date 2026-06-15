# implementation_change prompt router

## purpose
Implementation change under existing SSOT.

## trigger_condition
Worktype is `implementation_change`.

## required_reads
- .agent/docs/ssot-map.yaml
- mapped SSOT for touched runtime surface selected through `.agent/docs/ssot-map.yaml`
- .agent/protocols/implementation-change.md
- foundation SSOT read gate judgment (when applicable):
  1. docs/framework-core.yaml
  2. docs/design/runtime-orchestration-ssot.yaml
  3. docs/design/pipeline-continuity-ssot.yaml

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

Reusable substrate must be preferred before adding one-off implementation. If existing substrate is insufficient, add a reusable abstraction suitable for future bundles rather than a narrow one-off implementation, unless an explicit SSOT exception exists. This applies to frontend API wrappers, action handlers, dispatch payload mappers, projection builders, form/table renderers, repository methods, audit writers, validation flows, status transition flows, and provider compatibility checkers.

Use `docs/design/runtime-orchestration-ssot.yaml` boundaries before coding:

- hardcode allowed: runtime port, runtime handler, runtime skeleton, scheduler / dispatcher skeleton, endpoint shape, and abstract function shape when explicitly registered in SSOT.
- seed/data-defined required: UI schema, form/table projection, action buttons, action wiring, dispatch payload mapping, admin surface registration, entity operation binding, projection constructor mapping, function parameters, and runtime mapping where SSOT treats mapping as data-defined.

Forbidden for implementation agents:

- Do not add UI that can be expressed by existing seed/entity/projection/action substrate as a dedicated route or island.
- Do not add an action that can use the existing dispatch -> entity -> runtime circuit as a dedicated frontend API wrapper.
- Do not add one-off helpers for behavior covered by existing repository / audit / validation / status transition patterns.
- Do not create implementation-first shape and then add SSOT text to ratify it.
- Do not confuse hardcode-allowed runtime ports with UI/action/mapping surfaces that must be seed/data-defined.
- Do not proliferate narrow dedicated helpers that future bundles cannot reuse.

## output_shape
scope, implementation delta, protocol decisions, foundation_ssot_read_judgment, todo_granularity_judgment, check results
legacy_minimum_shape: scope, implementation delta, protocol decisions, todo_granularity_judgment, check results

## foundation_ssot_read_judgment
- framework_core_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
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
