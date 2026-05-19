-- Docker compose initializer SSOT.
-- This file expects container paths (/db/...) and is not intended for host-side `psql -f db/init.sql`.

\set ON_ERROR_STOP on

\echo '[init.sql] applying schema.sql'
\i /db/schema.sql

\echo '[init.sql] applying topology_tables.sql'
\i /db/topology_tables.sql

\echo '[init.sql] applying promotion_tables.sql'
\i /db/promotion_tables.sql

\echo '[init.sql] applying context_route_tables.sql'
\i /db/context_route_tables.sql

\echo '[init.sql] applying ui_topology_tables.sql'
\i /db/ui_topology_tables.sql

\echo '[init.sql] applying seed_empty.sql'
\i /db/seed_empty.sql

\echo '[init.sql] applying demo_seed.sql'
\i /db/demo_seed.sql
