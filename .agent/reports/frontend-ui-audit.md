# Frontend UI Audit

- Date: 2026-07-06
- Target repo: github.com/tk-ud/topolactor
- Worktype: audit
- Scope: admin / projection frontend UI, UX wording, selection surfaces, seed-visible labels, and test surface.
- Evidence boundary: GitHub read fallback audit. `.agent/tools` runtime was not executable from this chat. No CI log or Agent local-test result was available, so test execution success is not used as evidence.

## Finding 1: `/admin/contents` frames UI Builder handoff as `Step 4`

### 対象SSOT

- `docs/design/admin-console-workflow-ssot.yaml`

### Section

- `purpose.summary`
- `canonical_sequential_authoring_pipeline`
- `ui_builder_canvas_workspace`

### 不整合実装ファイル

#### 実装側

- `frontend/islands/ContentsScreenDesignPanel.tsx`
- Section: Step 3 completion banner / UI Builder handoff

#### seed側

- N/A: fixed frontend copy, not seed-derived.

### 不整合詳細

- SSOT states `/admin/contents` owns steps 1-3, and `/admin/ui-builder` is a standalone canvas workspace route, not a post-pipeline numbered step.
- Implementation renders `保存済み — 次は Step 4 です` and links to `/admin/ui-builder`.
- This creates a pipeline-step framing that makes UI Builder look like Step 4 of `/admin/contents`, despite the SSOT separating it as the canvas workspace route.

### 改善案

- Replace `保存済み — 次は Step 4 です` with business-facing non-numbered copy such as `保存済み — 次は画面づくりへ進めます`.
- Keep the CTA near the Step 3 save result, but do not label UI Builder as Step 4.
- Add/extend a guard test that scans `ContentsScreenDesignPanel.tsx` for `Step 4` and fails when `/admin/ui-builder` handoff is framed as a numbered contents step.

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

### 不整合実装ファイル

#### 実装側

- `frontend/tests/adminUxGuard.test.ts`
- `frontend/tests/visualLayoutBuilder.test.ts`
- `frontend/tests/uiBuilderPackageWiring.test.ts`

#### seed側

- `db/physical_search_crud_aggregate_preset_seed.sql`
- `db/physical_details_inline_editor_md_generator_preset_seed.sql`

### 不整合詳細

- Existing tests provide useful structure guards for UiBuilder workspace, flow canvas, embedded import, column type labels, normal-view banned terms, and guide copy.
- The current guard pattern excludes technical disclosures and scans source text, but it does not fully cover:
  - actual rendered component text after interpolation
  - UUID regex leakage in visible summaries
  - `Step 4` handoff framing
  - seed `propsJson` user-visible English labels
  - operator label raw-value exposure
  - mouse travel / visual jump / hierarchy depth as measurable UX surface
- No CI log or Agent local-test report was available in this audit, so test execution success is not treated as evidence.

### 改善案

- Add static guards:
  - `ContentsScreenDesignPanel` must not contain `/admin/ui-builder` handoff text with `Step 4`
  - normal-view copy must not contain UUID regex outside explicit technical details
  - `adminUxTerms` normal labels must not expose raw SQL/operator labels as primary labels
  - preset seed `propsJson` visible labels must be localized or explicitly classified as non-user-facing draft placeholder
- Add rendered tests for key admin surfaces:
  - Step 1 entry mode labels
  - Step 2.5 relation summary
  - Step 3 completion handoff
  - UI Builder selected-node inspector primary labels
- Add UX structure tests where possible:
  - no more than one nested disclosure level on normal authoring path
  - primary next action remains in same panel after save
  - save/handoff CTA appears near completion banner
