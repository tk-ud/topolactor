# Agent Task List

---
未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `owner-step3-contents-ux` | Step 3 オーナー UX（集計・サンプル・型・pro） | not_started | 5 | `docs/design/admin-console-workflow-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |

---

## Bundle `owner-step3-contents-ux`

**Status:** not_started  
**起点:** オーナー comments（admin/contents step 3）  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`（step 3 `search_aggregation_display`）

### 調査メモ（実装根拠）

- 集計式 UI は `ContentsAggregationMeasuresEditor` — 行ごとに `{列, 関数}` のみ。別 SQL／別テーブル由来の列の横断評価・リモート未解決列は弱い。
- サンプル表示は全操作種別の union のみ。操作種別セレクトなし。
- 日付型: `contentDataConformance.ts` 未検証。`between` は `Number()` 比較。
- UUID: seed demo UUID が RFC 厳格 regex 不合格。手入力 `<input>` のまま。
- プロ向け欄: `searchTargets` は列名リスト（`,` `;` 改行）。`aggregationSpec` はプレビュー未使用。案内なし。

### タスク

- [ ] **複数 SQL 由来の集計式**: 別 logical table / リモート manifest 列を含む複数 `aggregationMeasures` 行を追加・評価できるようにする
- [ ] **サンプル表示に操作種別セレクト**: 操作種別ごとに `entityTargetColumns` を切り替えてプレビュー
- [ ] **日付型の検証と範囲検索**: `date` / `timestamp with time zone` の入力検証と検索 `between` の日付比較
- [ ] **UUID 手入力廃止**: 参照ピッカー／生成／リレーション解決値の自動投入。regex と seed 形式の整合
- [ ] **プロ向け欄の案内**: `searchTargets` / `aggregationSpec` の期待形式・サンプルを UI に明示

---

## Bundle `future-external-bundle-gate`

**Status:** not_started  
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion/Sheets/Slack/GitHub/Webhook/REST-API-Connector/NoCode-Loop — 個別 SSOT 揃うまで実装しない

---

## Bundle `helper-manual`

**Status:** not_started  
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

- [ ] helper/manual category 候補の実装設計
- [ ] Desktop AI / CLI / MCP Reader 向けライティング方針
- [ ] ヘルプコンポーネント実装（SSOT カテゴリ構造ゲート）

---

## Bundle `product-nocode-loop-acceptance`

**Status:** not_started

- [ ] `product.dynamic_support_nocode_loop` 手動受入（roadmap 追従）
