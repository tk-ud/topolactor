# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `phase-origin-convergence-boundary-note` | 位相始点の観測から収束境界への概念補足 | not_started | 1 | `product.sql_attention_observation_runtime` / `product.admin_topology_authoring` | `docs/framework-policy.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

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

## Bundle `phase-origin-convergence-boundary-note`

**Status:** not_started
**Roadmap bundle:** `product.sql_attention_observation_runtime` / `product.admin_topology_authoring`
**SSOT:** `docs/framework-policy.yaml` (`registry_tensor_principle`, `topolactor_space_boundary_policy`)

Topolactor の正本モデルでは phase / attention / logs / registry tensor / hub / topology は上流の観測・指針・参照軸であり、UIBuilder / frontend は水平展開面、backend はアトラクタ方向への収束面、物理 DB テーブルは収束点。参照グラフは観測・参照・navigation を助けるが governance ではない。governance は SSOT contracts / backend validation / runtime authority / preview-validate-apply / audit protocol / DB constraints にある。

- [ ] `phase_origin_convergence_boundary_note`: **Problem:** Current UIBuilder SSOT reference graph can still be misread as if the graph itself governs convergence. **Purpose:** document that phase-origin concepts lead to downstream projection/authoring surfaces, while convergence judgment and persistence authority remain in backend/runtime/DB/SSOT boundaries. **Improvement direction:** use `docs/design/topolactor-phase-model.md` as conceptual guidance; when expanding cross-SSOT references, keep UIBuilder as a horizontal projection surface, backend as attractor-direction convergence, physical DB tables as convergence points, and hub/registry as maps/guidance rather than governance. **References:** `docs/design/topolactor-phase-model.md`, `docs/framework-policy.yaml` `registry_tensor_principle` / `topolactor_space_boundary_policy`, `docs/design/sql-attention-logs-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/ui-builder-preset-ecosystem-ssot.yaml`. **Targets:** future SSOT reference-note updates only; do not turn the graph into a governance authority.

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
