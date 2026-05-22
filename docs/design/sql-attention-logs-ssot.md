# SQL Attention Logs SSOT

## 1. Status

This markdown is the semantic SSOT for SQL Attention logs, including l2 norm observation meaning, norm-level route meaning, and physical table pressure meaning.

- Focus: formulas, meanings, routes, boundaries.
- Structural contracts (policy/schema/function/trigger/implementation order) are managed in `docs/design/sql-attention-logs-ssot.yaml` (including keys such as neighbor_score_min and phase_expansion_limit).

## 2. Core Definition

SQL Attention is a DB-native hub-attractor observation model.

- SQL Attention is **not** SQL-side Transformer QK dot-product reproduction.
- SQL Attention target is `hubs.*` Tensor / attractor (for example attractor_key-aligned hub-side semantics).
- `topologys.*` and registry surfaces are projection/support layers, not the SQL Attention target itself.
- SQL Attention is not topology search.
- SQL Attention is not registry search.

## 3. Observation Planes

SQL Attention observation uses three distinct planes:

- `logs.current` = physical pressure current.
- `logs.hub_current` = hub / attractor current.
- `logs.attention` = evidence plane.

These planes must remain semantically separate.

## 4. Main Attention Route

Main Attention is the primary SQL Attention exploration route and represents hub-attractor exploration.

```text
logs.current × logs.hub_current
→ hub-attractor neighbor search
→ neighbor_score / hit_rank / vector_json
→ logs.attention
```

This is the core search body of SQL Attention.

## 5. Phase Attention Route

Phase Attention is a post-main auxiliary route.

```text
logs.attention.vector_json
→ q = w + xi + yj + zk
→ phase_vector_json
```

- Main Attention is the primary exploration.
- Phase Attention is downstream auxiliary transformation.
- Phase Attention must not be treated as SQL Attention primary exploration.

## 6. Parent / Child Boundary

SQL Attention and topology recommendation are parent/child related, not identical.

- SQL Attention = parent observation model over hub-attractor field movement/expansion pressure.
- Topology recommendation currents = child projection surface for discrete candidate ranking.

The child projection must not be treated as SQL Attention itself.

## 7. Evidence Meaning Separation

Do not collapse the three evidence meanings into a single score.

```text
statistics      = convergence confidence / stability / continuity
Attention       = current excitation / neighbor hit strength
Phase Attention = exploratory variance / shifted candidate direction
```

`statistics`, `Attention`, and `Phase Attention` each preserve different evidence meaning.

## 8. Quaternion / Attractor Semantics

Phase semantics follow:

- `q = w + xi + yj + zk`
- `q = attractor`
- `w` = real scalar derived from L2 norm.
- `x / y / z` = hub-side record-count bases.
- `i / j / k` = movement amounts on each axis.

`phase_vector` is evidence/candidate data and is not automatic mutation.

## 9. Completion Boundary

SQL Attention completion means attention evidence has been produced and stored.

- Aggregation/current refresh alone is not full SQL Attention completion.
- Phase vector generation does not mean adopted topology state.
- Adoption/migration is a separate implementation path.

## 10. Target Boundary

Target boundary:

- Primary target: `hubs.*` Tensor / attractor semantics.
- Not primary target: direct `topologys.*` / registry search as SQL Attention body.
- `topologys.*` and registry are projection/support layers consuming evidence.

## 11. Non-goals

- Reproducing Transformer QK Attention in SQL.
- Treating Phase Attention as primary exploration.
- Collapsing statistics + Attention + Phase Attention into one score.
- Auto-mutating registry/topology state from `phase_vector` evidence.
