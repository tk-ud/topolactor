# Frontend UI Audit

- Date: 2026-07-06
- Target repo: github.com/tk-ud/topolactor
- Worktype: audit
- Scope: admin / projection frontend UI, UX wording, selection surfaces, seed-visible labels, and test surface.
- Evidence boundary: GitHub read fallback audit. `.agent/tools` runtime was not executable from this chat. No CI log or Agent local-test result was available, so test execution success is not used as evidence.
- Revision note: Finding 1 was revised after owner review. `contents -> ui-builder -> manifests` is a valid operator workflow. The remaining issue is the missing SSOT wording layer that distinguishes `/admin/contents` local submit steps 1-3 from the whole-admin operator workflow stages 4-5.
- Revision note: Finding 6 records owner direction for a large SSOT redesign. The intended direction is to keep the existing Figma-like layout canvas, add a switchable Markmap-style wiring canvas projection, use existing drag/drop interaction assets for wiring connection edits, keep `runtimeInteractions` as the canonical model, and switch inspectors by canvas mode.
- Revision note: Finding 7 records owner direction that non-canonical admin/debug/support pages must be removed from the admin workflow route surface and converted into initial seed / projection-app setting data where needed.

## Finding 1: admin workflow stage wording needs Step 4 / Step 5 SSOT clarification

### 対象SSOT

- `docs/design/admin-console-workflow-ssot.yaml`

### Section

- `purpose.summary`
- `canonical_sequential_authoring_pipeline`
- `ui_builder_canvas_workspace`
- Proposed addition: `canonical_admin_operator_workflow`

### 不整合実装ファイル

#### 実装側

- `frontend/islands/ContentsScreenDesignPanel.tsx`
- `frontend/content/adminGuides.ts`
- `frontend/tests/adminMainFlow.test.ts`
- Section: Step 3 completion banner / UI Builder handoff / admin main flow labels

#### seed側

- N/A: fixed frontend copy and guide/test contract, not seed-derived.

### 不整合詳細

- `contents -> ui-builder -> manifests` is not an invalid flow. It is the natural admin operator workflow and is already represented by guide/test surfaces.
- SSOT states `/admin/contents` owns numbered submit steps 1-3, and `/admin/ui-builder` is a canvas workspace route, not a `/admin/contents` local pipeline submit step.
- SSOT also describes `/admin/manifests` as post-contents hub / relation / navigation management.
- Implementation renders `保存済み — 次は Step 4 です` and links to `/admin/ui-builder`.
- The issue is not the UI Builder handoff itself. The issue is that `Step 4` is unqualified, so it can be read as `/admin/contents` local submit step 4 rather than whole-admin operator workflow stage 4.
- Because `ADMIN_MAIN_FLOW_STEPS` fixes `/admin/contents -> /admin/ui-builder -> /admin/manifests`, SSOT should explicitly define the whole-admin workflow layer and include Step 5 for `/admin/manifests`.

### 改善案

- Do not remove the `contents -> ui-builder -> manifests` flow.
- Add a SSOT block that separates two numbering layers:
  - `/admin/contents` local submit pipeline: Step 1-3 only.
  - whole-admin operator workflow: Step 1-3 contents, Step 4 UI Builder, Step 5 manifests.
- Define `/admin/ui-builder` as `whole-admin Step 4: 画面づくり`, while preserving `not contents submit pipeline step`.
- Define `/admin/manifests` as `whole-admin Step 5: ページ同士をつなぐ / navigation management`.
- Update visible copy to qualify the layer, for example `全体工程 Step 4: 画面づくりへ進めます` and `次は全体工程 Step 5: ページ同士をつなぐ`.
- Add/extend guard tests:
  - allow `Step 4` only when qualified as whole-admin workflow / 全体工程.
  - require `Step 5` for `/admin/manifests` in the same workflow layer.
  - prohibit wording that implies `/admin/ui-builder` or `/admin/manifests` are `/admin/contents` local submit steps.

## Finding 2: normal-view copy still exposes internal/system vocabulary

### 対象SSOT

- `docs/design/user-facing-helper-manual-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`

### Section

- `user_facing_message_policy.language_policy`
- `admin_topology_clone_lifecycle_reference_contract`
- `admin_contents_step1_entry_modes`

### 不整合実装ファイル

#### 実装側

- `frontend/content/adminUxTerms.ts`
  - `CONTENTS_STEP1_ENTRY_MODE_OPTIONS`
  - `UX_PACKAGE_WIRING_SECTION_HINT`
  - `UX_EXTERNAL_INTEGRATION_SECTION_HINT`
  - `UX_TOPOLOGY_API_*`
- `frontend/islands/ContentsScreenDesignPanel.tsx`
  - Step 1 labels / clone evidence / Step 2.5 relation text
- `frontend/islands/UiBuilderAdmin.tsx`
  - Canvas inspector visible labels for `componentKey`, `componentKind`, `layoutClassRefs`, `orderIndex`

#### seed側

- `db/physical_search_crud_aggregate_preset_seed.sql`
- `db/physical_details_inline_editor_md_generator_preset_seed.sql`

### 不整合詳細

- SSOT language policy requires plain business language and explicitly maps internal terms such as `topology`, `manifest`, `screen_data_shape`, `relationIntents`, `operationEntityBindings`, and `source_active_manifest_id` to user-facing vocabulary.
- Implementation still exposes `トポロジ`, `トポロジID`, `contents`, `manifest`, `active`, `DB`, `backend`, `componentKey`, `componentKind`, `layoutClassRefs`, `orderIndex`, and related internal terms in normal or near-normal authoring surfaces.
- Some details are under `details` / technical disclosure, but several labels and hints are primary authoring copy, not isolated integrator-only reference.
- Seed-visible preset text is English-first and includes implementation/system framing such as `Physical Search / CRUD / Aggregate preset seed`, `CRUD Surface`, `Search`, `Status`, `Add`, `Results`, `Emission debug`, `Record Details`, `Export PDF`, and `Field History`.

### 改善案

- Introduce a visible-label projection layer for admin authoring text:
  - `topology` -> `業務アプリの構造`
  - `manifest` -> `業務コンテンツ設定`
  - `route` -> `画面ルート` or `移動先画面`
  - `componentKey/componentKind` -> `部品ID/部品種別` only inside explicit advanced details
  - `layoutClassRefs/orderIndex` -> `配置スタイル/表示順`
- Keep raw keys as persisted values only; normal labels should use mapped Japanese copy.
- Move direct technical fields to an explicit `上級者向け / 技術情報` block and exclude them from the normal path.
- Localize preset seed user-visible `propsJson` labels/titles or add a separate user-facing label projection for preset load.
- Extend `NORMAL_VIEW_BANNED_TERMS` and source scanning to include Japanese/English variants currently escaping coverage: `トポロジ`, `マニフェスト`, `componentKey`, `componentKind`, `layoutClassRefs`, `orderIndex`, `Route`, `Primary Table`, `UI Builder Key`, `active`, `backend`, `DB`.

## Finding 3: UUID / raw identity leaks remain visible in relation and clone surfaces

### 対象SSOT

- `docs/design/user-facing-helper-manual-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`

### Section

- `admin_topology_clone_lifecycle_reference_contract.required_reference_fields_for_replacement_clone`
- `user_facing_message_policy.language_policy`
- `admin_contents_step2_5_relation_configuration`

### 不整合実装ファイル

#### 実装側

- `frontend/islands/ContentsScreenDesignPanel.tsx`
  - clone source technical details
  - relation intent summary line

#### seed側

- N/A: fixed frontend rendering of selected manifest identifiers.

### 不整合詳細

- Clone source options use friendly display labels, but expanded technical details render the full `cloneSourceId`.
- Step 2.5 relation summary renders `[active ${remoteManifestId.slice(0, 8)}…]` in the visible relation mapping.
- This creates user-visible raw identity leakage. It is not direct free-text UUID input, but it still violates the UX audit axis that users should not need to read/select raw UUID/system identifiers.

### 改善案

- Keep UUID values in select values and runtime payloads only.
- Render relation summaries by `remoteTargetLabel(...)` / user-facing page label, not UUID prefix.
- For technical disclosure, show `複製元ID` only on explicit copy action or collapsible integrator detail; avoid auto-visible raw UUID.
- Add a source guard for UUID regex in normal-view copy and rendered summaries, allowing only explicit technical detail blocks.

## Finding 4: visible English operator / abbreviated labels remain in admin selection UI

### 対象SSOT

- `docs/design/user-facing-helper-manual-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`

### Section

- `user_facing_message_policy.language_policy`
- `admin_contents_step3_physical_table_and_page_binding`
- `column_roles_contract`

### 不整合実装ファイル

#### 実装側

- `frontend/content/adminUxTerms.ts`
  - `AGGREGATION_FUNCTION_OPTIONS`
  - `SEARCH_OPERATOR_OPTIONS`
  - `LOGICAL_CONNECTOR_OPTIONS`
  - `UX_OPERATION_ENTITY_ROLE_RESPONSE`
  - `UX_OPERATION_ENTITY_ROLE_REQUEST`

#### seed側

- `db/physical_search_crud_aggregate_preset_seed.sql`
- `db/physical_details_inline_editor_md_generator_preset_seed.sql`

### 不整合詳細

- Selection labels expose raw or semi-raw English/SQL labels such as `sum`, `avg`, `like`, `ilike`, `between`, `in`, `is null`, `AND`, `OR`, `NOT`, `Res`, and `Req`.
- SSOT may preserve these as stored values or expert vocabulary, but the audit axis requires user-visible labels/explanations/operation copy not remain English-first.
- Seed preset labels are also English-first and would become visible after preset load unless rewritten or projected.

### 改善案

- Preserve raw operator values internally, but display Japanese labels first:
  - `like` -> `含む`
  - `ilike` -> `含む（大小文字を区別しない）`
  - `between` -> `範囲内`
  - `in` -> `リストに含まれる`
  - `is null` -> `空欄`
  - `AND/OR/NOT` -> `すべて満たす / いずれか満たす / 除外`
  - `Res/Req` -> `表示 / 入力`
- Keep raw tokens in `title` or technical tooltip only when needed.
- Add tests asserting user-facing labels for selection options do not equal raw stored values except symbol-only operators like `=` / `>`.

## Finding 5: test surface exists but does not fully lock the requested UX NG axes

### 対象SSOT

- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/user-facing-helper-manual-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`

### Section

- `test_surface_policy`
- `user_facing_message_policy.language_policy`
- `canvas_workspace_contract`
- Proposed addition: `canonical_admin_operator_workflow`

### 不整合実装ファイル

#### 実装側

- `frontend/tests/adminUxGuard.test.ts`
- `frontend/tests/adminMainFlow.test.ts`
- `frontend/tests/visualLayoutBuilder.test.ts`
- `frontend/tests/uiBuilderPackageWiring.test.ts`

#### seed側

- `db/physical_search_crud_aggregate_preset_seed.sql`
- `db/physical_details_inline_editor_md_generator_preset_seed.sql`

### 不整合詳細

- Existing tests provide useful structure guards for UiBuilder workspace, flow canvas, embedded import, column type labels, normal-view banned terms, guide copy, and admin route order.
- The current guard pattern excludes technical disclosures and scans source text, but it does not fully cover:
  - actual rendered component text after interpolation
  - UUID regex leakage in visible summaries
  - whole-admin Step 4 / Step 5 wording layer
  - seed `propsJson` user-visible English labels
  - operator label raw-value exposure
  - mouse travel / visual jump / hierarchy depth as measurable UX surface
- No CI log or Agent local-test report was available in this audit, so test execution success is not treated as evidence.

### 改善案

- Add static guards:
  - `ADMIN_MAIN_FLOW_STEPS` must preserve `/admin/contents -> /admin/ui-builder -> /admin/manifests`.
  - `/admin/ui-builder` may be `Step 4` only when qualified as whole-admin workflow / 全体工程.
  - `/admin/manifests` must be represented as whole-admin `Step 5` when Step 4 is shown.
  - wording must not imply `/admin/ui-builder` or `/admin/manifests` are `/admin/contents` local submit steps.
  - normal-view copy must not contain UUID regex outside explicit technical details.
  - `adminUxTerms` normal labels must not expose raw SQL/operator labels as primary labels.
  - preset seed `propsJson` visible labels must be localized or explicitly classified as non-user-facing draft placeholder.
- Add rendered tests for key admin surfaces:
  - Step 1 entry mode labels
  - Step 2.5 relation summary
  - Step 3 completion handoff
  - UI Builder selected-node inspector primary labels
  - UI Builder to manifests handoff / Step 5 label if exposed
- Add UX structure tests where possible:
  - no more than one nested disclosure level on normal authoring path
  - primary next action remains in same panel after save
  - save/handoff CTA appears near completion banner

## Finding 6: UI Builder event authoring needs large SSOT redesign before implementation

### 対象SSOT

- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/ui-builder-preset-ecosystem-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

### Section

- `ui_builder_canvas_workspace`
- `authoring_surface_reference_map`
- `per_component_wiring`
- `manifest_screenReadQueryWiring`
- `ui_builder_runtime_interaction_representative_scenario`
- Proposed addition: `ui_builder_canvas_mode_contract`
- Proposed addition: `ui_builder_layout_canvas_contract`
- Proposed addition: `ui_builder_wiring_canvas_contract`
- Proposed addition: `ui_builder_wiring_markmap_projection_contract`
- Proposed addition: `ui_builder_wiring_drag_drop_connection_edit_contract`
- Proposed addition: `runtimeInteractions_canonical_model_contract`
- Proposed addition: `ui_builder_mode_sensitive_inspector_contract`
- Proposed addition: `ui_builder_ui_event_authoring_contract`
- Proposed addition: `ui_builder_layout_design_authoring_contract`

### 不整合実装ファイル

#### 実装側

- `frontend/islands/UiBuilderAdmin.tsx`
- `frontend/components/FlowLayoutCanvas.tsx`
- `frontend/components/NodeEventAuthoringPanel.tsx`
- `frontend/components/ManifestStep3EventWiringPreset.tsx`
- `frontend/lib/runtimeInteractionAuthoring.ts`
- `frontend/lib/uiBuilderEventAuthoringHooks.ts`
- `frontend/runtime/visualLayoutUtils.ts`
- `frontend/runtime/renderEmission.ts`
- `frontend/runtime/runtimeComponentFactory.ts`
- `frontend/runtime/frontendScheduler.ts`
- Proposed: `frontend/components/WiringMarkmapCanvas.tsx`

#### test側

- `frontend/tests/runtimeUiInteractionScenario.test.ts`
- `frontend/tests/adminWiringExecutionLane.test.ts`
- `frontend/tests/visualLayoutBuilder.test.ts`
- `frontend/tests/uiBuilderPackageWiring.test.ts`

#### seed側

- preset seeds that create canvas layout nodes / wiring candidates / unresolved event bindings.

### 不整合詳細

- Existing implementation already has a Figma-like / mouse-driven layout canvas lineage inside `/admin/ui-builder`; this should be preserved as the layout canvas, not replaced.
- Existing `FlowLayoutCanvas` is already the layout projection surface for node selection, drag/drop intake, flow-layout preview, inlineText/link preview, calc/search callbacks, and inspector coordination.
- Existing layout patch serialization already persists `nodes[].runtimeInteractions`; this field should remain the canonical wiring model.
- Existing `NodeEventAuthoringPanel` already edits runtime interaction concepts such as `trigger`, `actionType`, `targetNodeId`, `statePath`, `payloadFrom`, `outputProp`, `portTargetRef`, and `instanceTargetRef`.
- Current SSOT does not clearly define a separate wiring authoring canvas for UI event wiring. As a result, API selection, UI state mutation, topology movement, external port dispatch, and instance operation dispatch are spread across PackageWiringEditor / RouteNavigationWiringPreset / NodeEventAuthoringPanel / runtime emission code.
- Owner direction is to redesign UI Builder as two switchable canvas modes:
  - layout canvas: existing Figma-like `FlowLayoutCanvas` lineage for placement, flow structure, css/responsive/text/link settings, props/state/propBindings, and layout_patch apply.
  - wiring canvas: Markmap-style / MindMap-style wiring projection that visualizes and edits `source UI node -> UI event -> setting category -> target/effect` from `draftNodes[].runtimeInteractions`.
- Markmap must be treated as projection/view, not semantic authority or persistence source.
- Drag/drop connection changes in the wiring canvas must update `draftNodes[].runtimeInteractions`, then rebuild the Markmap projection.
- Inspector content must switch by canvas mode:
  - layout mode: layout / design settings inspector.
  - wiring mode: UI event settings inspector.
- UI event settings should be defined as an explicit authoring model:
  - select UI event trigger: load / click / keyon / mouseon / etc.
  - configure via tabs: API設定 / 状態設定（監視変数設定）.
  - configure side effects: monitored variable assignment / output assignment / state mutation target.
  - configure topology movement: hub relation prev / next or explicit jump by selecting topology name.
- Layout settings should be defined as a separate authoring model:
  - CSS settings, including Tailwind or successor design-token/class vocabulary boundary.
  - responsive settings.
  - UI inlineText.
  - URL link settings including `target=_blank`.
- Current SSOT has runtime/test fragments for click/change/select and local state mutation, but does not define `load`, `keyon`, `mouseon`, topology movement, or lifecycle-trigger handling as first-class authoring triggers.
- `load` / initial display is not equivalent to DOM click/change. It needs a lifecycle lane with idempotency, preview inert behavior, route-enter timing, and refetch policy.
- `mouseon` / hover-style triggers need explicit policy before implementation because they may cause high-frequency dispatch, accidental external/API calls, excessive topology movement, or excessive component event logs.

### 改善案

- Treat this as `design_change` before `implementation_change`.
- Preserve the existing Figma-like `FlowLayoutCanvas` as the layout canvas implementation lineage.
- Add a large SSOT section for `ui_builder_canvas_mode_contract`:
  - mode: `layout_canvas`
    - purpose: placement / layout tree / design / css / responsive / inlineText / url link / propBindings / calculationBindings.
    - implementation lineage: existing `FlowLayoutCanvas` and `UiBuilderAdmin` layout/design inspector assets.
    - primary inspector: layout / design settings inspector.
    - persistence: layout_patch_json, design JSONB, tmp/apply boundary.
  - mode: `wiring_canvas`
    - purpose: UI event graph / API-state-topology-movement target wiring.
    - implementation lineage: new `WiringMarkmapCanvas` using existing canvas selection/drag-drop interaction assets where possible.
    - visual model: Markmap-style graph using source UI node -> event trigger -> settings tab -> target node/API/variable/topology movement.
    - primary inspector: UI event settings inspector.
    - persistence: `draftNodes[].runtimeInteractions` plus existing layout_patch_json serialization.
- Add `runtimeInteractions_canonical_model_contract`:
  - canonical source: `draftNodes[].runtimeInteractions` / `layout_patch_json.nodes[].runtimeInteractions`.
  - Markmap markdown/tree text is projection only.
  - Markmap AST / rendered SVG / rendered HTML must not become persistence authority.
  - drag/drop wiring edits must be translated into typed runtimeInteraction patches before save/apply.
  - unknown Markmap node kinds or unsupported edit targets must fail close.
- Add `ui_builder_wiring_markmap_projection_contract`:
  - projection input: selected route package draft nodes and their runtimeInteractions.
  - projection output: readable tree of UI node -> trigger -> API設定 / 状態設定 / 副作用設定 / トポロジ移動設定.
  - projection must preserve user-facing labels while keeping raw ids internal.
  - projection must rehydrate from runtimeInteractions, not from previously rendered markdown.
- Add `ui_builder_wiring_drag_drop_connection_edit_contract`:
  - drag source kinds: UI node, event trigger, action category, target/effect node.
  - drop target kinds: event trigger, API target, monitored variable, state target, topology movement target.
  - valid drop produces a typed runtimeInteraction patch.
  - invalid drop shows explicit error and does not mutate draft.
  - connection edits must be undoable through existing draft state before explicit apply.
- Add `ui_builder_mode_sensitive_inspector_contract`:
  - layout mode shows layout tree, CSS settings, responsive settings, inlineText, URL link target settings, sizing, spacing, propBindings, calculation bindings.
  - wiring mode shows selected event node, trigger selector, API設定 tab, 状態設定 tab, side-effect assignment, topology movement settings, target selector, payloadFrom, outputProp, preview/inert status.
  - inspectors must not expose unrelated controls for the inactive mode in the primary path.
  - existing `NodeEventAuthoringPanel` should be upgraded into the wiring inspector path, not discarded.
- Add `ui_builder_ui_event_authoring_contract`:
  - trigger vocabulary:
    - lifecycle: load / route_enter / initial_display.
    - pointer: click / mouseon / mouseout / hover_start / hover_end.
    - keyboard: keyon / keydown / keyup / enter / escape.
    - form: input / change / select / submit / focus / blur.
  - UI event settings tabs:
    - API設定: contents Step 3 API / manifest screenReadQueryWiring / external port / instance operation.
    - 状態設定（監視変数設定）: local UI state mutation / monitored variable set / toggle / clear.
  - side effects:
    - monitored variable assignment.
    - outputProp assignment.
    - targetNode state assignment.
    - explicit no-side-effect option.
  - topology movement settings:
    - hub relation prev.
    - hub relation next.
    - explicit jump by selecting topology name.
    - movement target must be selected by user-facing topology label/name, while raw topology identifiers remain internal.
- Add `ui_builder_layout_design_authoring_contract`:
  - CSS settings:
    - Tailwind or successor class/token vocabulary must be explicitly bounded by SSOT.
    - raw class string editing must be advanced-only unless SSOT allows it as normal-view vocabulary.
  - responsive settings:
    - breakpoint vocabulary and per-breakpoint behavior must be explicit.
  - UI inlineText:
    - normal text editing must reflect immediately on layout canvas preview.
  - URL link:
    - href and target settings must be explicit.
    - `target=_blank` must include security/UX policy such as rel handling if projected to anchor output.
- Define lifecycle trigger policy:
  - `load` / `initial_display` must not be implemented as synthetic click/change.
  - preview mode must be inert by default.
  - backend dispatch from load must require explicit author confirmation and idempotency rule.
  - route-enter reload behavior must be explicit.
- Define high-frequency trigger policy:
  - `mouseon` / hover / key repeat must not dispatch backend or external port by default.
  - high-frequency triggers may update local state / monitored variables by default.
  - backend/API dispatch or topology movement from high-frequency triggers requires debounce/throttle and explicit warning.
- Define relation to existing contents Step 3:
  - contents Step 3 produces API/action candidates.
  - wiring canvas selects those candidates per UI node event, not only at package level.
  - package-level wiring remains available as default/bulk wiring, but node-level event wiring is the normal explanation surface for user-facing behavior.
- Add proof surface requirements before implementation:
  - static SSOT guard for trigger vocabulary membership.
  - layout canvas / wiring canvas mode contract test.
  - inspector mode-switch visibility test.
  - runtimeInteractions -> Markmap projection snapshot test.
  - Markmap projection -> typed runtimeInteraction patch test.
  - wiring graph serialization round-trip test.
  - drag/drop connection edit valid/invalid drop tests.
  - topology movement target serialization / label projection test.
  - layout CSS / responsive / inlineText / URL link projection test.
  - lifecycle load trigger inert preview test.
  - high-frequency trigger debounce/fail-close test.
  - contents Step 3 API candidate -> wiring canvas -> renderEmission -> runtimeComponentFactory scenario test.

### Blocker classification

- This is not a small wording or inspector refactor.
- This should be treated as a large SSOT redesign bundle.
- Implementing `load`, `keyon`, `mouseon`, visual wiring canvas behavior, topology movement setting, or layout design setting expansion before SSOT is defined would violate `SSOT -> wiring -> test/proof surface -> implementation`.
- Existing Figma-like layout canvas assets must not be discarded or reimplemented without need.
- Markmap must not become the canonical persistence model; it is projection over `runtimeInteractions`.
- Existing click/change/select runtime interaction tests remain useful evidence, but they are not sufficient for the proposed wiring canvas, lifecycle/high-frequency event model, topology movement model, Markmap projection/edit model, and layout/design authoring model.

## Finding 7: non-canonical admin/debug/support routes should become initial seed projection data

### 対象SSOT

- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- Proposed: projection-place registry / initial-seed projection contract

### Section

- `frontend_routes.public`
- `frontend_routes.admin`
- `admin_console_workflow_ssot.authority.canonical_routes`
- `ADMIN_ROUTE_CARDS`
- Proposed addition: `canonical_projection_place_boundary`
- Proposed addition: `initial_seed_projection_surface_contract`

### 不整合実装ファイル

#### 実装側

- `frontend/fresh.gen.ts`
- `frontend/routes/admin/enums.tsx`
- `frontend/routes/admin/users.tsx`
- `frontend/routes/admin/team-dashboard/index.tsx`
- `frontend/routes/admin/scheduler.tsx`
- `frontend/routes/demo.tsx`
- `frontend/routes/runtime-status.tsx`
- `frontend/content/adminGuides.ts`

#### seed側

- Initial seed files that define app settings, roster defaults, scheduler defaults, preview/demo defaults, runtime status defaults, and projection app configuration.

### 不整合詳細

- Owner direction: the following routes are not canonical admin workflow pages and should not remain standalone admin/debug/support projection places:
  - `/admin/enums`
  - `/admin/users`
  - `/admin/team-dashboard`
  - `/admin/scheduler`
  - `/demo`
  - `/runtime-status`
- These concerns should be represented as initial seed / projection-app setting data where needed, not as top-level admin workflow pages.
- `/admin/enums`, `/admin/users`, `/admin/team-dashboard`, and `/admin/scheduler` are app-side settings / roster / dashboard / scheduler configuration concerns. They belong to the projected app's seeded configuration surface, not the admin authoring workflow route set.
- `/demo` is a preview/projection runtime concern. If it remains useful, it should be backed by seeded preview/projection data and gated as preview, not treated as a canonical page route.
- `/runtime-status` is debug/ops vocabulary and should not be a user/admin-visible canonical route. Required runtime status defaults or health indicators should be modeled as seed/projection data or ops-only internal diagnostics, not as a normal frontend route.
- Current route surfaces therefore mix three different meanings:
  - canonical auth/admin authoring projection places.
  - projection-app seeded settings.
  - debug/preview/ops surfaces.
- This mix weakens SSOT authority because `admin-console-workflow-ssot` is intended to describe the authoring flow, while these routes describe seeded app configuration or debug status.

### 改善案

- Keep canonical projection places limited to:
  - `/`
  - `/auth`
  - `/super_auth`
  - `/admin`
  - `/admin/contents`
  - `/admin/ui-builder`
  - `/admin/manifests`
- Remove or reclassify the following from canonical route registries and admin route cards:
  - `/admin/enums`
  - `/admin/users`
  - `/admin/team-dashboard`
  - `/admin/scheduler`
  - `/demo`
  - `/runtime-status`
- Convert the removed route concerns into initial seed data:
  - enum groups / items -> seeded app setting tables or projection settings.
  - users / roles / status defaults -> seeded app roster / auth projection settings.
  - team dashboard defaults -> seeded app dashboard projection configuration.
  - scheduler defaults -> seeded scheduler configuration / projection app settings, not admin workflow page.
  - demo/preview defaults -> seeded preview/projection configuration.
  - runtime status defaults -> seeded/internal ops diagnostics if needed; no normal route projection.
- Add SSOT boundary:
  - `admin-console-workflow-ssot` owns flow only.
  - route registry owns canonical projection places only.
  - projection app settings are seeded data, not route authority.
  - debug/ops diagnostics are not user/admin canonical routes.
- Add migration/todo requirement before implementation:
  - identify each route's current data responsibility.
  - identify target seed file or create seed contract.
  - remove route card and navigation exposure only after seed replacement exists.
  - leave no orphan API dependency or broken navigation link.

### Blocker classification

- This is a design_change before route deletion.
- Direct deletion without seed replacement is prohibited.
- Keeping the routes as canonical admin workflow surfaces is prohibited.
- `/runtime-status` must not remain a normal frontend projection route under canonical route authority.
- If a route is temporarily retained during migration, it must be explicitly classified as deprecated / dev-only / migration-only and excluded from the canonical admin workflow.
