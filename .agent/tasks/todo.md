# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `demo-layout-projection-continuity` | admin layout authoring → demo projection continuity | partial | 1 | `docs/design/pipeline-continuity-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |

---

## Bundle `demo-layout-projection-continuity`

**Status:** partial  
**SSOT:** `docs/design/pipeline-continuity-ssot.yaml`  
**Supporting SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/db-schema.yaml`

**Problem:** admin canvas authoring can persist layout design into `topology.components_layout_design`, but the demo projection route does not preserve or consume layout identity. The current projection path remains `structure_map -> packageId/schemaId/componentIds -> Emission -> renderEmission()`, so authored layout cannot reach `/demo`.

**Target surfaces:**
- `backend/schema/Contracts.cs` — `RuntimeWorkingShape`, `Emission`
- `backend/runtime/EmissionBuilder.cs` — `EmissionBuilder.Build`
- `frontend/api/dispatch.ts` — `Emission`
- `frontend/runtime/renderEmission.ts` — `renderEmission`
- `frontend/islands/UserDemoStepper.tsx` — dispatch result consumption
- `db/demo_seed.sql` / `topology.structure_maps` — layout identity mapping
- `topology.components_layout_design` — authored layout source

**Completion condition / TODO:**
- [ ] Preserve layout identity/payload as one canonical pipeline bundle from admin-authored layout storage through backend emission to `/demo` projection, including explicit failure for missing/malformed layout refs and tests covering emission identity, structure-map layout mapping, `renderEmission()`, and `UserDemoStepper` consumption.

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
