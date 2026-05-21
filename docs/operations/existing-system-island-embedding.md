# Existing System Island Embedding Operation

This document describes an operational adoption pattern for embedding Topolactor into an existing system without replacing the whole application at once.

It is not a PR review-loop memo. It is an application adoption model.

Topolactor can be introduced as a scaffolded island beside an existing system by mirroring existing table state into `jsonb`, registering that mirror into the registry, forking selected API events, and embedding topology-driven UI islands into the current application surface.

## Core Claim

Topolactor does not require a full rewrite to become useful.

It can operate as an island system:

```text
existing system
-> mirror existing physical table state into jsonb
-> register table/row/column state into Topolactor registry surfaces
-> fork selected existing API change events
-> embed topology-driven UI islands
-> expand island coverage gradually
```

The existing system remains the production authority while Topolactor gains controlled runtime territory.

## Why this matters

Most business systems cannot be replaced in one step.

They already have:

- production data;
- existing APIs;
- operational habits;
- user workflows;
- access-control assumptions;
- fragile screens that still matter.

Topolactor should not force those systems into an all-or-nothing migration.

Instead, it can scaffold beside them, mirror enough structure to observe them, and project new islands where topology-driven runtime behavior is valuable.

## Minimal Mirror Model

The first mirror does not need to reproduce the full relational design.

For each existing physical table, capture the table name, row identity, current row state, and column data as `jsonb`.

Minimal mirror record:

```text
legacy_table_snapshot:
  table_name
  row_id / primary_key
  row_state
  column_data_jsonb
  relation_hint_jsonb optional
  captured_at
```

The important point is that a relational legacy system can be observed initially as physical-table state.

Existing relations do not need to be fully migrated first. They can be carried as `relation_hint_jsonb` or extracted later as registry edges.

```text
legacy relational DB
-> physical table snapshots
-> jsonb mirror
-> registry registration
-> relation edge extraction later
```

This makes the mirror an observation layer, not a schema rewrite.

## Registry Promotion

After mirror ingestion, Topolactor registers the mirrored surface into its registry vocabulary.

```text
table_name = registry resolution key
row_id = state target
column_data_jsonb = observed state
relation_hint_jsonb = candidate topology edges
change event = attention/diff evidence
```

The table name becomes the first stable key for resolving a topology surface.

Later, columns and relations can be promoted into stronger semantic axes:

- table name -> registry basis vocabulary;
- column names -> semantic/projection axes;
- row state -> topology state;
- relation hints -> topology edge candidates;
- diffs/logs -> SQL Attention evidence.

Promotion should happen by evidence, not by assuming the whole legacy schema is already Topolactor-native.

## API Fork Event Model

After the mirror exists, the existing API does not need to understand the full Topolactor runtime.

It only needs to fork change information.

Minimal fork event:

```text
api_fork_event:
  table_name
  row_id / primary_key
  operation: create | update | delete | transition
  changed_data_jsonb or diff_jsonb
  actor optional
  source optional
  occurred_at optional
```

That is enough for Topolactor to resolve the table through the registry and update attention, diff, projection, or island state.

```text
existing API
-> sends table_name + change_info
-> Topolactor resolves table_name through registry
-> mirror/diff/attention surfaces update
-> island projection can refresh
```

The existing API does not need to embed Topolactor design knowledge. It only reports what table changed, which row changed, and what changed.

## Island Embedding

A Topolactor island is a bounded projection surface embedded inside the existing system.

An island can be:

- a contextual recommendation panel;
- a dynamic admin table/view;
- a workflow state inspector;
- an audit/diff viewer;
- a topology-aware search panel;
- a UI component preview generated from registry topology.

The island is not a full replacement screen.

It is a controlled runtime projection from mirrored topology.

Minimum island contract:

```text
island_id
legacy_surface
table_name
runtime_route
render_target
failure_policy
owner_boundary
promotion_condition
rollback_path
```

An island should be removable, observable, and bounded.

## Authority Boundary

During island adoption, authority must be explicit.

### Existing system authority

The existing system owns:

- current production data writes;
- legacy API contract stability;
- existing user workflow continuity;
- current access-control enforcement unless delegated.

### Topolactor island authority

Topolactor owns:

- mirrored topology interpretation;
- registry / projection / route metadata;
- island runtime behavior;
- topology-driven UI projection;
- island-specific audit and CI diagnostics.

The default mode is read-only observation:

```text
existing system = source of truth
Topolactor = mirror + attention + island projection
```

Write authority should be promoted only after ownership, rollback, and failure behavior are explicit.

## Mirror Modes

Recommended mirror modes:

```text
READ_ONLY_MIRROR
  Topolactor observes existing table state and projects islands.

SHADOW_WRITE_MIRROR
  Topolactor records intended writes or diffs without owning production writes.

DUAL_WRITE_GUARDED
  Topolactor and the existing system both write under explicit reconciliation rules.

PROMOTED_AUTHORITY
  Topolactor owns the selected surface after human approval.
```

Default mode should be `READ_ONLY_MIRROR`.

## API Fork Policy

An API fork must not silently replace existing behavior.

Each forked event/route should declare:

- source API route or event;
- table name;
- row identity key;
- read/write mode;
- owner boundary;
- failure behavior;
- rollback path;
- test evidence;
- promotion condition.

Silent fallback is prohibited.

If Topolactor cannot resolve the table name or registry route, it should return an explicit error or defer by declared policy.

## Operational Sequence

```text
1. Select legacy surface
2. Mirror relevant physical table state into jsonb
3. Register table_name and row state into Topolactor registry surfaces
4. Fork API change events with table_name + change_info
5. Embed bounded UI island
6. Run CI / semantic checks against island boundary
7. Observe usage, diffs, transitions, and failures
8. Promote, expand, or discard the island
```

This sequence supports gradual adoption without pretending that the old system has already disappeared.

## Relation to SQL Attention

Existing systems already contain attention evidence:

- table update frequency;
- row transition history;
- update recency;
- joins and foreign keys;
- logs and diffs;
- workflow sequence;
- error frequency.

By mirroring physical table state and API change events, Topolactor can observe this evidence as SQL Attention input.

This is the important point: adoption does not start from a blank registry. It can start from the topology already latent in the existing system.

## Anti-Patterns

Avoid:

- full rewrite before island proof;
- copying legacy tables into a new CRUD shell and calling it topology;
- silently replacing existing API behavior;
- embedding UI islands without owner/failure boundaries;
- promoting write authority before rollback semantics exist;
- treating mirrored data as source of truth before promotion;
- trying to fully normalize legacy relations before the first mirror;
- letting agents mark scaffolded islands as production-ready without evidence.

## Minimal First Island

A safe first island should be:

- read-only;
- admin-visible first;
- backed by mirrored table state;
- driven by `table_name + change_info` fork events;
- projected into a small existing screen region;
- independently removable;
- covered by route and projection checks;
- documented as partial until promoted.

Good first candidates:

- audit panel;
- contextual search;
- recommendation sidebar;
- topology state inspector;
- dynamic read-only table projection.

## Summary

Topolactor can enter an existing system through scaffolded islands.

The path is:

```text
mirror physical table state into jsonb
-> register table surface
-> fork API change events
-> embed island
-> observe topology
-> promote by evidence
```

The migration unit is not the whole application.

The migration unit is a governed island.
