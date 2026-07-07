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

status:
  audit_in_progress

implemented:
  no

partial:
  yes

lineage_rule:
  active_report_remains_authority
  this_report_is_subordinate_evidence
  no_parallel_report_lineage
```

## Active Report Boundary

```text
active_report:
  .agent/reports/frontend-ui-audit-bundle-semantic-frame.md

canonical_routes:
  projection_entry:
    /
  admin_authoring_settings:
    /admin
    /admin/contents
    /admin/ui-builder
    /admin/manifests

non_canonical_routes:
  /demo
  /runtime-status
  /admin/enums
  /admin/users
  /admin/team-dashboard
  /admin/scheduler

/demo_policy:
  route: non_canonical
  seed_replacement: none
  canonical_revival: NG
```

## Finding

```text
finding:
  /demo route ownership causes semantic misclassification.

reason:
  route_exists: true
  standalone_island_exists: DraftPreviewShell
  projection_surface_label: demo

misclassification:
  temporary_inspection_surface -> independent_route_domain_surface

correct_classification:
  existing_demo_logic: misplaced_inspection_logic
  existing_demo_route: non_canonical
  reusable_logic_target: UI Builder component scope
```

## SSOT Reference Guard

```text
before_implementation:
  search SSOT/docs for /demo

if_ssot_canonical_demo_reference_exists:
  status:
    design_gap
    route_taxonomy_conflict
    lineage_conflict

  required_order:
    design_change first
    then implementation_change

  required_alignment:
    /demo canonical reference: absent
    projection_inspection: UI Builder component scope
    active_report_route_taxonomy: preserved

NG:
  use stale SSOT wording to keep /demo as route
  add demo seed
  add standalone demo domain
```

## Component Placement

```text
preferred_target:
  /admin/ui-builder component / panel / tab

component_role:
  read_only_inspection
  production_equivalent_render_confirmation
  canvas_preview_vs_applied_projection_comparison
  applied_topology_confirmation
  route_package_manifest_confirmation
  read_query_confirmation
  propBindings_confirmation
  rows_confirmation
  activeColumns_confirmation
  displayColumnMode_confirmation

component_not:
  standalone_route
  canonical_projection_entry
  seed_fallback
  persistence_authority
  promotion_authority
```

## Production Surface Gap

```text
file:
  frontend/islands/ProjectionShell.tsx

observed:
  product_surface_exists: true
  renderEmission_path: true
  LayoutProjectionTree_path: true

blocking:
  initialDispatchAxes:
    operationType: Search
    target: default
    layer: screen_list
    action: Search

problem:
  product_surface_entry_is_default_bound
  arbitrary_UI_Builder_applied_topology_not_selectable

required:
  route_package_manifest_aware_projection_entry
```

## Collection Shape Gap

```text
frontend_file:
  frontend/runtime/projectionInput.ts

observed:
  screen_data_shape_query_result -> rows[0]

backend_file:
  backend/runtime/ScreenDataShapeQueryRuntime.cs

backend_payload:
  kind
  manifestId
  rows
  aggregationResults
  activeColumns
  displayColumnMode

problem:
  collection_outer_shape_lost
  rows_tail_lost
  aggregationResults_lost
  activeColumns_lost
  displayColumnMode_lost

required:
  preserve_outer_shape_unless_explicit_single_row_mapping
```

## OK Axis

```text
OK:
  - active report remains authority
  - /demo remains non-canonical
  - SSOT/docs searched for /demo before implementation
  - canonical /demo reference corrected by design_change if found
  - standalone /demo route ownership not retained
  - projection inspection lives in UI Builder component scope
  - projection inspection is read-only
  - canvas preview remains authoring aid
  - product projection is not default-bound
  - screen_data_shape_query_result outer shape preserved
  - rows / activeColumns / displayColumnMode proven through projection path
  - propBindings resolve from emission.data branches
```

## NG Axis

```text
NG:
  - new active report lineage
  - standalone /demo route retained
  - /demo treated as product projection proof
  - stale SSOT /demo wording used as route-retention reason
  - demo seed added
  - standalone demo domain added
  - first-row sample accepted as collection proof
  - seedLabel smoke accepted as UI projection proof
  - default/screen_list/Search accepted as arbitrary topology proof
  - rows[0] collapse retained for collection payload
```

## Bundle Direction

```text
bundle:
  UI projection surface architecture reinforcement

scope:
  /demo route ownership cleanup
  UI Builder projection inspection componentization
  SSOT /demo reference guard
  production projection surface
  projection constructor collection preservation
  projection proof reinforcement

required_work:
  - use directory-map before implementation
  - search SSOT/docs for /demo
  - design_change first if canonical /demo reference exists
  - move reusable inspection logic to UI Builder component scope
  - keep inspection read-only
  - make production projection route/package/manifest aware
  - preserve screen_data_shape_query_result outer shape
  - add proof for rows / activeColumns / displayColumnMode
  - keep UI Builder save/apply boundaries intact
```

## Owner Report Judgment

```text
implemented:
  no

partial:
  yes

blocking:
  /demo route ownership causes semantic misclassification.
  /demo is non-canonical under active report lineage.
  production ProjectionShell is default-bound.
  projectionInputFromData collapses collection payload to rows[0].

merge_judgment:
  do not mark implemented until Bundle scope addresses:
    /demo route ownership
    UI Builder component placement
    SSOT /demo reference guard
    production default-bound projection
    rows[0] collection collapse
    collection projection proof
```

## Reference Evidence

```text
entry_contract:
  AGENTS.md

active_report:
  .agent/reports/frontend-ui-audit-bundle-semantic-frame.md

SSOT:
  docs/design/ui-builder-preset-ecosystem-ssot.yaml
  docs/design/pipeline-continuity-ssot.yaml

implementation:
  frontend/islands/DraftPreviewShell.tsx
  frontend/islands/ProjectionShell.tsx
  frontend/runtime/projectionInput.ts
  backend/runtime/ScreenDataShapeQueryRuntime.cs
```
