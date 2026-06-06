-- Migration: add layout_id to topology.structure_maps and attach FK to components_layout_design
--
-- topology_tables.sql loads before ui_topology_tables.sql in init.sql, so the FK
-- cannot be declared inline in the CREATE TABLE DDL. This migration handles both:
--   - existing DBs: adds the column and FK constraint
--   - fresh DBs (after bootstrap): adds only the FK constraint (column already present)

ALTER TABLE topology.structure_maps
    ADD COLUMN IF NOT EXISTS layout_id UUID;

ALTER TABLE topology.structure_maps
    DROP CONSTRAINT IF EXISTS fk_structure_maps_layout_id;

ALTER TABLE topology.structure_maps
    ADD CONSTRAINT fk_structure_maps_layout_id
        FOREIGN KEY (layout_id)
        REFERENCES topology.components_layout_design (layout_id)
        ON DELETE SET NULL;

COMMENT ON COLUMN topology.structure_maps.layout_id IS
    'Optional reference to the admin-authored layout in topology.components_layout_design. '
    'When set, the layout_id is forwarded through EmissionBuilder into Emission.LayoutId '
    'and returned to the frontend as emission.layoutId for projection surface consumption. '
    'Null is valid and indicates no layout is bound to this structure map entry.';
