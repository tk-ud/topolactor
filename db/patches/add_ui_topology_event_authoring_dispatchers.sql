-- One-off patch: ui_topology event authoring candidate list dispatchers missing from older seed_empty.sql.
-- Safe to re-run (ON CONFLICT DO NOTHING). Apply when UI Builder operation events show
-- MANIFEST_NOT_FOUND for list_external_port_authoring_candidates or
-- list_instance_operation_authoring_candidates.

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-0000000000d3',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_topology","action":"list_external_port_authoring_candidates"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000d4',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_topology","action":"list_instance_operation_authoring_candidates"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;

INSERT INTO topology.structure_maps (
    structure_map_id, name, attractor_key,
    package_id, schema_id, component_ids, active
)
VALUES
    (
        '00000000-0000-0000-0000-0000000000d3',
        'admin_ui_topology_list_external_port_authoring_candidates',
        'admin:ui_topology:list_external_port_authoring_candidates',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000d4',
        'admin_ui_topology_list_instance_operation_authoring_candidates',
        'admin:ui_topology:list_instance_operation_authoring_candidates',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    )
ON CONFLICT (structure_map_id) DO NOTHING;
