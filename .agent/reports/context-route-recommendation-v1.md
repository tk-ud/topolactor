# Implementation Report: Context Route Recommendation Runtime (Issue #17)

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
| `backend/schema/ContextRouteContracts.cs` | 抽象 Runtime 用データコントラクト |
| `backend/repository/ContextRouteRepository.cs` | コンテキストルートリポジトリ（in-memory skeleton） |
| `backend/runtime/ContextVectorBuilder.cs` | sparse vector ビルダー（event vector / prefix vector / l2 norm） |
| `backend/runtime/ContextNeighborSearch.cs` | cosine similarity + nearest prefix search |
| `backend/runtime/ContextRouteRecommendationResolver.cs` | 推薦解決 resolver（canonical route 挿入点） |
| `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs` | テスト群 |

### 変更

| ファイル | 変更内容 |
|---|---|
| `backend/schema/Contracts.cs` | `OperationVector` にコンテキストフィールド追加、`RuntimeWorkingShape` / `Emission` に `ContextRouteRecommendation` 追加 |
| `backend/runtime/OperationVectorResolver.cs` | `Context` dict から `ContextSessionId` / `ContextUserId` / `ContextTokenIds` / `ContextRecordId` を抽出 |
| `backend/runtime/RuntimeExecutor.cs` | `ContextRouteRecommendationResolver` を Step 9 として組み込み |
| `backend/runtime/EmissionBuilder.cs` | `ContextRouteRecommendation` を `Emission` に転送 |
| `backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs` | `CreateExecutor()` に新依存を追加、emission 内 recommendation 確認アサーション追加 |
| `backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj` | schema glob を `../../schema/*.cs` に変更 |
| `backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj` | schema glob を `../../schema/*.cs` に変更 |
| `frontend/api/dispatch.ts` | `ContextRouteRecommendation` / `RecommendationCandidate` 型を追加、`Emission` に追加 |
| `frontend/components/EmissionView.tsx` | `RecommendationSection` コンポーネント追加（projection のみ、計算ロジックなし） |
| `db/README.md` | `context_route_tables.sql` を追加 |

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
| `ResolveNextOperations` | `ContextRouteRecommendationResolver` |
| `ResolveNextTokens` | `ContextRouteRecommendationResolver` |
| `AppendContextEventAsync` | `ContextRouteRepository` |
| `LoadActiveTokensAsync` | `ContextRouteRepository` |
| `LoadRecentPrefixVectorsAsync` | `ContextRouteRepository` |
| `GetTransitionStatsAsync` | `ContextRouteRepository` |
| `UpsertEventVectorCacheAsync` | `ContextRouteRepository` |
| `UpsertPrefixVectorCacheAsync` | `ContextRouteRepository` |

## business-specific naming の排除

- `enum_registry` → `context_token_registry`
- `session_event` → `context_event`
- `action_transition_stats` → `context_transition_stats`
- maintenance / work_content / parts / maint_log 等の業務語彙は Runtime 層に一切存在しない

## fallback の不存在

- `InsufficientHistory` は status の一値であり、silent fallback ではない
- 候補なしは `StatusDetail` に明示コード（`NO_SESSION_ID` / `NO_CONTEXT_HISTORY` / `INSUFFICIENT_CONTEXT_HISTORY`）を返す
- `ExplicitError` は resolver 内部例外時に返す明示状態
- null status は存在しない

## Frontend 計算ロジック隔離

- `EmissionView.tsx` の `RecommendationSection` は projection のみ（JSON 表示）
- cosine 計算・nearest search・遷移確率計算はすべて Backend Runtime のみ
- Frontend は `ContextRouteRecommendation` を data として受け取り表示するだけ

## PR #18 follow-up fixes（後続修正）

### Fix #2: DefaultEntitySearchIntegrationTests — 依存追加

`RuntimeExecutor` に `ContextRouteRecommendationResolver` を第12引数として追加した際、
`DefaultEntitySearchIntegrationTests.CreateEndpoint()` の構築コードが未更新だった。
`ContextRouteRepository` + `ContextRouteRecommendationResolver` の構築チェーンを追加して修正。

### Fix #3: 最近傍 prefix から next operation 候補が生成されない問題

**原因**: `ContextPrefixVectorRecord` は pure vector cache として設計され、後続イベントのデータを持っていなかった。
`FindNearestPrefixes` 内で `ContextNeighborResult` を生成する際に `NextOperation=null, NextTokenIdsHint=null` をハードコードしていた。

**修正**:
1. `ContextPrefixVectorRecord` に `NextOperation` と `NextTokenIdsHint` を optional フィールドとして追加（DB 側で後続イベントを JOIN してセット）
2. `FindNearestPrefixes` を `c.NextOperation` / `c.NextTokenIdsHint` を `ContextNeighborResult` に転送するよう修正
3. `LoadRecentPrefixVectorsAsync` を `virtual` に変更（テスト上書き可能）
4. `LoadActiveTokensAsync` を `virtual` に変更（テスト上書き可能）
5. `ResolveAsync_WithPrefixHistory_ReturnsOkWithNextOperationCandidates` テスト追加:
   stub subclass が 15 件のプレフィックス候補（`NextOperation="action_next"` 付き）を返し、
   `ResolveAsync` が `Status=Ok` かつ `NextOperations` に `"action_next"` を含むことを検証

## 実行チェック結果

| チェック | 結果 | 備考 |
|---|---|---|
| `bash .agent/tests/check-structure.sh` | PASS（全84項目） | dotnet/deno 不要。bash のみで実行 |
| `bash .agent/tests/check-db-schema.sh` | SKIP | PostgreSQL 接続情報が環境に存在しない |
| `bash .agent/tests/check-backend-tests.sh` | SKIP | dotnet がこの実行環境に存在しない（CI では実行） |
| `bash .agent/tests/check-frontend-types.sh` | SKIP | deno がこの実行環境に存在しない（CI では実行） |
| `bash .agent/tests/check-default-entity-search.sh` | SKIP | dotnet / deno 両方必要（CI では実行） |

structure-check は全項目 OK を確認。backend/frontend テストは CI で実行される。

---

## SSOT Config Registry リファクタリング（後続）

### 問題
`ContextRouteRecommendationResolver` にチューニング定数が7つハードコードされていた。

```csharp
private const float MinSimilarity = 0.05f;
private const int TopK = 50;
...
```

### 対応

#### SSOT 抽象化
- `backend/schema/ContextRouteConfigContracts.cs` 追加 — `ContextRouteConfig` レコード（全7パラメータ + `Default` 静的プロパティ）
- Runtime コードへの数値リテラル直書きを完全に排除

#### DB Registry（SSOT の永続化）
- `db/context_route_config.sql` 追加 — `context_route_config` テーブル（key-value レジストリ形式）
- 7パラメータのシード値を `INSERT ... ON CONFLICT DO NOTHING` で投入

#### Repository
- `backend/repository/ContextRouteConfigRepository.cs` 追加
  - `LoadConfigAsync` — スケルトン: `ContextRouteConfig.Default` を返す
  - `SaveConfigAsync` — スケルトン: no-op

#### Resolver への注入
- `ContextRouteRecommendationResolver` のコンストラクタ引数に `ContextRouteConfig config` 追加
- 全使用箇所を `_config.Xxx` に置き換え

#### テスト更新
- `RuntimeExecutorTests.CreateExecutor()` に `ContextRouteConfig.Default` を追加
- `ContextRouteRecommendationResolverTests.CreateResolver()` に `ContextRouteConfig.Default` を追加
- `DefaultEntitySearchIntegrationTests.CreateEndpoint()` に `ContextRouteConfig.Default` を追加

#### 管理 UI（UIで操作できる）

| ファイル | 役割 |
|---|---|
| `frontend/api/adminApi.ts` | Admin API クライアント型と fetch 関数 |
| `frontend/routes/api/admin/context-route-config.ts` | GET/PUT エンドポイント（スケルトン） |
| `frontend/routes/api/admin/context-token-registry.ts` | GET/POST エンドポイント（スケルトン） |
| `frontend/islands/ContextRouteConfigEditor.tsx` | 設定編集 Island（インタラクティブ） |
| `frontend/islands/ContextTokenRegistryEditor.tsx` | トークン Registry 管理 Island |
| `frontend/routes/admin/context-route-config.tsx` | `/admin/context-route-config` ページ |
| `frontend/routes/admin/context-token-registry.tsx` | `/admin/context-token-registry` ページ |
| `frontend/routes/admin/index.tsx` | Admin トップに Registry へのリンク追加 |

#### 設計原則遵守
- Runtime に定数直書きなし — `ContextRouteConfig` が唯一の数値ソース
- 自動学習（キャッシュ再構築）・レコメンドともに `context_token_registry` ハブ Registry 参照
- UI から設定を操作可能（`/admin/context-route-config`, `/admin/context-token-registry`）
- Frontend は projection のみ — 計算ロジックはすべて Backend Runtime

## SSOT 統一リファクタリング（PR #19 追加修正）

### 問題（修正前の状態）

Config の SSOT が3箇所に分散していた（data-defined topology 原則違反）:

1. `ContextRouteConfig.Default` — C# static fallback（production コードに hardcode）
2. `frontend/routes/api/admin/context-route-config.ts` の `DEFAULT_CONFIG` — Frontend 擬似 SSOT
3. `frontend/routes/api/admin/context-token-registry.ts` の `SEED_TOKENS` — Frontend が Registry として機能

### 修正内容

#### Backend schema
- `ContextRouteConfigContracts.cs` から `Default` 静的プロパティを完全削除
- `ConfigLoadResult` 判別共用体を追加: `Loaded(Config)` / `MissingPolicy` / `InvalidPolicy(Reason)`

#### Repository
- `LoadConfigAsync` の戻り値を `Task<ContextRouteConfig>` → `Task<ConfigLoadResult>` に変更
- スケルトンは `Default` ではなく `MissingPolicy` を返す — policy-missing が明示的状態になった

#### Resolver
- コンストラクタ引数を `ContextRouteConfig config` → `ContextRouteConfigRepository configRepository` に変更
- `ResolveAsync` の先頭で `LoadConfigAsync` を呼び出し、`MissingPolicy` / `InvalidPolicy` なら `ExplicitError` を返す
- `ResolveNextOperations` / `ResolveNextTokens` の引数に `ContextRouteConfig config` を追加（caller が渡す）

#### テスト
- `ContextRouteConfigTestFixtures.cs` を新規追加: `ValidConfig()` / `StubLoadedConfigRepository` / `StubMissingConfigRepository`
- `CreateResolver()` / `CreateExecutor()` / `CreateEndpoint()` が `ContextRouteConfig.Default` を参照しなくなった
- 新テスト追加: `ResolveAsync_MissingPolicy_ReturnsExplicitError_NotDefault`

#### Frontend API routes → 501
- `context-route-config.ts`: `DEFAULT_CONFIG` 削除 → GET/PUT が 501 `CONFIG_REGISTRY_ENDPOINT_NOT_BOUND`
- `context-token-registry.ts`: `SEED_TOKENS` 削除 → GET/POST が 501 `TOKEN_REGISTRY_ENDPOINT_NOT_BOUND`

#### Frontend client (`adminApi.ts`)
- `defaultContextRouteConfig` 定数を削除
- `fetchContextRouteConfig` の戻り値を `ContextRouteConfig | null`（501 → null）
- `fetchContextTokens` の戻り値を `ContextToken[] | null`（501 → null）

#### Islands
- `ContextRouteConfigEditor`: 初期値を `null`、501 時は「レジストリ未接続」表示。「デフォルトに戻す」ボタン削除
- `ContextTokenRegistryEditor`: 501 時は「レジストリ未接続」表示

#### ドキュメント更新
- `docs/design/context-route-recommendation.md`: Default fallback 禁止ルール明示追加
- `.agent/docs/design-ssot-index.md`: SSOT を DB registry 単一に統一（`ContextRouteConfig.Default` の記述削除）
- `db/context_route_config.sql`: コメントを「Default との同期」から「DB が唯一の正解ソース」に修正
- `db/README.md`: 同様修正

### 設計原則の回復

| 修正前 | 修正後 |
|---|---|
| DB 空 → `Default` で継続（silent fallback） | DB 空 → `MissingPolicy` → `ExplicitError` |
| Frontend API → hardcode data 返却 | Frontend API → 501（Registry 未接続を明示） |
| `ContextRouteConfig.Default` が擬似 SSOT | `context_route_config` テーブルが唯一 SSOT |
| test が production Default に依存 | test は fixture (`ValidConfig()`) を使用 |

## 残 TODO

`.agent/tasks/todo.md` 参照。
