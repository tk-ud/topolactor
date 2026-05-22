# Registry Tensor Policy

This protocol is condition-triggered. It is not an always-on read.

## Workflow Guard

- Use as a reference point during READ_TARGET_SURFACES / DEFINE_SCOPE / JUDGMENT when registry tensor semantics are in scope.
- Do not use this protocol to bypass workflow order.

## Trigger scope

Run this policy when changes touch registry tensor semantics, topology semantics, SSOT semantics, recommendation runtime semantics, registrar UI spec semantics, or topology governance policy text.

## Purpose

Define mandatory audit interpretation for registry/topology/attention/UI/runtime surfaces.

When SQL Attention logs semantics are in scope, `docs/design/sql-attention-logs-ssot.md` and `docs/design/sql-attention-logs-ssot.yaml` are the canonical parent definition.
This protocol is an audit interpretation layer, not a replacement SSOT.

Registry is a topology definition/configuration basis (tensor/vector axis definitions; tensor basis / vector basis), not a mere dictionary/config table and not a hub/attractor/Attention entity itself.

## Mandatory interpretation

- registry/registrar defines axes, settings, and vocabulary for target surfaces (hub/topology/UI/schema/state/relation).
- registry must not be treated as disconnected config only.
- registry must not be treated as hub/attractor/Attention entity itself.
- hub is a relation node.
- relation-bearing hub can be treated as tensor/vector coordinates.
- vectorized hub relations support neighborhood search.
- attractor is a vector convergence point.
- Attention is an observation point/operation, not the attractor.
- SQL Attention is an observation layer over logs pressure and hub/vector-indicated attractor evidence; it is not an attractor generator.
- topology is semantic definition space.
- hubs are relation maps over already-defined ids/references.
- topology semantic definitions must not be mixed into hubs relation-map payload/body.
- equal relation shape uses: id, related_ids[].
- parent-child relation shape uses: id, parent_id, child_ids[].
- UI relation shape uses: id, parent_id(physical_table_id_or_data_type_id), child_ids[](package_ids[]).
- package is UI payload bundle.
- relation-map columns can be normalized column-wise for neighborhood search.
- registry table must be explained as semantic matrix.
- row = `registryId` / topology vocabulary basis.
- column = semantic axis / projection axis / wiring axis.
- value = weight / state / relation / coordinate / connection cell.
- `registry id` combinations are sparse vectors / tensor coordinates.
- row/column/value combinations produce sparse vector / tensor coordinates.
- DB / UI / endpoint / runtime / scheduler / function / CI-diagnostic are projection or expansion surfaces of the same registry tensor.
- abstract function(tensor) executes projection/expansion onto each surface.
- Frontend is a projection surface; it is not the meaning-judgment authority.
- abstract tables and registry tables are tensor surfaces; registry table structure itself spans vector space.
- cross-registry attention is mandatory-capable via registryId / axis / relation bindings.
- same-table count/sum/average/recency/frequency/transition aggregates are attention weights (observation), not meaning source.
- real/sys operational tables use id/state/jsonb as basic shape; they are observed entity surfaces connected from registry tensor.
- jsonb keys are promotable to columns as observable semantic axes for attention/audit/projection.
- logs.diffs is append-only diff surface with basic shape id/tableId/jsonb/created; it is audit/rebuild history and must not replace current-state SoT.
- vector_sparse/l2_norm caches are rebuildable materialized projections, never SoT and never direct authoring targets.
- SQL Attention is not SQL-based dot-product attention and must not be explained as SQL reproduction of Transformer QK inner product over all elements.
- SQL Attention parent decomposition semantics must be read from SQL Attention Logs SSOT; this protocol uses that meaning as an audit lens.
- Theta/cosine may be used for neighborhood filtering only; semantic SoT remains registry tensor continuity.
- norm/l2_norm is projection cache for strength/impedance/weight observation and must not be treated as manually-authored semantic value.
- a record that references registry IDs is treated as a tensor state.
- record tensor coordinate is derived from registry_id references, relation bindings, jsonb/promoted columns, and logs/observations.
- vector_sparse/l2_norm are rebuildable projection caches derived from record tensor state.

## Projection surface map

- DB: tensor persistence projection
- UI: tensor projection
- endpoint: tensor projection
- runtime: tensor expansion
- scheduler: tensor expansion
- function: tensor expansion
- CI/diagnostic: tensor continuity projection

## SQL Attention interpretation

- current namespace split roadmap (future task): keep logs.* for time-axis/evidence and move rebuildable current projections to current.* naming without changing runtime/DDL in this task.

- SQL Attention is logs-time-axis pressure + hub/vector continuity observation.
- Attention operation observes attractor evidence; it does not create attractors.
- Hub relation surfaces are vectorizable for neighborhood reads.
- Aggregated statistics are attention weights.
- registryId is used as semantic loop/collapse control unit.
- Dynamic variable link logs are vocabulary expansion inputs.

## UI topology interpretation

- `packageId` / `layoutId` / `wiringId` are UI tensor axes.
- UI topology tables are tensor UI projection surfaces, not component catalogs.
- CRUD wiring / CanDI wiring are wiring-axis projections over UI topology tensor.
- code-only component/package without ID issuance + DB persistence is drift/GAP.
- components bucket is staging only; projection eligibility begins after package generator ID issuance and topology DB save.

## Drift / GAP classification

Treat the following as drift/GAP during audit when trigger scope applies:

1. registry table is not treated as semantic matrix
2. registry row/column/value matrix interpretation is missing
3. registry is explained as dictionary/config/metadata only (disconnected config-only misread)
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
17. logs.diffs append-only diff surface semantics are not preserved
18. vector cache (vector_sparse/l2_norm) is treated as SoT
19. record-level direct vector authoring is introduced
20. cosine similarity is treated as semantic meaning body instead of neighborhood filter

21. real/sys tables diverge from id/state/jsonb basic shape without explicit bounded reason
22. jsonb key promotion path to columnized observable axes is denied
23. record tensor coordinate derivation path (registry/relation/jsonb+promoted/logs) is bypassed
24. SQL Attention is implemented as SQL-side QK all-element dot-product reproduction
25. full-element inner product brute force is introduced in RDB as normal attention path
26. token.value manual numeric inputs are treated as semantic SoT
27. vector_sparse/l2_norm is treated as record/event semantic SoT
28. DB topology candidate narrowing via relation/log/aggregation/index is ignored

## Gate usage

- Keep rule.md lightweight and use this protocol for detailed audit semantics.
- Apply this policy in completion-facing summaries and PR descriptions without copying governance source body.
- Classify drift/GAP using this protocol when trigger scope applies.


Additional required drift checks:

29. registry is treated as hub/attractor/Attention itself
30. Attention is treated as attractor
31. attractor is treated as SQL Attention result artifact

32. topology semantic definitions are mixed inside hubs relation-map payloads
33. UI relation is described as one-to-one physical_table-to-package binding
34. role/state/route/context axes are mixed into UI relation matrix body
