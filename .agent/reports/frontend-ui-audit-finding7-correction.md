# Frontend UI Audit Finding 7 Correction

- Date: 2026-07-06
- Target repo: github.com/tk-ud/topolactor
- Worktype: audit / design_correction
- Supersedes: `.agent/reports/frontend-ui-audit.md` Finding 7 wording where it says `projection-app setting data` or `seed-backed app settings`.

## Correction summary

Finding 7 must not classify the target routes as generic `seed-backed app settings`.

Correct classification:

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
  enum CRUD
  user / role / status CRUD
  dashboard configuration CRUD
  scheduler configuration CRUD
  preview/demo configuration CRUD
  internal diagnostics/status CRUD if needed

non_canonical_hardcoded_routes:
  /admin/enums
  /admin/users
  /admin/team-dashboard
  /admin/scheduler
  /demo
  /runtime-status
```

## Corrected meaning

- `/auth` and `/super_auth` are gates, not projection pages.
- `/admin` and its canonical child routes are Topolactor projection-authoring/settings surfaces, not business projection itself.
- `/admin/enums`, `/admin/users`, `/admin/team-dashboard`, `/admin/scheduler`, `/demo`, and `/runtime-status` must not survive as hardcoded standalone frontend routes.
- Their responsibilities must be expressed as **initial projection-side admin CRUD seed**.
- The projection engine / canonical admin mechanism should render the seeded CRUD definitions, not route-specific hardcoded pages.

## Corrected seed target

```text
/admin/enums:
  -> initial projection-side admin CRUD seed for enum groups/items

/admin/users:
  -> initial projection-side admin CRUD seed for users / roles / status defaults

/admin/team-dashboard:
  -> initial projection-side admin CRUD seed for dashboard configuration

/admin/scheduler:
  -> initial projection-side admin CRUD seed for scheduler configuration

/demo:
  -> initial projection-side admin CRUD seed or preview seed for demo/preview configuration

/runtime-status:
  -> initial projection-side admin CRUD seed or internal diagnostics seed if needed
  -> no normal frontend route projection
```

## Test/proof correction

Existing tests must not simply delete evidence.

Correct test migration:

```text
old proof:
  hardcoded route exists

new proof:
  hardcoded non-canonical route is absent
  initial projection-side admin CRUD seed exists
  seeded CRUD definition is renderable through canonical projection/admin mechanism
  old route-specific page is not needed for the responsibility
```

Affected current test surface:

- `frontend/tests/adminMainFlow.test.ts`
  - `ADMIN_ROUTE_CARDS contain canonical admin routes only`
  - `Fresh /admin route registry matches runtime-orchestration SSOT exactly`

These tests currently preserve old hardcoded route authority and must be rewritten under the corrected seed/CRUD boundary.

## OK axis

- Canonical route authority only keeps:
  - `/`
  - `/auth`
  - `/super_auth`
  - `/admin`
  - `/admin/contents`
  - `/admin/ui-builder`
  - `/admin/manifests`
- Gate routes are classified as gates.
- Admin routes are classified as Topolactor projection-authoring/settings surfaces.
- Removed hardcoded routes have replacement initial projection-side admin CRUD seed definitions.
- Tests prove seeded CRUD renderability through the canonical projection/admin mechanism.

## NG axis

- Calling `/auth` or `/super_auth` projection pages.
- Calling `/admin` business projection itself.
- Calling the seed target generic `projection-app setting data` without CRUD/seed authority.
- Keeping `/admin/enums`, `/admin/users`, `/admin/team-dashboard`, `/admin/scheduler`, `/demo`, or `/runtime-status` as canonical hardcoded routes.
- Deleting tests without replacement seed/CRUD proof.
- Keeping tests that assert seed-migrated routes as canonical pages.
