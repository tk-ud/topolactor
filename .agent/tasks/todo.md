# Agent Task List

---
未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |
| `test-proof-manifest-ci-gate` | test 証明 Manifest / CI gate | partial | 1 | `docs/design/test-proof-manifest-ssot.yaml` |
| `frontend-admin-projection-expression-e2e-completion` | admin 投影登録/更新 E2E 完全化 | not_started | 1 | `docs/design/admin-console-workflow-ssot.yaml` |
| `aggregate-trigger-substrate` | 集計トリガー基盤 | not_started | 1 | `docs/design/runtime-orchestration-ssot.yaml` |

---

## Bundle `future-external-bundle-gate`

**Status:** not_started  
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion/Sheets/Slack/GitHub/Webhook/REST-API-Connector/NoCode-Loop — 個別 SSOT 揃うまで実装しない

---

## Bundle `helper-manual`

**Status:** not_started  
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

- [ ] helper/manual category 候補の実装設計
- [ ] Desktop AI / CLI / MCP Reader 向けライティング方針
- [ ] ヘルプコンポーネント実装（SSOT カテゴリ構造ゲート）

---

## Bundle `product-nocode-loop-acceptance`

**Status:** not_started

- [ ] `product.dynamic_support_nocode_loop` 手動受入（roadmap 追従）

---

## Bundle `test-proof-manifest-ci-gate`

**Status:** partial  
**SSOT:** `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/test-bundles.yaml`（reverse lookup）, `docs/system-roadmap.yaml`（参照）

- [x] system-wide test 証明 Manifest と CI gate 最適化（SSOT proof graph / reverse lookup / integrity gate / workflow wiring を追加。known gap は SSOT に残す）
  - 問題点: 既存 test は個別に存在するが、DB/schema/seed、backend runtime、scheduler cron/hook/client、manifest dispatch、external intake/API、instance substrate、admin/frontend projection がそれぞれ何を証明し、何を証明せず、どの時系列順序で接続されるかが SSOT として固定されていない。
  - 目的: `docs/design/test-proof-manifest-ssot.yaml` を正本として、既存 test 群を system-wide proof graph として扱える状態にする。
  - 改善方針: `proof_id`, `proof_order`, `scope_phase`, `domain`, `depends_on`, `unblocks`, `source_contract`, `target_contract`, `proves`, `does_not_prove` を軸に `.agent/docs/test-bundles.yaml` と CI gate を整合させる。frontend/admin だけでなく DB/backend/runtime/external/instance の未証明 edge も明示する。
  - 対応資料: `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/test-bundles.yaml`, `.agent/tests/check-unified-test-gate.sh`, `.agent/tests/check-frontend-all-tests.sh`, `.agent/tests/check-backend-tests.sh`, `.agent/tests/check-runtime-semantics.sh`
  - 対象ファイル: `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/test-bundles.yaml`, `.agent/tests/*.sh`, `backend/tests/**/*.cs`, `frontend/tests/*.test.ts`, `db/schema.sql`, `db/seed_empty.sql`, `db/demo_seed.sql`
  - 対象関数/単位: `proof_id`, `proof_order`, `scope_phase`, `domain`, `depends_on`, `unblocks`, `source_contract`, `target_contract`, `proves`, `does_not_prove`
  - OK軸: DB/backend/runtime/external/instance/frontend を含む時系列順序索引があり、各 domain の証明 edge が既存 test と未証明 gap に分解される。
  - NG軸: frontend/admin のみ、test file 一覧のみ、proves/does_not_prove のみで順序索引なし、DB/backend/runtime/external/instance gap の欠落、seed-only/JSON-only/backend-unit-only 成功扱い、CI gate が証明 Manifest と無関係。
  - 後続: `frontend-admin-projection-expression-e2e-completion` は本 Bundle 完了後に proof gap を読んで処理する。

---

## Bundle `frontend-admin-projection-expression-e2e-completion`

**Status:** not_started  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/component-catalog-classification-ssot.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `docs/system-roadmap.yaml`（参照）

- [ ] admin/** 登録・更新由来の projection expression E2E 完全化
  - 前提: `test-proof-manifest-ci-gate` 完了後、proof manifest の未証明 edge を読んで処理する。
  - 問題点: 既存 E2E/seed/projection/layout/render test は seed 到達・projection JSON 表示・個別 utility 境界を証明するが、admin 登録/更新から readback された投影が catalog/runtime/layout/visual DOM まで成立することを一気通貫で証明していない。
  - 目的: seed を test data / 初期条件として使うことは許可しつつ、証明入口は admin/** の登録・更新操作に固定し、frontend projection expression chain の未証明領域を潰す。
  - 改善方針: 既存 E2E 相当 test を、seed 由来の test data を admin 登録/更新経路へ投入 → projection readback → catalog 解決 → props/event/design/layout validation → runtime component render → visual guard surface → rendered DOM assertion まで通す完全実装へ昇格する。componentIds 表示、projection JSON 表示、fallback/error/empty DOM のみを成功扱いしない。
  - 対応資料: `.agent/docs/test-bundles.yaml`, `frontend/tests/projectionLaneSeedHarness.test.ts`, `frontend/tests/uiRenderedInteraction.test.ts`, `frontend/tests/adminUxGuard.test.ts`, `frontend/tests/visualLayoutBuilder.test.ts`, `frontend/tests/runtimeComponentFactory.test.ts`, `frontend/tests/projectionConstructor.test.ts`
  - 対象ファイル: `frontend/components/ProjectionView.tsx`, `frontend/components/LayoutVisualAuditCanvas.tsx`, `frontend/components/catalog.ts`, `frontend/runtime/projectionConstructor.ts`, `frontend/runtime/renderEmission.ts`, `frontend/runtime/projectionRuntime.ts`, `frontend/runtime/runtimeComponentAdapter.ts`, `frontend/runtime/runtimePrimitiveRenderer.ts`, `frontend/runtime/layoutComponentPreview.ts`, `frontend/runtime/visualLayoutUtils.ts`
  - 対象関数: `constructProjection`, `projectionFromEmission`, `renderRuntimeComponents`, `renderEmission`, `adaptComponentDataHub`, `renderRuntimeComponent`, `buildLayoutPreviewRuntimeSpec`, `renderLayoutComponentPreview`, `parseVisualLayoutPatchJson`, `buildVisualLayoutPatchJson`, `LayoutVisualAuditCanvas`, `ProjectionView`
  - OK軸: seed を test data として使用し、admin/** 経由の登録/更新結果を readback し、catalog 登録済み component として分類整合・props/event/design/layout 境界を通過し、runtime component と visual guard DOM で user-facing 表示を確認できる。
  - NG軸: seedData fixture の到達確認のみ、componentIds/projection JSON 表示のみ、admin 画面 DOM 表示のみ、backend promote unit のみ、catalog 外 componentKey 通過、分類ズレ、更新後 readback/DOM 差分なし、invalid layout node の skip による empty 成功、fallback/error component 成功扱い。
  - 要確認: unknown props を global fail-close にするか、SSOT/schema で許可された追加属性のみ透過するかを実装前に確定する。

---

## Bundle `aggregate-trigger-substrate`

**Status:** not_started  
**SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`

- [ ] 集計トリガー基盤の SSOT / test / backend / frontend(admin/contents Step3) 完全化
  - 問題点: 現行基盤には cron / hook / client trigger entry と admin/contents の集計 projection はあるが、UI/event/hook/cron 入力を処理関数へ渡し、idempotent event evidence、aggregate current row への atomic upsert、最低試行回数と対象比率による閾値判定、登録先 entity/relation への controlled materialization、materialization evidence までを一つの公開基盤設備として定義していない。
  - 目的: `aggregate-trigger-substrate` を公開基盤設備として定義し、admin/contents Step3 で構造化された処理関数を登録できるようにする。event は入力であり scope owner ではない。execution scope / transaction boundary / aggregate target / threshold policy / materialization target / approval policy は処理関数または operation definition 側が所有する。
  - 改善方針: UI は SQL / CASE / WHERE / 任意 table 名を保存しない。Step3 で、Step2 の logical entity または Step2.5 の relation definition から aggregate target / materialization target を選択し、conflict key、delta map、最低試行回数、比率分子/分母、比較演算子、target ratio、materialization policy、approval policy を構造化 payload として backend runtime に送る。repository は許可済み template に展開し、app-side read -> count++ -> update race を避けて atomic upsert / duplicate materialization guard を実装する。
  - 対応資料: `docs/framework-core.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`
  - 対象ファイル: `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`, `backend/runtime/AdminRuntime.cs`, `backend/runtime/RuntimeExecutor.cs`, `backend/runtime/ScreenDataShapeQueryRuntime.cs`, `backend/runtime/ScreenDataShapeQueryEvaluator.cs`, `backend/repository/ContextRouteRepository.cs`, `backend/repository/NpgsqlContextRouteRepository.cs`, `frontend/islands/**`, `frontend/components/**`, `frontend/tests/*.test.ts`, `backend/tests/**/*.cs`, `db/*.sql`
  - 対象関数/単位: `aggregate_trigger_definition`, `aggregate_trigger_event_log`, `aggregate_trigger_materialization_log`, `trigger_source`, `processing_function_scope`, `aggregate_target_binding`, `conflict_key_fields`, `delta_map`, `minimum_trial_count`, `ratio_numerator_field`, `ratio_denominator_field`, `comparison_operator`, `target_ratio`, `materialization_target_binding`, `materialization_payload_map`, `approval_policy`, `AggregateTriggerRuntime`, `AggregateTriggerRepository`, `AggregateTriggerDefinitionValidator`, `AggregateTriggerConditionEvaluator`
  - OK軸: SSOT が公開汎用設備として aggregate trigger を定義し、candidate/todo 等のアプリ固有名を正本化せず、Step3 が Step2/2.5 定義済み対象だけを選ばせ、backend が構造化定義を検証し、repository が fixed SQL template で idempotent event append / atomic upsert / minimum trial + ratio threshold / controlled materialization / duplicate prevention / evidence log を実装し、backend/frontend/test/proof manifest が event -> aggregate -> threshold -> materialize 経路を証明する。
  - NG軸: 特定アプリ専用の hardcode 実装、candidate/todo 等の具象名を substrate 要件へ混入、UI の raw SQL/CASE/WHERE 保存、任意 table 名入力、event 側を scope owner とする設計、Step2/2.5 未定義対象への登録、frontend persistence 判断、app-side read -> count++ -> update race、閾値超過時の二重 materialization、approval policy 未定義、projection aggregation のみで mutation/materialization 未証明、proof manifest の過剰主張。
  - 要確認: materialization payload map の初期表現を、processing function output として `function input event`, `aggregate current row`, `selected Step2 entity fields`, `selected Step2.5 relation fields`, `constant`, `generated value`, `runtime actor/source metadata` のどこまで許可するかを SSOT で確定する。
