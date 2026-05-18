# Design SSOT Index — 設計仕様参照インデックス

このファイルはエージェントが設計判断の根拠を参照するための SSOT インデックスです。
実装を変更する前に、該当するドキュメントを必ず参照してください。

---

## 設計ドキュメント一覧

### 1. Commit Inference Engine（コミット型推論エンジン）

| 種別 | パス |
|---|---|
| 仕様 YAML | `docs/design/commit-inference-engine.yaml` |
| 思想・取り扱い | `docs/design/commit-inference-engine.md` |

**概要**: トークン類似度 (S1) / ライフサイクル経過 (S3) / アイテムセット共起 (S2) の
三軸を組み合わせたコミット型推論エンジン。
二段階コミット（S1: アイテム確定 → S2: 解決コード確定）で `operation_log` を SSOT として書き込む。

**参照すべき場面**:
- `operation_log` / `operation_log_item` のスキーマを変更するとき
- キャッシュのキー設計（特に `cache_resolutions_by_context` の items_sig 専用キー）を議論するとき
- S3 ライフサイクル統計の信頼性ゲート条件を変更するとき

---

### 2. Context Route Recommendation（コンテキストルート推薦）

| 種別 | パス |
|---|---|
| 仕様 YAML | `docs/design/context-route-recommendation.yaml` |
| 思想・取り扱い | `docs/design/context-route-recommendation.md` |

**概要**: 離散トークン + セッションプレフィックスのコサイン類似度で
「次の操作」と「次の参考情報」を推薦するエンジン。
ニューラル訓練不要、オフラインファースト、CPU のみ。

**対応実装**:
- `db/context_route_tables.sql` — コアテーブル
- `db/seed_empty.sql` — `function_parameters` に policy シード値
- `backend/runtime/ContextRouteRecommendationResolver.cs` — 推薦解決 Resolver
- `backend/schema/ContextRoutePolicyContracts.cs` — `ContextRoutePolicy` レコード（純粋 DTO）
- `backend/repository/TopologyRepository.cs` — `LoadFunctionParameterAsync` 経由で policy 読み込み

**参照すべき場面**:
- `ContextRouteRecommendationResolver` の推薦ロジックを変更するとき
- チューニングパラメータ（min_similarity / top_k 等）の意味を確認するとき
- `context_token_registry` にトークンを追加するとき（value の範囲 [-1, 1] を守る）
- Admin UI ページ (`/admin/context-token-registry`) の仕様を変更するとき

---


---

### 3. Runtime Log Retention Scheduler（ログ保持スケジューラ）

| 種別 | パス |
|---|---|
| 仕様 YAML | `docs/design/runtime-log-retention-scheduler.yaml` |
| 思想・取り扱い | `docs/design/runtime-log-retention-scheduler.md` |

**概要**: self-learning 系ログの retention を topology policy (`function_parameters`) で管理し、
backend runtime worker が canonical route を通して delete/anonymize/aggregate を実行する。

**参照すべき場面**:
- retention 日数や対象ログ種別の追加・変更を議論するとき
- scheduler 実行導線を cron 直叩きではなく runtime 経路で保つ必要があるとき
- Disabled / PolicyMissing / PolicyInvalid の status 契約を確認するとき

## SSOT 原則のまとめ

| 対象 | SSOT の場所 |
|---|---|
| 推薦エンジンのチューニングパラメータ | `function_parameters` テーブル（`function_name='context_route_recommendation_resolve'`, `parameter_key='default_policy'`） |
| トークン辞書（意味方向ベクトル） | `context_token_registry` テーブル |
| コミット済みの操作ログ | `operation_log` + `operation_log_item` |
| Runtime コードの定数 | **禁止** — すべてレジストリまたはコンフィグから読む |

---

## 業務語彙の除外方針

上記設計ドキュメントは**業務固有語彙を意図的に除去**してあります。
業務固有の実装（maintenance / part_no / work_code 等）は
これらの抽象仕様を実装する際に使用するドメイン語彙であり、
Runtime 層には混入させません。

詳細: `.agent/reports/context-route-recommendation-v1.md` の
「business-specific naming の排除」セクションを参照。
