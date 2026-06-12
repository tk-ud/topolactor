# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `ui-builder-preset-ecosystem` | UIBuilder preset ecosystem / provisional presets | partial | 3 | `product.admin_topology_authoring` | `docs/design/ui-builder-preset-ecosystem-ssot.yaml` |
| `phase-origin-ssot-reference-graph` | 位相始点 SSOT 参照網拡張 | not_started | 1 | `product.sql_attention_observation_feedback` / `product.admin_topology_authoring` | `docs/framework-policy.yaml` |
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

## Bundle `ui-builder-preset-ecosystem`

**Status:** partial
**Roadmap bundle:** `product.admin_topology_authoring`
**SSOT:** `docs/design/ui-builder-preset-ecosystem-ssot.yaml` (new; per-preset contracts)
**Parent SSOT:** `docs/design/admin-console-workflow-ssot.yaml` (`ui_builder_canvas_workspace.authoring_flow.responsibilities.preset_ecosystem`)

Preset SSOT created (`docs/design/ui-builder-preset-ecosystem-ssot.yaml`). payloadFrom resolver implemented (`frontend/runtime/payloadFromResolver.ts`). hub_search wiring aligned to `content_bundle:search`. Three provisional preset seeds added (physical_search_crud_aggregate.v1, physical_details_inline_editor_md_generator.v1, aggregate_dashboard.v1). Tests added in `frontend/tests/payloadFromResolver.test.ts` and `frontend/tests/mockPresetIntake.test.ts`. Remaining gap: `logs_diff_record_history_binding` backend read boundary is absent.

- [ ] `logs_diff_record_history_binding`: **Problem:** Physical record history read boundary (`logs.diff`) is absent. `audit_diff_drawer.primitive` in `physical_details_inline_editor_md_generator.v1` has `emission.data.history` propBindings marked as pending in `unresolved_json`. **Purpose:** show latest and full field history (create/update/logical_delete/restore/physical_delete) without confusing logs.diff with topology_edit_log. **Improvement direction:** add `LoadPhysicalRecordHistoryAsync(tableId, recordId, ct)` to `SqlAttentionLogsRepository` / `NpgsqlSqlAttentionLogsRepository` and a corresponding AdminRuntime action (e.g. `physical_record:list_history`); prohibit `topology_edit_log` reuse for physical record history; scenario-contract protocol required for this backend persistence change. **References:** `docs/design/ui-builder-preset-ecosystem-ssot.yaml` logs_diff_record_history_binding_contract, `docs/design/sql-attention-logs-ssot.yaml`, `backend/repository/SqlAttentionLogsRepository.cs`, `backend/repository/NpgsqlSqlAttentionLogsRepository.cs`. **Targets:** `backend/repository/SqlAttentionLogsRepository.cs` (add read method), `backend/repository/NpgsqlSqlAttentionLogsRepository.cs` (Npgsql implementation), `backend/runtime/AdminRuntime.cs` (action registration), `frontend/tests/mockPresetIntake.test.ts` (update history binding test once backend exists), backend repository tests.
- [ ] `aggregation_function_ref_wiring`: **Problem:** `aggregate_dashboard.v1` seed SQL still has older placeholder wording (`targetSurface=content_bundle`, empty `targetRef`, `ssot_ambiguity`) even though the SSOT now defines the authoring surface as PackageWiringEditor. **Purpose:** after preset load, the author selects manifest `screenReadQueryWiring` candidates and saves `targetRef=manifest:<manifestId>:<wiringKey>` via `ui_topology:update_package_wiring`; aggregation dashboard candidates should normally come from `aggregationMeasures`. **Improvement direction:** update `db/migrations/aggregate_dashboard_preset_seed.sql` `knownGaps` / `unresolved_json` / wiring candidate wording to distinguish authoring-pending from implementation gaps and to reference PackageWiringEditor, `manifest:list_screen_read_query_wiring`, and `topology.ui_wiring_registry.target_ref`. **References:** `docs/design/ui-builder-preset-ecosystem-ssot.yaml` package_wiring_manifest_bridge and aggregate_dashboard_preset known_gaps, `db/migrations/aggregate_dashboard_preset_seed.sql` unresolved_json `aggregation_function_ref_wiring`. **Targets:** `db/migrations/aggregate_dashboard_preset_seed.sql` (update targetRef/wiring placeholder expression), preset seed compile snapshots/tests as needed.
- [ ] `aggregation_preview_table_prop_binding_capability`: **Problem:** `aggregation_preview_table.primitive` and `hub_statistics_panel.primitive` capability tags for `propBindings` data target need validation against the component catalog (`pending_catalog_validation`). `dashboard_aggregation_table` and `dashboard_stats_panel` in `aggregate_dashboard.v1` have propBindings data target marked pending in `unresolved_json`. **Purpose:** confirm the correct capability tag and propBindings schema for aggregation result display. **Improvement direction:** validate catalog component capability tags; update seed propBindings once confirmed. **References:** `docs/design/ui-builder-preset-ecosystem-ssot.yaml` aggregate_dashboard_preset known_gaps, `db/migrations/aggregate_dashboard_preset_seed.sql` unresolved_json `aggregation_preview_table_prop_binding_capability`. **Targets:** `db/migrations/aggregate_dashboard_preset_seed.sql` (update propBindings), `docs/design/ui-builder-preset-ecosystem-ssot.yaml` (update status once resolved).

Note: `hub_search.readonly.v1` wiring is now aligned to `content_bundle:search` (hub:search was not SSOT-authorized). payloadFrom resolver (`frontend/runtime/payloadFromResolver.ts`) handles `node:<nodeId>.value`, `event.<path>`, `literal:<value>` with structured errors and no silent fallback.

Note: md_viewer is now a dashboard/read-work component candidate shown in DashboardCandidatePalette; its completed preset seed / saved view flow evidence remains closed under `/admin/team-dashboard` primary route and is intentionally not retained as TODO evidence ledger.

---

## Bundle `phase-origin-ssot-reference-graph`

**Status:** not_started
**Roadmap bundle:** `product.sql_attention_observation_feedback` / `product.admin_topology_authoring`
**SSOT:** `docs/framework-policy.yaml` (`registry_tensor_principle`, `topolactor_space_boundary_policy`)

UIBuilder/preset/inspector の参照網は入口として整ったが、Topolactor の正本モデルでは phase / attention / registry tensor / logs / hub / topology が上流の意味軸。UIBuilder を参照グラフの root ではなく、phase-observed topology state から到達する downstream authoring/projection surface として接続する。

- [ ] `phase_origin_ssot_reference_graph`: **Problem:** Current UIBuilder SSOT reference graph is still UIBuilder/preset-centric. **Purpose:** expand SSOT cross references from phase-origin concepts to UIBuilder, manifest `screenReadQueryWiring`, package wiring, preset seed intake, SQL Attention observation, and runtime projection surfaces. **Improvement direction:** start from `docs/framework-policy.yaml` `registry_tensor_principle` / `topolactor_space_boundary_policy`; trace phase / attention / logs / registry tensor / hub relation axes into `docs/design/sql-attention-logs-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, and `docs/design/ui-builder-preset-ecosystem-ssot.yaml`; add references from non-UIBuilder SSOTs toward UIBuilder only where they cross projection/authoring boundaries. **Targets:** `docs/framework-policy.yaml`, `docs/design/sql-attention-logs-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/ui-builder-preset-ecosystem-ssot.yaml`.

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
