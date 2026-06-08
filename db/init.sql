-- Docker compose initializer SSOT.
-- This file expects container paths (/db/...) and is not intended for host-side `psql -f db/init.sql`.

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

\echo '[init.sql] applying mock_preset_registry_tables.sql'
\i /db/migrations/mock_preset_registry_tables.sql

\echo '[init.sql] applying team_markdown_registry_tables.sql'
\i /db/migrations/team_markdown_registry_tables.sql

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

\echo '[init.sql] applying demo_seed.sql'
\i /db/demo_seed.sql
