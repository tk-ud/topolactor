# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。
完了済み作業・PR修正履歴・旧方針の残骸は残さない。

## Context Route Recommendation Runtime

Context Route Recommendation v1 は main に統合済み。
以下は v1 以降の未実装項目のみ。

### Persistence / Repository

- [ ] `context-token-registry` API を 501 から実 DB 接続へ置換する
      → GET/POST の実 DB 接続は未実装。
        Deno postgres クライアントの追加またはバックエンド AdminEndpoint 経由での実装が必要。
- [ ] `context-token-registry` の deprecate エンドポイントを実 DB 接続へ置換する
      → `frontend/routes/api/admin/context-token-registry/[tokenId]/deprecate.ts` は定義済み（501）。
        UUID バリデーション済み。実 DB 更新は GET/POST と同様の方針で実装予定。

### Cache / Aggregation Pipeline

- [ ] `context_event` の retention job を実装する
      → `function_parameters` の `context_event_retention / retention_policy` からポリシーを読み込み、
        hot_days / cold_days / archive_strategy / batch_size に従って古いイベントを削除またはアーカイブ。

### Policy Scope

- [ ] `context_transition_stats` に coverage / confidence threshold の仕組みを導入する
      → prob01 が信頼できる最小サンプル数（min_events_for_stats）を function_parameters から読む。
        不十分なサンプルの行は推薦に使用しないか、信頼度を下げて出力する。

### Optional Analytics

- [ ] context clustering — session prefix の k-means クラスタリング
- [ ] cluster labeling — human / LLM によるクラスタラベル付け（LLM は naming のみ）
- [ ] drift / spike detection — Bollinger band による推薦傾向変化検知
- [ ] `context_drift_signal` への monitoring output 実装
- [ ] large-scale nearest search optimization — HNSW / ivfflat 等の近似最近傍対応

### Personalization

- [ ] user opt-in personalization UI — 個人ビューの opt-in / access-control UI
