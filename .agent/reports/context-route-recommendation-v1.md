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

## 実行チェック

| チェック | 結果 |
|---|---|
| `bash .agent/tests/check-structure.sh` | 実行予定（コミット前に確認） |
| `bash .agent/tests/check-db-schema.sh` | 実行予定（DB ツールの有無による） |
| `bash .agent/tests/check-backend-tests.sh` | 実行予定（dotnet 環境の有無による） |
| `bash .agent/tests/check-frontend-types.sh` | 実行予定（deno 環境の有無による） |

## 残 TODO

`.agent/tasks/todo.md` 参照。
