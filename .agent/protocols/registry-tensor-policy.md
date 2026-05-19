# Registry Tensor Policy

## Purpose

Define mandatory audit interpretation for registry/topology/attention/UI/runtime surfaces.

Registry is topology vocabulary basis (tensor basis / vector basis), not a mere dictionary/config table.

## Mandatory interpretation

- `registry id` is a topology vocabulary axis basis.
- `registry id` combinations are sparse vectors / tensor coordinates.
- DB / UI / endpoint / runtime / scheduler / function / CI-diagnostic are projection or expansion surfaces of the same registry tensor.
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

## Drift / GAP classification (blocking unless explicitly out-of-scope)

Treat the following as drift/GAP during audit:

1. registry is explained as dictionary/config only
2. tensor projection is decomposed into disconnected CRUD implementation units
3. UI topology is closed into frontend-local wiring/state authority
4. endpoint/scheduler/function are separated from registry tensor
5. runtime canonical route is not treated as tensor expansion
6. SQL Attention is reduced to recommendation UI only, simple aggregation, or ranking-only semantics
7. semantic continuity is not verified across projection surfaces

## Gate usage

- Keep rule.md lightweight and use this protocol for detailed audit semantics.
- Apply this policy in completion-facing summaries and PR descriptions without copying governance source body.
