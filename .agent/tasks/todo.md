# Agent Task List

未処理は **bundle 単位**で実装・レビューする。完了済みは末尾アーカイブ。

## 未処理 bundle 索引

| Bundle ID | 名称 | 件数 | 主 SSOT |
|-----------|------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |
| `user-login-seed-manifest-auth-boundary` | 通常ユーザログイン seed manifest / 認証境界 | 1 | `docs/design/runtime-orchestration-ssot.yaml` / auth SSOT（要追記） |
| `admin-relationship-active-manifest-targets` | Step 2.5 relationship 有効manifest参照 | 1 | `docs/design/admin-console-workflow-ssot.yaml` / `docs/design/db-schema.yaml` |

---

## 共通参照（完了 admin bundle の回帰時）

**方針（owner）:** SSOT 準拠でよい。設計意図に反する語彙・UI の残存は危険。収束は**反意図の削除・置換**を優先。

| パス | 読む節 |
|------|--------|
| `docs/design/admin-console-workflow-ssot.yaml` | v0.7.2 — `canonical_sequential_authoring_pipeline`, `edit_target_contract`, `admin_contents`, `admin_ui_builder` |
| `docs/design/runtime-orchestration-ssot.yaml` | `frontend_routes.admin` |
| `docs/framework-policy.yaml` | `ui_topology_tensor_persistence` |
| `docs/design/db-schema.yaml` | `manifest`, `packages`, `components_layout_design`, `components_style_design`, `ui_component_bucket` |

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

## Bundle `user-login-seed-manifest-auth-boundary`

**SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, auth/session/credential SSOT（要追記または既存正本特定）

**実行前:** AGENTS.md を読む。

- [ ] 通常ユーザ向けログイン UI を seed manifest として作成し、認証コアは既存 AuthService/Auth runtime に委譲する境界をSSOT化・実装する
  - 問題: admin 操作用ログインと通常ユーザ向けログインを topology manifest で扱う場合、JWT 署名・password hash・refresh token・admin 権限判定まで topology/hub/jsonb 側へ混入すると security boundary が崩れ、admin/user realm が混線する危険がある。
  - 目的: 通常ユーザ向けログイン画面はデータ駆動 UI として seed manifest で提供しつつ、credential 検証・password hash・JWT 署名・refresh token・session invalidation は既存ログイン基盤に隔離する。
  - 改善方針: まず auth/session/credential 境界のSSOTを追記または既存正本を特定し、`login UI topology manifest` と `AuthService/Auth runtime` の責務を分ける。seed は `/login` 等の user-facing manifest、入力フィールド、submit action binding、成功/失敗表示、遷移先だけを作る。submit は `auth_runtime.login` 等の既存認証 action に委譲し、claims には `realm=user`, `audience=user_app`, `scope/role=user` を付与する。admin は `realm=admin/system`, `audience=admin_console`, `scope/role=admin` として分離する。
  - 作らないもの: topology manifest 内の password_hash、JWT secret、token signing、refresh token 永続化、admin 判定ロジック、credential DB 直書き。
  - 対象ファイル候補: `docs/design/runtime-orchestration-ssot.yaml`, auth/session/credential SSOT（新規または既存）, seed 実装ファイル, auth runtime/API, frontend login manifest/rendering tests。
  - 完了条件: seed によって通常ユーザ向け login manifest が生成され、ログイン submit は既存認証基盤へ委譲される。admin/user realm・audience・scope が分離され、password hash/JWT secret/refresh token が topology/hub/jsonb に保存されないことをテストで固定する。

---

## Bundle `admin-relationship-active-manifest-targets`

**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/runtime-orchestration-ssot.yaml`

**実行前:** AGENTS.md を読む。

- [ ] Step 2.5 relationship の接続先として有効manifest/tableを選択可能にする
  - 問題: 現状の Step 2.5 relationship UI/データ配線が編集中 draft manifest 配下のテーブルだけを参照点として扱うと、編集中マニフェストから既存の有効マニフェスト上のテーブルへ接続する関係を作れない。データ駆動OSでは既存有効トポロジへの参照が閉じると、manifest間・既存データ間の連続性を表現できない。
  - 目的: 編集中 manifest の local side は draft 配下の logical tables のみを参照しつつ、remote/target side は published/active topology manifests とその table refs も選択できるようにする。
  - 改善方針: まず `docs/design/admin-console-workflow-ssot.yaml` の Step 2.5 relationship_configuration に、local side は current draft manifest scoped、remote side は current draft tables に加えて active/published manifests の table refs を選択可能、という境界を追記する。その後 frontend の relationship selector と backend intent validation/read model を更新し、remote target が active manifest table である場合も fail-close で解決・保存できるようにする。
  - 対象ファイル候補: `docs/design/admin-console-workflow-ssot.yaml`, `frontend/islands/ContentsScreenDesignPanel.tsx`, `frontend/lib/contentsAssign.ts`, `frontend/api/adminApi.ts`, `backend/runtime/AdminRuntime.cs`, `backend/repository/ManifestRepository.cs`, `backend/repository/NpgsqlManifestRepository.cs`, relationship / manifest management tests。
  - 完了条件: Step 2.5 UI で local side は編集中 manifest の配下テーブルに限定され、remote side は有効manifest/tableも選択できる。backend は remote target の manifest/table_ref を検証し、未解決時は silent fallback せず blocking error にする。テストで draft-only 固定への退行を防ぐ。

---

## 完了済みアーカイブ

### `admin-main-flow-step-ssot-alignment`（2026-06）

- [x] `/admin` トップの `ADMIN_MAIN_FLOW_STEPS` / `AdminMainFlowStepper` / `adminMainFlow.test.ts` を canonical admin workflow へ再整合（`/auth` 除外、contents subSteps 1/2/2.5/3）

### `admin-contents-data-input-import-subfeature`（2026-06）

- [x] `/admin/contents` step 3 データ入力に手入力 + `AdminImportPanel`（CSV/JSON preview→apply）を統合
- [x] `admin-console-workflow-ssot.yaml` に data_input_subfeature を明文化

### `ui-builder-layout-design-boundary`（2026-06）

- [x] `UiBuilderAdmin` layout / design タブ分離、`layout_patch` から cssTokenRefs 除去、backend strip + テスト

### `ssot-old-vocabulary-cleanup`（2026-06）

- [x] `dispatcher_legacy_alias: dispatchar` 削除（runtime-orchestration-ssot.yaml）
- [x] `ExistingSystemChangeIntake*` / `existing_system_intake` 語彙、JWT role claim 固定
- [x] `ExploreLegacyHubCurrentSupportCacheDiagnosticsAsync` 削除、テストは `ExploreAsync`（hubs.hub_relations）へ
- [x] `RuntimeJumpEvent` JSON `from`/`to`/`planned` 投影と SSOT 突合

### `main-data-wiring-ssot-audit`（2026-06）

- [x] `NpgsqlTopologyRepository` の demo transition 永続化参照を canonical `topology.demo_state_transitions` に統一

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

- [x] SQLA-IDSPACE-STEP3/4
