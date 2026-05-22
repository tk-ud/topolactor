# Context Route Recommendation — 設計思想と取り扱い方針

対応 YAML: `context-route-recommendation.yaml`
対応実装: `db/context_route_tables.sql`,
         `backend/runtime/ContextRouteRecommendationResolver.cs`

---

## 概念

離散トークン + 追記専用ログだけを使う軽量コサイン類似度推論エンジン。

ユーザーの操作列（session prefix）をスパースベクトルとして集計し、
過去の類似セッションを近傍検索して「次の操作」「次に必要な情報」を推薦する。

ニューラル訓練不要。学習は純粋な集計（バッチ or near-realtime）。

## Registry Tensor Principle（SSOT）

registry table は単なる辞書/設定/metadata ではなく、topology vocabulary の semantic matrix（tensor basis / vector basis）として扱う。
registry table は topology vocabulary の semantic matrix（tensor basis / vector basis）として扱う。
row は registryId、column は semantic axis / projection axis / wiring axis、value は weight / state / relation / coordinate / connection の観測値である。

同一テーブル内の count/sum/average/recency/frequency/transition 集計値は、意味本体ではなく attention weight の観測値として扱う。

## SQL Attention Logs SSOT との境界

この文書は context route recommendation の SSOT であり、SQL Attention Logs SSOT 本体ではない。
SQL Attention logs の canonical 親定義は以下を参照する。

- `docs/design/sql-attention-logs-ssot.md`
- `docs/design/sql-attention-logs-ssot.yaml`

境界ルール:
- `context_event` は SQL Attention の `logs.ui_operation` 相当になり得る **conditional signal source**
- `context_hub_recommendation_current` は topology-internal discrete recommendation の child current
- `context_hub_recommendation_current` は SQL Attention の `logs.current` ではない
- `context_hub_recommendation_current` は SQL Attention 本体探索ではない
- context route recommendation は Main Attention Route ではない
- context route recommendation は SQL Attention evidence / statistics / EMA / feedback を使い得る child projection / consumer

---

## Real/Sys Table Principle and logs.diffs

実部/sys テーブルの基本形は `id / state / jsonb` とする。
- `id` = identity
- `state` = current state
- `jsonb` = 潜在/半構造特徴保持面

実部/sys テーブルは registry tensor そのものではなく、registry tensor から観測・接続される実体面である。
jsonb key は観測頻度・意味重要度・監査要件に応じて column 化し、attention/audit/projection の観測軸として表出できる。

`logs.diffs` は append-only diff surface（基本形: `id / tableId / jsonb / created`）として扱う。
これは監査・再構築・履歴面であり、現在状態 SoT の代替ではない。



## Vector Cache Clarification

registry_id 群を参照する record は Tensor state として扱う。
record は手入力 vector を所有しない。
record の tensor coordinate は registry_id 参照、relation binding、jsonb/promoted column、logs/observations から導出される。
`vector_sparse` / `l2_norm` は SoT ではなく、その Tensor state から再生成可能な materialized projection cache である。
seed / UI / API から vector cache を直接 authoring する導線は drift/GAP として扱う。

### context_token_registry.value の取り扱い方針

`context_token_registry.value` は人間が設定する表示用参照値であり、推薦計算には使用しない。
推薦エンジンは token ID の **存在（multi-hot: 1.0）** を topology observation signal として使用する。
token.value を sparse vector の重みとして使用する実装は旧導線であり、drift/GAP として扱う。

### Multi-Hot Topology Observation

event vector は token_ids (UUID[]) の multi-hot として構成する:
- token 存在 → 1.0
- token 不在 → 0.0（sparse 表現から省略）

この multi-hot vector の SUM が prefix vector になる（低計算コスト・統計的安定性は維持）。
近傍検索は multi-hot cosine（= intersection_count / sqrt(|a| × |b|)）を使用し、
これは **neighborhood filter**（Θ）として扱う。cosine 自体は意味 SoT ではない。

### DB CHECK 制約と policy 可変値の分離方針（A4 fix）

`context_hub_recommendation_current` / `context_hub_feedback_event` の各フィールドについて:

| フィールド | 制約戦略 | 根拠 |
|---|---|---|
| `scope_limit` | `CHECK (scope_limit > 0)` のみ | policy-defined（`hub_attention.scope_limits`）が列挙権限。DDL 移行不要で policy 拡張可能 |
| `candidate_kind` | `CHECK (IN ('registry','hub','entity','relation','operation','token'))` | topology vocabulary。新 kind 追加時は DDL 移行が必要 |
| `feedback_kind` | `CHECK (IN ('selected','ignored','missing_candidate'))` | topology vocabulary。新 kind 追加時は DDL 移行が必要 |

**scope_limit** は function_parameters（`topology_vector_runtime.hub_attention.scope_limits`）が
列挙値の権限を持つ。DB は正値ガード（`> 0`）のみを担い、policy が変更されても DDL 移行は不要。

**candidate_kind / feedback_kind** は topology vocabulary として CHECK 制約で保護する。
新しい kind を追加する場合は DDL 移行が必要であり、これは意図的な設計決定である。
vocabulary 拡張は topology schema 変更と同等の重みを持つため、軽率な拡張を防ぐ。


## 推薦の二軸

| セクション | 推薦内容 | 方法 |
|---|---|---|
| Section 2 | 次アクション候補 | 遷移統計ベースライン + 近傍投票のブレンド |
| Section 1 | 参考情報（次トークン候補） | 近傍セッションの delta トークン投票 |

---

## SSOTの構造

```
context_token_registry   ← トークン辞書（Hub Registry）— /admin/context-token-registry で管理
function_parameters      ← 推薦エンジンチューニングパラメータ（topology データストア）
context_event            ← 唯一の必須ログ（追記専用）
context_prefix_vector_cache ← 近傍検索用プレフィックスベクトルキャッシュ
context_transition_stats ← 遷移確率集計（prob01 = count_hits / SUM(count_hits) over same scope）
```

推薦エンジンのポリシーは独立した設定テーブルではなく、既存 topology の
`function_parameters` テーブルに格納される（`function_name = 'context_route_recommendation_resolve'`,
`parameter_key = 'default_policy'`）。これは context route recommendation が
topolactor topology の **optional capability** であることを示す。

---

## ベクトル設計

トークン値は人間が離散的に設定（ニューラル最適化なし）。
推奨範囲 `[-1.0, 1.0]`。コサイン計算に互換な範囲であれば間隔は不均一でよい。

セッションベクトル = `SUM(v_event)` over events in prefix。
SUM を採用する理由: 低計算コスト + 統計的安定性。

---

## チューニングパラメータ（function_parameters SSOT）

Runtime コードへの直書き禁止。すべて `function_parameters` テーブルから読む。
`function_name = 'context_route_recommendation_resolve'`, `parameter_key = 'default_policy'`
に JSON ブロブとして格納される。

| パラメータ | シード値 | 意味 |
|---|---|---|
| `min_similarity` | 0.05 | 近傍候補の最小コサイン類似度 |
| `top_k` | 50 | 取得するプレフィックス候補数の上限 |
| `min_neighbors` | 10 | 推薦を出力するための最小近傍数 |
| `recent_days` | 90 | 履歴ウィンドウ（日数） |
| `max_candidates_shown` | 5 | 出力候補数の上限 |
| `baseline_weight` | 0.5 | 遷移統計ベースラインの重み |
| `neighbor_weight` | 0.5 | 近傍投票の重み |

管理UI: なし（topology データストア直接操作）

---

## status の扱い

```
Ok                  — 候補あり
InsufficientHistory — 履歴不足（エラーではない; cold start で想定内）
ExplicitError       — リゾルバー内部エラー（policy missing 含む）
```

silent fallback は存在しない。status は常に明示。

## Runtime 失敗時の fail-close 方針

Runtime 観測・統計・Attention 更新の失敗はすべて ExplicitError として返す。
LogError のみで処理を継続し recommendation result を返す経路は禁止。

| 失敗箇所 | ExplicitError code |
|---|---|
| AppendContextEventAsync | `CONTEXT_EVENT_APPEND_FAILED` |
| GetTransitionStatsAsync / GetWindowedTransitionStatsAsync | `TRANSITION_STATS_QUERY_FAILED` |
| RunTopologyVectorRuntimeExtensionAsync (hub attention 更新含む) | `TVR_EXTENSION_FAILED` |

観測ログ・統計根拠・Attention 更新の整合が取れた場合のみ recommendation result を返す。
部分成功・空統計フォールバック・観測されていない成功は禁止。

---

## 取り扱い方針

### やってよいこと
- `context_token_registry` の value 範囲は `[-1.0, 1.0]` 内で人間が設定
- `function_parameters` の policy JSON を直接更新（デプロイ不要）
- キャッシュは rebuildable として扱う（再構築可能、削除しても回復できる）
- Bollinger band drift/spike 検出は optional — v1 では不要

### やってはいけないこと
- Runtime コード（ContextRouteRecommendationResolver / NpgsqlContextRouteRepository）に数値定数を直書きする
  → `function_parameters` 経由で読むこと。smoothing α/β 等も直書き禁止
- production fallback を C# コードに持たせる
  → policy-missing は `ExplicitError(CONTEXT_ROUTE_POLICY_NOT_FOUND)` を返す。silent fallback 禁止
- malformed な `structure_maps.state_policy` を `default_policy` に silent fallback させる
  → JSON パースエラーは `ExplicitError(CONTEXT_ROUTE_STATE_POLICY_INVALID)` を返す
- `context_route_policy_ref` に空文字を設定して通過させる
  → 空 ref は `ExplicitError(CONTEXT_ROUTE_POLICY_REF_INVALID)` を返す
- DB unavailable または policy 未登録の状態でデフォルト値で継続する
  → policy-missing は `ExplicitError(CONTEXT_ROUTE_POLICY_NOT_FOUND)` を返す
- context route recommendation 専用の独立した設定テーブルを作る
  → policy は topology の `function_parameters` に統合する
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
- Policy は `TopologyRepository.LoadFunctionParameterAsync` 経由で読む

---

## Runtime Excitation Trigger との接続

Context route recommendation の log retention は、
`docs/design/runtime-excitation-and-package-dispatch.md` に定義する
**cron trigger → abstract delete package** パターンで処理する。

```text
cron trigger
→ trigger context { operation: "log:retention:cleanup" }
→ Runtime excitation
→ resolve Manifest: select abstract delete package
→ retention policy check (function_parameters: 対象 log 種別・期間・有効/無効)
→ execute: context_event / context_prefix_vector_cache の cleanup
→ update audit result
```

context route recommendation の retention policy（対象 log 種別・retention 期間・有効/無効）は
`function_parameters` テーブルに格納する。
cron 側に retention 期間や対象 log 種別を直書きしない。

---

## クラスタリング（optional）

月次 k-means でセッションをクラスタリング。
外部 LLM はクラスタ名の提案のみに使用（Runtime ロジックには不使用）。

---

## Topology Vector Runtime との関係

Context route recommendation は lightweight topology observation / recommendation 導線であり、
`context_event` / `context_prefix_vector_cache` / `context_transition_stats` /
`context_hub_recommendation_current` / `context_hub_feedback_event` を使って離散推薦を返す。

この導線は SQL Attention 親定義を再定義しない。SQL Attention 親意味は
`docs/design/sql-attention-logs-ssot.md` / `docs/design/sql-attention-logs-ssot.yaml` を参照する。

no silent fallback を維持し、policy は `function_parameters` から解決し、
失敗は `ExplicitError` として返す。

---

## Registrar-wide Topology Attention

This section describes registrar-side topology attention as a child/future projection route.
It does not define SQL Attention parent semantics, Main Attention Route, or Phase Attention quaternion semantics.
SQL Attention parent semantics are owned by `docs/design/sql-attention-logs-ssot.md` / `docs/design/sql-attention-logs-ssot.yaml`.

### スコープ定義

Topology Attention は screen transition や operation recommendation に限定されない。

`enum transition attention` は一部でしかない。
Registrar 全体を Key-Value Memory として扱い、
運用圧の高い table / record / diff activity を Query として、
registry / relation / schema / package / component / state / structure_map 全体へ Attention する。

### Query 源泉

Attention の Query は、以下のような運用圧シグナルから生成できる:

```text
record_count が多い table
edit_diff_count が多い table
recent_diff_rate が高い table
state_transition が多い table
re-edit / rollback / correction が多い table
現在操作中の hub / operation / record / state / table
```

これらのシグナルは「現場が意味圧をかけている場所」を示す。
運用圧の高い場所が Topology Attention の Query 候補になる。

### Key Space と Value Space

```text
Key:
  master_registry
  relation_registry
  schema_registry
  package_registry
  component_registry
  state_registry
  structure_maps
  hub_relations

Value:
  次に参照すべき topology node
  補完すべき registry
  分割すべき table
  追加すべき state
  接続すべき relation
  推薦すべき operation
```

Registrar は named lookup table ではなく、topology-wide Key-Value Memory として機能する。

Registry ID arrays がその sparse vector basis になる。

### State Transition Log と Edit Diff Log の分離

```text
state transition log  = phase change observation source
edit diff log         = value change observation source / Query generation source
```

両者は意味境界が異なる。混在させない。

現時点では edit diff log (`topology_edit_log` / `entity_edit_log`) は未作成である。
edit diff activity から Query を生成する機能は **TODO** である。
実装済みのように扱わない。

将来の `topology_edit_log` / `entity_edit_log` が Query generation source になる。

### やってはいけないこと

- screen transition / operation recommendation に閉じた説明で Topology Attention を定義する
- state transition log と edit diff log を同一テーブルに混在させる
- edit diff log が未作成のまま「edit diff から Query を生成している」と記述する
- Registrar を static lookup table として扱う（Key-Value Memory として扱う）
