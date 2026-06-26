# existing_pr_update prompt router

## purpose
Follow-up work for an already-open PR based on reviewed diff and residual gaps.

## trigger_condition
Worktype is `existing_pr_update`, including:
- 既存PRへ追加修正
- PR差分レビュー後 follow-up 作成
- merge前後の残ズレ修正
- PR #xxx 差分に対する追加commit/追加prompt作成

## required_reads
- PR diff or patch
- changed files
- previous review findings or explicit user findings
- .agent/tasks/todo.md
- docs/system-roadmap.yaml
- target implementation files
- .agent/protocols/completion-summary.md
- foundation SSOT read gate judgment (when applicable):
  1. docs/framework-core.yaml
  2. docs/design/runtime-orchestration-ssot.yaml
  3. docs/design/pipeline-continuity-ssot.yaml

## optional_reads
- .agent/docs/ssot-map.yaml when touched surface exists and relevant SSOT/protocol selection is needed
- .agent/docs/required-paths.yaml only when updating `.agent` structure or check expectation vocabulary
- relevant protocol(s) for touched surface

## protocol_triggers
- completion-summary protocol


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

## PR / Bundle residue handling
- PR merge unit is completion Bundle; merge前の残課題が同一Bundle内の未達なら、existing PR update として同一PRへ追加修正する。
- follow-up PR へ逃がして partial merge することは禁止。carry-over is not an approval substitute for unresolved work inside the same Bundle.
- merge後の残ズレ修正と、merge前の同一Bundle blocking residue を混同しない。
- checkpoint clear is not main merge approval; Bundle途中状態の監査clear後は、同一PR内で次checkpointへ進めるだけであり、main merge readiness は completion Bundle completion で判定する。

## output_shape
follow-up delta, foundation_ssot_read_judgment, checks, output sink state

## foundation_ssot_read_judgment
- framework_core_read: yes/no/not_required
- runtime_orchestration_read: yes/no/not_required
- pipeline_continuity_read: yes/no/not_required
- reason_if_not_required:
- target_ssot_read_after_foundation:

## out_of_scope
- replacing required PR follow-up comment sink with PR body-only edits
- breaking manual-paste-unit expectations for Completion Summary
