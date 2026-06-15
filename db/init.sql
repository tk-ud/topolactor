-- Docker compose initializer SSOT.
-- This file expects container paths (/db/...) and is not intended for host-side `psql -f db/init.sql`.
--
-- BOOTSTRAP POLICY:
--   Standard DB initialization: docker compose -v removes volumes, then
--   `docker compose up` executes this file via docker-entrypoint-initdb.d/00-init.sql
--   on the fresh container. All canonical DDL and seed data is in db/*.sql files.
--   db/migrations/ and db/patches/ are retired; do not add files there.

\set ON_ERROR_STOP on

\echo '[init.sql] applying schema.sql'
\i /db/schema.sql

\echo '[init.sql] applying topology_tables.sql'
\i /db/topology_tables.sql

\echo '[init.sql] applying promotion_tables.sql'
\i /db/promotion_tables.sql

\echo '[init.sql] applying sql_attention_logs_tables.sql'
\i /db/sql_attention_logs_tables.sql

\echo '[init.sql] applying ci_attention_guidance_tables.sql'
\i /db/ci_attention_guidance_tables.sql

\echo '[init.sql] applying context_route_tables.sql'
\i /db/context_route_tables.sql

\echo '[init.sql] applying ui_topology_tables.sql'
\i /db/ui_topology_tables.sql

\echo '[init.sql] applying topology_cross_table_wiring.sql'
\i /db/topology_cross_table_wiring.sql

\echo '[init.sql] applying mock_preset_tables.sql'
\i /db/mock_preset_tables.sql

\echo '[init.sql] applying team_markdown_tables.sql'
\i /db/team_markdown_tables.sql

\echo '[init.sql] applying credential_reference_tables.sql'
\i /db/credential_reference_tables.sql

\echo '[init.sql] applying manifest_tables.sql'
\i /db/manifest_tables.sql

\echo '[init.sql] applying enum_tables.sql'
\i /db/enum_tables.sql

\echo '[init.sql] applying enum_seed.sql'
\i /db/enum_seed.sql

\echo '[init.sql] applying auth_tables.sql'
\i /db/auth_tables.sql

\echo '[init.sql] applying auth_seed.sql'
\i /db/auth_seed.sql

\echo '[init.sql] applying legacy_mirror_tables.sql'
\i /db/legacy_mirror_tables.sql

\echo '[init.sql] applying seed_empty.sql'
\i /db/seed_empty.sql

\echo '[init.sql] applying hub_search_preset_seed.sql'
\i /db/hub_search_preset_seed.sql

\echo '[init.sql] applying physical_search_crud_aggregate_preset_seed.sql'
\i /db/physical_search_crud_aggregate_preset_seed.sql

\echo '[init.sql] applying physical_details_inline_editor_md_generator_preset_seed.sql'
\i /db/physical_details_inline_editor_md_generator_preset_seed.sql

\echo '[init.sql] applying aggregate_dashboard_preset_seed.sql'
\i /db/aggregate_dashboard_preset_seed.sql

\echo '[init.sql] applying ui_component_registry_preset_catalog_bootstrap.sql'
\i /db/ui_component_registry_preset_catalog_bootstrap.sql
