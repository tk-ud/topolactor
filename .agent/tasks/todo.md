# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `runtime-ui-interaction-wiring` | UI Builder runtime 操作配線 正規化 | partial | 1 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
| `runtime-pipeline-scenario-harness-policy` | runtime pipeline scenario harness policy | not_started | 1 | `product.admin_topology_authoring` | `docs/design/pipeline-continuity-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

---

## Bundle `runtime-pipeline-scenario-harness-policy`

**Status:** not_started  
**Roadmap bundle:** `product.admin_topology_authoring`  
**SSOT:** `docs/design/pipeline-continuity-ssot.yaml`  
**Supporting SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`

**問題点:** Admin UI Builder runtime 操作配線は、`buildVisualLayoutPatchJson` / `parseVisualLayoutPatchJson` / `draftPreviewResultToEmission` / `renderEmission` / `mergeNodeLocalProps` / `parseEventBinding` / `emitBoundEvent` / backend runtime・repository・SSE 系の単体または lane 検証に分かれており、代表 fixture で product runtime を一周させる scenario harness が不足している。

**目的:** 全APIへ full loop を無条件に課さず、runtime UI interaction・新規dispatch・DB→projection→frontend 系の変更だけに Tier 2 scenario harness を必須化する。

**改善方針:** 既存 test helper / lane test を組み合わせ、`button click → modal open → modal close` を最小代表scenarioとして定義する。backend は action/runtime 分類ごとの代表 fixture を置き、CRUD同型runtimeは代表 fixture + registration/static/vocabulary check に留める。backend `ok:true` のみ、frontend render 単体のみ、event log append のみでは completion pass としない。

**対応資料:** `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`, `.agent/tests/check-pipeline-continuity.sh`

**対象ファイル名:** `frontend/tests/visualLayoutBuilder.test.ts`, `frontend/tests/draftPreviewToEmission.test.ts`, `frontend/tests/adminWiringExecutionLane.test.ts`, `frontend/tests/runtimeComponentFactory.test.ts`, `frontend/tests/renderEmissionPropBindings.test.ts`, `frontend/tests/defaultEntitySearch.test.ts`, `frontend/runtime/visualLayoutUtils.ts`, `frontend/runtime/draftPreviewToEmission.ts`, `frontend/runtime/renderEmission.ts`, `frontend/runtime/runtimeComponentFactory.ts`, `frontend/runtime/frontendScheduler.ts`, `backend/endpoint/DispatchEndpoint.cs`, `backend/scheduler/RuntimeTimelineScheduler.cs`, `backend/runtime/ManifestDispatcher.cs`, `backend/runtime/RuntimeExecutor.cs`, `backend/runtime/EmissionBuilder.cs`, backend repository / runtime / SSE tests

**対象関数名:** `buildVisualLayoutPatchJson`, `parseVisualLayoutPatchJson`, `draftPreviewResultToEmission`, `renderEmission`, `mergeNodeLocalProps`, `resolvePropBindings`, `parseEventBinding`, `emitBoundEvent`, `enqueueRuntimeComponentCommand`, backend dispatch/runtime/repository/projection refresh handlers

**test tier policy:** Tier 0 は syntax/static、Tier 1 は lane unit/boundary、Tier 2 は runtime/action class representative scenario harness、Tier 3 は release/manual/full product acceptance のみ。

**「全APIにfull loopを課さない」適用条件:** CRUD等の同型APIは代表fixture + route/action/manifest registration + SSOT vocabulary/static check でよい。APIごとの差分が独自DB副作用・独自projection・独自SSE・独自frontend state を持つ場合は追加scenarioを要求する。

**representative fixture方針:** UI Builder runtime interaction は `button click → modal open/close` を最小fixtureにし、layout patch serialization、emission restore、`renderEmission`、runtimeSpec event binding、projection-local state store、final props/DOM state を assert する。backend/DB/SSE は `frontend test payload → /api/dispatch → backend runtime → DB state changed → projection/SSE or refetch → frontend receive/render final assertion` を action分類ごとの代表fixtureにする。

- [ ] 既存 helper を再利用した Tier 2 scenario harness を実装し、runtime UI interaction と backend DB/projection/SSE 系の代表 fixture を追加する

---
## Bundle `runtime-ui-interaction-wiring`

**Status:** partial  
**Roadmap bundle:** `product.admin_topology_authoring`  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`  
**Supporting SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`

**問題点:** UI Builder の runtime 操作配線（例: button click → modal/drawer/dialog open/close/toggle）が、通常導線ではなく上級・実験的な `propsJson.eventWirings` 手入力として露出している。`stateJson.open` は初期状態注入に留まり、`propsJson.eventWirings` が authoring → `layout_patch_json` → backend validate/apply → DB persistence → emission → `renderEmission` → `runtimeComponentFactory` → runtime UI state 反映まで一貫して実行される pipeline 証跡が不足している。

**目的:** modal / drawer / dialog / tabs / accordion 等の runtime UI state 操作を、ノーコード通常導線の必須イベント設定として正規化し、保存・復元・投影・本番実行・preview inert 差分までを bundle 単位で閉じる。

**改善方針:** `propsJson.eventWirings` は raw fallback / legacy 互換に降格し、正規の runtime UI interaction contract を `layout_patch_json` node contract または `state_policy_json` / wiring contract としてSSOT定義する。通常UXには「操作 / イベント」設定を追加し、eventType / actionType(open, close, toggle, setState, navigate, dispatch) / targetNodeId / targetStatePath / value を構造化入力させる。backend validate は target node / state path / unsupported action を blocking error にし、runtime は projection-local state store で対象 node props を更新する。既存の route navigation と backend runtimeDispatch は維持し、local UI state mutation と責務分離する。

**対応資料:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`, `.agent/tasks/todo.md`

**対象ファイル名:** `frontend/islands/UiBuilderAdmin.tsx`, `frontend/runtime/visualLayoutUtils.ts`, `frontend/runtime/renderEmission.ts`, `frontend/runtime/runtimeComponentAdapter.ts`, `frontend/runtime/runtimeComponentFactory.ts`, `frontend/runtime/frontendScheduler.ts`, `frontend/components/Modal.tsx`, `frontend/components/ApplyConfirmDialog.tsx`, `frontend/components/RowDetailDrawer.tsx`, `frontend/tests/visualLayoutBuilder.test.ts`, `frontend/tests/runtimeComponentFactory.test.ts`, `frontend/tests/draftPreviewToEmission.test.ts`, `db/ui_topology_tables.sql`, backend layout patch validate/apply/emission surfaces

**対象関数名:** `buildVisualLayoutPatchJson`, `parseVisualLayoutPatchJson`, `mergeNodeLocalProps`, `buildCatalogComponentEventBinding`, `buildRouteNavigationEventBinding`, `adaptComponentDataHub`, `renderRuntimeComponent`, `emitBoundEvent`, `enqueueRuntimeComponentCommand`, `Modal`, `RowDetailDrawer`, `LayoutPatchApplyModal` / `ApplyConfirmDialog` related handlers

- [ ] UI Builder runtime 操作配線を通常UXへ昇格し、authoring → persistence → emission → render → runtime state mutation → modal/drawer/dialog open/close までを正規 pipeline として実装・テストする

---

## Bundle `future-external-bundle-gate`

**Status:** not_started
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion / Google Sheets / Slack / GitHub Issues / generic webhooks / external REST API connectors は、個別 SSOT と connector adapter contract が揃うまで optional external surface として実装しない（CSV/JSON admin import と M6 self-hosted no-code loop とは別 bundle）

---

## Bundle `helper-manual`

**Status:** not_started
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

SSOT 上、helper/manual category candidates は実装ではなく方針整理。site page / UI component / help screen component 実装は explicitly out of scope。

- [ ] helper/manual category candidates を user promise / safety boundary / onboarding policy として整理する（ページ・コンポーネント実装はしない）
- [ ] Desktop AI / CLI / MCP Reader 向けに、plain business language と approval boundary のライティング方針を整理する

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
