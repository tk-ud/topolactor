# TODO Carry-over Protocol (Agenda: todo-carry-over)

## Workflow Guard

- Treat this protocol as post-JUDGMENT carry-over classification.
- Do not use TODO maintenance to bypass completion-governance gates.

## Trigger condition

Read this protocol only when classifying remaining work for `.agent/tasks/todo.md` or roadmap carry-over.

## foundation_ssot_read_gate
When TODO/roadmap classification touches projection/runtime lanes/API/SSE/event-log/DB-driven UI topology or implemented/partial `completion_condition` judgment, read foundation SSOT in this order before target-specific artifacts:

1. `docs/framework-core.yaml`
2. `docs/design/runtime-orchestration-ssot.yaml`
3. `docs/design/pipeline-continuity-ssot.yaml`
4. target SSOT / DB / implementation surfaces
5. `docs/design/db-schema.yaml` -- additionally mandatory whenever the classified work touches DB / manifest / seed SQL / UI topology / package / layout / design / wiring / tensor persistence or translator adoption targets; table authority/role/`manifest_reference` must be cross-checked against this SSOT, not assumed from a refs-only `manifest.topology` shape alone.

Skip is allowed only for typo/format-only/unrelated text cleanup with explicit `not_required` reason.

## TODO surface responsibility

- `.agent/tasks/todo.md` is for unresolved implementation, design, SSOT, or test-authoring work that must survive beyond the current PR/conversation.
- Do not store CI waiting, remote CI pass confirmation, local tool absence, or verification-only bookkeeping in `.agent/tasks/todo.md`.
- If verification results expose concrete follow-up implementation/design/SSOT/test-authoring work, record only that concrete work.

## Carry-over classification

### TODO granularity invariant

- canonical TODO carry-over は roadmap completion bundle 単位で作成する。
- implementation atom の羅列（alias追加/adapter接続/emit追加等）を canonical TODO として列挙することは禁止。
- TODO には対象 roadmap entry と `completion_condition` / `known_gap_ref` を紐づける。
- non-blocker carry-over は分類タグ付きで許可するが、SSOT completion_condition 未達を non-blocker 扱いしてはならない。
- 既存抽象・既存基板・既存回路で表現できるものを、小粒専用実装（route / UI / helper / API wrapper 等）としてTODO化してはならない。
- backend / UI / route / helper の implementation atom 分割で completion bundle を偽装してはならない。
- reusable abstraction / architecture substrate restoration が必要な場合は、roadmap completion bundle 単位のTODOとして扱う。
- 基板逸脱を `UI修正` / `API修正` / `helper修正` のような atom TODO に分割してはならない。
- 既存基板へ戻す必要がある場合は `substrate restoration bundle` として、対象Bundleの completion condition / known_gap_ref に紐づけて扱う。
- dedicated route / island / frontend API / one-off helper を後で消す作業も単独atomではなく、該当Bundleの基板復帰作業として扱う。

### 残すべき TODO 候補

- unresolved implementation residue
- unresolved design decision
- unresolved SSOT alignment work
- unresolved test-authoring work

### 削除 / close 候補

- item semantics already satisfied with required evidence
- item rewritten into smaller roadmap completion bundles (not implementation-atom TODO split)
- duplicate/outdated carry-over entries after reclassification

### roadmap 更新候補

- update `docs/system-roadmap.yaml` when completion status meaningfully changes
- reference `implementation_registry` or `known_gap_ref` for traceability
- avoid copying full roadmap matrix into TODO

## Auditor finalizer boundary

- Implementation agent normally proposes TODO/roadmap actions as Auditor TODO input.
- Auditor is the default finalizer for canonical TODO/roadmap closure.
- Completion summary `### 残タスク引き継ぎ指示` is Auditor TODO input, not canonical TODO closure.

## Implementation-agent direct close conditions

Implementation agent may directly close/update canonical TODO/roadmap only when all conditions hold:

1. task scope explicitly includes canonical TODO/roadmap maintenance,
2. required checks or remote-CI equivalent required success are confirmed,
3. target completion condition is semantically satisfied,
4. no concrete implementation/design/SSOT/test-authoring residue remains,
5. update is consistent with completion-governance protocol.

If any condition is unknown or unmet, defer closure and keep Auditor TODO input.


## Roadmap update judgment gate

When `docs/system-roadmap.yaml` is updated as part of TODO/roadmap maintenance, apply the following required judgments before completion:

- Run `bash .agent/tests/check-system-roadmap.sh` as a required check for roadmap-changing work.
- For status `partial` or `skeleton`, each changed entry must keep at least one explicit unresolved-condition anchor: `completion_condition` or `known_gap_ref`.
- On status transitions (`planned`/`skeleton`/`partial`/`implemented`), verify `public_summary`, `known_gap_ref`, and unresolved scope are semantically consistent.
- If implementation-complete phrasing is added to `public_summary`, keep only actually remaining unresolved scope in `known_gap_ref` with explicit boundaries.
- Remove stale/obsolete gap statements; do not leave historical unresolved wording after the gap is resolved or reclassified.

Failure to satisfy these checks is blocking for TODO/roadmap closure judgment.
