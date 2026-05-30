# DB Table Connectivity Audit — SQL Attention / Recommend Target Correction

status: compressed_follow_up_audit
scope: PR #313 target-boundary correction
last_updated: 2026-05-30

## Audit method

- Connectivity was checked with `rg` over `backend`, `frontend`, `db`, `docs`, and `.agent`; this follow-up records only decision-critical summary / risk / TODO.
- No SQL, backend, frontend, migration, or new-file change is part of this audit correction.

## Canonical target mapping

| canonical target | current sources / drift | decision |
|---|---|---|
| `hubs.hub` | `hubs.hubs` | Canonical topology meaning space / pseudo-RDB physical table group / JSONB join definition owner; SQL Attention target. |
| `hubs.topology_manifests` | unqualified `manifest` responsibility subset | Hub-side grouping of `topology.wiring_physical_to_package`; SQL Attention z axis. |
| `hubs.hub_relations` | current `hub_id + relation_registry_id + weight` shape | Keep concept; redefine as fixed hub order / UI transition order / topology meaning space sequence; SQL Attention x axis. |
| `topology.physical_tables` | `topologys.registrar_entries` | Physical table catalog and physical table id authority. |
| `topology.wiring_physical_to_package` | unqualified `manifest`, UI topology wiring tables | Single-screen manifest wiring; Recommend target. |
| `topology.components_*` | `ui_component_*`, `components`, `design`, `packages` | UI component/design/package migration target for Recommend. |
| `context_* learning surfaces` | public `context_*` tables | Recommend learning surfaces; placement still migration decision. |
| `logs.current` | existing SQL Attention table | Preserve as physical table heat/current and physical_table_id excitation source. |
| `logs.hub_current` | existing SQL Attention table | Preserve as hub current projection. |
| `logs.attention` | existing SQL Attention table | Preserve as attention evidence and phase_vector_json carrier. |

## Drift classification

public_or_unqualified_tables:
- status: schema_drift
- examples: `manifest`, `context_token_registry`, `context_event_vector_cache`, `ui_component_registry`, `ui_component_package`, `ui_topology_tensor`
- decision: migrate responsibilities into `hubs`, `topology`, or retained learning surfaces; do not treat public/unqualified names as SSOT.

topologys_schema:
- status: naming_drift
- decision: migrate to canonical `topology` schema; do not treat `topologys` as SSOT spelling.
- examples: `topologys.registrar_entries` -> `topology.physical_tables`; `topologys.structure_maps` / `topologys.entities` require responsibility reclassification.

## Target boundaries

SQL Attention target:
- `hubs.hub`
- `hubs.hub_relations`
- `logs.current`
- `logs.hub_current`
- `logs.attention`

Recommend target:
- `topology.*`
- `topology.wiring_physical_to_package`
- `topology.components_*`
- `context_* learning surfaces`

## Phase Attention axis mapping

- `w = l2_norm = physical table heat = physical_table_id excitation strength from logs.current`.
- `x = hubs.hub_relations` fixed hub sequence / UI transition axis.
- `y = hubs.hub` topology meaning space axis.
- `z = hubs.topology_manifests` manifest grouping axis.
- `i/j/k = phase movement amount`, the movement vector over hubs space.

Movement rule:
- physical_table_id is attended by `w`.
- `w` strength controls phase movement amount.
- movement direction is based on cosine-neighborhood rate.
- weak `w` moves toward near neighbors; strong `w` moves toward farther candidates using cosine-neighborhood rate.

Boundary:
- `phase_vector_json` is auxiliary evidence for rotating/exploring `hubs.hub_relations`, `hubs.hub`, and `hubs.topology_manifests`.
- `phase_vector_json` is not generic diff and is not adopted topology state.
- generic `population_count` / `recordcount` values are implementation-derived observations, not canonical x/y/z semantics.
