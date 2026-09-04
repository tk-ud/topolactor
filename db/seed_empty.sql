-- =============================================================================
-- seed_empty.sql
-- Empty/default topology seed for the topolactor database.
--
-- PURPOSE:
--   Inserts the minimum set of well-known default rows needed to bootstrap
--   the topology space and prove the canonical dummy flow end-to-end.
--   Contains NO real business data.
--
-- DETERMINISTIC IDs:
--   The four topology nodes below use fixed UUIDs so the backend in-memory
--   runtime and the DB seed reference the same IDs without coordination:
--
--     default_package:             00000000-0000-0000-0000-000000000001
--     default_schema:              00000000-0000-0000-0000-000000000002
--     default_projection_component:00000000-0000-0000-0000-000000000003
--     structure_map (default):     00000000-0000-0000-0000-000000000004
--
--   These IDs are seed-contract identifiers. They are not meaningful outside this seed.
--
-- HOW TO RUN (in order):
--   psql -d <database> -f db/schema.sql
--   psql -d <database> -f db/topology_tables.sql
--   psql -d <database> -f db/promotion_tables.sql
--   psql -d <database> -f db/seed_empty.sql
-- =============================================================================


-- ---------------------------------------------------------------------------
-- state_registry defaults
-- ---------------------------------------------------------------------------
INSERT INTO topology.state_registry (state_id, name, owner)
VALUES
    (gen_random_uuid(), 'active',    'system'),
    (gen_random_uuid(), 'operating', 'business'),
    (gen_random_uuid(), 'archived',  'system')
ON CONFLICT DO NOTHING;


-- ---------------------------------------------------------------------------
-- relation_registry defaults
-- ---------------------------------------------------------------------------
INSERT INTO topology.relation_registry (
    relation_registry_id,
    name, master_ids, category, type, "order", weight, manifest_candidate, active
)
VALUES (
    gen_random_uuid(),
    'default', '{}', 'default', 'structural', 0, 1.0, false, true
)
ON CONFLICT DO NOTHING;


-- ---------------------------------------------------------------------------
-- package_registry — deterministic ID so structure_maps can reference it
-- ---------------------------------------------------------------------------
INSERT INTO topology.package_registry (package_id, name, type, package_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'default_package', 'core', '{}', true
)
ON CONFLICT (package_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- schema_registry — deterministic ID so structure_maps can reference it
-- ---------------------------------------------------------------------------
INSERT INTO topology.schema_registry (schema_id, name, schema_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000002',
    'default_schema', '{"fields":[{"key":"label","type":"text","label":"Label"}]}', true
)
ON CONFLICT (schema_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- component_registry — deterministic ID so structure_maps can reference it
-- ---------------------------------------------------------------------------
INSERT INTO topology.component_registry (component_id, name, component_type, component_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000003',
    'default_projection_component', 'renderer',
    '{"renders":"emission_data"}', true
)
ON CONFLICT (component_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- structure_maps — connects attractor_key "default:entity:search" to the
-- default package, schema, and component.
-- attractor_key is stored lowercase to match backend normalization
-- (OperationVectorResolver lowercases Target:Layer:Action).
-- ---------------------------------------------------------------------------
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
    '00000000-0000-0000-0000-000000000004',
    'default',
    'default:entity:search',
    '00000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000002',
    ARRAY['00000000-0000-0000-0000-000000000003']::uuid[],
    true
)
ON CONFLICT (structure_map_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- manifest — active runtime route for manifest-backed dispatch verification.
-- This route intentionally targets default/entity/Search so /dispatch can verify
-- ManifestDispatcher -> RuntimeExecutor path (non demo/admin override).
-- ---------------------------------------------------------------------------
INSERT INTO manifest (
    manifest_id,
    relation_registry_id,
    topology,
    status
)
VALUES (
    '00000000-0000-0000-0000-000000000040',
    NULL,
    ARRAY[
      '{"type":"dispatcher_mapping","role":"admin","target":"default","layer":"entity","action":"Search"}'::jsonb,
      '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
      '{"type":"runtime_mapping","runtime_destination":"topology_transform_runtime"}'::jsonb,
      '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
      '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- External port dispatch manifest — generic external_port runtime boundary.
-- Design Inspector dispatchExternalPort reaches this route through normal
-- manifest runtime_mapping, not a ManifestDispatcher hardcoded target branch.
-- Provider-specific clients and canonical physical binding execution are not
-- represented here; this maps only to the generic port-record/policy boundary.
-- ---------------------------------------------------------------------------
INSERT INTO manifest (
    manifest_id,
    relation_registry_id,
    topology,
    status
)
VALUES (
    '00000000-0000-0000-0000-000000000041',
    NULL,
    ARRAY[
      '{"type":"dispatcher_mapping","role":"admin","target":"external_port","layer":"external_port","action":"dispatchExternalPort"}'::jsonb,
      '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
      '{"type":"runtime_mapping","runtime_destination":"external_port_runtime"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- Admin manifests — active runtime routes for admin target routes.
--
-- dispatcher_mapping entries for admin target explicitly require role=admin
-- so JWT role authority at /dispatch aligns with manifest axis matching.
-- AdminRuntimeDispatchAdapter → AdminRuntime.ExecuteDataAsync (Gap-1b complete).
--
-- IDs 50-5a (hex) avoid conflict with structure_map IDs (0x01-0x40).
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-000000000050',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"seed_runtime","action":"save"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000051',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"seed_runtime","action":"load"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000052',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"seed_runtime","action":"validate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000053',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"seed_runtime","action":"preview"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000054',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"seed_runtime","action":"import"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000055',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"context_token_registry","action":"list"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000056',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"context_token_registry","action":"create"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000057',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"context_token_registry","action":"deprecate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000058',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"registry_vector","action":"validate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000059',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_component_bucket","action":"list"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000cd',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"component_registration","action":"register_or_update_projection_component"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005a',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"package_generator","action":"generate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005b',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"system_ci","action":"list_targets"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005c',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"system_ci","action":"inspect"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005d',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ci_attention","action":"refresh_fragments"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005e',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"list"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005f',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"get"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000060',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"validate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000061',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"create_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000062',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"update_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000063',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"promote"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000064',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"deprecate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000007d',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"assign_hub_grouping"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000007e',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"assign_screen_data_shape"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000007f',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"list_relationship_remote_targets"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000065',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"list_hubs"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000066',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"list_entities"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000067',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"list_relations"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000068',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"list_states"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000069',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"get_entity"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000006a',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"search"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000006b',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"create_entity_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000006c',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"validate_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000006d',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"preview_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000006e',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"promote_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000006f',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"promotion_manifest","action":"list"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000070',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"promotion_manifest","action":"get"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000071',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"promotion_manifest","action":"validate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000072',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"promotion_manifest","action":"update_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000073',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"get_hub"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000074',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"get_relation"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000075',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"update_entity_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000076',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"content_bundle","action":"list_hub_relations"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- sql_attention dispatch manifest (ID 80)
-- Required by AdminRuntime sql_attention:list_projection switch case.
-- Frontend-originated projection must route through admin dispatch pipeline;
-- direct GET bypass route is prohibited (NG5 dispatch_resolution violation).
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-000000000080',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"sql_attention","action":"list_projection"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- hub_navigation dispatch manifests (IDs 77-7c)
-- Registered for hub_navigation layer: list_manifests / get_hub_relations /
-- create / update / deprecate / reorder.
-- Required by AdminRuntime hub_navigation:* switch cases.
-- Silent MANIFEST_NOT_FOUND failure occurs at runtime without these records.
--
-- identity_selector_read (round 19): declared ONLY on list_manifests/get_hub_relations's
-- own dispatcher_mapping entry -- the SSOT-owned classification
-- ManifestDispatcher.IsBareManifestNavigationReadTargetRefAsync reads (via
-- DispatcherMappingAxisAuthority.IsDeclaredIdentitySelectorRead) instead of a
-- hardcoded action-name allowlist in runtime code. Marks these two actions as
-- read-only and manifest-identity-agnostic (both read their real target from
-- payload.topologyManifestId, never from which manifest resolved a target_ref),
-- safe to reach via a bare "runtime_mapping only" manifest used purely as a
-- navigation-context selector. create/update/deprecate/reorder deliberately have
-- no such field -- they mutate hubs.hub_relations and must never be reachable
-- through a bare manifest's target_ref, only through their own authored
-- dispatcher_mapping/capability_requirement.
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-000000000077',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"hub_navigation","action":"list_manifests","identity_selector_read":true}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000078',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"hub_navigation","action":"get_hub_relations","identity_selector_read":true}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000079',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"hub_navigation","action":"create"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000007a',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"hub_navigation","action":"update"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000007b',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"hub_navigation","action":"deprecate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000007c',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"hub_navigation","action":"reorder"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- ui_topology / layout_patch / bucket create / promote / admin_csv_json_import
-- dispatch manifests (IDs 85-8f)
-- Required by AdminRuntime switch cases; without these rows POST /dispatch returns
-- MANIFEST_NOT_FOUND (frontend /api/dispatch proxies the same backend route).
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-000000000085',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_topology","action":"layout_candidates"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000086',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_topology","action":"promoted_palette"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
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
    ),
    (
        '00000000-0000-0000-0000-000000000087',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"layout_patch","action":"preview"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000088',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"layout_patch","action":"validate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000089',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"layout_patch","action":"apply"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000008a',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_component_bucket","action":"create"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000008b',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"package_generator","action":"promote"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000008c',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"admin_csv_json_import","action":"upload_preview"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000008d',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"admin_csv_json_import","action":"apply"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000008e',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"admin_csv_json_import","action":"list_manifests"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000008f',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"admin_csv_json_import","action":"list_schemas"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a3',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_topology","action":"list_package_components"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a0',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_topology","action":"list_packages"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a1',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"component_style_design","action":"list"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a2',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"component_style_design","action":"upsert"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a4',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_topology","action":"get_package_wiring"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a5',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_topology","action":"update_package_wiring"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a6',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"package_generator","action":"promote_package"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000bc',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"package_generator","action":"detach_package_components"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a7',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"enum_dictionary","action":"list_groups"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a8',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"enum_dictionary","action":"get_group"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000a9',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"enum_dictionary","action":"create_group"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000aa',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"enum_dictionary","action":"update_group"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000ab',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"enum_dictionary","action":"delete_group"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000ac',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"enum_dictionary","action":"create_item"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000ad',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"enum_dictionary","action":"update_item"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000ae',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"enum_dictionary","action":"delete_item"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000af',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"enum_dictionary","action":"set_group_items"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000b9',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"ui_topology","action":"get_layout_patch_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
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
    ),
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
ON CONFLICT (manifest_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- Admin topology nodes — deterministic IDs for admin attractor resolution.
--
-- Admin attractor keys (6) all reference these three nodes:
--   admin_package:   00000000-0000-0000-0000-000000000020
--   admin_schema:    00000000-0000-0000-0000-000000000021
--   admin_component: 00000000-0000-0000-0000-000000000022
-- ---------------------------------------------------------------------------
INSERT INTO topology.package_registry (package_id, name, type, package_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000020',
    'admin_package', 'admin', '{}', true
)
ON CONFLICT (package_id) DO NOTHING;

INSERT INTO topology.schema_registry (schema_id, name, schema_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000021',
    'admin_schema', '{}', true
)
ON CONFLICT (schema_id) DO NOTHING;

INSERT INTO topology.component_registry (component_id, name, component_type, component_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000022',
    'admin_component', 'admin',
    '{}', true
)
ON CONFLICT (component_id) DO NOTHING;

-- structure_maps for admin attractor keys.
-- attractor_key format: "{target}:{layer}:{action}" (lowercase, matches OperationVectorResolver output).
INSERT INTO topology.structure_maps (
    structure_map_id, name, attractor_key,
    package_id, schema_id, component_ids, active
)
VALUES
    (
        '00000000-0000-0000-0000-000000000030',
        'admin_context_token_registry_list',
        'admin:context_token_registry:list',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000031',
        'admin_context_token_registry_create',
        'admin:context_token_registry:create',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000032',
        'admin_context_token_registry_deprecate',
        'admin:context_token_registry:deprecate',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000033',
        'admin_registry_vector_validate',
        'admin:registry_vector:validate',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000034',
        'admin_ui_component_bucket_list',
        'admin:ui_component_bucket:list',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000035',
        'admin_package_generator_generate',
        'admin:package_generator:generate',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000041',
        'admin_system_ci_list_targets',
        'admin:system_ci:list_targets',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000042',
        'admin_system_ci_inspect',
        'admin:system_ci:inspect',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000043',
        'admin_ci_attention_refresh_fragments',
        'admin:ci_attention:refresh_fragments',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000090',
        'admin_ui_topology_layout_candidates',
        'admin:ui_topology:layout_candidates',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000091',
        'admin_ui_topology_promoted_palette',
        'admin:ui_topology:promoted_palette',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
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
    ),
    (
        '00000000-0000-0000-0000-000000000092',
        'admin_ui_component_bucket_create',
        'admin:ui_component_bucket:create',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000ce',
        'admin_component_registration_projection_upsert',
        'admin:component_registration:register_or_update_projection_component',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000093',
        'admin_package_generator_promote',
        'admin:package_generator:promote',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000094',
        'admin_layout_patch_preview',
        'admin:layout_patch:preview',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000095',
        'admin_layout_patch_validate',
        'admin:layout_patch:validate',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000096',
        'admin_layout_patch_apply',
        'admin:layout_patch:apply',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000097',
        'admin_admin_csv_json_import_upload_preview',
        'admin:admin_csv_json_import:upload_preview',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000098',
        'admin_admin_csv_json_import_apply',
        'admin:admin_csv_json_import:apply',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000099',
        'admin_admin_csv_json_import_list_manifests',
        'admin:admin_csv_json_import:list_manifests',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-00000000009a',
        'admin_admin_csv_json_import_list_schemas',
        'admin:admin_csv_json_import:list_schemas',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-00000000009b',
        'admin_ui_topology_list_packages',
        'admin:ui_topology:list_packages',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-00000000009c',
        'admin_component_style_design_list',
        'admin:component_style_design:list',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-00000000009d',
        'admin_component_style_design_upsert',
        'admin:component_style_design:upsert',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-00000000009e',
        'admin_ui_topology_list_package_components',
        'admin:ui_topology:list_package_components',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-00000000009f',
        'admin_ui_topology_get_package_wiring',
        'admin:ui_topology:get_package_wiring',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000a0',
        'admin_ui_topology_update_package_wiring',
        'admin:ui_topology:update_package_wiring',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000a1',
        'admin_package_generator_promote_package',
        'admin:package_generator:promote_package',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000c3',
        'admin_package_generator_detach_package_components',
        'admin:package_generator:detach_package_components',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000b0',
        'admin_enum_dictionary_list_groups',
        'admin:enum_dictionary:list_groups',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000b1',
        'admin_enum_dictionary_get_group',
        'admin:enum_dictionary:get_group',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000b2',
        'admin_enum_dictionary_create_group',
        'admin:enum_dictionary:create_group',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000b3',
        'admin_enum_dictionary_update_group',
        'admin:enum_dictionary:update_group',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000b4',
        'admin_enum_dictionary_delete_group',
        'admin:enum_dictionary:delete_group',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000b5',
        'admin_enum_dictionary_create_item',
        'admin:enum_dictionary:create_item',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000b6',
        'admin_enum_dictionary_update_item',
        'admin:enum_dictionary:update_item',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000b7',
        'admin_enum_dictionary_delete_item',
        'admin:enum_dictionary:delete_item',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000b8',
        'admin_enum_dictionary_set_group_items',
        'admin:enum_dictionary:set_group_items',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-0000000000c0',
        'admin_ui_topology_get_layout_patch_draft',
        'admin:ui_topology:get_layout_patch_draft',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
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
    ),
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
ON CONFLICT (structure_map_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- Seed Runtime admin structure_maps (Issue #84)
-- attractor_key format: "admin:seed_runtime:{action}" (lowercase)
-- All operations share the admin package / schema / component topology nodes.
-- ---------------------------------------------------------------------------
INSERT INTO topology.structure_maps (
    structure_map_id, name, attractor_key,
    package_id, schema_id, component_ids, active
)
VALUES
    (
        '00000000-0000-0000-0000-000000000036',
        'admin_seed_runtime_save',
        'admin:seed_runtime:save',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000037',
        'admin_seed_runtime_load',
        'admin:seed_runtime:load',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000038',
        'admin_seed_runtime_validate',
        'admin:seed_runtime:validate',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000039',
        'admin_seed_runtime_preview',
        'admin:seed_runtime:preview',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    ),
    (
        '00000000-0000-0000-0000-000000000040',
        'admin_seed_runtime_import',
        'admin:seed_runtime:import',
        '00000000-0000-0000-0000-000000000020',
        '00000000-0000-0000-0000-000000000021',
        ARRAY['00000000-0000-0000-0000-000000000022']::uuid[],
        true
    )
ON CONFLICT (structure_map_id) DO NOTHING;



-- ---------------------------------------------------------------------------
-- topology.components_bucket bootstrap seed (canonical schema — was: ui_component_bucket)
-- Canonical bootstrap: seed catalog entries that still require DB registration.
-- Re-runnable via unique (component_key, source_path).
-- ---------------------------------------------------------------------------
INSERT INTO topology.components_bucket (component_key, source_path, component_kind, status, metadata_json)
VALUES
    ('button.primitive','frontend/components/Button.tsx','action/button','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"action","visualRole":"button","capabilityTags":["emits_event","requires_event_binding","accepts_design"]}}'::jsonb),
    ('input.primitive','frontend/components/Input.tsx','form_input/input','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","requires_event_binding","accepts_design"]}}'::jsonb),
    ('table.primitive','frontend/components/Table.tsx','data_display/table','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"table","capabilityTags":["selectable","accepts_design"]}}'::jsonb),
    ('card.primitive','frontend/components/Card.tsx','display/card','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"card","capabilityTags":["accepts_children","accepts_design"]}}'::jsonb),
    ('card_list.primitive','frontend/components/CardList.tsx','display/card_list','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"card","capabilityTags":["displays_backend_result","selectable","accepts_design"]}}'::jsonb),
    ('box.primitive','frontend/components/Box.tsx','layout/box','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"box","capabilityTags":["accepts_children","accepts_layout","accepts_design"]}}'::jsonb),
    ('form_field.template','frontend/components/FormField.tsx','form_input/form_field','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"input","visualRole":"field","capabilityTags":["accepts_children","error_display","accepts_design"]}}'::jsonb),
    ('select.template','frontend/components/Select.tsx','form_input/select','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('checkbox.template','frontend/components/Checkbox.tsx','form_input/checkbox','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('badge.template','frontend/components/Badge.tsx','display/badge','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"display","visualRole":"badge","capabilityTags":["accepts_design"]}}'::jsonb),
    ('status_badge.template','frontend/components/Badge.tsx','display/status_badge','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"badge","capabilityTags":["accepts_design"]}}'::jsonb),
    ('alert.template','frontend/components/Alert.tsx','display/alert','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"alert","capabilityTags":["accepts_design","error_display"]}}'::jsonb),
    ('loading_state.template','frontend/components/LoadingState.tsx','feedback/loading','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"panel","capabilityTags":["loading_display","accepts_design"]}}'::jsonb),
    ('empty_state.template','frontend/components/EmptyState.tsx','feedback/empty','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"panel","capabilityTags":["accepts_design"]}}'::jsonb),
    ('error_state.template','frontend/components/ErrorState.tsx','feedback/error','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"panel","capabilityTags":["error_display","accepts_design"]}}'::jsonb),
    ('json_viewer.template','frontend/components/JsonViewer.tsx','data_display/json','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"data_viewer","visualRole":"json_viewer","capabilityTags":["displays_json","accepts_design"]}}'::jsonb),
    ('admin_page_shell.template','frontend/components/AdminPageShell.tsx','shell/admin_page','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"layout_shell","visualRole":"page_shell","capabilityTags":["accepts_children","accepts_actions","admin_only","accepts_layout"]}}'::jsonb),
    ('admin_section.template','frontend/components/AdminSection.tsx','shell/admin_section','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_children","accepts_actions","admin_only","accepts_layout"]}}'::jsonb),
    ('validation_result_panel.template','frontend/components/ValidationResultPanel.tsx','validation/result','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"validation","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('textarea.template','frontend/components/Textarea.tsx','form_input/textarea_template','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('tabs.template','frontend/components/Tabs.tsx','disclosure/tabs','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"navigation","visualRole":"tabs","capabilityTags":["selectable","controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('modal.template','frontend/components/Modal.tsx','disclosure/modal','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"display","visualRole":"modal","capabilityTags":["accepts_children","accepts_actions","accepts_design","requires_event_binding"]}}'::jsonb),
    ('tree.template','frontend/components/Tree.tsx','data_display/tree','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"navigation","visualRole":"tree","capabilityTags":["recursive","selectable","accepts_design"]}}'::jsonb),
    ('tree_node.template','frontend/components/Tree.tsx','data_display/tree_node','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":false,"lifecycleStatus":"registered","componentFamily":"composite","semanticRole":"data_viewer","visualRole":"tree","capabilityTags":["recursive","selectable","accepts_design"]}}'::jsonb),
    -- ---------------------------------------------------------------------------
    -- UI/UX Primitive Catalog bootstrap rows (catalog_definition_only)
    -- Upper SSOT: docs/design/ui-ux-primitive-catalog-ssot.yaml
    -- seed is bootstrap boundary only — YAML/SSOT is vocabulary authority
    -- runtimeConnected:false until factory registration + catalog sourcePath promotion
    -- ---------------------------------------------------------------------------
    -- Category A: Search / Suggest / Candidate UI
    ('autocomplete_input.primitive','frontend/components/AutoCompleteInput.tsx','search_suggest/autocomplete_input','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('suggest_input.primitive','frontend/components/SuggestInput.tsx','search_suggest/suggest_input','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('search_combobox.primitive','frontend/components/SearchCombobox.tsx','search_suggest/search_combobox','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","selectable","accepts_design"]}}'::jsonb),
    ('select_import_dialog.primitive','frontend/components/SelectImportDialog.tsx','search_suggest/select_import_dialog','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"modal","capabilityTags":["selectable","emits_event","requires_event_binding","accepts_actions","admin_only"]}}'::jsonb),
    ('relation_candidate_picker.primitive','frontend/components/RelationCandidatePicker.tsx','search_suggest/relation_candidate_picker','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["selectable","emits_event","accepts_design"]}}'::jsonb),
    ('recent_input_suggest.primitive','frontend/components/RecentInputSuggest.tsx','search_suggest/recent_input_suggest','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('duplicate_merge_candidate_panel.primitive','frontend/components/DuplicateMergeCandidatePanel.tsx','search_suggest/duplicate_merge_candidate_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["accepts_actions","displays_backend_result","accepts_design"]}}'::jsonb),
    ('candidate_confidence_badge.primitive','frontend/components/CandidateConfidenceBadge.tsx','search_suggest/candidate_confidence_badge','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"badge","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('relation_path_preview.primitive','frontend/components/RelationPathPreview.tsx','search_suggest/relation_path_preview','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('field_resolver_inspector.primitive','frontend/components/FieldResolverInspector.tsx','search_suggest/field_resolver_inspector','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('schema_promotion_candidate_panel.primitive','frontend/components/SchemaPromotionCandidatePanel.tsx','search_suggest/schema_promotion_candidate_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","admin_only","displays_backend_result"]}}'::jsonb),
    -- Category B: Inline Edit / Preview Update / Audit UI
    ('inline_editable_field.primitive','frontend/components/InlineEditableField.tsx','inline_edit/inline_editable_field','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('inline_editable_jsonb_field.primitive','frontend/components/InlineEditableJsonbField.tsx','inline_edit/inline_editable_jsonb_field','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"json_viewer","capabilityTags":["controlled_value","emits_event","displays_json","accepts_design"]}}'::jsonb),
    ('patch_preview_panel.primitive','frontend/components/PatchPreviewPanel.tsx','inline_edit/patch_preview_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('diff_strike_text.primitive','frontend/components/DiffStrikeText.tsx','inline_edit/diff_strike_text','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["accepts_design"]}}'::jsonb),
    ('audit_diff_drawer.primitive','frontend/components/AuditDiffDrawer.tsx','inline_edit/audit_diff_drawer','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"modal","capabilityTags":["displays_backend_result","accepts_children","accepts_design"]}}'::jsonb),
    ('optimistic_update_boundary.primitive','frontend/components/OptimisticUpdateBoundary.tsx','inline_edit/optimistic_update_boundary','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"validation","visualRole":"panel","capabilityTags":["accepts_children","accepts_design"]}}'::jsonb),
    ('confirmed_update_button.primitive','frontend/components/ConfirmedUpdateButton.tsx','inline_edit/confirmed_update_button','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"action","visualRole":"button","capabilityTags":["emits_event","requires_event_binding","accepts_design"]}}'::jsonb),
    ('undo_timeline.primitive','frontend/components/UndoTimeline.tsx','inline_edit/undo_timeline','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"navigation","visualRole":"panel","capabilityTags":["selectable","displays_backend_result","accepts_design"]}}'::jsonb),
    ('conflict_resolution_panel.primitive','frontend/components/ConflictResolutionPanel.tsx','inline_edit/conflict_resolution_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","displays_backend_result","admin_only"]}}'::jsonb),
    -- Category C: Table / List / View Operation UI
    ('faceted_filter_bar.primitive','frontend/components/FacetedFilterBar.tsx','table_op/faceted_filter_bar','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('column_filter.primitive','frontend/components/ColumnFilter.tsx','table_op/column_filter','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('column_visibility_editor.primitive','frontend/components/ColumnVisibilityEditor.tsx','table_op/column_visibility_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('sort_control.primitive','frontend/components/SortControl.tsx','table_op/sort_control','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('group_by_control.primitive','frontend/components/GroupByControl.tsx','table_op/group_by_control','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('saved_view_selector.primitive','frontend/components/SavedViewSelector.tsx','table_op/saved_view_selector','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["selectable","emits_event","accepts_design"]}}'::jsonb),
    ('bulk_action_panel.primitive','frontend/components/BulkActionPanel.tsx','table_op/bulk_action_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"action","visualRole":"panel","capabilityTags":["accepts_actions","emits_event","accepts_design"]}}'::jsonb),
    ('virtualized_data_table.primitive','frontend/components/VirtualizedDataTable.tsx','table_op/virtualized_data_table','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"table","capabilityTags":["selectable","accepts_design"]}}'::jsonb),
    ('row_detail_drawer.primitive','frontend/components/RowDetailDrawer.tsx','table_op/row_detail_drawer','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"modal","capabilityTags":["accepts_children","accepts_design"]}}'::jsonb),
    ('pagination_control.primitive','frontend/components/PaginationControl.tsx','table_op/pagination_control','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"navigation","visualRole":"panel","capabilityTags":["emits_event","accepts_design"]}}'::jsonb),
    ('export_candidate_panel.primitive','frontend/components/ExportCandidatePanel.tsx','table_op/export_candidate_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","admin_only"]}}'::jsonb),
    -- Category D: Kanban / Tree / Drag-Drop / State Transition UI
    ('kanban_board.primitive','frontend/components/KanbanBoard.tsx','kanban_drag/kanban_board','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"panel","capabilityTags":["accepts_children","selectable","accepts_layout"]}}'::jsonb),
    ('drag_drop_state_transition.primitive','frontend/components/DragDropStateTransition.tsx','kanban_drag/drag_drop_state_transition','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"action","visualRole":"panel","capabilityTags":["emits_event"]}}'::jsonb),
    ('drag_sort_list.primitive','frontend/components/DragSortList.tsx','kanban_drag/drag_sort_list','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"panel","capabilityTags":["accepts_children","selectable","emits_event","accepts_layout"]}}'::jsonb),
    ('relation_drop_zone.primitive','frontend/components/RelationDropZone.tsx','kanban_drag/relation_drop_zone','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_children","emits_event","accepts_layout"]}}'::jsonb),
    ('tree_reorder_drop_zone.primitive','frontend/components/TreeReorderDropZone.tsx','kanban_drag/tree_reorder_drop_zone','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"tree","capabilityTags":["accepts_children","emits_event","accepts_layout"]}}'::jsonb),
    ('layout_drop_zone.primitive','frontend/components/LayoutDropZone.tsx','kanban_drag/layout_drop_zone','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_children","accepts_layout","accepts_design"]}}'::jsonb),
    ('component_placement_handle.primitive','frontend/components/ComponentPlacementHandle.tsx','kanban_drag/component_placement_handle','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["admin_only","accepts_layout"]}}'::jsonb),
    ('snap_grid_overlay.primitive','frontend/components/SnapGridOverlay.tsx','kanban_drag/snap_grid_overlay','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_layout","admin_only"]}}'::jsonb),
    ('state_transition_arrow.primitive','frontend/components/StateTransitionArrow.tsx','kanban_drag/state_transition_arrow','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["accepts_design"]}}'::jsonb),
    ('slot_placeholder_panel.primitive','frontend/components/SlotPlaceholderPanel.tsx','kanban_drag/slot_placeholder_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_layout","admin_only"]}}'::jsonb),
    -- Category E: Design Token / Style Token / Layout Token UI
    ('font_token_editor.primitive','frontend/components/FontTokenEditor.tsx','design_token/font_token_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('background_color_editor.primitive','frontend/components/BackgroundColorEditor.tsx','design_token/background_color_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('text_color_editor.primitive','frontend/components/TextColorEditor.tsx','design_token/text_color_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('spacing_token_editor.primitive','frontend/components/SpacingTokenEditor.tsx','design_token/spacing_token_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('border_radius_editor.primitive','frontend/components/BorderRadiusEditor.tsx','design_token/border_radius_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('theme_preview_panel.primitive','frontend/components/ThemePreviewPanel.tsx','design_token/theme_preview_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('layout_grid_editor.primitive','frontend/components/LayoutGridEditor.tsx','design_token/layout_grid_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["controlled_value","emits_event","admin_only","accepts_layout"]}}'::jsonb),
    ('responsive_rule_editor.primitive','frontend/components/ResponsiveRuleEditor.tsx','design_token/responsive_rule_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["controlled_value","emits_event","admin_only","accepts_layout"]}}'::jsonb),
    ('style_token_picker.primitive','frontend/components/StyleTokenPicker.tsx','design_token/style_token_picker','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["selectable","emits_event","accepts_design"]}}'::jsonb),
    ('css_variable_preview.primitive','frontend/components/CssVariablePreview.tsx','design_token/css_variable_preview','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('shadow_token_editor.primitive','frontend/components/ShadowTokenEditor.tsx','design_token/shadow_token_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('animation_token_editor.primitive','frontend/components/AnimationTokenEditor.tsx','design_token/animation_token_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    -- Category F: Calculation / Topology Computation UI
    ('calculation_preview_panel.primitive','frontend/components/CalculationPreviewPanel.tsx','calc_topology/calculation_preview_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('formula_builder.primitive','frontend/components/FormulaBuilder.tsx','calc_topology/formula_builder','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('computed_field_preview.primitive','frontend/components/ComputedFieldPreview.tsx','calc_topology/computed_field_preview','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('relation_score_preview.primitive','frontend/components/RelationScorePreview.tsx','calc_topology/relation_score_preview','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('hub_statistics_panel.primitive','frontend/components/HubStatisticsPanel.tsx','calc_topology/hub_statistics_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('aggregation_preview_table.primitive','frontend/components/AggregationPreviewTable.tsx','calc_topology/aggregation_preview_table','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"table","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('cross_entity_calculation_panel.primitive','frontend/components/CrossEntityCalculationPanel.tsx','calc_topology/cross_entity_calculation_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('topology_distance_preview.primitive','frontend/components/TopologyDistancePreview.tsx','calc_topology/topology_distance_preview','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('route_cost_preview.primitive','frontend/components/RouteCostPreview.tsx','calc_topology/route_cost_preview','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('attention_weight_preview.primitive','frontend/components/AttentionWeightPreview.tsx','calc_topology/attention_weight_preview','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('cooccurrence_matrix_preview.primitive','frontend/components/CooccurrenceMatrixPreview.tsx','calc_topology/cooccurrence_matrix_preview','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('rank_score_preview.primitive','frontend/components/RankScorePreview.tsx','calc_topology/rank_score_preview','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    -- Category G: External / Helper Lookup UI
    ('kana_assist_input.primitive','frontend/components/KanaAssistInput.tsx','external_lookup/kana_assist_input','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('postal_address_lookup.primitive','frontend/components/PostalAddressLookup.tsx','external_lookup/postal_address_lookup','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('address_postal_lookup.primitive','frontend/components/AddressPostalLookup.tsx','external_lookup/address_postal_lookup','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('tel_address_candidate_lookup.primitive','frontend/components/TelAddressCandidateLookup.tsx','external_lookup/tel_address_candidate_lookup','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('normalize_address_candidate.primitive','frontend/components/NormalizeAddressCandidate.tsx','external_lookup/normalize_address_candidate','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('lookup_candidate_confirm_panel.primitive','frontend/components/LookupCandidateConfirmPanel.tsx','external_lookup/lookup_candidate_confirm_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"validation","visualRole":"panel","capabilityTags":["accepts_actions","displays_backend_result"]}}'::jsonb),
    ('bulk_import_candidate_panel.primitive','frontend/components/BulkImportCandidatePanel.tsx','external_lookup/bulk_import_candidate_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","admin_only","displays_backend_result"]}}'::jsonb),
    -- Category G2: Provider-agnostic Media UI
    ('audio_player.primitive','frontend/components/AudioPlayer.tsx','media/audio_player','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["accepts_design"]}}'::jsonb),
    ('video_player.primitive','frontend/components/VideoPlayer.tsx','media/video_player','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["accepts_design"]}}'::jsonb),
    -- Category H: Safety / Inspector / Operation Guard UI
    ('command_palette.primitive','frontend/components/CommandPalette.tsx','safety_guard/command_palette','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"navigation","visualRole":"modal","capabilityTags":["accepts_actions","admin_only"]}}'::jsonb),
    ('empty_state_action_panel.primitive','frontend/components/EmptyStateActionPanel.tsx','safety_guard/empty_state_action_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"feedback","visualRole":"panel","capabilityTags":["accepts_actions","accepts_design"]}}'::jsonb),
    ('operation_guard_banner.primitive','frontend/components/OperationGuardBanner.tsx','safety_guard/operation_guard_banner','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"feedback","visualRole":"alert","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('mutation_boundary_inspector.primitive','frontend/components/MutationBoundaryInspector.tsx','safety_guard/mutation_boundary_inspector','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('permission_hint_panel.primitive','frontend/components/PermissionHintPanel.tsx','safety_guard/permission_hint_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"feedback","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('dry_run_result_panel.primitive','frontend/components/DryRunResultPanel.tsx','safety_guard/dry_run_result_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('apply_confirm_dialog.primitive','frontend/components/ApplyConfirmDialog.tsx','safety_guard/apply_confirm_dialog','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"modal","capabilityTags":["accepts_actions","admin_only"]}}'::jsonb),
    ('rollback_candidate_panel.primitive','frontend/components/RollbackCandidatePanel.tsx','safety_guard/rollback_candidate_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","admin_only","displays_backend_result"]}}'::jsonb),
    ('operation_audit_log_panel.primitive','frontend/components/OperationAuditLogPanel.tsx','safety_guard/operation_audit_log_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('validation_error_panel.primitive','frontend/components/ValidationErrorPanel.tsx','safety_guard/validation_error_panel','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"validation","visualRole":"panel","capabilityTags":["error_display","displays_backend_result","accepts_design"]}}'::jsonb),
    -- Category G (Document Canvas): Document Canvas / Template Projection UI
    -- projection_scaffold_only | runtimeConnected:true | runtime projection surface
    ('document_canvas_template_editor.primitive','frontend/components/DocumentCanvasTemplateEditor.tsx','document_canvas/document_canvas_template_editor','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"canvas","capabilityTags":["accepts_design","accepts_layout","field_binding","preview_surface","export_snapshot","admin_only"]}}'::jsonb)
ON CONFLICT (component_key, source_path) DO UPDATE
SET component_kind = EXCLUDED.component_kind,
    status = EXCLUDED.status,
    metadata_json = EXCLUDED.metadata_json,
    updated_at = now();

-- ---------------------------------------------------------------------------
-- function_parameters — context route recommendation policy
--
-- Policy source for ContextRouteRecommendationResolver.
-- Loaded via TopologyRepository.LoadFunctionParameterAsync(
--   "context_route_recommendation_resolve", "default_policy").
-- Policy-missing → ExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND").
-- ---------------------------------------------------------------------------
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'context_route_recommendation_resolve',
    'default_policy',
    '{"min_similarity":0.05,"top_k":50,"min_neighbors":10,"recent_days":90,"max_candidates_shown":5,"baseline_weight":0.5,"neighbor_weight":0.5,"transition_aggregation":{"aggregation_limit":10000,"prefer_recent":true,"recent_days":null},"topology_vector_runtime":{"enabled":true,"registry_validation":{"enabled":true,"duplicate_threshold":1.0,"near_duplicate_threshold":0.85,"related_threshold":0.60,"top_k":10},"hub_attention":{"enabled":true,"scope_limits":[1000,3000,10000],"ema_fast_alpha":0.30,"ema_slow_alpha":0.10,"max_update_candidates_per_event":10000},"transition_key_evidence":{"enabled":true,"operation_contribution":1.0,"relation_contribution":0.8,"state_contribution":0.7,"table_contribution":0.6,"neighbor_top_k":3},"topology_mlp":{"enabled":true,"max_feature_cross_order":3},"feedback_weight_update":{"enabled":true,"positive_delta":0.05,"negative_delta":-0.02,"missing_candidate_delta":0.03},"recommendation_blend":{"enabled":true,"scope_limit":1000,"attention_score_weight":1.0,"trend_weight":0.0,"statistics_weight":0.0}}}',
    true
)
ON CONFLICT (function_name, parameter_key) DO UPDATE
    SET parameter_value = EXCLUDED.parameter_value,
        active          = EXCLUDED.active;


-- ---------------------------------------------------------------------------
-- context_event retention policy
-- Loaded by RetentionScheduler → LogRetentionRuntime via
--   TopologyRepository.LoadFunctionParameterAsync("context_event_retention", "retention_policy").
-- hot_days: safety floor — events newer than hot_days are never deleted or archived,
--           even if cold_days would otherwise select them. Must be a positive integer if present.
-- cold_days: events older than cold_days are eligible for cleanup (subject to hot_days).
-- archive_strategy: "delete" — permanently purge rows from context_event.
--                   "archive" — move rows to context_event_cold before removing from context_event.
--                   This seed uses "delete" as the default strategy.
-- batch_size: rows per cleanup batch to avoid long-lock transactions.
-- enabled: false disables cleanup; RetentionScheduler logs Disabled status instead of skipping silently.
-- schedule_interval_hours: how often RetentionScheduler triggers the runtime.
-- ---------------------------------------------------------------------------
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'context_event_retention',
    'retention_policy',
    '{"hot_days":90,"cold_days":365,"archive_strategy":"delete","batch_size":1000,"enabled":true,"schedule_interval_hours":24}',
    true
)
ON CONFLICT (function_name, parameter_key) DO NOTHING;


-- ---------------------------------------------------------------------------
-- sql_attention_topology_projection default_policy
-- Loaded by SqlAttentionTopologyProjectionRuntime via
--   TopologyRepository.LoadFunctionParameterAsync(
--     "sql_attention_topology_projection", "default_policy").
-- Policy-missing → MissingPolicy explicit failure (no silent fallback).
-- top_k: maximum number of evidence rows to retrieve from logs.attention.
-- min_neighbor_score: minimum neighbor_score threshold for evidence inclusion.
-- recent_window_days: rolling window in days for evidence recency filter.
-- All three keys are required and must be positive; absence or non-positive value
-- triggers MalformedPolicy explicit failure.
-- ---------------------------------------------------------------------------
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'sql_attention_topology_projection',
    'default_policy',
    '{"top_k":10,"min_neighbor_score":0.85,"recent_window_days":30}',
    true
)
ON CONFLICT (function_name, parameter_key) DO UPDATE
    SET parameter_value = EXCLUDED.parameter_value,
        active          = EXCLUDED.active;


-- ---------------------------------------------------------------------------
-- sql_attention_hub_attractor_exploration default_policy
-- Loaded by HubAttractorExplorationRuntime via
--   TopologyRepository.LoadFunctionParameterAsync(
--     "sql_attention_hub_attractor_exploration", "default_policy").
-- w / l2_norm gate: norm_level_high/medium classify weak/mid/high tiers;
-- exploration_budget_tiers.{weak,mid,high} bound topK, hub-table distance band,
-- and permutation expansion — not full-space search.
-- Policy-missing → MissingPolicy; malformed/non-positive → MalformedPolicy.
-- ---------------------------------------------------------------------------
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'sql_attention_hub_attractor_exploration',
    'default_policy',
    '{"norm_level_high":10.0,"norm_level_medium":1.0,"exploration_budget_tiers":{"weak":{"topK_per_hub_kind":1,"max_hub_tables_per_kind":2,"phase_expansion_limit":1,"search_mode":"near_neighbor_narrow_topK"},"mid":{"topK_per_hub_kind":3,"max_hub_tables_per_kind":5,"phase_expansion_limit":1,"search_mode":"normal_topK"},"high":{"topK_per_hub_kind":5,"max_hub_tables_per_kind":10,"phase_expansion_limit":3,"search_mode":"expanded_distance_band_or_permutation"}},"max_hub_kinds_per_current":5,"max_attention_rows_saved":20,"neighbor_score_min":0.0,"strong_hit_threshold":0.95,"normal_hit_threshold":0.90,"exploratory_hit_threshold":0.85}',
    true
)
ON CONFLICT (function_name, parameter_key) DO UPDATE
    SET parameter_value = EXCLUDED.parameter_value,
        active          = EXCLUDED.active;


-- ---------------------------------------------------------------------------
-- sql_attention_logs_watch default_policy
-- Loaded by logs.refresh_logs_current_watch for topN physical heat and norm-level watch.
-- ---------------------------------------------------------------------------
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'sql_attention_logs_watch',
    'default_policy',
    '{"top_n":3,"delta_threshold":0.0,"norm_level_high":10.0,"norm_level_medium":1.0}',
    true
)
ON CONFLICT (function_name, parameter_key) DO UPDATE
    SET parameter_value = EXCLUDED.parameter_value,
        active          = EXCLUDED.active;


-- ---------------------------------------------------------------------------
-- sql_attention_manifest_topology_key_expansion default_policy
-- Data-defined scoring/dampening policy for the manifest_topology_key_expansion
-- draft lane (logs.compile_sql_attention_manifest_topology_draft_candidates).
-- No hidden literals: discrete Key name patterns, generic column names, id column
-- suffixes, dampening factors, lift floor, candidate caps, and per-axis score
-- weights are all resolved from this row. Policy-missing or required-key-missing
-- triggers RAISE EXCEPTION fail-close in logs.resolve_sql_attention_key_expansion_policy.
--
-- Scoring is NOT raw count:
--   - routine high-frequency values (value present in > routine_value_manifest_fraction_dampen
--     of active manifests) are dampened by routine_value_dampen_factor (lift/pressure delta).
--   - generic columns (generic_column_names) are dampened by generic_column_dampen_factor.
--   - ID columns (id_column_suffixes) are treated as relationship axis / dimension candidates
--     with id_axis weight, never as primary display text.
--   - lift contributes only when lift >= lift_min.
-- ---------------------------------------------------------------------------
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'sql_attention_manifest_topology_key_expansion',
    'default_policy',
    '{"max_keys":25,"min_key_pressure":1,"max_candidates":50,"min_candidate_score":0.5,"discrete_key_name_patterns":["status","state","category","type","kind","enum","flag","bool","level","mode","stage","phase","priority","severity","role","group","class","tag","label","currency","country","lang","locale","unit"],"generic_column_names":["name","title","description","label","value","data","json","payload","note","comment","text","content","meta"],"id_column_suffixes":["_id","_ids","_ref","_key","_uuid"],"routine_value_manifest_fraction_dampen":0.6,"routine_value_dampen_factor":0.4,"generic_column_dampen_factor":0.5,"lift_min":1.0,"score_weights":{"same_name_axis":1.0,"same_type_axis":0.4,"common_axis":1.5,"enum_group_match":1.2,"value_overlap":0.8,"logs_diff_pressure":0.5,"logs_attention_pressure":0.6,"table_ref_reuse":0.5,"manifest_reuse":0.7,"id_axis":0.9,"lift":0.8}}',
    true
)
ON CONFLICT (function_name, parameter_key) DO UPDATE
    SET parameter_value = EXCLUDED.parameter_value,
        active          = EXCLUDED.active;


-- ---------------------------------------------------------------------------
-- manifest — user login UI (ID 090)
-- SSOT: docs/design/auth-db-session-credential-ssot.yaml
-- Submit delegates to auth_runtime.login — no credentials in topology.
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-000000000090',
    NULL,
    ARRAY[
        '{"type":"auth_action_binding","action":"login","runtime_destination":"auth_runtime","realm":"user","audience":"user_app"}'::jsonb,
        '{"type":"ui_projection_mapping","surface":"login_form","fields":["username","password"],"labels":{"username":"ユーザー名","password":"パスワード","submit":"ログイン"},"redirect_success":"/","redirect_failure":null}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

-- ---------------------------------------------------------------------------
-- manifest — auth.user boundary for Step 2.5 remote relationship targets (ID 091)
-- Exposes logical table auth.user (column id) for joins from business drafts (e.g. employees.user_id).
-- SSOT: docs/design/auth-db-session-credential-ssot.yaml (relationship_boundary_manifest)
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-000000000091',
    NULL,
    ARRAY[
        '{"type":"hub_grouping","manifestKey":"auth.user.boundary"}'::jsonb,
        '{"type":"screen_data_shape","contentsType":"relationship_boundary","logicalTables":[{"tableName":"auth.user","columns":[{"name":"id","dataType":"uuid","nullable":false},{"name":"username","dataType":"text","nullable":true}]}]}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

-- ---------------------------------------------------------------------------
-- manifest — auth/external credential management topology projection (ID 092)
-- Bundle: auth-external-credential-management-topology-projection
-- Fixed-form admin projection over existing manifest/screen_data_shape/Step 2.5
-- relation boundaries. This is not a UI Builder preset/component, dedicated
-- credential route/panel, or physical-table row editor. Secret/token/encrypted
-- payload values are intentionally absent; only reference metadata is projected.
-- assign_screen_data_shape dispatch route is manifest 07e only (see remove_duplicate_assign_screen_data_shape_dispatcher.sql).
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-000000000092',
    NULL,
    ARRAY[
        '{"type":"hub_grouping","manifestKey":"auth.external.credential_management.projection","bundle":"auth-external-credential-management-topology-projection","parentBundle":"external-port-substrate-implementation"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
        -- Round 4/5: manifest 092's OWN dispatcher_mapping entry, unified in round 5 onto
        -- credential_management:search (docs/design/admin-normal-surface-projection-seed-ssot.yaml
        -- surface_axes.admin.surfaces.credentials.seed_contract's shared credential_search/
        -- credential_category_filter/credential_result_list component_tree, spanning all three
        -- categories: users/external_api_credential/external_instance_credential). Round 4 first
        -- proved this same-manifest live-binding mechanism for external_api_credential alone; round
        -- 5 widens the SAME single dispatcher_mapping slot to a category-routed action
        -- (AdminRuntime.CredentialManagementSearch.cs) so every category gets the identical
        -- live-binding, never three separate ones. Deliberately the search action ONLY --
        -- ManifestTopologyValidator rejects 2+ dispatcher_mapping entries on one manifest, so
        -- create/update/delete stay on their own dispatch-only manifests (cd008-010, cd012-014,
        -- ad004-008) below. Making search resolve to 092's OWN manifest id (rather than a separate
        -- dispatch-only manifest, as create/update/delete use) means a search dispatch's
        -- response.emission.manifestId equals 092's adopted identity, so the EXISTING generic
        -- same-manifest response-adoption path in frontend/islands/ProjectionShell.tsx
        -- (handleRuntimeDispatchResult's expectedManifestId===adoptedManifestId branch) adopts the
        -- response into the SAME rendered screen -- the identical mechanism admin-enum's
        -- enum_dictionary:list_groups dispatch (its own manifest ae200) already uses for
        -- enum_table's propBindings.rows.source="emission.data.groups". No new frontend
        -- response-binding mechanism is introduced; this reuses that one exactly, only the
        -- seed-authored dispatch target and category routing differ.
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"credential_management","action":"search"}'::jsonb,
        '{"type":"fixed_form_projection","surface":"auth_external_credential_management","draft_edit_only":true,"validate_preview_apply_required":true,"ui_builder_authority":false,"physical_row_editor":false,"dedicated_credential_route":false,"consumer_bundle_connection":false,"credential_management_categories":["users","external_api_credential","external_instance_credential"],"default_category":"external_api_credential","category_selector_mode":"select_mode_category","secret_fields_forbidden":["plaintext_secret","secret","token","access_token","refresh_token","encrypted_payload","connection_string","endpoint_real_value","raw_sql","private_key","runtime_only_decrypted_payload","approval_bypass_authority"],"policy_step_editing":"template_selection_only"}'::jsonb,
        '{"type":"instance_settings_json_template_policy","category":"instance_settings","download_enabled":true,"import_enabled":true,"validate_preview_apply_required":true,"public_safe_shape_only":true,"template_sections":["db_instance_port","runtime_instance_port","instance_connection_policy","instance_operation_authority_binding"],"forbidden_template_fields":["secret","plaintext_secret","token","access_token","refresh_token","encrypted_payload","connection_string","endpoint_real_value","raw_sql","private_key","runtime_only_decrypted_payload","approval_bypass_authority"],"safe_reference_fields":["reference_key","instance_authority_key","operation_binding_key","policy_template_key"]}'::jsonb,
        '{"type":"admin_event_authoring_boundary","surface":"admin_ui_builder_design_inspector","category":"instance_settings","allowedActionType":"dispatchInstanceOperation","allowedTargetRefField":"instanceTargetRef","candidateScope":"approved_instance_operation_only","allowedAssignments":["trigger","payloadFrom","outputProp"],"forbiddenEditors":["instance_function_definition","address_edit","schema_edit","raw_sql_edit","credential_edit"],"targetRefPrefix":"instance-port:"}'::jsonb,
        '{"type":"physical_binding","mode":"seed_projection_marker_only","canonical_execution":"runtime_resolves_tableRef_to_physical_table_manifest_bindings","tables":["topology.external_access_ports","topology.external_response_ports","topology.external_hook_ports","topology.external_port_policies","topology.db_instance_port","topology.runtime_instance_port","topology.instance_connection_policy","topology.instance_operation_authority_binding"],"forbidden":"generic_physical_table_row_editor"}'::jsonb,
        '{"type":"canonical_port_bindings","manifestKey":"auth.external.credential_management.projection","portKindTableRefs":{"access_port":"topology.external_access_ports","response_port":"topology.external_response_ports","hook_port":"topology.external_hook_ports","db_instance_port":"topology.db_instance_port","runtime_instance_port":"topology.runtime_instance_port"},"instancePolicyTableRefs":{"instance_connection_policy":"topology.instance_connection_policy","instance_operation_authority_binding":"topology.instance_operation_authority_binding"},"verifiedBy":"physical_table_manifest_bindings"}'::jsonb,
        '{"type":"screen_data_shape","contentsType":"bundle_projection","topologySystemName":"auth-external-credential-management-topology-projection","userFacingTopologyLabel":"Auth / external credential management","tableRef":"topology.external_response_ports","dbTableName":"topology.external_response_ports","screenOperationKinds":["list","update"],"displayColumnMode":"selected","displayColumns":["credential_management_category.category_key","external_port_context.port_kind","external_port_context.provider_kind","external_port_context.credential_kind","external_port_context.reference_key","external_port_context.required_by_bundle","external_port_context.consumer_bundle_binding","external_port_context.policy_template_key","instance_connection_policy.instance_authority_key","instance_operation_authority_binding.operation_binding_key","instance_operation_authority_binding.approval_status"],"designInspectorEventCandidates":[{"actionType":"dispatchInstanceOperation","candidateScope":"approved_instance_operation_only","instanceTargetRef":"instance-port:db_instance_port:public-safe-placeholder:approved-operation-placeholder","assignableFields":["trigger","payloadFrom","outputProp"],"forbiddenEditors":["instance_function_definition","address_edit","schema_edit","raw_sql_edit","credential_edit"]}]}'::jsonb,
        '{"type":"credential_management_logical_table_shape","tableName":"credential_management_category","columns":[{"name":"category_key","dataType":"text","nullable":false},{"name":"mode","dataType":"text","nullable":false},{"name":"label","dataType":"text","nullable":false}]}'::jsonb,
        '{"type":"credential_management_logical_table_shape","tableName":"external_port_context","columns":[{"name":"port_context_id","dataType":"uuid","nullable":false},{"name":"category_key","dataType":"text","nullable":false},{"name":"port_kind","dataType":"text","nullable":false},{"name":"provider_kind","dataType":"text","nullable":false},{"name":"credential_kind","dataType":"text","nullable":false},{"name":"reference_key","dataType":"text","nullable":true},{"name":"required_by_bundle","dataType":"text","nullable":false},{"name":"consumer_bundle_binding","dataType":"text","nullable":true},{"name":"policy_template_key","dataType":"text","nullable":false},{"name":"auth_user_id","dataType":"uuid","nullable":true}]}'::jsonb,
        '{"type":"credential_management_logical_table_shape","tableName":"policy_template_selection","columns":[{"name":"policy_template_key","dataType":"text","nullable":false},{"name":"category_key","dataType":"text","nullable":false},{"name":"port_kind","dataType":"text","nullable":false},{"name":"required_by_bundle","dataType":"text","nullable":false}]}'::jsonb,
        '{"type":"credential_management_logical_table_shape","tableName":"db_instance_port","columns":[{"name":"db_instance_port_id","dataType":"uuid","nullable":false},{"name":"category_key","dataType":"text","nullable":false},{"name":"instance_authority_key","dataType":"text","nullable":false},{"name":"provider_kind","dataType":"text","nullable":false},{"name":"reference_key","dataType":"text","nullable":true},{"name":"policy_template_key","dataType":"text","nullable":false},{"name":"status","dataType":"text","nullable":false}]}'::jsonb,
        '{"type":"credential_management_logical_table_shape","tableName":"runtime_instance_port","columns":[{"name":"runtime_instance_port_id","dataType":"uuid","nullable":false},{"name":"category_key","dataType":"text","nullable":false},{"name":"instance_authority_key","dataType":"text","nullable":false},{"name":"provider_kind","dataType":"text","nullable":false},{"name":"reference_key","dataType":"text","nullable":true},{"name":"policy_template_key","dataType":"text","nullable":false},{"name":"status","dataType":"text","nullable":false}]}'::jsonb,
        '{"type":"credential_management_logical_table_shape","tableName":"instance_connection_policy","columns":[{"name":"policy_key","dataType":"text","nullable":false},{"name":"category_key","dataType":"text","nullable":false},{"name":"instance_authority_key","dataType":"text","nullable":false},{"name":"allowed_operation_scope","dataType":"text","nullable":false},{"name":"approval_required","dataType":"boolean","nullable":false},{"name":"timeout_policy_key","dataType":"text","nullable":false}]}'::jsonb,
        '{"type":"credential_management_logical_table_shape","tableName":"instance_operation_authority_binding","columns":[{"name":"operation_binding_key","dataType":"text","nullable":false},{"name":"category_key","dataType":"text","nullable":false},{"name":"instance_authority_key","dataType":"text","nullable":false},{"name":"operation_key","dataType":"text","nullable":false},{"name":"approval_status","dataType":"text","nullable":false},{"name":"event_action_type","dataType":"text","nullable":false},{"name":"instanceTargetRef","dataType":"text","nullable":false}]}'::jsonb,
        '{"type":"credential_management_logical_table_shape","tableName":"instance_settings_json_template","columns":[{"name":"template_key","dataType":"text","nullable":false},{"name":"category_key","dataType":"text","nullable":false},{"name":"public_safe_shape_only","dataType":"boolean","nullable":false},{"name":"validate_preview_apply_required","dataType":"boolean","nullable":false}]}'::jsonb,
        '{"type":"credential_management_relation_intents","relationIntents":[{"localTableRef":"external_port_context","joinTableRef":"credential_management_category","localKey":"category_key","remoteKey":"category_key"},{"localTableRef":"external_port_context","joinTableRef":"auth.user","localKey":"auth_user_id","remoteKey":"id","remoteManifestId":"00000000-0000-0000-0000-000000000091"},{"localTableRef":"external_port_context","joinTableRef":"policy_template_selection","localKey":"policy_template_key","remoteKey":"policy_template_key"},{"localTableRef":"db_instance_port","joinTableRef":"credential_management_category","localKey":"category_key","remoteKey":"category_key"},{"localTableRef":"runtime_instance_port","joinTableRef":"credential_management_category","localKey":"category_key","remoteKey":"category_key"},{"localTableRef":"instance_connection_policy","joinTableRef":"db_instance_port","localKey":"instance_authority_key","remoteKey":"instance_authority_key"},{"localTableRef":"instance_connection_policy","joinTableRef":"runtime_instance_port","localKey":"instance_authority_key","remoteKey":"instance_authority_key"},{"localTableRef":"instance_operation_authority_binding","joinTableRef":"instance_connection_policy","localKey":"instance_authority_key","remoteKey":"instance_authority_key"},{"localTableRef":"instance_settings_json_template","joinTableRef":"credential_management_category","localKey":"category_key","remoteKey":"category_key"}]}'::jsonb,
        '{"type":"credential_management_operation_entity_bindings","operationEntityBindings":[{"operationKind":"list","entityTargetColumns":["category_key","port_kind","provider_kind","credential_kind","reference_key","required_by_bundle","consumer_bundle_binding","policy_template_key","instance_authority_key","operation_binding_key","approval_status"]},{"operationKind":"update","entityTargetColumns":["credential_kind","reference_key","policy_template_key","instance_authority_key","operation_binding_key","approval_status"]},{"operationKind":"download_json_template","entityTargetColumns":["template_key","category_key","public_safe_shape_only"]},{"operationKind":"import_json_template","entityTargetColumns":["template_key","category_key"]},{"operationKind":"validate","entityTargetColumns":["template_key","category_key","public_safe_shape_only"]},{"operationKind":"preview","entityTargetColumns":["template_key","category_key","validate_preview_apply_required"]},{"operationKind":"apply","entityTargetColumns":["template_key","category_key","validate_preview_apply_required"]}]}'::jsonb,
        '{"type":"credential_management_admin_action_wiring","adminActionWiring":[{"category_key":"instance_settings","actionType":"download_json_template","template_key":"instance_settings_public_safe_template"},{"category_key":"instance_settings","actionType":"import_json_template","validate_preview_apply_required":true},{"category_key":"instance_settings","actionType":"list_edit","target":"registered_runtime_db_ports"},{"category_key":"instance_settings","actionType":"list_edit","target":"approved_instance_operations"}]}'::jsonb,
        '{"type":"credential_management_json_template_shape","jsonTemplateShape":{"template_key":"instance_settings_public_safe_template","category_key":"instance_settings","forbidden_fields":["secret","plaintext_secret","token","access_token","refresh_token","encrypted_payload","connection_string","endpoint_real_value","raw_sql","private_key","runtime_only_decrypted_payload","approval_bypass_authority"]}}'::jsonb,
        '{"type":"credential_management_json_template_section","template_key":"instance_settings_public_safe_template","section":"db_instance_port","db_instance_port":[{"instance_authority_key":"db-instance-reference-key","provider_kind":"data-label-only","reference_key":"runtime-reference-key-only","policy_template_key":"instance_connection_policy_template","status":"draft"}]}'::jsonb,
        '{"type":"credential_management_json_template_section","template_key":"instance_settings_public_safe_template","section":"runtime_instance_port","runtime_instance_port":[{"instance_authority_key":"runtime-instance-reference-key","provider_kind":"data-label-only","reference_key":"runtime-reference-key-only","policy_template_key":"instance_connection_policy_template","status":"draft"}]}'::jsonb,
        '{"type":"credential_management_json_template_section","template_key":"instance_settings_public_safe_template","section":"instance_connection_policy","instance_connection_policy":[{"policy_key":"instance_connection_policy_template","instance_authority_key":"db-or-runtime-instance-reference-key","allowed_operation_scope":"approved_operation_binding_only","approval_required":true,"timeout_policy_key":"bounded-timeout-policy"}]}'::jsonb,
        '{"type":"credential_management_json_template_section","template_key":"instance_settings_public_safe_template","section":"instance_operation_authority_binding","instance_operation_authority_binding":[{"operation_binding_key":"approved-operation-placeholder","instance_authority_key":"db-or-runtime-instance-reference-key","operation_key":"operation-reference-only","approval_status":"approved","event_action_type":"dispatchInstanceOperation","instanceTargetRef":"instance-port:db_instance_port:public-safe-placeholder:approved-operation-placeholder"}]}'::jsonb,
        '{"type":"credential_management_initial_data_rows","initialDataRows":[{"values":{"category_key":"users","mode":"category","label":"Users"},"lineage":{"source":"seed_projection","bundle":"credential-management-instance-settings-topology"}},{"values":{"category_key":"external_api_credential","mode":"category","label":"External API credential"},"lineage":{"source":"seed_projection","bundle":"auth-external-credential-management-topology-projection"}},{"values":{"category_key":"instance_settings","mode":"category","label":"Instance settings"},"lineage":{"source":"seed_projection","bundle":"credential-management-instance-settings-topology"}},{"values":{"category_key":"external_api_credential","port_kind":"access_port","provider_kind":"template-selected","credential_kind":"external","reference_key":"runtime-reference-key-only","required_by_bundle":"bundle-record-context","consumer_bundle_binding":"not-connected-in-this-bundle","policy_template_key":"external_access_port_generic_http"},"lineage":{"source":"seed_projection","bundle":"auth-external-credential-management-topology-projection"}},{"values":{"category_key":"external_api_credential","port_kind":"response_port","provider_kind":"template-selected","credential_kind":"external","reference_key":"runtime-reference-key-only","required_by_bundle":"bundle-record-context","consumer_bundle_binding":"not-connected-in-this-bundle","policy_template_key":"external_response_port_generic_http"},"lineage":{"source":"seed_projection","bundle":"auth-external-credential-management-topology-projection"}},{"values":{"category_key":"external_api_credential","port_kind":"hook_port","provider_kind":"template-selected","credential_kind":"external","reference_key":"runtime-reference-key-only","required_by_bundle":"bundle-record-context","consumer_bundle_binding":"not-connected-in-this-bundle","policy_template_key":"external_hook_port_scheduler_boundary"},"lineage":{"source":"seed_projection","bundle":"auth-external-credential-management-topology-projection"}},{"values":{"category_key":"instance_settings","template_key":"instance_settings_public_safe_template","public_safe_shape_only":true,"validate_preview_apply_required":true},"lineage":{"source":"seed_projection","bundle":"credential-management-instance-settings-topology"}}]}'::jsonb,
        -- instance_settings UI-entity payload lives in topology.ui_component_package /
        -- topology.components_layout_design / topology.ui_wiring_registry /
        -- topology.ui_topology_tensor (see the instance_settings category UI persistence
        -- block below this manifest INSERT) per
        -- docs/design/react-schema-topology-seed-translator-ssot.yaml
        -- storage_adoption_contract.adoption_candidate_separation_contract -- manifest.topology
        -- holds only this refs-only ui_projection entry (packageIds/layoutId/wiringId/tensorId).
        -- Regenerable via .agent/tools/react-schema-topology-seed-translator generate-react-schema
        --   --input .agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json
        --   | generate-topology-seed --input .../credential-management-0092.topology-seed.input.json
        -- Resolves declared_seed_surface_catalog known_gap
        -- instance_settings_projection_category_not_yet_represented.
        '{"type":"ui_projection","packageIds":["00000000-0000-0000-0000-0000000cd005"],"layoutId":"00000000-0000-0000-0000-0000000cd002","wiringId":"00000000-0000-0000-0000-0000000cd003","tensorId":"00000000-0000-0000-0000-0000000cd004"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

-- ---------------------------------------------------------------------------
-- instance_settings category UI persistence (package/layout/wiring/tensor)
-- SSOT: docs/design/react-schema-topology-seed-translator-ssot.yaml storage_adoption_contract.adoption_candidate_separation_contract
-- Regenerable via: .agent/tools/react-schema-topology-seed-translator generate-react-schema
--   --input .agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json
--   | generate-topology-seed --input .../credential-management-0092.topology-seed.input.json
-- manifest 092 (below) holds only a refs-only ui_projection entry pointing at these rows.
-- ---------------------------------------------------------------------------
INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000cd001',
    'auth.external.credential_management.projection.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey":"auth.external.credential_management.projection","surface":"auth.external.credential_management.projection","categoryKeys":["users","external_api_credential","instance_settings"],"sectionKeys":["credential_search_section"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

-- topology.ui_component_package (above) is a distinct "component group
-- bundle" identity required only by topology.ui_topology_tensor.package_id's
-- FK constraint. It is NOT the manifest-facing package authority -- per
-- docs/design/db-schema.yaml packages/components_package_design.manifest_reference
-- (manifest.topology[ui_projection].packageIds), that role belongs to
-- topology.components_package_design below. This surface authored no
-- component+design pairs via UI Component Builder (fixed_form_projection,
-- ui_builder_authority:false), so layout is honestly empty rather than
-- inventing componentId/designId pairs that were never authored.
INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000cd005',
    'auth.external.credential_management.projection.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000cd002',
    'auth.external.credential_management.projection.layout',
    'fixed_form_projection',
    '{"records":[{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"auth_external_credential_management_projection","record":{"recordType":"topology_ui_category","key":"users","label":"ユーザー","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.user_auth"],"sourceReactPath":"$.root.children[0]","knownGapRefs":[],"categoryKey":"users","visibilityBinding":{"source":"ui-local:credential_category_filter.selectedCategory","matchValue":"users"},"sectionKeys":["user_auth_section","credentials_users_account_section"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"users","record":{"recordType":"topology_ui_section","key":"user_auth_section","label":"ユーザー認証境界","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.user_auth"],"sourceReactPath":"$.root.children[0].children[0]","knownGapRefs":[],"sectionKey":"user_auth_section","sectionKind":"readonly_boundary","childKeys":["approval_status"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"user_auth_section","record":{"recordType":"topology_ui_field","key":"approval_status","label":"承認ステータス","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[0].children[0].children[0]","knownGapRefs":[],"fieldKey":"approval_status","control":"form_input/select","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"users","record":{"recordType":"topology_ui_section","key":"credentials_users_account_section","label":"ユーザーアカウントライフサイクル(作成 / 更新 / 削除 / 失効)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1]","knownGapRefs":[],"sectionKey":"credentials_users_account_section","sectionKind":"credentials_users_account_lifecycle_projection","childKeys":["credentials_users_username_input","credentials_users_password_input","credentials_users_approve_input","credentials_users_status_input","credentials_users_role_name_input","credentials_users_state_note_input","credentials_users_suspended_from_input","credentials_users_suspended_until_input","credentials_users_active_input","credentials_users_user_id_input","credentials_users_session_id_input","credentials_users_create_button","credentials_users_create_confirm_modal","credentials_users_update_button","credentials_users_update_confirm_modal","credentials_users_delete_button","credentials_users_delete_confirm_modal","credentials_users_revoke_credential_button","credentials_users_revoke_credential_confirm_modal","credentials_users_revoke_sessions_button","credentials_users_revoke_sessions_confirm_modal"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_username_input","label":"ユーザー名","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[0]","knownGapRefs":[],"fieldKey":"credentials_users_username_input","control":"form_input/form_field","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_password_input","label":"初期パスワード(作成時のみ。管理者によるパスワード置換/ローテーションは提供しない)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[1]","knownGapRefs":[],"fieldKey":"credentials_users_password_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_approve_input","label":"承認","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[2]","knownGapRefs":[],"fieldKey":"credentials_users_approve_input","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_status_input","label":"ステータス","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[3]","knownGapRefs":[],"fieldKey":"credentials_users_status_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_role_name_input","label":"ロール(admin / user)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[4]","knownGapRefs":[],"fieldKey":"credentials_users_role_name_input","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_state_note_input","label":"状態メモ","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[5]","knownGapRefs":[],"fieldKey":"credentials_users_state_note_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_suspended_from_input","label":"停止開始日時","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[6]","knownGapRefs":[],"fieldKey":"credentials_users_suspended_from_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_suspended_until_input","label":"停止終了日時","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[7]","knownGapRefs":[],"fieldKey":"credentials_users_suspended_until_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_active_input","label":"有効","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[8]","knownGapRefs":[],"fieldKey":"credentials_users_active_input","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_user_id_input","label":"ユーザーID(更新 / 削除 / 失効に必須)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[9]","knownGapRefs":[],"fieldKey":"credentials_users_user_id_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_field","key":"credentials_users_session_id_input","label":"セッションID(空欄で全セッション失効)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[10]","knownGapRefs":[],"fieldKey":"credentials_users_session_id_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_action","key":"credentials_users_create_button","label":"ユーザーアカウントを作成(初期クレデンシャル込み)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[11]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"credentials_users_create_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create","authority":"preview_only","payloadFrom":{"username":"node:credentials_users_username_input.value","password":"node:credentials_users_password_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_create_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_create_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create","payloadFrom":{"username":"node:credentials_users_username_input.value","password":"node:credentials_users_password_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","dryRun":"literal:true"},"sourceActionKey":"credentials_users_create_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_modal","key":"credentials_users_create_confirm_modal","label":"ユーザーアカウント作成の確認","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[0].children[1].children[12]","knownGapRefs":[],"modalKey":"credentials_users_create_confirm_modal","componentKind":"disclosure/modal","title":"ユーザーアカウントを作成","body":"指定したユーザー名・初期パスワード・ロール等でアカウントと初期クレデンシャルを作成します。","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_create_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_create_confirm_modal"}],"childKeys":["credentials_users_create_confirm_button","credentials_users_create_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_create_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_create_confirm_button","label":"作成を確定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[12].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"credentials_users_create_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create","authority":"draft_apply_not_execution_authority","payloadFrom":{"username":"node:credentials_users_username_input.value","password":"node:credentials_users_password_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","confirmed":"literal:true"}},"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create","payloadFrom":{"username":"node:credentials_users_username_input.value","password":"node:credentials_users_password_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","confirmed":"literal:true"},"sourceActionKey":"credentials_users_create_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_create_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_create_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[12].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"credentials_users_create_cancel_button","actionRef":"ui-local:credentials_users_create_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_create_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_create_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_create_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_create_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_action","key":"credentials_users_update_button","label":"ユーザーアカウントのメタデータを更新","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[13]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"credentials_users_update_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update","authority":"preview_only","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","username":"node:credentials_users_username_input.value","active":"node:credentials_users_active_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_update_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_update_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","username":"node:credentials_users_username_input.value","active":"node:credentials_users_active_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","dryRun":"literal:true"},"sourceActionKey":"credentials_users_update_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_modal","key":"credentials_users_update_confirm_modal","label":"ユーザーアカウント更新の確認","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[0].children[1].children[14]","knownGapRefs":[],"modalKey":"credentials_users_update_confirm_modal","componentKind":"disclosure/modal","title":"ユーザーアカウントを更新","body":"指定したユーザーIDのアカウントメタデータ(ステータス・ロール・状態メモ等)を更新します。","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_update_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_update_confirm_modal"}],"childKeys":["credentials_users_update_confirm_button","credentials_users_update_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_update_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_update_confirm_button","label":"更新を確定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[14].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"credentials_users_update_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update","authority":"draft_apply_not_execution_authority","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","username":"node:credentials_users_username_input.value","active":"node:credentials_users_active_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","confirmed":"literal:true"}},"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","username":"node:credentials_users_username_input.value","active":"node:credentials_users_active_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","confirmed":"literal:true"},"sourceActionKey":"credentials_users_update_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_update_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_update_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[14].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"credentials_users_update_cancel_button","actionRef":"ui-local:credentials_users_update_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_update_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_update_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_update_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_update_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_action","key":"credentials_users_delete_button","label":"アカウントを削除(クレデンシャル込み)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[15]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"credentials_users_delete_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete","authority":"preview_only","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_delete_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_delete_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","dryRun":"literal:true"},"sourceActionKey":"credentials_users_delete_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_modal","key":"credentials_users_delete_confirm_modal","label":"アカウント削除の確認","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[0].children[1].children[16]","knownGapRefs":[],"modalKey":"credentials_users_delete_confirm_modal","componentKind":"disclosure/modal","title":"アカウントを削除","body":"指定したユーザーIDのアカウントと紐づくクレデンシャルを削除します。この操作は取り消せません。","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_delete_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_delete_confirm_modal"}],"childKeys":["credentials_users_delete_confirm_button","credentials_users_delete_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_delete_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_delete_confirm_button","label":"削除を確定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[16].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"credentials_users_delete_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete","authority":"draft_apply_not_execution_authority","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","confirmed":"literal:true"}},"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","confirmed":"literal:true"},"sourceActionKey":"credentials_users_delete_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_delete_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_delete_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[16].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"credentials_users_delete_cancel_button","actionRef":"ui-local:credentials_users_delete_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_delete_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_delete_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_delete_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_delete_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_action","key":"credentials_users_revoke_credential_button","label":"クレデンシャルを失効(確認を開く)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[17]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"credentials_users_revoke_credential_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential","authority":"preview_only","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_revoke_credential_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_credential_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","dryRun":"literal:true"},"sourceActionKey":"credentials_users_revoke_credential_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_modal","key":"credentials_users_revoke_credential_confirm_modal","label":"クレデンシャル失効の確認","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[18]","knownGapRefs":[],"modalKey":"credentials_users_revoke_credential_confirm_modal","componentKind":"disclosure/modal","title":"クレデンシャルを失効","body":"指定したユーザーIDのクレデンシャル行を削除し、すべてのアクティブセッションを失効させます。","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_revoke_credential_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_credential_confirm_modal"}],"childKeys":["credentials_users_revoke_credential_confirm_button","credentials_users_revoke_credential_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_revoke_credential_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_revoke_credential_confirm_button","label":"クレデンシャル失効を確定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[18].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"credentials_users_revoke_credential_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential","authority":"draft_apply_not_execution_authority","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","confirmed":"literal:true"}},"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","confirmed":"literal:true"},"sourceActionKey":"credentials_users_revoke_credential_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_revoke_credential_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_revoke_credential_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[18].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"credentials_users_revoke_credential_cancel_button","actionRef":"ui-local:credentials_users_revoke_credential_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_revoke_credential_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_revoke_credential_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_revoke_credential_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_credential_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_action","key":"credentials_users_revoke_sessions_button","label":"セッションを失効(確認を開く)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[19]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"credentials_users_revoke_sessions_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions","authority":"preview_only","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","sessionId":"node:credentials_users_session_id_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_revoke_sessions_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_sessions_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","sessionId":"node:credentials_users_session_id_input.value","dryRun":"literal:true"},"sourceActionKey":"credentials_users_revoke_sessions_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_account_section","record":{"recordType":"topology_ui_modal","key":"credentials_users_revoke_sessions_confirm_modal","label":"セッション失効の確認","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[20]","knownGapRefs":[],"modalKey":"credentials_users_revoke_sessions_confirm_modal","componentKind":"disclosure/modal","title":"セッションを失効","body":"指定したセッションID(空欄の場合は全て)のアクティブセッションを失効させます。","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_revoke_sessions_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_sessions_confirm_modal"}],"childKeys":["credentials_users_revoke_sessions_confirm_button","credentials_users_revoke_sessions_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_revoke_sessions_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_revoke_sessions_confirm_button","label":"セッション失効を確定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[20].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"credentials_users_revoke_sessions_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions","authority":"draft_apply_not_execution_authority","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","sessionId":"node:credentials_users_session_id_input.value","confirmed":"literal:true"}},"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","sessionId":"node:credentials_users_session_id_input.value","confirmed":"literal:true"},"sourceActionKey":"credentials_users_revoke_sessions_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credentials_users_revoke_sessions_confirm_modal","record":{"recordType":"topology_ui_action","key":"credentials_users_revoke_sessions_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.users"],"sourceReactPath":"$.root.children[0].children[1].children[20].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"credentials_users_revoke_sessions_cancel_button","actionRef":"ui-local:credentials_users_revoke_sessions_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_revoke_sessions_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_revoke_sessions_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_revoke_sessions_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_sessions_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"auth_external_credential_management_projection","record":{"recordType":"topology_ui_category","key":"external_api_credential","label":"外部APIクレデンシャル","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.external"],"sourceReactPath":"$.root.children[1]","knownGapRefs":[],"categoryKey":"external_api_credential","visibilityBinding":{"source":"ui-local:credential_category_filter.selectedCategory","matchValue":"external_api_credential"},"sectionKeys":["external_section","scheduler_credential_binding_section","external_api_credential_crud_section"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential","record":{"recordType":"topology_ui_section","key":"external_section","label":"外部ポートクレデンシャルコンテキスト","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.external"],"sourceReactPath":"$.root.children[1].children[0]","knownGapRefs":[],"sectionKey":"external_section","sectionKind":"readonly_boundary","childKeys":["credential_reference_key"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_section","record":{"recordType":"topology_ui_field","key":"credential_reference_key","label":"クレデンシャル参照キー","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.external"],"sourceReactPath":"$.root.children[1].children[0].children[0]","knownGapRefs":[],"fieldKey":"credential_reference_key","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential","record":{"recordType":"topology_ui_section","key":"scheduler_credential_binding_section","label":"スケジューラージョブのクレデンシャル/ポートバインディング","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding"],"sourceReactPath":"$.root.children[1].children[1]","knownGapRefs":[],"sectionKey":"scheduler_credential_binding_section","sectionKind":"consumer_reference_binding_projection","childKeys":["scheduler_job_id_input","scheduler_credential_requirement_ref_input","scheduler_external_port_ref_input","configure_scheduler_job_credential_or_port_binding_button","configure_scheduler_job_credential_or_port_binding_confirm_modal"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"scheduler_credential_binding_section","record":{"recordType":"topology_ui_field","key":"scheduler_job_id_input","label":"スケジューラージョブID","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding"],"sourceReactPath":"$.root.children[1].children[1].children[0]","knownGapRefs":[],"fieldKey":"scheduler_job_id_input","control":"form_input/form_field","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"scheduler_credential_binding_section","record":{"recordType":"topology_ui_field","key":"scheduler_credential_requirement_ref_input","label":"クレデンシャル要件参照","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding"],"sourceReactPath":"$.root.children[1].children[1].children[1]","knownGapRefs":[],"fieldKey":"scheduler_credential_requirement_ref_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"scheduler_credential_binding_section","record":{"recordType":"topology_ui_field","key":"scheduler_external_port_ref_input","label":"外部ポート参照","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding"],"sourceReactPath":"$.root.children[1].children[1].children[2]","knownGapRefs":[],"fieldKey":"scheduler_external_port_ref_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"scheduler_credential_binding_section","record":{"recordType":"topology_ui_action","key":"configure_scheduler_job_credential_or_port_binding_button","label":"スケジューラージョブのクレデンシャル/ポートバインディングを設定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding"],"sourceReactPath":"$.root.children[1].children[1].children[3]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"configure_scheduler_job_credential_or_port_binding_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding","authority":"preview_only","payloadFrom":{"schedulerJobId":"node:scheduler_job_id_input.value","credentialRequirementRef":"node:scheduler_credential_requirement_ref_input.value","externalPortRef":"node:scheduler_external_port_ref_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","statePath":"open","sourceActionKey":"configure_scheduler_job_credential_or_port_binding_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding","payloadFrom":{"schedulerJobId":"node:scheduler_job_id_input.value","credentialRequirementRef":"node:scheduler_credential_requirement_ref_input.value","externalPortRef":"node:scheduler_external_port_ref_input.value","dryRun":"literal:true"},"sourceActionKey":"configure_scheduler_job_credential_or_port_binding_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"scheduler_credential_binding_section","record":{"recordType":"topology_ui_modal","key":"configure_scheduler_job_credential_or_port_binding_confirm_modal","label":"クレデンシャル/ポートバインディング設定の確認ダイアログ","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding"],"sourceReactPath":"$.root.children[1].children[1].children[4]","knownGapRefs":[],"modalKey":"configure_scheduler_job_credential_or_port_binding_confirm_modal","componentKind":"disclosure/modal","title":"スケジューラージョブのクレデンシャル/ポートバインディングを設定","body":"Bind the entered credential/port reference to the scheduler job. Writes only credential_requirement_ref/external_port_ref; never the job body/step-chain fields it owns.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","statePath":"open","sourceActionKey":"configure_scheduler_job_credential_or_port_binding_confirm_modal"}],"childKeys":["configure_scheduler_job_credential_or_port_binding_confirm_button","configure_scheduler_job_credential_or_port_binding_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"configure_scheduler_job_credential_or_port_binding_confirm_modal","record":{"recordType":"topology_ui_action","key":"configure_scheduler_job_credential_or_port_binding_confirm_button","label":"設定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding"],"sourceReactPath":"$.root.children[1].children[1].children[4].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"configure_scheduler_job_credential_or_port_binding_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding","authority":"draft_apply_not_execution_authority","payloadFrom":{"schedulerJobId":"node:scheduler_job_id_input.value","credentialRequirementRef":"node:scheduler_credential_requirement_ref_input.value","externalPortRef":"node:scheduler_external_port_ref_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","statePath":"open","sourceActionKey":"configure_scheduler_job_credential_or_port_binding_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding","payloadFrom":{"schedulerJobId":"node:scheduler_job_id_input.value","credentialRequirementRef":"node:scheduler_credential_requirement_ref_input.value","externalPortRef":"node:scheduler_external_port_ref_input.value","confirmed":"literal:true"},"sourceActionKey":"configure_scheduler_job_credential_or_port_binding_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"configure_scheduler_job_credential_or_port_binding_confirm_modal","record":{"recordType":"topology_ui_action","key":"configure_scheduler_job_credential_or_port_binding_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding"],"sourceReactPath":"$.root.children[1].children[1].children[4].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"configure_scheduler_job_credential_or_port_binding_cancel_button","actionRef":"ui-local:configure_scheduler_job_credential_or_port_binding_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:configure_scheduler_job_credential_or_port_binding_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","statePath":"open","sourceActionKey":"configure_scheduler_job_credential_or_port_binding_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential","record":{"recordType":"topology_ui_section","key":"external_api_credential_crud_section","label":"外部APIクレデンシャルレコード","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2]","knownGapRefs":[],"sectionKey":"external_api_credential_crud_section","sectionKind":"external_api_credential_base_crud_projection","childKeys":["external_api_credential_form_record_kind_input","external_api_credential_form_record_id_input","external_api_credential_form_provider_kind_input","external_api_credential_form_required_by_bundle_input","external_api_credential_form_reference_key_input","external_api_credential_form_active_input","external_api_credential_form_token_kind_input","external_api_credential_form_refresh_before_seconds_input","external_api_credential_form_url_or_env_reference_input","external_api_credential_form_credential_kind_input","external_api_credential_form_hook_path_input","external_api_credential_form_header_key_input","external_api_credential_form_route_key_input","external_api_credential_form_secret_input","external_api_credential_form_encryption_key_reference_input","external_api_credential_create_button","external_api_credential_create_confirm_modal","external_api_credential_update_button","external_api_credential_update_confirm_modal","external_api_credential_delete_button","external_api_credential_delete_confirm_modal"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_record_kind_input","label":"レコード種別","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[0]","knownGapRefs":[],"fieldKey":"external_api_credential_form_record_kind_input","control":"form_input/select","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_record_id_input","label":"レコードID(新規作成時は空欄)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[1]","knownGapRefs":[],"fieldKey":"external_api_credential_form_record_id_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_provider_kind_input","label":"プロバイダー種別","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[2]","knownGapRefs":[],"fieldKey":"external_api_credential_form_provider_kind_input","control":"form_input/form_field","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_required_by_bundle_input","label":"必要とするバンドル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[3]","knownGapRefs":[],"fieldKey":"external_api_credential_form_required_by_bundle_input","control":"form_input/form_field","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_reference_key_input","label":"参照キー","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[4]","knownGapRefs":[],"fieldKey":"external_api_credential_form_reference_key_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_active_input","label":"有効","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[5]","knownGapRefs":[],"fieldKey":"external_api_credential_form_active_input","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_token_kind_input","label":"トークン種別","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[6]","knownGapRefs":[],"fieldKey":"external_api_credential_form_token_kind_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_refresh_before_seconds_input","label":"リフレッシュ猶予(秒)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[7]","knownGapRefs":[],"fieldKey":"external_api_credential_form_refresh_before_seconds_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_url_or_env_reference_input","label":"URLまたは環境変数参照","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[8]","knownGapRefs":[],"fieldKey":"external_api_credential_form_url_or_env_reference_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_credential_kind_input","label":"クレデンシャル種別","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[9]","knownGapRefs":[],"fieldKey":"external_api_credential_form_credential_kind_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_hook_path_input","label":"フックパス","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[10]","knownGapRefs":[],"fieldKey":"external_api_credential_form_hook_path_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_header_key_input","label":"ヘッダーキー","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[11]","knownGapRefs":[],"fieldKey":"external_api_credential_form_header_key_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_route_key_input","label":"ルートキー","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[12]","knownGapRefs":[],"fieldKey":"external_api_credential_form_route_key_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_secret_input","label":"シークレット(書き込み専用。作成時はplaintextSecret、更新時はnewPlaintextSecret。表示には決して戻さない)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[13]","knownGapRefs":[],"fieldKey":"external_api_credential_form_secret_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_field","key":"external_api_credential_form_encryption_key_reference_input","label":"暗号鍵参照(env:ENV_VAR_NAME)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[14]","knownGapRefs":[],"fieldKey":"external_api_credential_form_encryption_key_reference_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_action","key":"external_api_credential_create_button","label":"外部APIクレデンシャルレコードを作成","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[15]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"external_api_credential_create_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create","authority":"preview_only","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","plaintextSecret":"node:external_api_credential_form_secret_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"external_api_credential_create_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_create_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","plaintextSecret":"node:external_api_credential_form_secret_input.value","dryRun":"literal:true"},"sourceActionKey":"external_api_credential_create_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_modal","key":"external_api_credential_create_confirm_modal","label":"外部APIクレデンシャルレコード作成の確認ダイアログ","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[16]","knownGapRefs":[],"modalKey":"external_api_credential_create_confirm_modal","componentKind":"disclosure/modal","title":"外部APIクレデンシャルレコードを作成","body":"Create a new record for the selected recordKind. Only allowed metadata fields plus a write-only secret input are sent; the secret is never echoed back.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"external_api_credential_create_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_create_confirm_modal"}],"childKeys":["external_api_credential_create_confirm_button","external_api_credential_create_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_create_confirm_modal","record":{"recordType":"topology_ui_action","key":"external_api_credential_create_confirm_button","label":"作成","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[16].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"external_api_credential_create_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","plaintextSecret":"node:external_api_credential_form_secret_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_create_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_create_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","plaintextSecret":"node:external_api_credential_form_secret_input.value","confirmed":"literal:true"},"sourceActionKey":"external_api_credential_create_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_create_confirm_modal","record":{"recordType":"topology_ui_action","key":"external_api_credential_create_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[16].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"external_api_credential_create_cancel_button","actionRef":"ui-local:external_api_credential_create_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:external_api_credential_create_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"external_api_credential_create_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_create_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_create_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_action","key":"external_api_credential_update_button","label":"外部APIクレデンシャルレコードを更新","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[17]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"external_api_credential_update_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update","authority":"preview_only","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","recordId":"node:external_api_credential_form_record_id_input.value","active":"node:external_api_credential_form_active_input.value","newPlaintextSecret":"node:external_api_credential_form_secret_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"external_api_credential_update_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_update_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","recordId":"node:external_api_credential_form_record_id_input.value","active":"node:external_api_credential_form_active_input.value","newPlaintextSecret":"node:external_api_credential_form_secret_input.value","dryRun":"literal:true"},"sourceActionKey":"external_api_credential_update_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_modal","key":"external_api_credential_update_confirm_modal","label":"外部APIクレデンシャルレコード更新の確認ダイアログ","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[18]","knownGapRefs":[],"modalKey":"external_api_credential_update_confirm_modal","componentKind":"disclosure/modal","title":"外部APIクレデンシャルレコードを更新","body":"Update the identified record''s metadata, and optionally rotate its secret via a write-only input. Never previews or echoes secret material.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"external_api_credential_update_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_update_confirm_modal"}],"childKeys":["external_api_credential_update_confirm_button","external_api_credential_update_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_update_confirm_modal","record":{"recordType":"topology_ui_action","key":"external_api_credential_update_confirm_button","label":"更新","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[18].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"external_api_credential_update_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","recordId":"node:external_api_credential_form_record_id_input.value","active":"node:external_api_credential_form_active_input.value","newPlaintextSecret":"node:external_api_credential_form_secret_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_update_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_update_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","recordId":"node:external_api_credential_form_record_id_input.value","active":"node:external_api_credential_form_active_input.value","newPlaintextSecret":"node:external_api_credential_form_secret_input.value","confirmed":"literal:true"},"sourceActionKey":"external_api_credential_update_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_update_confirm_modal","record":{"recordType":"topology_ui_action","key":"external_api_credential_update_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[18].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"external_api_credential_update_cancel_button","actionRef":"ui-local:external_api_credential_update_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:external_api_credential_update_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"external_api_credential_update_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_update_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_update_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_action","key":"external_api_credential_delete_button","label":"外部APIクレデンシャルレコードを無効化","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[19]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"external_api_credential_delete_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete","authority":"preview_only","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","recordId":"node:external_api_credential_form_record_id_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"external_api_credential_delete_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_delete_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","recordId":"node:external_api_credential_form_record_id_input.value","dryRun":"literal:true"},"sourceActionKey":"external_api_credential_delete_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_crud_section","record":{"recordType":"topology_ui_modal","key":"external_api_credential_delete_confirm_modal","label":"外部APIクレデンシャルレコード無効化の確認ダイアログ","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[20]","knownGapRefs":[],"modalKey":"external_api_credential_delete_confirm_modal","componentKind":"disclosure/modal","title":"外部APIクレデンシャルレコードを無効化","body":"Deactivate (soft-delete) the identified record by recordKind/recordId. This never removes the row or exposes secret material.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"external_api_credential_delete_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_delete_confirm_modal"}],"childKeys":["external_api_credential_delete_confirm_button","external_api_credential_delete_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_delete_confirm_modal","record":{"recordType":"topology_ui_action","key":"external_api_credential_delete_confirm_button","label":"無効化","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[20].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"external_api_credential_delete_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","recordId":"node:external_api_credential_form_record_id_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_delete_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_delete_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","recordId":"node:external_api_credential_form_record_id_input.value","confirmed":"literal:true"},"sourceActionKey":"external_api_credential_delete_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_api_credential_delete_confirm_modal","record":{"recordType":"topology_ui_action","key":"external_api_credential_delete_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_api_credential"],"sourceReactPath":"$.root.children[1].children[2].children[20].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"external_api_credential_delete_cancel_button","actionRef":"ui-local:external_api_credential_delete_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:external_api_credential_delete_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"external_api_credential_delete_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_delete_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_delete_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"auth_external_credential_management_projection","record":{"recordType":"topology_ui_category","key":"instance_settings","label":"インスタンス設定","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2]","knownGapRefs":[],"categoryKey":"instance_settings","visibilityBinding":{"source":"ui-local:credential_category_filter.selectedCategory","matchValue":"external_instance_credential"},"sectionKeys":["instance_settings_section","external_instance_credential_crud_section"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings","record":{"recordType":"topology_ui_section","key":"instance_settings_section","label":"インスタンス設定","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0]","knownGapRefs":[],"sectionKey":"instance_settings_section","sectionKind":"fixed_form_projection","childKeys":["instance_settings_import_form","instance_address_form","instance_operation_binding_form","instance_operation_approval_form","apply_confirm_dialog","approve_operation_candidate_confirm_dialog"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings_section","record":{"recordType":"topology_ui_form","key":"instance_settings_import_form","label":"インスタンス設定JSONインポート","sourceYamlRefs":["instance-port-substrate-ssot.yaml#seed_aligned_json_template_boundary"],"sourceReactPath":"$.root.children[2].children[0].children[0]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","formKey":"instance_settings_import_form","target":"instance_settings_json_template","mode":"import","fieldKeys":["template_file"],"actionKeys":["json_template_download","json_import"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings_import_form","record":{"recordType":"topology_ui_field","key":"template_file","label":"テンプレートファイル","sourceYamlRefs":["instance-port-substrate-ssot.yaml#seed_aligned_json_template_boundary.template_kind"],"sourceReactPath":"$.root.children[2].children[0].children[0].children[0]","knownGapRefs":[],"fieldKey":"template_file","control":"form_input/form_field","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings_import_form","record":{"recordType":"topology_ui_action","key":"json_template_download","label":"JSONテンプレートをダウンロード","sourceYamlRefs":["instance-port-substrate-ssot.yaml#seed_aligned_json_template_boundary.import_download_rule"],"sourceReactPath":"$.root.children[2].children[0].children[0].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"json_template_download","actionRef":"ui-local:instance_settings_import_form.template_download_trigger","eventBinding":{"trigger":"click","wiringLane":"internal_instance_wiring","targetRef":"ui-local:instance_settings_import_form.template_download_trigger","authority":"draft_or_projection_only","payloadFrom":{"category_key":"literal:instance_settings"}},"runtimeInteractions":[{"trigger":"click","actionType":"localStateMutation","payloadFrom":{"category_key":"literal:instance_settings"},"sourceActionKey":"json_template_download","targetRef":"ui-local:instance_settings_import_form.template_download_trigger"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings_import_form","record":{"recordType":"topology_ui_action","key":"json_import","label":"JSONテンプレートをインポート","sourceYamlRefs":["instance-port-substrate-ssot.yaml#seed_aligned_json_template_boundary.import_download_rule"],"sourceReactPath":"$.root.children[2].children[0].children[0].children[2]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"json_import","actionRef":"ui-local:instance_settings_import_form.template_import_trigger","eventBinding":{"trigger":"click","wiringLane":"internal_instance_wiring","targetRef":"ui-local:instance_settings_import_form.template_import_trigger","authority":"draft_or_projection_only","payloadFrom":{"template_file":"node:template_file.value"}},"runtimeInteractions":[{"trigger":"click","actionType":"localStateMutation","payloadFrom":{"template_file":"node:template_file.value"},"sourceActionKey":"json_import","targetRef":"ui-local:instance_settings_import_form.template_import_trigger"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings_section","record":{"recordType":"topology_ui_form","key":"instance_address_form","label":"インスタンスアドレス","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[1]","knownGapRefs":[],"authorityMarker":"validation_only","formKey":"instance_address_form","target":"db_instance_port","mode":"edit","fieldKeys":["instance_authority_key","instance_display_name","port_kind","provider_kind","required_by_bundle","address_ref","connection_policy_key"],"actionKeys":["validate"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_address_form","record":{"recordType":"topology_ui_field","key":"instance_authority_key","label":"インスタンス権限キー","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[1].children[0]","knownGapRefs":[],"fieldKey":"instance_authority_key","control":"form_input/form_field","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_address_form","record":{"recordType":"topology_ui_field","key":"instance_display_name","label":"インスタンス表示名","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[1].children[1]","knownGapRefs":[],"fieldKey":"instance_display_name","control":"form_input/form_field","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_address_form","record":{"recordType":"topology_ui_field","key":"port_kind","label":"ポート種別","sourceYamlRefs":["instance-port-substrate-ssot.yaml#port_kinds"],"sourceReactPath":"$.root.children[2].children[0].children[1].children[2]","knownGapRefs":[],"fieldKey":"port_kind","control":"form_input/select","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_address_form","record":{"recordType":"topology_ui_field","key":"provider_kind","label":"プロバイダー種別","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[1].children[3]","knownGapRefs":[],"fieldKey":"provider_kind","control":"form_input/form_field","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_address_form","record":{"recordType":"topology_ui_field","key":"required_by_bundle","label":"必要とするバンドル","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[1].children[4]","knownGapRefs":[],"fieldKey":"required_by_bundle","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_address_form","record":{"recordType":"topology_ui_field","key":"address_ref","label":"アドレス参照","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[1].children[5]","knownGapRefs":[],"fieldKey":"address_ref","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_address_form","record":{"recordType":"topology_ui_field","key":"connection_policy_key","label":"接続ポリシーキー","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[1].children[6]","knownGapRefs":[],"fieldKey":"connection_policy_key","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_address_form","record":{"recordType":"topology_ui_action","key":"validate","label":"検証","sourceYamlRefs":["instance-port-substrate-ssot.yaml#authority_model"],"sourceReactPath":"$.root.children[2].children[0].children[1].children[7]","knownGapRefs":[],"authorityMarker":"validation_only","actionKey":"validate","actionRef":"instance:db_instance_port:instance_authority_key:operation_binding_key","eventBinding":{"trigger":"click","wiringLane":"external_instance_wiring","targetRef":"instance:db_instance_port:instance_authority_key:operation_binding_key","authority":"validation_only","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"}},"runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"validate","instanceTargetRef":"instance-port:db_instance_port:instance_authority_key:operation_binding_key"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings_section","record":{"recordType":"topology_ui_form","key":"instance_operation_binding_form","label":"インスタンス操作バインディング","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[2]","knownGapRefs":[],"authorityMarker":"preview_only","formKey":"instance_operation_binding_form","target":"instance_operation_authority_binding","mode":"edit","fieldKeys":["operation_binding_key","display_label","operation_protocol","input_schema_key","output_shape_key","sanitize_policy_key","callable"],"actionKeys":["preview"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_binding_form","record":{"recordType":"topology_ui_field","key":"operation_binding_key","label":"操作バインディングキー","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[2].children[0]","knownGapRefs":[],"fieldKey":"operation_binding_key","control":"form_input/form_field","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_binding_form","record":{"recordType":"topology_ui_field","key":"display_label","label":"表示ラベル","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[2].children[1]","knownGapRefs":[],"fieldKey":"display_label","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_binding_form","record":{"recordType":"topology_ui_field","key":"operation_protocol","label":"操作プロトコル","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[2].children[2]","knownGapRefs":[],"fieldKey":"operation_protocol","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_binding_form","record":{"recordType":"topology_ui_field","key":"input_schema_key","label":"入力スキーマキー","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[2].children[3]","knownGapRefs":[],"fieldKey":"input_schema_key","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_binding_form","record":{"recordType":"topology_ui_field","key":"output_shape_key","label":"出力シェイプキー","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[2].children[4]","knownGapRefs":[],"fieldKey":"output_shape_key","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_binding_form","record":{"recordType":"topology_ui_field","key":"sanitize_policy_key","label":"サニタイズポリシーキー","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[2].children[5]","knownGapRefs":[],"fieldKey":"sanitize_policy_key","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_binding_form","record":{"recordType":"topology_ui_field","key":"callable","label":"呼び出し可能","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[2].children[6]","knownGapRefs":[],"fieldKey":"callable","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_binding_form","record":{"recordType":"topology_ui_action","key":"preview","label":"プレビュー","sourceYamlRefs":["instance-port-substrate-ssot.yaml#authority_model"],"sourceReactPath":"$.root.children[2].children[0].children[2].children[7]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"preview","actionRef":"instance:runtime_instance_port:instance_authority_key:operation_binding_key","eventBinding":{"trigger":"click","wiringLane":"external_instance_wiring","targetRef":"instance:runtime_instance_port:instance_authority_key:operation_binding_key","authority":"preview_only","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"}},"runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"preview","instanceTargetRef":"instance-port:runtime_instance_port:instance_authority_key:operation_binding_key"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings_section","record":{"recordType":"topology_ui_form","key":"instance_operation_approval_form","label":"インスタンス操作承認","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.guard_rule"],"sourceReactPath":"$.root.children[2].children[0].children[3]","knownGapRefs":[],"authorityMarker":"execution_candidate_authority_boundary","formKey":"instance_operation_approval_form","target":"instance_operation_authority_binding","mode":"approve","fieldKeys":["approval_status"],"actionKeys":["apply","approve"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_approval_form","record":{"recordType":"topology_ui_field","key":"approval_status","label":"承認ステータス","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.projection_categories.instance_settings"],"sourceReactPath":"$.root.children[2].children[0].children[3].children[0]","knownGapRefs":[],"fieldKey":"approval_status","control":"form_input/select","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_approval_form","record":{"recordType":"topology_ui_action","key":"apply","label":"適用","sourceYamlRefs":["instance-port-substrate-ssot.yaml#authority_model"],"sourceReactPath":"$.root.children[2].children[0].children[3].children[1]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"apply","actionRef":"instance:db_instance_port:instance_authority_key:operation_binding_key","eventBinding":{"trigger":"click","wiringLane":"external_instance_wiring","targetRef":"instance:db_instance_port:instance_authority_key:operation_binding_key","authority":"draft_apply_not_execution_authority","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"}},"runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"apply","instanceTargetRef":"instance-port:db_instance_port:instance_authority_key:operation_binding_key"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_operation_approval_form","record":{"recordType":"topology_ui_action","key":"approve","label":"承認","sourceYamlRefs":["instance-port-substrate-ssot.yaml#authority_model"],"sourceReactPath":"$.root.children[2].children[0].children[3].children[2]","knownGapRefs":[],"authorityMarker":"execution_candidate_authority_boundary","actionKey":"approve","actionRef":"instance:runtime_instance_port:instance_authority_key:operation_binding_key","eventBinding":{"trigger":"click","wiringLane":"external_instance_wiring","targetRef":"instance:runtime_instance_port:instance_authority_key:operation_binding_key","authority":"execution_candidate_authority_boundary","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"}},"runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"approve","instanceTargetRef":"instance-port:runtime_instance_port:instance_authority_key:operation_binding_key"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings_section","record":{"recordType":"topology_ui_validation","key":"apply_confirm_dialog","label":"適用の確認","sourceYamlRefs":["instance-port-substrate-ssot.yaml#authority_model"],"sourceReactPath":"$.root.children[2].children[0].children[4]","knownGapRefs":[],"validationKey":"apply_confirm_dialog","rule":"apply_creates_draft_metadata_only_runtime_execution_requires_admin_approval","severity":"warning"}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings_section","record":{"recordType":"topology_ui_validation","key":"approve_operation_candidate_confirm_dialog","label":"承認の確認","sourceYamlRefs":["instance-port-substrate-ssot.yaml#existing_credential_management_projection_extension.guard_rule"],"sourceReactPath":"$.root.children[2].children[0].children[5]","knownGapRefs":[],"validationKey":"approve_operation_candidate_confirm_dialog","rule":"approval_requires_prior_validate_and_preview_before_becoming_a_callable_operation","severity":"warning"}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"instance_settings","record":{"recordType":"topology_ui_section","key":"external_instance_credential_crud_section","label":"外部インスタンスクレデンシャルレコード(db/runtimeインスタンスポート)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1]","knownGapRefs":[],"sectionKey":"external_instance_credential_crud_section","sectionKind":"external_instance_credential_base_crud_projection","childKeys":["eic_record_kind_input","eic_record_id_input","eic_instance_authority_key_input","eic_provider_kind_input","eic_required_by_bundle_input","eic_reference_key_input","eic_active_input","eic_create_button","eic_create_confirm_modal","eic_update_button","eic_update_confirm_modal","eic_delete_button","eic_delete_confirm_modal"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_field","key":"eic_record_kind_input","label":"レコード種別(db_instance_port / runtime_instance_port)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[0]","knownGapRefs":[],"fieldKey":"eic_record_kind_input","control":"form_input/select","required":true,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_field","key":"eic_record_id_input","label":"レコードID(新規作成時は空欄)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[1]","knownGapRefs":[],"fieldKey":"eic_record_id_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_field","key":"eic_instance_authority_key_input","label":"インスタンス権限キー","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[2]","knownGapRefs":[],"fieldKey":"eic_instance_authority_key_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_field","key":"eic_provider_kind_input","label":"プロバイダー種別","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[3]","knownGapRefs":[],"fieldKey":"eic_provider_kind_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_field","key":"eic_required_by_bundle_input","label":"必要とするバンドル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[4]","knownGapRefs":[],"fieldKey":"eic_required_by_bundle_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_field","key":"eic_reference_key_input","label":"参照キー(既存のvaultクレデンシャルを参照する必要あり)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[5]","knownGapRefs":[],"fieldKey":"eic_reference_key_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_field","key":"eic_active_input","label":"有効","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[6]","knownGapRefs":[],"fieldKey":"eic_active_input","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_action","key":"eic_create_button","label":"外部インスタンスクレデンシャルレコードを作成","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[7]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"eic_create_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create","authority":"preview_only","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","instanceAuthorityKey":"node:eic_instance_authority_key_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"eic_create_confirm_modal","statePath":"open","sourceActionKey":"eic_create_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","instanceAuthorityKey":"node:eic_instance_authority_key_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","dryRun":"literal:true"},"sourceActionKey":"eic_create_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_modal","key":"eic_create_confirm_modal","label":"外部インスタンスクレデンシャルレコード作成の確認","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[8]","knownGapRefs":[],"modalKey":"eic_create_confirm_modal","componentKind":"disclosure/modal","title":"外部インスタンスクレデンシャルを作成","body":"選択したレコード種別に対して新しいレコードを作成します。許可されたメタデータ項目のみを送信します。","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"eic_create_confirm_modal","statePath":"open","sourceActionKey":"eic_create_confirm_modal"}],"childKeys":["eic_create_confirm_button","eic_create_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"eic_create_confirm_modal","record":{"recordType":"topology_ui_action","key":"eic_create_confirm_button","label":"作成を確定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[8].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"eic_create_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","instanceAuthorityKey":"node:eic_instance_authority_key_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","confirmed":"literal:true"}},"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","instanceAuthorityKey":"node:eic_instance_authority_key_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","confirmed":"literal:true"},"sourceActionKey":"eic_create_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"eic_create_confirm_modal","record":{"recordType":"topology_ui_action","key":"eic_create_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[8].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"eic_create_cancel_button","actionRef":"ui-local:eic_create_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:eic_create_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"eic_create_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"eic_create_confirm_modal","statePath":"open","sourceActionKey":"eic_create_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_action","key":"eic_update_button","label":"外部インスタンスクレデンシャルレコードを更新","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[9]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"eic_update_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update","authority":"preview_only","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","active":"node:eic_active_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"eic_update_confirm_modal","statePath":"open","sourceActionKey":"eic_update_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","active":"node:eic_active_input.value","dryRun":"literal:true"},"sourceActionKey":"eic_update_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_modal","key":"eic_update_confirm_modal","label":"外部インスタンスクレデンシャルレコード更新の確認","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[10]","knownGapRefs":[],"modalKey":"eic_update_confirm_modal","componentKind":"disclosure/modal","title":"外部インスタンスクレデンシャルを更新","body":"指定したレコードIDのメタデータを更新します。","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"eic_update_confirm_modal","statePath":"open","sourceActionKey":"eic_update_confirm_modal"}],"childKeys":["eic_update_confirm_button","eic_update_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"eic_update_confirm_modal","record":{"recordType":"topology_ui_action","key":"eic_update_confirm_button","label":"更新を確定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[10].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"eic_update_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","active":"node:eic_active_input.value","confirmed":"literal:true"}},"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","active":"node:eic_active_input.value","confirmed":"literal:true"},"sourceActionKey":"eic_update_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"eic_update_confirm_modal","record":{"recordType":"topology_ui_action","key":"eic_update_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[10].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"eic_update_cancel_button","actionRef":"ui-local:eic_update_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:eic_update_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"eic_update_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"eic_update_confirm_modal","statePath":"open","sourceActionKey":"eic_update_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_action","key":"eic_delete_button","label":"外部インスタンスクレデンシャルレコードを無効化","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[11]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"eic_delete_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete","authority":"preview_only","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"eic_delete_confirm_modal","statePath":"open","sourceActionKey":"eic_delete_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","dryRun":"literal:true"},"sourceActionKey":"eic_delete_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"external_instance_credential_crud_section","record":{"recordType":"topology_ui_modal","key":"eic_delete_confirm_modal","label":"外部インスタンスクレデンシャルレコード無効化の確認","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[12]","knownGapRefs":[],"modalKey":"eic_delete_confirm_modal","componentKind":"disclosure/modal","title":"外部インスタンスクレデンシャルを無効化","body":"指定したレコードIDを無効化します。","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"eic_delete_confirm_modal","statePath":"open","sourceActionKey":"eic_delete_confirm_modal"}],"childKeys":["eic_delete_confirm_button","eic_delete_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"eic_delete_confirm_modal","record":{"recordType":"topology_ui_action","key":"eic_delete_confirm_button","label":"無効化を確定","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[12].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"eic_delete_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","confirmed":"literal:true"}},"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","confirmed":"literal:true"},"sourceActionKey":"eic_delete_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"eic_delete_confirm_modal","record":{"recordType":"topology_ui_action","key":"eic_delete_cancel_button","label":"キャンセル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.categories.external_instance_credential"],"sourceReactPath":"$.root.children[2].children[1].children[12].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"eic_delete_cancel_button","actionRef":"ui-local:eic_delete_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:eic_delete_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"eic_delete_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"eic_delete_confirm_modal","statePath":"open","sourceActionKey":"eic_delete_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"auth_external_credential_management_projection","record":{"recordType":"topology_ui_section","key":"credential_search_section","label":"ユーザー / 外部APIクレデンシャル / 外部インスタンスクレデンシャルを横断検索","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3]","knownGapRefs":[],"sectionKey":"credential_search_section","sectionKind":"credential_management_unified_search_projection","childKeys":["credential_category_filter","credential_search_input","credential_filter_status_input","credential_filter_active_input","credential_filter_record_kind_input","credential_filter_provider_kind_input","credential_filter_required_by_bundle_input","credential_filter_expires_before_input","credential_filter_expires_after_input","credential_search_button","credential_result_list"]}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_field","key":"credential_category_filter","label":"カテゴリ (users / external_api_credential / external_instance_credential)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[0]","knownGapRefs":[],"fieldKey":"credential_category_filter","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_field","key":"credential_search_input","label":"検索(ユーザー名 / 参照キー / 非シークレットテキスト)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[1]","knownGapRefs":[],"fieldKey":"credential_search_input","control":"form_input/search_input","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_field","key":"credential_filter_status_input","label":"ステータス(usersカテゴリ)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[2]","knownGapRefs":[],"fieldKey":"credential_filter_status_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_field","key":"credential_filter_active_input","label":"有効","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[3]","knownGapRefs":[],"fieldKey":"credential_filter_active_input","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_field","key":"credential_filter_record_kind_input","label":"レコード種別(クレデンシャルカテゴリ)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[4]","knownGapRefs":[],"fieldKey":"credential_filter_record_kind_input","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_field","key":"credential_filter_provider_kind_input","label":"プロバイダー種別","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[5]","knownGapRefs":[],"fieldKey":"credential_filter_provider_kind_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_field","key":"credential_filter_required_by_bundle_input","label":"必要とするバンドル","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[6]","knownGapRefs":[],"fieldKey":"credential_filter_required_by_bundle_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_field","key":"credential_filter_expires_before_input","label":"有効期限(これより前)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[7]","knownGapRefs":[],"fieldKey":"credential_filter_expires_before_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_field","key":"credential_filter_expires_after_input","label":"有効期限(これより後)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[8]","knownGapRefs":[],"fieldKey":"credential_filter_expires_after_input","control":"form_input/form_field","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_action","key":"credential_search_button","label":"検索","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[9]","knownGapRefs":[],"authorityMarker":"read_only","actionKey":"credential_search_button","actionRef":"manifest:00000000-0000-0000-0000-000000000092:credential_management:search","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-000000000092:credential_management:search","authority":"read_only","payloadFrom":{"category":"node:credential_category_filter.value","query":"node:credential_search_input.value","status":"node:credential_filter_status_input.value","active":"node:credential_filter_active_input.value","recordKind":"node:credential_filter_record_kind_input.value","providerKind":"node:credential_filter_provider_kind_input.value","requiredByBundle":"node:credential_filter_required_by_bundle_input.value","expiresBefore":"node:credential_filter_expires_before_input.value","expiresAfter":"node:credential_filter_expires_after_input.value"}},"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-000000000092:credential_management:search","payloadFrom":{"category":"node:credential_category_filter.value","query":"node:credential_search_input.value","status":"node:credential_filter_status_input.value","active":"node:credential_filter_active_input.value","recordKind":"node:credential_filter_record_kind_input.value","providerKind":"node:credential_filter_provider_kind_input.value","requiredByBundle":"node:credential_filter_required_by_bundle_input.value","expiresBefore":"node:credential_filter_expires_before_input.value","expiresAfter":"node:credential_filter_expires_after_input.value"},"sourceActionKey":"credential_search_button"}}},{"type":"topology_ui_seed_record","seedKey":"auth.external.credential_management.projection","parentKey":"credential_search_section","record":{"recordType":"topology_ui_table","key":"credential_result_list","label":"クレデンシャル検索結果(カテゴリ別)","sourceYamlRefs":["docs/design/admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.credentials.seed_contract"],"sourceReactPath":"$.root.children[3].children[10]","knownGapRefs":[],"tableKey":"credential_result_list","source":"credential_management.search","display":"table","displayColumns":[{"key":"recordKind","header":"レコード種別"},{"key":"providerKind","header":"プロバイダー種別"},{"key":"requiredByBundle","header":"必要とするバンドル"},{"key":"referenceKey","header":"参照キー"},{"key":"active","header":"有効"},{"key":"expiresAt","header":"有効期限"},{"key":"updatedAt","header":"更新日時"}],"rowsSource":"emission.data.records","columnKeys":[]}}]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000cd003',
    'auth.external.credential_management.projection.wiring',
    'admin_runtime',
    'manifest',
    'auth.external.credential_management.projection',
    '{"actions":[{"wiringKey":"auth.external.credential_management.projection.credentials_users_create_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create","authority":"preview_only","payloadFrom":{"username":"node:credentials_users_username_input.value","password":"node:credentials_users_password_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","dryRun":"literal:true"}},"sourceActionKey":"credentials_users_create_button","authorityMarker":"preview_only"},"sourceRecordKey":"credentials_users_create_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_create_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create","authority":"draft_apply_not_execution_authority","payloadFrom":{"username":"node:credentials_users_username_input.value","password":"node:credentials_users_password_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","confirmed":"literal:true"}},"sourceActionKey":"credentials_users_create_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"credentials_users_create_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_create_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_create_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_create_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"credentials_users_create_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"credentials_users_create_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_update_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update","authority":"preview_only","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","username":"node:credentials_users_username_input.value","active":"node:credentials_users_active_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","dryRun":"literal:true"}},"sourceActionKey":"credentials_users_update_button","authorityMarker":"preview_only"},"sourceRecordKey":"credentials_users_update_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_update_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update","authority":"draft_apply_not_execution_authority","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","username":"node:credentials_users_username_input.value","active":"node:credentials_users_active_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","confirmed":"literal:true"}},"sourceActionKey":"credentials_users_update_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"credentials_users_update_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_update_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_update_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_update_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"credentials_users_update_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"credentials_users_update_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_delete_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete","authority":"preview_only","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","dryRun":"literal:true"}},"sourceActionKey":"credentials_users_delete_button","authorityMarker":"preview_only"},"sourceRecordKey":"credentials_users_delete_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_delete_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete","authority":"draft_apply_not_execution_authority","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","confirmed":"literal:true"}},"sourceActionKey":"credentials_users_delete_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"credentials_users_delete_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_delete_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_delete_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_delete_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"credentials_users_delete_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"credentials_users_delete_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_revoke_credential_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential","authority":"preview_only","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","dryRun":"literal:true"}},"sourceActionKey":"credentials_users_revoke_credential_button","authorityMarker":"preview_only"},"sourceRecordKey":"credentials_users_revoke_credential_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_revoke_credential_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential","authority":"draft_apply_not_execution_authority","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","confirmed":"literal:true"}},"sourceActionKey":"credentials_users_revoke_credential_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"credentials_users_revoke_credential_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_revoke_credential_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_revoke_credential_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_revoke_credential_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"credentials_users_revoke_credential_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"credentials_users_revoke_credential_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_revoke_sessions_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions","authority":"preview_only","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","sessionId":"node:credentials_users_session_id_input.value","dryRun":"literal:true"}},"sourceActionKey":"credentials_users_revoke_sessions_button","authorityMarker":"preview_only"},"sourceRecordKey":"credentials_users_revoke_sessions_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_revoke_sessions_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions","authority":"draft_apply_not_execution_authority","payloadFrom":{"userId":"node:credentials_users_user_id_input.value","sessionId":"node:credentials_users_session_id_input.value","confirmed":"literal:true"}},"sourceActionKey":"credentials_users_revoke_sessions_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"credentials_users_revoke_sessions_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.credentials_users_revoke_sessions_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:credentials_users_revoke_sessions_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"credentials_users_revoke_sessions_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"credentials_users_revoke_sessions_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"credentials_users_revoke_sessions_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.configure_scheduler_job_credential_or_port_binding_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding","authority":"preview_only","payloadFrom":{"schedulerJobId":"node:scheduler_job_id_input.value","credentialRequirementRef":"node:scheduler_credential_requirement_ref_input.value","externalPortRef":"node:scheduler_external_port_ref_input.value","dryRun":"literal:true"}},"sourceActionKey":"configure_scheduler_job_credential_or_port_binding_button","authorityMarker":"preview_only"},"sourceRecordKey":"configure_scheduler_job_credential_or_port_binding_button"},{"wiringKey":"auth.external.credential_management.projection.configure_scheduler_job_credential_or_port_binding_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding","authority":"draft_apply_not_execution_authority","payloadFrom":{"schedulerJobId":"node:scheduler_job_id_input.value","credentialRequirementRef":"node:scheduler_credential_requirement_ref_input.value","externalPortRef":"node:scheduler_external_port_ref_input.value","confirmed":"literal:true"}},"sourceActionKey":"configure_scheduler_job_credential_or_port_binding_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"configure_scheduler_job_credential_or_port_binding_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.configure_scheduler_job_credential_or_port_binding_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:configure_scheduler_job_credential_or_port_binding_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"configure_scheduler_job_credential_or_port_binding_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"configure_scheduler_job_credential_or_port_binding_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.external_api_credential_create_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create","authority":"preview_only","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","plaintextSecret":"node:external_api_credential_form_secret_input.value","dryRun":"literal:true"}},"sourceActionKey":"external_api_credential_create_button","authorityMarker":"preview_only"},"sourceRecordKey":"external_api_credential_create_button"},{"wiringKey":"auth.external.credential_management.projection.external_api_credential_create_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","plaintextSecret":"node:external_api_credential_form_secret_input.value","confirmed":"literal:true"}},"sourceActionKey":"external_api_credential_create_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"external_api_credential_create_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.external_api_credential_create_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:external_api_credential_create_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"external_api_credential_create_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"external_api_credential_create_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"external_api_credential_create_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.external_api_credential_update_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update","authority":"preview_only","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","recordId":"node:external_api_credential_form_record_id_input.value","active":"node:external_api_credential_form_active_input.value","newPlaintextSecret":"node:external_api_credential_form_secret_input.value","dryRun":"literal:true"}},"sourceActionKey":"external_api_credential_update_button","authorityMarker":"preview_only"},"sourceRecordKey":"external_api_credential_update_button"},{"wiringKey":"auth.external.credential_management.projection.external_api_credential_update_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","recordId":"node:external_api_credential_form_record_id_input.value","active":"node:external_api_credential_form_active_input.value","newPlaintextSecret":"node:external_api_credential_form_secret_input.value","confirmed":"literal:true"}},"sourceActionKey":"external_api_credential_update_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"external_api_credential_update_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.external_api_credential_update_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:external_api_credential_update_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"external_api_credential_update_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"external_api_credential_update_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"external_api_credential_update_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.external_api_credential_delete_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete","authority":"preview_only","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","recordId":"node:external_api_credential_form_record_id_input.value","dryRun":"literal:true"}},"sourceActionKey":"external_api_credential_delete_button","authorityMarker":"preview_only"},"sourceRecordKey":"external_api_credential_delete_button"},{"wiringKey":"auth.external.credential_management.projection.external_api_credential_delete_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:external_api_credential_form_record_kind_input.value","recordId":"node:external_api_credential_form_record_id_input.value","confirmed":"literal:true"}},"sourceActionKey":"external_api_credential_delete_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"external_api_credential_delete_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.external_api_credential_delete_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:external_api_credential_delete_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"external_api_credential_delete_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"external_api_credential_delete_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"external_api_credential_delete_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.json_template_download.wiring","wiringKind":"internal_instance_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"internal_instance_wiring","targetRef":"ui-local:instance_settings_import_form.template_download_trigger","authority":"draft_or_projection_only","payloadFrom":{"category_key":"literal:instance_settings"}},"sourceActionKey":"json_template_download","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"json_template_download"},{"wiringKey":"auth.external.credential_management.projection.json_import.wiring","wiringKind":"internal_instance_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"internal_instance_wiring","targetRef":"ui-local:instance_settings_import_form.template_import_trigger","authority":"draft_or_projection_only","payloadFrom":{"template_file":"node:template_file.value"}},"sourceActionKey":"json_import","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"json_import"},{"wiringKey":"auth.external.credential_management.projection.validate.wiring","wiringKind":"external_instance_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"external_instance_wiring","targetRef":"instance:db_instance_port:instance_authority_key:operation_binding_key","authority":"validation_only","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"}},"sourceActionKey":"validate","authorityMarker":"validation_only"},"sourceRecordKey":"validate"},{"wiringKey":"auth.external.credential_management.projection.preview.wiring","wiringKind":"external_instance_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"external_instance_wiring","targetRef":"instance:runtime_instance_port:instance_authority_key:operation_binding_key","authority":"preview_only","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"}},"sourceActionKey":"preview","authorityMarker":"preview_only"},"sourceRecordKey":"preview"},{"wiringKey":"auth.external.credential_management.projection.apply.wiring","wiringKind":"external_instance_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"external_instance_wiring","targetRef":"instance:db_instance_port:instance_authority_key:operation_binding_key","authority":"draft_apply_not_execution_authority","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"}},"sourceActionKey":"apply","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"apply"},{"wiringKey":"auth.external.credential_management.projection.approve.wiring","wiringKind":"external_instance_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"external_instance_wiring","targetRef":"instance:runtime_instance_port:instance_authority_key:operation_binding_key","authority":"execution_candidate_authority_boundary","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"}},"sourceActionKey":"approve","authorityMarker":"execution_candidate_authority_boundary"},"sourceRecordKey":"approve"},{"wiringKey":"auth.external.credential_management.projection.eic_create_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create","authority":"preview_only","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","instanceAuthorityKey":"node:eic_instance_authority_key_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","dryRun":"literal:true"}},"sourceActionKey":"eic_create_button","authorityMarker":"preview_only"},"sourceRecordKey":"eic_create_button"},{"wiringKey":"auth.external.credential_management.projection.eic_create_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","instanceAuthorityKey":"node:eic_instance_authority_key_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","confirmed":"literal:true"}},"sourceActionKey":"eic_create_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"eic_create_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.eic_create_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:eic_create_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"eic_create_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"eic_create_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"eic_create_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.eic_update_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update","authority":"preview_only","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","active":"node:eic_active_input.value","dryRun":"literal:true"}},"sourceActionKey":"eic_update_button","authorityMarker":"preview_only"},"sourceRecordKey":"eic_update_button"},{"wiringKey":"auth.external.credential_management.projection.eic_update_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","active":"node:eic_active_input.value","confirmed":"literal:true"}},"sourceActionKey":"eic_update_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"eic_update_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.eic_update_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:eic_update_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"eic_update_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"eic_update_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"eic_update_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.eic_delete_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete","authority":"preview_only","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","dryRun":"literal:true"}},"sourceActionKey":"eic_delete_button","authorityMarker":"preview_only"},"sourceRecordKey":"eic_delete_button"},{"wiringKey":"auth.external.credential_management.projection.eic_delete_confirm_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete","authority":"draft_apply_not_execution_authority","payloadFrom":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","confirmed":"literal:true"}},"sourceActionKey":"eic_delete_confirm_button","authorityMarker":"draft_apply_not_execution_authority"},"sourceRecordKey":"eic_delete_confirm_button"},{"wiringKey":"auth.external.credential_management.projection.eic_delete_cancel_button.wiring","wiringKind":"disclosure_state_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:eic_delete_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"eic_delete_confirm_modal","disclosureStatePath":"open"},"sourceActionKey":"eic_delete_cancel_button","authorityMarker":"draft_or_projection_only"},"sourceRecordKey":"eic_delete_cancel_button"},{"wiringKey":"auth.external.credential_management.projection.credential_search_button.wiring","wiringKind":"admin_runtime_dispatch_override_wiring","targetSurface":"manifest","wiringSchemaJson":{"eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-000000000092:credential_management:search","authority":"read_only","payloadFrom":{"category":"node:credential_category_filter.value","query":"node:credential_search_input.value","status":"node:credential_filter_status_input.value","active":"node:credential_filter_active_input.value","recordKind":"node:credential_filter_record_kind_input.value","providerKind":"node:credential_filter_provider_kind_input.value","requiredByBundle":"node:credential_filter_required_by_bundle_input.value","expiresBefore":"node:credential_filter_expires_before_input.value","expiresAfter":"node:credential_filter_expires_after_input.value"}},"sourceActionKey":"credential_search_button","authorityMarker":"read_only"},"sourceRecordKey":"credential_search_button"}]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000cd004',
    'admin/credential-management#instance_settings',
    '00000000-0000-0000-0000-0000000cd001',
    '00000000-0000-0000-0000-0000000cd002',
    '00000000-0000-0000-0000-0000000cd003',
    'default',
    0,
    '{"nodes":[{"nodeId":"credentials_users_create_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_create_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_create_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create"},"dispatchPayloadFromByTrigger":{"click":{"username":"node:credentials_users_username_input.value","password":"node:credentials_users_password_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u30e6\\u30fc\\u30b6\\u30fc\\u30a2\\u30ab\\u30a6\\u30f3\\u30c8\\u3092\\u4f5c\\u6210(\\u521d\\u671f\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u8fbc\\u307f)\"}"},{"nodeId":"credentials_users_create_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_create_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_create_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u30e6\\u30fc\\u30b6\\u30fc\\u30a2\\u30ab\\u30a6\\u30f3\\u30c8\\u3092\\u4f5c\\u6210\", \"body\": \"\\u6307\\u5b9a\\u3057\\u305f\\u30e6\\u30fc\\u30b6\\u30fc\\u540d\\u30fb\\u521d\\u671f\\u30d1\\u30b9\\u30ef\\u30fc\\u30c9\\u30fb\\u30ed\\u30fc\\u30eb\\u7b49\\u3067\\u30a2\\u30ab\\u30a6\\u30f3\\u30c8\\u3068\\u521d\\u671f\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u3092\\u4f5c\\u6210\\u3057\\u307e\\u3059\\u3002\"}}"},{"nodeId":"credentials_users_create_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad004:auth_users:create"},"dispatchPayloadFromByTrigger":{"click":{"username":"node:credentials_users_username_input.value","password":"node:credentials_users_password_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u4f5c\\u6210\\u3092\\u78ba\\u5b9a\"}"},{"nodeId":"credentials_users_create_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_create_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_create_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"credentials_users_update_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_update_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_update_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update"},"dispatchPayloadFromByTrigger":{"click":{"userId":"node:credentials_users_user_id_input.value","username":"node:credentials_users_username_input.value","active":"node:credentials_users_active_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u30e6\\u30fc\\u30b6\\u30fc\\u30a2\\u30ab\\u30a6\\u30f3\\u30c8\\u306e\\u30e1\\u30bf\\u30c7\\u30fc\\u30bf\\u3092\\u66f4\\u65b0\"}"},{"nodeId":"credentials_users_update_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_update_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_update_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u30e6\\u30fc\\u30b6\\u30fc\\u30a2\\u30ab\\u30a6\\u30f3\\u30c8\\u3092\\u66f4\\u65b0\", \"body\": \"\\u6307\\u5b9a\\u3057\\u305f\\u30e6\\u30fc\\u30b6\\u30fcID\\u306e\\u30a2\\u30ab\\u30a6\\u30f3\\u30c8\\u30e1\\u30bf\\u30c7\\u30fc\\u30bf(\\u30b9\\u30c6\\u30fc\\u30bf\\u30b9\\u30fb\\u30ed\\u30fc\\u30eb\\u30fb\\u72b6\\u614b\\u30e1\\u30e2\\u7b49)\\u3092\\u66f4\\u65b0\\u3057\\u307e\\u3059\\u3002\"}}"},{"nodeId":"credentials_users_update_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad005:auth_users:update"},"dispatchPayloadFromByTrigger":{"click":{"userId":"node:credentials_users_user_id_input.value","username":"node:credentials_users_username_input.value","active":"node:credentials_users_active_input.value","approve":"node:credentials_users_approve_input.value","status":"node:credentials_users_status_input.value","roleName":"node:credentials_users_role_name_input.value","suspendedFrom":"node:credentials_users_suspended_from_input.value","suspendedUntil":"node:credentials_users_suspended_until_input.value","stateNote":"node:credentials_users_state_note_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u66f4\\u65b0\\u3092\\u78ba\\u5b9a\"}"},{"nodeId":"credentials_users_update_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_update_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_update_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"credentials_users_delete_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_delete_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_delete_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete"},"dispatchPayloadFromByTrigger":{"click":{"userId":"node:credentials_users_user_id_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u30a2\\u30ab\\u30a6\\u30f3\\u30c8\\u3092\\u524a\\u9664(\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u8fbc\\u307f)\"}"},{"nodeId":"credentials_users_delete_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_delete_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_delete_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u30a2\\u30ab\\u30a6\\u30f3\\u30c8\\u3092\\u524a\\u9664\", \"body\": \"\\u6307\\u5b9a\\u3057\\u305f\\u30e6\\u30fc\\u30b6\\u30fcID\\u306e\\u30a2\\u30ab\\u30a6\\u30f3\\u30c8\\u3068\\u7d10\\u3065\\u304f\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u3092\\u524a\\u9664\\u3057\\u307e\\u3059\\u3002\\u3053\\u306e\\u64cd\\u4f5c\\u306f\\u53d6\\u308a\\u6d88\\u305b\\u307e\\u305b\\u3093\\u3002\"}}"},{"nodeId":"credentials_users_delete_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad006:auth_users:delete"},"dispatchPayloadFromByTrigger":{"click":{"userId":"node:credentials_users_user_id_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u524a\\u9664\\u3092\\u78ba\\u5b9a\"}"},{"nodeId":"credentials_users_delete_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_delete_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_delete_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"credentials_users_revoke_credential_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_revoke_credential_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_credential_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential"},"dispatchPayloadFromByTrigger":{"click":{"userId":"node:credentials_users_user_id_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u3092\\u5931\\u52b9(\\u78ba\\u8a8d\\u3092\\u958b\\u304f)\"}"},{"nodeId":"credentials_users_revoke_credential_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_revoke_credential_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_credential_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u3092\\u5931\\u52b9\", \"body\": \"\\u6307\\u5b9a\\u3057\\u305f\\u30e6\\u30fc\\u30b6\\u30fcID\\u306e\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u884c\\u3092\\u524a\\u9664\\u3057\\u3001\\u3059\\u3079\\u3066\\u306e\\u30a2\\u30af\\u30c6\\u30a3\\u30d6\\u30bb\\u30c3\\u30b7\\u30e7\\u30f3\\u3092\\u5931\\u52b9\\u3055\\u305b\\u307e\\u3059\\u3002\"}}"},{"nodeId":"credentials_users_revoke_credential_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad007:auth_users:revoke_credential"},"dispatchPayloadFromByTrigger":{"click":{"userId":"node:credentials_users_user_id_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u5931\\u52b9\\u3092\\u78ba\\u5b9a\"}"},{"nodeId":"credentials_users_revoke_credential_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_revoke_credential_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_credential_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"credentials_users_revoke_sessions_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"credentials_users_revoke_sessions_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_sessions_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions"},"dispatchPayloadFromByTrigger":{"click":{"userId":"node:credentials_users_user_id_input.value","sessionId":"node:credentials_users_session_id_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u30bb\\u30c3\\u30b7\\u30e7\\u30f3\\u3092\\u5931\\u52b9(\\u78ba\\u8a8d\\u3092\\u958b\\u304f)\"}"},{"nodeId":"credentials_users_revoke_sessions_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"credentials_users_revoke_sessions_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_sessions_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u30bb\\u30c3\\u30b7\\u30e7\\u30f3\\u3092\\u5931\\u52b9\", \"body\": \"\\u6307\\u5b9a\\u3057\\u305f\\u30bb\\u30c3\\u30b7\\u30e7\\u30f3ID(\\u7a7a\\u6b04\\u306e\\u5834\\u5408\\u306f\\u5168\\u3066)\\u306e\\u30a2\\u30af\\u30c6\\u30a3\\u30d6\\u30bb\\u30c3\\u30b7\\u30e7\\u30f3\\u3092\\u5931\\u52b9\\u3055\\u305b\\u307e\\u3059\\u3002\"}}"},{"nodeId":"credentials_users_revoke_sessions_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ad008:auth_users:revoke_sessions"},"dispatchPayloadFromByTrigger":{"click":{"userId":"node:credentials_users_user_id_input.value","sessionId":"node:credentials_users_session_id_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u30bb\\u30c3\\u30b7\\u30e7\\u30f3\\u5931\\u52b9\\u3092\\u78ba\\u5b9a\"}"},{"nodeId":"credentials_users_revoke_sessions_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"credentials_users_revoke_sessions_confirm_modal","statePath":"open","sourceActionKey":"credentials_users_revoke_sessions_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"configure_scheduler_job_credential_or_port_binding_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","statePath":"open","sourceActionKey":"configure_scheduler_job_credential_or_port_binding_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding"},"dispatchPayloadFromByTrigger":{"click":{"schedulerJobId":"node:scheduler_job_id_input.value","credentialRequirementRef":"node:scheduler_credential_requirement_ref_input.value","externalPortRef":"node:scheduler_external_port_ref_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u30b9\\u30b1\\u30b8\\u30e5\\u30fc\\u30e9\\u30fc\\u30b8\\u30e7\\u30d6\\u306e\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb/\\u30dd\\u30fc\\u30c8\\u30d0\\u30a4\\u30f3\\u30c7\\u30a3\\u30f3\\u30b0\\u3092\\u8a2d\\u5b9a\"}"},{"nodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","statePath":"open","sourceActionKey":"configure_scheduler_job_credential_or_port_binding_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u30b9\\u30b1\\u30b8\\u30e5\\u30fc\\u30e9\\u30fc\\u30b8\\u30e7\\u30d6\\u306e\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb/\\u30dd\\u30fc\\u30c8\\u30d0\\u30a4\\u30f3\\u30c7\\u30a3\\u30f3\\u30b0\\u3092\\u8a2d\\u5b9a\", \"body\": \"Bind the entered credential/port reference to the scheduler job. Writes only credential_requirement_ref/external_port_ref; never the job body/step-chain fields it owns.\"}}"},{"nodeId":"configure_scheduler_job_credential_or_port_binding_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","statePath":"open","sourceActionKey":"configure_scheduler_job_credential_or_port_binding_confirm_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding"},"dispatchPayloadFromByTrigger":{"click":{"schedulerJobId":"node:scheduler_job_id_input.value","credentialRequirementRef":"node:scheduler_credential_requirement_ref_input.value","externalPortRef":"node:scheduler_external_port_ref_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u8a2d\\u5b9a\"}"},{"nodeId":"configure_scheduler_job_credential_or_port_binding_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"configure_scheduler_job_credential_or_port_binding_confirm_modal","statePath":"open","sourceActionKey":"configure_scheduler_job_credential_or_port_binding_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"external_api_credential_create_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"external_api_credential_create_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_create_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","plaintextSecret":"node:external_api_credential_form_secret_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u5916\\u90e8API\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u4f5c\\u6210\"}"},{"nodeId":"external_api_credential_create_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"external_api_credential_create_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_create_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u5916\\u90e8API\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u4f5c\\u6210\", \"body\": \"Create a new record for the selected recordKind. Only allowed metadata fields plus a write-only secret input are sent; the secret is never echoed back.\"}}"},{"nodeId":"external_api_credential_create_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_create_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_create_confirm_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd008:external_api_credential:create"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","plaintextSecret":"node:external_api_credential_form_secret_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u4f5c\\u6210\"}"},{"nodeId":"external_api_credential_create_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_create_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_create_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"external_api_credential_update_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"external_api_credential_update_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_update_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","recordId":"node:external_api_credential_form_record_id_input.value","active":"node:external_api_credential_form_active_input.value","newPlaintextSecret":"node:external_api_credential_form_secret_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u5916\\u90e8API\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u66f4\\u65b0\"}"},{"nodeId":"external_api_credential_update_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"external_api_credential_update_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_update_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u5916\\u90e8API\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u66f4\\u65b0\", \"body\": \"Update the identified record''s metadata, and optionally rotate its secret via a write-only input. Never previews or echoes secret material.\"}}"},{"nodeId":"external_api_credential_update_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_update_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_update_confirm_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd009:external_api_credential:update"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:external_api_credential_form_record_kind_input.value","providerKind":"node:external_api_credential_form_provider_kind_input.value","requiredByBundle":"node:external_api_credential_form_required_by_bundle_input.value","referenceKey":"node:external_api_credential_form_reference_key_input.value","tokenKind":"node:external_api_credential_form_token_kind_input.value","refreshBeforeSeconds":"node:external_api_credential_form_refresh_before_seconds_input.value","urlOrEnvReference":"node:external_api_credential_form_url_or_env_reference_input.value","credentialKind":"node:external_api_credential_form_credential_kind_input.value","hookPath":"node:external_api_credential_form_hook_path_input.value","headerKey":"node:external_api_credential_form_header_key_input.value","routeKey":"node:external_api_credential_form_route_key_input.value","encryptionKeyReference":"node:external_api_credential_form_encryption_key_reference_input.value","recordId":"node:external_api_credential_form_record_id_input.value","active":"node:external_api_credential_form_active_input.value","newPlaintextSecret":"node:external_api_credential_form_secret_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u66f4\\u65b0\"}"},{"nodeId":"external_api_credential_update_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_update_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_update_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"external_api_credential_delete_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"external_api_credential_delete_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_delete_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:external_api_credential_form_record_kind_input.value","recordId":"node:external_api_credential_form_record_id_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u5916\\u90e8API\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u7121\\u52b9\\u5316\"}"},{"nodeId":"external_api_credential_delete_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"external_api_credential_delete_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_delete_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u5916\\u90e8API\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u7121\\u52b9\\u5316\", \"body\": \"Deactivate (soft-delete) the identified record by recordKind/recordId. This never removes the row or exposes secret material.\"}}"},{"nodeId":"external_api_credential_delete_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_delete_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_delete_confirm_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd010:external_api_credential:delete"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:external_api_credential_form_record_kind_input.value","recordId":"node:external_api_credential_form_record_id_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u7121\\u52b9\\u5316\"}"},{"nodeId":"external_api_credential_delete_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"external_api_credential_delete_confirm_modal","statePath":"open","sourceActionKey":"external_api_credential_delete_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"json_template_download","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"localStateMutation","payloadFrom":{"category_key":"literal:instance_settings"},"sourceActionKey":"json_template_download","targetRef":"ui-local:instance_settings_import_form.template_download_trigger"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"JSON\\u30c6\\u30f3\\u30d7\\u30ec\\u30fc\\u30c8\\u3092\\u30c0\\u30a6\\u30f3\\u30ed\\u30fc\\u30c9\"}"},{"nodeId":"json_import","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"localStateMutation","payloadFrom":{"template_file":"node:template_file.value"},"sourceActionKey":"json_import","targetRef":"ui-local:instance_settings_import_form.template_import_trigger"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"JSON\\u30c6\\u30f3\\u30d7\\u30ec\\u30fc\\u30c8\\u3092\\u30a4\\u30f3\\u30dd\\u30fc\\u30c8\"}"},{"nodeId":"validate","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"validate","instanceTargetRef":"instance-port:db_instance_port:instance_authority_key:operation_binding_key"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u691c\\u8a3c\"}"},{"nodeId":"preview","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"preview","instanceTargetRef":"instance-port:runtime_instance_port:instance_authority_key:operation_binding_key"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30d7\\u30ec\\u30d3\\u30e5\\u30fc\"}"},{"nodeId":"apply","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"apply","instanceTargetRef":"instance-port:db_instance_port:instance_authority_key:operation_binding_key"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u9069\\u7528\"}"},{"nodeId":"approve","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"approve","instanceTargetRef":"instance-port:runtime_instance_port:instance_authority_key:operation_binding_key"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u627f\\u8a8d\"}"},{"nodeId":"eic_create_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"eic_create_confirm_modal","statePath":"open","sourceActionKey":"eic_create_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:eic_record_kind_input.value","instanceAuthorityKey":"node:eic_instance_authority_key_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u5916\\u90e8\\u30a4\\u30f3\\u30b9\\u30bf\\u30f3\\u30b9\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u4f5c\\u6210\"}"},{"nodeId":"eic_create_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"eic_create_confirm_modal","statePath":"open","sourceActionKey":"eic_create_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u5916\\u90e8\\u30a4\\u30f3\\u30b9\\u30bf\\u30f3\\u30b9\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u3092\\u4f5c\\u6210\", \"body\": \"\\u9078\\u629e\\u3057\\u305f\\u30ec\\u30b3\\u30fc\\u30c9\\u7a2e\\u5225\\u306b\\u5bfe\\u3057\\u3066\\u65b0\\u3057\\u3044\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u4f5c\\u6210\\u3057\\u307e\\u3059\\u3002\\u8a31\\u53ef\\u3055\\u308c\\u305f\\u30e1\\u30bf\\u30c7\\u30fc\\u30bf\\u9805\\u76ee\\u306e\\u307f\\u3092\\u9001\\u4fe1\\u3057\\u307e\\u3059\\u3002\"}}"},{"nodeId":"eic_create_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd012:external_instance_credential:create"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:eic_record_kind_input.value","instanceAuthorityKey":"node:eic_instance_authority_key_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u4f5c\\u6210\\u3092\\u78ba\\u5b9a\"}"},{"nodeId":"eic_create_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"eic_create_confirm_modal","statePath":"open","sourceActionKey":"eic_create_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"eic_update_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"eic_update_confirm_modal","statePath":"open","sourceActionKey":"eic_update_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","active":"node:eic_active_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u5916\\u90e8\\u30a4\\u30f3\\u30b9\\u30bf\\u30f3\\u30b9\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u66f4\\u65b0\"}"},{"nodeId":"eic_update_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"eic_update_confirm_modal","statePath":"open","sourceActionKey":"eic_update_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u5916\\u90e8\\u30a4\\u30f3\\u30b9\\u30bf\\u30f3\\u30b9\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u3092\\u66f4\\u65b0\", \"body\": \"\\u6307\\u5b9a\\u3057\\u305f\\u30ec\\u30b3\\u30fc\\u30c9ID\\u306e\\u30e1\\u30bf\\u30c7\\u30fc\\u30bf\\u3092\\u66f4\\u65b0\\u3057\\u307e\\u3059\\u3002\"}}"},{"nodeId":"eic_update_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd013:external_instance_credential:update"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","providerKind":"node:eic_provider_kind_input.value","requiredByBundle":"node:eic_required_by_bundle_input.value","referenceKey":"node:eic_reference_key_input.value","active":"node:eic_active_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u66f4\\u65b0\\u3092\\u78ba\\u5b9a\"}"},{"nodeId":"eic_update_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"eic_update_confirm_modal","statePath":"open","sourceActionKey":"eic_update_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"eic_delete_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"eic_delete_confirm_modal","statePath":"open","sourceActionKey":"eic_delete_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"\\u5916\\u90e8\\u30a4\\u30f3\\u30b9\\u30bf\\u30f3\\u30b9\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u30ec\\u30b3\\u30fc\\u30c9\\u3092\\u7121\\u52b9\\u5316\"}"},{"nodeId":"eic_delete_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"eic_delete_confirm_modal","statePath":"open","sourceActionKey":"eic_delete_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"\\u5916\\u90e8\\u30a4\\u30f3\\u30b9\\u30bf\\u30f3\\u30b9\\u30af\\u30ec\\u30c7\\u30f3\\u30b7\\u30e3\\u30eb\\u3092\\u7121\\u52b9\\u5316\", \"body\": \"\\u6307\\u5b9a\\u3057\\u305f\\u30ec\\u30b3\\u30fc\\u30c9ID\\u3092\\u7121\\u52b9\\u5316\\u3057\\u307e\\u3059\\u3002\"}}"},{"nodeId":"eic_delete_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000cd014:external_instance_credential:delete"},"dispatchPayloadFromByTrigger":{"click":{"recordKind":"node:eic_record_kind_input.value","recordId":"node:eic_record_id_input.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"\\u7121\\u52b9\\u5316\\u3092\\u78ba\\u5b9a\"}"},{"nodeId":"eic_delete_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"eic_delete_confirm_modal","statePath":"open","sourceActionKey":"eic_delete_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"\\u30ad\\u30e3\\u30f3\\u30bb\\u30eb\"}"},{"nodeId":"credential_search_button","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-000000000092:credential_management:search"},"dispatchPayloadFromByTrigger":{"click":{"category":"node:credential_category_filter.value","query":"node:credential_search_input.value","status":"node:credential_filter_status_input.value","active":"node:credential_filter_active_input.value","recordKind":"node:credential_filter_record_kind_input.value","providerKind":"node:credential_filter_provider_kind_input.value","requiredByBundle":"node:credential_filter_required_by_bundle_input.value","expiresBefore":"node:credential_filter_expires_before_input.value","expiresAfter":"node:credential_filter_expires_after_input.value"}},"propsJson":"{\"label\": \"\\u691c\\u7d22\"}"},{"nodeId":"credential_result_list","nodeKind":"catalog_component","runtimeInteractions":[],"propsJson":"{\"table\": null, \"columns\": [{\"key\": \"recordKind\", \"header\": \"レコード種別\"}, {\"key\": \"providerKind\", \"header\": \"プロバイダー種別\"}, {\"key\": \"requiredByBundle\", \"header\": \"必要とするバンドル\"}, {\"key\": \"referenceKey\", \"header\": \"参照キー\"}, {\"key\": \"active\", \"header\": \"有効\"}, {\"key\": \"expiresAt\", \"header\": \"有効期限\"}, {\"key\": \"updatedAt\", \"header\": \"更新日時\"}], \"rows\": []}","propBindings":{"rows":{"source":"emission.data.records"},"columns":{"source":"emission.data.activeColumns","transform":"activeColumnsToTableColumns"}}},{"nodeId":"credential_search_section","runtimeInteractions":[{"trigger":"change","actionType":"setState","targetRef":"ui-local:credential_category_filter.selectedCategory","payloadFrom":{"value":"event.value"},"sourceActionKey":"credential_category_filter"}]},{"nodeId":"instance_settings_import_form","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"localStateMutation","payloadFrom":{"category_key":"literal:instance_settings"},"sourceActionKey":"json_template_download","targetRef":"ui-local:instance_settings_import_form.template_download_trigger"},{"trigger":"click","actionType":"localStateMutation","payloadFrom":{"template_file":"node:template_file.value"},"sourceActionKey":"json_import","targetRef":"ui-local:instance_settings_import_form.template_import_trigger"}]},{"nodeId":"instance_address_form","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"validate","instanceTargetRef":"instance-port:db_instance_port:instance_authority_key:operation_binding_key"}]},{"nodeId":"instance_operation_binding_form","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"preview","instanceTargetRef":"instance-port:runtime_instance_port:instance_authority_key:operation_binding_key"}]},{"nodeId":"instance_operation_approval_form","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"apply","instanceTargetRef":"instance-port:db_instance_port:instance_authority_key:operation_binding_key"},{"trigger":"click","actionType":"dispatchInstanceOperation","payloadFrom":{"instance_authority_key":"node:instance_authority_key.value","operation_binding_key":"node:operation_binding_key.value"},"sourceActionKey":"approve","instanceTargetRef":"instance-port:runtime_instance_port:instance_authority_key:operation_binding_key"}]},{"nodeId":"credential_category_filter","stateJson":"{\"selectedCategory\": \"external_api_credential\"}","propsJson":"{\"data\": {\"value\": \"external_api_credential\", \"options\": [{\"label\": \"ユーザー\", \"value\": \"users\"}, {\"label\": \"外部APIクレデンシャル\", \"value\": \"external_api_credential\"}, {\"label\": \"外部インスタンスクレデンシャル\", \"value\": \"external_instance_credential\"}], \"label\": \"カテゴリ (users / external_api_credential / external_instance_credential)\", \"placeholder\": \"カテゴリ (users / external_api_credential / external_instance_credential)\"}}"}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- ---------------------------------------------------------------------------
-- external_port_substrate canonical physical binding catalog.
-- Registers all external port substrate physical tables, the hub and canonical
-- topology_manifests row for the credential-management projection, and the
-- per-port-kind physical_table_manifest_bindings that back
-- LoadPortRecordByCanonicalBindingAsync resolution.
--
-- wiring_physical_to_package is NOT used here; it is the canonical table for
-- physical table → package wiring (UI Component Builder layer). Manifest-scoped
-- physical table associations belong in physical_table_manifest_bindings.
-- ---------------------------------------------------------------------------
INSERT INTO topology.physical_tables (table_ref, schema_name, category, active)
VALUES
    ('topology.external_access_ports',            'topology', 'external_port_substrate', true),
    ('topology.external_response_ports',          'topology', 'external_port_substrate', true),
    ('topology.external_hook_ports',              'topology', 'external_port_substrate', true),
    ('topology.external_port_policies',           'topology', 'external_port_substrate', true),
    ('topology.external_port_policy_steps',       'topology', 'external_port_substrate', true),
    ('topology.db_instance_port',                 'topology', 'instance_port_substrate', true),
    ('topology.runtime_instance_port',            'topology', 'instance_port_substrate', true),
    ('topology.instance_connection_policy',       'topology', 'instance_port_substrate', true),
    ('topology.instance_operation_authority_binding', 'topology', 'instance_port_substrate', true),
    ('topology.external_credential_vault',        'topology', 'external_port_substrate', true),
    ('topology.external_credential_refresh_attempt', 'topology', 'external_port_substrate', true)
ON CONFLICT (table_ref) DO UPDATE
    SET schema_name = EXCLUDED.schema_name,
        category    = EXCLUDED.category,
        active      = EXCLUDED.active;

-- Hub for the external port substrate canonical binding space.
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000000a1', '{"description":"external_port_substrate","system":true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

-- topology_manifests projection for manifest 092.
-- topology_manifest_id = manifest_id (per ManifestCanonicalProjection.UpsertTopologyManifestAsync).
INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000000a1'::uuid,
    'auth.external.credential_management.projection',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-000000000092'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

-- Per-port-kind physical_table_manifest_bindings for canonical binding resolution.
-- Only the three port kind tables are bound; policy/credential tables are catalog
-- entries only and do not have a portKind canonical binding path.
INSERT INTO topology.physical_table_manifest_bindings
    (physical_table_id, topology_manifest_id, active, binding_evidence_json)
SELECT pt.physical_table_id,
       '00000000-0000-0000-0000-000000000092'::uuid,
       true,
       jsonb_build_object(
           'portKind', CASE pt.table_ref
               WHEN 'topology.external_access_ports'   THEN 'access_port'
               WHEN 'topology.external_response_ports' THEN 'response_port'
               WHEN 'topology.external_hook_ports'     THEN 'hook_port'
               WHEN 'topology.db_instance_port'        THEN 'db_instance_port'
               WHEN 'topology.runtime_instance_port'   THEN 'runtime_instance_port'
           END,
           'source', 'external-port-canonical-physical-binding-execution',
           'manifestKey', 'auth.external.credential_management.projection')
FROM topology.physical_tables pt
WHERE pt.table_ref IN (
    'topology.external_access_ports',
    'topology.external_response_ports',
    'topology.external_hook_ports',
    'topology.db_instance_port',
    'topology.runtime_instance_port')
ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE
    SET active             = true,
        binding_evidence_json = EXCLUDED.binding_evidence_json,
        updated_at         = now();

-- Canonical hub_relations entry for manifest 092 (sequence_position=1). Self-referencing:
-- topology_manifest_id=092's own topology_manifest, related_hub_id=092's own existing hub
-- ('...a1', external_port_substrate) — no dedicated hub is introduced. hub '...a1' has exactly
-- one active hubs.topology_manifests row (092 itself), so TargetManifestId resolves
-- deterministically (docs/design/db-schema.yaml no_implicit_join_nullable_fallback semantics).
-- This is the canonical (non-demo) seed closing the prior gap where hubs.hub_relations existed
-- only in db/demo_seed.sql (and there, only for the unrelated demo manifest).
INSERT INTO hubs.hub_relations (
    hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, relation_config, status
)
VALUES (
    '00000000-0000-0000-0000-0000000000b1',
    '00000000-0000-0000-0000-000000000092',
    '00000000-0000-0000-0000-0000000000a1',
    1,
    '{"transition":"canonical_default_entry"}'::jsonb,
    'active'
)
ON CONFLICT (topology_manifest_id, sequence_position) DO NOTHING;

-- Manifest entry: admin dispatch for
-- credential_management:configure_scheduler_job_credential_or_port_binding → admin_runtime.
-- docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
-- credentials.categories.external_api_credential.consumer_reference_binding: writes only
-- topology.scheduler_jobs.credential_requirement_ref / .external_port_ref, identified by
-- scheduler_job_id plus a credential/port row's reference_key — never scheduler job body/
-- step-chain fields (those stay on the scheduler_jobs:create/edit dispatch above,
-- manifest 00000000-0000-0000-0000-0000000000f1/f2) and never the vault/port tables
-- themselves (those stay this category's own create/read/update/delete/search operations).
-- Dispatch-only entry (no ui_projection): this action is reached from the SAME credential
-- management screen/projection as manifest 092, never a dedicated route.
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000cd006',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"credential_management","action":"configure_scheduler_job_credential_or_port_binding"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO NOTHING;

-- Manifest entries: admin dispatch for external_api_credential:get/create/update/delete →
-- admin_runtime. docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.
-- admin.surfaces.credentials.categories.external_api_credential (create/read/update/delete/
-- search over topology.external_credential_vault/external_access_ports/external_response_ports/
-- external_hook_ports, secret-free projection only). Dispatch-only entries (no ui_projection):
-- these operations are reached from the SAME credential management screen/projection as
-- manifest 092, never a dedicated route/plane, mirroring the cd006 pattern above.
--
-- external_api_credential:search is NOT a dispatch-only manifest here (round 4) -- it is
-- manifest 092's OWN dispatcher_mapping entry (see the manifest 092 INSERT above), so a search
-- dispatch's response.manifestId equals 092's own id and the EXISTING generic same-manifest
-- adoption path (ProjectionShell.tsx handleRuntimeDispatchResult's expectedManifestId ===
-- adoptedManifestId branch -- the same mechanism admin-enum's enum_table/list_groups pair
-- already uses) adopts the response's Emission.Data.records into 092's own rendered screen; the
-- credential_result_list table node's propBindings.rows.source="emission.data.records" (see
-- cd002/cd004 below) then re-resolves against it, exactly like enum_table's
-- propBindings.rows.source="emission.data.groups". A separate dispatch-only manifest for search
-- (as create/update/delete still use) would put search's response on a DIFFERENT manifest
-- identity than 092, which the settlement gate never adopts (cross-manifest dispatch results are
-- discarded except for triggering a canonical_reread of the ADOPTED manifest's own original
-- axes) -- there is no existing generic mechanism to land a cross-manifest response into another
-- node's props, and this Round intentionally reuses the existing same-manifest authority instead
-- of inventing one.
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
(
    '00000000-0000-0000-0000-0000000cd008',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"external_api_credential","action":"create"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
),
(
    '00000000-0000-0000-0000-0000000cd009',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"external_api_credential","action":"update"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
),
(
    '00000000-0000-0000-0000-0000000cd010',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"external_api_credential","action":"delete"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
),
(
    '00000000-0000-0000-0000-0000000cd011',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"external_api_credential","action":"get"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO NOTHING;

-- =============================================================================
-- round 5: external_instance_credential:create/update/delete dispatch-only manifests
-- (AdminRuntime.ExternalInstanceCredential.cs / NpgsqlExternalInstanceCredentialAdminRepository).
-- Mirrors the cd008-011 pattern above. search is NOT a dispatch-only manifest here -- it is
-- unified onto manifest 092's own dispatcher_mapping entry (credential_management:search,
-- category=external_instance_credential), the same round-5 widening applied to
-- external_api_credential:search and users search.
-- =============================================================================
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
(
    '00000000-0000-0000-0000-0000000cd012',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"external_instance_credential","action":"create"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
),
(
    '00000000-0000-0000-0000-0000000cd013',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"external_instance_credential","action":"update"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
),
(
    '00000000-0000-0000-0000-0000000cd014',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"external_instance_credential","action":"delete"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
),
(
    '00000000-0000-0000-0000-0000000cd015',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"external_instance_credential","action":"get"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- file_storage_bundle physical table catalog.
-- Registers the domain metadata tables for export_job / file_artifact /
-- checksum / manifest / signed download authorization / record attachment binding.
-- ---------------------------------------------------------------------------
INSERT INTO topology.physical_tables (table_ref, schema_name, category, active)
VALUES
    ('topology.export_jobs',                   'topology', 'file_storage_bundle', true),
    ('topology.file_artifacts',                'topology', 'file_storage_bundle', true),
    ('topology.file_checksum_records',         'topology', 'file_storage_bundle', true),
    ('topology.export_manifests',              'topology', 'file_storage_bundle', true),
    ('topology.signed_download_authorizations','topology', 'file_storage_bundle', true),
    ('topology.record_file_attachment_bindings','topology', 'file_storage_bundle', true),
    ('topology.cli_reader_import_candidates','topology', 'cli_mcp_import_candidate_port', true)
ON CONFLICT (table_ref) DO UPDATE
    SET schema_name = EXCLUDED.schema_name,
        category    = EXCLUDED.category,
        active      = EXCLUDED.active;

-- Hub for file_storage bundle dispatch space.
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000000a2', '{"description":"file_storage_bundle","system":true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

-- topology_manifests for file_storage export_job dispatch boundary (manifest 093).
INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
VALUES (
    '00000000-0000-0000-0000-000000000093',
    '00000000-0000-0000-0000-0000000000a2',
    'file_storage.export_job.dispatch.projection',
    'active',
    '{"manifest_id":"00000000-0000-0000-0000-000000000093","source":"file-storage-bundle-export-job-dispatch","attachmentSurface":{"tableRef":"topology.record_file_attachment_bindings","artifactAuthority":"topology.file_artifacts","credentialPlane":"external_port_substrate_reference_key_only","operations":["topology.fs_bind_record_file_attachment","topology.fs_list_record_file_attachments","topology.fs_unbind_record_file_attachment"],"forbiddenProjectionFields":["storage_ref","authorization_key","signed_url","credential","bucket","endpoint"]},"screen_data_shape":{"type":"screen_data_shape","contentsType":"bundle_projection","topologySystemName":"file-storage-attachment-manifest-seed","userFacingTopologyLabel":"File attachments","tableRef":"topology.record_file_attachment_bindings","dbTableName":"topology.record_file_attachment_bindings","displayColumnMode":"selected","displayColumns":["record_file_attachment_bindings.record_table_ref","record_file_attachment_bindings.record_id","file_artifacts.file_name","file_artifacts.file_type","file_artifacts.byte_size","file_artifacts.checksum_value","record_file_attachment_bindings.relation_kind","record_file_attachment_bindings.created_at"],"logicalTables":[{"tableName":"record_file_attachment_bindings","columns":[{"name":"attachment_binding_id","dataType":"uuid","nullable":false},{"name":"record_table_ref","dataType":"text","nullable":false},{"name":"record_id","dataType":"text","nullable":false},{"name":"file_artifact_id","dataType":"uuid","nullable":false},{"name":"relation_kind","dataType":"text","nullable":false},{"name":"created_at","dataType":"timestamptz","nullable":false}]},{"tableName":"file_artifacts","columns":[{"name":"file_artifact_id","dataType":"uuid","nullable":false},{"name":"file_name","dataType":"text","nullable":false},{"name":"file_type","dataType":"text","nullable":false},{"name":"byte_size","dataType":"bigint","nullable":true},{"name":"checksum_value","dataType":"text","nullable":false}]}],"relationIntents":[{"localTableRef":"record_file_attachment_bindings","joinTableRef":"file_artifacts","localKey":"file_artifact_id","remoteKey":"file_artifact_id"}],"operationEntityBindings":[{"operationKind":"bind_attachment","function":"topology.fs_bind_record_file_attachment","entityTargetColumns":["record_table_ref","record_id","file_artifact_id","relation_kind"]},{"operationKind":"list_attachments","function":"topology.fs_list_record_file_attachments","entityTargetColumns":["record_table_ref","record_id"]},{"operationKind":"unbind_attachment","function":"topology.fs_unbind_record_file_attachment","entityTargetColumns":["record_table_ref","record_id","file_artifact_id","relation_kind"]}]}}'::jsonb
)
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET manifest_key = EXCLUDED.manifest_key,
        status       = EXCLUDED.status,
        updated_at   = now();

-- physical_table_manifest_bindings for file_storage domain tables.
INSERT INTO topology.physical_table_manifest_bindings
    (physical_table_id, topology_manifest_id, active, binding_evidence_json)
SELECT pt.physical_table_id,
       '00000000-0000-0000-0000-000000000093'::uuid,
       true,
       jsonb_build_object(
           'bundle', 'file_storage_bundle',
           'source', 'file-storage-bundle-export-job-dispatch',
           'manifestKey', 'file_storage.export_job.dispatch.projection')
FROM topology.physical_tables pt
WHERE pt.table_ref IN (
    'topology.export_jobs',
    'topology.file_artifacts',
    'topology.file_checksum_records',
    'topology.export_manifests',
    'topology.signed_download_authorizations',
    'topology.record_file_attachment_bindings')
ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE
    SET active                = true,
        binding_evidence_json = EXCLUDED.binding_evidence_json,
        updated_at            = now();

-- ---------------------------------------------------------------------------
-- external_port_substrate generic policy seed.
-- No provider credential plaintext is stored here; provider_kind remains data
-- classification and runtime executes by operation_key registry dispatch.
-- ---------------------------------------------------------------------------
INSERT INTO topology.external_port_policies (policy_id, policy_key, port_kind, required_by_bundle, active)
VALUES
    ('00000000-0000-0000-0000-0000000000e1', 'external_access_port_generic_http', 'access_port', 'external-port-substrate-seed-coding', true),
    ('00000000-0000-0000-0000-0000000000e2', 'external_response_port_generic_http', 'response_port', 'external-port-substrate-seed-coding', true),
    ('00000000-0000-0000-0000-0000000000e3', 'external_hook_port_scheduler_boundary', 'hook_port', 'external-port-substrate-seed-coding', true)
ON CONFLICT (policy_id) DO NOTHING;

INSERT INTO topology.external_port_policy_steps (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000101', '00000000-0000-0000-0000-0000000000e1', 1, 'resolve_port_record', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-0000000000e1', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000103', '00000000-0000-0000-0000-0000000000e1', 3, 'build_http_request', '{"method":"GET"}', NULL, true),
    ('00000000-0000-0000-0000-000000000104', '00000000-0000-0000-0000-0000000000e1', 4, 'send_http', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000105', '00000000-0000-0000-0000-0000000000e1', 5, 'capture_response', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000201', '00000000-0000-0000-0000-0000000000e2', 1, 'resolve_port_record', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000202', '00000000-0000-0000-0000-0000000000e2', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000203', '00000000-0000-0000-0000-0000000000e2', 3, 'build_http_request', '{"method":"POST"}', NULL, true),
    ('00000000-0000-0000-0000-000000000204', '00000000-0000-0000-0000-0000000000e2', 4, 'send_http', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000205', '00000000-0000-0000-0000-0000000000e2', 5, 'capture_response', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-0000000000e3', 1, 'resolve_port_record', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000302', '00000000-0000-0000-0000-0000000000e3', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000303', '00000000-0000-0000-0000-0000000000e3', 3, 'verify_signature_by_config', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000304', '00000000-0000-0000-0000-0000000000e3', 4, 'enqueue_scheduler_event', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000305', '00000000-0000-0000-0000-0000000000e3', 5, 'append_runtime_event_log', '{}', NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;


-- ---------------------------------------------------------------------------
-- Consumer bundle seed binding.
-- Each consumer bundle registers required_by_bundle + port_kind + policy_steps
-- through the existing port_target_ref lane. No provider-specific clients,
-- runtime branches, or credential plaintext values are stored here.
-- reference_key values are vault reference identifiers, not credential payloads.
-- url_or_env_reference values are env-variable reference names, not real URLs.
-- ---------------------------------------------------------------------------


-- Instance port runtime seed records use guarded vault reference metadata only.
INSERT INTO topology.external_credential_vault
    (credential_vault_id, provider_kind, required_by_bundle, token_kind, token_hash, encrypted_payload, encryption_key_reference, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000aa1', 'external_postgres', 'generic_instance_integration', 'runtime_connection_ref', 'sha256:instance-reference-only', decode('00','hex'), 'kms:instance-reference-key', 'vault:ref:generic_instance_runtime', true)
ON CONFLICT (credential_vault_id) DO NOTHING;

INSERT INTO topology.db_instance_port
    (instance_port_id, port_kind, instance_authority_key, provider_kind, required_by_bundle, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000ab1', 'db_instance_port', 'registered_instance_key', 'external_postgres', 'generic_instance_integration', 'vault:ref:generic_instance_runtime', true)
ON CONFLICT (instance_port_id) DO NOTHING;

INSERT INTO topology.runtime_instance_port
    (instance_port_id, port_kind, instance_authority_key, provider_kind, required_by_bundle, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000ab2', 'runtime_instance_port', 'registered_runtime_key', 'peer_runtime', 'generic_instance_integration', 'vault:ref:generic_instance_runtime', true)
ON CONFLICT (instance_port_id) DO NOTHING;

INSERT INTO topology.instance_connection_policy
    (policy_id, instance_authority_key, credential_reference_key, connection_timeout_ms, statement_timeout_ms, max_result_bytes, allowed_schemas, allowed_function_names, result_sanitize_policy_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000ac1', 'registered_instance_key', 'vault:ref:generic_instance_runtime', 3000, 3000, 65536, ARRAY['approved_schema'], ARRAY['approved_function'], 'secret_deny_default', true)
ON CONFLICT (instance_authority_key, credential_reference_key) DO NOTHING;

-- file_storage_bundle: object_storage access_port + response_port
INSERT INTO topology.external_access_ports
    (access_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f01', 'file_storage_bundle', 'object_storage', 'env:FILE_STORAGE_ENDPOINT_REF', 'external', 'vault:ref:file_storage_credential', true)
ON CONFLICT (access_port_id) DO NOTHING;

INSERT INTO topology.external_response_ports
    (response_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f02', 'file_storage_bundle', 'object_storage', 'env:FILE_STORAGE_ENDPOINT_REF', 'external', 'vault:ref:file_storage_credential', true),
    ('00000000-0000-0000-0000-000000000f0a', 'file_storage_attachment_bind', 'object_storage', 'env:FILE_STORAGE_ENDPOINT_REF', 'external', 'vault:ref:file_storage_credential', true),
    ('00000000-0000-0000-0000-000000000f0b', 'file_storage_attachment_list', 'object_storage', 'env:FILE_STORAGE_ENDPOINT_REF', 'external', 'vault:ref:file_storage_credential', true),
    ('00000000-0000-0000-0000-000000000f0c', 'file_storage_attachment_unbind', 'object_storage', 'env:FILE_STORAGE_ENDPOINT_REF', 'external', 'vault:ref:file_storage_credential', true)
ON CONFLICT (response_port_id) DO NOTHING;

-- email_bundle: smtp response_port
INSERT INTO topology.external_response_ports
    (response_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f03', 'email_bundle', 'smtp', 'env:SMTP_HOST_REF', 'external', 'vault:ref:email_smtp_credential', true)
ON CONFLICT (response_port_id) DO NOTHING;

-- stripe_bundle: stripe hook_port
INSERT INTO topology.external_hook_ports
    (hook_port_id, required_by_bundle, provider_kind, hook_path, header_key, route_key, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f04', 'stripe_bundle', 'stripe', '/hooks/stripe', 'stripe-signature', 'stripe', 'external', 'vault:ref:stripe_webhook_signing_key', true)
ON CONFLICT (hook_port_id) DO NOTHING;

-- webhook_inbox_bundle: generic_webhook hook_port
INSERT INTO topology.external_hook_ports
    (hook_port_id, required_by_bundle, provider_kind, hook_path, header_key, route_key, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f05', 'webhook_inbox_bundle', 'generic_webhook', '/hooks/webhook_inbox', 'x-webhook-signature', 'webhook_inbox', 'external', 'vault:ref:webhook_inbox_signing_key', true)
ON CONFLICT (hook_port_id) DO NOTHING;

-- job_scheduler_bundle: external scheduler access_port (built-in scheduler path does not depend on this record)
INSERT INTO topology.external_access_ports
    (access_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f06', 'job_scheduler_bundle', 'external_scheduler', 'env:JOB_SCHEDULER_ENDPOINT_REF', 'none', NULL, true)
ON CONFLICT (access_port_id) DO NOTHING;

-- job_scheduler_bundle: built-in scheduler hook_port (receives internal scheduler callbacks; credential_kind = none)
INSERT INTO topology.external_hook_ports
    (hook_port_id, required_by_bundle, provider_kind, hook_path, header_key, route_key, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f09', 'job_scheduler_bundle', 'built_in_scheduler', '/hooks/job_scheduler', 'x-scheduler-signature', 'job_scheduler', 'none', NULL, true)
ON CONFLICT (hook_port_id) DO NOTHING;

-- audit_approval_bundle: notification response_port
INSERT INTO topology.external_response_ports
    (response_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f07', 'audit_approval_bundle', 'notification', 'env:APPROVAL_NOTIFICATION_ENDPOINT_REF', 'external', 'vault:ref:audit_approval_notification_credential', true)
ON CONFLICT (response_port_id) DO NOTHING;

-- export_sftp_bundle: sftp response_port
INSERT INTO topology.external_response_ports
    (response_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f08', 'export_sftp_bundle', 'sftp', 'env:SFTP_HOST_REF', 'external', 'vault:ref:export_sftp_credential', true)
ON CONFLICT (response_port_id) DO NOTHING;

-- Consumer bundle policies
INSERT INTO topology.external_port_policies (policy_id, policy_key, port_kind, required_by_bundle, active)
VALUES
    ('00000000-0000-0000-0000-0000000000e4', 'file_storage_access_port_generic',     'access_port',   'file_storage_bundle',   true),
    ('00000000-0000-0000-0000-0000000000e5', 'file_storage_response_port_generic',    'response_port', 'file_storage_bundle',   true),
    ('00000000-0000-0000-0000-0000000000ed', 'file_storage_attachment_bind',         'response_port', 'file_storage_attachment_bind',   true),
    ('00000000-0000-0000-0000-0000000000ee', 'file_storage_attachment_list',         'response_port', 'file_storage_attachment_list',   true),
    ('00000000-0000-0000-0000-0000000000ef', 'file_storage_attachment_unbind',       'response_port', 'file_storage_attachment_unbind', true),
    ('00000000-0000-0000-0000-0000000000e6', 'email_response_port_generic',           'response_port', 'email_bundle',          true),
    ('00000000-0000-0000-0000-0000000000e7', 'stripe_hook_port_scheduler_boundary',   'hook_port',     'stripe_bundle',         true),
    ('00000000-0000-0000-0000-0000000000e8', 'webhook_inbox_hook_port_scheduler',     'hook_port',     'webhook_inbox_bundle',  true),
    ('00000000-0000-0000-0000-0000000000e9', 'job_scheduler_access_port_generic',     'access_port',   'job_scheduler_bundle',  true),
    ('00000000-0000-0000-0000-0000000000ec', 'job_scheduler_hook_port_enqueue',       'hook_port',     'job_scheduler_bundle',  true),
    ('00000000-0000-0000-0000-0000000000ea', 'audit_approval_response_port_generic',  'response_port', 'audit_approval_bundle', true),
    ('00000000-0000-0000-0000-0000000000eb', 'export_sftp_response_port_generic',     'response_port', 'export_sftp_bundle',    true)
ON CONFLICT (policy_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- Abstract function manifests for file-storage operations (af01-af07).
-- These rows are the primary authority surface: all 7 fs_* domain mutations route
-- through execute_abstract_function → call_postgres_function primitive (not execute_db_function).
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af01', 'file_storage.record_export_job', 'external_port_runtime', 'file_storage_bundle', '{"result":"ExportJobId"}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af02', 'file_storage.record_file_artifact', 'external_port_runtime', 'file_storage_bundle', '{"result":"FileArtifactId","step2_result":"OutputProp"}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af03', 'file_storage.write_manifest_record', 'external_port_runtime', 'file_storage_bundle', '{"result":"ManifestId"}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af04', 'file_storage.authorize_signed_download', 'external_port_runtime', 'file_storage_bundle', '{"result":"AuthorizationKey","step2_result":"OutputProp"}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    -- Attachment operations: authority_scope matches required_by_bundle of attachment port records.
    -- record_table_ref is manifest-authority (step_config), not payload-derived.
    ('00000000-0000-0000-0000-00000000af05', 'file_storage.bind_record_file_attachment',   'external_port_runtime', 'file_storage_attachment_bind',   '{"step1_result":"AttachmentBindingId","step2_result":"OutputProp"}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af06', 'file_storage.list_record_file_attachments',  'external_port_runtime', 'file_storage_attachment_list',   '{"result":"OutputProp"}',                                           ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af07', 'file_storage.unbind_record_file_attachment', 'external_port_runtime', 'file_storage_attachment_unbind', '{"step1_result":"RemovedCount","step2_result":"OutputProp"}',        ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true)
ON CONFLICT (abstract_function_id) DO UPDATE SET output_shape = EXCLUDED.output_shape;

INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf01', '00000000-0000-0000-0000-00000000af01', 1, 'call_postgres_function', '{"function":"topology.fs_record_export_job","required_table_authority":"topology.export_jobs","arguments":["idempotency_key","required_by_bundle","port_id","port_kind","requested_by","export_format","period"]}', 'ExportJobId', true),
    ('00000000-0000-0000-0000-00000000bf02', '00000000-0000-0000-0000-00000000af02', 1, 'call_postgres_function', '{"function":"topology.fs_record_file_artifact","required_table_authority":"topology.file_artifacts","arguments":["export_job_id","file_name","file_type","storage_ref","checksum_value"]}', 'FileArtifactId', true),
    ('00000000-0000-0000-0000-00000000bf03', '00000000-0000-0000-0000-00000000af03', 1, 'call_postgres_function', '{"function":"topology.fs_write_manifest_record","required_table_authority":"topology.export_manifests","arguments":["export_job_id","file_artifact_id","requested_by","period","export_format","checksum_value"]}', 'ManifestId', true),
    ('00000000-0000-0000-0000-00000000bf04', '00000000-0000-0000-0000-00000000af04', 1, 'call_postgres_function', '{"function":"topology.fs_authorize_signed_download","required_table_authority":"topology.signed_download_authorizations","arguments":["file_artifact_id","requested_by"]}', 'AuthorizationKey', true),
    -- bind: step 1 calls postgres function (record_table_ref from step_config), step 2 projects result to OutputProp
    ('00000000-0000-0000-0000-00000000bf05', '00000000-0000-0000-0000-00000000af05', 1, 'call_postgres_function', '{"function":"topology.fs_bind_record_file_attachment","required_table_authority":"topology.record_file_attachments","record_table_ref":"topology.export_jobs","arguments":["record_table_ref","record_id","file_artifact_id","relation_kind","created_by"]}', 'AttachmentBindingId', true),
    ('00000000-0000-0000-0000-00000000bf06', '00000000-0000-0000-0000-00000000af05', 2, 'projection',             '{}',                                                                                                                                                                                                                                                  'OutputProp',          true),
    -- list: single step; postgres function returns JSONB stored directly as OutputProp
    ('00000000-0000-0000-0000-00000000bf07', '00000000-0000-0000-0000-00000000af06', 1, 'call_postgres_function', '{"function":"topology.fs_list_record_file_attachments","required_table_authority":"topology.record_file_attachments","record_table_ref":"topology.export_jobs","arguments":["record_table_ref","record_id"]}',                                        'OutputProp',          true),
    -- unbind: step 1 calls postgres function, step 2 projects result to OutputProp
    ('00000000-0000-0000-0000-00000000bf08', '00000000-0000-0000-0000-00000000af07', 1, 'call_postgres_function', '{"function":"topology.fs_unbind_record_file_attachment","required_table_authority":"topology.record_file_attachments","record_table_ref":"topology.export_jobs","arguments":["record_table_ref","record_id","file_artifact_id","relation_kind"]}',   'RemovedCount',        true),
    ('00000000-0000-0000-0000-00000000bf09', '00000000-0000-0000-0000-00000000af07', 2, 'projection',             '{}',                                                                                                                                                                                                                                                  'OutputProp',          true)
ON CONFLICT (abstract_function_id, step_order) DO NOTHING;

-- Projection steps for af02 (record_file_artifact) and af04 (authorize_signed_download).
-- These add step 2 to each manifest: collects non-secret output fields into OutputProp for SSE broadcast.
-- storage_ref / signed_url / credential are absent from bindings and blocked by projection_deny_keys.
INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf0a', '00000000-0000-0000-0000-00000000af02', 2, 'projection', '{}', 'OutputProp', true),
    ('00000000-0000-0000-0000-00000000bf0b', '00000000-0000-0000-0000-00000000af04', 2, 'projection', '{}', 'OutputProp', true)
ON CONFLICT (abstract_function_id, step_order) DO NOTHING;


INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c001', '00000000-0000-0000-0000-00000000bf01', 'idempotency_key', 'payload', 'idempotency_key', true, false, true),
    ('00000000-0000-0000-0000-00000000c002', '00000000-0000-0000-0000-00000000bf01', 'required_by_bundle', 'external_context', 'required_by_bundle', true, false, true),
    ('00000000-0000-0000-0000-00000000c003', '00000000-0000-0000-0000-00000000bf01', 'port_id', 'external_context', 'port_id', false, false, true),
    ('00000000-0000-0000-0000-00000000c004', '00000000-0000-0000-0000-00000000bf01', 'port_kind', 'external_context', 'port_kind', true, false, true),
    ('00000000-0000-0000-0000-00000000c005', '00000000-0000-0000-0000-00000000bf01', 'requested_by', 'payload', 'requested_by', true, false, true),
    ('00000000-0000-0000-0000-00000000c006', '00000000-0000-0000-0000-00000000bf01', 'export_format', 'payload', 'export_format', false, false, true),
    ('00000000-0000-0000-0000-00000000c007', '00000000-0000-0000-0000-00000000bf01', 'period', 'payload', 'period', false, false, true),
    ('00000000-0000-0000-0000-00000000c008', '00000000-0000-0000-0000-00000000bf02', 'export_job_id', 'external_context', 'export_job_id', true, false, true),
    ('00000000-0000-0000-0000-00000000c009', '00000000-0000-0000-0000-00000000bf02', 'file_name', 'payload', 'file_name', true, false, true),
    ('00000000-0000-0000-0000-00000000c00a', '00000000-0000-0000-0000-00000000bf02', 'file_type', 'payload', 'file_type', true, false, true),
    ('00000000-0000-0000-0000-00000000c00b', '00000000-0000-0000-0000-00000000bf02', 'storage_ref', 'external_context', 'storage_ref', true, true, true),
    ('00000000-0000-0000-0000-00000000c00c', '00000000-0000-0000-0000-00000000bf02', 'checksum_value', 'external_context', 'checksum_value', true, false, true),
    ('00000000-0000-0000-0000-00000000c00d', '00000000-0000-0000-0000-00000000bf03', 'export_job_id', 'external_context', 'export_job_id', true, false, true),
    ('00000000-0000-0000-0000-00000000c00e', '00000000-0000-0000-0000-00000000bf03', 'file_artifact_id', 'external_context', 'file_artifact_id', true, false, true),
    ('00000000-0000-0000-0000-00000000c00f', '00000000-0000-0000-0000-00000000bf03', 'requested_by', 'payload', 'requested_by', true, false, true),
    ('00000000-0000-0000-0000-00000000c010', '00000000-0000-0000-0000-00000000bf03', 'period', 'payload', 'period', false, false, true),
    ('00000000-0000-0000-0000-00000000c011', '00000000-0000-0000-0000-00000000bf03', 'export_format', 'payload', 'export_format', false, false, true),
    ('00000000-0000-0000-0000-00000000c012', '00000000-0000-0000-0000-00000000bf03', 'checksum_value', 'external_context', 'checksum_value', false, false, true),
    ('00000000-0000-0000-0000-00000000c013', '00000000-0000-0000-0000-00000000bf04', 'file_artifact_id', 'external_context', 'file_artifact_id', true,  false, true),
    ('00000000-0000-0000-0000-00000000c014', '00000000-0000-0000-0000-00000000bf04', 'requested_by',    'payload',           'requested_by',    true,  false, true),
    -- bind step 1 (bf05): record_table_ref from step_config (manifest-authority, not payload)
    ('00000000-0000-0000-0000-00000000c015', '00000000-0000-0000-0000-00000000bf05', 'record_table_ref', 'step_config', 'record_table_ref', true,  false, true),
    ('00000000-0000-0000-0000-00000000c016', '00000000-0000-0000-0000-00000000bf05', 'record_id',        'payload',     'record_id',        true,  false, true),
    ('00000000-0000-0000-0000-00000000c017', '00000000-0000-0000-0000-00000000bf05', 'file_artifact_id', 'payload',     'file_artifact_id', true,  false, true),
    ('00000000-0000-0000-0000-00000000c018', '00000000-0000-0000-0000-00000000bf05', 'relation_kind',    'constant',    'attachment',       false, false, true),
    ('00000000-0000-0000-0000-00000000c019', '00000000-0000-0000-0000-00000000bf05', 'created_by',       'payload',     'requested_by',     false, false, true),
    -- bind step 2 (bf06): projection reads AttachmentBindingId from result_context → OutputProp
    ('00000000-0000-0000-0000-00000000c01a', '00000000-0000-0000-0000-00000000bf06', 'attachment_binding_id', 'result_context', 'AttachmentBindingId', true, false, true),
    -- list step 1 (bf07): record_table_ref from step_config
    ('00000000-0000-0000-0000-00000000c01b', '00000000-0000-0000-0000-00000000bf07', 'record_table_ref', 'step_config', 'record_table_ref', true, false, true),
    ('00000000-0000-0000-0000-00000000c01c', '00000000-0000-0000-0000-00000000bf07', 'record_id',        'payload',     'record_id',        true, false, true),
    -- unbind step 1 (bf08): record_table_ref from step_config
    ('00000000-0000-0000-0000-00000000c01d', '00000000-0000-0000-0000-00000000bf08', 'record_table_ref', 'step_config', 'record_table_ref', true,  false, true),
    ('00000000-0000-0000-0000-00000000c01e', '00000000-0000-0000-0000-00000000bf08', 'record_id',        'payload',     'record_id',        true,  false, true),
    ('00000000-0000-0000-0000-00000000c01f', '00000000-0000-0000-0000-00000000bf08', 'file_artifact_id', 'payload',     'file_artifact_id', true,  false, true),
    ('00000000-0000-0000-0000-00000000c020', '00000000-0000-0000-0000-00000000bf08', 'relation_kind',    'constant',    'attachment',       false, false, true),
    -- unbind step 2 (bf09): projection reads RemovedCount from result_context → OutputProp
    ('00000000-0000-0000-0000-00000000c021', '00000000-0000-0000-0000-00000000bf09', 'removed_count', 'result_context', 'RemovedCount', true, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

-- Input bindings for bf0a (af02 projection) and bf0b (af04 projection).
-- Projects non-secret artifact metadata and opaque authorization reference only.
-- storage_ref / signed_url / credential are absent — blocked at the binding level and by projection_deny_keys.
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    -- af02 projection step (bf0a): file_artifact_id from result_context, file_name/file_type from payload
    ('00000000-0000-0000-0000-00000000c022', '00000000-0000-0000-0000-00000000bf0a', 'file_artifact_id', 'result_context',   'FileArtifactId', true,  false, true),
    ('00000000-0000-0000-0000-00000000c023', '00000000-0000-0000-0000-00000000bf0a', 'file_name',        'payload',          'file_name',      true,  false, true),
    ('00000000-0000-0000-0000-00000000c024', '00000000-0000-0000-0000-00000000bf0a', 'file_type',        'payload',          'file_type',      false, false, true),
    -- af04 projection step (bf0b): authorization_key (opaque reference) from result_context, file_artifact_id from external_context
    ('00000000-0000-0000-0000-00000000c025', '00000000-0000-0000-0000-00000000bf0b', 'authorization_key', 'result_context',  'AuthorizationKey', true,  false, true),
    ('00000000-0000-0000-0000-00000000c026', '00000000-0000-0000-0000-00000000bf0b', 'file_artifact_id',  'external_context', 'file_artifact_id', false, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    ('00000000-0000-0000-0000-00000000af01', 'policy', 'file_storage_access_port_generic',  true),
    ('00000000-0000-0000-0000-00000000af02', 'policy', 'file_storage_access_port_generic',  true),
    ('00000000-0000-0000-0000-00000000af03', 'policy', 'file_storage_access_port_generic',  true),
    ('00000000-0000-0000-0000-00000000af04', 'policy', 'file_storage_access_port_generic',  true),
    ('00000000-0000-0000-0000-00000000af01', 'table',  'topology.export_jobs',              true),
    ('00000000-0000-0000-0000-00000000af02', 'table',  'topology.file_artifacts',           true),
    ('00000000-0000-0000-0000-00000000af03', 'table',  'topology.export_manifests',         true),
    ('00000000-0000-0000-0000-00000000af04', 'table',  'topology.signed_download_authorizations', true),
    -- Attachment manifests: authority_scope and policy_key match the attachment port required_by_bundle
    ('00000000-0000-0000-0000-00000000af05', 'policy', 'file_storage_attachment_bind',   true),
    ('00000000-0000-0000-0000-00000000af05', 'table',  'topology.record_file_attachments', true),
    ('00000000-0000-0000-0000-00000000af06', 'policy', 'file_storage_attachment_list',   true),
    ('00000000-0000-0000-0000-00000000af06', 'table',  'topology.record_file_attachments', true),
    ('00000000-0000-0000-0000-00000000af07', 'policy', 'file_storage_attachment_unbind', true),
    ('00000000-0000-0000-0000-00000000af07', 'table',  'topology.record_file_attachments', true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Abstract function manifest for SQL Attention projection (af08).
-- sql_attention.list_projection routes through admin_runtime (read-only projection).
-- Runtime lane: admin_runtime — not external_port_runtime.
-- Authority scope: admin_sql_attention.
-- Primitive: sql_attention — function_name and parameter_key from step_config (manifest-authority).
-- Input: source_set_id bound from payload.sourceSetId.
-- Table authority: logs.attention (read-only; no write to evidence layer).
-- Policy authority: admin_sql_attention_projection.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af08', 'sql_attention.list_projection', 'admin_runtime', 'admin_sql_attention', '{"sql_attention_result":"projection_result"}', ARRAY[]::text[], true)
ON CONFLICT (abstract_function_id) DO NOTHING;

INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf10', '00000000-0000-0000-0000-00000000af08', 1, 'sql_attention',
     '{"function_name":"sql_attention_topology_projection","parameter_key":"default_policy"}',
     'projection_result', true)
ON CONFLICT (abstract_function_id, step_order) DO NOTHING;

INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c030', '00000000-0000-0000-0000-00000000bf10', 'source_set_id', 'payload', 'sourceSetId', true, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    ('00000000-0000-0000-0000-00000000af08', 'policy', 'admin_sql_attention_projection', true),
    ('00000000-0000-0000-0000-00000000af08', 'table',  'logs.attention',                 true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO NOTHING;

-- Abstract function manifest for context route recommendation (af09).
-- Routes through runtime_executor lane with 4-step primitive decomposition:
--   recommendation_candidate_source → recommendation_eligibility →
--   recommendation_score_rank → recommendation_projection
-- ContextRouteRecommendationResolver methods are called per phase via primitive adapters.
-- Lane enforcement: recommendation_projection fails closed on sql_attention_projection mixing.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af09', 'context_route.recommendation_resolve', 'runtime_executor', 'context_route_recommendation', '{"recommendation_source_step":"recommendation_source","recommendation_eligibility_step":"recommendation_eligibility","recommendation_score_rank_step":"recommendation_score_rank","context_route_result":"recommendation_result"}', ARRAY[]::text[], true)
ON CONFLICT (abstract_function_id) DO NOTHING;

INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf11', '00000000-0000-0000-0000-00000000af09', 1, 'recommendation_candidate_source',
     '{"function_name":"context_route_recommendation_resolve","parameter_key":"default_policy"}',
     'recommendation_source', true),
    ('00000000-0000-0000-0000-00000000bf12', '00000000-0000-0000-0000-00000000af09', 2, 'recommendation_eligibility',
     '{}',
     'recommendation_eligibility', true),
    ('00000000-0000-0000-0000-00000000bf13', '00000000-0000-0000-0000-00000000af09', 3, 'recommendation_score_rank',
     '{}',
     'recommendation_score_rank', true),
    ('00000000-0000-0000-0000-00000000bf14', '00000000-0000-0000-0000-00000000af09', 4, 'recommendation_projection',
     '{}',
     'recommendation_result', true)
ON CONFLICT (abstract_function_id, step_order) DO NOTHING;

INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    -- bf11 (recommendation_candidate_source): working_shape from runtime_context
    ('00000000-0000-0000-0000-00000000c031', '00000000-0000-0000-0000-00000000bf11', 'working_shape',              'runtime_context', 'working_shape',              true,  false, true),
    -- bf12 (recommendation_eligibility): working_shape + recommendation_source
    ('00000000-0000-0000-0000-00000000c032', '00000000-0000-0000-0000-00000000bf12', 'working_shape',              'runtime_context', 'working_shape',              true,  false, true),
    ('00000000-0000-0000-0000-00000000c033', '00000000-0000-0000-0000-00000000bf12', 'recommendation_source',      'result_context',  'recommendation_source',      true,  false, true),
    -- bf13 (recommendation_score_rank): working_shape + recommendation_source + recommendation_eligibility
    ('00000000-0000-0000-0000-00000000c034', '00000000-0000-0000-0000-00000000bf13', 'working_shape',              'runtime_context', 'working_shape',              true,  false, true),
    ('00000000-0000-0000-0000-00000000c035', '00000000-0000-0000-0000-00000000bf13', 'recommendation_source',      'result_context',  'recommendation_source',      true,  false, true),
    ('00000000-0000-0000-0000-00000000c036', '00000000-0000-0000-0000-00000000bf13', 'recommendation_eligibility', 'result_context',  'recommendation_eligibility', true,  false, true),
    -- bf14 (recommendation_projection): recommendation_source + eligibility + score_rank
    ('00000000-0000-0000-0000-00000000c037', '00000000-0000-0000-0000-00000000bf14', 'recommendation_source',      'result_context',  'recommendation_source',      true,  false, true),
    ('00000000-0000-0000-0000-00000000c038', '00000000-0000-0000-0000-00000000bf14', 'recommendation_eligibility', 'result_context',  'recommendation_eligibility', true,  false, true),
    ('00000000-0000-0000-0000-00000000c039', '00000000-0000-0000-0000-00000000bf14', 'recommendation_score_rank',  'result_context',  'recommendation_score_rank',  true,  false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    ('00000000-0000-0000-0000-00000000af09', 'policy', 'context_route_recommendation_resolve', true),
    ('00000000-0000-0000-0000-00000000af09', 'table',  'context_route.context_hub_recommendation_current', true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO NOTHING;

-- Consumer bundle policy steps (operation_key values constrained to external-port SSOT allowed set)
-- file_storage steps use DELETE+INSERT to allow re-seeding with updated credential pipeline (17 steps)
DELETE FROM topology.external_port_policy_steps
WHERE policy_id IN (
    '00000000-0000-0000-0000-0000000000e4',
    '00000000-0000-0000-0000-0000000000e5',
    '00000000-0000-0000-0000-0000000000ed',
    '00000000-0000-0000-0000-0000000000ee',
    '00000000-0000-0000-0000-0000000000ef',
    '00000000-0000-0000-0000-0000000000e6',
    '00000000-0000-0000-0000-0000000000e7',
    '00000000-0000-0000-0000-0000000000e8',
    '00000000-0000-0000-0000-0000000000e9',
    '00000000-0000-0000-0000-0000000000ec',
    '00000000-0000-0000-0000-0000000000ea',
    '00000000-0000-0000-0000-0000000000eb'
);

INSERT INTO topology.external_port_policy_steps (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    -- file_storage_bundle access_port (17 steps: credential pipeline + compute_checksum + 4x execute_abstract_function interleaved with 4x append_runtime_event_log)
    ('00000000-0000-0000-0000-000000000401', '00000000-0000-0000-0000-0000000000e4',  1, 'resolve_port_record',              '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000402', '00000000-0000-0000-0000-0000000000e4',  2, 'resolve_credential_reference',     '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000403', '00000000-0000-0000-0000-0000000000e4',  3, 'load_encrypted_credential_payload','{}', NULL, true),
    ('00000000-0000-0000-0000-000000000404', '00000000-0000-0000-0000-0000000000e4',  4, 'decrypt_for_runtime_use',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000405', '00000000-0000-0000-0000-0000000000e4',  5, 'build_http_request',               '{"method":"PUT"}', NULL, true),
    ('00000000-0000-0000-0000-0000000004a0', '00000000-0000-0000-0000-0000000000e4',  6, 'inject_authorization_header',      '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004a1', '00000000-0000-0000-0000-0000000000e4',  7, 'send_http',                        '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004a2', '00000000-0000-0000-0000-0000000000e4',  8, 'capture_response',                 '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000407', '00000000-0000-0000-0000-0000000000e4',  9, 'compute_checksum',                 '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000490', '00000000-0000-0000-0000-0000000000e4', 10, 'append_runtime_event_log', '{"event_type":"checksum_verified","entity_ref_key":"checksum_value"}', NULL, true),
    ('00000000-0000-0000-0000-000000000406', '00000000-0000-0000-0000-0000000000e4', 11, 'execute_abstract_function', '{}', 'file_storage.record_export_job', true),
    ('00000000-0000-0000-0000-000000000491', '00000000-0000-0000-0000-0000000000e4', 12, 'append_runtime_event_log', '{"event_type":"export_job_initiated","entity_ref_key":"export_job_id"}', NULL, true),
    ('00000000-0000-0000-0000-000000000408', '00000000-0000-0000-0000-0000000000e4', 13, 'execute_abstract_function', '{}', 'file_storage.record_file_artifact', true),
    ('00000000-0000-0000-0000-000000000492', '00000000-0000-0000-0000-0000000000e4', 14, 'append_runtime_event_log', '{"event_type":"file_write_completed","entity_ref_key":"file_artifact_id"}', NULL, true),
    ('00000000-0000-0000-0000-000000000409', '00000000-0000-0000-0000-0000000000e4', 15, 'execute_abstract_function', '{}', 'file_storage.write_manifest_record', true),
    ('00000000-0000-0000-0000-000000000410', '00000000-0000-0000-0000-0000000000e4', 16, 'execute_abstract_function', '{}', 'file_storage.authorize_signed_download', true),
    ('00000000-0000-0000-0000-000000000493', '00000000-0000-0000-0000-0000000000e4', 17, 'append_runtime_event_log', '{"event_type":"signed_url_generated","entity_ref_key":"authorization_key"}', NULL, true),
    -- file_storage_bundle response_port (17 steps: credential pipeline + compute_checksum + 4x execute_abstract_function interleaved with 4x append_runtime_event_log)
    ('00000000-0000-0000-0000-000000000411', '00000000-0000-0000-0000-0000000000e5',  1, 'resolve_port_record',              '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000412', '00000000-0000-0000-0000-0000000000e5',  2, 'resolve_credential_reference',     '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000413', '00000000-0000-0000-0000-0000000000e5',  3, 'load_encrypted_credential_payload','{}', NULL, true),
    ('00000000-0000-0000-0000-000000000414', '00000000-0000-0000-0000-0000000000e5',  4, 'decrypt_for_runtime_use',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000415', '00000000-0000-0000-0000-0000000000e5',  5, 'build_http_request',               '{"method":"PUT"}', NULL, true),
    ('00000000-0000-0000-0000-0000000004b0', '00000000-0000-0000-0000-0000000000e5',  6, 'inject_authorization_header',      '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004b1', '00000000-0000-0000-0000-0000000000e5',  7, 'send_http',                        '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004b2', '00000000-0000-0000-0000-0000000000e5',  8, 'capture_response',                 '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000417', '00000000-0000-0000-0000-0000000000e5',  9, 'compute_checksum',                 '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000494', '00000000-0000-0000-0000-0000000000e5', 10, 'append_runtime_event_log', '{"event_type":"checksum_verified","entity_ref_key":"checksum_value"}', NULL, true),
    ('00000000-0000-0000-0000-000000000416', '00000000-0000-0000-0000-0000000000e5', 11, 'execute_abstract_function', '{}', 'file_storage.record_export_job', true),
    ('00000000-0000-0000-0000-000000000495', '00000000-0000-0000-0000-0000000000e5', 12, 'append_runtime_event_log', '{"event_type":"export_job_initiated","entity_ref_key":"export_job_id"}', NULL, true),
    ('00000000-0000-0000-0000-000000000418', '00000000-0000-0000-0000-0000000000e5', 13, 'execute_abstract_function', '{}', 'file_storage.record_file_artifact', true),
    ('00000000-0000-0000-0000-000000000496', '00000000-0000-0000-0000-0000000000e5', 14, 'append_runtime_event_log', '{"event_type":"file_write_completed","entity_ref_key":"file_artifact_id"}', NULL, true),
    ('00000000-0000-0000-0000-000000000419', '00000000-0000-0000-0000-0000000000e5', 15, 'execute_abstract_function', '{}', 'file_storage.write_manifest_record', true),
    ('00000000-0000-0000-0000-000000000420', '00000000-0000-0000-0000-0000000000e5', 16, 'execute_abstract_function', '{}', 'file_storage.authorize_signed_download', true),
    ('00000000-0000-0000-0000-000000000497', '00000000-0000-0000-0000-0000000000e5', 17, 'append_runtime_event_log', '{"event_type":"signed_url_generated","entity_ref_key":"authorization_key"}', NULL, true),
    -- file_storage attachment response_port policies: execute_abstract_function with manifest-authority record_table_ref
    ('00000000-0000-0000-0000-0000000004c1', '00000000-0000-0000-0000-0000000000ed', 1, 'resolve_port_record',       '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004c2', '00000000-0000-0000-0000-0000000000ed', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004c3', '00000000-0000-0000-0000-0000000000ed', 3, 'execute_abstract_function', '{}', 'file_storage.bind_record_file_attachment',   true),
    ('00000000-0000-0000-0000-0000000004d1', '00000000-0000-0000-0000-0000000000ee', 1, 'resolve_port_record',       '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004d2', '00000000-0000-0000-0000-0000000000ee', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004d3', '00000000-0000-0000-0000-0000000000ee', 3, 'execute_abstract_function', '{}', 'file_storage.list_record_file_attachments',  true),
    ('00000000-0000-0000-0000-0000000004e1', '00000000-0000-0000-0000-0000000000ef', 1, 'resolve_port_record',       '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004e2', '00000000-0000-0000-0000-0000000000ef', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004e3', '00000000-0000-0000-0000-0000000000ef', 3, 'execute_abstract_function', '{}', 'file_storage.unbind_record_file_attachment', true),
    -- email_bundle response_port
    ('00000000-0000-0000-0000-000000000421', '00000000-0000-0000-0000-0000000000e6', 1, 'resolve_port_record',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000422', '00000000-0000-0000-0000-0000000000e6', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000423', '00000000-0000-0000-0000-0000000000e6', 3, 'load_encrypted_credential_payload', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000424', '00000000-0000-0000-0000-0000000000e6', 4, 'decrypt_for_runtime_use',      '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000425', '00000000-0000-0000-0000-0000000000e6', 5, 'build_http_request',           '{"method":"POST"}', NULL, true),
    ('00000000-0000-0000-0000-000000000426', '00000000-0000-0000-0000-0000000000e6', 6, 'inject_authorization_header',  '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000427', '00000000-0000-0000-0000-0000000000e6', 7, 'send_http',                    '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000428', '00000000-0000-0000-0000-0000000000e6', 8, 'capture_response',             '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000429', '00000000-0000-0000-0000-0000000000e6', 9, 'append_runtime_event_log',     '{"event_type":"send_success","evidence_table_ref":"topology.email_delivery_evidence","projection_table_ref":"topology.email_delivery_evidence","status_value":"delivered"}', NULL, true),
    -- stripe_bundle hook_port
    ('00000000-0000-0000-0000-000000000431', '00000000-0000-0000-0000-0000000000e7', 1, 'resolve_port_record',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000432', '00000000-0000-0000-0000-0000000000e7', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000433', '00000000-0000-0000-0000-0000000000e7', 3, 'verify_signature_by_config',   '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000434', '00000000-0000-0000-0000-0000000000e7', 4, 'enqueue_scheduler_event',      '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000435', '00000000-0000-0000-0000-0000000000e7', 5, 'append_runtime_event_log',     '{"event_type":"webhook_received","evidence_table_ref":"topology.webhook_intake_snapshots","projection_table_ref":"topology.webhook_intake_snapshots","status_value":"scheduler_enqueued"}', NULL, true),
    -- webhook_inbox_bundle hook_port
    ('00000000-0000-0000-0000-000000000441', '00000000-0000-0000-0000-0000000000e8', 1, 'resolve_port_record',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000442', '00000000-0000-0000-0000-0000000000e8', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000443', '00000000-0000-0000-0000-0000000000e8', 3, 'verify_signature_by_config',   '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000444', '00000000-0000-0000-0000-0000000000e8', 4, 'enqueue_scheduler_event',      '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000445', '00000000-0000-0000-0000-0000000000e8', 5, 'append_runtime_event_log',     '{"event_type":"scheduler_enqueued","evidence_table_ref":"topology.webhook_intake_snapshots","projection_table_ref":"topology.webhook_intake_snapshots","status_value":"scheduler_enqueued"}', NULL, true),
    -- job_scheduler_bundle access_port
    ('00000000-0000-0000-0000-000000000451', '00000000-0000-0000-0000-0000000000e9', 1, 'resolve_port_record',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000452', '00000000-0000-0000-0000-0000000000e9', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000453', '00000000-0000-0000-0000-0000000000e9', 3, 'append_runtime_event_log',     '{"event_type":"trigger_received","evidence_table_ref":"topology.scheduler_external_event_evidence","projection_table_ref":"topology.scheduler_external_event_evidence","status_value":"trigger_received"}', NULL, true),
    -- job_scheduler_bundle hook_port (credential_kind = none; resolve_credential_reference skipped)
    ('00000000-0000-0000-0000-000000000481', '00000000-0000-0000-0000-0000000000ec', 1, 'resolve_port_record',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000482', '00000000-0000-0000-0000-0000000000ec', 2, 'enqueue_scheduler_event',      '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000483', '00000000-0000-0000-0000-0000000000ec', 3, 'append_runtime_event_log',     '{"event_type":"scheduler_enqueued","evidence_table_ref":"topology.scheduler_external_event_evidence","projection_table_ref":"topology.scheduler_external_event_evidence","status_value":"scheduler_enqueued"}', NULL, true),
    -- audit_approval_bundle response_port
    ('00000000-0000-0000-0000-000000000461', '00000000-0000-0000-0000-0000000000ea', 1, 'resolve_port_record',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000462', '00000000-0000-0000-0000-0000000000ea', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000463', '00000000-0000-0000-0000-0000000000ea', 3, 'load_encrypted_credential_payload', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000464', '00000000-0000-0000-0000-0000000000ea', 4, 'decrypt_for_runtime_use',      '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000465', '00000000-0000-0000-0000-0000000000ea', 5, 'build_http_request',           '{"method":"POST"}', NULL, true),
    ('00000000-0000-0000-0000-000000000466', '00000000-0000-0000-0000-0000000000ea', 6, 'inject_authorization_header',  '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000467', '00000000-0000-0000-0000-0000000000ea', 7, 'send_http',                    '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000468', '00000000-0000-0000-0000-0000000000ea', 8, 'capture_response',             '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000469', '00000000-0000-0000-0000-0000000000ea', 9, 'append_runtime_event_log',     '{"event_type":"approval_reviewed","evidence_table_ref":"topology.audit_approval_evidence","projection_table_ref":"topology.audit_approval_evidence","status_value":"reviewed"}', NULL, true),
    -- export_sftp_bundle response_port
    ('00000000-0000-0000-0000-000000000471', '00000000-0000-0000-0000-0000000000eb', 1, 'resolve_port_record',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000472', '00000000-0000-0000-0000-0000000000eb', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000473', '00000000-0000-0000-0000-0000000000eb', 3, 'load_encrypted_credential_payload', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000474', '00000000-0000-0000-0000-0000000000eb', 4, 'decrypt_for_runtime_use',      '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000475', '00000000-0000-0000-0000-0000000000eb', 5, 'build_http_request',           '{"method":"POST"}', NULL, true),
    ('00000000-0000-0000-0000-000000000476', '00000000-0000-0000-0000-0000000000eb', 6, 'inject_authorization_header',  '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000477', '00000000-0000-0000-0000-0000000000eb', 7, 'send_http',                    '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000478', '00000000-0000-0000-0000-0000000000eb', 8, 'capture_response',             '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000479', '00000000-0000-0000-0000-0000000000eb', 9, 'record_transfer_lifecycle_evidence', '{"evidence_table_ref":"topology.sftp_transfer_log","projection_table_ref":"topology.sftp_transfer_log","initiated_event_type":"transfer_initiated","completed_event_type":"transfer_completed","failed_event_type":"transfer_failed","checksum_mismatch_event_type":"checksum_mismatch","retry_event_type":"retry_attempted","requires_export_job":"true","requires_manifest":"true","requires_checksum":"true","retry_policy_ref":"scheduler_retry_requested","retry_trigger_target":"external-port:response_port:00000000-0000-0000-0000-000000000f08"}', NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;

-- export_sftp_bundle implemented transfer lifecycle policy. Re-seed this policy
-- idempotently so older bootstrap rows with only transfer_initiated cannot mask
-- explicit failure/retry evidence. Checksum/manifest verification is recorded as
-- an independent boundary from the generic response_port credential lane.
DELETE FROM topology.external_port_policy_steps
WHERE policy_id = '00000000-0000-0000-0000-0000000000eb';

INSERT INTO topology.external_port_policy_steps (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000471', '00000000-0000-0000-0000-0000000000eb',  1, 'resolve_port_record',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000472', '00000000-0000-0000-0000-0000000000eb',  2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000473', '00000000-0000-0000-0000-0000000000eb',  3, 'load_encrypted_credential_payload', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000474', '00000000-0000-0000-0000-0000000000eb',  4, 'decrypt_for_runtime_use',      '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000475', '00000000-0000-0000-0000-0000000000eb',  5, 'build_http_request',           '{"method":"POST","requires_export_job":"true","requires_manifest":"true","requires_checksum":"true"}', NULL, true),
    ('00000000-0000-0000-0000-000000000476', '00000000-0000-0000-0000-0000000000eb',  6, 'inject_authorization_header',  '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000477', '00000000-0000-0000-0000-0000000000eb',  7, 'send_http',                    '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000478', '00000000-0000-0000-0000-0000000000eb',  8, 'capture_response',             '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000479', '00000000-0000-0000-0000-0000000000eb',  9, 'record_transfer_lifecycle_evidence', '{"evidence_table_ref":"topology.sftp_transfer_log","projection_table_ref":"topology.sftp_transfer_log","initiated_event_type":"transfer_initiated","completed_event_type":"transfer_completed","failed_event_type":"transfer_failed","checksum_mismatch_event_type":"checksum_mismatch","retry_event_type":"retry_attempted","requires_export_job":"true","requires_manifest":"true","requires_checksum":"true","retry_policy_ref":"scheduler_retry_requested","retry_trigger_target":"external-port:response_port:00000000-0000-0000-0000-000000000f08"}', NULL, true)
ON CONFLICT (policy_id, step_order) DO UPDATE
    SET operation_key = EXCLUDED.operation_key,
        step_config = EXCLUDED.step_config,
        abstract_function_key = EXCLUDED.abstract_function_key,
        active = EXCLUDED.active;

-- file_storage_bundle domain operation steps use execute_abstract_function (manifest-authority).
-- All 7 fs_* operations (record_export_job, record_file_artifact, write_manifest_record, authorize_signed_download,
-- bind/list/unbind_record_file_attachment) are expressed through abstract function manifests af01-af07.
-- record_table_ref for attachment operations comes from step_config (manifest-authority), not from payload.

-- Idempotent fix: ensure build_http_request steps in generic substrate policies have explicit method.
-- These rows were inserted with ON CONFLICT DO NOTHING, so UPDATE is needed for re-seeds.
UPDATE topology.external_port_policy_steps SET step_config = '{"method":"GET"}'  WHERE policy_step_id = '00000000-0000-0000-0000-000000000103';
UPDATE topology.external_port_policy_steps SET step_config = '{"method":"POST"}' WHERE policy_step_id = '00000000-0000-0000-0000-000000000203';
UPDATE topology.external_port_policy_steps SET step_config = '{"method":"POST"}' WHERE policy_step_id = '00000000-0000-0000-0000-000000000425';
UPDATE topology.external_port_policy_steps SET step_config = '{"method":"POST"}' WHERE policy_step_id = '00000000-0000-0000-0000-000000000463';

-- webhook_inbox_bundle: expand policy e8 from 5 to 12 steps for full SSOT audit_log chain.
-- intake_snapshot→validate→preview→explicit_apply→canonical_runtime_route boundary expressed in event log.
-- Renumber existing steps to high step_orders first (reverse order avoids UNIQUE conflicts).
-- Step 3 (verify_signature_by_config, ID 443) stays at step_order=3 (no UPDATE needed).
UPDATE topology.external_port_policy_steps SET step_order = 12 WHERE policy_step_id = '00000000-0000-0000-0000-000000000445';
UPDATE topology.external_port_policy_steps SET step_order =  9 WHERE policy_step_id = '00000000-0000-0000-0000-000000000444';
-- Step 4: signature_verification_success evidence (only after verify_signature passes)
INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000449', '00000000-0000-0000-0000-0000000000e8', 4, 'append_runtime_event_log',
     '{"event_type":"signature_verification_success","evidence_table_ref":"topology.signature_verification_evidence","projection_table_ref":"topology.signature_verification_evidence","status_value":"verified"}',
     NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;
-- Step 5: intake snapshot write (webhook_received) — only after signature verified
INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000448', '00000000-0000-0000-0000-0000000000e8', 5, 'append_runtime_event_log',
     '{"event_type":"webhook_received","evidence_table_ref":"topology.webhook_intake_snapshots","projection_table_ref":"topology.webhook_intake_snapshots","status_value":"received"}',
     NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;
-- Step 6: intake_snapshot_created — canonical record that verified snapshot is created
INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000450', '00000000-0000-0000-0000-0000000000e8', 6, 'append_runtime_event_log',
     '{"event_type":"intake_snapshot_created","evidence_table_ref":"topology.webhook_intake_snapshots","status_value":"snapshot_created"}',
     NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;
-- Step 7: validation_completed — validate→preview→explicit_apply boundary (SSOT approval_boundary)
INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000455', '00000000-0000-0000-0000-0000000000e8', 7, 'append_runtime_event_log',
     '{"event_type":"validation_completed","evidence_table_ref":"topology.webhook_intake_snapshots","status_value":"validated"}',
     NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;
-- Step 8: preview_generated — preview boundary before explicit_apply
INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000456', '00000000-0000-0000-0000-0000000000e8', 8, 'append_runtime_event_log',
     '{"event_type":"preview_generated","evidence_table_ref":"topology.webhook_intake_snapshots","status_value":"preview_ready"}',
     NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;
-- Step 9: enqueue_scheduler_event — explicit_apply trigger (scheduler_then_runtime_route)
-- (policy_step_id 444, was original step 4; renumbered to 9 above)
-- Step 10: explicit_apply_initiated — audit evidence that scheduler apply was initiated
INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000457', '00000000-0000-0000-0000-0000000000e8', 10, 'append_runtime_event_log',
     '{"event_type":"explicit_apply_initiated","evidence_table_ref":"topology.webhook_intake_snapshots","status_value":"apply_initiated"}',
     NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;
-- Step 11: apply_completed — scheduler enqueue succeeded; intake-side apply is complete
INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000454', '00000000-0000-0000-0000-0000000000e8', 11, 'append_runtime_event_log',
     '{"event_type":"apply_completed","evidence_table_ref":"topology.webhook_intake_snapshots","status_value":"apply_completed"}',
     NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;
-- Step 12: scheduler_enqueued — final confirmation evidence
-- (policy_step_id 445, was original step 5; renumbered to 12 above)

-- ---------------------------------------------------------------------------
-- Abstract function manifest for credential token refresh (af10).
-- credential.refresh_token routes through external_port_runtime with 6 primitive steps:
--   credential_acquire_lease → credential_http_request → credential_compute_token_hash
--   → credential_parse_expires_at → credential_write_vault → credential_release_lease
-- Authority scope: external_credential_vault_refresh.
-- Decrypted payload exists only inside runtime context; never enters projection/log/SSOT.
-- No provider_kind switch; provider_kind remains data only.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af10', 'credential.refresh_token', 'external_port_runtime', 'external_credential_vault_refresh',
     '{}',
     ARRAY['credential','credential_payload','decrypted_payload','plaintext_payload','decrypted_credential_payload','token_response','token_body'],
     true)
ON CONFLICT (abstract_function_id) DO NOTHING;

INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf15', '00000000-0000-0000-0000-00000000af10', 1, 'credential_acquire_lease',
     '{"lease_owner":"external_credential_vault_refresh","lease_duration_minutes":"5"}',
     'credential_lease', true),
    ('00000000-0000-0000-0000-00000000bf16', '00000000-0000-0000-0000-00000000af10', 2, 'credential_http_request',
     '{"method":"POST"}',
     'token_response', true),
    ('00000000-0000-0000-0000-00000000bf17', '00000000-0000-0000-0000-00000000af10', 3, 'credential_compute_token_hash',
     '{}',
     'token_hash', true),
    ('00000000-0000-0000-0000-00000000bf18', '00000000-0000-0000-0000-00000000af10', 4, 'credential_parse_expires_at',
     '{"expires_at_response_key":"expires_at"}',
     'token_expires_at', true),
    ('00000000-0000-0000-0000-00000000bf19', '00000000-0000-0000-0000-00000000af10', 5, 'credential_write_vault',
     '{}',
     NULL, true),
    ('00000000-0000-0000-0000-00000000bf1a', '00000000-0000-0000-0000-00000000af10', 6, 'credential_release_lease',
     '{}',
     NULL, true)
ON CONFLICT (abstract_function_id, step_order) DO NOTHING;

-- bf1b is a compensation step: runs on failure to fail the acquired lease.
-- is_compensation_step = true so the executor skips it on the success path.
INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active, is_compensation_step)
VALUES
    ('00000000-0000-0000-0000-00000000bf1b', '00000000-0000-0000-0000-00000000af10', 7, 'credential_fail_lease',
     '{"failure_code":"step_failure"}',
     NULL, true, true)
ON CONFLICT (abstract_function_id, step_order) DO NOTHING;

INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    -- bf15 (credential_acquire_lease): vault id from external_context
    ('00000000-0000-0000-0000-00000000c040', '00000000-0000-0000-0000-00000000bf15', 'credential_vault_id',         'external_context', 'credential_vault_id',         true,  false, true),
    -- bf16 (credential_http_request): decrypted payload from external_context (secret)
    ('00000000-0000-0000-0000-00000000c041', '00000000-0000-0000-0000-00000000bf16', 'decrypted_credential_payload', 'external_context', 'decrypted_credential_payload', true,  true,  true),
    -- bf17 (credential_compute_token_hash): token_response from result_context (secret)
    ('00000000-0000-0000-0000-00000000c042', '00000000-0000-0000-0000-00000000bf17', 'token_response',               'result_context',   'token_response',               true,  true,  true),
    -- bf18 (credential_parse_expires_at): token_response from result_context (secret)
    ('00000000-0000-0000-0000-00000000c043', '00000000-0000-0000-0000-00000000bf18', 'token_response',               'result_context',   'token_response',               true,  true,  true),
    -- bf19 (credential_write_vault): lease, hash, expiry (non-secret), token_response (secret)
    ('00000000-0000-0000-0000-00000000c044', '00000000-0000-0000-0000-00000000bf19', 'credential_lease',             'result_context',   'credential_lease',             true,  false, true),
    ('00000000-0000-0000-0000-00000000c045', '00000000-0000-0000-0000-00000000bf19', 'token_hash',                   'result_context',   'token_hash',                   true,  false, true),
    ('00000000-0000-0000-0000-00000000c046', '00000000-0000-0000-0000-00000000bf19', 'token_expires_at',             'result_context',   'token_expires_at',             true,  false, true),
    ('00000000-0000-0000-0000-00000000c047', '00000000-0000-0000-0000-00000000bf19', 'token_response',               'result_context',   'token_response',               true,  true,  true),
    -- bf1a (credential_release_lease): credential_lease from result_context
    ('00000000-0000-0000-0000-00000000c048', '00000000-0000-0000-0000-00000000bf1a', 'credential_lease',             'result_context',   'credential_lease',             true,  false, true),
    -- bf1b (credential_fail_lease, compensation): credential_lease from result_context (not required: if
    -- lease was never acquired the adapter returns null rather than failing the compensation step)
    ('00000000-0000-0000-0000-00000000c049', '00000000-0000-0000-0000-00000000bf1b', 'credential_lease',             'result_context',   'credential_lease',             false, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    ('00000000-0000-0000-0000-00000000af10', 'policy', 'external_credential_vault_refresh',       true),
    ('00000000-0000-0000-0000-00000000af10', 'table',  'topology.external_credential_vaults',     true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO NOTHING;


-- Instance port runtime abstract function manifests (runtime_lane = instance_port_runtime).
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af90', 'registered_instance.approved_operation', 'instance_port_runtime', 'registered_instance.approved_operation', '{"result":"InstanceResult"}', ARRAY['credential','secret','token','connection_string','endpoint','private_key'], true),
    ('00000000-0000-0000-0000-00000000af91', 'registered_instance.bound_operation', 'instance_port_runtime', 'registered_instance.bound_operation', '{"result":"InstanceResult"}', ARRAY['credential','secret','token','connection_string','endpoint','private_key'], true)
ON CONFLICT (abstract_function_id) DO NOTHING;

INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf90', '00000000-0000-0000-0000-00000000af90', 1, 'call_instance_postgres_function', '{"function_schema":"approved_schema","function_name":"approved_function","arguments":["input_ref"]}', 'InstanceResult', true),
    ('00000000-0000-0000-0000-00000000bf91', '00000000-0000-0000-0000-00000000af91', 1, 'call_bound_instance_function', '{}', 'InstanceResult', true)
ON CONFLICT (abstract_function_id, step_order) DO NOTHING;

INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000cf90', '00000000-0000-0000-0000-00000000bf90', 'input_ref', 'payload', 'input_ref', true, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    ('00000000-0000-0000-0000-00000000af90', 'instance', 'registered_instance_key', true),
    ('00000000-0000-0000-0000-00000000af90', 'instance_function', 'registered_instance.approved_operation', true),
    ('00000000-0000-0000-0000-00000000af90', 'instance_schema', 'approved_schema', true),
    ('00000000-0000-0000-0000-00000000af90', 'instance_operation', 'approved-operation-placeholder', true),
    ('00000000-0000-0000-0000-00000000af90', 'output', 'InstanceResult', true),
    ('00000000-0000-0000-0000-00000000af91', 'instance', 'registered_runtime_key', true),
    ('00000000-0000-0000-0000-00000000af91', 'instance_function', 'registered_instance.bound_operation', true),
    ('00000000-0000-0000-0000-00000000af91', 'instance_schema', 'approved_schema', true),
    ('00000000-0000-0000-0000-00000000af91', 'instance_operation', 'approved-bound-placeholder', true),
    ('00000000-0000-0000-0000-00000000af91', 'output', 'InstanceResult', true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO NOTHING;

INSERT INTO topology.instance_operation_authority_binding
    (binding_id, operation_binding_key, instance_authority_key, function_key, function_schema, function_name, abstract_function_key, output_shape, secret_deny, active)
VALUES
    ('00000000-0000-0000-0000-000000000ad1', 'approved-operation-placeholder', 'registered_instance_key', 'registered_instance.approved_operation', 'approved_schema', 'approved_function', 'registered_instance.approved_operation', '{"result":"InstanceResult"}', true, true),
    ('00000000-0000-0000-0000-000000000ad2', 'approved-bound-placeholder', 'registered_runtime_key', 'registered_instance.bound_operation', 'approved_schema', 'approved_bound_function', 'registered_instance.bound_operation', '{"result":"InstanceResult"}', true, true)
ON CONFLICT (instance_authority_key, operation_binding_key) DO NOTHING;

-- external_credential_vault_refresh_bundle: token refresh access_port
INSERT INTO topology.external_access_ports
    (access_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f10', 'external_credential_vault_refresh', 'oauth_refresh', 'env:TOKEN_REFRESH_ENDPOINT_REF', 'external', 'vault:ref:token_refresh_credential', true)
ON CONFLICT (access_port_id) DO NOTHING;

-- Policy: external_credential_vault_refresh
-- Steps: resolve_port_record → resolve_credential_reference → load_encrypted_credential_payload
--        → decrypt_for_runtime_use → execute_abstract_function(credential.refresh_token)
--        → append_runtime_event_log
INSERT INTO topology.external_port_policies (policy_id, policy_key, port_kind, required_by_bundle, active)
VALUES
    ('00000000-0000-0000-0000-0000000000e0', 'external_credential_vault_refresh', 'access_port', 'external_credential_vault_refresh', true)
ON CONFLICT (policy_id) DO NOTHING;

DELETE FROM topology.external_port_policy_steps WHERE policy_id = '00000000-0000-0000-0000-0000000000e0';

INSERT INTO topology.external_port_policy_steps (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000501', '00000000-0000-0000-0000-0000000000e0', 1, 'resolve_port_record',              '{}',                                                              NULL,                          true),
    ('00000000-0000-0000-0000-000000000502', '00000000-0000-0000-0000-0000000000e0', 2, 'resolve_credential_reference',     '{}',                                                              NULL,                          true),
    ('00000000-0000-0000-0000-000000000503', '00000000-0000-0000-0000-0000000000e0', 3, 'load_encrypted_credential_payload','{}',                                                              NULL,                          true),
    ('00000000-0000-0000-0000-000000000504', '00000000-0000-0000-0000-0000000000e0', 4, 'decrypt_for_runtime_use',          '{}',                                                              NULL,                          true),
    ('00000000-0000-0000-0000-000000000505', '00000000-0000-0000-0000-0000000000e0', 5, 'execute_abstract_function',        '{}',                                                              'credential.refresh_token',    true),
    ('00000000-0000-0000-0000-000000000506', '00000000-0000-0000-0000-0000000000e0', 6, 'append_runtime_event_log',         '{"event_type":"credential_token_refreshed"}',                    NULL,                          true)
ON CONFLICT (policy_id, step_order) DO NOTHING;


-- =============================================================================
-- Scheduler job manifest substrate: canonical admin dispatch entry.
-- Demo entries (demo.scheduler_projection, demo_schedule) live in
-- db/demo/demo_scheduler.sql and are applied by demo/test compose after init.
-- =============================================================================

-- Manifest entry: admin dispatch for scheduler_jobs:list_settings → admin_runtime
-- Enables frontend SchedulerJobSettingsPanel to reach AdminRuntime.DataListSchedulerJobsSettingsAsync
-- via ManifestDispatcher.ResolveActiveManifestAsync(role=null, target=admin, layer=scheduler_jobs, action=list_settings).
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000000f0',
    NULL,
    ARRAY[
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"scheduler_jobs","action":"list_settings"}'::jsonb,
        '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO NOTHING;

-- admin.contents authoring dispatch: create / edit / disable scheduler job manifests.
-- These route through the canonical dispatch (frontend → ManifestDispatcher → admin_runtime).
-- The frontend submits a manifest draft only; runtime judgment / SQL / credential authority
-- stays in AdminRuntime. No secret material is carried by these mappings.
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-0000000000f1',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"scheduler_jobs","action":"create"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000f2',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"scheduler_jobs","action":"edit"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000000f3',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"scheduler_jobs","action":"disable"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    -- scheduler-settings subBundle (admin-surface-topology-seed-conversion): the symmetric
    -- counterpart of f3's disable, required by docs/design/admin-normal-surface-projection-seed-
    -- ssot.yaml surface_axes.admin.surfaces.scheduler.new_operation_note (set_scheduler_job_active_
    -- true mirroring disable's set_scheduler_job_active_false). Same axes shape as f0-f3 so the
    -- action is reachable on the ordinary axes route too, not only through the
    -- scheduler.settings.projection manifest's own target_ref dispatch below. Handler:
    -- AdminRuntime.SchedulerSettings.cs DataEnableSchedulerJobAsync -- the SAME SetJobActiveAsync
    -- authority disable already used, never a new authority.
    (
        '00000000-0000-0000-0000-0000000000f4',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"scheduler_jobs","action":"enable"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;

-- =============================================================================
-- scheduler-settings subBundle (admin-surface-topology-seed-conversion): topology UI seed for
-- scheduler.settings.projection.
--
-- SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
-- surface_axes.admin.surfaces.scheduler. React-like Schema authored, converted via
-- .agent/tools/react-schema-topology-seed-translator
-- (.agent/tests/fixtures/react-schema-topology-seed-translator/scheduler-settings-admin*.json,
-- regenerable: generate-react-schema --input scheduler-settings-admin.input.json |
-- generate-topology-seed --input scheduler-settings-admin.topology-seed.input.json).
-- Translator output is intake/draft evidence only (never adoption authority,
-- react-schema-topology-seed-translator-ssot.yaml topology_ui_seed_contract
-- active_topology_rule) -- the records[]/wiring/tensor below are the same shape, adopted
-- directly as this seed's structural authority (post-generation edits limited to identity
-- plumbing: layout/wiring/tensor/package UUIDs the generator leaves unresolved -- never
-- structural content).
--
-- 2026-07-22 Owner-confirmed design (.agent/tasks/todo.md "scheduler-settings 3分割設計の確定"):
-- this manifest is a seed-conversion projection artifact only -- it proves the
-- Manifest/hub/package/layout/wiring/tensor and mutation_confirmation_contract for this surface
-- are real and dispatchable. It does NOT become /admin/scheduler's own route body:
-- frontend/routes/admin/scheduler.tsx keeps mounting the existing (scope-reduced, not deleted)
-- frontend/islands/SchedulerJobSettingsPanel.tsx, which dispatches the SAME
-- scheduler_jobs:list_settings/enable/disable actions this manifest's own wiring targets. This
-- manifest's own reachability is hub_navigation_only (docs/design/runtime-orchestration-ssot.yaml
-- admin_route_retirement_matrix): an admin-authored hubs.hub_relations outbound navigation entry
-- via /admin/manifests, never a route-body swap. Deliberately seeds NO hubs.hub_relations row --
-- navigation_binding_authoring_and_verification is proven live, at test time, through the
-- hub_navigation:create dispatch action (backend/tests/Topolactor.Integration.Tests/
-- SchedulerSettingsHubRelationUiProjectionLiveDbTests.cs), exactly as admin-enum / team-dashboard /
-- credential-management prove it -- none of them ships a seeded relation row either.
--
-- dispatcher_mapping uses target="manifest" (not "admin"): the axes-routed scheduler_jobs rows
-- f0-f4 above already own (role=admin, target=admin, scheduler_jobs, *). Declaring the same
-- axes here would make axes resolution MANIFEST_AMBIGUOUS -- admin.enum.management.projection
-- (ae200) uses target="manifest" for exactly this reason. target_ref dispatch (the ONLY path this
-- manifest's own wiring uses -- ManifestDispatcher.cs TryParseManifestTargetRef) is wildcarded on
-- target, so these dispatcher_mapping entries remain the real authorization authority for this
-- manifest's own dispatches without colliding with f0-f4's axes-routed resolution.
-- =============================================================================

-- Hub owning the scheduler.settings.projection topology_manifest. Never itself a hub_relations
-- source/target -- required FK owner only (same as ae201/dd011).
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-00000005c101', '{"description":"scheduler_settings","system":true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

-- Runtime manifest row: real ui_projection, reached via explicit
-- payload.target_ref = manifest:<id>:<wiring_key> (the same target_ref dispatch path
-- admin-configured wiring already uses), routed to admin_runtime (the scheduler_jobs:* actions'
-- existing runtime_destination). default_screen_read on list_settings (2026-08-17 mechanism,
-- runtime-orchestration-ssot.yaml dispatcher_contract.default_screen_read_override) so the table
-- shows real data on initial mount, not only after a user-triggered search/filter.
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-00000005c100',
    NULL,
    ARRAY[
        '{"type":"hub_grouping","manifestKey":"scheduler.settings.projection","bundle":"admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
        '{"type":"dispatcher_mapping","role":"admin","target":"manifest","layer":"scheduler_jobs","action":"list_settings","default_screen_read":true}'::jsonb,
        '{"type":"dispatcher_mapping","role":"admin","target":"manifest","layer":"scheduler_jobs","action":"enable"}'::jsonb,
        '{"type":"dispatcher_mapping","role":"admin","target":"manifest","layer":"scheduler_jobs","action":"disable"}'::jsonb,
        '{"type":"ui_projection","packageIds":["00000000-0000-0000-0000-00000005c103"],"layoutId":"00000000-0000-0000-0000-00000005c104","wiringId":"00000000-0000-0000-0000-00000005c105","tensorId":"00000000-0000-0000-0000-00000005c106"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

-- hubs.topology_manifests projection: manifest_key is the stable identity NOTHING in the
-- frontend pins by yet (this manifest is not consumed by any route -- see the design boundary
-- note above); resolved backend-side by the generic manifest_key_target_ref_resolution_contract's
-- "exactly one active row" rule, available for a future hub_navigation link or explicit
-- ?manifest= selection.
INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT m.manifest_id, '00000000-0000-0000-0000-00000005c101'::uuid,
       'scheduler.settings.projection', m.status, to_jsonb(m.topology)
FROM manifest m WHERE m.manifest_id = '00000000-0000-0000-0000-00000005c100'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET hub_id         = EXCLUDED.hub_id,
        manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

-- scheduler.settings.projection UI persistence (package/layout/wiring/tensor).
INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-00000005c102',
    'scheduler.settings.projection.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey":"scheduler.settings.projection","surface":"scheduler.settings.projection","categoryKeys":["scheduler"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

-- Manifest-facing package authority (manifest.topology[ui_projection].packageIds points here,
-- not at ui_component_package above -- same split as ae202/ae203). No component+design pairs
-- were authored via UI Component Builder for this surface, so layout is honestly empty.
INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-00000005c103',
    'scheduler.settings.projection.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

-- Structural authority tree: React-like Schema -> translator -> topology_ui_seed_record
-- records[], adopted directly (see header comment). Category scheduler > Section
-- scheduler_job_roster > Field scheduler_search / Field scheduler_filter_trigger_kind / Field
-- scheduler_filter_schedule_policy_kind / Field scheduler_filter_active / Table
-- scheduler_job_list / Action scheduler_enable_button (opens scheduler_enable_confirm_modal) /
-- Modal scheduler_enable_confirm_modal (Action scheduler_enable_confirm_button + Action
-- scheduler_enable_cancel_button) / Action scheduler_disable_button (opens
-- scheduler_disable_confirm_modal) / Modal scheduler_disable_confirm_modal (Action
-- scheduler_disable_confirm_button + Action scheduler_disable_cancel_button) / Validation
-- scheduler_seed_is_not_production_route (documents the design boundary, see header comment).
INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-00000005c104',
    'scheduler.settings.projection.layout',
    'fixed_form_projection',
    $${"records": [{"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_settings_projection", "record": {"recordType": "topology_ui_category", "key": "scheduler", "label": "Scheduler", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler"], "sourceReactPath": "$.root.children[0]", "knownGapRefs": [], "categoryKey": "scheduler", "sectionKeys": ["scheduler_job_roster"]}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler", "record": {"recordType": "topology_ui_section", "key": "scheduler_job_roster", "label": "Configured scheduler jobs", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract"], "sourceReactPath": "$.root.children[0].children[0]", "knownGapRefs": [], "sectionKey": "scheduler_job_roster", "sectionKind": "scheduler_job_list_search_filter_toggle_projection", "childKeys": ["scheduler_search", "scheduler_filter_trigger_kind", "scheduler_filter_schedule_policy_kind", "scheduler_filter_active", "scheduler_job_list", "scheduler_enable_button", "scheduler_enable_confirm_modal", "scheduler_disable_button", "scheduler_disable_confirm_modal", "scheduler_seed_is_not_production_route"]}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_field", "key": "scheduler_search", "label": "Job key search", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.component_tree"], "sourceReactPath": "$.root.children[0].children[0].children[0]", "knownGapRefs": [], "authorityMarker": "draft_or_projection_only", "fieldKey": "scheduler_search", "control": "form_input/search_input", "required": false, "validationRefs": [], "valueFrom": "", "optionsSource": "", "optionsLabelPath": "", "optionsValuePath": "", "eventBinding": {"trigger": "change", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings", "authority": "draft_or_projection_only", "payloadFrom": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}}, "debounceMs": 300, "adminRuntimeDispatchOverride": {"trigger": "change", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings", "payloadFrom": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}, "sourceActionKey": "scheduler_search"}}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_field", "key": "scheduler_filter_trigger_kind", "label": "Trigger kind filter", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.component_tree"], "sourceReactPath": "$.root.children[0].children[0].children[1]", "knownGapRefs": [], "authorityMarker": "draft_or_projection_only", "fieldKey": "scheduler_filter_trigger_kind", "control": "form_input/select", "required": false, "validationRefs": [], "valueFrom": "", "optionsSource": "emission.data.triggerKindOptions", "optionsLabelPath": "label", "optionsValuePath": "value", "eventBinding": {"trigger": "change", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings", "authority": "draft_or_projection_only", "payloadFrom": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}}, "adminRuntimeDispatchOverride": {"trigger": "change", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings", "payloadFrom": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}, "sourceActionKey": "scheduler_filter_trigger_kind"}}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_field", "key": "scheduler_filter_schedule_policy_kind", "label": "Schedule policy filter", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.component_tree"], "sourceReactPath": "$.root.children[0].children[0].children[2]", "knownGapRefs": [], "authorityMarker": "draft_or_projection_only", "fieldKey": "scheduler_filter_schedule_policy_kind", "control": "form_input/select", "required": false, "validationRefs": [], "valueFrom": "", "optionsSource": "emission.data.schedulePolicyKindOptions", "optionsLabelPath": "label", "optionsValuePath": "value", "eventBinding": {"trigger": "change", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings", "authority": "draft_or_projection_only", "payloadFrom": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}}, "adminRuntimeDispatchOverride": {"trigger": "change", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings", "payloadFrom": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}, "sourceActionKey": "scheduler_filter_schedule_policy_kind"}}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_field", "key": "scheduler_filter_active", "label": "Active filter", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.component_tree"], "sourceReactPath": "$.root.children[0].children[0].children[3]", "knownGapRefs": [], "authorityMarker": "draft_or_projection_only", "fieldKey": "scheduler_filter_active", "control": "form_input/select", "required": false, "validationRefs": [], "valueFrom": "", "optionsSource": "emission.data.activeOptions", "optionsLabelPath": "label", "optionsValuePath": "value", "eventBinding": {"trigger": "change", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings", "authority": "draft_or_projection_only", "payloadFrom": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}}, "adminRuntimeDispatchOverride": {"trigger": "change", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings", "payloadFrom": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}, "sourceActionKey": "scheduler_filter_active"}}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_table", "key": "scheduler_job_list", "label": "Scheduler job list", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.existing_schema_fields_allowed_for_projection"], "sourceReactPath": "$.root.children[0].children[0].children[4]", "knownGapRefs": [], "tableKey": "scheduler_job_list", "source": "scheduler_jobs", "display": "table", "displayColumns": [{"key": "jobKey", "header": "Job Key"}, {"key": "triggerKind", "header": "Trigger Kind"}, {"key": "schedulePolicyKind", "header": "Schedule Policy"}, {"key": "active", "header": "Active"}, {"key": "updatedAt", "header": "Updated"}], "rowsSource": "emission.data.schedulerJobs", "columnKeys": []}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_action", "key": "scheduler_enable_button", "label": "Enable selected job", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.mutation_confirmation_contract"], "sourceReactPath": "$.root.children[0].children[0].children[5]", "knownGapRefs": [], "authorityMarker": "preview_only", "actionKey": "scheduler_enable_button", "actionRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable", "eventBinding": {"trigger": "click", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable", "authority": "preview_only", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "dryRun": "literal:true"}}, "runtimeInteractions": [{"trigger": "click", "actionType": "openModal", "targetNodeId": "scheduler_enable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_enable_button"}], "adminRuntimeDispatchOverride": {"trigger": "click", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "dryRun": "literal:true"}, "sourceActionKey": "scheduler_enable_button"}}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_modal", "key": "scheduler_enable_confirm_modal", "label": "Enable confirmation dialog", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.mutation_confirmation_contract"], "sourceReactPath": "$.root.children[0].children[0].children[6]", "knownGapRefs": [], "modalKey": "scheduler_enable_confirm_modal", "componentKind": "disclosure/modal", "title": "Enable scheduler job", "body": "Enable the selected scheduler job for due selection.", "runtimeInteractions": [{"trigger": "toggle", "actionType": "closeModal", "targetNodeId": "scheduler_enable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_enable_confirm_modal"}], "childKeys": ["scheduler_enable_confirm_button", "scheduler_enable_cancel_button"]}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_enable_confirm_modal", "record": {"recordType": "topology_ui_action", "key": "scheduler_enable_confirm_button", "label": "Enable", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.mutation_confirmation_contract"], "sourceReactPath": "$.root.children[0].children[0].children[6].children[0]", "knownGapRefs": [], "authorityMarker": "draft_apply_not_execution_authority", "actionKey": "scheduler_enable_confirm_button", "actionRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable", "eventBinding": {"trigger": "click", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable", "authority": "draft_apply_not_execution_authority", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "confirmed": "literal:true"}}, "runtimeInteractions": [{"trigger": "click", "actionType": "closeModal", "targetNodeId": "scheduler_enable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_enable_confirm_button"}], "adminRuntimeDispatchOverride": {"trigger": "click", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "confirmed": "literal:true"}, "sourceActionKey": "scheduler_enable_confirm_button"}}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_enable_confirm_modal", "record": {"recordType": "topology_ui_action", "key": "scheduler_enable_cancel_button", "label": "Cancel", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.mutation_confirmation_contract"], "sourceReactPath": "$.root.children[0].children[0].children[6].children[1]", "knownGapRefs": [], "authorityMarker": "draft_or_projection_only", "actionKey": "scheduler_enable_cancel_button", "actionRef": "ui-local:scheduler_enable_confirm_modal.close", "eventBinding": {"trigger": "click", "wiringLane": "disclosure_state_wiring", "targetRef": "ui-local:scheduler_enable_confirm_modal.open", "authority": "draft_or_projection_only", "disclosureActionType": "closeModal", "disclosureTargetNodeId": "scheduler_enable_confirm_modal", "disclosureStatePath": "open"}, "runtimeInteractions": [{"trigger": "click", "actionType": "closeModal", "targetNodeId": "scheduler_enable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_enable_cancel_button"}]}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_action", "key": "scheduler_disable_button", "label": "Disable selected job", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.mutation_confirmation_contract"], "sourceReactPath": "$.root.children[0].children[0].children[7]", "knownGapRefs": [], "authorityMarker": "preview_only", "actionKey": "scheduler_disable_button", "actionRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable", "eventBinding": {"trigger": "click", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable", "authority": "preview_only", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "dryRun": "literal:true"}}, "runtimeInteractions": [{"trigger": "click", "actionType": "openModal", "targetNodeId": "scheduler_disable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_disable_button"}], "adminRuntimeDispatchOverride": {"trigger": "click", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "dryRun": "literal:true"}, "sourceActionKey": "scheduler_disable_button"}}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_modal", "key": "scheduler_disable_confirm_modal", "label": "Disable confirmation dialog", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.mutation_confirmation_contract"], "sourceReactPath": "$.root.children[0].children[0].children[8]", "knownGapRefs": [], "modalKey": "scheduler_disable_confirm_modal", "componentKind": "disclosure/modal", "title": "Disable scheduler job", "body": "Disable the selected scheduler job. New due selection stops without deleting its run ledger.", "runtimeInteractions": [{"trigger": "toggle", "actionType": "closeModal", "targetNodeId": "scheduler_disable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_disable_confirm_modal"}], "childKeys": ["scheduler_disable_confirm_button", "scheduler_disable_cancel_button"]}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_disable_confirm_modal", "record": {"recordType": "topology_ui_action", "key": "scheduler_disable_confirm_button", "label": "Disable", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.mutation_confirmation_contract"], "sourceReactPath": "$.root.children[0].children[0].children[8].children[0]", "knownGapRefs": [], "authorityMarker": "draft_apply_not_execution_authority", "actionKey": "scheduler_disable_confirm_button", "actionRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable", "eventBinding": {"trigger": "click", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable", "authority": "draft_apply_not_execution_authority", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "confirmed": "literal:true"}}, "runtimeInteractions": [{"trigger": "click", "actionType": "closeModal", "targetNodeId": "scheduler_disable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_disable_confirm_button"}], "adminRuntimeDispatchOverride": {"trigger": "click", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "confirmed": "literal:true"}, "sourceActionKey": "scheduler_disable_confirm_button"}}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_disable_confirm_modal", "record": {"recordType": "topology_ui_action", "key": "scheduler_disable_cancel_button", "label": "Cancel", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.seed_contract.mutation_confirmation_contract"], "sourceReactPath": "$.root.children[0].children[0].children[8].children[1]", "knownGapRefs": [], "authorityMarker": "draft_or_projection_only", "actionKey": "scheduler_disable_cancel_button", "actionRef": "ui-local:scheduler_disable_confirm_modal.close", "eventBinding": {"trigger": "click", "wiringLane": "disclosure_state_wiring", "targetRef": "ui-local:scheduler_disable_confirm_modal.open", "authority": "draft_or_projection_only", "disclosureActionType": "closeModal", "disclosureTargetNodeId": "scheduler_disable_confirm_modal", "disclosureStatePath": "open"}, "runtimeInteractions": [{"trigger": "click", "actionType": "closeModal", "targetNodeId": "scheduler_disable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_disable_cancel_button"}]}}, {"type": "topology_ui_seed_record", "seedKey": "scheduler.settings.projection", "parentKey": "scheduler_job_roster", "record": {"recordType": "topology_ui_validation", "key": "scheduler_seed_is_not_production_route", "label": "This manifest (scheduler.settings.projection) is a seed-conversion projection artifact proving the scheduler-settings surface's Manifest/hub/package/layout/wiring/tensor and mutation_confirmation_contract are real and dispatchable. Per the 2026-07-22 Owner-confirmed design (.agent/tasks/todo.md scheduler-settings 3\u5206\u5272\u8a2d\u8a08\u306e\u78ba\u5b9a), /admin/scheduler itself is NOT retired to a ProjectionShell wrapper of this manifest -- the existing hardcoded route stays frontend/routes/admin/scheduler.tsx mounting the (scope-reduced) frontend/islands/SchedulerJobSettingsPanel.tsx, which dispatches the SAME scheduler_jobs:list_settings/enable/disable actions this manifest's own wiring targets. This manifest is reachable only via hub navigation authored through /admin/manifests (hub_navigation_only retirement_kind, runtime-orchestration-ssot.yaml admin_route_retirement_matrix), never via a route body swap.", "sourceYamlRefs": ["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.scheduler.scope_boundary"], "sourceReactPath": "$.root.children[0].children[0].children[9]", "knownGapRefs": [], "validationKey": "scheduler_seed_is_not_production_route", "rule": "seed_manifest_is_not_the_production_route", "severity": "informational"}}]}$$::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

-- Wiring: the search/filter Fields' admin_runtime_dispatch_override_wiring targeting THIS
-- manifest's own scheduler_jobs:list_settings action (target="manifest" axes above), and the
-- enable/disable Action/Modal chain's own admin_runtime_dispatch_override_wiring /
-- disclosure_state_wiring entries.
INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-00000005c105',
    'scheduler.settings.projection.wiring',
    'action_bundle',
    'manifest',
    $${"actions": [{"wiringKey": "scheduler.settings.projection.scheduler_enable_button.wiring", "wiringKind": "admin_runtime_dispatch_override_wiring", "targetSurface": "manifest", "wiringSchemaJson": {"eventBinding": {"trigger": "click", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable", "authority": "preview_only", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "dryRun": "literal:true"}}, "sourceActionKey": "scheduler_enable_button", "authorityMarker": "preview_only"}, "sourceRecordKey": "scheduler_enable_button"}, {"wiringKey": "scheduler.settings.projection.scheduler_enable_confirm_button.wiring", "wiringKind": "admin_runtime_dispatch_override_wiring", "targetSurface": "manifest", "wiringSchemaJson": {"eventBinding": {"trigger": "click", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable", "authority": "draft_apply_not_execution_authority", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "confirmed": "literal:true"}}, "sourceActionKey": "scheduler_enable_confirm_button", "authorityMarker": "draft_apply_not_execution_authority"}, "sourceRecordKey": "scheduler_enable_confirm_button"}, {"wiringKey": "scheduler.settings.projection.scheduler_enable_cancel_button.wiring", "wiringKind": "disclosure_state_wiring", "targetSurface": "manifest", "wiringSchemaJson": {"eventBinding": {"trigger": "click", "wiringLane": "disclosure_state_wiring", "targetRef": "ui-local:scheduler_enable_confirm_modal.open", "authority": "draft_or_projection_only", "disclosureActionType": "closeModal", "disclosureTargetNodeId": "scheduler_enable_confirm_modal", "disclosureStatePath": "open"}, "sourceActionKey": "scheduler_enable_cancel_button", "authorityMarker": "draft_or_projection_only"}, "sourceRecordKey": "scheduler_enable_cancel_button"}, {"wiringKey": "scheduler.settings.projection.scheduler_disable_button.wiring", "wiringKind": "admin_runtime_dispatch_override_wiring", "targetSurface": "manifest", "wiringSchemaJson": {"eventBinding": {"trigger": "click", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable", "authority": "preview_only", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "dryRun": "literal:true"}}, "sourceActionKey": "scheduler_disable_button", "authorityMarker": "preview_only"}, "sourceRecordKey": "scheduler_disable_button"}, {"wiringKey": "scheduler.settings.projection.scheduler_disable_confirm_button.wiring", "wiringKind": "admin_runtime_dispatch_override_wiring", "targetSurface": "manifest", "wiringSchemaJson": {"eventBinding": {"trigger": "click", "wiringLane": "admin_runtime_dispatch_override_wiring", "targetRef": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable", "authority": "draft_apply_not_execution_authority", "payloadFrom": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "confirmed": "literal:true"}}, "sourceActionKey": "scheduler_disable_confirm_button", "authorityMarker": "draft_apply_not_execution_authority"}, "sourceRecordKey": "scheduler_disable_confirm_button"}, {"wiringKey": "scheduler.settings.projection.scheduler_disable_cancel_button.wiring", "wiringKind": "disclosure_state_wiring", "targetSurface": "manifest", "wiringSchemaJson": {"eventBinding": {"trigger": "click", "wiringLane": "disclosure_state_wiring", "targetRef": "ui-local:scheduler_disable_confirm_modal.open", "authority": "draft_or_projection_only", "disclosureActionType": "closeModal", "disclosureTargetNodeId": "scheduler_disable_confirm_modal", "disclosureStatePath": "open"}, "sourceActionKey": "scheduler_disable_cancel_button", "authorityMarker": "draft_or_projection_only"}, "sourceRecordKey": "scheduler_disable_cancel_button"}]}$$::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

-- Tensor: flat layout_patch_json.nodes[] carrying componentKey + dispatchTargetRefByTrigger /
-- dispatchPayloadFromByTrigger for every catalog_component node, adopted directly from the
-- translator's tensorAdoptionCandidates (never hand-edited structural content).
INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-00000005c106',
    'scheduler.settings.projection',
    '00000000-0000-0000-0000-00000005c102',
    '00000000-0000-0000-0000-00000005c104',
    '00000000-0000-0000-0000-00000005c105',
    $${"nodes": [{"nodeId": "scheduler_search", "nodeKind": "catalog_component", "runtimeInteractions": [], "componentKey": "search_input.alias", "dispatchTargetRefByTrigger": {"change": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings"}, "dispatchPayloadFromByTrigger": {"change": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}}, "debounceMs": 300}, {"nodeId": "scheduler_filter_trigger_kind", "nodeKind": "catalog_component", "runtimeInteractions": [], "componentKey": "select.template", "dispatchTargetRefByTrigger": {"change": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings"}, "dispatchPayloadFromByTrigger": {"change": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}}, "propBindings": {"options": {"source": "emission.data.triggerKindOptions", "transform": "rowsToOptions", "labelPath": "label", "valuePath": "value"}}}, {"nodeId": "scheduler_filter_schedule_policy_kind", "nodeKind": "catalog_component", "runtimeInteractions": [], "componentKey": "select.template", "dispatchTargetRefByTrigger": {"change": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings"}, "dispatchPayloadFromByTrigger": {"change": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}}, "propBindings": {"options": {"source": "emission.data.schedulePolicyKindOptions", "transform": "rowsToOptions", "labelPath": "label", "valuePath": "value"}}}, {"nodeId": "scheduler_filter_active", "nodeKind": "catalog_component", "runtimeInteractions": [], "componentKey": "select.template", "dispatchTargetRefByTrigger": {"change": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:list_settings"}, "dispatchPayloadFromByTrigger": {"change": {"search": "node:scheduler_search.value", "triggerKind": "node:scheduler_filter_trigger_kind.value", "schedulePolicyKind": "node:scheduler_filter_schedule_policy_kind.value", "active": "node:scheduler_filter_active.value"}}, "propBindings": {"options": {"source": "emission.data.activeOptions", "transform": "rowsToOptions", "labelPath": "label", "valuePath": "value"}}}, {"nodeId": "scheduler_job_list", "nodeKind": "catalog_component", "runtimeInteractions": [], "propsJson": "{\"table\": null, \"columns\": [{\"key\": \"jobKey\", \"header\": \"Job Key\"}, {\"key\": \"triggerKind\", \"header\": \"Trigger Kind\"}, {\"key\": \"schedulePolicyKind\", \"header\": \"Schedule Policy\"}, {\"key\": \"active\", \"header\": \"Active\"}, {\"key\": \"updatedAt\", \"header\": \"Updated\"}]}", "propBindings": {"rows": {"source": "emission.data.schedulerJobs"}}}, {"nodeId": "scheduler_enable_button", "nodeKind": "catalog_component", "runtimeInteractions": [{"trigger": "click", "actionType": "openModal", "targetNodeId": "scheduler_enable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_enable_button"}], "componentKey": "button.primitive", "dispatchTargetRefByTrigger": {"click": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable"}, "dispatchPayloadFromByTrigger": {"click": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "dryRun": "literal:true"}}, "propsJson": "{\"label\": \"Enable selected job\"}"}, {"nodeId": "scheduler_enable_confirm_modal", "nodeKind": "catalog_component", "runtimeInteractions": [{"trigger": "toggle", "actionType": "closeModal", "targetNodeId": "scheduler_enable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_enable_confirm_modal"}], "componentKey": "modal.template", "componentKind": "disclosure/modal", "propsJson": "{\"data\": {\"open\": false, \"title\": \"Enable scheduler job\", \"body\": \"Enable the selected scheduler job for due selection.\"}}"}, {"nodeId": "scheduler_enable_confirm_button", "nodeKind": "catalog_component", "runtimeInteractions": [{"trigger": "click", "actionType": "closeModal", "targetNodeId": "scheduler_enable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_enable_confirm_button"}], "componentKey": "button.primitive", "dispatchTargetRefByTrigger": {"click": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:enable"}, "dispatchPayloadFromByTrigger": {"click": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "confirmed": "literal:true"}}, "propsJson": "{\"label\": \"Enable\"}"}, {"nodeId": "scheduler_enable_cancel_button", "nodeKind": "catalog_component", "runtimeInteractions": [{"trigger": "click", "actionType": "closeModal", "targetNodeId": "scheduler_enable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_enable_cancel_button"}], "componentKey": "button.primitive", "propsJson": "{\"label\": \"Cancel\"}"}, {"nodeId": "scheduler_disable_button", "nodeKind": "catalog_component", "runtimeInteractions": [{"trigger": "click", "actionType": "openModal", "targetNodeId": "scheduler_disable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_disable_button"}], "componentKey": "button.primitive", "dispatchTargetRefByTrigger": {"click": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable"}, "dispatchPayloadFromByTrigger": {"click": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "dryRun": "literal:true"}}, "propsJson": "{\"label\": \"Disable selected job\"}"}, {"nodeId": "scheduler_disable_confirm_modal", "nodeKind": "catalog_component", "runtimeInteractions": [{"trigger": "toggle", "actionType": "closeModal", "targetNodeId": "scheduler_disable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_disable_confirm_modal"}], "componentKey": "modal.template", "componentKind": "disclosure/modal", "propsJson": "{\"data\": {\"open\": false, \"title\": \"Disable scheduler job\", \"body\": \"Disable the selected scheduler job. New due selection stops without deleting its run ledger.\"}}"}, {"nodeId": "scheduler_disable_confirm_button", "nodeKind": "catalog_component", "runtimeInteractions": [{"trigger": "click", "actionType": "closeModal", "targetNodeId": "scheduler_disable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_disable_confirm_button"}], "componentKey": "button.primitive", "dispatchTargetRefByTrigger": {"click": "manifest:00000000-0000-0000-0000-00000005c100:scheduler_jobs:disable"}, "dispatchPayloadFromByTrigger": {"click": {"schedulerJobId": "node:scheduler_job_list.value.schedulerJobId", "confirmed": "literal:true"}}, "propsJson": "{\"label\": \"Disable\"}"}, {"nodeId": "scheduler_disable_cancel_button", "nodeKind": "catalog_component", "runtimeInteractions": [{"trigger": "click", "actionType": "closeModal", "targetNodeId": "scheduler_disable_confirm_modal", "statePath": "open", "sourceActionKey": "scheduler_disable_cancel_button"}], "componentKey": "button.primitive", "propsJson": "{\"label\": \"Cancel\"}"}]}$$::jsonb
)
ON CONFLICT (tensor_id) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- =============================================================================
-- admin-surface-topology-seed-conversion: production dispatcher_mapping closure
-- for auth_users:* (AdminRuntimeMasterRoster.cs / NpgsqlAuthMasterRepository.cs)
-- and team_markdown:* (AdminRuntime.TeamMarkdown.cs / NpgsqlTeamMarkdownRepository.cs).
-- Both handler families were fully implemented at the backend/DB layer already;
-- this closes the previously-confirmed gap where ManifestDispatcher.ResolveActiveManifestAsync
-- had no matching manifest row for these axes and would return MANIFEST_NOT_FOUND
-- (no silent fallback) before AdminRuntime.ExecuteDataAsync was ever reached.
-- =============================================================================
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-0000000ad001',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"auth_users","action":"list"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000ad002',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"auth_users","action":"search"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000ad003',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"auth_users","action":"get"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000ad004',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"auth_users","action":"create"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000ad005',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"auth_users","action":"update"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000ad006',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"auth_users","action":"delete"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;

-- =============================================================================
-- round 5: credentials.users revoke_credential/revoke_sessions dispatch-only manifests
-- (AdminRuntime.AuthUsersRevoke.cs) -- thin admin_runtime wrappers around the SAME
-- AuthService.AdminRevokeCredentialAsync/AdminRevokeSessionsAsync methods the existing
-- POST /admin/auth/users/{userId}/credential|sessions/revoke REST routes already call.
-- Mirrors the ad001-006 pattern above (one dispatcher_mapping per manifest).
-- =============================================================================
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-0000000ad007',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"auth_users","action":"revoke_credential"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000ad008',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"auth_users","action":"revoke_sessions"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;


INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-0000000e5001',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"template:create"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e5002',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"template:list"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e5003',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"template:get"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e5004',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"template:update"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e5005',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"template:archive"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e5006',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"saved_view:create"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e5007',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"saved_view:search"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e5008',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"saved_view:get"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e5009',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"saved_view:refresh"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e500a',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"saved_view:clone"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e500b',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"saved_view:rebind"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e500c',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"saved_view:update"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-0000000e500d',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_markdown","action":"saved_view:archive"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;

-- =============================================================================
-- admin-surface-topology-seed-conversion: admin hub relation navigation source
-- -- A FABRICATED "/admin landing" MANIFEST IS INTENTIONALLY NOT ADDED HERE
-- (owner correction, PR #584 review comments) -- and was never structurally
-- necessary in the first place. A prior revision of this file created a
-- manifest/hub/hub_relations row here solely to give /admin its own outbound
-- hub relation to manifest 092. That manifest carried no ui_projection and
-- represented no pre-existing screen -- its only purpose was to be a
-- hub_relations source, which is exactly the "empty/fake topology manifest
-- created for hub relation connection purposes" pattern the owner has
-- explicitly prohibited.
--
-- The real hub relation authoring mechanism does not require /admin to have
-- its own manifest at all: /admin/manifests (frontend/routes/admin/manifests.tsx
-- -> ManifestsAdmin.tsx + HubNavigationAdmin.tsx islands, already implemented,
-- not built by this Bundle) lets an admin pick ANY EXISTING topology_manifest_id
-- as the relation source, via the hub_navigation:create/update/deprecate/reorder
-- dispatch actions (all already seeded active below, and proven end-to-end
-- against a real database in
-- backend/tests/Topolactor.Integration.Tests/CredentialManagementHubRelationUiProjectionLiveDbTests.cs
-- DispatchAsync_HubNavigationCreate_RealAuthoringPath_...). This seed file
-- intentionally does not insert a concrete hubs.hub_relations row here --
-- authoring a specific relation (e.g. connecting some existing manifest to
-- credential-management's hub) is an ordinary admin/runtime action performed
-- through that existing surface, not seed content this file should carry. See
-- docs/design/admin-console-workflow-ssot.yaml
-- admin_hub_relation_navigation_contract for the full authoring contract.
-- =============================================================================

-- =============================================================================
-- admin-surface-topology-seed-conversion: admin-dashboard subBundle seed
-- (admin landing / navigation surface -- projection-side hub_relation link
-- navigation only, per docs/design/admin-normal-surface-projection-seed-ssot.yaml
-- surface_axes.admin.surfaces.dashboard: display_manifest_scoped_hub_relation_links /
-- use_selected_link_as_projection_change_trigger; capability_requirements
-- mutation: none_in_this_surface).
--
-- This is NOT the fake "/admin landing MANIFEST created solely to host an
-- outbound hub_relations row" pattern the owner explicitly prohibited (PR
-- #584 review; see the removed ad100/ad101/ad102 block this replaces) --
-- this manifest carries a real ui_projection (the component_tree below), and
-- it still owns ZERO hubs.hub_relations rows of its own: authoring a
-- specific relation is an ordinary admin/runtime action performed through
-- the existing /admin/manifests surface (hub_navigation:create, already
-- seeded and proven end to end in
-- CredentialManagementHubRelationUiProjectionLiveDbTests), never seed
-- content this file should carry.
--
-- component_tree follows admin-normal-surface-projection-seed-ssot.yaml
-- surface_axes.admin.surfaces.dashboard.seed_contract.component_tree exactly:
-- hub_relation_link_list (card_list.primitive -- table.primitive substituted
-- per owner instruction: not yet registered in topology.ui_component_registry,
-- and a fixed-column table resists responsive reflow; card_list is already
-- registered/active and reused by the hub_search.readonly.v1 preset,
-- db/hub_search_preset_seed.sql) + target_projection_shell (panel.alias, the
-- root shell). There is no separate hub_relation_search node: search is
-- card_list.primitive's own built-in searchable prop (frontend/components/
-- CardList.tsx), a self-contained local-Preact-state title/body substring
-- filter over the already-resolved items array -- no new propBindingResolver/
-- backend filter plumbing exists or is needed, since the generic
-- renderEmission pipeline has no mechanism (and none is required) to filter a
-- bound list prop by a live local input value. CSS-grid styling of the cards
-- themselves is NOT wired here -- known_gap_css_grid_styling in the SSOT's
-- seed_contract.component_tree explains why real (non-draft) dispatched
-- LayoutNodes have no design/style wiring point today; card_list's own
-- default block-flow rendering (not a fixed-column table) is what this seed
-- actually provides.
--
-- Authored directly as ui_topology_tensor.layout_patch_json.nodes[] (the
-- UI-Builder-native "tensor-only" path -- componentKey resolves componentId/
-- componentKind server-side via topology.ui_component_registry,
-- NpgsqlTopologyRepository.EnrichCatalogComponentIdsFromRegistryAsync /
-- LoadComponentKindsByIdsAsync -- no translator/records[] involved, since this
-- surface's component_tree is a real canvas composition, not a
-- Category/Section/Field topology_ui_seed_record tree). Reached via the same
-- admin_runtime structural-render fallback path (ADMIN_OPERATION_NOT_FOUND ->
-- structural success) manifest 092 already proves, so no new backend C# code
-- is required for reachability.
--
-- hub_relation_link_list.propBindings.items binds to emission.navigationSequence
-- (frontend/runtime/propBindingResolver.ts EMISSION_NAVIGATION_SEQUENCE_SOURCE,
-- extended in this Bundle) via the navigationLinksToCardItems transform --
-- ManifestDispatcher.EnrichWithHubNavigationAsync already populates that field
-- for any successful dispatch, so the card list shows real hub_relations rows
-- (authored later via /admin/manifests) with no additional wiring. This never
-- reads emission.recommendNavigationProjection (SQL Attention /
-- attention_recommendation_tab stays out of reach -- docs/framework-core.yaml
-- runtime_route_attention_boundary).
--
-- Real click-to-navigate for the links already happens for free via
-- ProjectionShell's own automatic hub navigation nav bar
-- (resolveHubNavigationLinks) rendered alongside this card list, so no
-- onSelect wiring was added here either.
-- =============================================================================

-- Hub owning the admin-dashboard-navigation topology_manifest. This hub is
-- never a hub_relations target/source by itself -- it exists only as the
-- required FK owner of the topology_manifest below.
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ad201', '{"description":"admin_dashboard_navigation","system":true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

-- Runtime manifest row (manifest table): real ui_projection, no
-- dispatcher_mapping target axis of its own -- reached the same way manifest
-- 092 is reached, via explicit payload.target_ref = manifest:<id>:projection_entry
-- (frontend/routes/admin/index.tsx redirects bare /admin to
-- /admin?manifest=<this id>, the same ?manifest= entry-selection path
-- frontend/runtime/projectionEntry.ts already supports for any manifest).
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ad200',
    NULL,
    ARRAY[
        '{"type":"hub_grouping","manifestKey":"admin.dashboard.navigation.projection","bundle":"admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
        '{"type":"ui_projection","packageIds":["00000000-0000-0000-0000-0000000ad203"],"layoutId":"00000000-0000-0000-0000-0000000ad204","wiringId":"00000000-0000-0000-0000-0000000ad205","tensorId":"00000000-0000-0000-0000-0000000ad206"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

-- hubs.topology_manifests projection for the admin-dashboard-navigation
-- manifest. topology_manifest_id = manifest_id, same convention as manifest 092.
INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ad201'::uuid,
    'admin.dashboard.navigation.projection',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ad200'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

-- admin-dashboard-navigation UI persistence (package/layout/wiring/tensor).
INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ad202',
    'admin.dashboard.navigation.projection.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey":"admin.dashboard.navigation.projection","surface":"admin.dashboard.navigation.projection","categoryKeys":[]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

-- Manifest-facing package authority (manifest.topology[ui_projection].packageIds
-- points here, not at ui_component_package above -- same split as manifest 092).
-- No component+design pairs were authored via UI Component Builder for this
-- surface (the tensor below carries componentKey/componentId directly), so
-- layout is honestly empty.
INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ad203',
    'admin.dashboard.navigation.projection.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

-- Empty records[] (NoRecords): this surface's component tree is a real
-- UI-Builder-native canvas composition, authored directly on the tensor
-- below, not a Category/Section/Field topology_ui_seed_record tree.
INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ad204',
    'admin.dashboard.navigation.projection.layout',
    'ui_builder_canvas',
    '{"records":[]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

-- No wiring actions authored -- no click/select event wiring was added (see
-- header comment: real click-to-navigate already happens through
-- ProjectionShell's own automatic hub navigation nav bar).
INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ad205',
    'admin.dashboard.navigation.projection.wiring',
    'read_only_no_actions',
    'manifest',
    'admin.dashboard.navigation.projection',
    '{"actions":[]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

-- Real UI-Builder-native tensor nodes: target_projection_shell (panel.alias,
-- root) containing hub_relation_link_list (card_list.primitive, items bound
-- to emission.navigationSequence via navigationLinksToCardItems, search
-- built in via its own searchable/searchPlaceholder props -- no separate
-- hub_relation_search node). componentKey resolves componentId/componentKind
-- server-side from topology.ui_component_registry; no hardcoded componentId
-- here.
INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ad206',
    'admin#dashboard_navigation',
    '00000000-0000-0000-0000-0000000ad202',
    '00000000-0000-0000-0000-0000000ad204',
    '00000000-0000-0000-0000-0000000ad205',
    'default',
    0,
    '{"nodes":[{"nodeId":"target_projection_shell","nodeKind":"catalog_component","componentKey":"panel.alias","parentNodeId":null,"slotKey":"root","orderIndex":0,"propsJson":"{\"title\": \"画面間ナビゲーション\"}"},{"nodeId":"hub_relation_link_list","nodeKind":"catalog_component","componentKey":"card_list.primitive","parentNodeId":"target_projection_shell","slotKey":"results","orderIndex":1,"propsJson":"{\"emptyMessage\": \"表示できる遷移先がまだありません\", \"searchable\": true, \"searchPlaceholder\": \"ナビ検索\"}","propBindings":{"items":{"source":"emission.navigationSequence","transform":"navigationLinksToCardItems"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- =============================================================================
-- admin-surface-topology-seed-conversion: admin-enum subBundle seed
-- (enum dictionary / enum group / enum item management projection, per
-- docs/design/admin-normal-surface-projection-seed-ssot.yaml
-- surface_axes.admin.surfaces.enum). React-like Schema authored, converted via
-- .agent/tools/react-schema-topology-seed-translator
-- (.agent/tests/fixtures/react-schema-topology-seed-translator/admin-enum-ae200*.json,
-- regenerable: generate-react-schema --input admin-enum-ae200.input.json |
-- generate-topology-seed --input admin-enum-ae200.topology-seed.input.json).
-- Translator output is intake/draft evidence only (never adoption authority,
-- react-schema-topology-seed-translator-ssot.yaml topology_ui_seed_contract
-- active_topology_rule) -- the records[] below are the same shape, adopted
-- directly as this seed's structural authority.
--
-- component_tree (seed_contract.component_tree) mapped 1:1 to existing
-- registered componentKeys: enum_search (search_input.alias), enum_group_filter
-- (select.template), enum_table (table.primitive -- promoted from
-- code_only_drift to active in this same subBundle, see
-- db/ui_component_registry_preset_catalog_bootstrap.sql). No SSOT-unjustified
-- substitution (e.g. card_list.primitive/data_grid.alias) was made --
-- table.primitive is exactly what the SSOT specifies.
--
-- enum_confirm_form/enum_form/enum_confirm_button (the original structural-
-- leaf-only edit-and-confirm stub, seeded before any of the 7 write manifests
-- or the admin_runtime_dispatch_override_wiring lane existed) were removed as
-- orphans (admin-enum subBundle closure round, .agent/tasks/todo.md):
-- enum_confirm_button's dispatch was ui-local:enum_confirm_button.open_confirm,
-- a local-state no-op with no backend target and zero frontend consumers of
-- the "confirm" state it opened. Fully superseded by the 7 real per-operation
-- confirm modals below.
--
-- BUG FIX (admin-enum subBundle closure round): the 7 write-flow typed fields
-- (enum_create_group_name_input/enum_update_group_name_input/
-- enum_create_item_name_input/enum_update_item_index_input/
-- enum_update_item_name_input/enum_delete_item_index_input/
-- enum_set_group_items_input) were authored with control=form_input/form_field
-- (form_field.template), which resolves to frontend/runtime/
-- runtimeComponentFactory.ts formFieldFactory -- FormField.tsx with a
-- hardcoded EMPTY SPAN child, no <input> element, no onChange, so the live
-- node value tracker never received a value for any of these nodes: in real
-- production, none of them were typeable, and every one of the 7 write
-- actions' typed-value payloadFrom sourced from them would fail closed with
-- PAYLOAD_FROM_NODE_NOT_FOUND. Corrected to control=form_input/input
-- (input.primitive -> inputFactory, a real <Input> with onChange), see
-- react-schema-topology-seed-translator-ssot.yaml
-- admin_enum_write_field_control_kind_corrected_to_real_input.
--
-- Authored via the translator/records[] path (components_layout_design below),
-- unlike admin-dashboard's tensor-only path: this component_tree genuinely is
-- a Category/Section/Form/Field/Table/Action authoring tree (enum_dictionary
-- category > enum_dictionary_roster section > enum_search/enum_group_filter
-- fields + enum_table table), not a flat UI-Builder
-- canvas composition. backend/repository/LayoutSchemaTensorComposer.cs's
-- FieldControlToComponentKey/TableDisplayToComponentKey convention maps were
-- extended in this subBundle with form_input/search_input->search_input.alias
-- and table->table.primitive (both ordinary additions to an existing
-- convention dictionary over already-registered componentKeys, not a new
-- resolution mechanism).
--
-- mutation_confirmation_contract (preview_dictionary_delta / validate_against_
-- enum_authority / explicit_confirm / write / diff_log) -- READ: this
-- manifest's wiring row dispatches a real enum_dictionary:list_groups call
-- (see ui_wiring_registry/ui_topology_tensor above); real enum.groups rows
-- flow into emission.data and render in enum_table.
--
-- WRITE: all 7 enum_dictionary write actions (create_group/update_group/
-- delete_group/create_item/update_item/delete_item/set_group_items) are
-- wired DIRECTLY into THIS manifest's own layout (not a separate screen) via
-- per-node admin_runtime_dispatch_override_wiring
-- (dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger, round 15-19,
-- admin-write-surface-selection-context-and-mode-composition-gap Bundle),
-- each targeting its own dedicated single-purpose write manifest
-- (00000000-0000-0000-0000-0000000ae210/ae220/ae230/ae240/ae250/ae260/ae270).
-- Each write action's visible button first dispatches a non-mutating dryRun
-- preview (payload.dryRun=literal:true, the SAME target manifest/payloadFrom
-- the eventual write uses); the confirm modal opens only on a successful
-- preview and its own confirm button re-resolves the SAME payloadFrom fresh
-- and dispatches payload.confirmed=literal:true (admin_runtime_dryrun_
-- preview_gated_confirm_modal). Group-identity fields (groupId) are re-
-- resolved fresh from enum_table's own tracked selected-row value at confirm
-- time; item-identity fields (indexNum) are typed values. Backend
-- mutation_confirmation_contract (preview_dictionary_delta / validate_
-- against_enum_authority / explicit_confirm / write / diff_log,
-- payload.dryRun/payload.confirmed) is implemented for all 7 in
-- backend/runtime/AdminRuntimeMasterRoster.cs -- see docs/design/
-- enum-dictionary-ssot.yaml fail_close.mutation_confirmation_contract_gate
-- and .agent/tasks/todo.md admin-enum subBundle 実装記録 for the full record.
-- The 7 dedicated write manifests (ae210-ae270) remain independently
-- reachable too (explicit ?manifest=<id> selection, and via
-- hub_navigation:create from ae200 --
-- AdminEnumHubRelationUiProjectionLiveDbTests.cs
-- DispatchAsync_AdminEnumManagementManifest_HubNavigationCreate_
-- ToCreateGroupWriteManifest_ResolutionChainReflectsIt, the representative
-- pattern for all 7).
-- =============================================================================

-- Hub owning the admin-enum-management topology_manifest. Never a
-- hub_relations target/source by itself -- required FK owner only.
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ae201', '{"description":"admin_enum_management","system":true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

-- Runtime manifest row: real ui_projection, reached via explicit
-- payload.target_ref = manifest:<id>:projection_entry (same ?manifest=
-- entry-selection path manifest 092 / ad200 already use), routed to
-- admin_runtime (the enum_dictionary:* actions' existing runtime_destination).
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae200',
    NULL,
    ARRAY[
        '{"type":"hub_grouping","manifestKey":"admin.enum.management.projection","bundle":"admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type":"dispatcher_mapping","role":"admin","target":"manifest","layer":"enum_dictionary","action":"list_groups"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
        '{"type":"ui_projection","packageIds":["00000000-0000-0000-0000-0000000ae203"],"layoutId":"00000000-0000-0000-0000-0000000ae204","wiringId":"00000000-0000-0000-0000-0000000ae205","tensorId":"00000000-0000-0000-0000-0000000ae206"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

-- hubs.topology_manifests projection for the admin-enum-management manifest.
-- topology_manifest_id = manifest_id, same convention as manifest 092/ad200.
INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ae201'::uuid,
    'admin.enum.management.projection',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ae200'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

-- admin-enum-management UI persistence (package/layout/wiring/tensor).
INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae202',
    'admin.enum.management.projection.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey":"admin.enum.management.projection","surface":"admin.enum.management.projection","categoryKeys":["enum_dictionary"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

-- Manifest-facing package authority (manifest.topology[ui_projection].packageIds
-- points here, not at ui_component_package above -- same split as manifest
-- 092/ad200). No component+design pairs were authored via UI Component
-- Builder for this surface, so layout is honestly empty.
INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ae203',
    'admin.enum.management.projection.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

-- Structural authority tree: React-like Schema -> translator ->
-- topology_ui_seed_record records[], adopted directly (see header comment).
-- Category enum_dictionary > Section enum_dictionary_roster >
-- Field enum_search / Field enum_group_filter / Table enum_table /
-- Form enum_create_group_form (Field enum_create_group_name_input + Action
-- enum_create_group_button) / Action enum_delete_group_button (opens the
-- Modal below, no direct write -- round 24, replaces the round 20
-- enum_delete_group_form/enum_delete_group_confirm_input design, see below) /
-- Modal enum_delete_group_confirm_modal (Action enum_delete_group_confirm_button
-- + Action enum_delete_group_cancel_button) + Validation enum_write_dispatch_gap
-- (documents the write-step gap, see header comment).
--
-- enum_create_group_form (round 19, admin-write-surface-selection-context-and-
-- mode-composition-gap Bundle): the FIRST of the 9 write/read operations
-- (create_group) wired directly into ae200's own single-surface layout via
-- the admin_runtime_dispatch_override_wiring lane
-- (.agent/tests/fixtures/react-schema-topology-seed-translator/
-- admin-enum-ae200.input.json / admin-enum-ae200.topology-seed.input.json --
-- these three records and the matching tensor node below are the DIRECT,
-- unedited generate-react-schema -> generate-topology-seed output for this
-- addition, adopted verbatim). Its Action's eventBinding.targetRef
-- ("manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group")
-- is ae210's OWN dedicated create_group manifest -- clicking this button
-- dispatches enum_dictionary:create_group under ae210's own
-- dispatcher_mapping/capability_requirement authority exactly as if the
-- button lived on ae210's own single-purpose screen (see
-- admin-uibuilder-ui-structure-wiring-ssot.yaml
-- admin_runtime_target_ref_override_contract).
--
-- delete_group (round 20, same Bundle): the SECOND operation wired in, and
-- the first one that needs an EXISTING record's identity rather than fresh
-- user input. enum_table's row-select event now ALSO writes `value: row`
-- into its own emitBoundEvent payload (runtimeComponentFactory.ts
-- tableFactory) -- the exact same universal Lane 3 node-value-tracking branch
-- every input/select-family component already relies on
-- (`"value" in payload` -> onNodeValueChange), just supplying the key a table
-- select event previously never carried. That tracked row object is then
-- addressed via payloadFromResolver.ts's new node:<id>.value.<field>
-- dotted-path extension (node:enum_table.value.groupId) -- a payloadFrom
-- source form that did not exist before round 20; owning SSOT:
-- docs/design/ui-builder-preset-ecosystem-ssot.yaml
-- payloadFrom_resolver_contract.recognized_source_patterns.node_value_path.
--
-- delete_group confirm dialog (round 24, admin-enum subBundle, mutation-
-- confirmation-workflow): round 20/21's delete_group_button dispatched the
-- real write DIRECTLY on click, with a decorative, never-read
-- enum_delete_group_confirm_input field wired to nothing -- an audited
-- correctness gap (a live confirmed:literal:true write with no actual
-- confirmation step, and a displayed-but-unused confirmation input). Fixed
-- by wiring the mutation-confirmation-workflow using the SAME format
-- admin/uibuilder's own NodeEventAuthoringPanel.tsx authoring flow and
-- backend/repository/NpgsqlUiTopologyRepository.cs ValidateRuntimeInteractions
-- actually persist/validate for an overlay (disclosure/modal +
-- openModal/closeModal/toggleModal actionTypes with targetNodeId/statePath),
-- NOT the older internal_instance_wiring/localStateMutation/ui-local: shape
-- (verified NOT persistable for an active-topology layout -- see
-- docs/design/react-schema-topology-seed-translator-ssot.yaml
-- wiring_lane_contract.lanes.disclosure_state_wiring for the full
-- investigation). enum_delete_group_button now only opens
-- enum_delete_group_confirm_modal (no write, no payloadFrom at all);
-- enum_delete_group_confirm_button carries BOTH the SAME
-- dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger override
-- round 20/21 already established (groupId still re-resolved fresh from
-- node:enum_table.value.groupId at confirm-click time -- never a captured/
-- stale selection, and emitBoundEvent fails the whole click closed with no
-- write and no modal-close if that resolution fails) AND an additional
-- closeModal runtimeInteractions[] entry on the same click
-- (secondaryDisclosureActionType, wiring_lane_contract.lanes
-- .disclosure_state_wiring.secondary_combination); enum_delete_group_cancel_button
-- only closes the modal, sending no write. The unused confirm-input field is
-- retired rather than left displayed-but-ignored. .agent/scripts/
-- react_schema_topology_seed_translator.py DSL extension for Modal/
-- disclosure_state_wiring, and backend/repository/LayoutSchemaTensorComposer.cs
-- recognizing topology_ui_modal, were both added this round to make this the
-- direct, unedited generate-react-schema -> generate-topology-seed output
-- (.agent/tests/fixtures/react-schema-topology-seed-translator/
-- admin-enum-ae200.input.json / admin-enum-ae200.topology-seed.input.json),
-- not a hand-authored shape.
--
-- The remaining 5 operations (update_group/create_item/update_item/
-- delete_item/set_group_items -- get_group/list_groups already served by this
-- layout's own default target_ref) are NOT yet wired into this surface -- see
-- enum_write_dispatch_gap's updated label below and .agent/tasks/todo.md.
INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae204',
    'admin.enum.management.projection.layout',
    'fixed_form_projection',
    '{"records":[{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"admin_enum_management_projection","record":{"recordType":"topology_ui_category","key":"enum_dictionary","label":"Enum dictionary","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum"],"sourceReactPath":"$.root.children[0]","knownGapRefs":[],"categoryKey":"enum_dictionary","sectionKeys":["enum_dictionary_roster"]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary","record":{"recordType":"topology_ui_section","key":"enum_dictionary_roster","label":"Enum groups and items","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract"],"sourceReactPath":"$.root.children[0].children[0]","knownGapRefs":[],"sectionKey":"enum_dictionary_roster","sectionKind":"enum_group_and_item_management_projection","childKeys":["enum_search","enum_group_filter","enum_table","enum_create_group_name_input","enum_create_group_button","enum_create_group_confirm_modal","enum_update_group_name_input","enum_update_group_button","enum_update_group_confirm_modal","enum_delete_group_button","enum_delete_group_confirm_modal","enum_create_item_name_input","enum_create_item_button","enum_create_item_confirm_modal","enum_update_item_index_input","enum_update_item_name_input","enum_update_item_button","enum_update_item_confirm_modal","enum_delete_item_index_input","enum_delete_item_button","enum_delete_item_confirm_modal","enum_set_group_items_input","enum_set_group_items_button","enum_set_group_items_confirm_modal","enum_write_dispatch_preview_gap","enum_write_dispatch_gap"]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_field","key":"enum_search","label":"Enum search","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.component_tree"],"sourceReactPath":"$.root.children[0].children[0].children[0]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","fieldKey":"enum_search","control":"form_input/search_input","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"","optionsLabelPath":"","optionsValuePath":"","eventBinding":{"trigger":"change","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups","authority":"draft_or_projection_only","payloadFrom":{"search":"node:enum_search.value","groupNameFilter":"node:enum_group_filter.value"}},"debounceMs":300,"adminRuntimeDispatchOverride":{"trigger":"change","targetRef":"manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups","payloadFrom":{"search":"node:enum_search.value","groupNameFilter":"node:enum_group_filter.value"},"sourceActionKey":"enum_search"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_field","key":"enum_group_filter","label":"Group filter","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.component_tree"],"sourceReactPath":"$.root.children[0].children[0].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","fieldKey":"enum_group_filter","control":"form_input/select","required":false,"validationRefs":[],"valueFrom":"","optionsSource":"emission.data.groupOptions","optionsLabelPath":"groupName","optionsValuePath":"groupName","eventBinding":{"trigger":"change","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups","authority":"draft_or_projection_only","payloadFrom":{"search":"node:enum_search.value","groupNameFilter":"node:enum_group_filter.value"}},"adminRuntimeDispatchOverride":{"trigger":"change","targetRef":"manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups","payloadFrom":{"search":"node:enum_search.value","groupNameFilter":"node:enum_group_filter.value"},"sourceActionKey":"enum_group_filter"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_table","key":"enum_table","label":"Enum groups and items table","sourceYamlRefs":["enum-dictionary-ssot.yaml#canonical_tables"],"sourceReactPath":"$.root.children[0].children[0].children[2]","knownGapRefs":[],"tableKey":"enum_table","source":"enum.groups","display":"table","displayColumns":[{"key":"groupName","header":"Group name"},{"key":"indexNum","header":"Index"},{"key":"groupId","header":"Group ID"},{"key":"itemsSummary","header":"Items"}],"rowsSource":"emission.data.groups","columnKeys":[]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_field","key":"enum_create_group_name_input","label":"Group name","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.component_tree"],"sourceReactPath":"$.root.children[0].children[0].children[3]","knownGapRefs":[],"fieldKey":"enum_create_group_name_input","control":"form_input/input","required":true,"validationRefs":[],"valueFrom":""}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_action","key":"enum_create_group_button","label":"Create group","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[4]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"enum_create_group_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group","authority":"preview_only","payloadFrom":{"groupName":"node:enum_create_group_name_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"enum_create_group_confirm_modal","statePath":"open","sourceActionKey":"enum_create_group_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group","payloadFrom":{"groupName":"node:enum_create_group_name_input.value","dryRun":"literal:true"},"sourceActionKey":"enum_create_group_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_modal","key":"enum_create_group_confirm_modal","label":"Create group confirmation dialog","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[5]","knownGapRefs":[],"modalKey":"enum_create_group_confirm_modal","componentKind":"disclosure/modal","title":"Create group","body":"Create a new enum group with the entered name.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_create_group_confirm_modal","statePath":"open","sourceActionKey":"enum_create_group_confirm_modal"}],"childKeys":["enum_create_group_confirm_button","enum_create_group_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_create_group_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_create_group_confirm_button","label":"Create","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[5].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"enum_create_group_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group","authority":"draft_apply_not_execution_authority","payloadFrom":{"groupName":"node:enum_create_group_name_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_create_group_confirm_modal","statePath":"open","sourceActionKey":"enum_create_group_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group","payloadFrom":{"groupName":"node:enum_create_group_name_input.value","confirmed":"literal:true"},"sourceActionKey":"enum_create_group_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_create_group_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_create_group_cancel_button","label":"Cancel","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[5].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"enum_create_group_cancel_button","actionRef":"ui-local:enum_create_group_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:enum_create_group_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"enum_create_group_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_create_group_confirm_modal","statePath":"open","sourceActionKey":"enum_create_group_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_field","key":"enum_update_group_name_input","label":"New group name","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.component_tree"],"sourceReactPath":"$.root.children[0].children[0].children[6]","knownGapRefs":[],"fieldKey":"enum_update_group_name_input","control":"form_input/input","required":true,"validationRefs":[],"valueFrom":"node:enum_table.value.groupName"}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_action","key":"enum_update_group_button","label":"Update selected group","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[7]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"enum_update_group_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group","authority":"preview_only","payloadFrom":{"groupId":"node:enum_table.value.groupId","groupName":"node:enum_update_group_name_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"enum_update_group_confirm_modal","statePath":"open","sourceActionKey":"enum_update_group_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group","payloadFrom":{"groupId":"node:enum_table.value.groupId","groupName":"node:enum_update_group_name_input.value","dryRun":"literal:true"},"sourceActionKey":"enum_update_group_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_modal","key":"enum_update_group_confirm_modal","label":"Update group confirmation dialog","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[8]","knownGapRefs":[],"modalKey":"enum_update_group_confirm_modal","componentKind":"disclosure/modal","title":"Update group","body":"Rename the selected enum group to the entered name.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_update_group_confirm_modal","statePath":"open","sourceActionKey":"enum_update_group_confirm_modal"}],"childKeys":["enum_update_group_confirm_button","enum_update_group_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_update_group_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_update_group_confirm_button","label":"Update","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[8].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"enum_update_group_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group","authority":"draft_apply_not_execution_authority","payloadFrom":{"groupId":"node:enum_table.value.groupId","groupName":"node:enum_update_group_name_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_update_group_confirm_modal","statePath":"open","sourceActionKey":"enum_update_group_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group","payloadFrom":{"groupId":"node:enum_table.value.groupId","groupName":"node:enum_update_group_name_input.value","confirmed":"literal:true"},"sourceActionKey":"enum_update_group_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_update_group_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_update_group_cancel_button","label":"Cancel","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[8].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"enum_update_group_cancel_button","actionRef":"ui-local:enum_update_group_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:enum_update_group_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"enum_update_group_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_update_group_confirm_modal","statePath":"open","sourceActionKey":"enum_update_group_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_action","key":"enum_delete_group_button","label":"Delete selected group","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[9]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"enum_delete_group_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group","authority":"preview_only","payloadFrom":{"groupId":"node:enum_table.value.groupId","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"enum_delete_group_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_group_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group","payloadFrom":{"groupId":"node:enum_table.value.groupId","dryRun":"literal:true"},"sourceActionKey":"enum_delete_group_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_modal","key":"enum_delete_group_confirm_modal","label":"Delete group confirmation dialog","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[10]","knownGapRefs":[],"modalKey":"enum_delete_group_confirm_modal","componentKind":"disclosure/modal","title":"Delete group","body":"This will permanently delete the selected enum group and its items. This cannot be undone.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_delete_group_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_group_confirm_modal"}],"childKeys":["enum_delete_group_confirm_button","enum_delete_group_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_delete_group_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_delete_group_confirm_button","label":"Delete","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[10].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"enum_delete_group_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group","authority":"draft_apply_not_execution_authority","payloadFrom":{"groupId":"node:enum_table.value.groupId","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_delete_group_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_group_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group","payloadFrom":{"groupId":"node:enum_table.value.groupId","confirmed":"literal:true"},"sourceActionKey":"enum_delete_group_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_delete_group_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_delete_group_cancel_button","label":"Cancel","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[10].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"enum_delete_group_cancel_button","actionRef":"ui-local:enum_delete_group_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:enum_delete_group_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"enum_delete_group_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_delete_group_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_group_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_field","key":"enum_create_item_name_input","label":"Item name","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.component_tree"],"sourceReactPath":"$.root.children[0].children[0].children[11]","knownGapRefs":[],"fieldKey":"enum_create_item_name_input","control":"form_input/input","required":true,"validationRefs":[],"valueFrom":""}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_action","key":"enum_create_item_button","label":"Create item","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[12]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"enum_create_item_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item","authority":"preview_only","payloadFrom":{"name":"node:enum_create_item_name_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"enum_create_item_confirm_modal","statePath":"open","sourceActionKey":"enum_create_item_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item","payloadFrom":{"name":"node:enum_create_item_name_input.value","dryRun":"literal:true"},"sourceActionKey":"enum_create_item_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_modal","key":"enum_create_item_confirm_modal","label":"Create item confirmation dialog","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[13]","knownGapRefs":[],"modalKey":"enum_create_item_confirm_modal","componentKind":"disclosure/modal","title":"Create item","body":"Create a new enum item with the entered name.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_create_item_confirm_modal","statePath":"open","sourceActionKey":"enum_create_item_confirm_modal"}],"childKeys":["enum_create_item_confirm_button","enum_create_item_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_create_item_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_create_item_confirm_button","label":"Create","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[13].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"enum_create_item_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item","authority":"draft_apply_not_execution_authority","payloadFrom":{"name":"node:enum_create_item_name_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_create_item_confirm_modal","statePath":"open","sourceActionKey":"enum_create_item_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item","payloadFrom":{"name":"node:enum_create_item_name_input.value","confirmed":"literal:true"},"sourceActionKey":"enum_create_item_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_create_item_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_create_item_cancel_button","label":"Cancel","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[13].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"enum_create_item_cancel_button","actionRef":"ui-local:enum_create_item_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:enum_create_item_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"enum_create_item_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_create_item_confirm_modal","statePath":"open","sourceActionKey":"enum_create_item_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_field","key":"enum_update_item_index_input","label":"Item index","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.component_tree"],"sourceReactPath":"$.root.children[0].children[0].children[14]","knownGapRefs":[],"fieldKey":"enum_update_item_index_input","control":"form_input/input","required":true,"validationRefs":[],"valueFrom":""}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_field","key":"enum_update_item_name_input","label":"New item name","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.component_tree"],"sourceReactPath":"$.root.children[0].children[0].children[15]","knownGapRefs":[],"fieldKey":"enum_update_item_name_input","control":"form_input/input","required":true,"validationRefs":[],"valueFrom":""}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_action","key":"enum_update_item_button","label":"Update item","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[16]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"enum_update_item_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item","authority":"preview_only","payloadFrom":{"indexNum":"node:enum_update_item_index_input.value","name":"node:enum_update_item_name_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"enum_update_item_confirm_modal","statePath":"open","sourceActionKey":"enum_update_item_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item","payloadFrom":{"indexNum":"node:enum_update_item_index_input.value","name":"node:enum_update_item_name_input.value","dryRun":"literal:true"},"sourceActionKey":"enum_update_item_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_modal","key":"enum_update_item_confirm_modal","label":"Update item confirmation dialog","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[17]","knownGapRefs":[],"modalKey":"enum_update_item_confirm_modal","componentKind":"disclosure/modal","title":"Update item","body":"Rename the enum item at the entered index to the entered name.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_update_item_confirm_modal","statePath":"open","sourceActionKey":"enum_update_item_confirm_modal"}],"childKeys":["enum_update_item_confirm_button","enum_update_item_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_update_item_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_update_item_confirm_button","label":"Update","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[17].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"enum_update_item_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item","authority":"draft_apply_not_execution_authority","payloadFrom":{"indexNum":"node:enum_update_item_index_input.value","name":"node:enum_update_item_name_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_update_item_confirm_modal","statePath":"open","sourceActionKey":"enum_update_item_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item","payloadFrom":{"indexNum":"node:enum_update_item_index_input.value","name":"node:enum_update_item_name_input.value","confirmed":"literal:true"},"sourceActionKey":"enum_update_item_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_update_item_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_update_item_cancel_button","label":"Cancel","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[17].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"enum_update_item_cancel_button","actionRef":"ui-local:enum_update_item_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:enum_update_item_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"enum_update_item_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_update_item_confirm_modal","statePath":"open","sourceActionKey":"enum_update_item_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_field","key":"enum_delete_item_index_input","label":"Item index to delete","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.component_tree"],"sourceReactPath":"$.root.children[0].children[0].children[18]","knownGapRefs":[],"fieldKey":"enum_delete_item_index_input","control":"form_input/input","required":true,"validationRefs":[],"valueFrom":""}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_action","key":"enum_delete_item_button","label":"Delete item","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[19]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"enum_delete_item_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item","authority":"preview_only","payloadFrom":{"indexNum":"node:enum_delete_item_index_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"enum_delete_item_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_item_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item","payloadFrom":{"indexNum":"node:enum_delete_item_index_input.value","dryRun":"literal:true"},"sourceActionKey":"enum_delete_item_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_modal","key":"enum_delete_item_confirm_modal","label":"Delete item confirmation dialog","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[20]","knownGapRefs":[],"modalKey":"enum_delete_item_confirm_modal","componentKind":"disclosure/modal","title":"Delete item","body":"This will permanently delete the enum item at the entered index. This cannot be undone.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_delete_item_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_item_confirm_modal"}],"childKeys":["enum_delete_item_confirm_button","enum_delete_item_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_delete_item_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_delete_item_confirm_button","label":"Delete","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[20].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"enum_delete_item_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item","authority":"draft_apply_not_execution_authority","payloadFrom":{"indexNum":"node:enum_delete_item_index_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_delete_item_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_item_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item","payloadFrom":{"indexNum":"node:enum_delete_item_index_input.value","confirmed":"literal:true"},"sourceActionKey":"enum_delete_item_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_delete_item_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_delete_item_cancel_button","label":"Cancel","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[20].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"enum_delete_item_cancel_button","actionRef":"ui-local:enum_delete_item_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:enum_delete_item_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"enum_delete_item_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_delete_item_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_item_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_field","key":"enum_set_group_items_input","label":"Item indexes (comma-separated)","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.component_tree"],"sourceReactPath":"$.root.children[0].children[0].children[21]","knownGapRefs":[],"fieldKey":"enum_set_group_items_input","control":"form_input/input","required":true,"validationRefs":[],"valueFrom":"node:enum_table.value.itemsIndexNums","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_action","key":"enum_set_group_items_button","label":"Set selected group''s items","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[22]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"enum_set_group_items_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items","authority":"preview_only","payloadFrom":{"groupId":"node:enum_table.value.groupId","enumIndexNums":"node:enum_set_group_items_input.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"enum_set_group_items_confirm_modal","statePath":"open","sourceActionKey":"enum_set_group_items_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items","payloadFrom":{"groupId":"node:enum_table.value.groupId","enumIndexNums":"node:enum_set_group_items_input.value","dryRun":"literal:true"},"sourceActionKey":"enum_set_group_items_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_modal","key":"enum_set_group_items_confirm_modal","label":"Set group items confirmation dialog","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[23]","knownGapRefs":[],"modalKey":"enum_set_group_items_confirm_modal","componentKind":"disclosure/modal","title":"Set group items","body":"Replace the selected group''s item membership with the entered indexes.","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_set_group_items_confirm_modal","statePath":"open","sourceActionKey":"enum_set_group_items_confirm_modal"}],"childKeys":["enum_set_group_items_confirm_button","enum_set_group_items_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_set_group_items_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_set_group_items_confirm_button","label":"Set items","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[23].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"enum_set_group_items_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items","authority":"draft_apply_not_execution_authority","payloadFrom":{"groupId":"node:enum_table.value.groupId","enumIndexNums":"node:enum_set_group_items_input.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_set_group_items_confirm_modal","statePath":"open","sourceActionKey":"enum_set_group_items_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items","payloadFrom":{"groupId":"node:enum_table.value.groupId","enumIndexNums":"node:enum_set_group_items_input.value","confirmed":"literal:true"},"sourceActionKey":"enum_set_group_items_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_set_group_items_confirm_modal","record":{"recordType":"topology_ui_action","key":"enum_set_group_items_cancel_button","label":"Cancel","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[23].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"enum_set_group_items_cancel_button","actionRef":"ui-local:enum_set_group_items_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:enum_set_group_items_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"enum_set_group_items_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_set_group_items_confirm_modal","statePath":"open","sourceActionKey":"enum_set_group_items_cancel_button"}]}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_validation","key":"enum_write_dispatch_preview_gap","label":"All 7 enum_dictionary write actions (create_group/update_group/delete_group/create_item/update_item/delete_item/set_group_items) now dispatch a non-mutating dryRun preview (payload.dryRun=literal:true, the SAME target manifest and the SAME payloadFrom field mapping the eventual confirmed write uses) when their own visible action button is clicked, via the SAME admin_runtime_dispatch_override_wiring lane the confirm button already used -- no new wiringLane, actionType, or per-operation branch. The confirm modal opens only when that preview dispatch settles successfully (secondaryDisclosureActionType=openModal deferred to dispatch success, the same deferLocalStateMutationToDispatchSuccess mechanism the confirm buttons already used for secondaryDisclosureActionType=closeModal); a failed preview (backend validation error) never opens the modal and surfaces the backend error message via the existing non-destructive refreshWarning banner, leaving every typed field and the tracked table selection exactly as the user left them. Confirm re-resolves the SAME payloadFrom mapping fresh from current node values (never a captured preview-time value) and sends payload.confirmed=literal:true; Cancel never dispatches anything. A settled dryRun preview result is classified by its own dispatched payload dryRun flag -- generic runtime context, never operation name/nodeId/manifest UUID -- and is therefore never adopted into this surface own projection state and never triggers the canonical re-read that only a settled CONFIRMED write success triggers.","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.enum.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[24]","knownGapRefs":[],"validationKey":"enum_write_dispatch_preview_gap","rule":"admin_runtime_dryrun_preview_gated_confirm_modal","severity":"resolved"}},{"type":"topology_ui_seed_record","seedKey":"admin.enum.management.projection","parentKey":"enum_dictionary_roster","record":{"recordType":"topology_ui_validation","key":"enum_write_dispatch_gap","label":"All 7 enum_dictionary write actions (create_group/update_group/delete_group/create_item/update_item/delete_item/set_group_items) are now wired into this single surface via admin_runtime_dispatch_override_wiring to their own dedicated per-action manifests (ae210/ae220/ae230/ae240/ae250/ae260/ae270), each gated behind its own disclosure/modal explicit-confirm dialog (open only via its own button, no write on cancel/backdrop-close, confirm dispatches the SAME override as before with group-identity fields (groupId) re-resolved fresh from enum_table own tracked selected-row value node:enum_table.value.groupId at confirm time -- never a captured/stale selection -- and fails closed with no write and no modal-close if that resolution fails); item-identity fields (indexNum) for update_item/delete_item/set_group_items are entered as typed values, matching the same manual-identity entry pattern already established by each operation own dedicated single-purpose write manifest -- items browse is composed into the existing enum_table (enum_table''s displayColumns now carries itemsSummary, a flattened index:name list per group, folded into list_groups own query -- no new list_items action, no cross-manifest dispatch, no direct child-Emission adoption). group-items membership is entered as a comma-separated indexNum list, matching the existing backend CSV-string enumIndexNums contract.","sourceYamlRefs":["react-schema-topology-seed-translator-ssot.yaml#declared_seed_surface_catalog.known_declared_surfaces.admin_enum_management_projection.known_gaps"],"sourceReactPath":"$.root.children[0].children[0].children[25]","knownGapRefs":[],"validationKey":"enum_write_dispatch_gap","rule":"admin_runtime_write_target_fully_resolved_via_this_surface_manual_item_identity_entry","severity":"resolved"}}]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

-- Read circuit (2026-07-23, admin-runtime-operation-dispatch-lane-determination
-- concrete boundary consumption, phase 1 of 2): wiring_kind="admin_runtime" +
-- target_ref="manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups"
-- (target_ref must stay a valid manifest:<uuid>:<key> reference for
-- ManifestDispatcher's OWN manifest resolution -- request.Layer/request.Action
-- carry "enum_dictionary"/"list_groups" as separate top-level dispatch fields,
-- not encoded inside target_ref; see frontend/runtime/renderEmission.ts
-- parseAdminRuntimeLayerAction's doc comment for the live-DB proof that caught
-- an earlier bare "<layer>:<action>" targetRef failing TARGET_REF_INVALID)
-- makes this layout's WHOLE screen
-- (search/filter/table -- all one canonical "list enum groups" operation, per
-- admin-runtime-operation-dispatch-lane-determination Bundle's
-- Bundle-unit-composition resolution: a layout is safe to bind uniformly to
-- ONE admin_runtime action only when the entire layout genuinely IS that one
-- operation) dispatch the real, existing enum_dictionary:list_groups action
-- via ManifestDispatcher -> AdminRuntimeDispatchAdapter ->
-- AdminRuntime.ExecuteDataAsync, exactly like every other admin_runtime
-- manifest. search_input/group_filter's own change/select triggers are NOT
-- overridden -- they safely re-issue the same no-payload, idempotent
-- list_groups read (harmless, if not yet a real server-side filter -- see
-- header comment's known_gaps for why: there is no
-- enum_dictionary:search/filter action, and no existing mechanism captures
-- free-typed field values into a dispatchable payload in production; adding
-- one is out of scope here, not fabricated as already solved).
INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae205',
    'admin.enum.management.projection.wiring',
    'admin_runtime',
    'manifest',
    'manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups',
    '{"actions":[{"wiringKey":"admin.enum.management.projection.read.wiring","wiringKind":"admin_runtime","targetSurface":"manifest","targetRef":"manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups","note":"Layout-wide dispatch spec (frontend/runtime/renderEmission.ts mapWiringKindToLayer/mapWiringKindToAction parseAdminRuntimeLayerAction). targetRef MUST stay a valid manifest:<uuid>:<key> reference -- ManifestDispatcher.TryParseManifestTargetRef (backend/runtime/ManifestDispatcher.cs) only validates the first two colon-separated segments (manifest: prefix + this manifest own UUID) for MANIFEST resolution; the remaining \"enum_dictionary:list_groups\" segment is free-text there and unused for that resolution step -- it is what parseAdminRuntimeLayerAction reads as layer:action. This wiring_schema_json content itself has no runtime/frontend consumer, see react-schema-topology-seed-translator-ssot.yaml wiring_lane_contract.known_gaps; it documents the flat wiring_kind/target_surface/target_ref columns above, which ARE the real, consumed dispatch config."}]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        target_surface = EXCLUDED.target_surface,
        target_ref = EXCLUDED.target_ref,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

-- Tensor carries two independent node-local contributions, merged onto the
-- schema-composed leaves by exact NodeId match
-- (backend/repository/LayoutSchemaTensorComposer.cs BuildNodeLocalDataByNodeId/
-- Compose, added in this pass -- previously only RuntimeInteractions merged
-- through for schema-composed layouts; PropsJson/StateJson/PropBindingsJson on
-- a tensor node were silently unused for this layout shape until now):
-- 1. enum_table: propsJson clears the inert data_display/table preview
--    placeholder's nested `table` object (table:null -- frontend/runtime/
--    runtimeComponentFactory.ts tableFactory falls back to top-level props
--    when `table` is not a non-null object) and supplies real static
--    columns; propBindings.rows binds emission.data (the raw
--    EnumDictionaryGroupDto[] array enum_dictionary:list_groups returns)
--    directly, per backend/runtime/StructureMapResolver.cs
--    ComponentArrayPropCapabilities["data_display/table"] = {rows, columns}.
-- 2. enum_create_group_button (round 19): dispatchTargetRefByTrigger/
--    dispatchPayloadFromByTrigger merge target, nodeId matches the LEAF
--    Action's OWN resolved record key directly (NOT an owning form's key)
--    -- LayoutSchemaTensorComposer.Compose merges these two fields
--    via a plain NodeId match against a catalog leaf
--    (nodeLocalDataByNodeId), never via BuildInteractionsBySourceActionKey's
--    sourceActionKey scoping that only applies to runtimeInteractionsJson. A
--    live-DB round trip caught the translator originally keying this
--    override by the owning form's nodeId (a topology_ui_form record, which
--    Compose classifies as structural, so its isCatalogLeaf-gated merge
--    silently never attached the override to anything) -- fixed in
--    .agent/scripts/react_schema_topology_seed_translator.py's tensor-node
--    emission (round 19). "click" carries groupName from
--    enum_create_group_name_input's own DOM value and a literal
--    confirmed:true, dispatched to ae210's own enum_dictionary:create_group
--    under ae210's own dispatcher_mapping/capability_requirement authority
--    (see comment above components_layout_design INSERT).
-- 3. enum_delete_group_confirm_button (round 20, retargeted round 24 from
--    enum_delete_group_button): same dispatchTargetRefByTrigger/
--    dispatchPayloadFromByTrigger merge as #2, keyed by its own leaf record
--    key. Its payloadFrom.groupId ("node:enum_table.value.groupId") is the
--    first payloadFrom source in this codebase to read a DIFFERENT node's
--    tracked value by field, not just a form input's own .value -- see
--    frontend/runtime/payloadFromResolver.ts's node:<id>.value.<path>
--    extension and runtimeComponentFactory.ts tableFactory's onRowClick
--    (round 20, owning SSOT: ui-builder-preset-ecosystem-ssot.yaml
--    payloadFrom_resolver_contract.recognized_source_patterns.node_value_path).
--    round 24 also gives it a SECOND, independent runtimeInteractions[]
--    entry (closeModal, targeting enum_delete_group_confirm_modal) on the
--    same "click" trigger -- dispatchTargetRefByTrigger/
--    dispatchPayloadFromByTrigger and runtimeInteractions[] are separate
--    node-local fields (see #2's own note), so both apply independently to
--    the same click.
-- 4. enum_delete_group_button (round 24): no longer carries
--    dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger at all -- it
--    only opens enum_delete_group_confirm_modal (runtimeInteractions[]
--    openModal), the real write moved to #3 above.
-- 5. enum_dictionary_roster / enum_delete_group_confirm_modal /
--    enum_delete_group_cancel_button (round 24, propsJson shape corrected
--    round 25): the Section-scoped self-close-on-toggle interaction (backend/
--    repository/LayoutSchemaTensorComposer.cs Compose merges a catalog leaf's
--    OWN runtimeInteractions by "{leaf's resolved ParentNodeId}::{leaf's own
--    key}" -- for the Modal leaf that parent is the Section, hence this
--    tensor entry's nodeId is "enum_dictionary_roster", not the Modal's own
--    key), the Modal's own propsJson, and the Cancel button's closeModal-only
--    interaction, respectively. enum_delete_group_confirm_modal's propsJson
--    MUST nest title/body/open under a "data" object and set "open":false
--    explicitly -- frontend/runtime/renderEmission.ts mergeNodeLocalProps does
--    a SHALLOW top-level merge of propsJson onto the componentKind's default
--    props, and frontend/runtime/layoutComponentPreview.ts's
--    "disclosure/modal" default is {data:{open:true,title:"Modal",
--    body:"プレビュー"}} -- a flat top-level {title,body} propsJson (round
--    24's original, uncorrected shape) would add new top-level keys while
--    leaving that default's own nested `data` (still open:true) completely
--    untouched, so modalFactory (which reads props.data when present) would
--    silently keep showing the placeholder open by default with the wrong
--    title/body, never this node's authored content. Caught by a real
--    ProjectionShell DOM mount test (round 25), not by any Composer/live-DB
--    proof alone -- see frontend/tests/
--    projectionShellAdminRuntimeWritePayloadCapture.test.ts's confirm-modal
--    scenarios.
-- componentId/componentKind for every leaf (enum_search/enum_group_filter/
-- enum_table/enum_create_group_name_input/
-- enum_create_group_button/enum_delete_group_button/
-- enum_delete_group_confirm_modal/enum_delete_group_confirm_button/
-- enum_delete_group_cancel_button) resolve from components_layout_design.records[]
-- above via the existing ui_component_registry preset catalog (Modal's
-- componentKind is the record's own literal value, never a registry lookup --
-- see LayoutSchemaTensorComposer.cs's ModalRecordType handling).
INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ae206',
    'admin#enum_management',
    '00000000-0000-0000-0000-0000000ae202',
    '00000000-0000-0000-0000-0000000ae204',
    '00000000-0000-0000-0000-0000000ae205',
    'default',
    0,
    '{"nodes":[{"nodeId":"enum_search","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"change":"manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups"},"dispatchPayloadFromByTrigger":{"change":{"search":"node:enum_search.value","groupNameFilter":"node:enum_group_filter.value"}},"debounceMs":300},{"nodeId":"enum_group_filter","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"change":"manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups"},"dispatchPayloadFromByTrigger":{"change":{"search":"node:enum_search.value","groupNameFilter":"node:enum_group_filter.value"}},"propBindings":{"options":{"source":"emission.data.groupOptions","transform":"rowsToOptions","labelPath":"groupName","valuePath":"groupName"}}},{"nodeId":"enum_table","nodeKind":"catalog_component","runtimeInteractions":[],"propsJson":"{\"table\": null, \"columns\": [{\"key\": \"groupName\", \"header\": \"Group name\"}, {\"key\": \"indexNum\", \"header\": \"Index\"}, {\"key\": \"groupId\", \"header\": \"Group ID\"}, {\"key\": \"itemsSummary\", \"header\": \"Items\"}]}","propBindings":{"rows":{"source":"emission.data.groups"}}},{"nodeId":"enum_dictionary_roster","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"enum_create_group_confirm_modal","statePath":"open","sourceActionKey":"enum_create_group_button"},{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_create_group_confirm_modal","statePath":"open","sourceActionKey":"enum_create_group_confirm_modal"},{"trigger":"click","actionType":"openModal","targetNodeId":"enum_update_group_confirm_modal","statePath":"open","sourceActionKey":"enum_update_group_button"},{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_update_group_confirm_modal","statePath":"open","sourceActionKey":"enum_update_group_confirm_modal"},{"trigger":"click","actionType":"openModal","targetNodeId":"enum_delete_group_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_group_button"},{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_delete_group_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_group_confirm_modal"},{"trigger":"click","actionType":"openModal","targetNodeId":"enum_create_item_confirm_modal","statePath":"open","sourceActionKey":"enum_create_item_button"},{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_create_item_confirm_modal","statePath":"open","sourceActionKey":"enum_create_item_confirm_modal"},{"trigger":"click","actionType":"openModal","targetNodeId":"enum_update_item_confirm_modal","statePath":"open","sourceActionKey":"enum_update_item_button"},{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_update_item_confirm_modal","statePath":"open","sourceActionKey":"enum_update_item_confirm_modal"},{"trigger":"click","actionType":"openModal","targetNodeId":"enum_delete_item_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_item_button"},{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_delete_item_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_item_confirm_modal"},{"trigger":"click","actionType":"openModal","targetNodeId":"enum_set_group_items_confirm_modal","statePath":"open","sourceActionKey":"enum_set_group_items_button"},{"trigger":"toggle","actionType":"closeModal","targetNodeId":"enum_set_group_items_confirm_modal","statePath":"open","sourceActionKey":"enum_set_group_items_confirm_modal"}]},{"nodeId":"enum_create_group_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group"},"dispatchPayloadFromByTrigger":{"click":{"groupName":"node:enum_create_group_name_input.value","dryRun":"literal:true"}}},{"nodeId":"enum_create_group_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_create_group_confirm_modal","statePath":"open","sourceActionKey":"enum_create_group_confirm_button"},{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_create_group_confirm_modal","statePath":"open","sourceActionKey":"enum_create_group_cancel_button"}],"propsJson":"{\"data\": {\"open\": false, \"title\": \"Create group\", \"body\": \"Create a new enum group with the entered name.\"}}"},{"nodeId":"enum_create_group_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group"},"dispatchPayloadFromByTrigger":{"click":{"groupName":"node:enum_create_group_name_input.value","confirmed":"literal:true"}}},{"nodeId":"enum_update_group_name_input","nodeKind":"catalog_component","runtimeInteractions":[],"propBindings":{"value":{"source":"node:enum_table.value.groupName"}}},{"nodeId":"enum_set_group_items_input","nodeKind":"catalog_component","runtimeInteractions":[],"propBindings":{"value":{"source":"node:enum_table.value.itemsIndexNums"}}},{"nodeId":"enum_update_group_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group"},"dispatchPayloadFromByTrigger":{"click":{"groupId":"node:enum_table.value.groupId","groupName":"node:enum_update_group_name_input.value","dryRun":"literal:true"}}},{"nodeId":"enum_update_group_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_update_group_confirm_modal","statePath":"open","sourceActionKey":"enum_update_group_confirm_button"},{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_update_group_confirm_modal","statePath":"open","sourceActionKey":"enum_update_group_cancel_button"}],"propsJson":"{\"data\": {\"open\": false, \"title\": \"Update group\", \"body\": \"Rename the selected enum group to the entered name.\"}}"},{"nodeId":"enum_update_group_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group"},"dispatchPayloadFromByTrigger":{"click":{"groupId":"node:enum_table.value.groupId","groupName":"node:enum_update_group_name_input.value","confirmed":"literal:true"}}},{"nodeId":"enum_delete_group_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group"},"dispatchPayloadFromByTrigger":{"click":{"groupId":"node:enum_table.value.groupId","dryRun":"literal:true"}}},{"nodeId":"enum_delete_group_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_delete_group_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_group_confirm_button"},{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_delete_group_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_group_cancel_button"}],"propsJson":"{\"data\": {\"open\": false, \"title\": \"Delete group\", \"body\": \"This will permanently delete the selected enum group and its items. This cannot be undone.\"}}"},{"nodeId":"enum_delete_group_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group"},"dispatchPayloadFromByTrigger":{"click":{"groupId":"node:enum_table.value.groupId","confirmed":"literal:true"}}},{"nodeId":"enum_create_item_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item"},"dispatchPayloadFromByTrigger":{"click":{"name":"node:enum_create_item_name_input.value","dryRun":"literal:true"}}},{"nodeId":"enum_create_item_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_create_item_confirm_modal","statePath":"open","sourceActionKey":"enum_create_item_confirm_button"},{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_create_item_confirm_modal","statePath":"open","sourceActionKey":"enum_create_item_cancel_button"}],"propsJson":"{\"data\": {\"open\": false, \"title\": \"Create item\", \"body\": \"Create a new enum item with the entered name.\"}}"},{"nodeId":"enum_create_item_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item"},"dispatchPayloadFromByTrigger":{"click":{"name":"node:enum_create_item_name_input.value","confirmed":"literal:true"}}},{"nodeId":"enum_update_item_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item"},"dispatchPayloadFromByTrigger":{"click":{"indexNum":"node:enum_update_item_index_input.value","name":"node:enum_update_item_name_input.value","dryRun":"literal:true"}}},{"nodeId":"enum_update_item_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_update_item_confirm_modal","statePath":"open","sourceActionKey":"enum_update_item_confirm_button"},{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_update_item_confirm_modal","statePath":"open","sourceActionKey":"enum_update_item_cancel_button"}],"propsJson":"{\"data\": {\"open\": false, \"title\": \"Update item\", \"body\": \"Rename the enum item at the entered index to the entered name.\"}}"},{"nodeId":"enum_update_item_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item"},"dispatchPayloadFromByTrigger":{"click":{"indexNum":"node:enum_update_item_index_input.value","name":"node:enum_update_item_name_input.value","confirmed":"literal:true"}}},{"nodeId":"enum_delete_item_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item"},"dispatchPayloadFromByTrigger":{"click":{"indexNum":"node:enum_delete_item_index_input.value","dryRun":"literal:true"}}},{"nodeId":"enum_delete_item_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_delete_item_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_item_confirm_button"},{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_delete_item_confirm_modal","statePath":"open","sourceActionKey":"enum_delete_item_cancel_button"}],"propsJson":"{\"data\": {\"open\": false, \"title\": \"Delete item\", \"body\": \"This will permanently delete the enum item at the entered index. This cannot be undone.\"}}"},{"nodeId":"enum_delete_item_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item"},"dispatchPayloadFromByTrigger":{"click":{"indexNum":"node:enum_delete_item_index_input.value","confirmed":"literal:true"}}},{"nodeId":"enum_set_group_items_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items"},"dispatchPayloadFromByTrigger":{"click":{"groupId":"node:enum_table.value.groupId","enumIndexNums":"node:enum_set_group_items_input.value","dryRun":"literal:true"}}},{"nodeId":"enum_set_group_items_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_set_group_items_confirm_modal","statePath":"open","sourceActionKey":"enum_set_group_items_confirm_button"},{"trigger":"click","actionType":"closeModal","targetNodeId":"enum_set_group_items_confirm_modal","statePath":"open","sourceActionKey":"enum_set_group_items_cancel_button"}],"propsJson":"{\"data\": {\"open\": false, \"title\": \"Set group items\", \"body\": \"Replace the selected group''s item membership with the entered indexes.\"}}"},{"nodeId":"enum_set_group_items_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items"},"dispatchPayloadFromByTrigger":{"click":{"groupId":"node:enum_table.value.groupId","enumIndexNums":"node:enum_set_group_items_input.value","confirmed":"literal:true"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- =============================================================================
-- admin-surface-topology-seed-conversion: admin-enum subBundle -- write-side
-- single-purpose write layouts (2026-07-27, PR #600 review round 2 correction).
--
-- docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml lane_storage_boundary
-- remaining_write_payload_capture_gap already settled this design question
-- (2026-07-23): "a single-purpose write layout, exactly like the read layout
-- above, is a safe and sufficient composition -- no per-node extension needed".
-- component_wiring_execution_lane's wiringKind/target_ref is a per-wiring-row
-- (whole-layout) binding, not per-node (proven by ae205 above, where enum_search/
-- enum_group_filter/enum_table all inherit the SAME list_groups target_ref
-- uniformly) -- so each of the 7 enum_dictionary write actions gets its OWN
-- dedicated hub/manifest/layout/wiring/tensor below, each single-purpose (layout =
-- 1 canonical admin_runtime action), reusing the exact same Lane 2 mechanism ae200's
-- own read circuit already proves, plus the node-level dispatchPayloadFromByTrigger
-- field (round 6-8 of admin-runtime-operation-dispatch-lane-determination) for
-- typed-value payload capture. No new component kind, action type, runtime lane, or
-- payload resolver -- only more instances of already-registered search_input.alias
-- (form_input/search_input, the only catalog component whose generic factory wires
-- a real onChange -> node-value-tracked event; form_field.template's generic factory
-- renders a static empty span, verified in frontend/runtime/runtimeComponentFactory.ts
-- formFieldFactory, so it cannot capture a typed value today) and button.primitive.
--
-- Each of the 7 owns its OWN dedicated hub (not one shared hub): hubs.topology_
-- manifests.LoadHubNavigationSequenceAsync (NpgsqlContentBundleRepository.cs) only
-- resolves a hub_relations row's target manifest when exactly one ACTIVE topology_
-- manifests row exists for that related_hub_id (HAVING COUNT(*) = 1) -- a shared hub
-- across multiple active manifests would resolve to NULL (ambiguous), making every
-- one of these 7 unreachable via hub_relations navigation. One hub per manifest, the
-- same discipline every other manifest in this file already follows.
--
-- Each layout carries two Action nodes sharing the SAME layout-wide wiringKind/
-- target_ref (the operation itself): preview_button (dispatchPayloadFromByTrigger
-- carries dryRun:literal:true -- preview_dictionary_delta/validate_against_enum_
-- authority, non-mutating) and confirm_button (confirmed:literal:true -- explicit_
-- confirm -> write -> diff_log via the existing AdminMasterRosterAudit.AppendAsync,
-- unchanged). cancel is simply never clicking confirm_button. Field nodes
-- (search_input.alias) also inherit the SAME layout-wide Lane 2 binding on their own
-- change trigger (no dispatchPayloadFromByTrigger of their own) -- their raw event-
-- time payload ({value:...}) does not match the action's expected request shape, so
-- every keystroke harmlessly fails ENUM_*_PAYLOAD_REQUIRED without persisting, the
-- exact same accepted tradeoff already documented for ae200's read circuit ("wasteful,
-- not incorrect") -- not a new risk this Bundle introduces.
--
-- All 7 also stay reachable via explicit ?manifest=<id> selection like ae200 itself;
-- no hub_relations seed rows are authored here -- that remains an ordinary /admin/
-- manifests admin/runtime action (hub_navigation:create), proven end to end by
-- AdminEnumHubRelationUiProjectionLiveDbTests.cs, same discipline as ae200 itself and
-- every other subBundle manifest in this file).
-- =============================================================================

-- single-purpose write layout: enum_dictionary:create_group
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ae211', '{"description": "admin_enum_management_write_create_group", "system": true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae210',
    NULL,
    ARRAY[
        '{"type": "hub_grouping", "manifestKey": "admin.enum.management.write.create_group", "bundle": "admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type": "dispatcher_mapping", "role": "admin", "target": "manifest", "layer": "enum_dictionary", "action": "create_group"}'::jsonb,
        '{"type": "runtime_mapping", "runtime_destination": "admin_runtime"}'::jsonb,
        '{"type": "ui_projection", "packageIds": ["00000000-0000-0000-0000-0000000ae213"], "layoutId": "00000000-0000-0000-0000-0000000ae214", "wiringId": "00000000-0000-0000-0000-0000000ae215", "tensorId": "00000000-0000-0000-0000-0000000ae216"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ae211'::uuid,
    'admin.enum.management.write.create_group',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ae210'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET hub_id         = EXCLUDED.hub_id,
        manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae212',
    'admin.enum.management.write.create_group.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey": "admin.enum.management.write.create_group", "surface": "admin.enum.management.write.create_group", "categoryKeys": ["enum_dictionary"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ae213',
    'admin.enum.management.write.create_group.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae214',
    'admin.enum.management.write.create_group.layout',
    'ui_builder_canvas',
    '{"records":[]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae215',
    'admin.enum.management.write.create_group.wiring',
    'admin_runtime',
    'manifest',
    'manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group',
    '{"actions":[]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        target_surface = EXCLUDED.target_surface,
        target_ref = EXCLUDED.target_ref,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ae216',
    'admin#enum_management_write_create_group',
    '00000000-0000-0000-0000-0000000ae212',
    '00000000-0000-0000-0000-0000000ae214',
    '00000000-0000-0000-0000-0000000ae215',
    'default',
    0,
    '{"nodes": [{"nodeId": "group_name_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 0}, {"nodeId": "preview_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 1, "propsJson": "{\"label\": \"Preview\"}", "dispatchPayloadFromByTrigger": {"click": {"groupName": "node:group_name_field.value", "dryRun": "literal:true"}}}, {"nodeId": "confirm_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 2, "propsJson": "{\"label\": \"Confirm & write\"}", "dispatchPayloadFromByTrigger": {"click": {"groupName": "node:group_name_field.value", "confirmed": "literal:true"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- single-purpose write layout: enum_dictionary:update_group
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ae221', '{"description": "admin_enum_management_write_update_group", "system": true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae220',
    NULL,
    ARRAY[
        '{"type": "hub_grouping", "manifestKey": "admin.enum.management.write.update_group", "bundle": "admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type": "dispatcher_mapping", "role": "admin", "target": "manifest", "layer": "enum_dictionary", "action": "update_group"}'::jsonb,
        '{"type": "runtime_mapping", "runtime_destination": "admin_runtime"}'::jsonb,
        '{"type": "ui_projection", "packageIds": ["00000000-0000-0000-0000-0000000ae223"], "layoutId": "00000000-0000-0000-0000-0000000ae224", "wiringId": "00000000-0000-0000-0000-0000000ae225", "tensorId": "00000000-0000-0000-0000-0000000ae226"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ae221'::uuid,
    'admin.enum.management.write.update_group',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ae220'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET hub_id         = EXCLUDED.hub_id,
        manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae222',
    'admin.enum.management.write.update_group.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey": "admin.enum.management.write.update_group", "surface": "admin.enum.management.write.update_group", "categoryKeys": ["enum_dictionary"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ae223',
    'admin.enum.management.write.update_group.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae224',
    'admin.enum.management.write.update_group.layout',
    'ui_builder_canvas',
    '{"records":[]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae225',
    'admin.enum.management.write.update_group.wiring',
    'admin_runtime',
    'manifest',
    'manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group',
    '{"actions":[]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        target_surface = EXCLUDED.target_surface,
        target_ref = EXCLUDED.target_ref,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

-- Pre-fill (2026-07-28, PR #600 review round 10-11): load_button dispatches update_group's
-- OWN canonical action (dryRun=true, groupId only -- groupName deliberately omitted so
-- DataEnumDictionaryUpdateGroupAsync's request.GroupName?.Trim() ?? before.GroupName falls
-- back to the real current value) purely to populate emission.data.preview.groupName --
-- no separate read/get action, no new carrier, still exactly one canonical admin_runtime
-- action for this layout. group_name_field's propBindings.value pre-fills its displayed
-- value from that response (frontend/runtime/propBindingResolver.ts
-- COMPONENT_ARRAY_PROP_CAPABILITIES["form_input/search_input"], a generic capability
-- extension, not admin-enum-specific) -- ProjectionShell.tsx's
-- seedTrackerFromPropBindingsValue additionally seeds liveNodeValueTracker with that same
-- value so an unedited pre-filled value still resolves for preview_button/confirm_button's
-- own node:group_name_field.value payloadFrom (without this, an untouched pre-fill would
-- fail PAYLOAD_FROM_NODE_NOT_FOUND on Preview/Confirm). Carrying groupId itself from
-- ae200's row selection remains the unresolved part of
-- admin-write-surface-selection-context-and-mode-composition-gap (.agent/tasks/todo.md) --
-- this pass only removes the NEED to already know the group's current name once its id is
-- known, whether typed manually (today) or eventually auto-carried.
INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ae226',
    'admin#enum_management_write_update_group',
    '00000000-0000-0000-0000-0000000ae222',
    '00000000-0000-0000-0000-0000000ae224',
    '00000000-0000-0000-0000-0000000ae225',
    'default',
    0,
    '{"nodes": [{"nodeId": "group_id_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 0}, {"nodeId": "load_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 1, "propsJson": "{\"label\": \"Load current values\"}", "dispatchPayloadFromByTrigger": {"click": {"groupId": "node:group_id_field.value", "dryRun": "literal:true"}}}, {"nodeId": "group_name_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 2, "propBindings": {"value": {"source": "emission.data.preview.groupName"}}}, {"nodeId": "preview_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 3, "propsJson": "{\"label\": \"Preview\"}", "dispatchPayloadFromByTrigger": {"click": {"groupId": "node:group_id_field.value", "groupName": "node:group_name_field.value", "dryRun": "literal:true"}}}, {"nodeId": "confirm_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 4, "propsJson": "{\"label\": \"Confirm & write\"}", "dispatchPayloadFromByTrigger": {"click": {"groupId": "node:group_id_field.value", "groupName": "node:group_name_field.value", "confirmed": "literal:true"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- single-purpose write layout: enum_dictionary:delete_group
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ae231', '{"description": "admin_enum_management_write_delete_group", "system": true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae230',
    NULL,
    ARRAY[
        '{"type": "hub_grouping", "manifestKey": "admin.enum.management.write.delete_group", "bundle": "admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type": "dispatcher_mapping", "role": "admin", "target": "manifest", "layer": "enum_dictionary", "action": "delete_group"}'::jsonb,
        '{"type": "runtime_mapping", "runtime_destination": "admin_runtime"}'::jsonb,
        '{"type": "ui_projection", "packageIds": ["00000000-0000-0000-0000-0000000ae233"], "layoutId": "00000000-0000-0000-0000-0000000ae234", "wiringId": "00000000-0000-0000-0000-0000000ae235", "tensorId": "00000000-0000-0000-0000-0000000ae236"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ae231'::uuid,
    'admin.enum.management.write.delete_group',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ae230'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET hub_id         = EXCLUDED.hub_id,
        manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae232',
    'admin.enum.management.write.delete_group.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey": "admin.enum.management.write.delete_group", "surface": "admin.enum.management.write.delete_group", "categoryKeys": ["enum_dictionary"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ae233',
    'admin.enum.management.write.delete_group.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae234',
    'admin.enum.management.write.delete_group.layout',
    'ui_builder_canvas',
    '{"records":[]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae235',
    'admin.enum.management.write.delete_group.wiring',
    'admin_runtime',
    'manifest',
    'manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group',
    '{"actions":[]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        target_surface = EXCLUDED.target_surface,
        target_ref = EXCLUDED.target_ref,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ae236',
    'admin#enum_management_write_delete_group',
    '00000000-0000-0000-0000-0000000ae232',
    '00000000-0000-0000-0000-0000000ae234',
    '00000000-0000-0000-0000-0000000ae235',
    'default',
    0,
    '{"nodes": [{"nodeId": "group_id_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 0}, {"nodeId": "preview_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 1, "propsJson": "{\"label\": \"Preview\"}", "dispatchPayloadFromByTrigger": {"click": {"groupId": "node:group_id_field.value", "dryRun": "literal:true"}}}, {"nodeId": "confirm_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 2, "propsJson": "{\"label\": \"Confirm & write\"}", "dispatchPayloadFromByTrigger": {"click": {"groupId": "node:group_id_field.value", "confirmed": "literal:true"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- single-purpose write layout: enum_dictionary:create_item
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ae241', '{"description": "admin_enum_management_write_create_item", "system": true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae240',
    NULL,
    ARRAY[
        '{"type": "hub_grouping", "manifestKey": "admin.enum.management.write.create_item", "bundle": "admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type": "dispatcher_mapping", "role": "admin", "target": "manifest", "layer": "enum_dictionary", "action": "create_item"}'::jsonb,
        '{"type": "runtime_mapping", "runtime_destination": "admin_runtime"}'::jsonb,
        '{"type": "ui_projection", "packageIds": ["00000000-0000-0000-0000-0000000ae243"], "layoutId": "00000000-0000-0000-0000-0000000ae244", "wiringId": "00000000-0000-0000-0000-0000000ae245", "tensorId": "00000000-0000-0000-0000-0000000ae246"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ae241'::uuid,
    'admin.enum.management.write.create_item',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ae240'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET hub_id         = EXCLUDED.hub_id,
        manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae242',
    'admin.enum.management.write.create_item.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey": "admin.enum.management.write.create_item", "surface": "admin.enum.management.write.create_item", "categoryKeys": ["enum_dictionary"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ae243',
    'admin.enum.management.write.create_item.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae244',
    'admin.enum.management.write.create_item.layout',
    'ui_builder_canvas',
    '{"records":[]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae245',
    'admin.enum.management.write.create_item.wiring',
    'admin_runtime',
    'manifest',
    'manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item',
    '{"actions":[]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        target_surface = EXCLUDED.target_surface,
        target_ref = EXCLUDED.target_ref,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ae246',
    'admin#enum_management_write_create_item',
    '00000000-0000-0000-0000-0000000ae242',
    '00000000-0000-0000-0000-0000000ae244',
    '00000000-0000-0000-0000-0000000ae245',
    'default',
    0,
    '{"nodes": [{"nodeId": "item_name_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 0}, {"nodeId": "preview_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 1, "propsJson": "{\"label\": \"Preview\"}", "dispatchPayloadFromByTrigger": {"click": {"name": "node:item_name_field.value", "dryRun": "literal:true"}}}, {"nodeId": "confirm_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 2, "propsJson": "{\"label\": \"Confirm & write\"}", "dispatchPayloadFromByTrigger": {"click": {"name": "node:item_name_field.value", "confirmed": "literal:true"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- single-purpose write layout: enum_dictionary:update_item
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ae251', '{"description": "admin_enum_management_write_update_item", "system": true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae250',
    NULL,
    ARRAY[
        '{"type": "hub_grouping", "manifestKey": "admin.enum.management.write.update_item", "bundle": "admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type": "dispatcher_mapping", "role": "admin", "target": "manifest", "layer": "enum_dictionary", "action": "update_item"}'::jsonb,
        '{"type": "runtime_mapping", "runtime_destination": "admin_runtime"}'::jsonb,
        '{"type": "ui_projection", "packageIds": ["00000000-0000-0000-0000-0000000ae253"], "layoutId": "00000000-0000-0000-0000-0000000ae254", "wiringId": "00000000-0000-0000-0000-0000000ae255", "tensorId": "00000000-0000-0000-0000-0000000ae256"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ae251'::uuid,
    'admin.enum.management.write.update_item',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ae250'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET hub_id         = EXCLUDED.hub_id,
        manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae252',
    'admin.enum.management.write.update_item.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey": "admin.enum.management.write.update_item", "surface": "admin.enum.management.write.update_item", "categoryKeys": ["enum_dictionary"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ae253',
    'admin.enum.management.write.update_item.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae254',
    'admin.enum.management.write.update_item.layout',
    'ui_builder_canvas',
    '{"records":[]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae255',
    'admin.enum.management.write.update_item.wiring',
    'admin_runtime',
    'manifest',
    'manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item',
    '{"actions":[]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        target_surface = EXCLUDED.target_surface,
        target_ref = EXCLUDED.target_ref,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ae256',
    'admin#enum_management_write_update_item',
    '00000000-0000-0000-0000-0000000ae252',
    '00000000-0000-0000-0000-0000000ae254',
    '00000000-0000-0000-0000-0000000ae255',
    'default',
    0,
    '{"nodes": [{"nodeId": "item_index_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 0}, {"nodeId": "item_name_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 1}, {"nodeId": "preview_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 2, "propsJson": "{\"label\": \"Preview\"}", "dispatchPayloadFromByTrigger": {"click": {"indexNum": "node:item_index_field.value", "name": "node:item_name_field.value", "dryRun": "literal:true"}}}, {"nodeId": "confirm_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 3, "propsJson": "{\"label\": \"Confirm & write\"}", "dispatchPayloadFromByTrigger": {"click": {"indexNum": "node:item_index_field.value", "name": "node:item_name_field.value", "confirmed": "literal:true"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- single-purpose write layout: enum_dictionary:delete_item
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ae261', '{"description": "admin_enum_management_write_delete_item", "system": true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae260',
    NULL,
    ARRAY[
        '{"type": "hub_grouping", "manifestKey": "admin.enum.management.write.delete_item", "bundle": "admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type": "dispatcher_mapping", "role": "admin", "target": "manifest", "layer": "enum_dictionary", "action": "delete_item"}'::jsonb,
        '{"type": "runtime_mapping", "runtime_destination": "admin_runtime"}'::jsonb,
        '{"type": "ui_projection", "packageIds": ["00000000-0000-0000-0000-0000000ae263"], "layoutId": "00000000-0000-0000-0000-0000000ae264", "wiringId": "00000000-0000-0000-0000-0000000ae265", "tensorId": "00000000-0000-0000-0000-0000000ae266"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ae261'::uuid,
    'admin.enum.management.write.delete_item',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ae260'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET hub_id         = EXCLUDED.hub_id,
        manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae262',
    'admin.enum.management.write.delete_item.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey": "admin.enum.management.write.delete_item", "surface": "admin.enum.management.write.delete_item", "categoryKeys": ["enum_dictionary"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ae263',
    'admin.enum.management.write.delete_item.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae264',
    'admin.enum.management.write.delete_item.layout',
    'ui_builder_canvas',
    '{"records":[]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae265',
    'admin.enum.management.write.delete_item.wiring',
    'admin_runtime',
    'manifest',
    'manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item',
    '{"actions":[]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        target_surface = EXCLUDED.target_surface,
        target_ref = EXCLUDED.target_ref,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ae266',
    'admin#enum_management_write_delete_item',
    '00000000-0000-0000-0000-0000000ae262',
    '00000000-0000-0000-0000-0000000ae264',
    '00000000-0000-0000-0000-0000000ae265',
    'default',
    0,
    '{"nodes": [{"nodeId": "item_index_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 0}, {"nodeId": "preview_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 1, "propsJson": "{\"label\": \"Preview\"}", "dispatchPayloadFromByTrigger": {"click": {"indexNum": "node:item_index_field.value", "dryRun": "literal:true"}}}, {"nodeId": "confirm_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 2, "propsJson": "{\"label\": \"Confirm & write\"}", "dispatchPayloadFromByTrigger": {"click": {"indexNum": "node:item_index_field.value", "confirmed": "literal:true"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- single-purpose write layout: enum_dictionary:set_group_items
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ae271', '{"description": "admin_enum_management_write_set_group_items", "system": true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae270',
    NULL,
    ARRAY[
        '{"type": "hub_grouping", "manifestKey": "admin.enum.management.write.set_group_items", "bundle": "admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type": "dispatcher_mapping", "role": "admin", "target": "manifest", "layer": "enum_dictionary", "action": "set_group_items"}'::jsonb,
        '{"type": "runtime_mapping", "runtime_destination": "admin_runtime"}'::jsonb,
        '{"type": "ui_projection", "packageIds": ["00000000-0000-0000-0000-0000000ae273"], "layoutId": "00000000-0000-0000-0000-0000000ae274", "wiringId": "00000000-0000-0000-0000-0000000ae275", "tensorId": "00000000-0000-0000-0000-0000000ae276"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ae271'::uuid,
    'admin.enum.management.write.set_group_items',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ae270'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET hub_id         = EXCLUDED.hub_id,
        manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae272',
    'admin.enum.management.write.set_group_items.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey": "admin.enum.management.write.set_group_items", "surface": "admin.enum.management.write.set_group_items", "categoryKeys": ["enum_dictionary"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ae273',
    'admin.enum.management.write.set_group_items.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae274',
    'admin.enum.management.write.set_group_items.layout',
    'ui_builder_canvas',
    '{"records":[]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae275',
    'admin.enum.management.write.set_group_items.wiring',
    'admin_runtime',
    'manifest',
    'manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items',
    '{"actions":[]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        target_surface = EXCLUDED.target_surface,
        target_ref = EXCLUDED.target_ref,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ae276',
    'admin#enum_management_write_set_group_items',
    '00000000-0000-0000-0000-0000000ae272',
    '00000000-0000-0000-0000-0000000ae274',
    '00000000-0000-0000-0000-0000000ae275',
    'default',
    0,
    '{"nodes": [{"nodeId": "group_id_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 0}, {"nodeId": "items_csv_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 1}, {"nodeId": "preview_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 2, "propsJson": "{\"label\": \"Preview\"}", "dispatchPayloadFromByTrigger": {"click": {"groupId": "node:group_id_field.value", "enumIndexNums": "node:items_csv_field.value", "dryRun": "literal:true"}}}, {"nodeId": "confirm_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 3, "propsJson": "{\"label\": \"Confirm & write\"}", "dispatchPayloadFromByTrigger": {"click": {"groupId": "node:group_id_field.value", "enumIndexNums": "node:items_csv_field.value", "confirmed": "literal:true"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;

-- =============================================================================
-- admin-surface-topology-seed-conversion: admin-enum subBundle -- single-purpose
-- READ-DETAIL layout for enum_dictionary:get_group (2026-07-27, PR #600 review
-- round 9 correction). enum_dictionary:get_group is an existing, already-
-- dispatcher-registered, already-tested admin_runtime action (AdminRuntime.cs
-- DataEnumDictionaryGetGroupAsync, live-DB-proven by
-- AdminEnumHubRelationUiProjectionLiveDbTests.cs) that had never been wired into
-- any manifest/layout -- an "existing contract, unconnected in production" gap,
-- not a missing mechanism. Same single-purpose-manifest pattern as ae210-ae270
-- (layout = 1 canonical admin_runtime action); no per-node target override, no
-- new component kind/actionType/lane, no change to enum_dictionary:list_groups's
-- response shape (so AdminEnumsRoster.tsx / adminApi.ts, which still consume that
-- shape directly, are unaffected). groupId is entered directly (same pattern
-- ae210-ae270's own identity fields already use) -- carrying the row's identity
-- here automatically from ae200's enum_table selection remains the acknowledged,
-- unresolved gap in admin-write-surface-selection-context-and-mode-composition-gap
-- (.agent/tasks/todo.md); this manifest does not solve that, it makes get_group
-- itself production-reachable so that gap has something real to eventually land
-- on to. group_detail_json renders the full EnumDictionaryGroupDetailDto
-- (groupName/indexNum/items) via the same json_viewer.template + propBindings
-- pattern every other manifest in this file already uses for its own debug/detail
-- panel. Honest limitation carried over unchanged from DataEnumDictionaryGetGroupAsync
-- (pre-existing, not touched by this pass): a group with zero items returns
-- ENUM_GROUP_ITEMS_EMPTY rather than an empty detail -- this manifest surfaces
-- that behavior as-is, does not work around it.
-- =============================================================================

INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000ae281', '{"description": "admin_enum_management_read_get_group", "system": true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae280',
    NULL,
    ARRAY[
        '{"type": "hub_grouping", "manifestKey": "admin.enum.management.read.get_group", "bundle": "admin-surface-topology-seed-conversion"}'::jsonb,
        '{"type": "dispatcher_mapping", "role": "admin", "target": "manifest", "layer": "enum_dictionary", "action": "get_group"}'::jsonb,
        '{"type": "runtime_mapping", "runtime_destination": "admin_runtime"}'::jsonb,
        '{"type": "ui_projection", "packageIds": ["00000000-0000-0000-0000-0000000ae283"], "layoutId": "00000000-0000-0000-0000-0000000ae284", "wiringId": "00000000-0000-0000-0000-0000000ae285", "tensorId": "00000000-0000-0000-0000-0000000ae286"}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT
    m.manifest_id,
    '00000000-0000-0000-0000-0000000ae281'::uuid,
    'admin.enum.management.read.get_group',
    m.status,
    to_jsonb(m.topology)
FROM manifest m
WHERE m.manifest_id = '00000000-0000-0000-0000-0000000ae280'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET hub_id         = EXCLUDED.hub_id,
        manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at     = now();

INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae282',
    'admin.enum.management.read.get_group.component_group_bundle',
    'fixed_form_projection',
    '{"seedKey": "admin.enum.management.read.get_group", "surface": "admin.enum.management.read.get_group", "categoryKeys": ["enum_dictionary"]}'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET package_schema_json = EXCLUDED.package_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES (
    '00000000-0000-0000-0000-0000000ae283',
    'admin.enum.management.read.get_group.package',
    '[]'::jsonb,
    'active'
)
ON CONFLICT (package_id) DO UPDATE
    SET layout = EXCLUDED.layout,
        state = EXCLUDED.state;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae284',
    'admin.enum.management.read.get_group.layout',
    'ui_builder_canvas',
    '{"records":[]}'::jsonb,
    'active'
)
ON CONFLICT (layout_id) DO UPDATE
    SET layout_schema_json = EXCLUDED.layout_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES (
    '00000000-0000-0000-0000-0000000ae285',
    'admin.enum.management.read.get_group.wiring',
    'admin_runtime',
    'manifest',
    'manifest:00000000-0000-0000-0000-0000000ae280:enum_dictionary:get_group',
    '{"actions":[]}'::jsonb,
    'active'
)
ON CONFLICT (wiring_id) DO UPDATE
    SET wiring_kind = EXCLUDED.wiring_kind,
        target_surface = EXCLUDED.target_surface,
        target_ref = EXCLUDED.target_ref,
        wiring_schema_json = EXCLUDED.wiring_schema_json,
        status = EXCLUDED.status;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000ae286',
    'admin#enum_management_read_get_group',
    '00000000-0000-0000-0000-0000000ae282',
    '00000000-0000-0000-0000-0000000ae284',
    '00000000-0000-0000-0000-0000000ae285',
    'default',
    0,
    '{"nodes": [{"nodeId": "group_id_field", "nodeKind": "catalog_component", "componentKey": "search_input.alias", "parentNodeId": null, "slotKey": "default", "orderIndex": 0}, {"nodeId": "load_button", "nodeKind": "catalog_component", "componentKey": "button.primitive", "parentNodeId": null, "slotKey": "default", "orderIndex": 1, "propsJson": "{\"label\": \"Load group detail\"}", "dispatchPayloadFromByTrigger": {"click": {"groupId": "node:group_id_field.value"}}}, {"nodeId": "group_detail_json", "nodeKind": "catalog_component", "componentKey": "json_viewer.template", "parentNodeId": null, "slotKey": "default", "orderIndex": 2, "propBindings": {"data": {"source": "emission.data"}}}]}'::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE
    SET layout_patch_json = EXCLUDED.layout_patch_json;


-- Representative existing cron absorption: log retention.
-- The former RetentionScheduler BackgroundService is absorbed into the scheduler
-- job manifest substrate. The retention domain body stays in LogRetentionRuntime
-- behind the log_retention abstract function primitive; the scheduler substrate
-- only ticks, dispatches, and records run status — it knows no retention policy.
-- =============================================================================

-- Abstract function manifest: system.log_retention (runtime_lane = scheduler_job_runtime)
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af12', 'system.log_retention',
     'scheduler_job_runtime', 'system_log_retention',
     '{"retention_status":"retention_result"}',
     ARRAY['credential','credential_payload','decrypted_payload','plaintext_payload',
           'decrypted_credential_payload','token_response','token_body',
           'api_key','access_token','refresh_token','client_secret'],
     true)
ON CONFLICT (abstract_function_id) DO NOTHING;

-- Step: log_retention primitive (no input bindings — policy loaded from function_parameters).
INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf41', '00000000-0000-0000-0000-00000000af12', 1,
     'log_retention', '{}', 'retention_result', true)
ON CONFLICT (abstract_function_step_id) DO NOTHING;

-- Authority binding (policy authority required by AbstractFunctionExecutor).
INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    ('00000000-0000-0000-0000-00000000af12', 'policy', 'system_log_retention_policy', true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO NOTHING;

-- Scheduler job: log_retention_sweep (interval_seconds; no input table — maintenance sweep).
-- LogRetentionRuntime enforces the actual retention policy from function_parameters; this
-- interval is only the dispatch cadence. MissingPolicy/MalformedPolicy fail-close explicitly.
INSERT INTO topology.scheduler_jobs
    (scheduler_job_id, job_key, trigger_kind, schedule_policy_kind, schedule_interval_seconds,
     manual_run_allowed, active, authority_scope, max_batch_size, lease_seconds,
     retry_policy, projection_policy, created_by)
VALUES
    ('00000000-0000-0000-0000-00000000c070', 'log_retention_sweep', 'cron', 'interval_seconds', 3600,
     false, true, 'system_log_retention', 1, 300,
     '{"max_attempts":1,"backoff_seconds":0}',
     '{"allowed_result_keys":["retention_result"]}',
     'seed')
ON CONFLICT (scheduler_job_id) DO NOTHING;

-- Scheduler job step: step 1 → system.log_retention
INSERT INTO topology.scheduler_job_steps
    (scheduler_job_step_id, scheduler_job_id, step_order, abstract_function_key,
     input_binding, result_context_key, result_binding, on_error, active)
VALUES
    ('00000000-0000-0000-0000-00000000c071',
     '00000000-0000-0000-0000-00000000c070',
     1, 'system.log_retention',
     '{}', 'retention_result', '{}', 'fail_run', true)
ON CONFLICT (scheduler_job_id, step_order) DO NOTHING;

-- ---------------------------------------------------------------------------
-- external_port_substrate consumer bundle completion physical catalog and
-- manifest bindings. These are projection/evidence surfaces only; no provider
-- credential, endpoint, token, or signed URL values are seeded.
-- ---------------------------------------------------------------------------
INSERT INTO topology.physical_tables (table_ref, schema_name, category, active)
VALUES
    ('topology.email_drafts', 'topology', 'email_bundle', true),
    ('topology.email_approval_records', 'topology', 'email_bundle', true),
    ('topology.email_delivery_evidence', 'topology', 'email_bundle', true),
    ('topology.webhook_intake_snapshots', 'topology', 'stripe_webhook_inbox_bundle', true),
    ('topology.signature_verification_evidence', 'topology', 'stripe_webhook_inbox_bundle', true),
    ('topology.payment_state_projections', 'topology', 'stripe_bundle', true),
    ('topology.scheduler_external_event_evidence', 'topology', 'job_scheduler_bundle', true),
    ('topology.audit_approval_requests', 'topology', 'audit_approval_bundle', true),
    ('topology.audit_approval_evidence', 'topology', 'audit_approval_bundle', true),
    ('topology.audit_notification_evidence', 'topology', 'audit_approval_bundle', true),
    ('topology.sftp_transfer_log', 'topology', 'export_sftp_bundle', true)
ON CONFLICT (table_ref) DO UPDATE
    SET schema_name = EXCLUDED.schema_name,
        category    = EXCLUDED.category,
        active      = EXCLUDED.active;

INSERT INTO hubs.hub (hub_id, relation)
VALUES
    ('00000000-0000-0000-0000-0000000000a3', '{"description":"email_bundle","system":true}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a4', '{"description":"stripe_bundle","system":true}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a5', '{"description":"webhook_inbox_bundle","system":true}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a6', '{"description":"job_scheduler_bundle_external_intake","system":true}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a7', '{"description":"audit_approval_bundle","system":true}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a8', '{"description":"export_sftp_bundle","system":true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
VALUES
    ('00000000-0000-0000-0000-0000000000a3', '00000000-0000-0000-0000-0000000000a3', 'email.response_port.approval.delivery.projection', 'active', '{"source":"external-port-consumer-completion","bundle":"email_bundle","portTargetRefActionWiring":"dispatchExternalPort","preset":"physical_search_crud_aggregate.v1","responseProjection":"delivery_status_and_approval_evidence"}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a4', '00000000-0000-0000-0000-0000000000a4', 'stripe.hook_port.intake.payment.projection', 'active', '{"source":"external-port-consumer-completion","bundle":"stripe_bundle","hook_port_receive":"hook_path_route_key_to_scheduler_enqueue_event","responseProjection":"payment_state_projected"}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a5', '00000000-0000-0000-0000-0000000000a5', 'webhook_inbox.hook_port.intake.projection', 'active', '{"source":"external-port-consumer-completion","bundle":"webhook_inbox_bundle","hook_port_receive":"hook_path_route_key_to_scheduler_enqueue_event","responseProjection":"intake_snapshot_and_signature_evidence"}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a6', '00000000-0000-0000-0000-0000000000a6', 'job_scheduler.external_hook.evidence.projection', 'active', '{"source":"external-port-consumer-completion","bundle":"job_scheduler_bundle","builtInSchedulerPortDependency":"forbidden","externalHookOnly":"external_port_substrate"}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a7', '00000000-0000-0000-0000-0000000000a7', 'audit_approval.response_port.evidence.projection', 'active', '{"source":"external-port-consumer-completion","bundle":"audit_approval_bundle","portTargetRefActionWiring":"dispatchExternalPort","preset":"physical_search_crud_aggregate.v1","responseProjection":"approval_status_and_audit_evidence"}'::jsonb),
    ('00000000-0000-0000-0000-0000000000a8', '00000000-0000-0000-0000-0000000000a8', 'export_sftp.response_port.transfer.projection', 'active', '{"source":"external-port-consumer-completion","bundle":"export_sftp_bundle","portTargetRefActionWiring":"dispatchExternalPort","preset":"physical_search_crud_aggregate.v1","checksumBoundary":"pre_and_post_transfer","responseProjection":"transfer_status"}'::jsonb)
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET manifest_key = EXCLUDED.manifest_key,
        status       = EXCLUDED.status,
        topology_jsonb = EXCLUDED.topology_jsonb,
        updated_at   = now();

INSERT INTO topology.physical_table_manifest_bindings
    (physical_table_id, topology_manifest_id, active, binding_evidence_json)
SELECT pt.physical_table_id,
       CASE pt.category
           WHEN 'email_bundle' THEN '00000000-0000-0000-0000-0000000000a3'::uuid
           WHEN 'stripe_bundle' THEN '00000000-0000-0000-0000-0000000000a4'::uuid
           WHEN 'stripe_webhook_inbox_bundle' THEN
               CASE WHEN pt.table_ref = 'topology.payment_state_projections'
                    THEN '00000000-0000-0000-0000-0000000000a4'::uuid
                    ELSE '00000000-0000-0000-0000-0000000000a5'::uuid END
           WHEN 'job_scheduler_bundle' THEN '00000000-0000-0000-0000-0000000000a6'::uuid
           WHEN 'audit_approval_bundle' THEN '00000000-0000-0000-0000-0000000000a7'::uuid
           WHEN 'export_sftp_bundle' THEN '00000000-0000-0000-0000-0000000000a8'::uuid
       END,
       true,
       jsonb_build_object(
           'source', 'external-port-consumer-completion',
           'bundle', pt.category,
           'uiBuilderPreset', CASE WHEN pt.category IN ('email_bundle','audit_approval_bundle','export_sftp_bundle') THEN 'physical_search_crud_aggregate.v1' ELSE NULL END,
           'portTargetRefLane', true,
           'credentialProjection', 'reference_only')
FROM topology.physical_tables pt
WHERE pt.table_ref IN (
    'topology.email_drafts',
    'topology.email_approval_records',
    'topology.email_delivery_evidence',
    'topology.webhook_intake_snapshots',
    'topology.signature_verification_evidence',
    'topology.payment_state_projections',
    'topology.scheduler_external_event_evidence',
    'topology.audit_approval_requests',
    'topology.audit_approval_evidence',
    'topology.audit_notification_evidence',
    'topology.sftp_transfer_log')
ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE
    SET active = true,
        binding_evidence_json = EXCLUDED.binding_evidence_json,
        updated_at = now();

-- CLI/MCP reader port seed-defined admin/runtime surface.
INSERT INTO topology.cli_reader_ports (
    port_key, port_id, enabled, expires_at, allowed_roles, allowed_users, allowed_tables,
    allowed_columns, allowed_filters, allowed_periods, row_scope, required_capabilities,
    rate_limit_per_minute, audit_required, file_stream_enabled, config_json
) VALUES (
    'cli_reader_port.default', '00000000-0000-0000-0000-00000000c100', true, NULL,
    '["admin","reader"]'::jsonb,
    '[]'::jsonb,
    '["topology.entity"]'::jsonb,
    '{"topology.entity":["entity_id","entity_jsonb","state_id"]}'::jsonb,
    '["state_id","entity_id"]'::jsonb,
    '["today","last_7_days","last_30_days"]'::jsonb,
    '{"admin-user":"state_id=active","reader-user":"state_id=active"}'::jsonb,
    '["cli_reader_port.read"]'::jsonb,
    60,
    true,
    true,
    '{"admin_projection":"contents","surface":"cli_reader_port","secret_projection":"denied","dispatch_runtime_destination":"cli_reader_port_runtime","allowed_business_objects":["account"],"allowed_assignment_target_scopes":["topology.entity"]}'::jsonb
) ON CONFLICT (port_key) DO UPDATE SET
    port_id = EXCLUDED.port_id,
    enabled = EXCLUDED.enabled,
    allowed_roles = EXCLUDED.allowed_roles,
    allowed_tables = EXCLUDED.allowed_tables,
    allowed_columns = EXCLUDED.allowed_columns,
    allowed_filters = EXCLUDED.allowed_filters,
    allowed_periods = EXCLUDED.allowed_periods,
    row_scope = EXCLUDED.row_scope,
    required_capabilities = EXCLUDED.required_capabilities,
    file_stream_enabled = EXCLUDED.file_stream_enabled,
    config_json = EXCLUDED.config_json,
    updated_at = now();

-- =============================================================================
-- team-dashboard subBundle (admin-surface-topology-seed-conversion)
-- SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
--   surface_axes.admin.surfaces.team_dashboard /
--   surface_axes.normal.surfaces.dashboard.team_dashboard_canonical_shared_contract
--
-- ONE shared physical table (topology.team_dashboard_note, ONE canonical row) is read by
-- BOTH the admin (edit) and normal (read-only) manifests below via the same team_dashboard:get
-- action -- no duplicated data identity between axes. Only the admin manifest's own tensor
-- carries the editable Textarea + Save button nodes (team_dashboard:update, admin-role-gated
-- in-method by AdminRuntime.TeamDashboard.cs) -- the normal manifest's tensor never includes
-- them at all, so there is no edit control in the Normal-rendered DOM to reach, not merely a
-- hidden one. Component composition (textarea.template + md_viewer.projection bare-markdown
-- mode) reuses physical_details_inline_editor_md_generator_preset's own proven node shape
-- (PR #604) -- not a new Markdown component/editor/runtime lane.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- topology.team_dashboard_note: physical table registration + the single canonical row.
-- ---------------------------------------------------------------------------
INSERT INTO topology.physical_tables (table_ref, schema_name, category, active)
VALUES ('topology.team_dashboard_note', 'topology', 'team_dashboard', true)
ON CONFLICT (table_ref) DO UPDATE
    SET schema_name = EXCLUDED.schema_name,
        category    = EXCLUDED.category,
        active      = EXCLUDED.active;

INSERT INTO topology.team_dashboard_note (note_id, title, body_markdown)
VALUES (
    '00000000-0000-0000-0000-0000000dd001',
    'Team Dashboard',
    '# Team Dashboard' || chr(10) || chr(10) || 'Shared notes go here.'
)
ON CONFLICT (note_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Admin edit manifest: team_dashboard.admin.projection
-- ---------------------------------------------------------------------------
WITH upserted_manifest AS (
    INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
    VALUES (
        '00000000-0000-0000-0000-0000000dd010',
        NULL,
        ARRAY[
            '{"type":"hub_grouping","manifestKey":"team_dashboard.admin.projection","bundle":"admin-surface-topology-seed-conversion"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_dashboard","action":"get","default_screen_read":true}'::jsonb,
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"team_dashboard","action":"update"}'::jsonb,
            '{"type":"ui_projection","packageIds":["00000000-0000-0000-0000-0000000dd016"],"layoutId":"00000000-0000-0000-0000-0000000dd013","wiringId":"00000000-0000-0000-0000-0000000dd014","tensorId":"00000000-0000-0000-0000-0000000dd015"}'::jsonb
        ]::jsonb[],
        'active'
    )
    ON CONFLICT (manifest_id) DO UPDATE SET topology = EXCLUDED.topology, status = EXCLUDED.status
    RETURNING manifest_id, status
)
INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
SELECT '00000000-0000-0000-0000-0000000dd012', 'team_dashboard.admin.projection.component_group_bundle',
       'fixed_form_projection', '{"seedKey":"team_dashboard.admin.projection"}'::jsonb, 'active'
FROM upserted_manifest
ON CONFLICT (package_id) DO UPDATE SET package_schema_json = EXCLUDED.package_schema_json, status = EXCLUDED.status;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES ('00000000-0000-0000-0000-0000000dd013', 'team_dashboard.admin.projection.layout',
        'fixed_form_projection', '{"records":[]}'::jsonb, 'active')
ON CONFLICT (layout_id) DO UPDATE SET layout_schema_json = EXCLUDED.layout_schema_json, status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES ('00000000-0000-0000-0000-0000000dd014', 'team_dashboard.admin.projection.wiring',
        'admin_runtime', 'manifest', 'team_dashboard.admin.projection',
        '{"actions":["team_dashboard:get","team_dashboard:update"]}'::jsonb, 'active')
ON CONFLICT (wiring_id) DO UPDATE SET wiring_schema_json = EXCLUDED.wiring_schema_json, status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES ('00000000-0000-0000-0000-0000000dd016', 'team_dashboard.admin.projection.package', '[]'::jsonb, 'active')
ON CONFLICT (package_id) DO UPDATE SET name = EXCLUDED.name, state = EXCLUDED.state;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000dd015',
    'admin/team-dashboard#default',
    '00000000-0000-0000-0000-0000000dd012',
    '00000000-0000-0000-0000-0000000dd013',
    '00000000-0000-0000-0000-0000000dd014',
    'default', 0,
    $$
    {"nodes":[
      {"nodeId":"team_dashboard_admin_viewer","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"md_viewer.projection","propsJson":"{\"label\": \"Rendered preview\"}","propBindings":{"markdown":{"source":"emission.data.bodyMarkdown"}}},
      {"nodeId":"team_dashboard_admin_body","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"textarea.template","propsJson":"{\"label\": \"Markdown body\"}","propBindings":{"value":{"source":"emission.data.bodyMarkdown"}}},
      {"nodeId":"team_dashboard_admin_save_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update"},"dispatchPayloadFromByTrigger":{"click":{"bodyMarkdown":"node:team_dashboard_admin_body.value","dryRun":"literal:true"}},"propsJson":"{\"label\": \"Save\"}"},
      {"nodeId":"team_dashboard_admin_save_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_confirm_modal"}],"componentKey":"modal.template","componentKind":"disclosure/modal","propsJson":"{\"data\": {\"open\": false, \"title\": \"Save team dashboard\", \"body\": \"Save the edited Markdown as the team dashboard's shared content.\"}}"},
      {"nodeId":"team_dashboard_admin_save_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_confirm_button"}],"componentKey":"button.primitive","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update"},"dispatchPayloadFromByTrigger":{"click":{"bodyMarkdown":"node:team_dashboard_admin_body.value","confirmed":"literal:true"}},"propsJson":"{\"label\": \"Save\"}"},
      {"nodeId":"team_dashboard_admin_save_cancel_button","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_cancel_button"}],"componentKey":"button.primitive","propsJson":"{\"label\": \"Cancel\"}"}
    ]}
    $$::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE SET layout_patch_json = EXCLUDED.layout_patch_json;

-- Hub + hubs.topology_manifests mirror + physical table binding for the admin manifest.
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000dd011', '{"description":"team_dashboard_admin","system":true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT m.manifest_id, '00000000-0000-0000-0000-0000000dd011'::uuid,
       'team_dashboard.admin.projection', m.status, to_jsonb(m.topology)
FROM manifest m WHERE m.manifest_id = '00000000-0000-0000-0000-0000000dd010'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET manifest_key = EXCLUDED.manifest_key, status = EXCLUDED.status, topology_jsonb = EXCLUDED.topology_jsonb, updated_at = now();

INSERT INTO topology.physical_table_manifest_bindings (physical_table_id, topology_manifest_id, active, binding_evidence_json)
SELECT pt.physical_table_id, '00000000-0000-0000-0000-0000000dd010'::uuid, true,
       '{"note":"team_dashboard.admin.projection reads/writes topology.team_dashboard_note via team_dashboard:get/update"}'::jsonb
FROM topology.physical_tables pt WHERE pt.table_ref = 'topology.team_dashboard_note'
ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE
    SET active = EXCLUDED.active, binding_evidence_json = EXCLUDED.binding_evidence_json, updated_at = now();

-- ---------------------------------------------------------------------------
-- Normal read-only manifest: team_dashboard.normal.projection
-- Same underlying data (topology.team_dashboard_note via team_dashboard:get) — no editor nodes.
-- ---------------------------------------------------------------------------
WITH upserted_manifest AS (
    INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
    VALUES (
        '00000000-0000-0000-0000-0000000dd020',
        NULL,
        ARRAY[
            '{"type":"hub_grouping","manifestKey":"team_dashboard.normal.projection","bundle":"admin-surface-topology-seed-conversion"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
            '{"type":"dispatcher_mapping","role":"normal","target":"admin","layer":"team_dashboard","action":"get","default_screen_read":true}'::jsonb,
            '{"type":"capability_requirement","layer":"screen_list","action":"Search"}'::jsonb,
            '{"type":"capability_requirement","layer":"team_dashboard","action":"get"}'::jsonb,
            '{"type":"ui_projection","packageIds":["00000000-0000-0000-0000-0000000dd026"],"layoutId":"00000000-0000-0000-0000-0000000dd023","wiringId":"00000000-0000-0000-0000-0000000dd024","tensorId":"00000000-0000-0000-0000-0000000dd025"}'::jsonb
        ]::jsonb[],
        'active'
    )
    ON CONFLICT (manifest_id) DO UPDATE SET topology = EXCLUDED.topology, status = EXCLUDED.status
    RETURNING manifest_id, status
)
INSERT INTO topology.ui_component_package (package_id, package_key, package_kind, package_schema_json, status)
SELECT '00000000-0000-0000-0000-0000000dd022', 'team_dashboard.normal.projection.component_group_bundle',
       'fixed_form_projection', '{"seedKey":"team_dashboard.normal.projection"}'::jsonb, 'active'
FROM upserted_manifest
ON CONFLICT (package_id) DO UPDATE SET package_schema_json = EXCLUDED.package_schema_json, status = EXCLUDED.status;

INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind, layout_schema_json, status)
VALUES ('00000000-0000-0000-0000-0000000dd023', 'team_dashboard.normal.projection.layout',
        'fixed_form_projection', '{"records":[]}'::jsonb, 'active')
ON CONFLICT (layout_id) DO UPDATE SET layout_schema_json = EXCLUDED.layout_schema_json, status = EXCLUDED.status;

INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status)
VALUES ('00000000-0000-0000-0000-0000000dd024', 'team_dashboard.normal.projection.wiring',
        'admin_runtime', 'manifest', 'team_dashboard.normal.projection',
        '{"actions":["team_dashboard:get"]}'::jsonb, 'active')
ON CONFLICT (wiring_id) DO UPDATE SET wiring_schema_json = EXCLUDED.wiring_schema_json, status = EXCLUDED.status;

INSERT INTO topology.components_package_design (package_id, name, layout, state)
VALUES ('00000000-0000-0000-0000-0000000dd026', 'team_dashboard.normal.projection.package', '[]'::jsonb, 'active')
ON CONFLICT (package_id) DO UPDATE SET name = EXCLUDED.name, state = EXCLUDED.state;

INSERT INTO topology.ui_topology_tensor (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index, layout_patch_json)
VALUES (
    '00000000-0000-0000-0000-0000000dd025',
    'dashboard#default',
    '00000000-0000-0000-0000-0000000dd022',
    '00000000-0000-0000-0000-0000000dd023',
    '00000000-0000-0000-0000-0000000dd024',
    'default', 0,
    $$
    {"nodes":[
      {"nodeId":"team_dashboard_normal_viewer","nodeKind":"catalog_component","runtimeInteractions":[],"componentKey":"md_viewer.projection","propsJson":"{\"label\": \"Rendered preview\"}","propBindings":{"markdown":{"source":"emission.data.bodyMarkdown"}}}
    ]}
    $$::jsonb
)
ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO UPDATE SET layout_patch_json = EXCLUDED.layout_patch_json;

-- Hub + hubs.topology_manifests mirror + physical table binding for the normal manifest.
INSERT INTO hubs.hub (hub_id, relation)
VALUES ('00000000-0000-0000-0000-0000000dd021', '{"description":"team_dashboard_normal","system":true}'::jsonb)
ON CONFLICT (hub_id) DO NOTHING;

INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
SELECT m.manifest_id, '00000000-0000-0000-0000-0000000dd021'::uuid,
       'team_dashboard.normal.projection', m.status, to_jsonb(m.topology)
FROM manifest m WHERE m.manifest_id = '00000000-0000-0000-0000-0000000dd020'
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET manifest_key = EXCLUDED.manifest_key, status = EXCLUDED.status, topology_jsonb = EXCLUDED.topology_jsonb, updated_at = now();

INSERT INTO topology.physical_table_manifest_bindings (physical_table_id, topology_manifest_id, active, binding_evidence_json)
SELECT pt.physical_table_id, '00000000-0000-0000-0000-0000000dd020'::uuid, true,
       '{"note":"team_dashboard.normal.projection reads topology.team_dashboard_note via team_dashboard:get (read-only, no write path)"}'::jsonb
FROM topology.physical_tables pt WHERE pt.table_ref = 'topology.team_dashboard_note'
ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE
    SET active = EXCLUDED.active, binding_evidence_json = EXCLUDED.binding_evidence_json, updated_at = now();
