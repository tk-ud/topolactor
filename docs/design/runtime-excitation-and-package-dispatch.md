# Runtime Excitation Trigger と Package Dispatch — 設計思想と取り扱い方針

---

## 概念

Runtime は **passive な executor** である。Runtime は自律的に処理を選ぶ常駐主体ではなく、
励起トリガによって呼び起こされる実行体として設計する。

```text
Runtime is passive.
Runtime is awakened by an excitation trigger.
The trigger does not contain domain-specific logic.
The trigger provides context.
Runtime resolves topology.
Topology selects package.
The selected package runtime executes concrete operation.
```

---

## Excitation Trigger の分類

| Trigger 種別 | 発生源 | 役割 |
|---|---|---|
| `cron` | 定期スケジューラ | 定期発注トリガ。OS cron / scheduled worker が発行する |
| `webhook` | 外部システム | 外部受注トリガ。受信した外部イベントを context として Runtime に渡す |
| `event` | UI / ユーザー操作 | UI / 操作トリガ。frontend 操作または内部操作イベントが発行する |

**Trigger 層は context を Runtime に渡すだけである。**
Trigger 層に domain-specific logic を置いてはならない。
package 選択・validation・routing・retention の判断はすべて Runtime が行う。

---

## External API Runtime の位置づけ

External API Runtime は excitation trigger ではない。

```text
External API Runtime は、励起後に topology 側で選択される package / runtime である。
```

| 区分 | 説明 |
|---|---|
| Excitation trigger | cron / webhook / event の三種。Runtime を起動する |
| External API Runtime | topology が package として選択する runtime。REST API / gRPC / message queue 等の外部呼び出しを実装する package runtime として扱う |

External API integration を trigger として設計すると、
trigger 種別ごとに別 runtime architecture が増殖するリスクがある。これを禁止する。

---

## Package Dispatch フロー

```text
excitation trigger
→ trigger context
→ Runtime excitation
→ resolve Manifest / Registry / function_parameters / structure_map policy
→ select package
→ execute selected package runtime
→ validate / policy check
→ update state / log / recommendation / audit result
```

このフローは canonical runtime route（`stored_topology_data → … → emission_or_projection`）の
**前段プロセス**として接続する。

Trigger は canonical route を bypass しない。
Trigger は canonical route を開始するための context を Runtime に与える役割のみを持つ。

---

## Policy Surface — hardcode 禁止項目

以下の値は Runtime コードに直書きしてはならない。
すべて Registry / Manifest / `function_parameters` / `structure_map policy` などの
policy surface から解決する。

| 項目 | 説明 |
|---|---|
| package selection | どの package を選択するか |
| enabled state | trigger / package / operation が有効かどうか |
| frequency | cron interval / retry interval |
| retry | 失敗時のリトライ回数・間隔 |
| validation | 実行前後の validation 条件 |
| routing | trigger context をどの package / operation に routing するか |
| retention | log / audit result の保持期間・対象 |

policy surface が未登録の場合は、production fallback 定数で継続してはならない。
`missing-policy` として明示的にエラーを返す。

---

## 明示 Status

Trigger や package dispatch の結果は、次の明示 status を返す。
silent fallback は存在しない。

```text
Ok                   — 正常完了
Disabled             — trigger または package が無効化されている
MissingPolicy        — policy / Manifest / Registry が未登録
MalformedPolicy      — policy データが破損または JSON パース不可
ValidationFailed     — 実行前後の validation チェック不合格
ExternalError        — 外部 API / 外部システムからのエラー
```

`Disabled` は silent に処理を skip してはならない。
policy で明示的に無効化された場合も status として記録する。

---

## Runtime Log Retention との接続

Runtime Log Retention（`.agent/tasks/todo.md` 参照）は、
**cron trigger が abstract delete package を選択する例**として本設計に接続する。

```text
cron trigger (定期実行)
→ trigger context { operation: "log:retention:cleanup" }
→ Runtime excitation
→ resolve Manifest: select abstract delete package
→ execute delete / anonymize / aggregate-and-promote
→ retention policy check (対象 log 種別・期間・on/off)
→ update audit result / log
```

この例における policy surface:
- `function_parameters` — 対象 log 種別・retention 期間・有効/無効フラグ
- `Manifest` — abstract delete package のバインド定義
- `structure_map policy` — cleanup operation の routing 定義

cron trigger は「いつ実行するか」という context を Runtime に渡すだけである。
「何を削除するか」「いつまでのログか」「有効かどうか」は topology データが決める。

cleanup が無効（disabled）の場合も `Disabled` status を返し、silent skip は禁止する。

---

## 取り扱い方針

### やってよいこと
- cron / webhook / event を context 生成のみに使用する
- External API Runtime を topology 側の package として登録し、Runtime 経由で選択させる
- package selection / enabled / frequency / retry / routing / retention を policy surface で管理する
- trigger の実行結果を audit log として記録する
- `Disabled` を明示 status として扱い、policy 変更で動的に有効/無効を切り替える

### やってはいけないこと
- cron / webhook / event receiver に domain-specific logic を直書きする
- External API integration を trigger として扱う（trigger 種別ごとに runtime が分岐しない）
- package selection / enabled state / frequency / retry / routing / retention を Runtime コードに hardcode する
- policy missing / malformed policy のとき production fallback 定数で継続する（`MissingPolicy` / `MalformedPolicy` を返す）
- `Disabled` を silent skip にする（`Disabled` status を記録する）
- `ExternalError` を飲み込んで正常完了として扱う
- trigger 種別ごとに別 runtime architecture を作る（trigger の違いは context の違いであり、Runtime アーキテクチャは共通）

---

## topolactor Runtime との接続

package dispatch は canonical route の前段として接続する:

```text
excitation trigger
→ trigger context
→ Runtime excitation              ← 本 SSOT のスコープ
→ stored_topology_data
→ user_operation / trigger operation
→ operation_vector
→ attractor_resolve
→ structure_map_resolve
→ package_resolve                 ← package dispatch の主体
→ schema_resolve
→ component_expand
→ emission_or_projection
```

- Trigger layer: context 生成のみ。domain-specific logic なし
- Runtime layer: Manifest / Registry / function_parameters / structure_map を解決し package を選択
- Package runtime layer: 選択された package（External API / delete / emit など）を実行
