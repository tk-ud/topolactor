# SQL Attention Logs SSOT

## Status

This Markdown is the semantic SSOT for SQL Attention.

It defines the meaning, calculation route, and observation boundary of SQL Attention. Structural contracts, policy keys, thresholds, schema fields, function contracts, trigger boundaries, and implementation order belong to `docs/design/sql-attention-logs-ssot.yaml`.

## Core definition

SQL Attention is a DB-native hub-attractor observation model.

It is not SQL-side Transformer QK dot-product reproduction, not topology search by itself, and not registry search by itself.

```text
SQL Attention
= physical pressure current
+ hub / attractor current
+ attention evidence plane
```

The target is `hubs.*` Tensor / attractor continuity. `topologys.*` and registry surfaces are projection/support surfaces, not the direct SQL Attention target.

## Observation route

SQL Attention observes two current planes and records their contact as evidence.

```text
logs.* signal sources
→ logs.current
→ physical pressure current

hubs.* / hub-attractor state
→ logs.hub_current
→ hub / attractor current

logs.current × logs.hub_current
→ logs.attention
→ hub-attractor evidence
```

Meaning:

```text
logs.current
= physical-side pressure basis
= table / column / jsonb_path / operation / component pressure

logs.hub_current
= hub-side Tensor / attractor current
= hub field state used to observe movement and phase

logs.attention
= evidence plane
= physical pressure × hub current contact result
```

`logs.current` alone is not SQL Attention completion. SQL Attention completes only when hub-attractor evidence is recorded into `logs.attention`.

## Parent / child boundary

SQL Attention and `context_hub_recommendation_current` are related by parent/child semantics, not by identity.

```text
SQL Attention
= parent observation model
= topology gravity / hub-field distortion / expansion pressure / phase candidate / collapse tendency
= observes how the hub Tensor / attractor field wants to move or expand

context_hub_recommendation_current
= child projection current
= topology-internal discrete recommendation
= ranks enum / token / state / operation-like candidates inside the existing topology
```

`context_hub_recommendation_current` may use attention-like evidence, EMA, feature crossing, statistics, and feedback. It is still a child projection surface. It is not SQL Attention itself and not the hub/attractor expansion mechanism.

## Evidence meaning separation

SQL Attention keeps three meanings separate.

```text
statistics
= convergence confidence / stability / continuity

Attention
= current excitation / neighbor hit strength

Phase Attention
= exploratory variance / shifted candidate direction
```

These must not be collapsed into a single score at the evidence layer.

```text
statistics_json / ema_score
= stable confidence layer

l2_norm / vector_json / neighbor_score
= excitation and convergent neighbor-hit layer

phase_vector_json
= exploratory phase-shifted candidate layer
```

Meaning:

```text
vector_json
= convergent hub-attractor neighbor hit

phase_vector_json
= divergent / exploratory candidate direction
```

## Phase Attention / quaternion semantics

Phase Attention describes how an observed hub-attractor hit becomes a shifted candidate direction.

It uses normal quaternion notation:

```text
q = w + xi + yj + zk
```

Semantic assignment:

```text
q
= attractor

w
= real scalar from l2_norm

x / y / z
= hub-side record-count bases for table / column-axis / UI-operation axes

i / j / k
= movement amounts on each axis
```

The movement meaning is:

```text
L2 norm gives the real scalar w.
w is evaluated against x / y / z hub-side record-count bases.
The resulting axis movements i / j / k are z-score normalized and clamped.
q = w + xi + yj + zk is the attractor movement expression.
```

This is not Manifest-driven movement. Manifest / policy may bound runtime execution, persistence, and exploration budget, but it is not the mathematical source of phase movement.

## Completion boundary

Dangerous misunderstanding:

```text
logs aggregation completed = SQL Attention completed
```

Correct boundary:

```text
logs aggregation completed
= attention-query basis is ready

hub-attractor evidence saved to logs.attention
= SQL Attention observation completed

phase_vector generated on logs.attention
= phase candidate visible for later review / aggregation

adoption / migration / column promotion
= separate implementation path
```

`phase_vector` is candidate/evidence data. It must not auto-trigger registry mutation, migration, or column promotion.

## Target boundary clarification

```text
SQL Attention target
= hubs.* Tensor / attractor

topologys.*
= projected meaning space attached to hit hub / attractor

registry
= support / grammar surface

context_hub_recommendation_current
= topology-internal discrete recommendation child
```

## Non-goals

SQL Attention is not:

```text
- SQL-based QK dot-product reproduction
- full-element brute-force attention in RDB
- registry search as the primary target
- topology search as the primary target
- a recommendation UI by itself
- completed by aggregate counts alone
- completed by context_hub_recommendation_current alone
- automatic registry mutation
- automatic migration
- automatic column promotion
```
