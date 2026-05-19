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

Apply files in this order:

```bash
psql -d <database> -f db/schema.sql
psql -d <database> -f db/topology_tables.sql
psql -d <database> -f db/promotion_tables.sql
psql -d <database> -f db/context_route_tables.sql
psql -d <database> -f db/seed_empty.sql
```

`schema.sql` creates all registry tables and `function_parameters`.
`topology_tables.sql` creates `hubs`, `entities`, `hub_relations`, `structure_maps`.
`promotion_tables.sql` creates `usage_metrics`, `promotion_candidates`.
`context_route_tables.sql` creates the context route recommendation runtime tables.
`seed_empty.sql` inserts the minimum default topology rows including the
`default:entity:search` structure map and the context route recommendation policy
row in `function_parameters` needed for the dummy canonical flow.

To also load the public scaffold demo data (fake data only, no real business data):

```bash
psql -d <database> -f db/demo_seed.sql
```

`demo_seed.sql` adds demo hub / entities / context tokens / demo policy, demo structure maps,
and demo auth credentials (bcrypt-hashed) for the JWT login scaffold.
It is safe to apply after `seed_empty.sql`. All rows use `ON CONFLICT DO NOTHING`.
See `docs/demo-walkthrough.md` for what to observe after applying the demo seed.

**docker compose:** On a fresh volume, `docker compose -f infra/docker-compose.yml up -d`
applies all files above (01–06) automatically via `docker-entrypoint-initdb.d`.
On an existing volume, run `psql -d topolactor_demo -f db/demo_seed.sql` manually.

---

## Files

| File | Purpose |
|---|---|
| `schema.sql` | Top-level entrypoint. Extensions, all registry tables, `function_parameters`. References `topology_tables.sql` and `promotion_tables.sql` via `\i` comments. |
| `topology_tables.sql` | Topology definition tables (`hub_relations`, `structure_maps`) and converged entity data tables (`hubs`, `entities`). |
| `promotion_tables.sql` | Promotion policy tables (`usage_metrics`, `promotion_candidates`). Advisory only — no migrations executed here. |
| `context_route_tables.sql` | Context route recommendation runtime tables. Append-only event log, rebuildable sparse vector cache projections, transition stats. Optional cluster/drift tables isolated at bottom. |
| `seed_empty.sql` | Minimal default seed rows. Includes context route policy row in `function_parameters`. No real business data. |
| `demo_seed.sql` | Public scaffold demo seed. Fake/demo data only: hub, entities, context tokens, demo_policy, demo structure maps. Apply after `seed_empty.sql`. |

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
- `hub_relations` — weighted relation bindings between hubs and `relation_registry` entries
- `function_parameters` — data-driven configuration parameters for canonical flow functions

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
