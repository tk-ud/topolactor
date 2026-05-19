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

## Gate usage

- Keep rule.md lightweight and use this protocol for detailed audit semantics.
- Apply this policy in completion-facing summaries and PR descriptions without copying governance source body.
