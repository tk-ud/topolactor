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

**Standard procedure: docker compose fresh volume bootstrap.**

```bash
docker compose -v                                                      # remove Docker volumes
docker compose --env-file infra/.env -f infra/docker-compose.yml up -d  # fresh bootstrap
```

`docker compose -v` removes Docker volumes. On the next `docker compose up`, the container executes
`db/init.sql` via `docker-entrypoint-initdb.d/00-init.sql` on a clean database. All canonical DDL
and seed data lives in `db/*.sql` files applied in explicit order with `ON_ERROR_STOP`.

`db/migrations/` and `db/patches/` are **retired as of 2026-06-15**. Do not add files there.
Existing-DB incremental migration via `psql -f db/migrations/*.sql` is **not** the standard
operational procedure. Use `docker compose -v` fresh bootstrap.

**Bootstrap file order** (managed by `db/init.sql`):

```
schema.sql → topology_tables.sql → promotion_tables.sql → sql_attention_logs_tables.sql →
ci_attention_guidance_tables.sql → context_route_tables.sql → ui_topology_tables.sql →
topology_cross_table_wiring.sql → mock_preset_tables.sql → team_markdown_tables.sql →
manifest_tables.sql → enum_tables.sql → enum_seed.sql → auth_tables.sql → auth_seed.sql →
legacy_mirror_tables.sql → seed_empty.sql → hub_search_preset_seed.sql →
physical_search_crud_aggregate_preset_seed.sql → physical_details_inline_editor_md_generator_preset_seed.sql →
aggregate_dashboard_preset_seed.sql → ui_component_registry_preset_catalog_bootstrap.sql
```

`schema.sql` creates all registry tables and `function_parameters`.
`topology_tables.sql` creates `hubs`, `entities`, `hub_relations`, `structure_maps`.
`promotion_tables.sql` creates `usage_metrics`, `promotion_candidates`.
`context_route_tables.sql` creates the context route recommendation runtime tables.
`topology_cross_table_wiring.sql` applies cross-table FK constraints that span separately loaded files.
`mock_preset_tables.sql` creates `topology.mock_preset_*` tables.
`team_markdown_tables.sql` creates `topology.team_markdown_*` tables.
`manifest_tables.sql` creates manifest draft/active storage.
`auth_tables.sql` creates the canonical `auth.*` identity, credential, session, refresh token, login event, role, scope, and grant tables.
`auth_seed.sql` inserts minimal user/admin credentials into `auth.*`; credentials are not stored in topology or manifest rows.
`seed_empty.sql` inserts the minimum default topology rows including the
`default:entity:search` structure map, the user login seed manifest, and the context route recommendation policy
row in `function_parameters` needed for the canonical flow.
`hub_search_preset_seed.sql`, `physical_search_crud_aggregate_preset_seed.sql`,
`physical_details_inline_editor_md_generator_preset_seed.sql`, `aggregate_dashboard_preset_seed.sql` — preset
seed rows for each canonical UIBuilder mock preset.
`ui_component_registry_preset_catalog_bootstrap.sql` inserts the fixed-UUID `components_bucket` and
`ui_component_registry` rows needed for the preset ecosystem.

---

## Files

| File | Purpose |
|---|---|
| `schema.sql` | Top-level entrypoint. Extensions, all registry tables, `function_parameters`. |
| `topology_tables.sql` | Topology definition tables (`hub_relations`, `structure_maps`) and converged entity data tables (`hubs`, `entities`). |
| `promotion_tables.sql` | Promotion policy tables (`usage_metrics`, `promotion_candidates`). |
| `sql_attention_logs_tables.sql` | SQL Attention signal and observation log tables. |
| `ci_attention_guidance_tables.sql` | CI attention guidance tables. |
| `context_route_tables.sql` | Context route recommendation runtime tables. Append-only event log, rebuildable sparse vector cache projections, transition stats. |
| `ui_topology_tables.sql` | UI component, package, layout, and wiring definition tables in `topology.*` schema. |
| `topology_cross_table_wiring.sql` | Cross-table FK constraints that span separately loaded files (e.g., `structure_maps.layout_id → components_layout_design`). Must run after `topology_tables.sql` and `ui_topology_tables.sql`. |
| `mock_preset_tables.sql` | `topology.mock_preset_*` tables: registry, object mapping, wiring candidates, compile snapshots. |
| `team_markdown_tables.sql` | `topology.team_markdown_*` tables: template registry, saved views, saved view events. |
| `manifest_tables.sql` | Manifest draft/active storage and promotion lifecycle tables. |
| `enum_tables.sql` | Canonical enum item / enum group dictionary (`enum.items`, `enum.groups`, `enum.group_items`). |
| `enum_seed.sql` | Minimal enum dictionary for admin select. Apply after `enum_tables.sql`. |
| `auth_tables.sql` | Canonical auth store for identities, password hashes, sessions, refresh-token hashes, login events, roles, scopes, and grants. |
| `auth_seed.sql` | Minimal user/admin credentials in `auth.*` for login. Apply after `auth_tables.sql`. |
| `legacy_mirror_tables.sql` | Legacy compatibility tables retained for runtime routing compatibility only. |
| `seed_empty.sql` | Minimal default seed rows. Includes context route policy row in `function_parameters`, admin dispatch manifests, and the user login seed manifest. No real business data. |
| `hub_search_preset_seed.sql` | UIBuilder hub_search.readonly.v1 preset seed rows. |
| `physical_search_crud_aggregate_preset_seed.sql` | UIBuilder physical_search_crud_aggregate.v1 preset seed rows. |
| `physical_details_inline_editor_md_generator_preset_seed.sql` | UIBuilder physical_details_inline_editor_md_generator.v1 preset seed rows. |
| `aggregate_dashboard_preset_seed.sql` | UIBuilder aggregate_dashboard.v1 preset seed rows. |
| `ui_component_registry_preset_catalog_bootstrap.sql` | Fixed-UUID `components_bucket` and `ui_component_registry` bootstrap rows for the preset ecosystem. |
| `init.sql` | Docker initialization SSOT. Uses psql meta commands to execute all SQL files in explicit order with `ON_ERROR_STOP`. Container-path specific (`/db/...`). |

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