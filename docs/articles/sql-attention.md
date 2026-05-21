# SQL Attention in topolactor

## Positioning

SQL Attention is topolactor’s DB-native way to observe **topology continuity** and **attention weight**. It is not SQL-based dot-product attention and is not defined as a recommendation widget or a simple ranking feature.

## Concept

- topology hub space is treated as a linear/relational semantic space,
- observed aggregates (transitions, continuity, weighted signals) become attention evidence,
- registry and context units act as loop/collapse control boundaries.

In this model, SQL aggregates are operational attention weights over topology history, not a detached analytics dashboard. SQL Attention does not reproduce Transformer QK inner product over all elements.

## Runtime intuition

The public intuition is simple: observe logs, keep a compact current basis, and only explore registry/composition neighbors when the pressure level changes.

For example, table change pressure, column candidate pressure, and UI operation pressure can be treated as a small pressure vector. A simple L2 norm can act as a level signal, but the formula is not the main point. The important boundary is that aggregation only prepares the attention query; the attention observation is completed when neighbor evidence is recorded.

## Decomposition

- **Theta / neighborhood:** narrows candidates via registry_id references, relation bindings, topology continuity, and indexed DB structure.
- **Norm / impedance / weight:** observes excitation strength via aggregation, transition, recency, frequency, diff, and logs.
- **Projection cache:** `vector_sparse` / `l2_norm` are rebuildable helper projections, not meaning SoT.

## Implemented SQL observation surfaces

The SQL observation surface for SQL Attention already exists in `db/context_route_tables.sql`.
These tables are the implemented DB-side observation and materialized-signal layer, while full runtime completion remains governed by roadmap status.

- `context_event`: append-only operation event log. It records session, role, table, operation, and active `token_ids` at operation time.
- `context_event_vector_cache`: rebuildable multi-hot event vector cache. `vector_sparse` maps `token_id` to `1.0`; `context_token_registry.value` is not a computation weight.
- `context_prefix_vector_cache`: rebuildable prefix vector cache for nearest-prefix neighborhood filtering.
- `context_transition_stats`: transition aggregate for `prev_operation -> next_operation` probabilities.
- `context_hub_recommendation_current`: rebuildable hub-attention materialized current with cosine, relation weight, statistical weight, EMA/trend, feedback adjustment, attention score, and evidence JSON.
- `context_hub_feedback_event`: append-only feedback event log for selected / ignored / missing-candidate weight updates.

## Logs model boundary

Newer SQL Attention logs design separates three roles:

- signal sources observe physical-side pressure,
- current keeps the calculation basis,
- attention evidence records registry-neighbor results.

This keeps the public concept simple while leaving exact schema and runtime policy to the design SSOT files.

## Implementation boundary

The DB schema above is an implemented SQL Attention observation surface. It is not the runtime authority by itself:

- topology meaning remains in registry tensor continuity, relation bindings, jsonb/promoted-column observations, and logs,
- caches and recommendation-current rows are rebuildable projections,
- runtime policy values must be read from `function_parameters`, not hardcoded,
- roadmap entries may still be `partial` when runtime wiring, CI coverage, or production hardening is incomplete.

## What it is not

- not SQL-based QK dot-product reproduction,
- not full-element inner product brute force in RDB,
- not manual token vector SoT or vector DB-style ownership,
- not `context_token_registry.value` as sparse-vector weight,
- not only a recommendation UI,
- not merely a ranking table,
- not completed by aggregate counts alone.

## Status

- **Implemented now:** SQL-backed context-route recommendation tables, hub-attention materialized current, append-only feedback events, and policy-driven weighting surfaces.
- **Design-guarded:** attention interpretation as topology continuity observation.
- **Partial outside SQL:** runtime completion, production hardening, and CI coverage follow the roadmap / implementation registry.
- **Future:** planned expansions explicitly marked in SSOT docs.
