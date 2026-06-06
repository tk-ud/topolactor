-- Migration: add layout_id to topology.structure_maps
-- Preserves layout identity from admin-authored layout storage through
-- backend emission to the application projection surface.
-- layout_id is nullable: existing rows without a layout remain valid.

ALTER TABLE topology.structure_maps
    ADD COLUMN IF NOT EXISTS layout_id UUID
        REFERENCES topology.components_layout_design (layout_id)
        ON DELETE SET NULL;

COMMENT ON COLUMN topology.structure_maps.layout_id IS
    'Optional reference to the admin-authored layout in topology.components_layout_design. '
    'When set, the layout_id is forwarded through EmissionBuilder into Emission.LayoutId '
    'and returned to the frontend as emission.layoutId for projection surface consumption. '
    'Null is valid and indicates no layout is bound to this structure map entry.';
