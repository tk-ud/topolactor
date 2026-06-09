-- One-off patch: register layout_patch:save_tmp and component_style_design:save_tmp for admin dispatch.
-- Run when UI Builder canvas auto-save returns MANIFEST_NOT_FOUND for save_tmp.
-- Safe to re-run (ON CONFLICT on manifest_id / structure_map_id).

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-0000000000ba',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"layout_patch","action":"save_tmp"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000bb',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"component_style_design","action":"save_tmp"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO topology.structure_maps (
    structure_map_id, name, attractor_key,
    package_id, schema_id, component_ids, active
)
VALUES
    (
        '00000000-0000-0000-0000-0000000000c1',
        'admin_layout_patch_save_tmp',
        'admin:layout_patch:save_tmp',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000c2',
        'admin_component_style_design_save_tmp',
        'admin:component_style_design:save_tmp',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    )
ON CONFLICT (structure_map_id) DO UPDATE
    SET name          = EXCLUDED.name,
        attractor_key = EXCLUDED.attractor_key,
        package_id    = EXCLUDED.package_id,
        schema_id     = EXCLUDED.schema_id,
        component_ids = EXCLUDED.component_ids,
        active        = EXCLUDED.active;
