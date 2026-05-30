# DB Table Connectivity Audit — Canonical hubs / topology / logs split

status: compressed_follow_up_audit
scope: PR #313 three-schema SSOT correction
last_updated: 2026-05-30

## Audit method

- Connectivity was checked with `rg` over `backend`, `frontend`, `db`, `docs`, and `.agent`; this follow-up records only decision-critical summary / risk / TODO.
- No SQL, backend, frontend, migration, or new-file change is part of this audit correction.

## Canonical target mapping

| canonical target | current sources / drift | decision |
|---|---|---|
| `hubs.hub` | `hubs.hubs` | Canonical topology meaning space / pseudo-RDB physical table group / JSONB join definition owner. |
| `hubs.topology_manifests` | unqualified `manifest` responsibility subset | Hub-side grouping of `topology.wiring_physical_to_package` records. |
| `hubs.hub_relations` | current `hub_id + relation_registry_id + weight` shape | Keep concept; redefine as fixed hub order / UI transition order / topology meaning space sequence. |
| `hubs.current` | `logs.hub_current`, parts of `logs.current` | Current aggregate projection for SQL Attention / recommendation / cosine neighborhood. |
| `topology.physical_tables` | `topologys.registrar_entries` | Physical table catalog and physical table id authority. |
| `topology.wiring_physical_to_package` | unqualified `manifest`, UI topology wiring tables | Single-screen manifest wiring between physical table / hub / package / layout / wiring. |
| `topology.components_bucket` | `ui_component_bucket` | UI component candidate bucket under canonical topology schema. |
| `topology.components_style_design` | public `design`, CSS token refs | Component style design migration target. |
| `topology.components_layout_design` | `ui_layout_registry`, layout/tensor patch surfaces | Component layout design migration target. |
| `topology.components_package_design` | `ui_component_package`, `ui_package_component_map`, legacy `packages` | Component package design migration target. |
| `logs.physical_diff` | `logs.current` mutation/diff source responsibilities | Physical table diff/evidence layer, not current projection. |
| `logs.components_diff` | UI component/design/package change logs | UI component/design/package diff layer. |
| `logs.attention_diff` | `logs.attention` | Attention evidence / attention event diff layer. |
| `logs.hub_diff` | hub/topology meaning space changes | Hub diff layer. |

## Drift classification

public_or_unqualified_tables:
- status: schema_drift
- examples: `manifest`, `context_token_registry`, `context_event_vector_cache`, `ui_component_registry`, `ui_component_package`, `ui_topology_tensor`
- decision: migrate responsibilities into `hubs`, `topology`, or `logs`; do not treat public/unqualified names as SSOT.

topologys_schema:
- status: naming_drift
- decision: migrate to canonical `topology` schema; do not treat `topologys` as SSOT spelling.
- examples: `topologys.registrar_entries` -> `topology.physical_tables`; `topologys.structure_maps` / `topologys.entities` require responsibility reclassification.

legacy_ui_builder:
- status: duplicate_candidate
- examples: `components`, `design`, `packages`
- decision: migrate to `topology.components_*` surfaces; do not keep as canonical public tables.

## Meaning collision inventory

1. `hubs.hub.relation` vs `relation_registry_id`: relation is JSONB join config over `topology.physical_tables.physical_table_id`.
2. `hubs.hub.relation` vs UI order: relation is not UI display or transition order.
3. `hubs.hub_relations`: owns fixed hub/UI transition sequence; current `weight` is not sequence authority.
4. `hubs.current` vs `logs.*`: current aggregate projection is in `hubs.current`; logs are diff/evidence only.
5. SQL Attention / recommendation / cosine neighborhood target `hubs.hub` and `hubs.current`, not topology registry tables.
6. Unqualified `manifest` splits toward `topology.wiring_physical_to_package` and `hubs.topology_manifests`.
7. Public `context_*` placement remains a migration decision, not canonical public authority.
8. Public `ui_component_*` placement migrates toward `topology.components_*`.

## Remaining questions

- How should current `manifest.topology` fields split between `topology.wiring_physical_to_package` and `hubs.topology_manifests`?
- Which `context_*` tables belong under `topology` versus future recommendation-specific placement?
- How should `logs.current` / `logs.hub_current` split into `hubs.current` versus `logs.physical_diff` / `logs.hub_diff`?
