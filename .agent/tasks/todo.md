# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。
完了済み作業・PR修正履歴・旧方針の残骸は残さない。

## Context Route Recommendation Runtime

Context Route Recommendation v1 は main に統合済み。
以下は v1 以降の未実装項目のみ。

### Persistence / Repository

- [ ] `ContextRouteRepository` を in-memory skeleton から実 DB クエリへ置換する
- [ ] `TopologyRepository.LoadFunctionParameterAsync` を `function_parameters` 実読みに置換する
- [ ] `context-token-registry` API を 501 から実 DB 接続へ置換する
- [ ] `context-token-registry` の deprecate エンドポイントを実装する

### Cache / Aggregation Pipeline

- [ ] `UpsertEventVectorCacheAsync` / `UpsertPrefixVectorCacheAsync` の deferred batch 実装
- [ ] `context_transition_stats` の near-realtime 集計パイプライン実装
- [ ] `context_event` の hot/cold archive retention policy を定義する

### Policy Scope

- [ ] Context Route Recommendation の `policy_ref` を `structure_maps.state_policy` から解決できるようにする
- [ ] `default_policy` 固定参照を structure_map / relation / hub scoped policy に拡張する

### Optional Analytics

- [ ] context clustering — session prefix の k-means クラスタリング
- [ ] cluster labeling — human / LLM によるクラスタラベル付け（LLM は naming のみ）
- [ ] drift / spike detection — Bollinger band による推薦傾向変化検知
- [ ] `context_drift_signal` への monitoring output 実装
- [ ] large-scale nearest search optimization — HNSW / ivfflat 等の近似最近傍対応

### Personalization

- [ ] user opt-in personalization UI — 個人ビューの opt-in / access-control UI
