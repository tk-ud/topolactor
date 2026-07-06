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

## Corrected meaning

- `/auth` and `/super_auth` are gates, not projection pages.
- `/admin` and its canonical child routes are Topolactor projection-authoring/settings surfaces, not business projection itself.
- The initial projection-side admin CRUD seed is the correct authority for credentials auth / external api / external instance, enum CRUD, user / role / status CRUD, dashboard configuration CRUD, and scheduler configuration CRUD.
- `/admin/enums`, `/admin/users`, `/admin/team-dashboard`, and `/admin/scheduler` must not survive as hardcoded standalone frontend routes.
- Their responsibilities must be expressed as **initial projection-side admin CRUD seed**.
- The projection engine / canonical admin mechanism should render the seeded CRUD definitions, not route-specific hardcoded pages.
- `/demo` is not required and must not be moved into seed.
- `/runtime-status` / diagnostics are not required and must not be moved into seed.

## UI Builder event wiring implication

Initial projection-side admin CRUD seed is not complete if the registered external capabilities cannot be used from UI Builder event settings.

Required wiring consequence:

```text
registered_external_api:
  source: initial projection-side admin CRUD seed
  usable_in: /admin/ui-builder UI event settings
  event_wiring_target: runtimeInteraction external api dispatch target

registered_external_instance:
  source: initial projection-side admin CRUD seed
  usable_in: /admin/ui-builder UI event settings
  event_wiring_target: runtimeInteraction external instance dispatch target

registered_credentials_auth:
  source: initial projection-side admin CRUD seed
  usable_in: /admin/ui-builder UI event settings as selectable authority/credential requirement
  event_wiring_target: runtimeInteraction credential / authority requirement reference
```

UI Builder must not hardcode external api / external instance choices. It must read selectable candidates from the canonical seed-backed registry/projection admin mechanism and write typed event wiring into the UI structure/wiring authority.

## Corrected seed target

```text
credentials auth / external api / external instance:
  -> initial projection-side admin CRUD seed
  -> no separate hardcoded frontend route
  -> selectable from /admin/ui-builder UI event settings

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
  initial projection-side admin CRUD seed exists for credentials auth / external api / external instance, enum/users/dashboard/scheduler responsibilities
  registered external api / external instance candidates are selectable in UI Builder event settings
  UI Builder writes typed runtimeInteraction references for selected external api / external instance / credential authority requirements
  seeded CRUD definition is renderable through canonical projection/admin mechanism
  old route-specific page is not needed for the responsibility
  /demo has no seed replacement requirement
  /runtime-status has no diagnostics seed replacement requirement
```

Affected current test surface:

- `frontend/tests/adminMainFlow.test.ts`
  - `ADMIN_ROUTE_CARDS contain canonical admin routes only`
  - `Fresh /admin route registry matches runtime-orchestration SSOT exactly`
- UI Builder event setting tests must cover registered external api / external instance candidate selection and typed runtimeInteraction output.

These tests currently preserve old hardcoded route authority and must be rewritten under the corrected seed/CRUD boundary. UI Builder proof must also show that registered external capabilities become selectable wiring targets.

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
  - credentials auth / external api / external instance
  - enum CRUD
  - user / role / status CRUD
  - dashboard configuration CRUD
  - scheduler configuration CRUD
- Registered external api / external instance entries are selectable in `/admin/ui-builder` UI event settings.
- UI Builder event settings write typed runtimeInteraction references, not raw hardcoded route/page references.
- `/demo` is removed without seed replacement.
- `/runtime-status` / diagnostics are removed without seed replacement.
- Tests prove seeded CRUD renderability through the canonical projection/admin mechanism only for required CRUD responsibilities.
- Tests prove UI Builder event wiring can use registered external api / external instance / credential authority candidates.

## NG axis

- Calling `/auth` or `/super_auth` projection pages.
- Calling `/admin` business projection itself.
- Calling the seed target generic `projection-app setting data` without CRUD/seed authority.
- Moving `/demo` into seed.
- Moving `/runtime-status` or diagnostics into seed.
- Keeping `/admin/enums`, `/admin/users`, `/admin/team-dashboard`, `/admin/scheduler`, `/demo`, or `/runtime-status` as canonical hardcoded routes.
- Registering external api / external instance entries without making them selectable in UI Builder event settings.
- Hardcoding external api / external instance event choices inside UI Builder instead of reading registered candidates.
- Writing raw route/page references instead of typed runtimeInteraction references.
- Deleting tests without replacement seed/CRUD proof for the required CRUD responsibilities.
- Keeping tests that assert seed-migrated routes as canonical pages.
