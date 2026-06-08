# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `preset_team_markdown_saved_view_seed` | Preset / Markdown saved-view unresolved work queue | partial | 1 | `product.preset_db_seed_registration`, `product.component_markdown_authoring_projection`, `product.md_viewer_projection_component`, `product.completed_preset_seed_projection_gate` | `docs/design/team-markdown-dashboard-saved-view-ssot.yaml` |
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

## Bundle `preset_team_markdown_saved_view_seed`

**Status:** partial
**Owner / target SSOT:** `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`
**Parent SSOT:** `docs/design/admin-console-workflow-ssot.yaml` (`preset_ecosystem`)
**Supporting SSOT:** `docs/design/mock-preset-intake-compiler-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/runtime-orchestration-ssot.yaml`

実装済み:
- DB migration `db/migrations/team_markdown_registry_tables.sql` (3 tables: template_registry, saved_view, saved_view_event)
- `db/init.sql` にマイグレーション追加
- `backend/schema/TeamMarkdownContracts.cs` (全 request/response 型 + `CompletedPresetSeedValidator`)
- `backend/repository/TeamMarkdownRepository.cs` + `NpgsqlTeamMarkdownRepository.cs`
- `backend/runtime/AdminRuntime.TeamMarkdown.cs` (template create/list/get/update/archive, saved_view create/search/get/refresh/update/archive)
- `AdminRuntime.cs` に `_teamMarkdownRepository` フィールド・コンストラクタ追加、dispatch に team_markdown レイヤー追加
- `Program.cs` に `NpgsqlTeamMarkdownRepository` DI 登録追加
- `frontend/api/teamMarkdownApi.ts` (全 API 関数)
- `frontend/components/MdViewer.tsx` (hardcoded projection component)
- `frontend/islands/TeamMarkdownDashboard.tsx` (search input, result cards, click expand drawer, UI-only action boundary notices)
- `frontend/routes/admin/team-dashboard/index.tsx` (`/admin/team-dashboard` AdminAuthGate routable placement)
- `frontend/islands/UiBuilderAdmin.tsx` (UIBuilder preset_ecosystem `md_viewer` child projection surface placement)
- `frontend/components/catalog.ts` (`md_viewer.projection` catalog visibility as projection child, not seed registration)
- `backend/tests/Topolactor.Runtime.Tests/TeamMarkdownSavedViewTests.cs`
- `frontend/tests/teamMarkdownSavedView.test.ts`
- `check-bootstrap-validation.sh` に team_markdown テーブル検証追加
- `docs/design/db-schema.yaml` に migration_ddl_available エントリ更新
- `docs/system-roadmap.yaml` の 4 capability バンドルを partial に更新

残タスク (carry-over):
- [ ] **search_scope_completion**: rendered_markdown / tags(dashboard_ref.tags または card_metadata_json) / status filter を含む saved view search scope の completion。SSOT completion_condition: `saved_views_are_searchable_from_team_dashboard`。Roadmap known_gap: `product.component_markdown_authoring_projection#search_scope_rendered_markdown_and_tags_not_searched`
- [ ] **seed_validator_depth_completion**: CompletedPresetSeedValidator を nested required fields (binding_ref.required_placeholder_keys, dashboard_ref.card_metadata_json, dashboard_ref.search_index_basis_json, adjustment_ref.user_adjustment_patch_json 等) まで検証するよう completion。SSOT completion_condition: `completed_preset_seed_validation_blocks_incomplete_seed`。Roadmap known_gap: `product.completed_preset_seed_projection_gate#seed_validator_depth_nested_fields_not_validated_only_top_level_and_render_hash`
- [ ] **markdown_binding_renderer_completion**: explicit binding resolver (placeholder → record field value 解決)、required placeholder blocking (REQUIRED_PLACEHOLDER_UNBOUND)、optional placeholder empty-state handling の completion。SSOT completion_condition: `markdown_renderer_resolves_explicit_bindings_without_ai_inference`, `unresolved_required_placeholders_block_save`。Roadmap known_gap: `product.component_markdown_authoring_projection#markdown_binding_renderer_not_implemented_required_placeholder_blocking_not_enforced`
- [ ] **refresh_rebind_clone_gate_completion**: clone backend action (AdminRuntime.TeamMarkdown.cs に saved_view:clone 追加)、rebind 設計または action、seed invalid block を backend/frontend 両方で完結。SSOT completion_condition: `result_card_and_expanded_view_can_be_rehydrated_from_completed_preset_seed`。Roadmap known_gap: `product.completed_preset_seed_projection_gate#clone_action_backend_implementation_pending`, `rebind_action_not_designed_or_implemented`
- [x] **md_viewer_dashboard_action_wiring_completion (UI boundary scope)**: TeamMarkdownDashboard から open_source_record / edit_saved_view_adjustment / create_follow_up_todo_candidate を UI-only notice として配線し、refresh / clone / rebind は backend future boundary として明示 disabled。Roadmap known_gap removed from `product.md_viewer_projection_component`; backend refresh/clone/rebind completion remains in `refresh_rebind_clone_gate_completion`.
- [x] **dashboard_surface_mounting_completion**: preferred `/admin/team-dashboard` route を AdminAuthGate 配下に作成し、UIBuilder child placement とは別に routable placement を完了。Roadmap known_gap removed from `product.component_markdown_authoring_projection`.
- [ ] **preset_catalog_seed_registration_completion**: preset catalog seed rows / bootstrap registration / metadata DB registration。Roadmap known_gap: `product.preset_db_seed_registration#preset_catalog_seed_data_rows_not_yet_bootstrapped`
- [x] UIBuilder_preset_ecosystem_md_viewer_child_surface_wiring — `/admin/ui-builder` 内に projection-only TeamMarkdownDashboard child surface を配置。Roadmap known_gap removed from `product.md_viewer_projection_component`.
- [x] **md_translation_template_seed_registration_surface (minimal authoring entry)**: existing component bucket parts expose template seed metadata registration/list/update/archive without bespoke Markdown-only Modal/Drawer/Form creation. Roadmap known_gap reworded to `product.component_markdown_authoring_projection#md_translation_template_seed_registration_surface_minimal_flow_needs_completion_hardening`.
- [x] **md_translation_binding_seed_authoring_surface (minimal authoring entry)**: explicit source/record/placeholder binding seed metadata and optional empty-state construction are reachable through TeamMarkdownDashboard using existing bucket parts. Roadmap known_gap reworded to `product.component_markdown_authoring_projection#md_translation_binding_seed_authoring_surface_minimal_flow_needs_completion_hardening`.
- [x] **md_translation_saved_view_create_seed_flow (minimal authoring entry)**: client-side seed candidate helper builds template_ref/source_ref/binding_ref/render_ref/adjustment_ref/dashboard_ref/lineage_ref from explicit metadata and submits saved_view:create through team_markdown API. Roadmap known_gap reworded to `product.completed_preset_seed_projection_gate#seed_builder_helper_for_client_side_construction_minimal_entry_exists_deep_completion_pending`.
- [x] full_drawer_placement_via_UIBuilder_canvas — UIBuilder child surface 上の saved view card click から既存 MdViewer drawer/panel が開く配置を完了。Roadmap known_gap removed from `product.md_viewer_projection_component`.

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
