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

## Bundle: frontend surface UI structure/wiring SSOTs

```text
owns:
  UI structure and wiring per canonical surface
  visible labels
  normal/technical disclosure boundary
  seed/input boundary
  proof surface per surface

proposed_units:
  root_projection_ui_structure_wiring_ssot: /
  auth_gate_ui_structure_wiring_ssot: /auth
  superauth_gate_ui_structure_wiring_ssot: /super_auth
  admin_index_ui_structure_wiring_ssot: /admin
  admin_contents_ui_structure_wiring_ssot: /admin/contents
  admin_uibuilder_ui_structure_wiring_ssot: /admin/ui-builder
  admin_manifests_ui_structure_wiring_ssot: /admin/manifests

OK:
  each canonical surface has owning UI structure/wiring SSOT
  implementation/test maps to that SSOT
  raw ids stay internal or explicit technical disclosure

NG:
  using implementation as SSOT
  placing this task under ui-builder-preset-ecosystem-ssot
  mixing projection, gate, admin settings, and seed CRUD in one category
```

## Bundle: initial-projection-side-admin-crud-seed-ssot

```text
owns:
  credentials auth / external api / external instance
  enum CRUD
  user / role / status CRUD
  dashboard configuration CRUD
  scheduler configuration CRUD

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

required:
  layout_mode keeps existing Figma-like layout canvas lineage
  wiring_mode edits UI event wiring
  persistence authority is runtimeInteractions
  UI event settings can select registered external api
  UI event settings can select registered external instance
  UI event settings can select credential / authority requirement
  selected target writes typed runtimeInteraction reference

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
```

## Bundle: pipeline-continuity-ssot

```text
owns:
  proof surface policy
  local test expectation
  replacement tests for route removal and seed CRUD renderability

required_proof:
  route registry contains canonical routes only
  non-canonical hardcoded routes are absent
  required initial projection-side admin CRUD seed exists
  seeded CRUD renders through canonical projection/admin mechanism
  registered external api / external instance selectable in UI Builder event settings
  selected capability writes typed runtimeInteraction
  normal labels do not expose raw ids / UUIDs / internal vocabulary

OK:
  tests follow owning SSOT
  old route-presence tests replaced by seed/render/wiring proof
  test deletion is paired with replacement proof

NG:
  test-only deletion
  old tests asserting seed-migrated routes as canonical
  completion claim without agent-ui-local-test or routed fallback checks
```
