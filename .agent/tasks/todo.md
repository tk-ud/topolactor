# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `layout-application-projection-continuity` | admin layout authoring → application/draft projection continuity | partial | 1 | `docs/design/pipeline-continuity-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |

---

## Bundle `layout-application-projection-continuity`

**Status:** partial  
**SSOT:** `docs/design/pipeline-continuity-ssot.yaml`  
**Supporting SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/db-schema.yaml`

**Problem:** admin canvas authoring can persist layout design into `topology.components_layout_design`, but the production application projection path does not preserve or consume layout identity. The current `/` route is an admin/demo guide surface, while runtime projection remains `structure_map -> packageId/schemaId/componentIds -> Emission -> renderEmission()`, so authored layout cannot reach the application projection surface. The current `/demo` surface is also not a coherent product preview because it does not let the user select an admin-authored layout and project draft content through that layout.

**Target surfaces:**
- `frontend/routes/index.tsx` — current guide-only top route; replace or bridge to production projection shell when implementing application projection
- `frontend/routes/demo.tsx` — either delete as obsolete, or repurpose as an explicit draft preview surface that selects an admin-authored layout and projects draft content through it
- `frontend/routes/demo/debug.tsx` — delete if no longer useful after coherent draft preview exists; otherwise keep only as developer raw-runtime inspection linked from dev-facing surfaces, not product flow
- `backend/schema/Contracts.cs` — `RuntimeWorkingShape`, `Emission`
- `backend/runtime/EmissionBuilder.cs` — `EmissionBuilder.Build`
- `frontend/api/dispatch.ts` — `Emission`
- `frontend/runtime/renderEmission.ts` — `renderEmission`
- `frontend/components/EmissionView.tsx` / `frontend/components/ProjectionView.tsx` — application projection consumers
- `topology.structure_maps` / related seed or migration surfaces — layout identity mapping
- `topology.components_layout_design` — authored layout source
- draft/content source surfaces used by admin contents authoring

**Completion condition / TODO:**
- [ ] Preserve layout identity/payload as one canonical pipeline bundle from admin-authored layout storage through backend emission to the application projection surface, including explicit failure for missing/malformed layout refs and tests covering emission identity, structure-map layout mapping, production route projection, `renderEmission()`, and projection component consumption.
- [ ] Decide `/demo` by implementation value only: delete it if it remains a vague demo page; otherwise repurpose it into a draft preview surface where the user selects an admin-authored layout and projects selected draft content through that layout. Do not keep the current vague demo state.

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
