# Agent Task List

未処理は **bundle 単位**で実装・レビューする。完了済みは末尾アーカイブ。

## 未処理 bundle 索引

| Bundle ID | 名称 | 件数 | 主 SSOT |
|-----------|------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |
| `ssot-old-vocabulary-cleanup` | SSOT旧語彙 / 不要 production surface cleanup | 4 | `docs/design/runtime-orchestration-ssot.yaml` / `docs/design/sql-attention-logs-ssot.yaml` |
| `main-data-wiring-ssot-audit` | main データ配線 SSOT 監査 follow-up | 1 | `docs/design/db-schema.yaml` / `docs/design/runtime-orchestration-ssot.yaml` |
| `ui-builder-layout-design-boundary` | UI Builder layout/design 責務分離 | 1 | `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/db-schema.yaml` |
| `admin-main-flow-step-ssot-alignment` | admin top step 表示のSSOT整合 | 1 | `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/runtime-orchestration-ssot.yaml` |

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

## Bundle `ssot-old-vocabulary-cleanup`

**SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/sql-attention-logs-ssot.yaml`, `docs/framework-policy.yaml`

**実行前:** AGENTS.md を読む。

- [ ] `dispatcher_legacy_alias: dispatchar` を削除し、必要なら migration / negative-check に限定して旧語彙を隔離
- [ ] `LegacyChangeIntake*` / `legacy_mirror` / `legacy_system` を SSOT 準拠の existing-system / external-intake 語彙へ収束、または design SSOT に明示し、`/intake/legacy-change` の role は JWT claim 由来に固定して body 由来 role を manifest axis に使わない
- [ ] `HubAttractorExplorationRuntime.ExploreLegacyHubCurrentSupportCacheDiagnosticsAsync` と related legacy support-cache cosine diagnostics / compatibility payload を削除または test-only / negative-check に隔離し、canonical scheduler route が `hubs.hub_relations` 探索のみであることをテスト更新
- [ ] `runtime_jump_event_contract` の `from` / `to` 形と実装の `PastAddress` / `CurrentAddress` / `PlannedAddress` 形を突合し、SSOT・DTO・テストの canonical jump event shape を一本化

---

## Bundle `admin-main-flow-step-ssot-alignment`

**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`

**実行前:** AGENTS.md を読む。

- [ ] `/admin` トップの `ADMIN_MAIN_FLOW_STEPS` / `AdminMainFlowStepper` / `adminMainFlow.test.ts` を canonical admin workflow の step 番号へ再整合する
  - 問題: SSOT の canonical admin routes は `/admin`, `/admin/contents`, `/admin/ui-builder`, `/admin/manifests` で、`/auth` は利用前提であって admin authoring pipeline の Step 1 ではない。一方、現状は `ADMIN_MAIN_FLOW_STEPS` と `UX_MAIN_FLOW_STEP_LABELS` が「ログイン」を Step 1 として表示・テスト固定し、`/admin/contents` を Step 2 にずらしている。
  - 目的: `/admin/contents` の step 1/2/2.5/3、`/admin/ui-builder` の Step 4、`/admin/manifests` の post-pipeline を SSOT 通りに投影し、frontend の step 表示が DB/manifest topology authoring pipeline の意味境界を壊さないようにする。
  - 改善方針: ログインは prerequisite/注意文に隔離し、`ADMIN_MAIN_FLOW_STEPS` は canonical admin workflow のみを表す。必要なら contents step を subSteps で 1/2/2.5/3 表示し、ui-builder は Step 4、manifests は post-pipeline として扱う。`adminMainFlow.test.ts` は `/auth` を canonical main flow に含めない assertion に更新する。
  - 対象ファイル: `frontend/content/adminGuides.ts`, `frontend/content/adminUxTerms.ts`, `frontend/islands/AdminMainFlowStepper.tsx`, `frontend/routes/admin/index.tsx`, `frontend/tests/adminMainFlow.test.ts`。
  - 完了条件: `/auth` が canonical admin workflow step として表示/テスト固定されず、SSOT の step 1/2/2.5/3/4/post-pipeline 境界と admin route registry が一致する。

---

---

## 完了済みアーカイブ


### `main-data-wiring-ssot-audit`（2026-06）

- [x] `NpgsqlTopologyRepository` の demo transition 永続化参照を canonical `topology.demo_state_transitions` に統一
## Bundle `ui-builder-layout-design-boundary`

**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/framework-policy.yaml`, `docs/design/css-dictionary-ssot.yaml`

**実行前:** AGENTS.md を読む。

- [ ] `/admin/ui-builder` の layout/design 編集境界を SSOT 通りに再分離する
  - 問題: SSOT は layout を「UI 配置場所（canvas placement / slots / order / responsive layout class refs）」、component_design を「色・形・styling tokens・reaction presentation intent」と分けるが、`frontend/islands/UiBuilderAdmin.tsx` は `layout` タブ内で `PackageDesignPanel` と `LayoutBuilderSection` を同列表示し、さらに `layout_patch` 経路で `cssTokenRefs` / `responsiveTokenRefs` を `components_layout_design` / `ui_topology_tensor` へ保存している。これにより色・余白・トークン設定が layout 編集の保存責務に混入して見える。
  - 目的: データ駆動OSの正本である DB authority に合わせ、layout はフレーム配置図、component_design は色・形・プロパティ設定として UI と保存経路を明確に分離する。
  - 改善方針: `UiBuilderAdmin.tsx` の通常導線を `package selection` → `layout child editor` → `component design child editor` の明示サブセクションに分け、layout_patch は配置・slot・order・layout class refs に限定する。色・余白・フォント・CSS token refs・reactionIntent は `component_style_design:upsert` / `components_style_design` 側へ寄せる。既存 DB カラムに互換保存が必要な場合は advanced/debug か migration TODO として明示し、通常 UI で layout と design を混同させない。
  - 対象ファイル: `frontend/islands/UiBuilderAdmin.tsx`, `backend/runtime/AdminRuntime.cs`, `backend/repository/NpgsqlUiTopologyRepository.cs`, `backend/tests/Topolactor.Runtime.Tests/AdminRuntimeLayoutPatchTests.cs`, frontend UI builder tests。
  - 完了条件: 通常 UI で layout は配置図編集のみ、design は色等プロパティ編集のみになり、layout_patch request/DB write と component_style_design upsert の責務境界をテストで固定する。

---

## 完了済みアーカイブ

### `ui-topology-package-bucket-vector`（2026-06）

- [x] `package_schema_json.bucketItemIds` と 1 route = 1 `package_key`（`{routeKey}:pkg`）を契約固定
- [x] `package_generator:promote_package`（`routeKey` + `bucketItemIds[]`）— 1 トランザクションで 1 package・複数 `ui_package_component_map`・1 tensor
- [x] `generate` を全 `bucketItemIds` に対して実行してから promote（`packaging` 前提の整合）
- [x] `PackageGenerateBatchResponseDto` に `bucketItemIds[]` / `componentIds[]` 配列、`list_packages` が component_ids / bucket_item_ids を投影
- [x] Step 4.1 複数選択 → 1 回の `promote_package` submit（ループ promote 廃止）；Step 4.2 は返却 `packageId` 1 件を編集ルートに固定
- [x] 統合テスト（2 bucket → 1 package、map 2 行、tensor 1 行）+ package generate テスト更新
- [x] `package_schema_json` vector 上書き禁止 — 既存配列と今回 batch を union/dedup（後追い追加テスト付き）
- [x] `ui_package_component_map` 重複防止 — `slot_key='default'` canonical + `NULLS NOT DISTINCT` 制約（migration 追加）
- [x] mixed selection 状態表示 — promote 件数と skip 件数を分けて表示

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