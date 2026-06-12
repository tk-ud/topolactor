# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `ui-builder-preset-ecosystem` | UIBuilder preset ecosystem / provisional presets | partial | 7 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
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
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml` (`ui_builder_canvas_workspace.authoring_flow.responsibilities.preset_ecosystem`)
**Supporting SSOT:** `docs/design/ui-builder-seed-first-gap-discovery-ssot.yaml`, `docs/design/mock-preset-intake-compiler-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/sql-attention-logs-ssot.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`

UIBuilder preset ecosystem parent surface is partial. Provisional preset surfaces remain tracked at bundle level until implemented or explicitly completed/descoped by SSOT. The design direction is seed-first preset SSOT: reusable preset contracts must be defined as SSOT-backed seed surfaces, not hardcoded active topology or one-off screen implementations.

- [ ] `preset_ssot_seed_contract`: **Problem:** `physical_search_crud_aggregate` / `physical_details_inline_editor_md_generator` are named as provisional preset surfaces, but their seed contract is not yet specified deeply enough for audit. **Purpose:** make seed化 / preset SSOT化 the canonical design boundary. **Improvement direction:** either create a dedicated preset ecosystem SSOT or extend the existing admin-console + seed-first SSOTs with per-preset contract blocks for layout tree, component catalog dependencies, wiring candidates, payloadFrom shape, logs.diff history binding, validate→save flow, and preview/validate/apply boundary. **References:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/ui-builder-seed-first-gap-discovery-ssot.yaml`, `docs/design/mock-preset-intake-compiler-ssot.yaml`. **Targets:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/ui-builder-seed-first-gap-discovery-ssot.yaml`, optional new `docs/design/ui-builder-preset-ecosystem-ssot.yaml`, `.agent/docs/ssot-map.yaml` / design index if a new SSOT is added.
- [ ] `runtime_event_payload_binding_from_node_and_event_values`: **Problem:** preset actions cannot reliably dispatch runtime payloads from current node/interface values and event payload values. **Purpose:** make seed preset actions executable without screen-specific hardcoding. **Improvement direction:** implement a reusable `payloadFrom` resolver for `node:<nodeId>.value`, `event.item.id`, `event.row.id`, `event.record.id`, literals, and structured unresolved-ref errors; no silent fallback. **References:** `docs/design/ui-builder-seed-first-gap-discovery-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `db/migrations/hub_search_preset_seed.sql`. **Targets:** `frontend/runtime/*payload*resolver*.ts` or equivalent new runtime helper, `frontend/runtime/layoutComponentPreview.ts`, `frontend/components/FlowLayoutCanvas.tsx`, `frontend/islands/UiBuilderAdmin.tsx`, `frontend/tests/mockPresetIntake.test.ts`, new focused resolver tests.
- [ ] `physical_search_crud_aggregate_seed`: **Problem:** CRUD aggregate preset is still provisional and not available as a seed-first reusable UIBuilder preset. **Purpose:** compose existing runtime/catalog capabilities into a reusable search/add/detail/edit/delete/history preset. **Improvement direction:** seed a canvas preset containing search input + search button, add button → modal → generated form, enum/status select generation, `card_list` rows, details/edit/delete buttons, confirm dialog reuse, logical delete wiring, tree/tree-node data binding, and node history drawer action. **References:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`, `docs/design/sql-attention-logs-ssot.yaml`. **Targets:** `db/migrations/*physical_search_crud*_preset_seed.sql` or existing preset seed registry path, `frontend/components/catalog.ts`, `frontend/tests/mockPresetIntake.test.ts`, seed compile/preview tests, `backend/runtime/AdminRuntime.cs` only if missing dispatch names are found by audit.
- [ ] `physical_details_inline_editor_md_generator_seed`: **Problem:** detail/inline editor/Markdown/PDF preview preset is still provisional and not seed-backed. **Purpose:** provide a reusable details surface fed by CRUD card selection. **Improvement direction:** seed header back button + PDF export preview, main tabs, tab1 two-column label/value grid with inline editable fields and enum/status selects, tab2 latest field history list, full-history side drawer, and document canvas preview/export snapshot wiring. **References:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`, `backend/runtime/PdfExportSnapshotRuntime.cs`, `frontend/components/DocumentCanvasTemplateEditor.tsx`. **Targets:** `db/migrations/*physical_details*_preset_seed.sql` or existing preset seed registry path, `frontend/components/catalog.ts`, `frontend/tests/documentCanvasCatalogCheck.test.ts`, `frontend/tests/mockPresetIntake.test.ts`, details seed compile/preview tests.
- [ ] `logs_diff_record_history_binding`: **Problem:** physical record history UI is designed around `logs.diff`, but preset read binding/history drawer contract is not fully captured as a preset requirement. **Purpose:** show latest field history and full field history without confusing `logs.diff` with topology definition audit logs. **Improvement direction:** bind history buttons/drawers to physical-table record lifecycle diff evidence (`create`, `update`, `logical_delete`, `restore`, `physical_delete`), preserve `logs.diff` as physical mutation pressure source, and prohibit direct reuse of `topology_edit_log` for physical record history. **References:** `docs/design/sql-attention-logs-ssot.yaml`, `backend/repository/SqlAttentionLogsRepository.cs`, `backend/repository/NpgsqlSqlAttentionLogsRepository.cs`. **Targets:** backend read API/repository method if absent, preset seed wiring candidates, `frontend/tests/mockPresetIntake.test.ts`, backend repository tests for history read boundary if implemented.
- [ ] `existing_entity_validate_save_dispatch_alignment`: **Problem:** CRUD/detail preset must use existing entity/draft dispatch and must not create a parallel hardcoded CRUD runtime. **Purpose:** keep mutation flow aligned with existing content bundle entity lifecycle. **Improvement direction:** document and test preset wiring for validate→save/update draft→promote/save active entity using existing `content_bundle:*` dispatch where applicable; if direct active update/logical delete dispatch is missing, add TODO/SSOT contract before implementation rather than inventing a hidden route. **References:** `backend/runtime/AdminRuntime.cs`, `backend/repository/ContentBundleRepository.cs`, `backend/repository/NpgsqlContentBundleRepository.cs`, `docs/design/runtime-orchestration-ssot.yaml`. **Targets:** preset seed wiring candidates, `frontend/lib/packageWiringOptions.ts`, `frontend/lib/packageWiringPicker.ts`, `frontend/tests/mockPresetIntake.test.ts`, backend dispatch tests only if new dispatch names are added.
- [ ] `aggregate_dashboard_seed`: **Problem:** aggregate_dashboard remains provisional even though it should be a thin contents-side aggregation preset. **Purpose:** let authors choose aggregation elements and a period width/window, then render those results as a reusable dashboard preset. **Improvement direction:** implement or reuse contents-side aggregation functions, wire preset configuration to those functions, and seed a dashboard preset that maps aggregation element list + period width/window + binding source to existing display components; do not create a separate one-off dashboard runtime. **References:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/system-roadmap.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`. **Targets:** contents-side aggregation function surface, preset SSOT block, optional seed migration/tests, `frontend/tests/mockPresetIntake.test.ts`, `.agent/tasks/todo.md` status update after audit.

Note: `hub_search.readonly.v1` is now registered by `db/migrations/hub_search_preset_seed.sql` as a UIBuilder canvas preset seed composed from existing component catalog entries only; it is not a new component implementation and does not write active topology. Its remaining interactive init-wiring gap is tracked by `runtime_event_payload_binding_from_node_and_event_values` above, including both search query submit and search-result hub selection dispatch payload resolution.

Note: md_viewer is now a dashboard/read-work component candidate shown in DashboardCandidatePalette; its completed preset seed / saved view flow evidence remains closed under `/admin/team-dashboard` primary route and is intentionally not retained as TODO evidence ledger.

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
