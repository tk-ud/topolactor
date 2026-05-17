# Implementation Report: Context Route Recommendation Runtime

## 実装概要

topolactor の抽象 Runtime 機能として **Context Route Recommendation Runtime** を実装した。

業務固有の命名（maintenance / work_content / parts 等）は Runtime 層に一切混入していない。
canonical runtime route を迂回せず、`component_expand` の後に `context_route_recommendation_resolve` として接続した。

## Canonical Route 接続位置

```text
stored_topology_data
→ user_operation
→ operation_vector
→ attractor_resolve
→ structure_map_resolve
→ package_resolve
→ schema_resolve
→ component_expand
→ context_route_recommendation_resolve   ← 挿入位置（Step 9）
→ emission_or_projection                 ← Step 10
```

## 変更ファイル

### 新規追加

| ファイル | 役割 |
|---|---|
| `db/context_route_tables.sql` | context route recommendation 用 DB テーブル群 |
| `db/context_route_config.sql` | context_route_config レジストリテーブル + シード値 |
| `backend/schema/ContextRouteContracts.cs` | 抽象 Runtime 用データコントラクト |
| `backend/schema/ContextRouteConfigContracts.cs` | `ContextRouteConfig` レコード + `ConfigLoadResult` 判別共用体 |
| `backend/repository/ContextRouteRepository.cs` | コンテキストルートリポジトリ（in-memory skeleton） |
| `backend/repository/ContextRouteConfigRepository.cs` | 設定レジストリリポジトリ（in-memory skeleton） |
| `backend/runtime/ContextVectorBuilder.cs` | sparse vector ビルダー（event vector / prefix vector / l2 norm） |
| `backend/runtime/ContextNeighborSearch.cs` | cosine similarity + nearest prefix search |
| `backend/runtime/ContextRouteRecommendationResolver.cs` | 推薦解決 resolver（canonical route 挿入点） |
| `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs` | テスト群 |
| `backend/tests/Topolactor.Runtime.Tests/ContextRouteConfigTestFixtures.cs` | テスト専用 fixture / stub |
| `docs/design/context-route-recommendation.yaml` | 抽象設計 YAML（SSOT） |
| `docs/design/context-route-recommendation.md` | 設計思想・取り扱い方針 |
| `docs/design/commit-inference-engine.yaml` | 関連設計 YAML（SSOT） |
| `docs/design/commit-inference-engine.md` | 関連設計思想 |
| `.agent/docs/design-ssot-index.md` | 設計 SSOT 参照インデックス |

### 変更

| ファイル | 変更内容 |
|---|---|
| `backend/schema/Contracts.cs` | `OperationVector` にコンテキストフィールド追加、`RuntimeWorkingShape` / `Emission` に `ContextRouteRecommendation` 追加 |
| `backend/runtime/OperationVectorResolver.cs` | `Context` dict から `ContextSessionId` / `ContextUserId` / `ContextTokenIds` / `ContextRecordId` を抽出 |
| `backend/runtime/RuntimeExecutor.cs` | `ContextRouteRecommendationResolver` を Step 9 として組み込み |
| `backend/runtime/EmissionBuilder.cs` | `ContextRouteRecommendation` を `Emission` に転送 |
| `backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs` | `CreateExecutor()` に新依存を追加、emission 内 recommendation 確認アサーション追加 |
| `backend/tests/Topolactor.Integration.Tests/DefaultEntitySearchIntegrationTests.cs` | `CreateEndpoint()` に新依存を追加 |
| `frontend/api/dispatch.ts` | `ContextRouteRecommendation` / `RecommendationCandidate` 型を追加、`Emission` に追加 |
| `frontend/components/EmissionView.tsx` | `RecommendationSection` コンポーネント追加（projection のみ、計算ロジックなし） |

## 追加テーブル（context_route_tables.sql）

### コア（v1 必須）

| テーブル | 役割 |
|---|---|
| `context_token_registry` | 離散トークン辞書。value が sparse vector component |
| `context_token_binding` | optional: テーブル/ドメインごとのトークングループスコープ |
| `context_session` | implicit session grouping |
| `context_event` | append-only 操作イベントログ（唯一の必須ログ） |
| `context_record_snapshot_cache` | レコードごとの現在 token_ids キャッシュ |
| `context_event_vector_cache` | イベントごとの sparse vector + l2_norm キャッシュ |
| `context_prefix_vector_cache` | session prefix ごとの累積 vector キャッシュ（cosine 検索用） |
| `context_transition_stats` | 操作遷移確率集計（Bayesian smoothing） |

### optional（v1 延期・isolated）

| テーブル | 役割 |
|---|---|
| `context_cluster` | optional: セッションクラスタ割当 |
| `context_cluster_label` | optional: クラスタラベル（human/LLM） |
| `context_drift_signal` | optional: Bollinger band drift/spike 検出出力 |

## 追加関数

| 関数 | クラス |
|---|---|
| `BuildEventVector` | `ContextVectorBuilder` |
| `BuildPrefixVector` | `ContextVectorBuilder` |
| `ComputeL2Norm` | `ContextVectorBuilder` |
| `ComputeCosineSimilarity` | `ContextNeighborSearch` |
| `FindNearestPrefixes` | `ContextNeighborSearch` |
| `ResolveAsync` | `ContextRouteRecommendationResolver` |
| `ResolveNextOperations(neighbors, stats, config)` | `ContextRouteRecommendationResolver` |
| `ResolveNextTokens(neighbors, config)` | `ContextRouteRecommendationResolver` |
| `AppendContextEventAsync` | `ContextRouteRepository` |
| `LoadActiveTokensAsync` | `ContextRouteRepository` |
| `LoadRecentPrefixVectorsAsync` | `ContextRouteRepository` |
| `GetTransitionStatsAsync` | `ContextRouteRepository` |
| `LoadConfigAsync` | `ContextRouteConfigRepository` |
| `SaveConfigAsync` | `ContextRouteConfigRepository` |

## ContextRouteConfig SSOT 設計

### 原則

Runtime コードへの数値リテラル直書きは禁止。全チューニングパラメータは
`context_route_config` テーブル（DB registry）が唯一の SSOT。

`ContextRouteConfig.Default` 静的プロパティは存在しない。
policy-missing は `ConfigLoadResult.MissingPolicy` として明示される。

### ConfigLoadResult 判別共用体

```csharp
ConfigLoadResult.Loaded(Config)      — 正常ロード
ConfigLoadResult.MissingPolicy       — テーブルが空（seed 未実行）
ConfigLoadResult.InvalidPolicy(Reason) — 必須キー欠損
```

### In-memory skeleton の動作（DB 接続前）

`ContextRouteConfigRepository.LoadConfigAsync` は `db/context_route_config.sql` の
INSERT 行に対応する seed 値を返す（`TopologyRepository` が `db/seed_empty.sql` に対応する
in-memory レコードを返すのと同じパターン）。

```
LoadConfigAsync → Loaded(seedConfig)   # skeleton
                                       # 実 DB: SELECT → Loaded / MissingPolicy / InvalidPolicy
SaveConfigAsync → no-op               # skeleton
                                       # 実 DB: INSERT ... ON CONFLICT DO UPDATE
```

### Resolver での policy-missing ハンドリング

```
LoadConfigAsync → MissingPolicy  → ExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND")
LoadConfigAsync → InvalidPolicy  → ExplicitError("CONTEXT_ROUTE_POLICY_INCOMPLETE:...")
LoadConfigAsync → Loaded(config) → 通常処理継続
```

## Admin UI（未接続・プレースホルダー）

Admin UI ページ（`/admin/context-route-config`, `/admin/context-token-registry`）は
**表示層のプレースホルダー**として実装済み。

**現在の状態**:
- フロントエンド API ルート (`/api/admin/context-route-config`, `/api/admin/context-token-registry`) は **501 Not Implemented** を返す
- Island は 501 受信時に「レジストリ未接続」を表示（ハードコード値なし）
- 管理者による設定変更は**まだ機能しない**

**TODO（実装が必要な残作業）**:
- `ContextRouteConfigRepository` の実 DB 接続（`LoadConfigAsync` / `SaveConfigAsync`）
- `ContextRouteRepository` の実 DB 接続（`context_token_registry` 読み書き）
- フロントエンド API ルートの実装（501 → 実バックエンド呼び出し）
- deprecate エンドポイント実装（`/api/admin/context-token-registry/:id/deprecate`）

詳細は `.agent/tasks/todo.md` 参照。

## business-specific naming の排除

- `enum_registry` → `context_token_registry`
- `session_event` → `context_event`
- `action_transition_stats` → `context_transition_stats`
- maintenance / work_content / parts / maint_log 等の業務語彙は Runtime 層に一切存在しない

## status の明示性

- `InsufficientHistory` は status の一値であり、silent fallback ではない
- 候補なしは `StatusDetail` に明示コード（`NO_SESSION_ID` / `NO_CONTEXT_HISTORY` / `INSUFFICIENT_CONTEXT_HISTORY`）を返す
- `ExplicitError` は resolver 内部例外時または policy-missing 時に返す明示状態
- null status は存在しない

## Frontend 計算ロジック隔離

- `EmissionView.tsx` の `RecommendationSection` は projection のみ（JSON 表示）
- cosine 計算・nearest search・遷移確率計算はすべて Backend Runtime のみ
- Frontend は `ContextRouteRecommendation` を data として受け取り表示するだけ

## 実行チェック結果

| チェック | 結果 | 備考 |
|---|---|---|
| `bash .agent/tests/check-structure.sh` | PASS（全84項目） | dotnet/deno 不要。bash のみで実行 |
| `bash .agent/tests/check-db-schema.sh` | SKIP | PostgreSQL 接続情報が環境に存在しない |
| `bash .agent/tests/check-backend-tests.sh` | SKIP | dotnet がこの実行環境に存在しない（CI では実行） |
| `bash .agent/tests/check-frontend-types.sh` | SKIP | deno がこの実行環境に存在しない（CI では実行） |
| `bash .agent/tests/check-default-entity-search.sh` | SKIP | dotnet / deno 両方必要（CI では実行） |

structure-check は全項目 OK を確認。backend/frontend テストは CI で実行される。

## 残 TODO

`.agent/tasks/todo.md` 参照。
