# SQL Attention in topolactor

## Positioning

SQL Attention is topolactor’s DB-native way to observe **topology continuity** and **attention weight**. It is not SQL-based dot-product attention and is not defined as a recommendation widget or a simple ranking feature.

## Concept

- topology hub space is treated as a linear/relational semantic space,
- observed aggregates (transitions, continuity, weighted signals) become attention evidence,
- registry and context units act as loop/collapse control boundaries.

In this model, SQL aggregates are operational attention weights over topology history, not a detached analytics dashboard. SQL Attention does not reproduce Transformer QK inner product over all elements.

## Decomposition

- **Theta / neighborhood:** narrows candidates via registry_id references, relation bindings, topology continuity, and indexed DB structure.
- **Norm / impedance / weight:** observes excitation strength via aggregation, transition, recency, frequency, diff, and logs.
- **Projection cache:** `vector_sparse` / `l2_norm` are rebuildable helper projections, not meaning SoT.

## What it is not

- not SQL-based QK dot-product reproduction,
- not full-element inner product brute force in RDB,
- not manual token vector SoT or vector DB-style ownership,
- not only a recommendation UI,
- not merely a ranking table.

## Status

- **Implemented now:** SQL-backed context-route recommendation and policy-driven weighting surfaces.
- **Design-guarded:** attention interpretation as topology continuity observation.
- **Future:** planned expansions explicitly marked in SSOT docs.
