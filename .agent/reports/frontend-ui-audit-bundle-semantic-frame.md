# Frontend UI Audit Bundle Semantic Frame

- Date: 2026-07-06
- Target repo: github.com/tk-ud/topolactor
- Worktype: audit / design_correction
- Purpose: integrated planning view by owning SSOT bundle.
- Retained audit evidence:
  - `.agent/reports/frontend-ui-audit.md`
  - `.agent/reports/frontend-ui-audit-finding7-correction.md`
- Cleanup rule: retain source/correction reports during audit. Delete auxiliary correction only after audit close.

## Global rule

```text
Bundle:
  owning SSOT unit

Bundle group:
  multiple owning SSOT units listed together for audit planning only

Not Bundle:
  finding number
  implementation phase
  route deletion batch
  test-only batch

Design order:
  SSOT -> wiring -> proof surface -> implementation
```

## Surface taxonomy

```text
projection_entry:
  /

gate:
  /auth
  /super_auth

topolactor_projection_authoring_settings:
  /admin
  /admin/contents
  /admin/ui-builder
  /admin/manifests

initial_projection_side_admin_crud_seed:
  credentials auth / external api / external instance
  enum CRUD
  user / role / status CRUD
  dashboard configuration CRUD
  scheduler configuration CRUD

non_canonical_hardcoded_routes_to_remove:
  /admin/enums
  /admin/users
  /admin/team-dashboard
  /admin/scheduler
  /demo
  /runtime-status
```

## Bundle: runtime-orchestration-ssot

```text
owns:
  canonical frontend route authority
  route taxonomy
  projection_entry / gate / admin_authoring_settings boundary

required:
  keep canonical routes only:
    /
    /auth
    /super_auth
    /admin
    /admin/contents
    /admin/ui-builder
    /admin/manifests

OK:
  /auth and /super_auth are gates
  /admin routes are authoring/settings surfaces
  non-canonical routes are absent from canonical route registry

NG:
  /auth or /super_auth classified as projection pages
  /admin classified as business projection
  non-canonical routes kept as canonical route authority
```

## Bundle: admin-console-workflow-ssot

```text
owns:
  Topolactor admin authoring workflow
  /admin/contents -> /admin/ui-builder -> /admin/manifests
  Step wording layer

required:
  /admin/contents = local submit pipeline Step 1-3
  /admin/ui-builder = whole-admin Step 4
  /admin/manifests = whole-admin Step 5

OK:
  contents -> ui-builder -> manifests remains valid
  Step 4/5 are qualified as whole-admin workflow

NG:
  removing contents -> ui-builder -> manifests flow
  calling /admin/ui-builder /admin/contents local Step 4
  omitting whole-admin Step 5 for /admin/manifests
```

## Bundle group: frontend surface UI structure/wiring SSOTs

```text
individual_bundles:
  root_projection_ui_structure_wiring_ssot: /
  auth_gate_ui_structure_wiring_ssot: /auth
  superauth_gate_ui_structure_wiring_ssot: /super_auth
  admin_index_ui_structure_wiring_ssot: /admin
  admin_contents_ui_structure_wiring_ssot: /admin/contents
  admin_uibuilder_ui_structure_wiring_ssot: /admin/ui-builder
  admin_manifests_ui_structure_wiring_ssot: /admin/manifests

shared_owns:
  UI structure and wiring per canonical surface
  visible labels
  normal/technical disclosure boundary
  seed/input boundary
  proof surface per surface

label_boundary:
  raw values remain persistence/internal values
  normal labels use user-facing projection vocabulary
  raw ids/UUIDs route/page refs are not normal-view meaning

normal_view_raw_terms_to_map_or_hide:
  topology
  manifest
  screen_data_shape
  relationIntents
  operationEntityBindings
  source_active_manifest_id
  active
  DB
  backend
  componentKey
  componentKind
  layoutClassRefs
  orderIndex
  Route
  Primary Table
  UI Builder Key
  UUID/raw id

operator_label_boundary:
  stored operators may remain raw
  visible labels must not be raw-first:
    like -> 含む
    ilike -> 含む（大小文字を区別しない）
    between -> 範囲内
    in -> リストに含まれる
    is null -> 空欄
    AND/OR/NOT -> すべて満たす / いずれか満たす / 除外
    Res/Req -> 表示 / 入力

seed_visible_label_boundary:
  preset seed propsJson visible labels are user-facing or explicitly draft/technical
  English-first labels are not normal-view authority

OK:
  each canonical surface has owning UI structure/wiring SSOT
  implementation/test maps to that SSOT
  raw ids stay internal or explicit technical disclosure

NG:
  using implementation as SSOT
  placing this audit scope under ui-builder-preset-ecosystem-ssot
  mixing projection, gate, admin settings, and seed CRUD in one category
  normal path exposes raw technical vocabulary as meaning
```

## Bundle: initial-projection-side-admin-crud-seed-ssot

```text
owns:
  credentials auth / external api / external instance
  enum CRUD
  user / role / status CRUD
  dashboard configuration CRUD
  scheduler configuration CRUD

does_not_own:
  hardcoded frontend routes
  /demo seed replacement
  /runtime-status diagnostics replacement
  UI Builder persistence model

required:
  /admin/enums -> remove route; replace by enum CRUD seed
  /admin/users -> remove route; replace by user / role / status CRUD seed
  /admin/team-dashboard -> remove route; replace by dashboard configuration CRUD seed
  /admin/scheduler -> remove route; replace by scheduler configuration CRUD seed
  /demo -> remove route; no seed replacement
  /runtime-status -> remove route; no diagnostics seed replacement

OK:
  seed CRUD exists before route removal
  seeded CRUD renders through canonical projection/admin mechanism
  /demo and /runtime-status are removed without seed fallback

NG:
  route deletion before replacement seed for required CRUD responsibilities
  moving /demo into seed
  moving /runtime-status diagnostics into seed
  leaving old route tests as authority
```

## Bundle: admin-uibuilder-ui-structure-wiring-ssot

```text
owns:
  /admin/ui-builder UI structure
  layout canvas / wiring canvas boundary
  UI event settings
  runtimeInteractions authoring
  external capability selection from seed registry

layout_mode:
  preserve existing Figma-like layout canvas lineage
  owns placement / css / responsive / inlineText / URL link / props / propBindings / calculationBindings
  primary inspector = layout / design settings

wiring_mode:
  UI event graph projection
  visual model = source UI node -> event trigger -> setting category -> target/effect
  primary inspector = UI event settings
  persistence authority = draftNodes[].runtimeInteractions / layout_patch_json.nodes[].runtimeInteractions

Markmap_policy_if_used:
  projection/view only
  not semantic authority
  not persistence source
  rehydrate from runtimeInteractions

trigger_vocabulary:
  lifecycle: load / route_enter / initial_display
  pointer: click / mouseon / mouseout / hover_start / hover_end
  keyboard: keyon / keydown / keyup / enter / escape
  form: input / change / select / submit / focus / blur

UI_event_settings:
  API設定:
    contents Step 3 API candidates
    screenReadQueryWiring candidates
    external api candidates
    external instance candidates
  状態設定:
    local UI state mutation
    monitored variable set / toggle / clear
  authority:
    credential / authority requirement candidate
  side_effects:
    monitored variable assignment
    outputProp assignment
    targetNode state assignment
    explicit no-side-effect
  topology_movement:
    hub relation prev
    hub relation next
    explicit jump by user-facing topology label/name
    raw topology ids stay internal

external_event_candidates:
  registered external api from initial CRUD seed
  registered external instance from initial CRUD seed
  registered credential authority from initial CRUD seed
  selected target writes typed runtimeInteraction reference

lifecycle_policy:
  load / initial_display is not synthetic click/change
  preview is inert by default
  backend dispatch from load requires explicit author confirmation
  load dispatch needs idempotency and route-enter/refetch policy

high_frequency_policy:
  mouseon / hover / key repeat must not dispatch backend/external calls by default
  local state / monitored variable update is allowed by default
  backend/API dispatch or topology movement requires debounce/throttle and explicit warning

drag_drop_wiring_edit:
  valid drop -> typed runtimeInteraction patch
  invalid drop -> explicit error and no draft mutation
  edit is draft/undoable before apply

OK:
  registered external api / external instance are selectable in UI Builder event settings
  candidates come from canonical seed-backed registry/projection admin mechanism
  runtimeInteractions store typed references
  Markmap/wiring projection is view only if used

NG:
  hardcoded external api / instance choices in UI Builder
  raw route/page references written as event wiring
  event settings unable to use registered external capabilities
  Markmap/rendered graph treated as persistence authority
  lifecycle/high-frequency triggers implemented before policy
```

## Bundle: pipeline-continuity-ssot

```text
owns:
  proof surface policy
  local test expectation
  replacement tests for route removal and seed CRUD renderability

target_test_files:
  frontend/tests/adminUxGuard.test.ts
  frontend/tests/adminMainFlow.test.ts
  frontend/tests/visualLayoutBuilder.test.ts
  frontend/tests/uiBuilderPackageWiring.test.ts
  frontend/tests/runtimeUiInteractionScenario.test.ts
  frontend/tests/adminWiringExecutionLane.test.ts

required_proof:
  route registry contains canonical routes only
  non-canonical hardcoded routes are absent
  required initial projection-side admin CRUD seed exists
  seeded CRUD renders through canonical projection/admin mechanism
  registered external api / external instance selectable in UI Builder event settings
  selected capability writes typed runtimeInteraction
  normal labels do not expose raw ids / UUIDs / internal vocabulary
  whole-admin Step 4/5 wording is qualified
  runtimeInteractions -> wiring projection round-trip
  valid/invalid drag-drop wiring edit
  topology movement target label projection
  lifecycle load trigger inert preview
  high-frequency trigger debounce/fail-close

OK:
  tests follow owning SSOT
  old route-presence tests replaced by seed/render/wiring proof
  test deletion is paired with replacement proof

NG:
  test-only deletion
  old tests asserting seed-migrated routes as canonical
  completion claim without agent-ui-local-test or routed fallback checks
```
