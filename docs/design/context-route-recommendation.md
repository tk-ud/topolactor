# Context Route Recommendation — 設計思想と取り扱い方針

対応 YAML: `context-route-recommendation.yaml`
対応実装: `db/context_route_tables.sql`, `db/context_route_config.sql`,
         `backend/runtime/ContextRouteRecommendationResolver.cs`

---

## 概念

離散トークン + 追記専用ログだけを使う軽量コサイン類似度推論エンジン。

ユーザーの操作列（session prefix）をスパースベクトルとして集計し、
過去の類似セッションを近傍検索して「次の操作」「次に必要な情報」を推薦する。

ニューラル訓練不要。学習は純粋な集計（バッチ or near-realtime）。

---

## 推薦の二軸

| セクション | 推薦内容 | 方法 |
|---|---|---|
| Section 2 | 次アクション候補 | 遷移統計ベースライン + 近傍投票のブレンド |
| Section 1 | 参考情報（次トークン候補） | 近傍セッションの delta トークン投票 |

---

## SSOTの構造

```
context_token_registry   ← トークン辞書（Hub Registry）— /admin/context-token-registry で管理
context_route_config     ← チューニングパラメータ SSOT — /admin/context-route-config で管理
context_event            ← 唯一の必須ログ（追記専用）
context_prefix_vector_cache ← 近傍検索用プレフィックスベクトルキャッシュ
context_transition_stats ← 遷移確率集計（Bayesian smoothing α=1, β=10）
```

---

## ベクトル設計

トークン値は人間が離散的に設定（ニューラル最適化なし）。
推奨範囲 `[-1.0, 1.0]`。コサイン計算に互換な範囲であれば間隔は不均一でよい。

セッションベクトル = `SUM(v_event)` over events in prefix。
SUM を採用する理由: 低計算コスト + 統計的安定性。

---

## チューニングパラメータ（ContextRouteConfig SSOT）

Runtime コードへの直書き禁止。すべて `context_route_config` テーブルから読む。

| パラメータ | デフォルト | 意味 |
|---|---|---|
| `min_similarity` | 0.05 | 近傍候補の最小コサイン類似度 |
| `top_k` | 50 | 取得するプレフィックス候補数の上限 |
| `min_neighbors` | 10 | 推薦を出力するための最小近傍数 |
| `recent_days` | 90 | 履歴ウィンドウ（日数） |
| `max_candidates_shown` | 5 | 出力候補数の上限 |
| `baseline_weight` | 0.5 | 遷移統計ベースラインの重み |
| `neighbor_weight` | 0.5 | 近傍投票の重み |

管理UI: `/admin/context-route-config`

---

## status の扱い

```
Ok                  — 候補あり
InsufficientHistory — 履歴不足（エラーではない; cold start で想定内）
ExplicitError       — リゾルバー内部エラー
```

silent fallback は存在しない。status は常に明示。

---

## 取り扱い方針

### やってよいこと
- `context_token_registry` の value 範囲は `[-1.0, 1.0]` 内で人間が設定
- `context_route_config` の値を admin UI から変更（デプロイ不要）
- キャッシュは rebuildable として扱う（再構築可能、削除しても回復できる）
- Bollinger band drift/spike 検出は optional — v1 では不要

### やってはいけないこと
- Runtime コード（ContextRouteRecommendationResolver）に数値定数を直書きする
  → `ContextRouteConfig` 経由で読むこと
- `context_token_registry.group` をベクトル計算に使う
  → group は UI グルーピング専用。計算対象は `value` のみ
- セッションの explicit end を想定したロジックを書く
  → セッションは implicit に終了する（no explicit termination）
- 個人別ビューをデフォルト表示にする
  → GLOBAL/role がデフォルト。個人別は opt-in かつアクセス制御必須

---

## topolactor Runtime との接続

canonical route への挿入位置:

```
...
→ component_expand
→ context_route_recommendation_resolve   ← Step 9
→ emission_or_projection                 ← Step 10
```

- Runtime 層に業務固有語彙（maintenance / parts / work_code 等）は一切混入しない
- `ContextRouteRecommendationResult` は純粋なデータレコード（計算メソッドなし）
- Frontend は受け取ったデータを projection するだけ（cosine 計算は Backend のみ）

---

## クラスタリング（optional）

月次 k-means でセッションをクラスタリング。
外部 LLM はクラスタ名の提案のみに使用（Runtime ロジックには不使用）。
