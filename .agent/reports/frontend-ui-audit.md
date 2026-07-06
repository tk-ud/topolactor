# Frontend UI Audit

- Date: 2026-07-06
- Target repo: github.com/tk-ud/topolactor
- Worktype: audit
- Scope: admin / projection frontend UI, UX wording, selection surfaces, seed-visible labels, and test surface.
- Evidence boundary: GitHub read fallback audit. `.agent/tools` runtime was not executable from this chat. No CI log or Agent local-test result was available, so test execution success is not used as evidence.
- Revision note: Finding 1 was revised after owner review. `contents -> ui-builder -> manifests` is a valid operator workflow. The remaining issue is the missing SSOT wording layer that distinguishes `/admin/contents` local submit steps 1-3 from the whole-admin operator workflow stages 4-5.

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
