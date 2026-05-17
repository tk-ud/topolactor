# Agent Task List — Remaining TODO

## Context Route Recommendation Runtime (Issue #17) — v1 以降

以下は v1 スコープ外として意図的に延期した項目。
実装時は `context_route_tables.sql` の optional テーブル群（isolated section）を使用すること。

- [ ] context clustering — k-means による session prefix クラスタリング
- [ ] cluster labeling — human/LLM によるクラスタラベル付け（external API は naming のみ）
- [ ] drift / spike detection — Bollinger band による推薦傾向変化検知
- [ ] Bollinger band monitoring — `context_drift_signal` テーブルへの出力
- [ ] user opt-in personalization UI — 個人ビューの opt-in / access-control UI
- [ ] archive / retention policy — `context_event` hot/cold 分離（90日 hot → cold archive）
- [ ] large-scale nearest search optimization — HNSW / ivfflat 等の近似最近傍対応
- [ ] ContextRouteRepository 実装 — in-memory skeleton から実 DB クエリへの置換
- [ ] TopologyRepository.LoadFunctionParameterAsync 実装 — `function_parameters` テーブルを実際に読む（現在 in-memory skeleton）
- [ ] context-token-registry API エンドポイント実装 — 501 → 実 DB 接続
- [ ] UpsertEventVectorCacheAsync / UpsertPrefixVectorCacheAsync の deferred batch 実装
- [ ] context_transition_stats の near-realtime 集計パイプライン
- [ ] tag_scope 対応の cache_part_lifecycle_stats 相当（context 版）
- [ ] Context Route Recommendation の policy_ref を `structure_maps.state_policy` から解決できるようにする
      （現在は `PolicyFunctionName = "context_route_recommendation_resolve"` 固定参照）
- [ ] default_policy 固定参照を将来、structure_map / relation / hub scoped policy に拡張する
      （function_name と parameter_key を呼び出し元の context から決定できるようにする）

---

このファイルは agent task surface として使用する。
新しい issue または follow-up が開かれた場合のみ追記する。
