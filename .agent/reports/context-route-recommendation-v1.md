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

### 新規追加（現在の状態 — fix 3 適用後）

| ファイル | 役割 |
|---|---|
| `db/context_route_tables.sql` | context route recommendation 用 DB テーブル群 |
| `backend/schema/ContextRouteContracts.cs` | 抽象 Runtime 用データコントラクト |
| `backend/schema/ContextRoutePolicyContracts.cs` | `ContextRoutePolicy` 純粋 DTO（defaults なし） |
| `backend/repository/ContextRouteRepository.cs` | コンテキストルートリポジトリ（in-memory skeleton） |
| `backend/runtime/ContextVectorBuilder.cs` | sparse vector ビルダー（event vector / prefix vector / l2 norm） |
| `backend/runtime/ContextNeighborSearch.cs` | cosine similarity + nearest prefix search |
| `backend/runtime/ContextRouteRecommendationResolver.cs` | 推薦解決 resolver（canonical route 挿入点） |
| `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs` | テスト群 |
| `backend/tests/Topolactor.Runtime.Tests/ContextRoutePolicyTestFixtures.cs` | テスト専用 fixture / stub |
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
| `LoadFunctionParameterAsync` | `TopologyRepository` |

## Policy SSOT 設計（fix 3 以降の現状）

### 原則

Runtime コードへの数値リテラル直書きは禁止。チューニングパラメータは
`function_parameters` テーブル（topology 定義テーブル）に格納される。
Context Route Recommendation は独立した設定サブシステムではなく、
既存 topology の **optional capability**。

- `function_name = 'context_route_recommendation_resolve'`
- `parameter_key = 'default_policy'`
- `parameter_value = JSONB blob`

policy-missing は `ExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND")` として明示される。
production fallback は Runtime コードに存在しない。

### In-memory skeleton の動作（DB 接続前）

`TopologyRepository.LoadFunctionParameterAsync` は `db/seed_empty.sql` の
function_parameters INSERT と一致する seed JSON を返す。

```
LoadFunctionParameterAsync("context_route_recommendation_resolve", "default_policy")
  → seed JSON (string)  # skeleton
  → null                # skeleton: 上記以外の function_name / parameter_key
  → null なら ExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND")
```

### Resolver での policy-missing ハンドリング

```
LoadFunctionParameterAsync → null    → ExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND")
LoadFunctionParameterAsync → 不正JSON → ExplicitError("CONTEXT_ROUTE_POLICY_INVALID:...")
LoadFunctionParameterAsync → 正常JSON → ParsePolicy → 通常処理継続
```

## Admin UI（token registry のみ）

Admin UI ページ:
- `/admin/context-token-registry` — context_token_registry 管理（島コンポーネントあり）
- `/admin/context-route-config` — **廃止**（fix 3 で削除）

**現在の状態**:
- フロントエンド API ルート `/api/admin/context-token-registry` は **501 Not Implemented** を返す
- token registry Island は 501 受信時に「レジストリ未接続」を表示（ハードコード値なし）
- 推薦エンジン設定の UI はない — `function_parameters` を直接操作する

**TODO（実装が必要な残作業）**:
- `ContextRouteRepository` の実 DB 接続（`context_token_registry` 読み書き）
- `TopologyRepository.LoadFunctionParameterAsync` の実 DB 接続（`function_parameters` SELECT）
- フロントエンド API ルートの実装（501 → 実 DB 接続）
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

---

## context_route_config 廃止 / topology 統合（PR #19 follow-up fix 3）

### 変更の目的

`context_route_config` を独立した設定テーブルとして扱っていた構造を廃止し、
context route recommendation を topolactor topology の **optional capability** として統合した。

### 削除ファイル

| ファイル | 理由 |
|---|---|
| `db/context_route_config.sql` | 独立 SSOT テーブル廃止 |
| `backend/repository/ContextRouteConfigRepository.cs` | 独立設定リポジトリ廃止 |
| `backend/schema/ContextRouteConfigContracts.cs` | `ContextRouteConfig` → `ContextRoutePolicy` に移行 |
| `backend/tests/Topolactor.Runtime.Tests/ContextRouteConfigTestFixtures.cs` | `ContextRoutePolicyTestFixtures.cs` に置換 |
| `frontend/routes/api/admin/context-route-config.ts` | 独立設定 API 廃止 |
| `frontend/islands/ContextRouteConfigEditor.tsx` | 独立設定 editor island 廃止 |
| `frontend/routes/admin/context-route-config.tsx` | 独立設定 admin page 廃止 |

### 新規 / 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `backend/schema/ContextRoutePolicyContracts.cs` | `ContextRoutePolicy` 純粋 DTO（defaults なし） |
| `backend/repository/TopologyRepository.cs` | `LoadFunctionParameterAsync` 追加（seed JSON 返す） |
| `backend/runtime/ContextRouteRecommendationResolver.cs` | `TopologyRepository` から policy 読み込みに変更 |
| `backend/tests/Topolactor.Runtime.Tests/ContextRoutePolicyTestFixtures.cs` | `ValidPolicy()` + `StubMissingPolicyTopologyRepository` |
| `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs` | `ContextRoutePolicy` / `TopologyRepository` に更新 |
| `backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs` | `topologyRepository` を共有して渡すように更新 |
| `backend/tests/Topolactor.Integration.Tests/DefaultEntitySearchIntegrationTests.cs` | `topologyRepository` を共有して渡すように更新 |
| `db/seed_empty.sql` | `function_parameters` に policy INSERT 追加 |
| `db/README.md` | `context_route_config.sql` 参照削除 |
| `frontend/api/adminApi.ts` | `context_route_config` 関連型・関数削除 |
| `frontend/routes/admin/index.tsx` | context-route-config リンク削除 |
| `frontend/routes/admin/context-token-registry.tsx` | context-route-config リンク削除 |
| `docs/design/context-route-recommendation.md` | topology policy source に更新 |
| `docs/design/context-route-recommendation.yaml` | `context_route_config` entity 削除、`topology_policy_source` 追加 |
| `.agent/docs/design-ssot-index.md` | SSOT テーブル更新 |

### Policy SSOT の変更

| 変更前 | 変更後 |
|---|---|
| `context_route_config` テーブル（独立） | `function_parameters` テーブル（topology 統合） |
| `ContextRouteConfigRepository.LoadConfigAsync` | `TopologyRepository.LoadFunctionParameterAsync` |
| `ConfigLoadResult` 判別共用体 | `string?` null-check（null → ExplicitError） |
| `ContextRouteConfig` レコード | `ContextRoutePolicy` レコード（defaults なし） |

### Policy-missing の動作

`TopologyRepository.LoadFunctionParameterAsync` が null を返した場合:
→ `ExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND")`

production fallback は Runtime コードに存在しない。

---

## CI red 修正（PR #19 follow-up fix 4）

### CI red 原因

GitHub Actions の `db-schema-check` が以下のエラーで失敗していた。

```
there is no unique or exclusion constraint matching the ON CONFLICT specification
```

`db/seed_empty.sql` で

```sql
ON CONFLICT (function_name, parameter_key) DO NOTHING
```

を使用しているが、`db/schema.sql` の `function_parameters` テーブルに
`(function_name, parameter_key)` の UNIQUE constraint が存在しなかった。

### 修正内容

`db/schema.sql` の `function_parameters` 定義に UNIQUE constraint を追加した。

```sql
CONSTRAINT uq_function_parameters_function_key
    UNIQUE (function_name, parameter_key)
```

これにより:
- `seed_empty.sql` の `ON CONFLICT (function_name, parameter_key) DO NOTHING` が成功する
- 同一 `(function_name, parameter_key)` に複数行が挿入されることを DB 側で防止する
- topology policy SSOT が分裂しない（function_parameters は per-function one policy row）

### PR description 修正

PR #19 の description が旧実装（context_route_config、ContextRouteConfig.Default 等）の
説明のままだったため、現状の実装方針（function_parameters SSOT、ContextRoutePolicy、
policy-missing → ExplicitError）を正確に反映した内容に全面更新した。

### 実行チェック結果

| チェック | 結果 | 備考 |
|---|---|---|
| `bash .agent/tests/check-structure.sh` | PASS（全84項目） | 実行済み |
| `bash .agent/tests/check-db-schema.sh` | SKIP | PostgreSQL 接続情報がこの実行環境に存在しない（CI で実行） |
| `bash .agent/tests/check-backend-tests.sh` | SKIP | dotnet がこの実行環境に存在しない（CI で実行） |
| `bash .agent/tests/check-frontend-types.sh` | SKIP | deno がこの実行環境に存在しない（CI で実行） |

### 残 TODO

`.agent/tasks/todo.md` 参照。
構造的な TODO として、`PolicyFunctionName` / `PolicyParameterKey` の固定参照を
将来 `structure_maps.state_policy` scoped policy に拡張する項目を追加済み。
