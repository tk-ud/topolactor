# SQL Attention in topolactor

## Positioning

SQL Attention is topolactor’s DB-native way to observe **logs-side physical pressure over time**, **hub/vector continuity**, and **attractor evidence**. It is not SQL-based dot-product attention, not a recommendation widget, and not a simple ranking feature.

## Concept

SQL Attention observes two current planes separately:

- **physical current:** table, column / JSON path, candidate, operation, component, diff, and log pressure.
- **hub current:** hubs relation-map continuity represented as a bounded square-matrix field.

The SQL Attention target is hubs relation-map continuity and attractor evidence observation under topology definitions. Registry and context surfaces provide definitions and bindings, but registry itself is not the SQL Attention target, and topology semantic definitions are not mixed into hubs relation-map payloads.

In this model, SQL aggregates are operational evidence for attention, not detached analytics and not the meaning authority. SQL Attention does not reproduce Transformer QK inner product over all elements.

## Parent / child boundary

SQL Attention and the topology recommendation current are related, but they are not the same layer.

- **SQL Attention is the parent observation model.** It visualizes topology gravity, hub-field distortion, expansion pressure, phase candidates, and collapse-point tendency. Its role is to observe how hub/vector fields indicate movement toward/away from convergence points.
- **`context_hub_recommendation_current` is a child projection.** It is a topology-internal discrete recommendation current for enum / token / state / operation-like candidates inside the existing topology. Its role is to rank currently selectable discrete candidates, not to define SQL Attention itself.

The child recommendation current may use SQL Attention-style evidence, EMA, feature crossing, and feedback signals. It remains a projection current inside the topology, while SQL Attention remains the parent observation model for logs pressure and hub/vector-indicated attractor movement evidence.

## Runtime intuition

The public intuition is simple: observe two current planes, keep compact current bases, and run bounded hub/vector neighborhood exploration only when the level signal changes.

For example, table change pressure, column candidate pressure, and UI operation pressure can be treated as a small physical-pressure vector. Separately, hub continuity can be observed as a bounded square matrix over hub/relation vector surfaces. A simple L2 norm can act as a level signal, but the formula is not the main point.

The important boundary is that aggregation only prepares an attention basis. Attention observation is completed when hub/vector-indicated attractor evidence is recorded, with statistics, excitation, vector, phase-vector, and supporting evidence preserved as separate meanings.

## Decomposition

- **Physical pressure:** observes external excitation via table, column / JSON path, candidate, UI operation, diff, and log signals.
- **Hub field:** observes internal hub/relation vector continuity as a bounded square-matrix semantic field.
- **Theta / neighborhood:** narrows candidates via hub, relation, registry_id references, topology continuity, and indexed DB structure.
- **Norm / impedance / weight:** observes excitation strength via aggregation, transition, recency, frequency, diff, and logs.
- **Attention evidence:** records observations of hub/vector-indicated attractor hits, statistics, vector, phase-vector, neighbor score, and evidence JSON without collapsing them into one scalar.
- **Projection cache:** `vector_sparse` / `l2_norm` are rebuildable helper projections, not meaning SoT.

## Implemented projection and signal surfaces

`db/context_route_tables.sql` contains implemented context-route projection and signal surfaces used by the topology runtime. These tables are useful observation inputs and rebuildable outputs, but they are not the SQL Attention target by themselves.

- `context_event`: append-only operation event log. It records session, role, table, operation, and active `token_ids` at operation time.
- `context_event_vector_cache`: rebuildable multi-hot event vector cache. `vector_sparse` maps `token_id` to `1.0`; `context_token_registry.value` is not a computation weight.
- `context_prefix_vector_cache`: rebuildable prefix vector cache for nearest-prefix neighborhood filtering.
- `context_transition_stats`: transition aggregate for `prev_operation -> next_operation` probabilities.
- `context_hub_recommendation_current`: rebuildable topology-internal discrete recommendation current for context-route and topology-vector use. It ranks enum / token / state / operation-like candidates inside the existing topology. It is not the SQL Attention target and not the meaning authority.
- `context_hub_feedback_event`: append-only feedback event log for selected / ignored / missing-candidate weight updates.

## Logs model boundary

Newer SQL Attention logs design separates these roles:

- signal sources observe physical-side pressure,
- physical current keeps the physical pressure calculation basis,
- hub current keeps the hub / attractor square-matrix current basis,
- attention evidence records hub-attractor hit and phase-vector evidence.

This keeps the public concept simple while leaving exact schema and runtime policy to the design SSOT files.

## Implementation boundary

The DB schema above is an implemented projection / signal surface. It is not the runtime authority by itself:

- topology meaning remains in registry semantic matrix continuity, hub-attractor continuity, relation bindings, jsonb/promoted-column observations, and logs,
- caches and recommendation-current rows are rebuildable projections,
- `context_hub_recommendation_current` is the topology-internal discrete recommendation current, not SQL Attention itself,
- runtime policy values must be read from `function_parameters`, not hardcoded,
- roadmap entries may still be `partial` when runtime wiring, production hardening, or CI coverage remains incomplete.

## What it is not

- not SQL-based QK dot-product reproduction,
- not full-element inner product brute force in RDB,
- not manual token vector SoT or vector DB-style ownership,
- not `context_token_registry.value` as sparse-vector weight,
- not only a recommendation UI,
- not merely a ranking table,
- not completed by aggregate counts alone,
- not completed by topology-internal discrete recommendation current alone.

## Status

- **Implemented now (child projection surfaces):** SQL-backed context-route recommendation tables, topology-internal discrete recommendation current, append-only feedback events, and policy-driven weighting surfaces.
- **Partial (parent SQL Attention runtime):** SQL Attention observation runtime remains partial in roadmap tracking; logs.attention evidence-row persistence and some hub-current/runtime hardening are still open.
- **Design-guarded boundary:** child recommendation projection must not be treated as SQL Attention parent completion.
- **Future:** planned expansions are explicitly marked in roadmap/SSOT docs.


## Namespace roadmap (no DDL rename in this PR)

- `logs.*` remains the physical time-axis/log pressure/evidence namespace.
- Current projection surfaces are planned to move conceptually to `current.*` in a future migration task.
- Candidate mapping:
  - `logs.current` -> `current.table_relation` (or `current.physical_relation`)
  - `logs.hub_current` -> `current.hub_relation`
  - optional future: `current.ui_relation` (`id, parent_id, child_ids[]`)
- `logs.attention` remains under `logs.*` as append-only evidence (optional future rename: `logs.attention_evidence`).
