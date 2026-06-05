# Agent Task List

---
未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `owner-step3-contents-ux` | Step 3 オーナー UX（集計・サンプル・型・pro） | not_started | 5 | `docs/design/admin-console-workflow-ssot.yaml` |
| `owner-ui-builder-layout-preview` | UI Builder 配置プレビュー／canvas 欠落 | not_started | 3 | `docs/design/admin-console-workflow-ssot.yaml` step 4 / `docs/design/topology-layout-class-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |

---

## Bundle `owner-step3-contents-ux`

**Status:** not_started  
**起点:** オーナー comments（admin/contents step 3）  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`（step 3 `search_aggregation_display`）

### 調査メモ（実装根拠）

- 集計式 UI は `ContentsAggregationMeasuresEditor` — 行ごとに `{列, 関数}` のみ。複数行追加はできるが、**別 SQL／別テーブル由来の列をまたいだ式**や、リモート未解決列は候補に出ない／評価できない（`step3FieldSourceFromDesign` + `ScreenDataShapeQueryEvaluator` は in-memory 行の数値列前提）。
- サンプル表示 `SamplePreviewPanel` は `projectionColumnsFromOperationBindings` で**全選択操作種別の union**のみ。操作種別セレクトなし。
- 日付型: `contentDataConformance.ts` に `date` / `timestamp` 検証なし。`applySearchConditions` の `between` は `Number()` 比較のため日付範囲は機能しない。
- UUID: `validateFieldType` が RFC4122 厳格 regex（3 番目グループ `[1-5]`、4 番目 `[89ab]`）。**db seed の demo UUID**（例 `00000000-0000-0000-0000-0000000000a1`, `…0011`, `…0044`）は **すべて regex 不合格** → コピペでも琥珀警告。`ContentsDataEditor` は uuid 列を自由入力 `<input>` のまま。
- プロ向け欄: `searchTargets` は `parseSearchTargets` で **カンマ・セミコロン・改行**区切りの列名リスト（SQL ではない）。`aggregationSpec` は DB 保存のみで**サンプルプレビュー未使用**。UI に形式説明・サンプルなし。

### タスク

- [ ] **複数 SQL 由来の集計式**: 別 logical table / リモート manifest 列を含む複数 `aggregationMeasures` 行を追加・評価できるようにする（候補プール・初期データ・サンプルプレビュー・保存ペイロードの一貫性）
- [ ] **サンプル表示に操作種別セレクト**: 操作種別ごとに `entityTargetColumns` を切り替えてプレビュー（list / search / create 等で列集合が違うケースを確認できる UI）
- [ ] **日付型の検証と範囲検索**: `date` / `timestamp with time zone` の入力検証（または日付ピッカー）と、検索 `between`・サンプルフィルタの日付比較
- [ ] **UUID 手入力廃止**: uuid 列は手入力させない（既存 seed 互換の参照ピッカー／生成／リレーション解決値の自動投入）。検証 regex と seed 形式の整合も取る
- [ ] **プロ向け欄の案内**: `searchTargets` / `aggregationSpec` に期待形式を明示（pro なら SQL 入力であること、またはサンプル SQL／列名リスト例を UI に表示）。現状 `aggregationSpec` がプレビューに効かないならその旨も表示

---

## Bundle `owner-ui-builder-layout-preview`

**Status:** not_started  
**起点:** オーナー comments（ui builder）  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml` step 4.2 / `docs/registrar-admin-ui-specification.md` §5.6

### 調査メモ（実装根拠）

- `LayoutBuilderSection` の `draftNodes` は `useState<DraftNode[]>([])` で初期化。**DB `layout_patch_json` の読み込み API・hydrate 処理がフロントに存在しない**（書き込み `layout_patch:apply` のみ）。
- 期待フロー「form 入力 → draft preview → canvas 反映」に対し、配置タブには **ノードを積むフォーム／パレット追加以外の draft 入力面がなく**、パッケージ化直後も canvas は空のまま。
- 「1. プレビュー」(`callLayoutPatch("preview")`) は `LayoutPatchSummaryPanel` + debug JSON を出すだけで、**`draftNodes` や canvas DOM を更新しない**。`PreviewLayoutPatchAsync` も normalize のみ（投影結果を返さない）。画面が変わらないのは現状仕様どおりの欠落。
- `layoutClassRefs` は `tensorPatchJson` トップレベルに付与。canvas には外枠 `div` の class マージのみ（`canvasPreviewClass`）。**ノード単位の反映 UI なし**（`CanvasInspector` に class 割当なし）。アコーディオン `defaultOpen={false}` で設定欄も隠れやすい。

### タスク

- [ ] **layout draft の読み込みと canvas 初期表示**: パッケージ選択時に `ui_topology_tensor.layout_patch_json` を取得し `draftNodes` / `selectedLayoutClassRefs` に hydrate。パッケージ化直後の初期ノードも seed する
- [ ] **プレビューボタンで canvas に反映**: `layout_patch:preview` の正規化結果（またはローカル resolver 結果）を canvas／レイヤーツリーに即時反映。サマリパネルだけでは不十分
- [ ] **layout class ref の draft preview**: 選択した class を canvas 上の対象（レイアウト root／選択ノード wrapper）で視覚確認できるようにする。`allowed_for`（`layout_root` / `component_wrapper`）と UI の対応を明示

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
