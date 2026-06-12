-- One-off patch: register mock_preset admin dispatch axes for UIBuilder preset intake.
-- Run when POST /dispatch returns MANIFEST_NOT_FOUND for mock_preset:list (or sibling actions).
-- Safe to re-run (ON CONFLICT on manifest_id / structure_map_id).
-- SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-0000000000bd',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"mock_preset","action":"create"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000be',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"mock_preset","action":"list"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000bf',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"mock_preset","action":"get"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000c4',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"mock_preset","action":"compile"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000c5',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"mock_preset","action":"bind"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000c6',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"mock_preset","action":"save_mappings"}'::jsonb,
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
        '00000000-0000-0000-0000-0000000000c7',
        'admin_mock_preset_create',
        'admin:mock_preset:create',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000c8',
        'admin_mock_preset_list',
        'admin:mock_preset:list',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000c9',
        'admin_mock_preset_get',
        'admin:mock_preset:get',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000ca',
        'admin_mock_preset_compile',
        'admin:mock_preset:compile',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000cb',
        'admin_mock_preset_bind',
        'admin:mock_preset:bind',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000cc',
        'admin_mock_preset_save_mappings',
        'admin:mock_preset:save_mappings',
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
