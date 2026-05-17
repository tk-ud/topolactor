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
- [ ] ContextRouteConfigRepository 実装 — `LoadConfigAsync` が `context_route_config` テーブルを実際に読む
- [ ] context-route-config / context-token-registry API エンドポイント実装 — 501 → 実 DB 接続
- [ ] UpsertEventVectorCacheAsync / UpsertPrefixVectorCacheAsync の deferred batch 実装
- [ ] context_transition_stats の near-realtime 集計パイプライン
- [ ] tag_scope 対応の cache_part_lifecycle_stats 相当（context 版）

---

このファイルは agent task surface として使用する。
新しい issue または follow-up が開かれた場合のみ追記する。
