-- One-off patch: register package_generator:detach_package_components for admin dispatch.
-- Run when canvas node delete sync returns MANIFEST_NOT_FOUND for detach_package_components.

INSERT INTO manifest (manifest_id, hub_id, topology, status)
SELECT
    '00000000-0000-0000-0000-0000000000a9'::uuid,
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"package_generator","action":"detach_package_components"}'::jsonb,
        '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
WHERE NOT EXISTS (
    SELECT 1 FROM manifest m
    WHERE m.manifest_id = '00000000-0000-0000-0000-0000000000a9'::uuid
);

INSERT INTO topology.structure_maps (
    structure_map_id,
    name,
    attractor_key,
    package_id,
    schema_id,
    component_ids,
    active
)
SELECT
    '00000000-0000-0000-0000-0000000000a2'::uuid,
    'admin_package_generator_detach_package_components',
    'admin:package_generator:detach_package_components',
    '00000000-0000-0000-0000-000000000020'::uuid,
    '00000000-0000-0000-0000-000000000021'::uuid,
    ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
    true
WHERE NOT EXISTS (
    SELECT 1 FROM topology.structure_maps
    WHERE attractor_key = 'admin:package_generator:detach_package_components'
);
