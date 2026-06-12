-- Docker compose initializer SSOT.
-- This file expects container paths (/db/...) and is not intended for host-side `psql -f db/init.sql`.
-- Fresh compose volumes apply migrations/ and patches/ here; do not run those files separately in compose.

\set ON_ERROR_STOP on

\echo '[init.sql] applying schema.sql'
\i /db/schema.sql

\echo '[init.sql] applying topology_tables.sql'
\i /db/topology_tables.sql

\echo '[init.sql] applying hub_relations_legacy_to_manifest_scoped.sql'
\i /db/migrations/hub_relations_legacy_to_manifest_scoped.sql

\echo '[init.sql] applying promotion_tables.sql'
\i /db/promotion_tables.sql

\echo '[init.sql] applying sql_attention_logs_tables.sql'
\i /db/sql_attention_logs_tables.sql

\echo '[init.sql] applying sql_attention_phase_generation_line.sql'
\i /db/migrations/sql_attention_phase_generation_line.sql

\echo '[init.sql] applying ci_attention_guidance_tables.sql'
\i /db/ci_attention_guidance_tables.sql

\echo '[init.sql] applying context_route_tables.sql'
\i /db/context_route_tables.sql

\echo '[init.sql] applying ui_topology_tables.sql'
\i /db/ui_topology_tables.sql

\echo '[init.sql] applying ui_topology_to_canonical_schema.sql'
\i /db/migrations/ui_topology_to_canonical_schema.sql

\echo '[init.sql] applying structure_map_add_layout_id.sql'
\i /db/migrations/structure_map_add_layout_id.sql

\echo '[init.sql] applying ui_topology_tensor_layout_draft_tmp.sql'
\i /db/migrations/ui_topology_tensor_layout_draft_tmp.sql

\echo '[init.sql] applying components_style_design_draft_tmp.sql'
\i /db/migrations/components_style_design_draft_tmp.sql

\echo '[init.sql] applying ui_package_component_map_nulls_not_distinct.sql'
\i /db/migrations/ui_package_component_map_nulls_not_distinct.sql

\echo '[init.sql] applying ui_topology_tensor_nulls_not_distinct.sql'
\i /db/migrations/ui_topology_tensor_nulls_not_distinct.sql

\echo '[init.sql] applying mock_preset_registry_tables.sql'
\i /db/migrations/mock_preset_registry_tables.sql

\echo '[init.sql] applying team_markdown_registry_tables.sql'
\i /db/migrations/team_markdown_registry_tables.sql

\echo '[init.sql] applying hub_search_preset_seed.sql'
\i /db/migrations/hub_search_preset_seed.sql

\echo '[init.sql] applying physical_search_crud_aggregate_preset_seed.sql'
\i /db/migrations/physical_search_crud_aggregate_preset_seed.sql

\echo '[init.sql] applying physical_details_inline_editor_md_generator_preset_seed.sql'
\i /db/migrations/physical_details_inline_editor_md_generator_preset_seed.sql

\echo '[init.sql] applying aggregate_dashboard_preset_seed.sql'
\i /db/migrations/aggregate_dashboard_preset_seed.sql

\echo '[init.sql] applying manifest_tables.sql'
\i /db/manifest_tables.sql

\echo '[init.sql] applying admin_import_topology_manifest_migration.sql'
\i /db/migrations/admin_import_topology_manifest_migration.sql

\echo '[init.sql] applying enum_tables.sql'
\i /db/enum_tables.sql

\echo '[init.sql] applying enum_seed.sql'
\i /db/enum_seed.sql

\echo '[init.sql] applying auth_tables.sql'
\i /db/auth_tables.sql

\echo '[init.sql] applying auth_users_master_roster_state.sql'
\i /db/migrations/auth_users_master_roster_state.sql

\echo '[init.sql] applying auth_seed.sql'
\i /db/auth_seed.sql

\echo '[init.sql] applying legacy_mirror_tables.sql'
\i /db/legacy_mirror_tables.sql

\echo '[init.sql] applying seed_empty.sql'
\i /db/seed_empty.sql

\echo '[init.sql] applying patch add_enum_dictionary_dispatch_manifests.sql'
\i /db/patches/add_enum_dictionary_dispatch_manifests.sql

\echo '[init.sql] applying patch add_layout_patch_save_tmp_manifests.sql'
\i /db/patches/add_layout_patch_save_tmp_manifests.sql

\echo '[init.sql] applying patch add_manifest_list_relationship_remote_targets.sql'
\i /db/patches/add_manifest_list_relationship_remote_targets.sql

\echo '[init.sql] applying patch add_auth_relationship_remote_boundary_manifest.sql'
\i /db/patches/add_auth_relationship_remote_boundary_manifest.sql

\echo '[init.sql] applying patch add_package_generator_detach_manifest.sql'
\i /db/patches/add_package_generator_detach_manifest.sql

\echo '[init.sql] applying patch add_package_generator_promote_package_manifest.sql'
\i /db/patches/add_package_generator_promote_package_manifest.sql

\echo '[init.sql] applying demo_seed.sql'
\i /db/demo_seed.sql
