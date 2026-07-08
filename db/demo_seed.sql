-- =============================================================================
-- demo_seed.sql
-- Public scaffold demo seed for the topolactor database.
--
-- PURPOSE:
--   Inserts fake/demo-only data to illustrate the canonical flow end-to-end:
--   hub / entities / topology registry / structure_map / package-schema-component
--   refs / context token registry / context events / recommendation policy.
--
--   NO real business data. All data is synthetic and labelled as demo.
--
-- DETERMINISTIC IDs:
--   Demo topology nodes use fixed UUIDs so frontend scaffold and DB seed
--   reference the same IDs without coordination:
--
--     demo_relation_registry:      00000000-0000-0000-0000-000000000011
--     demo_schema_registry:        00000000-0000-0000-0000-000000000012
--     demo_package_registry:       00000000-0000-0000-0000-000000000013
--     demo_component_hub_overview: 00000000-0000-0000-0000-000000000014
--     demo_component_entity_table: 00000000-0000-0000-0000-000000000015
--     demo_component_recommendation:00000000-0000-0000-0000-000000000016
--     demo_component_token_badges: 00000000-0000-0000-0000-000000000017
--     demo_structure_map:          00000000-0000-0000-0000-000000000018
--     demo_hub:                    00000000-0000-0000-0000-000000000010
--     demo_hub_secondary:          00000000-0000-0000-0000-00000000001d
--     demo_topology_manifest:      00000000-0000-0000-0000-000000000044
--     demo_hub_relation_primary:   00000000-0000-0000-0000-000000000045
--     demo_entity_alpha:           00000000-0000-0000-0000-000000000041
--     demo_entity_beta:            00000000-0000-0000-0000-000000000042
--     demo_entity_gamma:           00000000-0000-0000-0000-000000000043
--     demo_token_active:           00000000-0000-0000-0000-000000000021
--     demo_token_warning:          00000000-0000-0000-0000-000000000022
--     demo_token_critical:         00000000-0000-0000-0000-000000000023
--     demo_session:                00000000-0000-0000-0000-000000000031
--     demo_event_overview:         00000000-0000-0000-0000-000000000051
--     demo_event_entity_list:      00000000-0000-0000-0000-000000000052
--     demo_dispatch_manifest_hub_overview:  00000000-0000-0000-0000-000000000080
--     demo_dispatch_manifest_entity_list:   00000000-0000-0000-0000-000000000081
--     demo_dispatch_manifest_entity_detail: 00000000-0000-0000-0000-000000000082
--     demo_dispatch_manifest_entity_create: 00000000-0000-0000-0000-000000000083
--     demo_dispatch_manifest_entity_advance: 00000000-0000-0000-0000-000000000084
--
-- HOW TO RUN (after seed_empty.sql):
--   psql -d <database> -f db/schema.sql
--   psql -d <database> -f db/topology_tables.sql
--   psql -d <database> -f db/promotion_tables.sql
--   psql -d <database> -f db/context_route_tables.sql
--   psql -d <database> -f db/seed_empty.sql
--   psql -d <database> -f db/demo_seed.sql
--
-- SCHEMA INTEGRITY:
--   SQL execution validity is secondary to schema integrity.
--   Rows with FK or constraint conflicts will be skipped via ON CONFLICT DO NOTHING.
--   If a reference cannot be satisfied at apply time, a comment below explains why.
-- =============================================================================


-- ---------------------------------------------------------------------------
-- relation_registry — demo relation
-- Anchors the demo hub to a named relation.
-- Changing this relation's weight or manifest_candidate flag alters
-- how the demo hub is traversed in attractor_resolve.
-- ---------------------------------------------------------------------------
INSERT INTO topology.relation_registry (
    relation_registry_id,
    name, master_ids, category, type, "order", weight, manifest_candidate, active
)
VALUES (
    '00000000-0000-0000-0000-000000000011',
    'demo_relation', '{}', 'demo', 'structural', 10, 1.0, false, true
)
ON CONFLICT (relation_registry_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- schema_registry — demo hub/entity schema
-- Defines the converged entity payload shape for demo entities.
-- Changing schema_def here changes what fields schema_resolve produces.
-- ---------------------------------------------------------------------------
INSERT INTO topology.schema_registry (schema_id, name, schema_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000012',
    'demo_entity_schema',
    '{"fields":[{"key":"label","type":"text","label":"Label"},{"key":"state","type":"text","label":"State"},{"key":"hub_id","type":"text","label":"Hub ID"}]}',
    true
)
ON CONFLICT (schema_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- package_registry — demo package
-- Groups the demo hub overview and entity table components.
-- Changing component membership here changes what component_expand produces.
-- ---------------------------------------------------------------------------
INSERT INTO topology.package_registry (package_id, name, type, package_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000013',
    'demo_hub_overview_package',
    'demo',
    '{"description":"Demo package for hub overview, entity table, and recommendation projections"}',
    true
)
ON CONFLICT (package_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- component_registry — demo hub overview component
-- ---------------------------------------------------------------------------
INSERT INTO topology.component_registry (component_id, name, component_type, component_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000014',
    'demo_hub_overview',
    'demo-renderer',
    '{"renders":"hub_overview","package":"demo_hub_overview_package"}',
    true
)
ON CONFLICT (component_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- component_registry — demo entity table component
-- ---------------------------------------------------------------------------
INSERT INTO topology.component_registry (component_id, name, component_type, component_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000015',
    'demo_entity_table',
    'demo-renderer',
    '{"renders":"entity_table","package":"demo_entity_registry_package"}',
    true
)
ON CONFLICT (component_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- component_registry — demo recommendation panel component
-- ---------------------------------------------------------------------------
INSERT INTO topology.component_registry (component_id, name, component_type, component_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000016',
    'demo_recommendation_panel',
    'demo-renderer',
    '{"renders":"recommendation","package":"demo_recommendation_package"}',
    true
)
ON CONFLICT (component_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- component_registry — demo context token badges component
-- ---------------------------------------------------------------------------
INSERT INTO topology.component_registry (component_id, name, component_type, component_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000017',
    'demo_token_badges',
    'demo-renderer',
    '{"renders":"context_token_badges"}',
    true
)
ON CONFLICT (component_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- structure_maps — demo hub overview attractor
--
-- state_policy uses context_route_policy_ref = 'demo_policy' so the
-- recommendation resolver picks up the demo-scoped policy from function_parameters
-- instead of default_policy.
-- Changing context_route_policy_ref → resolver loads a different policy key.
-- ---------------------------------------------------------------------------
INSERT INTO topology.structure_maps (
    structure_map_id,
    name,
    attractor_key,
    package_id,
    schema_id,
    component_ids,
    state_policy,
    active
)
VALUES (
    '00000000-0000-0000-0000-000000000018',
    'demo_hub_overview',
    'demo:hub:overview',
    '00000000-0000-0000-0000-000000000013',
    '00000000-0000-0000-0000-000000000012',
    ARRAY[
        '00000000-0000-0000-0000-000000000014',
        '00000000-0000-0000-0000-000000000015',
        '00000000-0000-0000-0000-000000000016',
        '00000000-0000-0000-0000-000000000017'
    ]::uuid[],
    '{"context_route_policy_ref":"demo_policy"}',
    true
)
ON CONFLICT (structure_map_id) DO NOTHING;

INSERT INTO topology.structure_maps (
    structure_map_id, name, attractor_key, package_id, schema_id, component_ids, state_policy, active
)
VALUES
    ('00000000-0000-0000-0000-000000000019','demo_entity_list','demo:entity:list','00000000-0000-0000-0000-000000000013','00000000-0000-0000-0000-000000000012',ARRAY['00000000-0000-0000-0000-000000000015','00000000-0000-0000-0000-000000000017']::uuid[],'{"context_route_policy_ref":"demo_policy"}',true),
    ('00000000-0000-0000-0000-00000000001a','demo_entity_detail','demo:entity:detail','00000000-0000-0000-0000-000000000013','00000000-0000-0000-0000-000000000012',ARRAY['00000000-0000-0000-0000-000000000015','00000000-0000-0000-0000-000000000017']::uuid[],'{"context_route_policy_ref":"demo_policy"}',true),
    ('00000000-0000-0000-0000-00000000001b','demo_entity_create','demo:entity:create','00000000-0000-0000-0000-000000000013','00000000-0000-0000-0000-000000000012',ARRAY['00000000-0000-0000-0000-000000000015','00000000-0000-0000-0000-000000000017']::uuid[],'{"context_route_policy_ref":"demo_policy"}',true),
    ('00000000-0000-0000-0000-00000000001c','demo_entity_advance','demo:entity:advance','00000000-0000-0000-0000-000000000013','00000000-0000-0000-0000-000000000012',ARRAY['00000000-0000-0000-0000-000000000015','00000000-0000-0000-0000-000000000017']::uuid[],'{"context_route_policy_ref":"demo_policy"}',true)
ON CONFLICT (structure_map_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- manifest — demo dispatch routes (IDs 80-84)
-- Required when ManifestDispatcher uses DB manifest resolution (production path).
-- Maps demo OperationPanel presets to topology_transform_runtime (canonical pipeline).
-- Without these rows, POST /dispatch returns MANIFEST_NOT_FOUND for demo presets.
-- ---------------------------------------------------------------------------
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-000000000080',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"demo","layer":"hub","action":"overview"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"topology_transform_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000081',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"demo","layer":"entity","action":"list"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"topology_transform_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000082',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"demo","layer":"entity","action":"detail"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"topology_transform_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000083',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"demo","layer":"entity","action":"create"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"topology_transform_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000084',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"demo","layer":"entity","action":"advance"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"topology_transform_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000085',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"demo","layer":"screen_list","action":"Read"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"topology_transform_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- hubs — demo hub
-- Populated by attractor_resolve during canonical flow traversal.
-- state_id references state_registry by name to avoid hard-coding gen_random_uuid().
-- If 'active' state does not exist (seed_empty.sql not applied), this insert is skipped.
-- ---------------------------------------------------------------------------
INSERT INTO hubs.hub (hub_id, relation_registry_id, state_id)
VALUES (
    '00000000-0000-0000-0000-000000000010',
    '00000000-0000-0000-0000-000000000011',
    (SELECT state_id FROM topology.state_registry WHERE name = 'active' LIMIT 1)
)
ON CONFLICT (hub_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- hubs — secondary demo hub (related_hub_id target for manifest-scoped sequence)
-- ---------------------------------------------------------------------------
INSERT INTO hubs.hub (hub_id, relation_registry_id, state_id)
VALUES (
    '00000000-0000-0000-0000-00000000001d',
    '00000000-0000-0000-0000-000000000011',
    (SELECT state_id FROM topology.state_registry WHERE name = 'active' LIMIT 1)
)
ON CONFLICT (hub_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- hubs.topology_manifests — demo manifest under primary demo hub
-- ---------------------------------------------------------------------------
INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
VALUES (
    '00000000-0000-0000-0000-000000000044',
    '00000000-0000-0000-0000-000000000010',
    'demo_manifest',
    'active',
    '{"demo": true}'::jsonb
)
ON CONFLICT (topology_manifest_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- hubs.hub_relations — manifest-scoped sequence entries (no hub_id source authority)
-- ---------------------------------------------------------------------------
INSERT INTO hubs.hub_relations (
    hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, relation_config, status
)
VALUES
    (
        '00000000-0000-0000-0000-000000000045',
        '00000000-0000-0000-0000-000000000044',
        '00000000-0000-0000-0000-00000000001d',
        1,
        '{"transition": "to_secondary_hub"}'::jsonb,
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000046',
        '00000000-0000-0000-0000-000000000044',
        '00000000-0000-0000-0000-000000000010',
        2,
        '{"transition": "return_to_primary_hub"}'::jsonb,
        'active'
    )
ON CONFLICT (topology_manifest_id, sequence_position) DO NOTHING;


-- ---------------------------------------------------------------------------
-- entities — three demo entities inside the demo hub
-- entity_jsonb shape matches demo_entity_schema fields.
-- Changing a field value here changes what the schema_resolve step surfaces.
-- ---------------------------------------------------------------------------
INSERT INTO topology.entities (entity_id, hub_id, entity_jsonb, relation_ids, state_id)
VALUES
    (
        '00000000-0000-0000-0000-000000000041',
        '00000000-0000-0000-0000-000000000010',
        '{"label":"Alpha Entity","state":"active","hub_id":"00000000-0000-0000-0000-000000000010"}',
        ARRAY['00000000-0000-0000-0000-000000000011']::uuid[],
        (SELECT state_id FROM topology.state_registry WHERE name = 'active' LIMIT 1)
    ),
    (
        '00000000-0000-0000-0000-000000000042',
        '00000000-0000-0000-0000-000000000010',
        '{"label":"Beta Entity","state":"operating","hub_id":"00000000-0000-0000-0000-000000000010"}',
        ARRAY['00000000-0000-0000-0000-000000000011']::uuid[],
        (SELECT state_id FROM topology.state_registry WHERE name = 'operating' LIMIT 1)
    ),
    (
        '00000000-0000-0000-0000-000000000043',
        '00000000-0000-0000-0000-000000000010',
        '{"label":"Gamma Entity","state":"active","hub_id":"00000000-0000-0000-0000-000000000010"}',
        ARRAY['00000000-0000-0000-0000-000000000011']::uuid[],
        (SELECT state_id FROM topology.state_registry WHERE name = 'active' LIMIT 1)
    )
ON CONFLICT (entity_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- context_token_registry — demo status tokens
-- value is the meaning direction component [-1.0, 1.0].
-- Changing a token's value changes the sparse vector for any event that
-- includes that token, which changes cosine similarity and recommendation scores.
-- This is the key observable: token value → runtime resolution change.
-- ---------------------------------------------------------------------------
INSERT INTO context_token_registry (token_id, label, "group", value, status)
VALUES
    ('00000000-0000-0000-0000-000000000021', 'active',   'status', 1.0,  'active'),
    ('00000000-0000-0000-0000-000000000022', 'warning',  'status', 0.0,  'active'),
    ('00000000-0000-0000-0000-000000000023', 'critical', 'status', -1.0, 'active')
ON CONFLICT (label, "group") DO NOTHING;


-- ---------------------------------------------------------------------------
-- context_session — demo session
-- ---------------------------------------------------------------------------
INSERT INTO context_session (session_id, user_id, role, last_seen_at)
VALUES (
    '00000000-0000-0000-0000-000000000031',
    'demo_user',
    'demo_role',
    now()
)
ON CONFLICT (session_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- context_event — demo operation events (prefix vector source)
-- Two sequential events in the demo session showing a demo:hub:overview →
-- demo:entity:list transition.
-- Fixed UUIDs allow context_event_vector_cache and context_prefix_vector_cache
-- to be seeded deterministically below.
-- ---------------------------------------------------------------------------
INSERT INTO context_event (
    event_id, session_id, user_id, role,
    table_name, operation, token_ids, created_at
)
VALUES
    (
        '00000000-0000-0000-0000-000000000051',
        '00000000-0000-0000-0000-000000000031',
        'demo_user', 'demo_role',
        'hubs',
        'demo:hub:overview',
        ARRAY['00000000-0000-0000-0000-000000000021']::uuid[],
        now() - interval '2 minutes'
    ),
    (
        '00000000-0000-0000-0000-000000000052',
        '00000000-0000-0000-0000-000000000031',
        'demo_user', 'demo_role',
        'entities',
        'demo:entity:list',
        ARRAY['00000000-0000-0000-0000-000000000021', '00000000-0000-0000-0000-000000000022']::uuid[],
        now() - interval '1 minute'
    )
ON CONFLICT (event_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- context_event_vector_cache — pre-computed event vectors for demo events
--
-- vector_sparse: {token_id_string: value} derived from context_token_registry.
--   demo_token_active  (0021): value=1.0  → contributes to l2_norm
--   demo_token_warning (0022): value=0.0  → present but does not affect l2_norm
--
-- l2_norm = sqrt(SUM(value^2)):
--   event_overview:    sqrt(1.0^2)         = 1.0
--   event_entity_list: sqrt(1.0^2 + 0.0^2) = 1.0
-- ---------------------------------------------------------------------------
INSERT INTO context_event_vector_cache (event_id, vector_sparse, l2_norm, updated_at)
VALUES
    (
        '00000000-0000-0000-0000-000000000051',
        '{"00000000-0000-0000-0000-000000000021": 1.0}'::jsonb,
        1.0,
        now()
    ),
    (
        '00000000-0000-0000-0000-000000000052',
        '{"00000000-0000-0000-0000-000000000021": 1.0, "00000000-0000-0000-0000-000000000022": 0.0}'::jsonb,
        1.0,
        now()
    )
ON CONFLICT (event_id) DO UPDATE
    SET vector_sparse = EXCLUDED.vector_sparse,
        l2_norm       = EXCLUDED.l2_norm,
        updated_at    = now();


-- ---------------------------------------------------------------------------
-- context_prefix_vector_cache — pre-computed prefix vectors for demo session
--
-- Prefix vector = SUM(event_vectors[0..prefix_index]).
--   prefix_index 0: SUM([event_overview])               → {0021: 1.0},           l2_norm=1.0
--   prefix_index 1: SUM([event_overview, entity_list])  → {0021: 2.0, 0022: 0.0}, l2_norm=2.0
--
-- next_operation for each prefix is resolved at query time via LATERAL JOIN on
-- context_event (first event after last_event_id in the same session):
--   prefix 0 (last=event_overview)    → next: demo:entity:list  (always stable)
--   prefix 1 (last=event_entity_list) → next: NULL at recommendation candidate read time
--
-- The NULL for prefix 1 holds at recommendation candidate read time because
-- ContextRouteRecommendationResolver runs LoadRecentPrefixVectorsAsync before
-- AppendContextEventAsync. At the point the LATERAL JOIN executes, no event follows
-- demo_event_entity_list in the session — prefix 1 finds NULL and does not vote.
-- AppendContextEventAsync runs after all reads on every dispatch path (including
-- cold-start), so the current operation is recorded for future recommendations.
--
-- With demo_policy min_neighbors=1, a dispatch with ContextSessionId=demo_session
-- and token_ids including token_active (0021) will find prefix 0 at similarity=1.0
-- and recommend demo:entity:list as the sole next operation candidate.
-- ---------------------------------------------------------------------------
INSERT INTO context_prefix_vector_cache
    (session_id, prefix_index, last_event_id, vector_sparse, l2_norm, updated_at)
VALUES
    (
        '00000000-0000-0000-0000-000000000031',
        0,
        '00000000-0000-0000-0000-000000000051',
        '{"00000000-0000-0000-0000-000000000021": 1.0}'::jsonb,
        1.0,
        now()
    ),
    (
        '00000000-0000-0000-0000-000000000031',
        1,
        '00000000-0000-0000-0000-000000000052',
        '{"00000000-0000-0000-0000-000000000021": 2.0, "00000000-0000-0000-0000-000000000022": 0.0}'::jsonb,
        2.0,
        now()
    )
ON CONFLICT (session_id, prefix_index) DO UPDATE
    SET last_event_id = EXCLUDED.last_event_id,
        vector_sparse = EXCLUDED.vector_sparse,
        l2_norm       = EXCLUDED.l2_norm,
        updated_at    = now();


-- ---------------------------------------------------------------------------
-- function_parameters — demo_auth / demo_users (deprecated marker only)
-- Credential hashes live only in auth.credentials (db/auth_seed.sql).
-- ---------------------------------------------------------------------------
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'demo_auth',
    'demo_users',
    '{"credential_source":"auth_db","note":"Login uses auth.users/auth.credentials; this row is a demo boundary marker only."}',
    true
)
ON CONFLICT (function_name, parameter_key) DO UPDATE
    SET parameter_value = EXCLUDED.parameter_value,
        active          = EXCLUDED.active;


-- ---------------------------------------------------------------------------
-- function_parameters — demo_policy
-- Demo-scoped recommendation policy referenced by demo structure_maps via
-- context_route_policy_ref = 'demo_policy'.
-- Changing aggregation_limit or prefer_recent here changes the windowed
-- transition stats scope used when resolving demo route recommendations.
-- ---------------------------------------------------------------------------
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'context_route_recommendation_resolve',
    'demo_policy',
    '{"min_similarity":0.02,"top_k":20,"min_neighbors":1,"recent_days":null,"max_candidates_shown":3,"baseline_weight":0.4,"neighbor_weight":0.6,"transition_aggregation":{"aggregation_limit":10000,"prefer_recent":true,"recent_days":null},"topology_vector_runtime":{"recommendation_blend":{"enabled":true,"scope_limit":1000,"attention_score_weight":1.0,"trend_weight":0.0,"statistics_weight":0.0}}}',
    true
)
ON CONFLICT (function_name, parameter_key) DO UPDATE
    SET parameter_value = EXCLUDED.parameter_value,
        active          = EXCLUDED.active;


-- ---------------------------------------------------------------------------
-- UI/UX Primitive Catalog bootstrap rows (demo context)
-- Upper SSOT: docs/design/ui-ux-primitive-catalog-ssot.yaml
-- These rows are bootstrap-only. seed_empty.sql is the canonical source.
-- Included here so demo_seed.sql is self-contained when run in isolation.
-- ON CONFLICT DO NOTHING: seed_empty.sql rows take precedence.
-- ---------------------------------------------------------------------------
INSERT INTO topology.components_bucket (component_key, source_path, component_kind, status, metadata_json)
VALUES
    ('autocomplete_input.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/autocomplete_input','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('suggest_input.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/suggest_input','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('search_combobox.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/search_combobox','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","selectable","accepts_design"]}}'::jsonb),
    ('select_import_dialog.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/select_import_dialog','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"modal","capabilityTags":["accepts_children","accepts_actions","admin_only"]}}'::jsonb),
    ('relation_candidate_picker.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/relation_candidate_picker','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["selectable","emits_event","accepts_design"]}}'::jsonb),
    ('recent_input_suggest.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/recent_input_suggest','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('duplicate_merge_candidate_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/duplicate_merge_candidate_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["accepts_actions","displays_backend_result","accepts_design"]}}'::jsonb),
    ('candidate_confidence_badge.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/candidate_confidence_badge','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"badge","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('relation_path_preview.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/relation_path_preview','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('field_resolver_inspector.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/field_resolver_inspector','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('schema_promotion_candidate_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','search_suggest/schema_promotion_candidate_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","admin_only","displays_backend_result"]}}'::jsonb),
    ('inline_editable_field.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','inline_edit/inline_editable_field','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('inline_editable_jsonb_field.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','inline_edit/inline_editable_jsonb_field','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"json_viewer","capabilityTags":["controlled_value","emits_event","displays_json","accepts_design"]}}'::jsonb),
    ('patch_preview_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','inline_edit/patch_preview_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('diff_strike_text.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','inline_edit/diff_strike_text','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["accepts_design"]}}'::jsonb),
    ('audit_diff_drawer.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','inline_edit/audit_diff_drawer','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"modal","capabilityTags":["displays_backend_result","accepts_children","accepts_design"]}}'::jsonb),
    ('optimistic_update_boundary.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','inline_edit/optimistic_update_boundary','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"validation","visualRole":"panel","capabilityTags":["accepts_children","accepts_design"]}}'::jsonb),
    ('confirmed_update_button.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','inline_edit/confirmed_update_button','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"action","visualRole":"button","capabilityTags":["emits_event","requires_event_binding","accepts_design"]}}'::jsonb),
    ('undo_timeline.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','inline_edit/undo_timeline','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"navigation","visualRole":"panel","capabilityTags":["selectable","displays_backend_result","accepts_design"]}}'::jsonb),
    ('conflict_resolution_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','inline_edit/conflict_resolution_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","displays_backend_result","admin_only"]}}'::jsonb),
    ('faceted_filter_bar.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/faceted_filter_bar','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('column_filter.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/column_filter','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('column_visibility_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/column_visibility_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('sort_control.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/sort_control','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('group_by_control.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/group_by_control','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('saved_view_selector.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/saved_view_selector','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["selectable","emits_event","accepts_design"]}}'::jsonb),
    ('bulk_action_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/bulk_action_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"action","visualRole":"panel","capabilityTags":["accepts_actions","emits_event","accepts_design"]}}'::jsonb),
    ('virtualized_data_table.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/virtualized_data_table','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"table","capabilityTags":["selectable","accepts_design"]}}'::jsonb),
    ('row_detail_drawer.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/row_detail_drawer','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"modal","capabilityTags":["accepts_children","accepts_design"]}}'::jsonb),
    ('pagination_control.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/pagination_control','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"navigation","visualRole":"panel","capabilityTags":["emits_event","accepts_design"]}}'::jsonb),
    ('export_candidate_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','table_op/export_candidate_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","admin_only"]}}'::jsonb),
    ('kanban_board.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/kanban_board','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"panel","capabilityTags":["accepts_children","selectable","accepts_layout"]}}'::jsonb),
    ('drag_drop_state_transition.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/drag_drop_state_transition','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"action","visualRole":"panel","capabilityTags":["emits_event","requires_event_binding"]}}'::jsonb),
    ('drag_sort_list.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/drag_sort_list','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"panel","capabilityTags":["accepts_children","selectable","emits_event","accepts_layout"]}}'::jsonb),
    ('relation_drop_zone.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/relation_drop_zone','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_children","emits_event","accepts_layout"]}}'::jsonb),
    ('tree_reorder_drop_zone.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/tree_reorder_drop_zone','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"tree","capabilityTags":["accepts_children","emits_event","accepts_layout"]}}'::jsonb),
    ('layout_drop_zone.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/layout_drop_zone','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_children","accepts_layout","accepts_design"]}}'::jsonb),
    ('component_placement_handle.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/component_placement_handle','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["admin_only","accepts_layout"]}}'::jsonb),
    ('snap_grid_overlay.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/snap_grid_overlay','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_layout","admin_only"]}}'::jsonb),
    ('state_transition_arrow.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/state_transition_arrow','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["accepts_design"]}}'::jsonb),
    ('slot_placeholder_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','kanban_drag/slot_placeholder_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_layout","admin_only"]}}'::jsonb),
    ('font_token_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/font_token_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('background_color_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/background_color_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('text_color_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/text_color_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('spacing_token_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/spacing_token_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('border_radius_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/border_radius_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('theme_preview_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/theme_preview_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('layout_grid_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/layout_grid_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["controlled_value","emits_event","admin_only","accepts_layout"]}}'::jsonb),
    ('responsive_rule_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/responsive_rule_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["controlled_value","emits_event","admin_only","accepts_layout"]}}'::jsonb),
    ('style_token_picker.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/style_token_picker','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["selectable","emits_event","accepts_design"]}}'::jsonb),
    ('css_variable_preview.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/css_variable_preview','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('shadow_token_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/shadow_token_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('animation_token_editor.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','design_token/animation_token_editor','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('calculation_preview_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/calculation_preview_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('formula_builder.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/formula_builder','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"panel","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('computed_field_preview.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/computed_field_preview','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('relation_score_preview.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/relation_score_preview','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('hub_statistics_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/hub_statistics_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('aggregation_preview_table.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/aggregation_preview_table','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"table","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('cross_entity_calculation_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/cross_entity_calculation_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('topology_distance_preview.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/topology_distance_preview','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('route_cost_preview.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/route_cost_preview','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('attention_weight_preview.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/attention_weight_preview','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('cooccurrence_matrix_preview.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/cooccurrence_matrix_preview','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('rank_score_preview.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','calc_topology/rank_score_preview','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('kana_assist_input.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','external_lookup/kana_assist_input','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('postal_address_lookup.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','external_lookup/postal_address_lookup','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('address_postal_lookup.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','external_lookup/address_postal_lookup','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('tel_address_candidate_lookup.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','external_lookup/tel_address_candidate_lookup','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('normalize_address_candidate.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','external_lookup/normalize_address_candidate','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('lookup_candidate_confirm_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','external_lookup/lookup_candidate_confirm_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"validation","visualRole":"panel","capabilityTags":["accepts_actions","displays_backend_result"]}}'::jsonb),
    ('bulk_import_candidate_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','external_lookup/bulk_import_candidate_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","admin_only","displays_backend_result"]}}'::jsonb),
    ('command_palette.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/command_palette','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"navigation","visualRole":"modal","capabilityTags":["accepts_actions","admin_only"]}}'::jsonb),
    ('empty_state_action_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/empty_state_action_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"feedback","visualRole":"panel","capabilityTags":["accepts_actions","accepts_design"]}}'::jsonb),
    ('operation_guard_banner.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/operation_guard_banner','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"feedback","visualRole":"alert","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('mutation_boundary_inspector.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/mutation_boundary_inspector','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('permission_hint_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/permission_hint_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"feedback","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('dry_run_result_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/dry_run_result_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('apply_confirm_dialog.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/apply_confirm_dialog','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"modal","capabilityTags":["accepts_actions","admin_only"]}}'::jsonb),
    ('rollback_candidate_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/rollback_candidate_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"admin_operation","visualRole":"panel","capabilityTags":["accepts_actions","admin_only","displays_backend_result"]}}'::jsonb),
    ('operation_audit_log_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/operation_audit_log_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"diagnostic","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('validation_error_panel.primitive','docs/design/ui-ux-primitive-catalog-ssot.yaml','safety_guard/validation_error_panel','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"validation","visualRole":"panel","capabilityTags":["error_display","displays_backend_result","accepts_design"]}}'::jsonb)
ON CONFLICT (component_key, source_path) DO NOTHING;

-- ---------------------------------------------------------------------------
-- projection lane seed hardening fixture (DB-free static tests also parse this)
-- ---------------------------------------------------------------------------
INSERT INTO topology.physical_tables (table_ref, schema_name, category, active)
VALUES ('seed.projection_lane', 'seed', 'projection_lane_test', true)
ON CONFLICT (table_ref) DO NOTHING;

INSERT INTO topology.wiring_physical_to_package (physical_table_id, package_id, wiring_def, active)
SELECT physical_table_id,
       '00000000-0000-0000-0000-000000000001'::uuid,
       '{"seedContract":"projection-lane-seed-test-hardening"}'::jsonb,
       true
FROM topology.physical_tables
WHERE table_ref = 'seed.projection_lane'
ON CONFLICT DO NOTHING;
