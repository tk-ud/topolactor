# design_change protocol

## workflow_guard
Use for worktype `design_change`.

## trigger_condition
SSOT/docs/external contract changes.

## judgment_scope
Design delta integrity and impact propagation.

## foundation_ssot_read_gate
Before judging or changing an SSOT surface, read top SSOT first:

1. `docs/framework-core.yaml`
2. `docs/framework-policy.yaml`

Additionally, `docs/design/db-schema.yaml` is mandatory (not optional target-specific reading) whenever the SSOT change touches DB / manifest / seed SQL / UI topology / package / layout / design / wiring / tensor persistence or translator adoption targets. `db/*.sql` is the canonical DDL/seed surface, but table authority, table role, and `manifest_reference` meaning must be cross-checked against this SSOT before the design change is judged correct.

## blocking_conditions
- SSOT change without impact propagation assessment.
- Route/vocabulary drift left unresolved in governance surfaces.
- DB/manifest/seed/UI-topology/package/layout/design/wiring/tensor-touching SSOT change made without cross-checking `docs/design/db-schema.yaml` table authority/role/`manifest_reference`.

## pass_conditions
- SSOT change impact handled.
- Related routing/check references updated consistently.
