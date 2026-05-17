# Implementation Report: Context Route Persistence & Policy Scope

## Summary

v1 以降の未実装項目のうち、Persistence / Repository・Cache / Aggregation Pipeline・Policy Scope の各カテゴリを実装。Optional Analytics および Personalization は引き続き未実装（`.agent/tasks/todo.md` 参照）。

## Changes

### Persistence / Repository

#### NpgsqlTopologyRepository (新規)
`backend/repository/NpgsqlTopologyRepository.cs`

- `TopologyRepository` を継承した Npgsql 実装
- `LoadStructureMapAsync` — `structure_maps` テーブルから SELECT。`state_policy::text` も取得し `StatePolicyJson` に格納
- `LoadPackageAsync` — `package_registry` テーブルから SELECT
- `LoadSchemaAsync` — `schema_registry` テーブルから SELECT
- `LoadFunctionParameterAsync` — `function_parameters` テーブルから SELECT。policy-missing → null（呼び出し側で ExplicitError）

#### NpgsqlContextRouteRepository (新規)
`backend/repository/NpgsqlContextRouteRepository.cs`

- `ContextRouteRepository` を継承した Npgsql 実装
- `LoadActiveTokensAsync` — `context_token_registry` から `ANY(@ids) AND status='active'`
- `AppendContextEventAsync` — `context_session` upsert → `context_event` INSERT → near-realtime transition stats upsert
- `LoadRecentPrefixVectorsAsync` — `context_prefix_vector_cache` JOIN `context_session` + LATERAL JOIN で次イベントの `next_operation`/`next_token_ids_hint` を取得
- `GetTransitionStatsAsync` — `context_transition_stats` から role fallback 付き SELECT
- `UpsertEventVectorCacheAsync` — `context_event_vector_cache` に ON CONFLICT DO UPDATE
- `UpsertPrefixVectorCacheAsync` — `context_prefix_vector_cache` に ON CONFLICT DO UPDATE

#### 基底クラス変更
`backend/repository/TopologyRepository.cs` / `ContextRouteRepository.cs`

- `_connectionString` / `_logger` を `private` → `protected`（派生クラスから参照可能に）
- 全 Load* メソッドを `virtual` に変更（テストは基底クラスのメモリ実装を引き続き使用）
- `StructureMapRecord` に `string? StatePolicyJson = null` を追加

#### Npgsql パッケージ追加
両テストプロジェクト `.csproj` に `<PackageReference Include="Npgsql" Version="8.0.7" />` を追加。
テストコードは仮想メソッドをオーバーライドしたスタブを使用するため、テストから Npgsql コードは実行されない。

### Cache / Aggregation Pipeline

#### Near-realtime 遷移統計集計
`NpgsqlContextRouteRepository.AppendContextEventAsync` 内で `UpsertTransitionStatAsync` を呼び出し。

- 同一セッション内の直前イベントを取得
- `(prev_operation, next_operation, role, user_id)` の遷移統計を upsert
- Bayesian smoothing: `prob01 = (count_hits + 1) / (count_events + 11)` (α=1, β=10)

#### Event/Prefix Vector Cache Upsert
`UpsertEventVectorCacheAsync` / `UpsertPrefixVectorCacheAsync` を `NpgsqlContextRouteRepository` で実装。
バッチキューによる deferred batch は背景ジョブ基盤確立後に実装予定。

#### Retention Policy 定義
`db/seed_empty.sql` に `context_event_retention / retention_policy` を追加:

```json
{"hot_days": 90, "cold_days": 365, "archive_strategy": "delete", "batch_size": 1000}
```

リテンションジョブは `TopologyRepository.LoadFunctionParameterAsync("context_event_retention", "retention_policy")` でこのポリシーを読み込む。

### Policy Scope

#### structure_maps.state_policy からの policy_ref 解決
変更ファイル: `Contracts.cs`, `StructureMapResolver.cs`, `ContextRouteRecommendationResolver.cs`

`RuntimeWorkingShape` に `StructureMapStatePolicyJson` フィールドを追加。
`StructureMapResolver` がこれを `record.StatePolicyJson` から格納。

`ContextRouteRecommendationResolver.ResolvePolicyKey` が state_policy の `context_route_policy_ref` キーを読み出し、関数パラメータキーを動的に決定:

```json
// structure_maps.state_policy の例
{"context_route_policy_ref": "customer_portal_policy"}
```

この設定があると `function_parameters` の `parameter_key = 'customer_portal_policy'` を読み込む。
なければ `default_policy` にフォールバック（既存の動作と同一）。

### Deprecate エンドポイント追加

`frontend/routes/api/admin/context-token-registry/[tokenId]/deprecate.ts`

- `POST /api/admin/context-token-registry/{tokenId}/deprecate` を定義
- UUID 形式バリデーション付き
- 現状は 501（実 DB 接続未実装）。UIはこの 501 を処理済みのため 404 エラーは解消
- 実 DB 接続: Deno postgres クライアント追加またはバックエンド AdminEndpoint 経由で実装予定

## CI Status

| Check | Result |
|---|---|
| `check-structure.sh` | PASS |
| `check-backend-tests.sh` | 未実行 — `dotnet` コマンド環境なし |
| `check-frontend-types.sh` | 未実行 — `deno` コマンド環境なし |
| `check-default-entity-search.sh` | 未実行 — 両ツール不在 |
| `check-db-schema.sh` | 未実行（DB 変更なし） |

`dotnet` / `deno` が使用可能な環境での CI 実行が必要。

## Remaining TODO

`.agent/tasks/todo.md` 参照。残存項目:
- `context-token-registry` GET/POST の実 DB 接続
- Optional Analytics (clustering, drift detection, HNSW)
- Personalization UI
