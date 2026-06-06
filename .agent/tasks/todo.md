# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `layout-application-projection-continuity` | layout 投影 continuity | partial | 4 | `docs/design/pipeline-continuity-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |

---

## Bundle `layout-application-projection-continuity`

**Status:** partial
**SSOT:** `docs/design/pipeline-continuity-ssot.yaml`

### 完了済み（PR #367 + このコミット）

- [x] `structure_maps.layout_id` カラム追加（bootstrap DDL + migration FK）
- [x] LayoutId identity plumbing: DB → StructureMapRecord → RuntimeWorkingShape → Emission → frontend Emission type
- [x] `LayoutNode` 型追加（backend Contracts.cs + frontend dispatch.ts）
- [x] `LayoutNodeRecord` + `LoadLayoutNodesAsync` 追加（TopologyRepository + NpgsqlTopologyRepository）
- [x] `StructureMapResolver`: layout_id 設定時に ui_topology_tensor から tensor rows をロードし LayoutNodes を構成
- [x] `layoutId` 設定だが tensor rows ゼロ → `LAYOUT_NODES_NOT_FOUND` ValidationError（silent fallback なし）
- [x] `EmissionBuilder`: LayoutNodes を Emission に転送
- [x] `renderEmission()`: layoutNodes 存在時は tensor slot 順で ComponentSpec を構成
- [x] `renderEmission()`: layoutId 設定だが layoutNodes なし → 明示的 error spec
- [x] `ComponentSpec`: slotKey / orderIndex フィールド追加
- [x] `/` を production application projection shell に変更（UserDemoStepper を除去）
- [x] `/demo` から UserDemoStepper を除去（draft preview 未実装として明示）
- [x] Backend tests: LayoutNodes pipeline、LAYOUT_NODES_NOT_FOUND エラーパス
- [x] Frontend tests: layout ordering が projection structure を変えることを検証

### 残タスク

- [ ] `/demo` 本格実装: admin-authored layout セレクター + content_entity_drafts ドラフトコンテンツセレクター + 選択 layout への draft 投影プレビュー（content_entity_drafts API endpoint が必要）
- [ ] production integration test: real tensor rows → LayoutNodes → renderEmission ordering 変化の E2E 検証

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
