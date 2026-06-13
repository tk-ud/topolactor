# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `runtime-pipeline-scenario-harness-policy` | runtime pipeline scenario harness policy | partial | 2 | `product.admin_topology_authoring` | `docs/design/pipeline-continuity-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

---

## Bundle `runtime-pipeline-scenario-harness-policy`

**Status:** partial  
**Roadmap bundle:** `product.admin_topology_authoring`  
**SSOT:** `docs/design/pipeline-continuity-ssot.yaml`  
**Supporting SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`

**問題点:** Admin UI Builder runtime 操作配線の canonical frontend harness と backend validate/apply fail-close は追加済みだが、runtime-pipeline policy 全体としては backend DB/projection/SSE 系の representative fixture が未完了。加えて、通常表示コピーに内部語彙が残り `adminUxGuard` の normal view source guard が失敗している。

**目的:** 全APIへ full loop を無条件に課さず、runtime UI interaction・新規dispatch・DB→projection→frontend 系の変更だけに Tier 2 scenario harness を必須化する。同時に、通常画面では内部語彙を露出せず、技術語は詳細/上級者向け表示へ退避する。

**改善方針:** 既存 test helper / lane test を組み合わせ、frontend runtime UI interaction は canonical `runtimeInteractions` の `button click → modal open → modal close` 最小代表scenarioで維持する。backend は action/runtime 分類ごとの代表 fixture を置き、CRUD同型runtimeは代表 fixture + registration/static/vocabulary check に留める。backend `ok:true` のみ、frontend render 単体のみ、event log append のみでは completion pass としない。通常表示コピーは `NORMAL_VIEW_BANNED_TERMS` に従い、`runtime` / `layout_patch_json` / `propsJson` / `legacy fallback` 等を通常文言から除外する。

**対応資料:** `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`, `.agent/tests/check-pipeline-continuity.sh`, `frontend/tests/adminUxGuard.test.ts`

**対象ファイル名:** `frontend/tests/visualLayoutBuilder.test.ts`, `frontend/tests/draftPreviewToEmission.test.ts`, `frontend/tests/adminWiringExecutionLane.test.ts`, `frontend/tests/runtimeComponentFactory.test.ts`, `frontend/tests/renderEmissionPropBindings.test.ts`, `frontend/tests/defaultEntitySearch.test.ts`, `frontend/tests/adminUxGuard.test.ts`, `frontend/runtime/visualLayoutUtils.ts`, `frontend/runtime/draftPreviewToEmission.ts`, `frontend/runtime/renderEmission.ts`, `frontend/runtime/runtimeComponentFactory.ts`, `frontend/runtime/frontendScheduler.ts`, `frontend/islands/UiBuilderAdmin.tsx`, `frontend/content/adminUxTerms.ts`, `backend/endpoint/DispatchEndpoint.cs`, `backend/scheduler/RuntimeTimelineScheduler.cs`, `backend/runtime/ManifestDispatcher.cs`, `backend/runtime/RuntimeExecutor.cs`, `backend/runtime/EmissionBuilder.cs`, backend repository / runtime / SSE tests

**対象関数名:** `buildVisualLayoutPatchJson`, `parseVisualLayoutPatchJson`, `draftPreviewResultToEmission`, `renderEmission`, `mergeNodeLocalProps`, `resolvePropBindings`, `parseEventBinding`, `emitBoundEvent`, `enqueueRuntimeComponentCommand`, `PackageDesignPanel`, `extractNormalViewCopy`, `NORMAL_VIEW_BANNED_TERMS`, backend dispatch/runtime/repository/projection refresh handlers

**test tier policy:** Tier 0 は syntax/static、Tier 1 は lane unit/boundary、Tier 2 は runtime/action class representative scenario harness、Tier 3 は release/manual/full product acceptance のみ。

**「全APIにfull loopを課さない」適用条件:** CRUD等の同型APIは代表fixture + route/action/manifest registration + SSOT vocabulary/static check でよい。APIごとの差分が独自DB副作用・独自projection・独自SSE・独自frontend state を持つ場合は追加scenarioを要求する。

**representative fixture方針:** UI Builder runtime interaction は `frontend/tests/runtimeUiInteractionScenario.test.ts` の canonical `runtimeInteractions` `button click → modal open/close` 最小fixtureで、layout patch serialization、emission restore、`renderEmission`、runtimeSpec event binding、projection-local state store、preview inert 差分、final props state を assert 済み。backend validate/apply の invalid `targetNodeId` / `statePath` / `actionType` / target kind mismatch は `backend/tests/Topolactor.Runtime.Tests/NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs` で fail-close を固定済み。backend/DB/SSE は `frontend test payload → /api/dispatch → backend runtime → DB state changed → projection/SSE or refetch → frontend receive/render final assertion` を action分類ごとの代表fixtureにする。

- [x] 既存 helper を再利用した frontend runtime UI interaction Tier 2 scenario harness（canonical `runtimeInteractions` の `button click → modal open/close`）を追加する
- [x] backend validate/apply で runtime UI interaction の invalid `targetNodeId` / `statePath` / `actionType` / target kind mismatch を blocking error にする検証を追加する
- [ ] `frontend/islands/UiBuilderAdmin.tsx` の通常表示コピーから internal vocabulary を除去し、`frontend/tests/adminUxGuard.test.ts` の normal view source guard を通す（技術語は `<details>` / 上級者向け表示へ退避）
- [ ] backend DB/projection/SSE 系の代表 fixture を action/runtime 分類ごとに追加する（全API full loop は要求しない）

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
