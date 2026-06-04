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

**関連実装surface**:
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

### 3. Runtime Orchestration OS（Runtime Orchestration SSOT）

| 種別 | パス |
|---|---|
| 仕様 YAML | `docs/design/runtime-orchestration-ssot.yaml` |

**概要**: Runtime Orchestration OS の独立 SSOT。
`runtime_timeline_scheduler` / `runtime_queue_scheduler` を polling ではない時間軸整列層として定義し、
`manifest_dispatcher`、frontend の API trigger lane と SSE projection lane、
backend の topology payload 変換・json linking・topology_function_binder・response/db notify/registry attractor を定義する。

**参照すべき場面**:
- scheduler / dispatcher / runtime destination の責務境界を変更するとき
- frontend の API trigger lane / SSE projection lane の経路意味を変更するとき
- backend topology runtime（vector conversion / json linking / topology_function_binder / output lanes）の設計境界を変更するとき
- manifest authority（role / dispatcher / runtime / topology_function_binding mapping / projection_constructor mapping）を変更するとき

---

### 4. SQL Attention Logs SSOT

| 種別 | パス |
|---|---|
| 仕様 YAML | `docs/design/sql-attention-logs-ssot.yaml` |
| 意味・取り扱い | `docs/design/sql-attention-logs-ssot.md` |

**概要**: SQL Attention の logs/current/hub_current/attention evidence を定義する静的 SSOT。  
primary target は `hubs.*`。`topologys.*` / registry は projection/support layer。  
`logs.current` / `logs.hub_current` は current-side projection の現行物理名。  
`logs.attention` は append-only evidence 境界。

**参照すべき場面**:
- SQL Attention logs schema / repository / scheduler / runtime を変更するとき
- `logs.current` / `logs.hub_current` / `logs.attention` の意味境界を確認するとき
- registry / hub / topology / attention の混同を避けるとき
- phase_vector / production evidence filling / topology projection recommendation の境界を確認するとき

---

### 5. Enum Dictionary SSOT（列候補辞書）

| 種別 | パス |
|---|---|
| 仕様 YAML | `docs/design/enum-dictionary-ssot.yaml` |

**概要**: enum item（点）と enum group（点集合ベクトル）の正本。管理コンソール Step 2 の列 `enumGroupId` 参照と Step 3 の select 入力に使用する。

**関連実装surface**:
- `db/enum_tables.sql` / `db/enum_seed.sql` — 物理テーブルと最小 seed
- `backend/runtime/AdminRuntime.cs` — `enum_dictionary:list_groups` / `get_group`、assign 時検証
- `frontend/islands/ContentsScreenDesignPanel.tsx` — Step 2/3 UI

**参照すべき場面**:
- 列定義に select 候補を紐づけるとき
- topology / manifest に enum 候補を埋め込まない境界を確認するとき
- enum_group 未解決時の blocking error 契約を変更するとき

---

### 6. Admin Console Workflow SSOT（管理コンソールワークフロー）

| 種別 | パス |
|---|---|
| 仕様 YAML | `docs/design/admin-console-workflow-ssot.yaml` |

**概要**: 管理コンソールページの責務分割とワークフロー順序を定義する SSOT。
- `admin/manifests` → DB系 / 配線系 / hub系
- `admin/contents` → UI系 / イベントトリガ系
- `admin/hub-navigation` → hub_relation によるページナビ順序設定（新規コンポーネント）

マニフェストは一画面単位のため、画面遷移の hub_relation 順序設定は独立した設定サーフェスとして宣言される。
Wiring（dispatcher axes: role/target/layer/action）は `admin/manifests` で設定するのが canonical であり、後工程の独立ステップではない。

**関連実装surface**:
- `frontend/islands/AdminMainFlowStepper.tsx` — メインフロー表示
- `frontend/content/adminGuides.ts` — `ADMIN_MAIN_FLOW_STEPS`
- `frontend/routes/admin/` — 各管理ページルート
- `frontend/islands/HubNavigationAdmin.tsx` — hub_relation 順序設定 Island（実装済み: list/create/update/deprecate/reorder）
- `hubs.hub_relations` / `hubs.topology_manifests` — DB binding

**参照すべき場面**:
- 管理コンソールのページ責務分割を変更するとき
- ワークフロー順序（6ステップ）を変更するとき
- `AdminMainFlowStepper` のステップ定義を変更するとき
- `admin/hub-navigation` コンポーネントの仕様・設計判断を確認するとき
- wiring 設定の位置（どのページで行うか）を確認するとき

---

## SSOT 原則のまとめ

| 対象 | SSOT の場所 |
|---|---|
| 推薦エンジンのチューニングパラメータ | `function_parameters` テーブル（`function_name='context_route_recommendation_resolve'`, `parameter_key='default_policy'`） |
| トークン辞書（意味方向ベクトル） | `context_token_registry` テーブル |
| コミット済みの操作ログ | `operation_log` + `operation_log_item` |
| Runtime コードの定数 | **禁止** — すべてレジストリまたはコンフィグから読む |
| 管理コンソールページ責務分割・ワークフロー順序 | `docs/design/admin-console-workflow-ssot.yaml` |

---

## 業務語彙の除外方針

上記設計ドキュメントは**業務固有語彙を意図的に除去**してあります。
業務固有の実装（maintenance / part_no / work_code 等）は
これらの抽象仕様を実装する際に使用するドメイン語彙であり、
Runtime 層には混入させません。

詳細: `.agent/reports/context-route-recommendation-v1.md` の
「business-specific naming の排除」セクションを参照。
