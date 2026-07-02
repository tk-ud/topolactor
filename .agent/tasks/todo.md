# Agent Task List

---
未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |
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

## Bundle `aggregate-trigger-substrate`

**Status:** in_progress  
**SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`

- [ ] 集計トリガー基盤の test / backend / frontend(admin/contents Step3) 実装（SSOT contract は設計確定済み）
  - 問題点: SSOT contract は確定済み。現状は backend runtime / validator / repository fixed-template contract / frontend structured authoring helper / DB DDL の部分実装とローカル contract test はあるが、Admin contents Step3 本体UI enforcement、live PostgreSQL適用、AdminRuntime定義保存経路、cron/hook/client scheduler-to-manifest 実DB経路の executable proof は未完了。
  - 目的: `aggregate-trigger-substrate` を公開基盤設備として定義し、admin/contents Step3 で構造化された処理関数を登録できるようにする。event は入力であり scope owner ではない。execution scope / transaction boundary / aggregate target / threshold policy / materialization target / approval policy は処理関数または operation definition 側が所有する。
  - 改善方針: UI は SQL / CASE / WHERE / 任意 table 名を保存しない。Step3 で、Step2 の logical entity または Step2.5 の relation definition から aggregate target / materialization target を選択し、conflict key、delta map、最低試行回数、比率分子/分母、比較演算子、target ratio、materialization policy、approval policy を構造化 payload として backend runtime に送る。repository は許可済み template に展開し、app-side read -> count++ -> update race を避けて atomic upsert / duplicate materialization guard を実装する。
  - 対応資料: `docs/framework-core.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`
  - 対象ファイル: `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`, `backend/runtime/AdminRuntime.cs`, `backend/runtime/RuntimeExecutor.cs`, `backend/runtime/ScreenDataShapeQueryRuntime.cs`, `backend/runtime/ScreenDataShapeQueryEvaluator.cs`, `backend/repository/ContextRouteRepository.cs`, `backend/repository/NpgsqlContextRouteRepository.cs`, `frontend/islands/**`, `frontend/components/**`, `frontend/tests/*.test.ts`, `backend/tests/**/*.cs`, `db/*.sql`
  - 対象関数/単位: `aggregate_trigger_definition`, `aggregate_trigger_event_evidence`, `aggregate_trigger_materialization_evidence`, `trigger_source`, `processing_function_scope`, `aggregate_target_binding`, `conflict_key_fields`, `delta_map`, `minimum_trial_count`, `ratio_numerator_field`, `ratio_denominator_field`, `comparison_operator`, `target_ratio`, `materialization_target_binding`, `materialization_payload_map`, `approval_policy`, `AggregateTriggerRuntime`, `AggregateTriggerRepository`, `AggregateTriggerDefinitionValidator`, `AggregateTriggerConditionEvaluator`
  - OK軸: SSOT が公開汎用設備として aggregate trigger を定義し、特定アプリケーション名・特定ユースケース名を正本化せず、Step3 が Step2/2.5 定義済み対象だけを選ばせ、backend が構造化定義を検証し、repository が fixed SQL template で idempotent event append / atomic upsert / minimum trial + ratio threshold / controlled materialization / duplicate prevention / evidence log を実装し、backend/frontend/test/proof manifest が event -> aggregate -> threshold -> materialize 経路を証明する。
  - NG軸: 特定アプリ専用の hardcode 実装、特定アプリケーション名・特定ユースケース名を substrate 要件へ混入、UI の raw SQL/CASE/WHERE 保存、任意 table 名入力、event 側を scope owner とする設計、Step2/2.5 未定義対象への登録、frontend persistence 判断、app-side read -> count++ -> update race、閾値超過時の二重 materialization、approval policy 未定義、projection aggregation のみで mutation/materialization 未証明、proof manifest の過剰主張。
  - SSOT確定済み: materialization payload map の初期表現は `function_input_event`, `aggregate_current_row`, `selected_step2_entity_fields`, `selected_step2_5_relation_fields`, `constant`, `generated_value`, `runtime_actor_source_metadata` を許可し、raw SQL / CASE / WHERE / 未宣言 json path は禁止する。
