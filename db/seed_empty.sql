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
      '{"type":"runtime_mapping","runtime_destination":"topology_transform_runtime"}'::jsonb
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
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005f',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"get"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000060',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"validate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000061',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"create_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000062',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"update_draft"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000063',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"promote"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000064',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"deprecate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000007d',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"assign_hub_grouping"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000007e',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"assign_screen_data_shape"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000007f',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"list_relationship_remote_targets"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
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
-- hub_navigation dispatch manifests (IDs 77-7b)
-- Registered for hub_navigation layer: list_manifests / get_hub_relations /
-- create / update / deprecate.
-- Required by AdminRuntime hub_navigation:* switch cases.
-- Silent MANIFEST_NOT_FOUND failure occurs at runtime without these records.
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-000000000077',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"hub_navigation","action":"list_manifests"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000078',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"hub_navigation","action":"get_hub_relations"}'::jsonb,
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
        '00000000-0000-0000-0000-000000000092',
        'admin_ui_component_bucket_create',
        'admin:ui_component_bucket:create',
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
    ('tabs.template','frontend/components/Tabs.tsx','disclosure/tabs','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"navigation","visualRole":"tabs","capabilityTags":["selectable","accepts_design"]}}'::jsonb),
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
        '{"type":"screen_data_shape","logicalTables":[{"tableName":"auth.user","columns":[{"name":"id","dataType":"uuid","nullable":false},{"name":"username","dataType":"text","nullable":true}]}]}'::jsonb
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
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    '00000000-0000-0000-0000-000000000092',
    NULL,
    ARRAY[
        '{"type":"hub_grouping","manifestKey":"auth.external.credential_management.projection","bundle":"auth-external-credential-management-topology-projection","parentBundle":"external-port-substrate-implementation"}'::jsonb,
        '{"type":"dispatcher_mapping","role":"admin","target":"admin","layer":"manifest","action":"assign_screen_data_shape"}'::jsonb,
        '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb,
        '{"type":"fixed_form_projection","surface":"auth_external_credential_management","draft_edit_only":true,"validate_preview_apply_required":true,"ui_builder_authority":false,"physical_row_editor":false,"dedicated_credential_route":false,"consumer_bundle_connection":false,"secret_fields_forbidden":["plaintext_secret","secret","token","access_token","refresh_token","encrypted_payload"],"policy_step_editing":"template_selection_only"}'::jsonb,
        '{"type":"physical_binding","mode":"seed_projection_marker_only","canonical_execution":"runtime_resolves_tableRef_to_physical_table_manifest_bindings","tables":["topology.external_access_ports","topology.external_response_ports","topology.external_hook_ports","topology.external_port_policies"],"forbidden":"generic_physical_table_row_editor"}'::jsonb,
        '{"type":"canonical_port_bindings","manifestKey":"auth.external.credential_management.projection","portKindTableRefs":{"access_port":"topology.external_access_ports","response_port":"topology.external_response_ports","hook_port":"topology.external_hook_ports"},"verifiedBy":"physical_table_manifest_bindings"}'::jsonb,
        '{"type":"screen_data_shape","topologySystemName":"auth-external-credential-management-topology-projection","userFacingTopologyLabel":"Auth / external credential management","tableRef":"topology.external_response_ports","dbTableName":"topology.external_response_ports","screenOperationKinds":["list","update"],"displayColumnMode":"selected","displayColumns":["external_port_context.port_kind","external_port_context.provider_kind","external_port_context.credential_kind","external_port_context.reference_key","external_port_context.required_by_bundle","external_port_context.consumer_bundle_binding","external_port_context.policy_template_key"],"logicalTables":[{"tableName":"external_port_context","columns":[{"name":"port_context_id","dataType":"uuid","nullable":false},{"name":"port_kind","dataType":"text","nullable":false},{"name":"provider_kind","dataType":"text","nullable":false},{"name":"credential_kind","dataType":"text","nullable":false},{"name":"reference_key","dataType":"text","nullable":true},{"name":"required_by_bundle","dataType":"text","nullable":false},{"name":"consumer_bundle_binding","dataType":"text","nullable":true},{"name":"policy_template_key","dataType":"text","nullable":false},{"name":"auth_user_id","dataType":"uuid","nullable":true}]},{"tableName":"policy_template_selection","columns":[{"name":"policy_template_key","dataType":"text","nullable":false},{"name":"port_kind","dataType":"text","nullable":false},{"name":"required_by_bundle","dataType":"text","nullable":false}]}],"relationIntents":[{"localTableRef":"external_port_context","joinTableRef":"auth.user","localKey":"auth_user_id","remoteKey":"id","remoteManifestId":"00000000-0000-0000-0000-000000000091"},{"localTableRef":"external_port_context","joinTableRef":"policy_template_selection","localKey":"policy_template_key","remoteKey":"policy_template_key"}],"operationEntityBindings":[{"operationKind":"list","entityTargetColumns":["port_kind","provider_kind","credential_kind","reference_key","required_by_bundle","consumer_bundle_binding","policy_template_key"]},{"operationKind":"update","entityTargetColumns":["credential_kind","reference_key","policy_template_key"]}],"initialDataRows":[{"values":{"port_kind":"access_port","provider_kind":"template-selected","credential_kind":"external","reference_key":"runtime-reference-key-only","required_by_bundle":"bundle-record-context","consumer_bundle_binding":"not-connected-in-this-bundle","policy_template_key":"external_access_port_generic_http"},"lineage":{"source":"seed_projection","bundle":"auth-external-credential-management-topology-projection"}},{"values":{"port_kind":"response_port","provider_kind":"template-selected","credential_kind":"external","reference_key":"runtime-reference-key-only","required_by_bundle":"bundle-record-context","consumer_bundle_binding":"not-connected-in-this-bundle","policy_template_key":"external_response_port_generic_http"},"lineage":{"source":"seed_projection","bundle":"auth-external-credential-management-topology-projection"}},{"values":{"port_kind":"hook_port","provider_kind":"template-selected","credential_kind":"external","reference_key":"runtime-reference-key-only","required_by_bundle":"bundle-record-context","consumer_bundle_binding":"not-connected-in-this-bundle","policy_template_key":"external_hook_port_scheduler_boundary"},"lineage":{"source":"seed_projection","bundle":"auth-external-credential-management-topology-projection"}}]}'::jsonb
    ]::jsonb[],
    'active'
)
ON CONFLICT (manifest_id) DO UPDATE
    SET topology = EXCLUDED.topology,
        status   = EXCLUDED.status;

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
VALUES (
    '00000000-0000-0000-0000-000000000092',
    '00000000-0000-0000-0000-0000000000a1',
    'auth.external.credential_management.projection',
    'active',
    '{"manifest_id":"00000000-0000-0000-0000-000000000092","source":"external-port-canonical-physical-binding-execution"}'::jsonb
)
ON CONFLICT (topology_manifest_id) DO UPDATE
    SET manifest_key   = EXCLUDED.manifest_key,
        status         = EXCLUDED.status,
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
           END,
           'source', 'external-port-canonical-physical-binding-execution',
           'manifestKey', 'auth.external.credential_management.projection')
FROM topology.physical_tables pt
WHERE pt.table_ref IN (
    'topology.external_access_ports',
    'topology.external_response_ports',
    'topology.external_hook_ports')
ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE
    SET active             = true,
        binding_evidence_json = EXCLUDED.binding_evidence_json,
        updated_at         = now();

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
    ('topology.record_file_attachment_bindings','topology', 'file_storage_bundle', true)
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
    '{"manifest_id":"00000000-0000-0000-0000-000000000093","source":"file-storage-bundle-export-job-dispatch","attachmentSurface":{"tableRef":"topology.record_file_attachment_bindings","artifactAuthority":"topology.file_artifacts","credentialPlane":"external_port_substrate_reference_key_only","operations":["topology.fs_bind_record_file_attachment","topology.fs_list_record_file_attachments","topology.fs_unbind_record_file_attachment"],"forbiddenProjectionFields":["storage_ref","authorization_key","signed_url","credential","bucket","endpoint"]},"screen_data_shape":{"type":"screen_data_shape","topologySystemName":"file-storage-attachment-manifest-seed","userFacingTopologyLabel":"File attachments","tableRef":"topology.record_file_attachment_bindings","dbTableName":"topology.record_file_attachment_bindings","displayColumnMode":"selected","displayColumns":["record_file_attachment_bindings.record_table_ref","record_file_attachment_bindings.record_id","file_artifacts.file_name","file_artifacts.file_type","file_artifacts.byte_size","file_artifacts.checksum_value","record_file_attachment_bindings.relation_kind","record_file_attachment_bindings.created_at"],"logicalTables":[{"tableName":"record_file_attachment_bindings","columns":[{"name":"attachment_binding_id","dataType":"uuid","nullable":false},{"name":"record_table_ref","dataType":"text","nullable":false},{"name":"record_id","dataType":"text","nullable":false},{"name":"file_artifact_id","dataType":"uuid","nullable":false},{"name":"relation_kind","dataType":"text","nullable":false},{"name":"created_at","dataType":"timestamptz","nullable":false}]},{"tableName":"file_artifacts","columns":[{"name":"file_artifact_id","dataType":"uuid","nullable":false},{"name":"file_name","dataType":"text","nullable":false},{"name":"file_type","dataType":"text","nullable":false},{"name":"byte_size","dataType":"bigint","nullable":true},{"name":"checksum_value","dataType":"text","nullable":false}]}],"relationIntents":[{"localTableRef":"record_file_attachment_bindings","joinTableRef":"file_artifacts","localKey":"file_artifact_id","remoteKey":"file_artifact_id"}],"operationEntityBindings":[{"operationKind":"bind_attachment","function":"topology.fs_bind_record_file_attachment","entityTargetColumns":["record_table_ref","record_id","file_artifact_id","relation_kind"]},{"operationKind":"list_attachments","function":"topology.fs_list_record_file_attachments","entityTargetColumns":["record_table_ref","record_id"]},{"operationKind":"unbind_attachment","function":"topology.fs_unbind_record_file_attachment","entityTargetColumns":["record_table_ref","record_id","file_artifact_id","relation_kind"]}]}}'::jsonb
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
    ('00000000-0000-0000-0000-000000000490', '00000000-0000-0000-0000-0000000000e4', 10, 'append_runtime_event_log', '{"event_type":"checksum_verified","entity_ref_key":"ChecksumValue"}', NULL, true),
    ('00000000-0000-0000-0000-000000000406', '00000000-0000-0000-0000-0000000000e4', 11, 'execute_abstract_function', '{}', 'file_storage.record_export_job', true),
    ('00000000-0000-0000-0000-000000000491', '00000000-0000-0000-0000-0000000000e4', 12, 'append_runtime_event_log', '{"event_type":"export_job_initiated","entity_ref_key":"ExportJobId"}', NULL, true),
    ('00000000-0000-0000-0000-000000000408', '00000000-0000-0000-0000-0000000000e4', 13, 'execute_abstract_function', '{}', 'file_storage.record_file_artifact', true),
    ('00000000-0000-0000-0000-000000000492', '00000000-0000-0000-0000-0000000000e4', 14, 'append_runtime_event_log', '{"event_type":"file_write_completed","entity_ref_key":"FileArtifactId"}', NULL, true),
    ('00000000-0000-0000-0000-000000000409', '00000000-0000-0000-0000-0000000000e4', 15, 'execute_abstract_function', '{}', 'file_storage.write_manifest_record', true),
    ('00000000-0000-0000-0000-000000000410', '00000000-0000-0000-0000-0000000000e4', 16, 'execute_abstract_function', '{}', 'file_storage.authorize_signed_download', true),
    ('00000000-0000-0000-0000-000000000493', '00000000-0000-0000-0000-0000000000e4', 17, 'append_runtime_event_log', '{"event_type":"signed_url_generated","entity_ref_key":"AuthorizationKey"}', NULL, true),
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
    ('00000000-0000-0000-0000-000000000494', '00000000-0000-0000-0000-0000000000e5', 10, 'append_runtime_event_log', '{"event_type":"checksum_verified","entity_ref_key":"ChecksumValue"}', NULL, true),
    ('00000000-0000-0000-0000-000000000416', '00000000-0000-0000-0000-0000000000e5', 11, 'execute_abstract_function', '{}', 'file_storage.record_export_job', true),
    ('00000000-0000-0000-0000-000000000495', '00000000-0000-0000-0000-0000000000e5', 12, 'append_runtime_event_log', '{"event_type":"export_job_initiated","entity_ref_key":"ExportJobId"}', NULL, true),
    ('00000000-0000-0000-0000-000000000418', '00000000-0000-0000-0000-0000000000e5', 13, 'execute_abstract_function', '{}', 'file_storage.record_file_artifact', true),
    ('00000000-0000-0000-0000-000000000496', '00000000-0000-0000-0000-0000000000e5', 14, 'append_runtime_event_log', '{"event_type":"file_write_completed","entity_ref_key":"FileArtifactId"}', NULL, true),
    ('00000000-0000-0000-0000-000000000419', '00000000-0000-0000-0000-0000000000e5', 15, 'execute_abstract_function', '{}', 'file_storage.write_manifest_record', true),
    ('00000000-0000-0000-0000-000000000420', '00000000-0000-0000-0000-0000000000e5', 16, 'execute_abstract_function', '{}', 'file_storage.authorize_signed_download', true),
    ('00000000-0000-0000-0000-000000000497', '00000000-0000-0000-0000-0000000000e5', 17, 'append_runtime_event_log', '{"event_type":"signed_url_generated","entity_ref_key":"AuthorizationKey"}', NULL, true),
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
    ('00000000-0000-0000-0000-000000000479', '00000000-0000-0000-0000-0000000000eb', 9, 'append_runtime_event_log',     '{"event_type":"transfer_initiated","evidence_table_ref":"topology.sftp_transfer_log","projection_table_ref":"topology.sftp_transfer_log","status_value":"transfer_initiated"}', NULL, true)
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
    ('00000000-0000-0000-0000-000000000479', '00000000-0000-0000-0000-0000000000eb',  9, 'append_runtime_event_log',     '{"event_type":"transfer_initiated","evidence_table_ref":"topology.sftp_transfer_log","projection_table_ref":"topology.sftp_transfer_log","status_value":"transfer_initiated","manifest_ref":"manifest_required","checksum_after_ref":"pending"}', NULL, true),
    ('00000000-0000-0000-0000-00000000047a', '00000000-0000-0000-0000-0000000000eb', 10, 'append_runtime_event_log',     '{"event_type":"transfer_completed","evidence_table_ref":"topology.sftp_transfer_log","projection_table_ref":"topology.sftp_transfer_log","status_value":"transfer_completed","manifest_ref":"manifest_required","checksum_after_ref":"verified_after_transfer"}', NULL, true),
    ('00000000-0000-0000-0000-00000000047b', '00000000-0000-0000-0000-0000000000eb', 11, 'append_runtime_event_log',     '{"event_type":"transfer_failed","evidence_table_ref":"topology.sftp_transfer_log","projection_table_ref":"topology.sftp_transfer_log","status_value":"transfer_failed","manifest_ref":"manifest_required"}', NULL, true),
    ('00000000-0000-0000-0000-00000000047c', '00000000-0000-0000-0000-0000000000eb', 12, 'append_runtime_event_log',     '{"event_type":"checksum_mismatch","evidence_table_ref":"topology.sftp_transfer_log","projection_table_ref":"topology.sftp_transfer_log","status_value":"checksum_mismatch","manifest_ref":"manifest_required","checksum_after_ref":"mismatch"}', NULL, true),
    ('00000000-0000-0000-0000-00000000047d', '00000000-0000-0000-0000-0000000000eb', 13, 'append_runtime_event_log',     '{"event_type":"retry_attempted","evidence_table_ref":"topology.sftp_transfer_log","projection_table_ref":"topology.sftp_transfer_log","status_value":"retry_attempted","manifest_ref":"manifest_required"}', NULL, true)
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
