# topolactor — database layer

The database is the semantic topology space for the topolactor system. It is
not a conventional CRUD/MVC datastore. Tables fall into distinct categories
with different roles in the canonical flow:

```
stored_topology_data
  → user_operation
  → operation_vector
  → attractor_resolve
  → structure_map_resolve
  → package_resolve
  → schema_resolve
  → component_expand
  → emission_or_projection
```

---

## How to run

Apply files in this order (this is the standard local `psql` path):

```bash
psql -d <database> -f db/schema.sql
psql -d <database> -f db/topology_tables.sql
psql -d <database> -f db/promotion_tables.sql
psql -d <database> -f db/sql_attention_logs_tables.sql
psql -d <database> -f db/ci_attention_guidance_tables.sql
psql -d <database> -f db/context_route_tables.sql
psql -d <database> -f db/ui_topology_tables.sql
psql -d <database> -f db/manifest_tables.sql
psql -d <database> -f db/enum_tables.sql
psql -d <database> -f db/enum_seed.sql
psql -d <database> -f db/auth_tables.sql
psql -d <database> -f db/auth_seed.sql
psql -d <database> -f db/legacy_mirror_tables.sql
psql -d <database> -f db/seed_empty.sql
```

`schema.sql` creates all registry tables and `function_parameters`.
`topology_tables.sql` creates `hubs`, `entities`, `hub_relations`, `structure_maps`.
`promotion_tables.sql` creates `usage_metrics`, `promotion_candidates`.
`context_route_tables.sql` creates the context route recommendation runtime tables.
`manifest_tables.sql` creates manifest draft/active storage.
`auth_tables.sql` creates the canonical `auth.*` identity, credential, session, refresh token, login event, role, scope, and grant tables.
`auth_seed.sql` inserts minimal user/admin credentials into `auth.*`; credentials are not stored in topology or manifest rows.
`seed_empty.sql` inserts the minimum default topology rows including the
`default:entity:search` structure map, the user login seed manifest, and the context route recommendation policy
row in `function_parameters` needed for the canonical flow.

**docker compose:** On a fresh volume, `docker compose --env-file infra/.env -f infra/docker-compose.yml up -d`
executes `db/init.sql` via `docker-entrypoint-initdb.d/00-init.sql`. This file is **compose/container-path specific** (`/db/...`) and assumes `infra/docker-compose.yml` mounts `../db` to `/db`.
It applies `schema.sql -> topology_tables.sql -> promotion_tables.sql -> sql_attention_logs_tables.sql -> ci_attention_guidance_tables.sql -> context_route_tables.sql -> ui_topology_tables.sql -> manifest_tables.sql -> enum_tables.sql -> enum_seed.sql -> auth_tables.sql -> auth_seed.sql -> legacy_mirror_tables.sql -> seed_empty.sql` in one explicit order with `ON_ERROR_STOP`.
For normal host-side `psql` usage, use the ordered per-file commands above (not `psql -f db/init.sql`).
On an existing volume, run `psql -d topolactor_demo -f db/auth_tables.sql` and `psql -d topolactor_demo -f db/auth_seed.sql` manually if needed.

---

## Files

| File | Purpose |
|---|---|
| `schema.sql` | Top-level entrypoint. Extensions, all registry tables, `function_parameters`. References `topology_tables.sql` and `promotion_tables.sql` via `\i` comments. |
| `topology_tables.sql` | Topology definition tables (`hub_relations`, `structure_maps`) and converged entity data tables (`hubs`, `entities`). |
| `promotion_tables.sql` | Promotion policy tables (`usage_metrics`, `promotion_candidates`). Advisory only — no migrations executed here. |
| `context_route_tables.sql` | Context route recommendation runtime tables. Append-only event log, rebuildable sparse vector cache projections, transition stats. Optional cluster/drift tables isolated at bottom. |
| `manifest_tables.sql` | Manifest draft/active storage and promotion lifecycle tables. |
| `enum_tables.sql` | Canonical enum item / enum group dictionary (`enum.items`, `enum.groups`, `enum.group_items`). |
| `enum_seed.sql` | Minimal demo enum dictionary for admin select regression. Apply after `enum_tables.sql`. |
| `auth_tables.sql` | Canonical auth store for identities, password hashes, sessions, refresh-token hashes, login events, roles, scopes, and grants. |
| `auth_seed.sql` | Minimal user/admin credentials in `auth.*` for login. Apply after `auth_tables.sql`. |
| `seed_empty.sql` | Minimal default seed rows. Includes context route policy row in `function_parameters`, admin dispatch manifests, and the user login seed manifest. No real business data. |
| `init.sql` | Docker initialization SSOT. Uses psql meta commands to execute all SQL files in explicit order with fail-fast behavior. |

---

## Table categories

### Topology definition tables

These define the shape and rules of the topology space. They are the
authoritative source of truth for how the system resolves operations. Changes
here alter topology behaviour for all future canonical flow traversals.

- `registrar_entries` — meta-registry of all topology tables
- `master_registry` — top-level domain concept taxonomy
- `state_registry` — named operational states
- `relation_registry` — named relations; primary structuring mechanism
- `package_registry` — versioned/typed package definitions (resolved in `package_resolve`)
- `schema_registry` — schema definitions governing converged entity shape (resolved in `schema_resolve`)
- `component_registry` — discrete reusable behaviour units (expanded in `component_expand`)
- `structure_maps` — binds `attractor_key` → package → schema → components; the central topology definition artifact
- `hub_relations` — manifest-scoped hub sequence entries under `hubs.topology_manifests`
- `function_parameters` — data-driven configuration parameters for canonical flow functions

### Auth boundary tables

These are not topology authoring tables. They are the credential/session security boundary.

- `auth.users` — canonical user identity
- `auth.credentials` — password hash store
- `auth.sessions` — logical session store
- `auth.refresh_tokens` — refresh-token hash store
- `auth.login_events` — auth audit trail
- `auth.roles`, `auth.scopes`, `auth.grants` — realm/role/scope binding

### Converged entity data tables

Basic shape principle for real/sys tables: `id / state / jsonb`.
- `id`: identity
- `state`: current state
- `jsonb`: latent and semi-structured payload retained before/alongside selective column promotion

JSONB keys can be promoted to columns when observation/audit/projection requires explicit semantic axes.
This promotion is treated as surfacing semantic axes, not as abandoning the tensor-first architecture subject.


These hold the runtime-converged state produced by traversing the topology.
They are outputs of the canonical flow, not source-of-truth business data.
Data here reflects the current resolved projection of operations against the
topology definition.

- `hubs` — resolved grouping points, populated during `attractor_resolve`
- `entities` — resolved data nodes within hubs, populated by `schema_resolve` + `component_expand`
- `diff_logs` — append-only diff surface (basic shape: id / tableId / jsonb / created) for audit/rebuild history; not current-state SoT

### Promotion policy tables

These track observed usage patterns and hold advisory structural change
suggestions. No migrations are executed by these tables.

- `usage_metrics` — observed read/filter/aggregation/join patterns per table/column/jsonb-path
- `promotion_candidates` — advisory suggestions (index, generated column, registry promotion, physical table promotion)

---

## Real business data

Real business data is out of scope for this layer. The seed (`seed_empty.sql`)
inserts only the minimum structural defaults required to bootstrap the topology
space. Business data is introduced through the canonical flow at runtime.