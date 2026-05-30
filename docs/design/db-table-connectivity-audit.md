# DB Table Connectivity Audit — Manifest Hub / Runtime Manifest / UI Topology

status: draft_audit
scope: DB / Manifest / Hub / UI Topology meaning separation
last_updated: 2026-05-30

## Audit method

- Used `rg -n --fixed-strings "<table_name>" backend frontend tests db docs .agent` equivalents because repository guidance prohibits `grep -R`; the top-level `tests` path is absent, while `backend/tests` and `frontend/tests` are included through `backend` / `frontend`.
- No SQL migration, destructive SQL change, backend repository rewrite, or frontend UI rewrite is included in this audit.
- This document records connection state and decision class only; unresolved semantic choices remain `needs_question`.

## Active path fixed by SSOT

- `topologys.registrar_entries` = physical table catalog authority; future canonical id is `physical_table_id bigint`.
- `hubs.hubs` = Manifest Hub; one-screen unordered physical table group; future `relation bigint[]` is an unordered `physical_table_id` set.
- `manifest` = runtime dispatch distribution surface; owns dispatcher/runtime/projection wiring and is not Manifest Hub.
- `ui_component_*` / `ui_topology_tensor` = UI component/package/layout/wiring/projection composition DB authority.
- `logs.*` = physical table pressure/current/hub_current/attention evidence.
- `context_*` = operation log / recommendation learning.

## Meaning collision inventory

1. `registrar_entries` vs `physical_table_id`: current SQL has `registrar_entry_id uuid`; active design requires future `physical_table_id bigint` catalog authority.
2. `hubs.hubs.relation` vs `relation_registry`: future `relation bigint[]` is a physical table id set, not `relation_registry_id`.
3. `hubs.hub_relations` vs `hubs.hubs.relation`: legacy relation edge table must not be treated as Manifest Hub membership.
4. `manifest` vs `hubs.hubs`: runtime dispatch distribution and one-screen Manifest Hub are separate objects.
5. `manifest ui_projection packageIds` vs `packages` / `ui_component_package`: current comments and UI still point packageIds at legacy package semantics; target authority needs decision.
6. `ui_component_registry` vs `components`: normalized active registry conflicts with legacy component catalog.
7. `ui_component_package` vs `packages`: normalized active package authority conflicts with legacy package table.
8. `ui_package_component_map` vs `packages.layout`: normalized join table conflicts with legacy JSON layout binding.
9. `design.classname/tailwind` vs `css-dictionary` / `topology-layout-class`: legacy free-form class storage must not bypass style vocabulary SSOTs.
10. `hubs.hub_relations.weight` vs `logs.current` / `logs.hub_current` / `logs.attention`: legacy static weight is not dynamic pressure/current/attention evidence.
11. `structure_maps` vs `manifest` dispatch distribution: attractor/route cache semantics must not be conflated with runtime manifest wiring.
12. `entities` / `content_entity_drafts` vs physical table records: content payload/draft tables are not the physical table catalog and not Manifest Hub relation members.

## topologys.registrar_entries

status: ssot_gap

intent:
- Active physical table catalog and future `physical_table_id bigint` authority for physical tables registered into Manifest Hub.

backend_refs:
- No direct backend repository hits in the required audit paths.

frontend_refs:
- No frontend hits.

test_refs:
- No direct test hits found under backend/frontend test paths.

seed_refs:
- `db/schema.sql` defines `topologys.registrar_entries`; `db/README.md` describes it as a meta-registry.

ssot_refs:
- `docs/design/db-schema.yaml` and `docs/design/sql-attention-logs-ssot.yaml` mention the registrar surface.

risk:
- Current SQL authority is `registrar_entry_id uuid`; target `physical_table_id bigint` does not yet exist.

decision:
- redefine

todo:
- Add migration/design follow-up for `physical_table_id bigint` without changing SQL in this pass.

## hubs.hubs

status: connected_but_semantic_drift

intent:
- Active Manifest Hub parent authoring unit for one-screen physical table groups.
- Future `relation bigint[]` is an unordered `physical_table_id` set.

backend_refs:
- `NpgsqlContentBundleRepository` reads `hubs.hubs` and joins `relation_registry_id` for current content-bundle admin behavior.

frontend_refs:
- Indirect content bundle/admin UI surfaces display hub data through API contracts; no direct SQL reference.

test_refs:
- No direct backend/frontend test hit for literal `hubs.hubs`.

seed_refs:
- `db/topology_tables.sql` defines current `hubs.hubs`; `db/demo_seed.sql` inserts by `relation_registry_id`.

ssot_refs:
- `docs/design/db-schema.yaml` now fixes Manifest Hub meaning and prohibits UI order/relation registry/runtime-weight meanings.

risk:
- Current SQL and backend semantics still model `relation_registry_id`, not unordered `physical_table_id` membership.

decision:
- migrate

todo:
- Keep SQL migration follow-up for `status` enum/shape and `relation bigint[]` addition.

## hubs.hub_relations

status: connected_but_semantic_drift

intent:
- Legacy/transition relation-edge table; not Manifest Hub membership and not dynamic pressure.

backend_refs:
- `NpgsqlContentBundleRepository` lists, counts, and validates `hubs.hub_relations`.

frontend_refs:
- `ContentsAdmin` displays hub relation counts/details through admin API.

test_refs:
- No direct backend/frontend test hit for literal `hubs.hub_relations`.

seed_refs:
- `db/topology_tables.sql` defines the table and `weight` column.

ssot_refs:
- `docs/design/db-schema.yaml` classifies it as a legacy/transition candidate.

risk:
- `weight` and `relation_registry_id` can be confused with runtime traversal/current pressure or future `hubs.hubs.relation` membership.

decision:
- needs_question

todo:
- Decide delete vs redefine after migration design.

## topologys.entities

status: connected_but_semantic_drift

intent:
- Current converged/content payload table; must not be treated as physical table catalog or Manifest Hub membership authority.

backend_refs:
- `NpgsqlContentBundleRepository` and `NpgsqlTopologyRepository` read/write `topologys.entities`; `NpgsqlContextRouteRepository` includes it in table mapping.

frontend_refs:
- Indirect content/admin surfaces only.

test_refs:
- No direct literal in root `tests`; backend/frontend test coverage is indirect through runtime/admin tests.

seed_refs:
- `db/topology_tables.sql` defines it; `db/demo_seed.sql` inserts demo rows.

ssot_refs:
- `docs/design/db-schema.yaml` lists existing converged entity semantics and legacy/transition reclassification.

risk:
- Can be mistaken for actual physical table records or runtime-converged canonical data.

decision:
- needs_question

todo:
- Decide whether to keep as content payload cache or migrate/delete.

## topologys.content_entity_drafts

status: connected_but_semantic_drift

intent:
- Draft payload staging surface for content bundle admin flow.

backend_refs:
- `NpgsqlContentBundleRepository` inserts, updates, reads, and promotes drafts.

frontend_refs:
- Indirect content bundle admin API use.

test_refs:
- No direct literal test hit found.

seed_refs:
- `db/topology_tables.sql` defines it.

ssot_refs:
- No strong target SSOT beyond current db-schema legacy/transition classification.

risk:
- Draft payload responsibilities overlap with future physical table record authoring semantics.

decision:
- needs_question

todo:
- Redefine with `topologys.entities` decision.

## topologys.structure_maps

status: connected_but_semantic_drift

intent:
- Existing attractor/package/schema/component resolution surface; transition candidate relative to runtime manifest dispatch distribution.

backend_refs:
- `NpgsqlTopologyRepository` resolves active structure maps; runtime environment checks reference admin attractor keys.

frontend_refs:
- Admin index displays default structure map data from frontend defaults, not direct SQL.

test_refs:
- `.agent/tests/check-db-schema.sh` asserts required demo/admin `structure_maps` rows.

seed_refs:
- `db/topology_tables.sql` defines it; `db/demo_seed.sql` seeds demo/admin attractor rows.

ssot_refs:
- `docs/design/db-schema.yaml`, runtime orchestration SSOT, and pipeline continuity SSOT reference structure-map route continuity.

risk:
- Can duplicate or conflict with `manifest` dispatcher/runtime distribution.

decision:
- needs_question

todo:
- Decide whether this remains runtime resolution cache, is integrated into manifest, or is retired.

## manifest

status: connected

intent:
- Runtime dispatch distribution surface for dispatcher/runtime/ui_projection/projection constructor/SSE projection wiring.

backend_refs:
- `NpgsqlManifestRepository` lists, fetches, creates, updates, activates, deprecates, and resolves active manifest rows.
- `AdminRuntime` dispatches manifest-management operations.

frontend_refs:
- `frontend/api/adminApi.ts` and `ManifestsAdmin` author/list manifest mappings and projection constructor data.

test_refs:
- Backend and frontend tests reference manifest management, projection editor, SSE lanes, and projection runtime packageIds.

seed_refs:
- `db/manifest_tables.sql` defines `manifest`; seed files create demo manifest routing records.

ssot_refs:
- `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, and `docs/design/db-schema.yaml` define runtime manifest meaning.

risk:
- Current comments point `ui_projection.packageIds` to legacy `packages.package_id`; must not be conflated with Manifest Hub.

decision:
- keep

todo:
- Decide whether projection packageIds should reference `ui_component_package` or `ui_topology_tensor`.

## ui_component_registry

status: connected

intent:
- Active UI component DB authority.

backend_refs:
- `NpgsqlUiTopologyRepository` joins and inserts `ui_component_registry` during package generation.

frontend_refs:
- UI Builder interacts through admin runtime layers rather than direct SQL.

test_refs:
- `.agent/tests/check-db-schema.sh` checks table existence; bootstrap/structure checks include UI topology surfaces.

seed_refs:
- `db/ui_topology_tables.sql` defines it.

ssot_refs:
- `docs/design/db-schema.yaml`, component catalog classification SSOT, and SQL Attention SSOT reference it.

risk:
- Duplicate concept with legacy `components` table.

decision:
- keep

todo:
- Plan migration away from legacy `components`.

## ui_component_package

status: connected

intent:
- Active UI package DB authority.

backend_refs:
- `NpgsqlUiTopologyRepository` joins and inserts `ui_component_package` and maps components through `ui_package_component_map`.

frontend_refs:
- UI Builder package generation consumes admin runtime responses.

test_refs:
- `.agent/tests/check-db-schema.sh` checks table existence via UI topology checks.

seed_refs:
- `db/ui_topology_tables.sql` defines it.

ssot_refs:
- `docs/design/db-schema.yaml` and SQL Attention SSOT reference it.

risk:
- Duplicate concept with legacy `packages` and `manifest.ui_projection.packageIds` comments.

decision:
- keep

todo:
- Decide packageId convergence for manifest projections.

## ui_package_component_map

status: connected

intent:
- Active normalized component membership for UI packages.

backend_refs:
- `NpgsqlUiTopologyRepository` joins and inserts map rows during package generation.

frontend_refs:
- Indirect UI Builder runtime use.

test_refs:
- DB schema and bootstrap checks cover UI topology tables.

seed_refs:
- `db/ui_topology_tables.sql` defines it.

ssot_refs:
- `docs/design/db-schema.yaml` lists it under active UI topology authority.

risk:
- Conflicts with legacy `packages.layout` JSON binding.

decision:
- keep

todo:
- Migrate legacy `packages.layout` responsibilities.

## ui_layout_registry

status: connected

intent:
- Active UI layout and display-order authority paired with wiring/tensor rows.

backend_refs:
- `NpgsqlUiTopologyRepository` inserts and updates layout registry data and layout patches.

frontend_refs:
- UI Builder edits layout patch/class refs through admin runtime.

test_refs:
- DB schema checks cover existence.

seed_refs:
- `db/ui_topology_tables.sql` defines it.

ssot_refs:
- `docs/design/db-schema.yaml`, CSS dictionary SSOT, and topology layout class SSOT align display/layout authority here.

risk:
- Must remain separate from `hubs.hubs.relation` membership.

decision:
- keep

todo:
- Keep display order on UI topology surfaces.

## ui_wiring_registry

status: connected

intent:
- Active UI wiring authority paired with layout/tensor rows.

backend_refs:
- `NpgsqlUiTopologyRepository` inserts wiring registry rows during package generation.

frontend_refs:
- Indirect UI Builder runtime use.

test_refs:
- DB schema checks cover existence.

seed_refs:
- `db/ui_topology_tables.sql` defines it.

ssot_refs:
- `docs/design/db-schema.yaml` lists it under UI topology authority.

risk:
- Wiring semantics must not be stored in Manifest Hub relation arrays.

decision:
- keep

todo:
- None in this pass beyond package migration decisions.

## ui_topology_tensor

status: connected

intent:
- Active projection composition/tensor route authority.

backend_refs:
- `NpgsqlUiTopologyRepository` reads tensors by route and inserts promoted tensors.

frontend_refs:
- UI Builder displays promoted tensor identifiers and updates layout patches.

test_refs:
- `.agent/tests/check-db-schema.sh`, `.agent/tests/check-bootstrap-validation.sh`, and structure checks reference it.

seed_refs:
- `db/ui_topology_tables.sql` defines it.

ssot_refs:
- `docs/design/db-schema.yaml`, runtime orchestration SSOT, and SQL Attention SSOT reference it.

risk:
- Needs explicit decision whether manifest projection packageIds converge on package or tensor identifiers.

decision:
- keep

todo:
- Resolve manifest projection reference target.

## logs.current

status: connected

intent:
- Physical table pressure/current projection.

backend_refs:
- SQL Attention log repository/runtime surfaces read current/hub_current/attention evidence.

frontend_refs:
- No direct SQL reference found.

test_refs:
- `.agent/tests` DB/runtime checks reference logs current surfaces.

seed_refs:
- `db/sql_attention_logs_tables.sql` defines it and related indexes/triggers.

ssot_refs:
- `docs/design/sql-attention-logs-ssot.md` and `.yaml` define current semantics.

risk:
- Must not be confused with `hubs.hubs.relation` or static `hub_relations.weight`.

decision:
- keep

todo:
- Decide bigint/text consistency for `physical_table_id` across logs.

## logs.hub_current

status: connected

intent:
- Hub/current pressure projection for SQL Attention exploration.

backend_refs:
- SQL Attention repository comments and runtime paths reference `logs.hub_current`.

frontend_refs:
- No direct SQL reference found.

test_refs:
- `.agent/tests` and SQL Attention design checks reference it.

seed_refs:
- `db/sql_attention_logs_tables.sql` defines it.

ssot_refs:
- SQL Attention SSOT defines hub_current separately from Manifest Hub membership.

risk:
- Naming can be confused with `hubs.hubs` or `hubs.hub_relations.weight`.

decision:
- keep

todo:
- Keep semantic separation in migration notes.

## logs.attention

status: connected

intent:
- Append-only attention evidence.

backend_refs:
- SQL Attention repository appends/loads attention evidence.

frontend_refs:
- No direct SQL reference found.

test_refs:
- `.agent/tests` and SQL Attention checks reference it.

seed_refs:
- `db/sql_attention_logs_tables.sql` defines it.

ssot_refs:
- SQL Attention SSOT defines append-only evidence semantics.

risk:
- Evidence must not be treated as Manifest Hub relation ordering or weight.

decision:
- keep

todo:
- Include in physical_table_id type-alignment follow-up.

## logs.usage_metrics

status: obsolete_candidate

intent:
- Legacy promotion policy usage metric table.

backend_refs:
- No backend repository hits in required audit.

frontend_refs:
- No direct frontend SQL use; text references exist in policy/docs.

test_refs:
- No direct runtime test refs found.

seed_refs:
- `db/promotion_tables.sql` defines it.

ssot_refs:
- `docs/design/db-schema.yaml` and SQL Attention SSOT mention legacy/initial alignment.

risk:
- Overlaps with SQL Attention pressure/current surfaces.

decision:
- delete_candidate

todo:
- Decide whether any usage metric semantics survive in logs.current/logs.attention.

## logs.promotion_candidates

status: obsolete_candidate

intent:
- Legacy promotion-policy candidate table.

backend_refs:
- No backend repository hits in required audit.

frontend_refs:
- No direct SQL use; wording appears in frontend guide text.

test_refs:
- No direct runtime test refs found.

seed_refs:
- `db/promotion_tables.sql` defines it.

ssot_refs:
- `docs/design/db-schema.yaml` and SQL Attention SSOT mention it.

risk:
- Overlaps with newer recommendation/SQL Attention promotion evidence.

decision:
- delete_candidate

todo:
- Confirm whether promotion candidates become recommendation outputs or are removed.

## components

status: duplicate_candidate

intent:
- Legacy UI builder component catalog.

backend_refs:
- Generic `components` term is noisy; no active repository table SQL was found for `FROM components` / `INSERT INTO components` in required paths.

frontend_refs:
- Many frontend component-directory references are unrelated to the DB table; UI Builder active path uses `ui_component_bucket` and generated `ui_component_registry`.

test_refs:
- Structure checks mention frontend components and docs vocabulary, not necessarily this DB table.

seed_refs:
- `db/ui_topology_tables.sql` defines legacy `components`.

ssot_refs:
- `docs/design/db-schema.yaml` classifies as legacy/transition.

risk:
- Duplicate with `ui_component_registry`.

decision:
- migrate

todo:
- Decide compatibility period and migrate/delete legacy UI builder component table.

## design

status: duplicate_candidate

intent:
- Legacy UI builder design/style binding.

backend_refs:
- Generic `design` term is noisy; no active repository table SQL was found for `FROM design` / `INSERT INTO design` in required paths.

frontend_refs:
- Frontend design wording is mostly docs/UI text; UI Builder now exposes CSS dictionary and topology layout class candidates.

test_refs:
- Structure/vocabulary checks cover design docs broadly.

seed_refs:
- `db/ui_topology_tables.sql` defines legacy `design`.

ssot_refs:
- CSS dictionary and topology layout class SSOTs are vocabulary authority for style/layout class refs.

risk:
- Free-form classname/tailwind can bypass CSS/layout vocabularies.

decision:
- migrate

todo:
- Decide legacy style compatibility and migration into layout/tensor CSS token refs.

## packages

status: duplicate_candidate

intent:
- Legacy UI builder package/layout JSON table.

backend_refs:
- Generic `packages` term is noisy; no active repository table SQL was found for `FROM packages` / `INSERT INTO packages` in required paths.

frontend_refs:
- Manifest projection editor and tests still use `packageIds` language; this may refer to runtime projection IDs rather than direct SQL.

test_refs:
- Frontend projection/SSE tests use `packageIds` extensively.

seed_refs:
- `db/ui_topology_tables.sql` defines legacy `packages`; manifest SQL comments still point `packageIds` to `packages.package_id`.

ssot_refs:
- `docs/design/db-schema.yaml` classifies legacy/transition and notes manifest reference collision.

risk:
- Duplicate with `ui_component_package`; `packages.layout` duplicates `ui_package_component_map` plus layout/tensor authority.

decision:
- migrate

todo:
- Decide whether manifest `packageIds` target `ui_component_package` or `ui_topology_tensor`.

## context_token_registry

status: connected

intent:
- Context/recommendation token dictionary used by admin token management and vectorization.

backend_refs:
- `NpgsqlContextRouteRepository` lists/creates/deprecates tokens and loads token records.
- `AdminRuntime` dispatches `context_token_registry` operations.

frontend_refs:
- `frontend/api/adminApi.ts` and context token registry admin page call context token actions.

test_refs:
- `.agent/tests/check-db-schema.sh` and runtime environment checks reference this table/layer.

seed_refs:
- `db/context_route_tables.sql` defines it; `db/demo_seed.sql` inserts demo tokens.

ssot_refs:
- `docs/design/db-schema.yaml` and context route recommendation SSOT reference it.

risk:
- Low; keep separate from SQL Attention current pressure.

decision:
- keep

todo:
- None in this pass.

## context_record_snapshot_cache

status: ssot_gap

intent:
- Rebuildable context/recommendation cache for record-token snapshots.

backend_refs:
- No backend repository hits in required audit.

frontend_refs:
- No frontend hits.

test_refs:
- No direct test hits found.

seed_refs:
- `db/context_route_tables.sql` defines it.

ssot_refs:
- Context route recommendation SSOT describes update/read behavior.

risk:
- Defined and documented but apparently not connected to repository code.

decision:
- needs_question

todo:
- Decide whether to implement, remove, or keep as future cache.

## context_event_vector_cache

status: connected

intent:
- Rebuildable vector cache for context events.

backend_refs:
- `NpgsqlContextRouteRepository` reads/upserts event vector cache entries.

frontend_refs:
- No direct frontend hits.

test_refs:
- Indirect context route tests/checks; no root `tests` path.

seed_refs:
- `db/context_route_tables.sql` defines it; `db/demo_seed.sql` seeds demo vectors.

ssot_refs:
- `docs/design/db-schema.yaml` and context route recommendation SSOT reference it.

risk:
- Cache must remain rebuildable projection, not SSOT.

decision:
- keep

todo:
- None in this pass.

## context_cluster

status: ssot_gap

intent:
- Optional/rebuildable context clustering output.

backend_refs:
- No backend repository hits in required audit.

frontend_refs:
- No frontend hits.

test_refs:
- No direct test hits found.

seed_refs:
- `db/context_route_tables.sql` defines it.

ssot_refs:
- Context route recommendation SSOT references clustering.

risk:
- Optional table without connected runtime can look canonical.

decision:
- needs_question

todo:
- Decide keep/delete/redefine once recommendation clustering scope is implemented.

## context_cluster_label

status: ssot_gap

intent:
- Optional labels for context clusters.

backend_refs:
- No backend repository hits in required audit.

frontend_refs:
- No frontend hits.

test_refs:
- No direct test hits found.

seed_refs:
- `db/context_route_tables.sql` defines it.

ssot_refs:
- Context route recommendation SSOT references cluster labels.

risk:
- Depends on unresolved `context_cluster` lifecycle.

decision:
- needs_question

todo:
- Decide with `context_cluster`.

## context_drift_signal

status: ssot_gap

intent:
- Optional drift/spike detection output for context recommendation.

backend_refs:
- No backend repository hits in required audit.

frontend_refs:
- No frontend hits.

test_refs:
- No direct test hits found.

seed_refs:
- `db/context_route_tables.sql` defines it.

ssot_refs:
- Context route recommendation SSOT references drift signal.

risk:
- Optional analytics output may be mistaken for active recommendation authority.

decision:
- needs_question

todo:
- Decide keep/redefine/delete after recommendation analytics scope is set.

## topologys.demo_state_transitions

status: connected_but_semantic_drift

intent:
- Demo-only state transition support table.

backend_refs:
- `NpgsqlTopologyRepository` references state transition behavior in demo/runtime topology flows.

frontend_refs:
- Admin/runtime demo flows are indirect.

test_refs:
- No direct literal hit in required grep set captured for this table in the mandatory list.

seed_refs:
- `db/topology_tables.sql` defines/seeds demo transition support.

ssot_refs:
- `docs/design/db-schema.yaml` marks it legacy/transition.

risk:
- Demo runtime state can be mistaken for canonical Manifest Hub/runtime manifest design.

decision:
- delete_candidate

todo:
- Confirm demo compatibility before removal.

## topologys.topology_edit_log

status: connected_but_semantic_drift

intent:
- Legacy topology edit logging.

backend_refs:
- Content/admin repository flows may append/read edit logs depending on route; not part of active path.

frontend_refs:
- Indirect admin content flows.

test_refs:
- No direct mandatory hit captured.

seed_refs:
- `db/topology_tables.sql` defines it.

ssot_refs:
- `docs/design/db-schema.yaml` marks it legacy/transition.

risk:
- Log semantics overlap with context operation logs and SQL Attention evidence.

decision:
- redefine

todo:
- Decide whether to migrate to context/log evidence or remove.

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
- Should `hubs.hub_relations` be deleted or redefined as a separate non-membership concept?
- Should `topologys.entities` remain as content payload cache or be deleted/migrated?
- Should `topologys.structure_maps` remain as runtime resolution cache or be integrated into manifest dispatch distribution?
- Should `components` / `design` / `packages` be migrated immediately or kept through a compatibility window?
- Should manifest `ui_projection.packageIds` converge on `ui_component_package` or `ui_topology_tensor`?
- Should `logs.*.physical_table_id` converge on bigint or retain text compatibility?
