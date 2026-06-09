# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `ui-builder-preset-ecosystem` | UIBuilder preset ecosystem / provisional presets | partial | 5 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
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

UIBuilder preset ecosystem parent surface is partial. Provisional preset surfaces remain tracked at bundle level until implemented or explicitly completed/descoped by SSOT. Completed md_viewer / completed preset seed evidence remains closed and must not be reclassified as unfinished work without contradicting SSOT/evidence.

- [ ] aggregate_dashboard provisional preset surface is not yet implemented or explicitly completed
- [ ] hub_search provisional preset surface is not yet implemented or explicitly completed
- [ ] physical_search_crud_aggregate provisional preset surface is not yet implemented or explicitly completed
- [ ] physical_details_inline_editor_md_generator provisional preset surface is not yet implemented or explicitly completed
- [ ] md_viewer remains completed child surface evidence and must not be reopened as unresolved unless SSOT/evidence contradicts it

---

## Bundle `preset_team_markdown_saved_view_seed`

**Status:** implemented
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
- `docs/system-roadmap.yaml` の 4 capability バンドルを実装実態に更新

完了済み carry-over:
- [x] **search_scope_completion**: rendered_markdown / tags(dashboard_ref.tags または card_metadata_json) / status filter を含む saved view search scope の completion。SSOT completion_condition: `saved_views_are_searchable_from_team_dashboard`。Roadmap known_gap: `product.component_markdown_authoring_projection#search_scope_rendered_markdown_and_tags_not_searched`
- [x] **seed_validator_depth_completion**: CompletedPresetSeedValidator を nested required fields (binding_ref.required_placeholder_keys, dashboard_ref.card_metadata_json, dashboard_ref.search_index_basis_json, adjustment_ref.user_adjustment_patch_json 等) まで検証するよう completion。SSOT completion_condition: `completed_preset_seed_validation_blocks_incomplete_seed`。Roadmap known_gap: `product.completed_preset_seed_projection_gate#seed_validator_depth_nested_fields_not_validated_only_top_level_and_render_hash`
- [x] **markdown_binding_renderer_completion**: explicit binding resolver (placeholder → record field value 解決)、required placeholder blocking (REQUIRED_PLACEHOLDER_UNBOUND)、optional placeholder empty-state handling の completion。SSOT completion_condition: `markdown_renderer_resolves_explicit_bindings_without_ai_inference`, `unresolved_required_placeholders_block_save`。Roadmap known_gap: `product.component_markdown_authoring_projection#markdown_binding_renderer_not_implemented_required_placeholder_resolution_not_completed`
- [x] **refresh_rebind_clone_gate_completion**: clone backend action (AdminRuntime.TeamMarkdown.cs に saved_view:clone 追加)、rebind 設計または action、seed invalid block を backend/frontend 両方で完結。SSOT completion_condition: `result_card_and_expanded_view_can_be_rehydrated_from_completed_preset_seed`。Roadmap known_gap: `product.completed_preset_seed_projection_gate#clone_action_backend_implementation_pending`, `rebind_action_not_designed_or_implemented`
- [x] **md_viewer_dashboard_action_wiring_completion (UI boundary scope)**: TeamMarkdownDashboard から open_source_record / edit_saved_view_adjustment / create_follow_up_todo_candidate を UI-only notice として配線し、refresh / clone / rebind は backend action registration 後、explicit payload required notice と seed-invalid disabled gate に更新。Roadmap known_gap removed from `product.md_viewer_projection_component`.
- [x] **dashboard_surface_mounting_completion**: preferred `/admin/team-dashboard` route を AdminAuthGate 配下に作成し、UIBuilder child placement とは別に routable placement を完了。Roadmap known_gap removed from `product.component_markdown_authoring_projection`.
- [x] **preset_catalog_seed_registration_completion**: preset catalog seed rows / bootstrap registration / metadata DB registration。Roadmap known_gap: `product.preset_db_seed_registration#preset_catalog_seed_data_rows_not_yet_bootstrapped`
- [x] UIBuilder_preset_ecosystem_md_viewer_child_surface_wiring — `/admin/ui-builder` 内に projection-only TeamMarkdownDashboard child surface を配置。Roadmap known_gap removed from `product.md_viewer_projection_component`.
- [x] **md_translation_template_seed_registration_surface_completion**: complete and harden template seed registration/list/update/archive as a seed-driven authoring surface using existing component bucket parts, not bespoke Markdown-only Modal/Drawer/Form creation. Current PR#396 scope only introduced minimal client entry/contract pieces; full prompt-level surface remains pending. Roadmap known_gap: `product.component_markdown_authoring_projection#md_translation_template_seed_registration_surface_completion_pending`. Completed: `MdTranslationAuthoringSeedSurface.tsx` — template registration form uses `createTemplate` API, template list via `listTemplates` API; registry-driven `Select` with `+ Register new template` toggle; existing bucket parts only.
- [x] **md_translation_binding_seed_authoring_surface_completion**: complete and harden explicit source table / source record / jsonb path / saved query field / static text binding selection as a seed-driven authoring surface using existing component bucket parts. Current PR#396 scope implements the seed metadata contract and optional empty-state construction, not the full prompt-level authoring surface. Roadmap known_gap: `product.component_markdown_authoring_projection#md_translation_binding_seed_authoring_surface_completion_pending`. Completed: `MdTranslationAuthoringSeedSurface.tsx` — registry-driven source table via `listRelationshipRemoteTargets`; `<datalist>` column candidates; explicit manual fallback labeled; `saved_query_result_field` explicit no-enumeration label; `data-source-record-ref-manual` attribute.
- [x] **md_translation_seed_candidate_builder_contract**: client-side seed candidate helper builds template_ref/source_ref/binding_ref/render_ref/adjustment_ref/dashboard_ref/lineage_ref from explicit metadata, blocks unresolved required placeholders, preserves optional empty-state metadata, and does not reverse-engineer from rendered Markdown. Roadmap completion: seed builder helper contract implemented.
- [x] **unresolved_required_placeholder_backend_gate**: CompletedPresetSeedValidator blocks non-empty render_ref.unresolved_placeholder_keys and binding_ref.unresolved_required_placeholder_keys with explicit REQUIRED_PLACEHOLDER_UNBOUND.
- [x] **md_translation_saved_view_create_seed_flow_completion**: complete and harden the prompt-level authoring-surface-to-saved_view:create flow, including full surface integration, explicit response/error handling evidence, and component bucket composition review. Roadmap known_gap: `product.component_markdown_authoring_projection#md_translation_saved_view_create_seed_flow_completion_pending`. Completed: `MdTranslationAuthoringSeedSurface.tsx` — `handleSave` calls `createSavedView` via team_markdown API; explicit error display; `completedPresetSeedJson` via `buildMdTranslationAuthoringSeedCandidate` from seed builder lib; unresolved required gate blocks save.
- [x] **existing_component_bucket_composition_hardening**: verify/harden that Modal/Drawer/Input/Select/Panel usage remains existing component bucket composition and not bespoke Markdown-only UI component creation. Roadmap known_gap: `product.component_markdown_authoring_projection#existing_component_bucket_composition_hardening_pending`. Completed: `MdTranslationAuthoringSeedSurface.tsx` — uses only `Select` (select.template), native `<input>` (input.primitive), `<textarea>` (textarea.template), `<button>` (button.primitive); `data-component-bucket-parts="select input textarea button existing_bucket_parts"` on root div; no bespoke modal/drawer/form created.
- [x] full_drawer_placement_via_UIBuilder_canvas — UIBuilder child surface 上の saved view card click から既存 MdViewer drawer/panel が開く配置を完了。Roadmap known_gap removed from `product.md_viewer_projection_component`.

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
