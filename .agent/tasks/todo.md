# Agent Task List

未処理は **bundle 単位**で実装・レビューする。完了済みは末尾アーカイブ。

## 未処理 bundle 索引

| Bundle ID | 名称 | 件数 | 主 SSOT |
|-----------|------|------|---------|
| `ui-topology-package-bucket-vector` | Step 4 package = bucket_item_id ベクトル | 6 | `admin-console-workflow-ssot.yaml` step 4.1 / `framework-policy.yaml` `ui_topology_tensor_persistence` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |

---

## 共通参照（全 admin bundle）

**方針（owner）:** SSOT 準拠でよい。設計意図に反する語彙・UI の残存は危険。収束は**反意図の削除・置換**を優先。

| パス | 読む節 |
|------|--------|
| `docs/design/admin-console-workflow-ssot.yaml` | v0.7.2 — `canonical_sequential_authoring_pipeline`, `edit_target_contract`, `admin_contents`, `admin_ui_builder` |
| `docs/design/runtime-orchestration-ssot.yaml` | `frontend_routes.admin` |
| `docs/framework-policy.yaml` | `ui_topology_tensor_persistence` |
| `docs/design/db-schema.yaml` | `manifest`, `packages`, `components_layout_design`, `components_style_design`, `ui_component_bucket` |
| `docs/design/component-catalog-classification-ssot.yaml` | catalog / registration |
| `docs/design/css-dictionary-ssot.yaml` | component design トークン |
| `docs/registrar-admin-ui-specification.md` | **従属**（主正本にしない） |

**実装サーフェス:** `frontend/routes/admin/*`, `ContentsAdmin.tsx`, `ContentsScreenDesignPanel.tsx`, `ContentsPromotionPanel.tsx`, `UiBuilderAdmin.tsx`, `UiBuilderFlowStepper.tsx`, `adminGuides.ts`, `adminUxTerms.ts`, `adminUxGuard.test.ts`, `adminMainFlow.test.ts`

---

## Bundle `ui-topology-package-bucket-vector`

**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`（step 4.1 — `component_id set` on one package）、`docs/framework-policy.yaml`（`ui_topology_tensor_persistence`）、`docs/design/db-schema.yaml`（`ui_component_package` / `ui_package_component_map` / `ui_topology_tensor`）、`articles/ui-topology-tensor.md`（relation `child_ids[]` = package_ids）

**現状ギャップ（2026-06 調査）:** `PromoteBucketItemAsync` が **部品 1 件 → package 1 件**（`package_key = {routeKey}:{componentKey}:pkg`）。Step 4.1 複数選択は promote をループし **package が散在**。package に **`bucket_item_id[]`（部品の使われ方ベクトル）** が載らず、Step 4.2 の編集ルートと SSOT の「1 画面 1 package + component_id set」が一致しない。

**あるべきモデル:**

- `route_key` あたり **1 package**
- package 正本: **`bucket_item_ids[]`**（staging 由来）→ promote 後 **`component_ids[]`**（`ui_package_component_map` + 任意 `order_index`）
- **`ui_topology_tensor` 1 行**（その package の layout / wiring）
- hub / relation 側は **`child_ids[]` = package_id[]**（使われ方の比較はベクトル化可能）

**実装サーフェス:** `backend/repository/NpgsqlUiTopologyRepository.cs`、`backend/runtime/AdminRuntime.cs`、`PackageGeneratorRuntime.cs`、`backend/schema/UiTopologyContracts.cs`、`frontend/islands/UiBuilderAdmin.tsx`、`frontend/tests/*`（package generate / admin flow）

**UX 暫定対応済み（本 bundle の本修正ではない）:** 4.1 ルート入力のメイン露出、4.1 後の `list_packages` 再読込・自動選択、4.2 layout 候補フォールバック — データモデル修正後に再確認すること。

- [ ] **設計固定:** `package_schema_json.bucketItemIds`（または同等）と 1 route = 1 `package_key`（例 `{routeKey}:pkg`）を SSOT / db-schema に明記
- [ ] **Backend:** `package_generator:promote_package`（`routeKey` + `bucketItemIds[]`）— 1 トランザクションで 1 package・複数 `ui_package_component_map`・1 tensor；既存単体 promote は互換または内部委譲
- [ ] **Backend:** `generate` を全 `bucketItemIds` に対して実行してから promote（`packaging` 前提の整合）
- [ ] **API 応答:** `PackageGenerateResponseDto` に `bucketItemIds` / `componentIds`（配列）を返し、`list_packages` / `promoted_palette` がメンバー集合を投影
- [ ] **Frontend:** Step 4.1 複数選択 → **1 回の submit**（ループ promote 廃止）；Step 4.2 は返却 `packageId` 1 件を編集ルートに固定
- [ ] **検証:** 統合テスト（2 bucket → 1 package、map 2 行、tensor 1 行）+ `adminUxGuard` / package generate テスト更新

---

## Bundle `future-external-bundle-gate`

**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion/Sheets/Slack/GitHub/Webhook/REST-API-Connector/NoCode-Loop — 個別 SSOT 揃うまで実装しない

---

## Bundle `helper-manual`

**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

- [ ] helper/manual category 候補の実装設計
- [ ] Desktop AI / CLI / MCP Reader 向けライティング方針
- [ ] ヘルプコンポーネント実装（SSOT カテゴリ構造ゲート）

---

## Bundle `product-nocode-loop-acceptance`

- [ ] `product.dynamic_support_nocode_loop` 手動受入（roadmap 追従）

---

## 完了済みアーカイブ

### `admin-v072-audit-followup`（2026-06）

- [x] `audit-component-design-ui` — PackageDesignPanel: classname / tailwind / cssTokenRefs / reactionIntent + upsert payload
- [x] `audit-contents-step-payload` — `contentsAssign.ts` step 専用 payload（existing から非ステップ項目を保持）
- [x] `audit-ui-builder-aux-tabs` — catalog / CI を `<details>` 参照専用化、編集ルートバナー常設
- [x] `audit-layout-patch-package-gate` — `layout_patch` に `packageId`、tensor 所属検証、apply WHERE に package 固定
- [x] `audit-docs-v072-sync` — `adminGuides` / `UiBuilderFlowStepper` v0.7.2 文言同期

### `admin-v072-convergence`（2026-06 — plan WU1–WU5 + 続き）

- [x] `admin-ux-feedback` — `adminSubmitUx.ts` / `AdminSubmitStatus`、confirm・loading・status 統一
- [x] `admin-contents-v072` — Contents pipeline stepper 1/2/2.5/3、multi-op、operationEntityBindings、legacy promote を details へ
- [x] `admin-ui-builder-v072` — FlowStepper 4.1/4.2、複数選択パッケージ化、package スコープ layout、wiring 編集 API/UI、`component_style_design` dispatch（design 通常 UI の深度は `admin-v072-audit-followup` へ）
- [x] `admin-guides-regression` — `adminGuides.ts` / `ADMIN_MAIN_FLOW_STEPS` / `ADMIN_ROUTE_CARDS` v0.7.2、ux guard・mainFlow テスト拡張
- [x] `HubNavigationAdmin` — create/update/delete confirm

### `admin-blocking-verify`（merge 時確認）

- [x] `/admin/*` ルート registry — `runtime-orchestration-ssot.yaml`
- [x] contents / manifests 責務分割
- [x] contents promote guard fail-close
- [x] `table_ref` SSOT wiring
- [x] hub navigation on `/admin/manifests`
- [x] `ManifestScreenOperationDeriver` manifest-scoped axes

### `frontend.admin_routes`（旧 roadmap bundle — 実装済みだが v0.7.2 と乖離あり）

- [x] contents wizard 前半・列型 select・初期データ・relation・search・集計 UI
- [x] backend `screen_data_shape` 拡張・`WIRING_TABLE_REF_NOT_FOUND`
- [x] ui-builder catalog/CSS/wiring/apply/CI（**v0.7.2 package ルート・bucket ベクトルは `ui-topology-package-bucket-vector` bundle**）
- [x] UX 語彙・ContentsPromotionPanel ステップ表示

### `admin_visual_layout_builder`

- [x] layoutId round-trip・responsive token UI・tests

### `cli-mcp-port-ssot` / `core-runtime-bundle-ssot`

- [x] CLI/MCP 実装 SSOT・Email/Stripe/File/Export bundle SSOT
- [x] Webhook/Job/Audit/Secret bundle SSOT

### `legacy-debug-isolation`

- [x] `/dev/admin/*` wrapper 削除

### `sql-attention-m7`

- [x] SQLA-IDSPACE-STEP3/4・SQLA-GENERATION-STEP4
