# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `ui-builder-preset-ecosystem` | UIBuilder preset ecosystem / provisional presets | partial | 4 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |
| `ui-builder-search-suggest-candidate-boundary` | UIBuilder autocomplete / suggest / combobox candidate boundary | partial | 1 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml` |
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

- [ ] aggregate_dashboard provisional preset surface is not yet implemented or explicitly completed
- [ ] hub_search provisional preset surface is not yet implemented or explicitly completed
- [ ] physical_search_crud_aggregate provisional preset surface is not yet implemented or explicitly completed
- [ ] physical_details_inline_editor_md_generator provisional preset surface is not yet implemented or explicitly completed

Note: md_viewer is now a dashboard/read-work component candidate shown in DashboardCandidatePalette; its completed preset seed / saved view flow evidence remains closed under `/admin/team-dashboard` primary route and is intentionally not retained as TODO evidence ledger.

---

## Bundle `ui-builder-search-suggest-candidate-boundary`

**Status:** partial
**Roadmap bundle:** `product.admin_topology_authoring`
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/ui-ux-primitive-catalog-ssot.yaml`
**Target files:** `frontend/components/AutoCompleteInput.tsx`, `frontend/components/SuggestInput.tsx`, `frontend/components/SearchCombobox.tsx`, `frontend/lib/uiBuilderAutocompleteCandidates.ts`, `frontend/components/catalog.ts`

**実装済み:**
- `AutoCompleteInput.tsx` / `SuggestInput.tsx` に `onSearch?: (query: string) => void` を追加。debounce + backend read-only search 境界を prop として宣言。no mutation during typing コメント付与。
- `runtimeComponentFactory.ts` の autocomplete / suggest factory に `onSearch` → `eventBinding.search` バインディングを追加。
- `uiBuilderAutocompleteCandidates.ts` ヘッダーを「combobox candidate derivation」として明確化。NOT autocomplete/suggest body と明示。
- `frontend/tests/uiBuilderAutocompleteCandidates.test.ts` ヘッダーをcombobox candidate derivation framing へ更新。
- `catalog.ts` の3コンポーネント notes を更新: autocomplete/suggest に `candidate_source_boundary:debounce_backend_readonly_search | no_mutation_during_typing:true`、combobox に `combobox_candidate_source:uiBuilderAutocompleteCandidates.ts`。
- `docs/design/ui-ux-primitive-catalog-ssot.yaml` に境界フィールド追加 (`candidate_source_boundary`, `no_mutation_during_typing`, `onSearch_note`, etc.)。
- `frontend/tests/searchSuggestCandidateBoundary.test.ts` を新規作成: prop 型境界・no fetch/eval/Function 境界・framing 宣言テスト。

**残作業 (partial 判定理由):**
- [ ] backend read-only search endpoint の実装: `onSearch` に対応する backend search API は未実装。runtime spec の "search" event binding を受け取る backend handler が必要。実装は backend dispatch / search endpoint 側の別 bundle 扱い。
- [ ] `runtimeComponentFactory` の "search" event で suggestions を更新するリアクティブ更新機構: island/page 側で `onSearch` → debounce → backend search → `suggestions` prop 更新の loop が必要。UIBuilder island 実装は別 bundle。
- [ ] `SearchCombobox` へのcombobox candidate derivation integration テスト: `uiBuilderAutocompleteCandidates.ts` helpers を `SearchCombobox` props へ接続する UiBuilderAdmin island 側のテストカバレッジ未整備。

---

## Bundle `ui-builder-projection-authoring-assist-roadmap-alignment`

**Status:** not_started
**Roadmap bundle:** `product.admin_topology_authoring`
**Depends on:** `ui-builder-selection-model`, `ui-builder-autocomplete-candidates`, `ui-builder-batch-operation`, `ui-builder-suggest-authoring-assist`, `ui-builder-search-suggest-candidate-boundary`
**SSOT:** `docs/system-roadmap.yaml`, `.agent/docs/ssot-map.yaml`, `docs/design/admin-console-workflow-ssot.yaml`

- [ ] UIBuilder projection setting authoring assist の bundle 群が実装された後、roadmap / TODO / SSOT / required evidence を同じ completion boundary へ揃える。

Scope:
- `docs/system-roadmap.yaml` の known_gap_ref / completion_condition / evidence_ref への反映要否を判断する。
- `.agent/docs/ssot-map.yaml` の worktype / required surface 追加要否を判断する。
- `docs/design/admin-console-workflow-ssot.yaml` への contract 追加要否を判断する。
- 実装完了できない残項目がある場合は partial 判定できる粒度で残 todo を全列挙する。

**Carry-over boundary correction:**
- [ ] **active-remote-manifest-column-suggest** は UIBuilder 側の設計判断待ち / backend dispatch 実装待ちとして扱わない。`/admin/contents` Step 2.5 が relationship 設定を担当し、active remote 側の `remote_manifest_id` / `join_table_ref` / `remote_key` は `screen_data_shape.relationIntents` に保存・検証される。remote target の一覧取得と検証は `manifest:list_relationship_remote_targets` / `assign_screen_data_shape` 側の責務で完結する。UIBuilder の責務は、保存済み `screen_data_shape` / `relationIntents` / projection data shape を component props / propBindings / layout projection へ配線することに限定する。残作業は、roadmap / TODO / SSOT / evidence 上でこの責務境界を明確化し、`deriveQualifiedColumnCandidates` の `remoteTargets = []` 固定を UIBuilder 実装漏れとして誤判定しないよう completion boundary を揃えること。

Completion condition:
- roadmap / TODO / SSOT の責務が食い違わず、後続 PR closure が bundle 単位で判定できる。
- 実装完了判定は roadmap/TODO 記述だけで行わず、実コード・テスト evidence と突合する。

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する

---

## Bundle `preset_team_markdown_saved_view_seed` (completed evidence anchor)

**Status:** implemented
**SSOT:** `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`

完了済み carry-over (evidence anchor — test guard for seed-driven wording):
- [x] **md_translation_template_seed_registration_surface_completion**: MdTranslationAuthoringSeedSurface.tsx — template registration form uses createTemplate API, template list via listTemplates API; registry-driven Select with + Register new template toggle; existing bucket parts only. Roadmap known_gap: `product.component_markdown_authoring_projection#md_translation_template_seed_registration_surface_completion_pending`.
- [x] **md_translation_binding_seed_authoring_surface_completion**: MdTranslationAuthoringSeedSurface.tsx — registry-driven source table via listRelationshipRemoteTargets; datalist column candidates; explicit manual fallback labeled. Roadmap known_gap: `product.component_markdown_authoring_projection#md_translation_binding_seed_authoring_surface_completion_pending`.
- [x] **md_translation_seed_candidate_builder_contract**: seed candidate helper builds template_ref/source_ref/binding_ref/render_ref/adjustment_ref/dashboard_ref/lineage_ref; blocks unresolved required placeholders.
- [x] **unresolved_required_placeholder_backend_gate**: CompletedPresetSeedValidator blocks non-empty render_ref.unresolved_placeholder_keys and binding_ref.unresolved_required_placeholder_keys with explicit REQUIRED_PLACEHOLDER_UNBOUND.
- [x] **md_translation_saved_view_create_seed_flow_completion**: handleSave calls createSavedView via team_markdown API; completedPresetSeedJson via buildMdTranslationAuthoringSeedCandidate; unresolved required gate blocks save. Roadmap known_gap: `product.component_markdown_authoring_projection#md_translation_saved_view_create_seed_flow_completion_pending`.
- [x] **existing_component_bucket_composition_hardening**: MdTranslationAuthoringSeedSurface.tsx uses only Select/input/textarea/button existing bucket parts; data-component-bucket-parts attribute on root div; no bespoke modal/drawer/form created. Roadmap known_gap: `product.component_markdown_authoring_projection#existing_component_bucket_composition_hardening_pending`.
