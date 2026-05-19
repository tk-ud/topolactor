# Registry Tensor Policy

## Purpose

Define mandatory audit interpretation for registry/topology/attention/UI/runtime surfaces.

Registry is topology vocabulary basis (tensor basis / vector basis), not a mere dictionary/config table.

## Mandatory interpretation

- registry table must be explained as semantic matrix.
- row = `registryId` / topology vocabulary basis.
- column = semantic axis / projection axis / wiring axis.
- value = weight / state / relation / coordinate / connection cell.
- `registry id` is a topology vocabulary axis basis.
- `registry id` combinations are sparse vectors / tensor coordinates.
- row/column/value combinations produce sparse vector / tensor coordinates.
- DB / UI / endpoint / runtime / scheduler / function / CI-diagnostic are projection or expansion surfaces of the same registry tensor.
- abstract function(tensor) executes projection/expansion onto each surface above.
- Frontend is a projection surface; it is not the meaning-judgment authority.

- abstract tables and registry tables are tensor surfaces; registry table structure itself spans vector space.
- cross-registry attention is mandatory-capable via registryId / axis / relation bindings.
- same-table count/sum/average/recency/frequency/transition aggregates are attention weights (observation), not meaning source itself.
- real/sys operational tables use id/state/jsonb as basic shape; they are observed entity surfaces connected from registry tensor, not the tensor basis itself.
- jsonb keys are promotable to columns as observable semantic axes for attention/audit/projection.
- logs.diffs is append-only diff surface with basic shape id/tableId/jsonb/created; it is audit/rebuild history and must not replace current-state SoT.
- vector_sparse/l2_norm caches are rebuildable materialized projections, never SoT and never direct authoring targets.
- SQL Attention is not SQL-based dot-product attention and must not be explained as SQL reproduction of Transformer QK inner product over all elements.
- SQL Attention decomposes attention-equivalent observation into neighborhood narrowing (registry_id/relation/topology continuity/indexed DB structure) and excitation strength observation (aggregation/transition/recency/frequency/diff/log signals).
- Theta/cosine may be used for neighborhood filtering only; semantic SoT remains registry tensor continuity, not cosine itself.
- norm/l2_norm is projection cache for strength/impedance/weight observation and must not be treated as manually-authored semantic value.
- a record that references registry IDs is treated as a tensor state.
- record does not own a manually-authored vector.
- record tensor coordinate is derived from registry_id references, relation bindings, jsonb/promoted columns, and logs/observations.
- vector_sparse/l2_norm are rebuildable projection caches derived from that record tensor state.

## Projection surface map

- DB: tensor persistence projection
- UI: tensor projection
- endpoint: tensor projection
- runtime: tensor expansion
- scheduler: tensor expansion
- function: tensor expansion
- CI/diagnostic: tensor continuity projection

## SQL Attention interpretation

- SQL Attention is tensor continuity / attention weight observation.
- Hub is treated as linear space.
- Aggregated statistics are attention weights.
- registryId is used as semantic loop/collapse control unit.
- Dynamic variable link logs are vocabulary expansion inputs.

## UI topology interpretation

- `packageId` / `layoutId` / `wiringId` are UI tensor axes.
- UI topology tables are tensor UI projection surfaces, not component catalogs.
- CRUD wiring / CanDI wiring are wiring-axis projections over UI topology tensor.

- code-only component/package without ID issuance + DB persistence is drift/GAP.
- components bucket is staging only; projection eligibility begins after package generator ID issuance and topology DB save.

## Drift / GAP classification (blocking unless explicitly out-of-scope)

Treat the following as drift/GAP during audit:

1. registry table is not treated as semantic matrix
2. registry row/column/value matrix interpretation is missing
3. registry is explained as dictionary/config/metadata only
4. tensor projection is decomposed into disconnected CRUD implementation units
5. abstract function(tensor) projection/expansion explanation is missing
6. UI topology is closed into component catalog or frontend-local wiring/state authority
7. endpoint/scheduler/function are separated from registry tensor
8. runtime canonical route is not treated as tensor expansion
9. data-driven explanation is reduced to external settings or CRUD metadata
10. SQL Attention is reduced to recommendation UI only, simple aggregation, or ranking-only semantics
11. code-only component/package is treated as acceptable topology entity
12. components bucket staging state is projected before ID issuance/DB save
13. semantic continuity is not verified across projection surfaces

14. registry table structure itself is not treated as vector space
15. cross-registry attention capability is ignored
16. same-table aggregates are treated as meaning source instead of attention weight observation
17. real/sys tables diverge from id/state/jsonb basic shape without explicit bounded reason
18. jsonb key promotion path to columnized observable axes is denied
19. logs.diffs append-only diff surface semantics (id/tableId/jsonb/created) are not preserved
20. vector cache (vector_sparse/l2_norm) is treated as SoT
21. record-level direct vector authoring is introduced
22. seed/UI/API directly authors vector_sparse as normal path
23. record is treated as manual vector container instead of tensor state derived from registry references
24. record tensor coordinate derivation path (registry/relation/jsonb+promoted/logs) is bypassed
25. SQL Attention is implemented as SQL-side QK all-element dot-product reproduction
26. full-element inner product brute force is introduced in RDB as normal attention path
27. token.value manual numeric inputs are treated as semantic SoT
28. cosine similarity is treated as semantic meaning body instead of neighborhood filter
29. vector_sparse/l2_norm is treated as record/event semantic SoT
30. DB topology candidate narrowing via relation/log/aggregation/index is ignored

## Gate usage

- Keep rule.md lightweight and use this protocol for detailed audit semantics.
- Apply this policy in completion-facing summaries and PR descriptions without copying governance source body.
