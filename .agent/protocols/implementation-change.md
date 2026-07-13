# implementation_change protocol

## workflow_guard
Use for worktype `implementation_change`.

## trigger_condition
Runtime/code changes under existing SSOT.

## judgment_scope
Implementation coherence and trigger-based protocol application.

## foundation_ssot_read_gate
Before judging or changing runtime/frontend/backend/db behavior, apply foundation SSOT read in this order when work touches projection, dispatch, runtime lanes, DB-driven UI, pipeline identity, or completion judgment:

1. `docs/framework-core.yaml`
2. `docs/framework-policy.yaml`
3. `docs/design/runtime-orchestration-ssot.yaml`
4. `docs/design/pipeline-continuity-ssot.yaml`
5. target-specific SSOT / DB / implementation files
6. `docs/design/db-schema.yaml` -- mandatory in addition to the above whenever work touches DB / manifest / seed SQL / UI topology / package / layout / design / wiring / tensor persistence or translator adoption targets. `db/*.sql` is the canonical DDL/seed surface, but table authority, table role, and `manifest_reference` meaning must be cross-checked against this SSOT; a refs-only `manifest.topology` shape alone is not proof that the refs point at the DB-design-authoritative table.

Do not treat this as always-read for unrelated typo/format-only edits. When skipped, record explicit `not_required` reason.

## blocking_conditions
- Missing scenario-contract when runtime/persistence/projection changed (route-aware: tool-first route -- required scenario-contract fields `target_file`/`senario_summary`/`ng_boundary` not passed to `agent-ui-initial-contract end`; fallback route -- missing manually-created `.agent/tmp/tmp.txt` scenario-contract only when the tool is unavailable).
- Missing policy-judgment when scoring/threshold/policy changed.
- DB/manifest/seed SQL/UI topology/package/layout/design/wiring/tensor persistence or translator adoption target work without cross-checking `docs/design/db-schema.yaml` table authority/role/`manifest_reference`.

## pass_conditions
- Required conditional protocols were applied when triggered.
- Required checks were executed or explicitly NOT_EXECUTED/REQUIRED_NOT_EXECUTED.

## todo_granularity_guard
- PR merge unit is completion Bundle: implementation agents may make small implementation progress, but main merge readiness is judged only after the active completion Bundle closes.
- small implementation progress may continue within the same PR after audit clear; Bundle途中状態で監査clearされた場合は、同一PR内で次checkpointへ進める。
- checkpoint clear is not main merge approval; PR-internal checkpoint clear must not be used to merge a Bundle未達PR into `main`.
- implementation atom を別PR化して、同一Bundle未達を main へ分割投入してはならない。
- commit granularity is not a governance requirement; commit粒度を Bundle completion / PR merge 可否の判定主語にしない。
- 実装差分の小粒進捗は PR summary の completed sub-scope に記載してよい。
- `.agent/tasks/todo.md` へ新規 TODO を追加する場合、roadmap completion bundle 単位で追加する。
- implementation atom（例: alias追加/adapter接続/emit追加/単体test1件）単位の canonical TODO 追加は禁止。
- TODO には、どの roadmap entry の `completion_condition` / `known_gap_ref` を閉じる bundle かを明記する。
- bundle 境界が不明な場合は TODO を追加せず、follow-up prompt または確認事項として出力する。
- Issue の closed / aggregated / not_planned 状態を implemented 根拠にしない。`docs/system-roadmap.yaml` の `completion_condition` / `known_gap_ref` を completion judgment の参照として使うが、ロードマップの status 記述のみを実装根拠にしない。実装状態の確認は実コードおよびテストから行う。
