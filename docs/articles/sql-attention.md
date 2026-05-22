# SQL Attention in topolactor

## Positioning

SQL Attention is topolactor’s DB-native way to observe **topology continuity**, **hub-attractor excitation**, and **attention evidence**. It is not SQL-based dot-product attention, not a recommendation widget, and not a simple ranking feature.

## Concept

SQL Attention observes two current planes separately:

- **physical current:** table, column / JSON path, candidate, operation, component, diff, and log pressure.
- **hub current:** hub / attractor continuity represented as a bounded square-matrix semantic field.

The SQL Attention target is the hub-attractor side: tensor, attractor, and collapse-point continuity. Registry and context surfaces provide vocabulary, bindings, and loop / collapse control boundaries, but registry rows are not the direct target of SQL Attention.

In this model, SQL aggregates are operational evidence for attention, not detached analytics and not the meaning authority. SQL Attention does not reproduce Transformer QK inner product over all elements.

## Runtime intuition

The public intuition is simple: observe two current planes, keep compact current bases, and run bounded hub-attractor exploration only when the level signal changes.

For example, table change pressure, column candidate pressure, and UI operation pressure can be treated as a small physical-pressure vector. Separately, hub continuity can be observed as a bounded square matrix over hub / attractor relations. A simple L2 norm can act as a level signal, but the formula is not the main point.

The important boundary is that aggregation only prepares an attention basis. Attention observation is completed when hub-attractor evidence is recorded, with statistics, excitation, vector, phase-vector, and supporting evidence preserved as separate meanings.

## Decomposition

- **Physical pressure:** observes external excitation via table, column / JSON path, candidate, UI operation, diff, and log signals.
- **Hub field:** observes internal hub / attractor continuity as a bounded square-matrix semantic field.
- **Theta / neighborhood:** narrows candidates via hub, relation, registry_id references, topology continuity, and indexed DB structure.
- **Norm / impedance / weight:** observes excitation strength via aggregation, transition, recency, frequency, diff, and logs.
- **Attention evidence:** records hub-attractor hits, statistics, vector, phase-vector, neighbor score, and evidence JSON without collapsing them into one scalar.
- **Projection cache:** `vector_sparse` / `l2_norm` are rebuildable helper projections, not meaning SoT.

## Implemented projection and signal surfaces

`db/context_route_tables.sql` contains implemented context-route projection and signal surfaces used by the topology runtime. These tables are useful observation inputs and rebuildable outputs, but they are not the SQL Attention target by themselves.

- `context_event`: append-only operation event log. It records session, role, table, operation, and active `token_ids` at operation time.
- `context_event_vector_cache`: rebuildable multi-hot event vector cache. `vector_sparse` maps `token_id` to `1.0`; `context_token_registry.value` is not a computation weight.
- `context_prefix_vector_cache`: rebuildable prefix vector cache for nearest-prefix neighborhood filtering.
- `context_transition_stats`: transition aggregate for `prev_operation -> next_operation` probabilities.
- `context_hub_recommendation_current`: rebuildable state / enum / token / operation recommendation current for context-route and topology-vector use. It is not the SQL Attention target and not the meaning authority.
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

- topology meaning remains in registry tensor continuity, hub-attractor continuity, relation bindings, jsonb/promoted-column observations, and logs,
- caches and recommendation-current rows are rebuildable projections,
- `context_hub_recommendation_current` is a state / enum recommendation current, not SQL Attention itself,
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
- not completed by state / enum recommendation current alone.

## Status

- **Implemented now:** SQL-backed context-route recommendation tables, state / enum recommendation current, append-only feedback events, and policy-driven weighting surfaces.
- **Design-guarded:** SQL Attention interpretation as physical-current and hub-current observation with hub-attractor evidence persistence.
- **Partial outside SQL:** runtime completion, production hardening, hub-current / attention-evidence wiring, and CI coverage follow the roadmap / implementation registry.
- **Future:** planned expansions explicitly marked in SSOT docs.
