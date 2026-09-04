# credential-management projection design (temporary)

## Status

Temporary projection design surface. Not top SSOT. Must not contradict:

- `docs/framework-core.yaml`, `docs/framework-policy.yaml` (top SSOT)
- `docs/design/auth-db-session-credential-ssot.yaml`
- `docs/design/instance-port-substrate-ssot.yaml` `existing_credential_management_projection_extension`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml` `lane_storage_boundary`
- `docs/design/react-schema-topology-seed-translator-ssot.yaml` `storage_adoption_contract`
- `docs/design/db-schema.yaml` (DB table authority / role / `manifest_reference` design of
  record — see `package_authority_boundary` below, corrected after PR review)

If this document and any of the above disagree, the SSOT above wins and this document is
wrong and must be fixed in the same PR that discovers the disagreement.

**PR review correction**: the first version of this bundle referenced
`topology.ui_component_package` from `manifest.topology[ui_projection].packageIds`. A PR
review verified against `docs/design/db-schema.yaml` that this is the wrong table —
`topology.ui_component_package` (role `component_group_bundle`) exists only to satisfy
`topology.ui_topology_tensor.package_id`'s FK constraint; the manifest-facing package
authority (`manifest_reference: manifest.topology[ui_projection].packageIds`) is
`topology.components_package_design`. See `package_authority_boundary` in the SSOT sections
below and the corrected seed/translator/test content in this same PR.

## Purpose

Bundle `ui-seed-translator-uibuilder-top-ssot-alignment` moved the credential-management
admin screen's UI-entity payload (previously 37 `topology_ui_seed_record` array elements
embedded directly in `manifest.topology`) into `topology.ui_component_package` /
`topology.components_layout_design` / `topology.ui_wiring_registry` /
`topology.ui_topology_tensor`, per `docs/framework-policy.yaml`
`ui_topology_tensor_persistence` and `db/manifest_tables.sql`'s documented
`manifest.topology` refs-only shape. This document records the resulting design so a
future bundle building the actual admin authoring frontend/backend
(`instance_settings_admin_authoring_ui_pending`) has one place to read the current shape
from, instead of re-deriving it from seed SQL diffs.

## Tab / category structure

One screen (`auth.external.credential_management.projection`, manifest
`00000000-0000-0000-0000-000000000092`), three modes switched by select/mode/category —
**not** three separate routes:

| Category key | Role | Backing |
|---|---|---|
| `user_auth` | Existing user/auth credential boundary (readonly join) | `auth.user.boundary` (manifest `091`) |
| `external` | External port credential context | `topology.external_access_ports` / `external_response_ports` / `external_hook_ports` |
| `instance_settings` | DB/runtime instance address + operation binding drafts | `topology.db_instance_port` / `runtime_instance_port` / `instance_connection_policy` / `instance_operation_authority_binding` |

`user_auth` is **not** the same thing as the standalone `/admin/users` route
(`admin-master-roster-management-ssot.yaml`, `frontend/routes/admin/users.tsx`,
`AdminUsersRoster.tsx`). `/admin/users` is master-roster CRUD over `auth.users`
(approve/status/suspension). The credential-management `user_auth` tab is a **readonly**
relation-boundary join through `auth.user.boundary` (manifest `091`) — it must not become a
second CRUD surface over `auth.users`, and `/admin/users` must not be collapsed into or
replaced by this screen. This bundle does not touch `/admin/users`; route presence/absence
of `/admin/users` is not proof of anything in this bundle.

## manifest 091 / manifest 092 / structure_maps 091 / structure_maps 092 authority boundary

Two **unrelated** tables both happen to use the trailing hex `091`/`092` in different
UUIDs — do not conflate them:

- **`manifest` table, `manifest_id = 00000000-…-091`**: `auth.user.boundary`. Relation-relay
  manifest only — exposes the logical `auth.user` table (`id`, `username`) for Step 2.5
  remote joins. No credential-management screen content lives here.
- **`manifest` table, `manifest_id = 00000000-…-092`**: `auth.external.credential_management.projection`.
  Fixed-form admin projection (`ui_builder_authority: false`) over existing
  manifest/screen_data_shape/relation-boundary content. After this bundle's migration, its
  `topology` array holds: manifest-native config/policy/schema descriptors (`hub_grouping`,
  `runtime_mapping`, `fixed_form_projection`, `screen_data_shape`,
  `credential_management_logical_table_shape` ×7, relation intents, operation/action
  wiring refs, JSON template shape/section rows, initial category/port data rows — all of
  these are manifest-appropriate per `docs/framework-policy.yaml` `manifest:`
  responsibilities: `form_schema`/`jsonb_shape`/`relation_link_candidates`), **plus one**
  refs-only `ui_projection` entry (`packageIds`/`layoutId`/`wiringId`/`tensorId`) pointing
  at the `instance_settings` category's package/layout/wiring/tensor rows. It no longer
  embeds a `topology_ui_seed_record` tree directly.
- **`topology.structure_maps` table, `structure_map_id = 00000000-…-091`**
  (`admin_ui_topology_promoted_palette`) and **`structure_map_id = 00000000-…-092`**
  (`admin_ui_component_bucket_create`): admin UI-topology attractor-key rows, **unrelated**
  to credential management. Same trailing hex, different table, different UUID, different
  meaning. A future edit must never read "092" alone and assume it means the
  credential-management manifest.

## auth.users / auth.credentials authority

- `auth.users`: approval-state authority (`active`, `approve`, `status`, `suspended_from`,
  `suspended_until`, `state_note`). Owned by `/admin/users` master-roster CRUD, not by this
  screen.
- `auth.credentials`: `password_hash` boundary authority. Never read, projected, or
  seeded from this screen or its translator/seed surfaces.
- Neither table's secret-bearing columns (`password_hash`) may appear in
  `manifest.topology`, `topology.ui_*` package/layout/design/wiring/tensor rows, or
  translator output. `docs/design/react-schema-topology-seed-translator-ssot.yaml`
  `translator_input_authority.protected_boundary_vocabulary` and the new
  `credential_secret_projection_detected` validation rule are the enforcement points.

## package / layout / design / wiring / tensor adoption (instance_settings category)

Built by `.agent/tools/react-schema-topology-seed-translator generate-topology-seed` from
`.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.topology-seed.input.json`,
per `docs/design/react-schema-topology-seed-translator-ssot.yaml`
`storage_adoption_contract.adoption_candidate_separation_contract`:

- **package authority** (`topology.components_package_design`): one row (`layout: []`,
  no component+design pairs authored — this is a `fixed_form_projection`, not a UI
  Component Builder screen). This is the row `manifest.topology[ui_projection].packageIds`
  references, per `docs/design/db-schema.yaml` `packages`/`components_package_design`
  `manifest_reference: manifest.topology[ui_projection].packageIds`. **Not**
  `topology.ui_component_package` — see the distinct identity below. (Corrected after a PR
  review finding: the first version of this bundle used `topology.ui_component_package` for
  this reference, which is the wrong table per DB design authority.)
- **component group bundle** (`topology.ui_component_package`): a second, distinct row,
  required only because `topology.ui_topology_tensor.package_id` has a physical FK
  constraint against this table (`db/ui_topology_tables.sql`). It has its own key
  (`...component_group_bundle`, deliberately different from the package authority row's
  key) and is never referenced from `manifest.topology[ui_projection].packageIds`.
- **layout** (`topology.components_layout_design`): one row holding the flattened
  category → section → form → field/action/validation tree (the same
  `topology_ui_seed_record[]` shape previously embedded in `manifest.topology`, now stored
  in a plain `jsonb` column with no GIN per-element budget).
- **design**: not applicable for this surface (no `style_ref` records) — an empty
  `designAdoptionCandidates` bucket is correct, not a gap.
- **wiring** (`topology.ui_wiring_registry`): one aggregate row per Projection —
  `wiring_schema_json.actions[]` carries each Action/Step record's `eventBinding`
  (`json_template_download`, `json_import`, `validate`, `preview`, `apply`, `approve`) as a
  separate array entry. **Not** a separate row for each Action record — `manifest.topology
  [ui_projection].wiringId` and `topology.ui_topology_tensor.wiring_id` are both singular refs
  (`db/manifest_tables.sql`, `db/ui_topology_tables.sql`), so a Projection's N actions must
  resolve to exactly one wiring row, never N independent rows. (Corrected after a PR review
  finding: an earlier translator version emitted one `wiringAdoptionCandidates` entry per
  Action, whose keys the single `wiringId` ref could never resolve to — see
  `docs/design/react-schema-topology-seed-translator-ssot.yaml`
  `adoption_candidate_separation_contract.candidate_buckets.wiringAdoptionCandidates
  .cardinality_note` and `manifest_refs_candidate_reference_resolution`.)
- **tensor** (`topology.ui_topology_tensor`): one `layout_patch_json` row whose
  `nodes[].runtimeInteractions[]` carries the same six actions as dispatch candidates
  (`localStateMutation` for the two `internal_instance_wiring` actions,
  `dispatchInstanceOperation` for the four `external_instance_wiring` actions), **without**
  `runtimeInteractionId` — backend assignment authority
  (`NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds`, invoked from
  `ApplyConfirmedLayoutPatchAsync`) is unchanged by this bundle and remains the only place
  `runtimeInteractionId` is minted, the first time this tensor row is persisted through that
  path.

## idempotency carrier

Seed-authored `dispatchInstanceOperation` runtimeInteractions candidates in the tensor row
above carry `trigger` / `actionType` / `instanceTargetRef` / `payloadFrom` — the fields
`docs/design/react-schema-topology-seed-translator-ssot.yaml`'s
`idempotency_carrier_missing_for_runtime_dispatch` rule requires. They do **not** carry
`runtimeInteractionId` or a literal `idempotency_key` — both remain backend/frontend
runtime-assigned:

- `runtimeInteractionId`: assigned by `AssignRuntimeInteractionIds` the first time this
  tensor row is persisted via `ApplyConfirmedLayoutPatchAsync` (unchanged authority).
- `idempotency_key`: computed at dispatch time by
  `frontend/lib/uiBuilderWiringProjection.ts computeDispatchIdempotencyKey` /
  `appendResolvedPayloadToIdempotencyKey`, forwarded through
  `frontend/runtime/renderEmission.ts` / `frontend/runtime/uiEventEffectRunner.ts` /
  `frontend/runtime/frontendScheduler.ts` to the dispatch payload's `idempotency_key` field
  (existing implementation; not modified by this bundle — verified unchanged by this
  bundle's audit, not re-implemented).

## Seed migration summary

`db/seed_empty.sql` manifest `00000000-…-092`:

- **Removed**: 37 `topology_ui_seed_record` array elements (previous
  `instance_settings_projection_category_not_yet_represented` gap-closure shape).
- **Added elsewhere in the same file**: `topology.ui_component_package` (component group
  bundle, tensor-FK-only) / `topology.components_package_design` (manifest-facing package
  authority) / `topology.components_layout_design` / `topology.ui_wiring_registry` /
  `topology.ui_topology_tensor` rows carrying the same tree/action content, under a
  dedicated deterministic UUID block reserved for this bundle (`…-0000000cd0xx`) — chosen to
  avoid any collision with the existing `structure_maps` `091`/`092` rows or any other
  seeded id.
- **Added to manifest 092's array**: one refs-only `ui_projection` entry pointing at the
  five new rows, with `packageIds` referencing the `components_package_design` row (not the
  `ui_component_package` row).
- **Not changed**: manifest `091`, `auth.users`, `auth.credentials`, `/admin/users`, and
  every non-`topology_ui_seed_record` element already in manifest `092` (these were already
  manifest-appropriate refs/policy/schema content, not UI-entity payload — see the manifest
  091/092 boundary section above).

## Known gaps (explicit, not silently closed)

- `instance_settings_admin_authoring_ui_pending` (`docs/system-roadmap.yaml`): the actual
  admin authoring frontend route/island and backend action for JSON template
  download/import/validate/preview/apply/approve remain unimplemented. This bundle only
  relocates the seed-representation; it does not build that UI.
- `package_internal_api_wiring_lane` idempotency applicability
  (`docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml` `lane_storage_boundary`):
  left as `known_gap`, not asserted resolved by this bundle.
- The remaining select-controlled Fields sharing the users/external_api_credential/
  external_instance_credential CRUD forms (`record_kind`/`active`/`approve`/`role_name`) render
  a real `<select>` (control=form_input/select, never the form_input/form_field bug the
  credential-management-ux-gaps round fixed for their sibling text Fields) but with only the
  placeholder `""` option — `data.options` is empty in the real seed. A user cannot actually
  choose a business value from these controls today; this is a separate, still-open seed/
  options-wiring gap, not resolved by this document's revision or by
  credential-management-ux-gaps.

## credential-management-ux-gaps round (production DOM UX remediation)

Two follow-up rounds after the migration above, a captured production DOM revealed and this
round fixed:

- 37 Fields across all three categories whose value a create/update/search Action's own
  `payloadFrom` reads (`node:<id>.value`) were authored `control=form_input/form_field`
  (`formFieldFactory` → a label plus a hardcoded empty `<span>`, never a real `<input>`) — not
  actually typeable in production. Re-authored to `control=form_input/input` in the canonical
  markup source and regenerated through the translator (never hand-edited into
  `db/seed_empty.sql`). Fields with no `payloadFrom` reference were left read-only.
- `credential_search_section` (the shared cross-category search/filter bar hosting
  `credential_category_filter`, the tabs.template category selector) rendered AFTER all three
  gated category subtrees' own content, because `topology_ui_projection`'s
  `SEED_RECORD_NESTED_LIST_KEYS` entry always flattens every Category child before every Section
  child, independent of authored source order — confirmed to have NO existing override authority
  (neither DSL source-text position nor a `ui_topology_tensor.layout_patch_json` override, since a
  schema-composed layout's override delta never carries `ParentNodeId`/`OrderIndex`/`SlotKey`).
  Resolved by adding a new, generic, opt-in `mixed_sibling_ordering_contract` capability to
  `docs/design/react-schema-topology-seed-translator-ssot.yaml` / the translator (an authored
  `siblingOrder` integer on a Category or Section, default-preserving for every surface that does
  not author it) and authoring `credential_search_section siblingOrder="-1"`, so the category
  selector itself is reachable before any category's own subtree content. See that SSOT's
  `mixed_sibling_ordering_contract` for the full contract and the alternatives it rejected.
- The credential-management hub-navigation link showed the raw hub UUID as its visible label
  (`hubs.hub.relation_registry_id` was never seeded). Fixed with a seed-data-only
  `topology.relation_registry` row; no runtime/backend code changed.
