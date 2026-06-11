# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `ui-builder-preset-ecosystem` | UIBuilder preset ecosystem / provisional presets | partial | 4 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
| `ui-builder-batch-operation` | UIBuilder projection setting batch operation | not_started | 1 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
| `ui-builder-suggest-authoring-assist` | UIBuilder projection setting suggest assist | not_started | 1 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
| `ui-builder-projection-authoring-assist-roadmap-alignment` | UIBuilder projection authoring assist roadmap / SSOT alignment | not_started | 1 | `product.admin_topology_authoring` | `docs/system-roadmap.yaml`, `.agent/docs/ssot-map.yaml` |
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

UIBuilder preset ecosystem parent surface is partial. Provisional preset surfaces remain tracked at bundle level until implemented or explicitly completed/descoped by SSOT.

**Resolved (design_change 2026-06-09):** `UiBuilderPresetEcosystemPanel` permanent child surface has been removed from `/admin/ui-builder` to resolve responsibility mixing. `md_viewer.projection` is now a dashboard/read-work component candidate (not a UIBuilder preset_ecosystem permanent child). Team Markdown Dashboard primary route remains `/admin/team-dashboard`. SSOT/roadmap/tests updated accordingly.

**Resolved (existing_pr_update 2026-06-09):** `md_viewer.projection` は `DashboardCandidatePalette` として UIBuilder canvas に追加 — `dashboard_placement_candidate` タグでフィルタ、`registrationRequired:false` なので DB bucket 登録不要。`ComponentCapabilityTag` 型に `dashboard_placement_candidate` を追加。`registrationRequired` と palette 表示可否の責務分離完了。SSOT/roadmap/tests 更新済み。

**Resolved (cardlist-array-binding-completion 2026-06-10):**
- [x] `display/card_list` の catalog / runtime factory / preview / tests を閉じる — `CardList.tsx` コンポーネント実装、`catalog.ts` に `card_list.primitive` 追加、`runtimeComponentFactory.ts` に `cardListFactory` 追加、`layoutComponentPreview.ts` に preview サイズ・placeholder props 追加
- [x] `display/card` は単体表示専用として items/rows を受けないことをテストで固定する — `propBindingResolver.test.ts` と `renderEmissionPropBindings.test.ts` に rejection テスト追加
- [x] UI Builder に「データ接続（API受信 / emission.data）」セクションを通常導線として追加する — `data_binding` タブ追加（全 path フィールド: keyPath/labelPath/valuePath/childrenPath、非配列コンポーネント向け誘導、tree 未接続ステータス説明）
- [x] API送信配線、UIローカルイベント、API受信データ投影、生JSON override を同じ advanced ブロックに混在させない — 旧 `配列 Prop バインド` AdvancedManualOverride を advanced タブから削除
- [x] propBindings の labelPath/valuePath/childrenPath も UI から編集できるようにする — データ接続タブで全 path フィールド編集可能
- [x] frontend tests に CardList rows binding の end-to-end-ish ケースを追加する — `renderEmissionPropBindings.test.ts` に 3 ケース追加
- [x] docs/system-roadmap.yaml と .agent/tasks/todo.md を completion bundle 単位で更新する

**Resolved (runtime-catalog-full-connection-rk9ije 2026-06-10):**
- [x] `runtime_component_catalog_full_connection_bundle` — COMPONENT_CATALOG_ENTRIES の runtimeConnected:false エントリを全量接続。select / checkbox / badge / status_badge / alert / loading / empty / error / json_viewer / admin_page_shell / admin_section / validation_result / textarea_template / tabs / tree / md_viewer の 16 component に runtime factory 追加。tree_node は sub-component として registrationRequired:false / non-runtime 明示。双方向整合テスト (runtimeComponentCatalogFullConnection.test.ts) 追加。registrationRequired:true + runtimeConnected:false 違反ゼロ確認済み。

**Resolved (md_viewer_runtime_completion_bundle 2026-06-10):**
- [x] `md_viewer_runtime_completion_bundle` — `mdViewerPreviewFactory` を placeholder div から MdViewer 実体への runtime renderer に置き換え。`buildLayoutPreviewPlaceholderProps("data_display/md_viewer")` に savedView/seedValid サンプル projection props を整備。runtime props 正規化 (`normalizeMdViewerSavedView`) 追加。savedView 欠損時の明示エラー (`RUNTIME_MD_VIEWER_MISSING_SAVED_VIEW_PROPS` / `RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS`) 実装。全 mutation action callbacks を未提供（authority は /admin/team-dashboard 側に維持）。preview mode では `disabledActionReasons` で明示的 disabled 理由を表示。`mdViewerRuntimeCompletion.test.ts` 追加。

- [ ] aggregate_dashboard provisional preset surface is not yet implemented or explicitly completed
- [ ] hub_search provisional preset surface is not yet implemented or explicitly completed
- [ ] physical_search_crud_aggregate provisional preset surface is not yet implemented or explicitly completed
- [ ] physical_details_inline_editor_md_generator provisional preset surface is not yet implemented or explicitly completed
Note: md_viewer is now a dashboard/read-work component candidate shown in DashboardCandidatePalette; its completed preset seed / saved view flow evidence remains closed under `/admin/team-dashboard` primary route.

---

## UIBuilder projection setting authoring assist 作業順序

`ui-builder-selection-model` は実装済み。`ui-builder-autocomplete-candidates` は実装済み。次 bundle は `ui-builder-batch-operation`。

残り実装順序:
1. `ui-builder-batch-operation`
2. `ui-builder-suggest-authoring-assist`
3. `ui-builder-projection-authoring-assist-roadmap-alignment`

---

## Bundle `ui-builder-batch-operation`

**Status:** not_started
**Roadmap bundle:** `product.admin_topology_authoring`
**Depends on:** `ui-builder-selection-model`
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml` (`ui_builder_canvas_workspace`), `docs/design/topology-layout-class-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml` (`component_wiring_execution_lane`)

- [ ] selection set に対する layoutClassRefs / propsJson / stateJson / propBindings / wiring / calc binding の batch authoring assist を、preview / validate / apply boundary を迂回せずに実装する。

Scope:
- layoutClassRefs batch add/remove/replace は allowedFor / conflict_group / raw className 禁止を守る。
- propsJson / stateJson は JSON object merge/patch として扱い、malformed JSON や配列 root は明示エラーにする。
- propBindings は componentKind capability を検証し、非対応 node は silent skip せず per-node error として表示する。
- wiring / frontend-local calc binding の複数 node 参照補助を追加する。ただし input/change ごとの backend dispatch・逐次DB保存・eval/Function は追加しない。
- batch apply 前に対象 node 数、変更内容、per-node validation result を preview 表示する。

Completion condition:
- batch 対象・変更内容・per-node validation が UI 上で確認でき、silent skip がない。
- existing layout_patch preview / validate / apply と component_style_design / wiring / frontend-local calculation binding の境界を壊さない。

---

## Bundle `ui-builder-suggest-authoring-assist`

**Status:** not_started
**Roadmap bundle:** `product.admin_topology_authoring`
**Depends on:** `ui-builder-selection-model`, `ui-builder-autocomplete-candidates`
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml` (`frontend_local_derived_calculation_binding`), `docs/design/pipeline-continuity-ssot.yaml` (`component_wiring_execution_lane`)

- [ ] 選択中 node / selection set / loaded emission data から、次に設定すべき source node / target node / ruleTable matchConditions / targetProp 候補を suggest する authoring assist を実装する。

Scope:
- selected node から次候補を提示するが、自動 mutation authority にはしない。
- source node / target node 候補は draftNodes・nodeKind・componentKind・targetProp capability から導出する。
- ruleTable matchConditions 候補は tablePath rows の field と node value source 候補から導出する。
- targetProp 候補は既存 `resolveAllowedTargetProps` / `validateCalcTargetProp` と整合させる。
- suggest 採用は user action のみとし、投影・計算・wiring の runtime authority を持たせない。

Completion condition:
- suggest は候補提示に留まり、ユーザー採用なしに draft mutation しない。
- frontend-local calc boundary と component wiring execution boundary を壊さない。

---

## Bundle `ui-builder-projection-authoring-assist-roadmap-alignment`

**Status:** not_started
**Roadmap bundle:** `product.admin_topology_authoring`
**Depends on:** `ui-builder-selection-model`, `ui-builder-autocomplete-candidates`, `ui-builder-batch-operation`, `ui-builder-suggest-authoring-assist`
**SSOT:** `docs/system-roadmap.yaml`, `.agent/docs/ssot-map.yaml`, `docs/design/admin-console-workflow-ssot.yaml`

- [ ] UIBuilder projection setting authoring assist の bundle 群が実装された後、roadmap / TODO / SSOT / required evidence を同じ completion boundary へ揃える。

Scope:
- `docs/system-roadmap.yaml` の known_gap_ref / completion_condition / evidence_ref への反映要否を判断する。
- `.agent/docs/ssot-map.yaml` の worktype / required surface 追加要否を判断する。
- `docs/design/admin-console-workflow-ssot.yaml` への contract 追加要否を判断する。
- 実装完了できない残項目がある場合は partial 判定できる粒度で残 todo を全列挙する。

Completion condition:
- roadmap / TODO / SSOT の責務が食い違わず、後続 PR closure が bundle 単位で判定できる。
- 実装完了判定は roadmap/TODO 記述だけで行わず、実コード・テスト evidence と突合する。

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
- `frontend/islands/UiBuilderAdmin.tsx` (UIBuilder preset_ecosystem `md_viewer` child projection surface placement — later removed: permanent placement resolved as responsibility mixing; /admin/team-dashboard is primary route)
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
