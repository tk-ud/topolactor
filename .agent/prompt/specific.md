# specific prompt router

## purpose
Single-file/function/design-point local inspection or fix.

## trigger_condition
Worktype is `specific` and scope is strictly local:
- 単一ファイルの局所修正
- 単一関数のバグ修正
- 監査済みで修正箇所が明確な1点変更

## tool_first_entry
When `.agent/tools/agent-ui-initial-contract` is usable, follow `worktypes` → `start` → `resolve-ssot` → `sections` → `end` per [`docs/governance/agent-ui-protocol-ssot.yaml`](../../docs/governance/agent-ui-protocol-ssot.yaml). `start` inlines the routed prompt as `prompt_content` and the routed required/triggered protocol full text as `protocol_trigger_hints[].content`; SSOT sections are read in `resolve-ssot` / `sections`, not in `start` alone. Protocol file paths listed below are fallback-route/manual verification references only when the tool is unusable, or when checking tool-output absence/routing inconsistency; under tool-first they are already represented by `protocol_trigger_hints[].content` and are not an extra mandatory manual read. After implementation or audit work, close with the full `agent-ui-local-test` chain through `summary`. This file remains the fallback router when the tool is not usable.
## required_reads
- target files only
- .agent/protocols/specific.md

## optional_reads
- .agent/tasks/todo.md and docs/system-roadmap.yaml only when target touches roadmap/todo/status judgment
- .agent/docs/ssot-map.yaml when target file/function maps to SSOT-mapped surfaces
- .agent/docs/required-paths.yaml only when touching `.agent` structure, required paths, or structure-check expectation vocabulary

## protocol_triggers
- .agent/protocols/specific.md
- conditional: .agent/protocols/todo-carry-over.md when the local target touches TODO/roadmap/status judgment or changes canonical progress state
- runtime/policy/scenario protocols only if trigger applies


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

## PR / Bundle boundary
- `specific` は局所修正 / hotfix / PR内checkpoint の作業には使える。
- specific route must not carve unresolved scope out of an active completion Bundle to justify merging a Bundle未達PR into `main`.
- checkpoint clear is not main merge approval; local checkpoint progress remains inside the active PR/Bundle until completion.
- 複数surfaceの意味整合やBundle completion判断が必要な場合は `specific` ではなく適切な worktype（audit / implementation_change / design_change / existing_pr_update）へ戻す。

## output_shape
local scope statement, touched targets, decisions, foundation_ssot_read_judgment, checks

## foundation_ssot_read_judgment
- framework_core_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
- reason_if_not_required:
- target_ssot_read_after_foundation:

## out_of_scope
- PR差分監査
- merge判断
- roadmap/todo/repo整合確認
- 複数surfaceの意味整合確認
- Summary の検証
- 「差分見ろ」「進捗見て」「マージしていいか」系
- reading all docs/protocols by default
- scope expansion without explicit reason
