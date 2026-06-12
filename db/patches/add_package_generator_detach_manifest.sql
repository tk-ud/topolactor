-- One-off patch: register package_generator:detach_package_components for admin dispatch.
-- Run when canvas node delete sync returns MANIFEST_NOT_FOUND for detach_package_components.
-- Safe to re-run (ON CONFLICT on manifest_id / structure_map_id).

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000000bc',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"package_generator","action":"detach_package_components"}'::jsonb,
        '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO topology.structure_maps (
    structure_map_id,
    name,
    attractor_key,
    package_id,
    schema_id,
    component_ids,
    active
)
VALUES (
    '00000000-0000-0000-0000-0000000000c3',
    'admin_package_generator_detach_package_components',
    'admin:package_generator:detach_package_components',
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
