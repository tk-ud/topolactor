-- One-off patch: active manifest with auth.user logical table for Step 2.5 remote targets.
-- Run when "有効マニフェストのテーブル" has no options (empty list_relationship_remote_targets).
-- Safe to re-run (ON CONFLICT updates topology).

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-000000000091',
    NULL,
    ARRAY[
        '{"type":"hub_grouping","manifestKey":"auth.user.boundary"}'::jsonb,
        '{"type":"screen_data_shape","logicalTables":[{"tableName":"auth.user","columns":[{"name":"id","dataType":"uuid","nullable":false},{"name":"username","dataType":"text","nullable":true}]}]}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;
