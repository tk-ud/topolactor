# DB Table Connectivity Audit — Manifest Hub / Runtime Manifest / UI Topology

status: compressed_follow_up_audit
scope: PR #313 SSOT correction
last_updated: 2026-05-30

## Audit method

- Connectivity was checked with `rg` over `backend`, `frontend`, `db`, `docs`, and `.agent`; the repository has no top-level `tests` directory, so backend/frontend test paths are covered through `backend` and `frontend`.
- This follow-up keeps only summary, risk, decision, and TODO information needed for migration planning.
- No SQL, backend, frontend, migration, or new-file change is part of this audit correction.

## Corrected active path

- `topologys.registrar_entries`: physical table catalog; future `physical_table_id bigint` remains the id referenced by relation config entries.
- `hubs.hubs`: one topology meaning space; pseudo-RDB physical table group; manifest projection unit.
- `hubs.hubs.relation`: JSONB physical table relation config. Minimum entry shape is `{ "id": <physical_table_id>, "relationKey": "...", "joinType": "..." }`.
- `manifest`: runtime dispatch distribution wiring; not Manifest Hub and not UI component package.
- `hubs.hub_relations`: topology sequence / continuous processing order between hubs attached to a manifest; concept is kept, schema requires redefinition.
- `ui_component_*` / `ui_topology_tensor`: UI component/package/layout/wiring/projection DB authority.
- `logs.*`: physical table pressure/current/attention evidence, not relation config or hub sequence.
- `context_*`: operation log / recommendation learning and rebuildable caches.

## Meaning collision inventory

1. `registrar_entries` vs `physical_table_id`: catalog authority exists, but the future numeric physical table id still needs migration.
2. `hubs.hubs.relation` vs `relation_registry_id`: relation is JSONB physical table join config, not relation vocabulary.
3. `hubs.hub_relations` vs `hubs.hubs.relation`: hub_relations chains topology meaning spaces; hubs.relation configures physical table joins inside one hub.
4. `manifest` vs `hubs.hubs`: manifest dispatches runtime/projection wiring; hubs.hubs is an attachable topology meaning space.
5. `manifest ui_projection packageIds` vs `packages` / `ui_component_package`: target package/tensor reference remains unresolved.
6. `ui_component_registry` vs `components`: active normalized registry conflicts with legacy UI builder table.
7. `ui_component_package` vs `packages`: active normalized package table conflicts with legacy package table.
8. `ui_package_component_map` vs `packages.layout`: normalized membership conflicts with legacy JSON layout binding.
9. `design.classname/tailwind` vs `css-dictionary` / `topology-layout-class`: free-form classes must not bypass style/layout vocabularies.
10. `hubs.hub_relations.weight` vs sequence/current/attention: weight is not sequence authority and not dynamic pressure evidence.
11. `structure_maps` vs `manifest`: attractor route resolution/cache must not be conflated with runtime manifest dispatch distribution.
12. `entities` / `content_entity_drafts` vs physical table records: payload/draft tables are not the physical table catalog.

## Table decisions

| table | status | risk | decision | todo |
|---|---|---|---|---|
| `topologys.registrar_entries` | ssot_gap | Current SQL has UUID registrar entry id while relation config needs `physical_table_id`. | redefine | Add physical-table-id migration plan. |
| `hubs.hubs` | connected_but_semantic_drift | Previous audit incorrectly treated relation as an unordered numeric array; current SQL still uses `relation_registry_id`. | redefine relation from array semantics to JSONB relation config | Migrate to `hub_id/status/relation jsonb/created_at/updated_at`; validate `id/relationKey/joinType`. |
| `hubs.hub_relations` | connected_but_semantic_drift | Current `hub_id + relation_registry_id + weight` shape does not represent manifest-scoped hub sequence. | keep concept; redefine schema later; not a removal candidate | Redefine as manifest hub chain with source/target hubs, `sequence_index`, optional `relation_config`, status, timestamps. |
| `manifest` | connected | Projection package ids still need target authority decision. | keep | Decide whether package refs converge on `ui_component_package` or `ui_topology_tensor`. |
| `topologys.structure_maps` | connected_but_semantic_drift | Can duplicate runtime manifest dispatch semantics. | needs_question | Decide runtime resolution cache vs manifest integration. |
| `topologys.entities` | connected_but_semantic_drift | Can be confused with physical table records. | needs_question | Decide content payload cache vs migration/removal. |
| `topologys.content_entity_drafts` | connected_but_semantic_drift | Draft payload staging overlaps future physical-table authoring. | needs_question | Redefine with `topologys.entities`. |
| `ui_component_registry` | connected | Duplicate concept with legacy `components`. | keep | Migrate active component authority away from legacy table. |
| `ui_component_package` | connected | Duplicate concept with legacy `packages`. | keep | Resolve manifest projection package references. |
| `ui_package_component_map` | connected | Duplicates `packages.layout` membership semantics. | keep | Migrate legacy package layout binding. |
| `ui_layout_registry` | connected | Display order must not move into hub relation config. | keep | Keep UI order on UI topology surfaces. |
| `ui_wiring_registry` | connected | Wiring must not move into Manifest Hub relation config. | keep | No immediate action beyond migration alignment. |
| `ui_topology_tensor` | connected | Needs projection reference decision with manifest. | keep | Decide package/tensor reference target. |
| `logs.current` | connected | Can be confused with hub relation config or sequence. | keep | Align physical-table-id type with registrar migration. |
| `logs.hub_current` | connected | Name can be confused with `hubs.hubs`. | keep | Keep pressure semantics separate. |
| `logs.attention` | connected | Evidence can be mistaken for order/weight. | keep | Keep append-only evidence semantics separate. |
| `context_token_registry` | connected | Low; token dictionary is separate from relation config. | keep | None in this pass. |
| `context_event_vector_cache` | connected | Rebuildable cache must not become SSOT. | keep | None in this pass. |
| `context_record_snapshot_cache` | ssot_gap | Documented but no direct repository connection found. | needs_question | Decide implement/remove/future cache. |
| `context_cluster` | ssot_gap | Optional output without connected runtime can look canonical. | needs_question | Decide with recommendation analytics scope. |
| `context_cluster_label` | ssot_gap | Depends on unresolved clustering lifecycle. | needs_question | Decide with `context_cluster`. |
| `context_drift_signal` | ssot_gap | Optional analytics output may look active. | needs_question | Decide keep/redefine/delete after analytics scope. |
| `logs.usage_metrics` | obsolete_candidate | Overlaps with SQL Attention pressure/current surfaces. | delete_candidate | Decide whether semantics survive in logs/current evidence. |
| `logs.promotion_candidates` | obsolete_candidate | Overlaps with recommendation/promotion evidence. | delete_candidate | Confirm replacement or removal path. |
| `components` | duplicate_candidate | Legacy table duplicates `ui_component_registry`. | migrate | Decide compatibility period. |
| `design` | duplicate_candidate | Free-form style storage conflicts with CSS/layout vocabularies. | migrate | Migrate into token/layout refs or remove. |
| `packages` | duplicate_candidate | Legacy package/layout table conflicts with normalized package/map/tensor authority. | migrate | Resolve manifest package reference target. |
| `topologys.demo_state_transitions` | connected_but_semantic_drift | Demo state can look canonical. | delete_candidate | Confirm demo compatibility before removal. |
| `topologys.topology_edit_log` | connected_but_semantic_drift | Overlaps context/log evidence. | redefine | Decide migrate to context/log evidence or remove. |

## Summary classifications

connected_tables:
- `manifest`
- `ui_component_registry`
- `ui_component_package`
- `ui_package_component_map`
- `ui_layout_registry`
- `ui_wiring_registry`
- `ui_topology_tensor`
- `logs.current`
- `logs.hub_current`
- `logs.attention`
- `context_token_registry`
- `context_event_vector_cache`

drift_tables:
- `hubs.hubs`
- `hubs.hub_relations`
- `topologys.entities`
- `topologys.content_entity_drafts`
- `topologys.structure_maps`
- `topologys.demo_state_transitions`
- `topologys.topology_edit_log`

duplicate_candidates:
- `components`
- `design`
- `packages`

obsolete_candidates:
- `logs.usage_metrics`
- `logs.promotion_candidates`

unknown_or_ssot_gap_tables:
- `topologys.registrar_entries`
- `context_record_snapshot_cache`
- `context_cluster`
- `context_cluster_label`
- `context_drift_signal`

questions:
- Should `topologys.entities` remain as content payload cache or be deleted/migrated?
- Should `topologys.structure_maps` remain as runtime resolution cache or be integrated into manifest dispatch distribution?
- Should `components` / `design` / `packages` be migrated immediately or kept through a compatibility window?
- Should manifest `ui_projection.packageIds` converge on `ui_component_package` or `ui_topology_tensor`?
- Should `logs.*.physical_table_id` converge on bigint or retain text compatibility?
