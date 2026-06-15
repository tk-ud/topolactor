-- =============================================================================
-- topology_cross_table_wiring.sql
-- Cross-table FK wiring that cannot be declared inline in CREATE TABLE DDL
-- because the referencing and referenced tables are defined in separate files
-- that load in a fixed order in init.sql.
--
-- Must run AFTER both topology_tables.sql AND ui_topology_tables.sql.
--
-- BOOTSTRAP POLICY: this file is part of the canonical fresh bootstrap
-- sequence executed by db/init.sql via docker-entrypoint-initdb.d/00-init.sql.
-- Do not apply db/migrations/ separately; the canonical schema lives here.
-- =============================================================================

-- FK: topology.structure_maps.layout_id -> topology.components_layout_design.layout_id
--
-- topology_tables.sql loads before ui_topology_tables.sql, so the FK to
-- topology.components_layout_design cannot be declared inline in the
-- CREATE TABLE topology.structure_maps definition. It is added here instead.
ALTER TABLE topology.structure_maps
    DROP CONSTRAINT IF EXISTS fk_structure_maps_layout_id;

ALTER TABLE topology.structure_maps
    ADD CONSTRAINT fk_structure_maps_layout_id
        FOREIGN KEY (layout_id)
        REFERENCES topology.components_layout_design (layout_id)
        ON DELETE SET NULL;
