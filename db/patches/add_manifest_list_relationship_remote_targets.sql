-- One-off patch: register manifest:list_relationship_remote_targets for admin dispatch.
-- Run when Step 2.5 returns ADMIN_OPERATION_NOT_FOUND or MANIFEST_NOT_FOUND for this action.
-- Safe to re-run (ON CONFLICT DO NOTHING on manifest_id).

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-00000000007f',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"list_relationship_remote_targets"}'::jsonb,
        '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;
