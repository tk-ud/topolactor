-- One-off patch: manifest clone lifecycle dispatcher rows missing from older seed_empty.sql.
-- Safe to re-run (ON CONFLICT DO NOTHING). Apply when Step 1 returns MANIFEST_NOT_FOUND
-- for create_new_topology_draft without recreating the whole database volume.

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-000000000081',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"create_new_topology_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000082',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"create_clone_replacement_draft_from_active"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000083',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"create_clone_new_topology_draft_from_active"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000084',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"load_clone_source_evidence"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000cf',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"validate_clone_replacement_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000d0',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"merge_clone_replacement_draft_to_active"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000d1',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"list_screen_read_query_wiring"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000d2',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"list_aggregate_trigger_processing_functions"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;
