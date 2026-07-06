# Frontend UI Audit Finding 7 Correction

- Date: 2026-07-06
- Target repo: github.com/tk-ud/topolactor
- Worktype: audit / design_correction
- Supersedes: `.agent/reports/frontend-ui-audit.md` Finding 7 wording where it says `projection-app setting data`, `seed-backed app settings`, `demo/preview defaults`, or `runtime/internal diagnostics defaults`.

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

non_canonical_hardcoded_routes_to_remove:
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
- `/admin/enums`, `/admin/users`, `/admin/team-dashboard`, and `/admin/scheduler` must not survive as hardcoded standalone frontend routes.
- Their responsibilities must be expressed as **initial projection-side admin CRUD seed**.
- The projection engine / canonical admin mechanism should render the seeded CRUD definitions, not route-specific hardcoded pages.
- `/demo` is not required and must not be moved into seed.
- `/runtime-status` / diagnostics are not required and must not be moved into seed.

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
  -> remove hardcoded route
  -> no seed replacement

/runtime-status:
  -> remove hardcoded route
  -> no diagnostics seed replacement
```

## Test/proof correction

Existing tests must not simply delete evidence.

Correct test migration:

```text
old proof:
  hardcoded route exists

new proof:
  hardcoded non-canonical route is absent
  initial projection-side admin CRUD seed exists for enum/users/dashboard/scheduler responsibilities
  seeded CRUD definition is renderable through canonical projection/admin mechanism
  old route-specific page is not needed for the responsibility
  /demo has no seed replacement requirement
  /runtime-status has no diagnostics seed replacement requirement
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
- Removed hardcoded admin CRUD routes have replacement initial projection-side admin CRUD seed definitions:
  - enum CRUD
  - user / role / status CRUD
  - dashboard configuration CRUD
  - scheduler configuration CRUD
- `/demo` is removed without seed replacement.
- `/runtime-status` / diagnostics are removed without seed replacement.
- Tests prove seeded CRUD renderability through the canonical projection/admin mechanism only for required CRUD responsibilities.

## NG axis

- Calling `/auth` or `/super_auth` projection pages.
- Calling `/admin` business projection itself.
- Calling the seed target generic `projection-app setting data` without CRUD/seed authority.
- Moving `/demo` into seed.
- Moving `/runtime-status` or diagnostics into seed.
- Keeping `/admin/enums`, `/admin/users`, `/admin/team-dashboard`, `/admin/scheduler`, `/demo`, or `/runtime-status` as canonical hardcoded routes.
- Deleting tests without replacement seed/CRUD proof for the required CRUD responsibilities.
- Keeping tests that assert seed-migrated routes as canonical pages.
