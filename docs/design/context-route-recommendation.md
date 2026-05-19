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

## Topology Vector Runtime

### 概念

registry / hub / relation / entity が保持する ID 配列を sparse vector として扱い、
SQL cosine 近傍検索を runtime / validation / recommendation に統合する拡張。

外部 embedding / pgvector を必要としない。
PostgreSQL の `UUID[]`, `JSONB`, GIN index, relation weight, transition stats を利用する
data-defined topology runtime 拡張。

### SQL Attention

SQL Attention は Topology Vector Runtime の説明ラベルであり、別 Runtime 名ではない。
PostgreSQL 上の `UUID[]` / `JSONB` / GIN index / relation weight / transition stats / EMA trend を使い、
source of truth の業務データではなく recommendation current / projection 用の materialized signal として扱う。
外部 embedding / pgvector / learned neural weights / neural attention 実装は必須ではない。

```text
Query  = current hub / operation / runtime context
Key    = registry sparse vector / relation / hub / entity / token
Value  = candidate hub / registry / operation / token
Score  = cosine + relation weight + transition/co-occurrence + EMA trend/cross + feedback
Output = ranked recommendation current / projection input
```

### 設計定義

```text
Topology Attention:
  遷移に効いている重要 Key を抽出する

Transition Key Evidence:
  どの table / relation / state / entity が遷移 Key になっているかを説明する

Topology MLP:
  抽出された Key 群を組み合わせ、次状態 / 次候補 score へ変換する
  初期実装は feature crossing + weighted score transform
  neural network 実装ではない

Feedback Weight Update:
  推薦結果と実際の選択差分から、統計重みを補正する
  selected → 重み強化
  ignored  → 重み弱化
  missing candidate → 欠落特徴を補正
```

「BP」という名称は初期実装では使わない。
explainable statistical feedback として成立させ、将来の gradient / backprop 相当の拡張余地だけ残す。

### Registry Sparse Vector 定義

```text
UUID[] = multi-hot sparse vector
UUID + weight = weighted sparse vector
```

対象:
- `relation_registry.master_ids`
- `entities.relation_ids`
- `structure_maps.component_ids`
- `hub_relations.relation_registry_id + weight`

注意:
- registry table 自体は辞書 / 基底定義
- ID 配列を保持する row が vector を持つ topology node
- 文字列 label / name は検索補助であり、意味近傍の主軸ではない
- 既存の GIN index は候補集合の粗探索に利用する

### SQL Cosine Neighbor Search

multi-hot UUID 配列に対する SQL cosine:

```text
cosine = intersection_count / sqrt(cardinality(a) * cardinality(b))
```

weighted vector の場合:

```text
cosine = dot(a, b) / sqrt(norm(a) * norm(b))
```

要件:
- 空 vector / zero norm は explicit result として扱う（silent fallback 禁止）
- candidate search は GIN overlap で粗探索してから cosine score を計算する
- threshold / top_k / blocking 条件は function_parameters から読む
- Runtime コードへ magic number を直書きしない

### Registry Vector Validation

Registrar の Draft → Validate → Promote flow に vector neighbor validation を追加する。
既存の duplicate key check は維持する。

追加する validation class:

| class | cosine 範囲 | 扱い |
|---|---|---|
| `duplicate_vector` | >= duplicate_threshold | blocking |
| `near_duplicate_vector` | >= near_duplicate_threshold | blocking or confirm-required |
| `related_existing_registry` | >= related_threshold | warning |
| `pass` | < related_threshold | 通過 |
| `zero_vector` | zero norm | explicit validation result |
| `explicit_error` | DB unavailable / infrastructure error | **blocking（fail-closed）** |

threshold は function_parameters (topology_vector_runtime.registry_validation) から読む。

DB unavailable は fail-closed: `ValidationClass.ExplicitError` + `IsBlocking:true` を返す。
`Pass` + `IsBlocking:false` （fail-open）は禁止。

要件:
- UI が独自判定しない（backend structured validation result を projection するだけ）
- Backend が structured validation result を返す
- broken refs / malformed ids / DB unavailable は silent fallback しない → ExplicitError + blocking

### Hub Attention Recommendation

hub 同士を static relation と統計 recommendation 両方で attention できるようにする。

hub identity 要件:
- `context_hub_recommendation_current` への書き込みは、dispatch に `IdOrHubId` が提供されている場合のみ実行する。
- `IdOrHubId` なしの dispatch（null）は hub attention current write をスキップする。sessionId を hubId の代替に使わない。
- hub attention は hub-entity-scoped であり、session-scoped ではない。

```text
current hub = Query
candidate hub / relation / entity vector = Key
hub attention weight = static_relation_weight
                     + cosine_similarity
                     + statistical_weight
                     + mlp_feature_score
                     + feedback_adjustment
attended hub = Value
```

信号:
- 静的接続: `hub_relations.weight`
- 意味近傍: registry sparse vector cosine
- 統計接続: co-occurrence / transition stats / recommendation current
- 短期 trend: EMA fast (alpha from policy)
- 長期基準: EMA slow (alpha from policy)
- 転換点: cross_up / cross_down / none

current は正本ではなく rebuildable materialized current として扱う。

`why this hub?` に対して `evidence_json` に relation/cosine/stat/EMA/cross の根拠を返す。

### Topology MLP

```text
feature crossing 例:
  relation_id × state_id
  table_id × operation
  hub_id × recent_trend
  cosine_similarity × EMA cross
  relation_id × table_id × selected_operation
```

max_feature_cross_order は function_parameters (topology_vector_runtime.topology_mlp) から読む。
feature crossing の根拠は `mlp_feature_json` に保存する。

### Feedback Weight Update

```text
推薦した → ユーザーが選んだ   → positive_delta 加算
推薦した → 無視された         → negative_delta 加算
推薦しなかった → 選ばれた     → missing_candidate_delta 加算
```

delta 値は function_parameters (topology_vector_runtime.feedback_weight_update) から読む。
feedback は context_hub_feedback_event (append-only) にも記録する。
aggregate current は再構築可能にする。

feedback の適用粒度は `(hub_id, target_table, candidate_kind, candidate_id, scope_limit)` — context_hub_recommendation_current の PK 全体と一致する。

`target_table` を含めない場合、同一 candidate_id が複数の target_table に存在すると feedback が誤って複数の target_table に適用される。HubFeedbackEvent と context_hub_feedback_event の両方が `target_table` を持つことで、この誤適用を防ぐ。

### Policy ストレージ

topology_vector_runtime の policy は独立した設定テーブルではなく、
既存の `function_parameters` に統合する:

```
function_name = 'context_route_recommendation_resolve'
parameter_key = 'default_policy'
```

JSON 内の `topology_vector_runtime` サブオブジェクトとして格納する。

```json
{
  "topology_vector_runtime": {
    "enabled": true,
    "registry_validation": {
      "enabled": true,
      "duplicate_threshold": 1.0,
      "near_duplicate_threshold": 0.85,
      "related_threshold": 0.60,
      "top_k": 10
    },
    "hub_attention": {
      "enabled": true,
      "scope_limits": [1000, 3000, 10000],
      "ema_fast_alpha": 0.30,
      "ema_slow_alpha": 0.10,
      "max_update_candidates_per_event": 10000
    },
    "transition_key_evidence": {
      "enabled": true,
      "operation_contribution": 1.0,
      "relation_contribution": 0.8,
      "state_contribution": 0.7,
      "table_contribution": 0.6,
      "neighbor_top_k": 3
    },
    "topology_mlp": {
      "enabled": true,
      "max_feature_cross_order": 3
    },
    "feedback_weight_update": {
      "enabled": true,
      "positive_delta": 0.05,
      "negative_delta": -0.02,
      "missing_candidate_delta": 0.03
    }
  }
}
```

policy missing → ExplicitError("TOPOLOGY_VECTOR_RUNTIME_POLICY_NOT_FOUND")
enabled=false → explicit disabled result（silent fallback 禁止）
policy invalid → ExplicitError("TOPOLOGY_VECTOR_RUNTIME_POLICY_INVALID")

### 意味境界

Frontend:
- cosine 判定しない
- topology 判定しない
- MLP feature crossing 判定しない
- feedback weight update 判定しない
- backend structured result / evidence を projection するだけ

Backend:
- structured validation result / recommendation evidence を返す

DB:
- topology definition / current / append-only event を保持する
- `context_hub_recommendation_current` = rebuildable materialized current
- `context_hub_feedback_event` = append-only event

### やってはいけないこと

- Runtime コードに threshold / alpha / delta / limit の magic number を直書きする
- topology_vector_runtime 専用の独立した設定テーブルを作る
- enabled=false を silent に skip する
- policy missing / invalid で production fallback する
- Frontend に cosine / topology / MLP 判定を持たせる
- `context_hub_recommendation_current` を正本として扱う

---

## Registrar-wide Topology Attention

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
