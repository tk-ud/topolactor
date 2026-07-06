# Frontend UI Audit Bundle Semantic Frame

- Date: 2026-07-06
- Target repo: github.com/tk-ud/topolactor
- Worktype: audit / design_correction
- Purpose: integrated planning view by owning SSOT bundle.
- Active report: this file.
- Superseded reports removed after audit close:
  - `.agent/reports/frontend-ui-audit.md`
  - `.agent/reports/frontend-ui-audit-finding7-correction.md`

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

lineage_boundary:
  current reusable canvas lineage = FlowLayoutCanvas
  historical VisualLayoutCanvas direct-manipulation lineage was replaced before this report scope
  historical VisualLayoutCanvas restoration is not required by this Bundle
  implementation must not treat old Figma-like wording as current blocking authority

layout_mode:
  preserve current FlowLayoutCanvas layout canvas lineage
  owns placement / css / responsive / inlineText / URL link / props / propBindings / calculationBindings
  primary inspector = layout / design settings

existing_canvas_reuse_policy:
  reuse existing FlowLayoutCanvas / drag-drop interaction assets where compatible
  do not replace the existing layout canvas with a separate authority
  wiring canvas is a switchable projection/edit mode over runtimeInteractions
  new wiring components are allowed only as view/edit projection, not persistence authority
  existing drag/drop connection edit assets are the preferred adaptation path before new interaction implementation

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
  rule:
    stored values may remain raw identifiers
    visible labels use user-facing projection vocabulary
    report / SSOT / test must keep the same trigger group mapping

UI_event_settings:
  event_trigger:
    source UI node emits trigger vocabulary event
    trigger UI and target/effect axes are independent

  frontend_side:
    UI監視割当:
      defines state slot / monitored variable / watched dependency source
      useState-like binding, not a mutation
      examples: stateKey / initialValue / valueType / scope / watchedBy / readableFrom
    UI状態更新:
      writes to an already-declared state slot / monitored variable
      setState-like mutation
      examples: set / toggle / clear / open / close / statePath write
    副作用設定:
      useEffect-like reaction settings
      trigger source may be UI event / watched state slot / monitored variable / outputProp / targetNode state
      write target may be local state slot / monitored variable / outputProp / targetNode state / API result binding
      payloadFrom / outputProp / targetNode state assignment are effect fields, not the effect authority itself
      explicit no-side-effect must be selectable

  backend_side:
    内部API:
      contents Step 3 API candidates
      screenReadQueryWiring candidates
      internal topology/API dispatch candidates
    外部API連携:
      external api candidates from seed-backed external port registry
      selected target writes typed dispatchExternalPort runtimeInteraction reference
    外部インスタンス連携:
      external instance candidates from seed-backed instance operation registry
      selected target writes typed dispatchInstanceOperation runtimeInteraction reference

  authority:
    credential / authority requirement candidate is displayed from selected capability record
    credential material is never editable in UI Builder

  topology_movement:
    hub relation prev
    hub relation next
    explicit jump by user-facing topology label/name
    raw topology ids stay internal

component_runtime_state_effect_boundary:
  state/effect authoring does not require ad-hoc useEffect code inside every component
  required runtime boundary:
    runtime component wrapper / factory owns useEffect-like effect runner
    primitive components expose event/value surfaces and remain mostly pure renderers
    state slots live in projection-local state store or equivalent declared runtime state store
    UI監視割当 declares state slot / dependency source before mutation or effect selection
    UI状態更新 writes only through runtime state dispatcher
    副作用設定 resolves dependency graph before execution and runs through effect runner
  component edit boundary:
    edit individual component only when required event/value surface is missing
    unsupported component kind must fail-close in proof tests
    component-specific state/effect implementation is exception, not default path
  required receive surfaces:
    current state values
    mutation dispatcher
    effect binding metadata
    dispatch lanes for internal API / external API / external instance
  NG:
    embedding ad-hoc useEffect in every component factory
    executing effects from rendered graph persistence authority
    writing component state directly outside declared runtime state dispatcher
    treating propsJson/stateJson as sufficient UI監視割当 proof
    treating event -> localStateMutation as sufficient 副作用設定 proof

side_effect_cycle_policy:
  effect target candidates must be filtered by dependency not-in rule before selection
  selectable_write_targets = all_write_targets - dependency_closure(trigger_source)
  direct self-loop is prohibited
  indirect loop must fail-close when dependency graph can detect it
  debounce/throttle is not loop-safety proof
  NG examples:
    watched A -> set A
    watched A -> outputProp A
    watched A -> API result writes A
    A -> B and B -> A without explicit cycle break

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
  debounce/throttle does not prove side-effect loop safety

drag_drop_wiring_edit:
  valid drop -> typed runtimeInteraction patch
  invalid drop -> explicit error and no draft mutation
  edit is draft/undoable before apply

OK:
  current FlowLayoutCanvas / drag-drop interaction assets are preserved and reused where compatible
  wiring graph / Markmap is projection/view only if used
  runtimeInteractions store typed references
  UI event settings use report vocabulary, not implementation-derived catch-all categories
  frontend side separates UI監視割当 / UI状態更新 / 副作用設定
  backend side separates 内部API / 外部API連携 / 外部インスタンス連携
  registered external api / external instance are selectable in UI Builder event settings
  candidates come from canonical seed-backed registry/projection admin mechanism
  runtime wrapper owns useEffect-like effect execution boundary
  primitive components remain mostly pure unless event/value surface is missing
  side-effect write target candidates exclude dependency closure of trigger source
  tests assert the report vocabulary and fail if implementation collapses categories

NG:
  discarding existing canvas / drag-drop interaction assets without SSOT reason
  replacing the existing layout canvas with a new persistence authority
  reviving historical VisualLayoutCanvas restoration as current PR574 blocker
  using implementation categories as SSOT taxonomy
  collapsing UI監視割当 / UI状態更新 / 副作用設定 into legacy or 状態設定
  treating monitored variable assignment as both binding and mutation without explicit distinction
  hiding 内部API outside the wiring inspector while claiming backend side completion
  hardcoded external api / instance choices in UI Builder
  external api / external port / 外部連携 terminology drift
  external instance / instance operation / インスタンス操作 terminology drift
  raw route/page references written as event wiring
  event settings unable to use registered external capabilities
  Markmap/rendered graph treated as persistence authority
  lifecycle/high-frequency triggers implemented before policy
  side-effect target candidates allowing direct or detectable indirect loops
  treating debounce/throttle as loop-safety proof
  requiring ad-hoc useEffect insertion in every component as default implementation strategy
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
  frontend/tests/uiBuilderWiringProjection.test.ts

required_proof:
  route registry contains canonical routes only
  non-canonical hardcoded routes are absent
  required initial projection-side admin CRUD seed exists
  seeded CRUD renders through canonical projection/admin mechanism
  registered external api / external instance selectable in UI Builder event settings
  selected capability writes typed runtimeInteraction
  normal labels do not expose raw ids / UUIDs / internal vocabulary
  whole-admin Step 4/5 wording is qualified
  current FlowLayoutCanvas / drag-drop reuse boundary is preserved or explicitly justified by SSOT
  runtimeInteractions -> wiring projection round-trip
  valid/invalid drag-drop wiring edit
  UI event setting taxonomy matches report vocabulary
  frontend side separates UI監視割当 / UI状態更新 / 副作用設定
  backend side separates 内部API / 外部API連携 / 外部インスタンス連携
  component runtime wrapper owns useEffect-like effect runner boundary
  primitive components expose required event/value surfaces without ad-hoc per-component effect ownership
  state slot declaration exists before mutation/effect selection
  effect target candidate filtering excludes dependency closure of trigger source
  direct side-effect self-loop fails close
  detectable indirect side-effect loop fails close
  topology movement target label projection
  lifecycle load trigger inert preview
  high-frequency trigger debounce/fail-close

OK:
  tests follow owning SSOT
  old route-presence tests replaced by seed/render/wiring proof
  test deletion is paired with replacement proof
  test vocabulary rejects implementation-derived catch-all categories
  tests reject ad-hoc component-local effect ownership as default strategy

NG:
  test-only deletion
  old tests asserting seed-migrated routes as canonical
  completion claim without agent-ui-local-test or routed fallback checks
  tests accepting legacy / 状態設定 catch-all as complete UI event taxonomy
  tests accepting debounce/throttle as side-effect loop-safety proof
  tests accepting event -> localStateMutation as sufficient UI監視割当 / 副作用設定 proof
```
