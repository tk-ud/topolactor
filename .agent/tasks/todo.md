# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。
完了済み作業・PR修正履歴・旧方針の残骸は残さない。

## Context Route Recommendation Runtime

Context Route Recommendation v1 は main に統合済み。
以下は v1 以降の未実装項目のみ。

### Persistence / Repository

- [x] `ContextRouteRepository` を in-memory skeleton から実 DB クエリへ置換する
      → `NpgsqlContextRouteRepository` を追加。全メソッドを Npgsql で実装。
- [x] `TopologyRepository.LoadFunctionParameterAsync` を `function_parameters` 実読みに置換する
      → `NpgsqlTopologyRepository` を追加。全 Load* メソッドを Npgsql で実装。
- [ ] `context-token-registry` API を 501 から実 DB 接続へ置換する
      → deprecate エンドポイントは追加済み（501）。GET/POST の実 DB 接続は未実装。
        Deno postgres クライアントの追加またはバックエンド AdminEndpoint 経由での実装が必要。
- [x] `context-token-registry` の deprecate エンドポイントを実装する
      → `frontend/routes/api/admin/context-token-registry/[tokenId]/deprecate.ts` を追加。
        UUID バリデーション済み。実 DB 接続は上記 GET/POST と同様の方針で実装予定。

### Cache / Aggregation Pipeline

- [x] `UpsertEventVectorCacheAsync` / `UpsertPrefixVectorCacheAsync` の deferred batch 実装
      → `NpgsqlContextRouteRepository` で ON CONFLICT DO UPDATE による upsert を実装。
        バッチキューによる遅延書き込みは次フェーズ（背景ジョブ基盤の確立後）。
- [x] `context_transition_stats` の near-realtime 集計パイプライン実装
      → `AppendContextEventAsync` 内で前イベントを検索し (prev→current) 遷移統計を inline upsert。
        Bayesian smoothing: prob01 = (hits + 1) / (events + 11)。
- [x] `context_event` の hot/cold archive retention policy を定義する
      → `db/seed_empty.sql` に `context_event_retention / retention_policy` を追加。
        hot_days=90, cold_days=365, archive_strategy=delete, batch_size=1000。

### Policy Scope

- [x] Context Route Recommendation の `policy_ref` を `structure_maps.state_policy` から解決できるようにする
      → `StructureMapRecord.StatePolicyJson` を追加し `RuntimeWorkingShape.StructureMapStatePolicyJson` として伝播。
        `ContextRouteRecommendationResolver.ResolvePolicyKey` が `context_route_policy_ref` キーを読み出す。
- [x] `default_policy` 固定参照を structure_map / relation / hub scoped policy に拡張する
      → 上記 `ResolvePolicyKey` により、structure_map 単位で異なる function_parameters キーを指定可能。
        relation / hub スコープは structure_map の state_policy 経由で参照する設計。

### Optional Analytics

- [ ] context clustering — session prefix の k-means クラスタリング
- [ ] cluster labeling — human / LLM によるクラスタラベル付け（LLM は naming のみ）
- [ ] drift / spike detection — Bollinger band による推薦傾向変化検知
- [ ] `context_drift_signal` への monitoring output 実装
- [ ] large-scale nearest search optimization — HNSW / ivfflat 等の近似最近傍対応

### Personalization

- [ ] user opt-in personalization UI — 個人ビューの opt-in / access-control UI
