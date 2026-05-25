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
2. `docs/design/runtime-orchestration-ssot.yaml`
3. `docs/design/pipeline-continuity-ssot.yaml`
4. target-specific SSOT / DB / implementation files

Do not treat this as always-read for unrelated typo/format-only edits. When skipped, record explicit `not_required` reason.

## blocking_conditions
- Missing scenario-contract when runtime/persistence/projection changed.
- Missing policy-judgment when scoring/threshold/policy changed.

## pass_conditions
- Required conditional protocols were applied when triggered.
- Required checks were executed or explicitly NOT_EXECUTED/REQUIRED_NOT_EXECUTED.

## todo_granularity_guard
- 実装差分の小粒進捗は PR summary の completed sub-scope に記載してよい。
- `.agent/tasks/todo.md` へ新規 TODO を追加する場合、roadmap completion bundle 単位で追加する。
- implementation atom（例: alias追加/adapter接続/emit追加/単体test1件）単位の canonical TODO 追加は禁止。
- TODO には、どの roadmap entry の `completion_condition` / `known_gap_ref` を閉じる bundle かを明記する。
- bundle 境界が不明な場合は TODO を追加せず、follow-up prompt または確認事項として出力する。
- Issue の closed / aggregated / not_planned 状態を implemented 根拠にしない。`docs/system-roadmap.yaml` の `completion_condition` / `known_gap_ref` を completion judgment の参照として使うが、ロードマップの status 記述のみを実装根拠にしない。実装状態の確認は実コードから行う。
