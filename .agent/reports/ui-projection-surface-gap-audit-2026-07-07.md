# Audit Memo: UI projection surface gap

## Semantic Frame

```text
repo:
  github.com/tk-ud/topolactor

base_lineage:
  branch: main
  action: audit_memo_only
  source_change: none
  report_path: .agent/reports/ui-projection-surface-gap-audit-2026-07-07.md
  parent_active_report: .agent/reports/frontend-ui-audit-bundle-semantic-frame.md

lineage_rule:
  this memo is subordinate evidence for the active frontend UI audit bundle report.
  this memo does not supersede the active report.
  this memo must not create a parallel lineage.
  this memo must not revive /demo as a canonical route.

status:
  audit_in_progress

implemented:
  no

partial:
  yes

blocking_summary:
  UI projection confirmation gap spans both demo and production surfaces.
  /demo is mostly canvas/layout draft preview and remains non-canonical under the active report lineage.
  production ProjectionShell is product projection surface, but initial dispatch is default-bound.
  Therefore UI Builder authored/applied topology is not sufficiently confirmable as user-facing UI projection before real production use.
```

## Active Report Lineage

```text
active_report:
  .agent/reports/frontend-ui-audit-bundle-semantic-frame.md

active_report_status:
  Active report: this file

active_report_route_taxonomy:
  projection_entry:
    /

  canonical_admin_authoring_settings:
    /admin
    /admin/contents
    /admin/ui-builder
    /admin/manifests

  non_canonical_hardcoded_routes_to_remove:
    /demo
    /runtime-status
    /admin/enums
    /admin/users
    /admin/team-dashboard
    /admin/scheduler

active_report_demo_policy:
  /demo -> remove route; no seed replacement
  /demo must not move into seed

lineage_correction:
  This memo describes the projection-confirmation gap observed around existing /demo and production ProjectionShell.
  It does not redefine /demo as canonical.
  It does not request a /demo seed.
  It does not request preserving /demo as a route.
  Required future surface must follow the active report route taxonomy and owning SSOT.
```

## Read Scope

```text
entry_contract:
  AGENTS.md

active_report:
  .agent/reports/frontend-ui-audit-bundle-semantic-frame.md

SSOT:
  docs/design/ui-builder-preset-ecosystem-ssot.yaml
    - cross_preset_authoring_boundary
    - tmp_draft_boundary
    - explicit_apply_boundary
    - author_resolution_map
    - package_wiring_manifest_bridge

  docs/design/pipeline-continuity-ssot.yaml
    - api_command_lane
    - frontend.projection
    - pipeline_body_test
    - frontend_renderEmission_and_runtimeComponentFactory_tests

implementation:
  frontend/islands/DraftPreviewShell.tsx
    - DraftPreviewShell
    - draftPreviewResultToEmission
    - renderEmission previewMode path
    - initialDataRows first-row slot display

  frontend/islands/ProjectionShell.tsx
    - ProjectionShell
    - initialDispatchAxes
    - data-projection-surface="product"
    - SSE refresh identity forwarding

  frontend/runtime/projectionInput.ts
    - projectionInputFromData

  backend/runtime/ScreenDataShapeQueryRuntime.cs
    - screen_data_shape_query_result payload
```

## SSOT Contract

```text
ui_builder_authoring_surface:
  role:
    horizontal projection / authoring / navigation aid

  not:
    convergence governance
    promotion judgment authority
    DB direct write authority

preset_seed_and_mock_compiler_output:
  role:
    draft/intake artifact
    pre-populate canvas
    pre-populate wiring candidate list
    pre-populate style candidates
    pre-populate unresolved_json

  active_topology_mutation:
    forbidden until explicit UIBuilder save/apply

tmp_draft_boundary:
  layout_and_node_data:
    layout_patch:save_tmp -> topology.ui_topology_tensor.layout_draft_tmp_json

  design_data:
    component_style_design:save_tmp -> topology.components_style_design.design_draft_tmp_json

explicit_apply_boundary:
  layout_and_bindings:
    layout_patch:validate -> layout_patch:apply -> topology.ui_topology_tensor.layout_patch_json

  design:
    component_style_design:upsert -> topology.components_style_design.design JSONB

  package_wiring:
    ui_topology:update_package_wiring -> topology.ui_wiring_registry

projection_authoring_refs:
  manifest_read_query_targetRef:
    ui_surface: PackageWiringEditor
    save_action: ui_topology:update_package_wiring
    persisted_field: topology.ui_wiring_registry.target_ref

  propBindings:
    ui_surfaces:
      - Design Inspector propBindings tab
      - AuthoringSuggestAssistPanel
    candidates: emission.data branches from saved projection/read-query output shape
    save_action: layout_patch:apply
    persisted_field: topology.ui_topology_tensor.layout_patch_json.nodes[].propBindings

  layout_structure:
    ui_surfaces:
      - Canvas
      - Layout Inspector
    save_action: layout_patch:apply
    persisted_field: topology.ui_topology_tensor.layout_patch_json.nodes[]
```

## Expected Projection Surface Split

```text
canvas_preview:
  purpose:
    authoring confirmation
    layout structure confirmation
    slot/topology intent confirmation
    draft/intake confirmation

  acceptable_location:
    UI Builder authoring flow

pre_production_ui_projection_confirmation:
  purpose:
    production-equivalent UI projection confirmation before real production use

  route_policy:
    must follow active frontend UI audit route taxonomy
    must not preserve /demo as canonical route unless owning SSOT is amended first
    must not become a seed fallback

  must_confirm:
    applied topology
    route/package/manifest linkage
    read-query / screen_data_shape linkage
    propBindings against emission.data branches
    rows collection projection
    activeColumns projection behavior
    displayColumnMode projection behavior
    user-facing rendered UI

production_projection_surface:
  purpose:
    user-facing product projection
    route/manifest selected or resolved through canonical axes

  must_not_be_limited_to:
    default screen_list search only
```

## Current Implementation Evidence

### Existing /demo surface

```text
file:
  frontend/islands/DraftPreviewShell.tsx

observed:
  /demo is described as pre-publish projection preview.
  Layout source is UI Builder applied layout_patch_json + component_style_design.
  Content source is manifest screen_data_shape.initialDataRows.
  Preview request is selected by layoutId.
  Preview result is converted by draftPreviewResultToEmission.
  renderEmission is called with previewMode: true.
  LayoutProjectionTree renders resulting specs.
  Topology intent display uses initialDataRows[0].
  UI text says initialDataRows first row -> slot.
  Multiple rows are displayed as a table, not as the main projected UI collection.
```

```text
classification:
  /demo exists in current implementation.
  /demo is useful as canvas/layout draft preview.
  /demo is weak as production-equivalent UI projection confirmation.
  /demo remains non-canonical under the active frontend UI audit report.
```

### production surface

```text
file:
  frontend/islands/ProjectionShell.tsx

observed:
  ProjectionShell is explicitly documented as production application projection shell.
  It renders data-projection-surface="product".
  It uses renderEmission and LayoutProjectionTree.
  SSE refresh path preserves identity fields.

blocking observation:
  initial dispatch axes are fixed:
    operationType: Search
    target: default
    layer: screen_list
    action: Search

impact:
  product projection surface exists.
  but initial product projection is default-bound.
  UI Builder authored/applied topology is not selectable as arbitrary route/package/manifest projection from this shell.
```

### constructor input normalization

```text
file:
  frontend/runtime/projectionInput.ts

observed:
  projectionInputFromData returns screen_data_shape_query_result.rows[0]
  when data.kind is screen_data_shape_query_result.

backend contract:
  backend/runtime/ScreenDataShapeQueryRuntime.cs returns:
    kind
    manifestId
    rows
    aggregationResults
    activeColumns
    displayColumnMode

classification:
  implementation bug

impact:
  collection outer shape is lost.
  rows[1..] are lost.
  aggregationResults are lost.
  activeColumns are lost.
  displayColumnMode is lost.
```

## Gap Classification

```text
Gap-1:
  id: existing_demo_canvas_preview_not_ui_projection_confirmation
  type:
    implementation_gap
    product_surface_gap
    projection_confirmation_gap

  problem:
    Existing /demo is mainly canvas/layout draft preview.
    It does not sufficiently confirm production-equivalent user-facing UI projection.

  lineage_boundary:
    /demo is non-canonical in the active frontend UI audit report.
    This gap must not be fixed by preserving /demo as canonical.

  evidence:
    DraftPreviewShell loads selected layoutId.
    It uses applied layout_patch_json and initialDataRows.
    It renders first-row slot intent display.

  not:
    missing demo route
    seedLabel smoke issue
    credential management 0092 issue
    request to keep /demo canonical

Gap-2:
  id: production_projection_default_bound
  type:
    implementation_gap
    projection_surface_gap

  problem:
    Production ProjectionShell exists but initial projection is fixed to default/screen_list/Search.
    UI Builder authored/applied topology cannot be selected and confirmed as arbitrary product projection through this surface.

  evidence:
    initialDispatchAxes target/layer/action are hardcoded to default/screen_list/Search.
    product surface marker exists, but entry projection is default-bound.

Gap-3:
  id: constructor_rows_first_collapse
  type:
    implementation_bug

  problem:
    screen_data_shape_query_result is collapsed to rows[0].

  evidence:
    frontend projectionInputFromData rows[0]
    backend ScreenDataShapeQueryRuntime returns rows + aggregationResults + activeColumns + displayColumnMode

Gap-4:
  id: collection_projection_proof_gap
  type:
    test_proof_gap
    projection_confirmation_gap

  problem:
    No sufficient proof that rows / activeColumns / displayColumnMode are preserved and rendered through production-equivalent projection surface.

  not_sufficient:
    first-row sample display
    seedLabel smoke fixture
    initialDataRows table dump
```

## OK Axis

```text
OK:
  - Active report remains the frontend UI audit bundle authority.
  - /demo remains non-canonical unless owning SSOT is amended first.
  - Canvas preview remains available within UI Builder authoring flow or another canonical authoring surface.
  - Pre-production UI projection confirmation is separated from canvas preview.
  - Production projection surface can resolve/select UI Builder applied topology by canonical route/package/manifest/read-query axes.
  - Product projection is not limited to default/screen_list/Search.
  - screen_data_shape_query_result outer shape is preserved unless a projection definition explicitly requests single-row mapping.
  - rows collection projection is visible through user-facing UI projection.
  - activeColumns and displayColumnMode are preserved or explicitly mapped.
  - propBindings continue to resolve from emission.data branches.
  - Tests assert production-equivalent projection behavior, not only first-row sample display.
  - Existing 0092 credential management namespace remains separate from demo/product projection proof.
```

## NG Axis

```text
NG:
  - Treating this memo as a new active report lineage.
  - Reinstating /demo as a canonical route without SSOT amendment.
  - Moving /demo into seed.
  - Treating /demo canvas preview as production-equivalent UI projection proof.
  - Treating default/screen_list/Search product shell as sufficient proof for arbitrary UI Builder authored topology.
  - Keeping projectionInputFromData rows[0] collapse for screen_data_shape_query_result collection payload.
  - Using seedLabel smoke or first-row sample display as proof of UI projection correctness.
  - Adding a hardcoded one-off route screen instead of canonical route/package/manifest/read-query projection path.
  - Moving DB write/promotion authority into UI Builder surface.
  - Mixing manifest 0092 auth.external.credential_management.projection with topology.structure_maps 0092 admin_ui_component_bucket_create.
```

## Bundle Direction

```text
bundle:
  UI projection surface architecture reinforcement

parent_lineage:
  frontend-ui-audit-bundle-semantic-frame.md

scope:
  non-canonical existing /demo observation
  production projection surface
  projection constructor collection preservation
  projection proof/test reinforcement

required_work:
  - do not treat this memo as a parallel active report
  - do not preserve /demo as canonical unless owning SSOT is amended first
  - split canvas preview from production-equivalent UI projection confirmation
  - place pre-production UI projection confirmation under canonical route/surface taxonomy
  - make production projection surface route/package/manifest aware instead of default-only
  - preserve screen_data_shape_query_result outer payload shape
  - add proof for rows / activeColumns / displayColumnMode through production-equivalent projection
  - keep UI Builder save/apply authority boundaries intact
  - keep 0092 credential namespace outside demo/product proof scope
```

## Owner Report Judgment

```text
implemented:
  no

partial:
  yes

blocking:
  UI projection confirmation is structurally weak on both sides:
    existing /demo is canvas/layout draft preview and non-canonical.
    production ProjectionShell is default-bound.

result:
  UI Builder authored/applied topology cannot be sufficiently confirmed as real user-facing UI projection before or through a route/package/manifest-aware product surface.

merge_judgment:
  do not mark implemented until the projection surface gap and rows[0] collection collapse bug are addressed at Bundle scope under the active frontend UI audit report lineage.
```

## Reference Evidence

```text
AGENTS.md:
  - Repository Design Order: SSOT -> wiring -> test/proof surface -> implementation
  - Existing implementation is not design authority

active_report_refs:
  .agent/reports/frontend-ui-audit-bundle-semantic-frame.md
    - active report declaration
    - route taxonomy
    - /demo non-canonical removal policy

SSOT refs:
  docs/design/ui-builder-preset-ecosystem-ssot.yaml
    - cross_preset_authoring_boundary
    - invariant
    - tmp_draft_boundary
    - explicit_apply_boundary
    - author_resolution_map

  docs/design/pipeline-continuity-ssot.yaml
    - api_command_lane
    - frontend.projection
    - pipeline_body_test

Implementation refs:
  frontend/islands/DraftPreviewShell.tsx
    - DraftPreviewShell
    - draftPreviewResultToEmission
    - renderEmission previewMode path
    - initialDataRows first-row slot display

  frontend/islands/ProjectionShell.tsx
    - ProjectionShell
    - initialDispatchAxes
    - data-projection-surface="product"

  frontend/runtime/projectionInput.ts
    - projectionInputFromData

  backend/runtime/ScreenDataShapeQueryRuntime.cs
    - screen_data_shape_query_result payload
```
