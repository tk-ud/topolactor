# Agent Task List — PR336 workflow boundary hardening

## Blocking (resolved in branch — verify on merge)

- [x] `TryProjectWiringAsync` uses `topology.physical_tables.table_ref` (SSOT); legacy `dbTableName` accepted at API boundary.
- [x] Hub grouping primary UI on `/admin/manifests`; contents shows readonly summary + promote gate.
- [x] `ManifestScreenOperationDeriver` uses manifest-scoped target/layer (list vs detail no longer share `admin/default/entity/Read`).

## Implementation gap (explicit — not blocking promote path)

- [ ] Contents UI: structured inputs for relation/join, aggregation viewing key, aggregation display columns.
      → SSOT: `admin-console-workflow-ssot.yaml` db_design; current: tableRef, columns, searchTargets, aggregationSpec string only.
- [ ] Backend: persist structured relation/join + aggregation display fields on `screen_data_shape` topology extension.
      → Depends on schema design in `docs/design/db-schema.yaml` + validator updates.
- [ ] Promote: explicit validation when `table_ref` not found in `topology.physical_tables` (currently skips wiring insert silently).
      → Prefer explicit skipped status in projection result vs silent no-op.

## Optional follow-up

- [ ] `product.dynamic_support_nocode_loop` manual acceptance (unchanged from roadmap).
- [ ] Auto-refresh dispatcher axes on contents save when manifest_key already set on manifests page (partial: refresh on assign_screen_data_shape + assign_hub_grouping).
