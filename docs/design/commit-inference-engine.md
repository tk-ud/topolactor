# Commit Inference Engine — 設計思想と取り扱い方針

対応 YAML: `commit-inference-engine.yaml`

---

## 概念

コミット型二段階推論エンジン。オフラインファースト、CPU のみ、追記専用ログ設計。

ユーザーが **S1（アイテム選択）→ S2（解決コード選択）** の順に確定操作を行うことで、
`operation_log`（ヘッダー）と `operation_log_item`（正規化ファクト）への書き込みが確定する。

この確定ログが後続の機械学習（キャッシュ再構築）と推薦の唯一のSSOT。

---

## 三つの推論軸

| 軸 | ステージ | 入力 | 出力 |
|---|---|---|---|
| Language (S1) | トークン類似度 | input_tokens | アイテム候補 |
| Time (S3) | ライフサイクル経過 | lifecycle_metric + 交換履歴 | 交換推奨アイテム |
| Physics (S2) | アイテムセット共起 | confirmed_items | 解決コード候補 |

S1 は Language + Time の合流（S3 の結果は S1 出力にマージされる）。

---

## SSOT の境界

```
session_s1 / session_s1_item_event   ← UI 操作の監査証跡（コミット前）
          ↓ S1 commit
session_s2 / session_s2_resolution_event  ← UI 操作の監査証跡（コミット前）
          ↓ S2 commit
operation_log          ← コミット済みSSOT（ヘッダー）
operation_log_item     ← コミット済みSSOT（正規化ファクト）← S3 の唯一の正解ソース
operation_log_note     ← 手動フリーテキスト（SSOT に上書きしない）
```

**S3 の正解ソースは `operation_log_item`（`event_type='replaced'`）のみ。**
`operation_log.item_keys` は表示用デノーマライズであり、正確性クエリには使用禁止。

---

## キャッシュ設計方針

| キャッシュ | キー | 目的 |
|---|---|---|
| `cache_items_by_token_sig` | `(model, token_sig)` | S1 Language 高速化 |
| `cache_resolutions_by_context` | `(model, items_sig)` | S2 高速化。**tokens_sig はキーに含めない**（爆発回避） |
| `cache_item_lifecycle_stats` | `(model, item_key, category_scope)` | S3 母集団統計 |

### cache_resolutions_by_context のキー設計

`items_sig` のみをキーにする。`tokens_sig` をキーに加えると：
- (model × items_sig × tokens_sig) の直積でキー空間が爆発
- キャッシュヒット率が激減

tokens は最終スコアへの **弱い加算項**（`0.1 * token_cooccur_score`）としてのみ使用。

---

## 信頼性ゲート（S3）

ライフサイクル統計は母集団が小さいと推薦が不安定になる。

```
use_only_if: subjects_n >= 3 AND intervals_n >= 3
else: time_alert を非表示（"データ不足"として扱う）
```

初回交換履歴がない個体への time_alert は **null_policy = no_alert**（人間判断に委ねる）。

---

## 取り扱い方針

### やってよいこと
- キャッシュ再構築は `operation_log_item` から Batch/Lazy で実行
- `resolution_master` へのバインドは S2 commit 後に強制（free text で上書きしない）
- `session_*` テーブルはコミット前の監査証跡として保持（削除禁止）

### やってはいけないこと
- `operation_log.item_keys`（デノーマライズ列）を S3 計算に使う
- `tokens_sig` を `cache_resolutions_by_context` のキーに含める
- キャッシュ未ヒット時に no_alert を返さずに推薦を出す（信頼性ゲート必須）
- `operation_log_note.manual_text` で `resolution_master.content` を上書きする

---

## topolactor との対応

このエンジンは topolactor の **relation_registry スコープのログドメイン** として実装される。

| 抽象コンセプト | topolactor 実装 |
|---|---|
| `subject_id` | hub.hub_id（対象エンティティ） |
| `model` | relation_registry 上の分類 |
| `resolution_master` | master_registry エントリ |
| `operation_log` | relation_registry スコープのログテーブル |
| キャッシュ再構築 | batch/background_service |
