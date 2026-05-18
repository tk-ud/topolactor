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
--     demo_entity_alpha:           00000000-0000-0000-0000-000000000041
--     demo_entity_beta:            00000000-0000-0000-0000-000000000042
--     demo_entity_gamma:           00000000-0000-0000-0000-000000000043
--     demo_token_active:           00000000-0000-0000-0000-000000000021
--     demo_token_warning:          00000000-0000-0000-0000-000000000022
--     demo_token_critical:         00000000-0000-0000-0000-000000000023
--     demo_session:                00000000-0000-0000-0000-000000000031
--     demo_event_overview:         00000000-0000-0000-0000-000000000051
--     demo_event_entity_list:      00000000-0000-0000-0000-000000000052
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
INSERT INTO relation_registry (
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
INSERT INTO schema_registry (schema_id, name, schema_def, active)
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
INSERT INTO package_registry (package_id, name, type, package_def, active)
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
INSERT INTO component_registry (component_id, name, component_type, component_def, active)
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
INSERT INTO component_registry (component_id, name, component_type, component_def, active)
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
INSERT INTO component_registry (component_id, name, component_type, component_def, active)
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
INSERT INTO component_registry (component_id, name, component_type, component_def, active)
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
INSERT INTO structure_maps (
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


-- ---------------------------------------------------------------------------
-- hubs — demo hub
-- Populated by attractor_resolve during canonical flow traversal.
-- state_id references state_registry by name to avoid hard-coding gen_random_uuid().
-- If 'active' state does not exist (seed_empty.sql not applied), this insert is skipped.
-- ---------------------------------------------------------------------------
INSERT INTO hubs (hub_id, relation_registry_id, state_id)
VALUES (
    '00000000-0000-0000-0000-000000000010',
    '00000000-0000-0000-0000-000000000011',
    (SELECT state_id FROM state_registry WHERE name = 'active' LIMIT 1)
)
ON CONFLICT (hub_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- entities — three demo entities inside the demo hub
-- entity_jsonb shape matches demo_entity_schema fields.
-- Changing a field value here changes what the schema_resolve step surfaces.
-- ---------------------------------------------------------------------------
INSERT INTO entities (entity_id, hub_id, entity_jsonb, relation_ids, state_id)
VALUES
    (
        '00000000-0000-0000-0000-000000000041',
        '00000000-0000-0000-0000-000000000010',
        '{"label":"Alpha Entity","state":"active","hub_id":"00000000-0000-0000-0000-000000000010"}',
        ARRAY['00000000-0000-0000-0000-000000000011']::uuid[],
        (SELECT state_id FROM state_registry WHERE name = 'active' LIMIT 1)
    ),
    (
        '00000000-0000-0000-0000-000000000042',
        '00000000-0000-0000-0000-000000000010',
        '{"label":"Beta Entity","state":"operating","hub_id":"00000000-0000-0000-0000-000000000010"}',
        ARRAY['00000000-0000-0000-0000-000000000011']::uuid[],
        (SELECT state_id FROM state_registry WHERE name = 'operating' LIMIT 1)
    ),
    (
        '00000000-0000-0000-0000-000000000043',
        '00000000-0000-0000-0000-000000000010',
        '{"label":"Gamma Entity","state":"active","hub_id":"00000000-0000-0000-0000-000000000010"}',
        ARRAY['00000000-0000-0000-0000-000000000011']::uuid[],
        (SELECT state_id FROM state_registry WHERE name = 'active' LIMIT 1)
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
--   prefix 0 (last=event_overview)   → next: demo:entity:list
--   prefix 1 (last=event_entity_list) → next: NULL (no subsequent event)
--
-- With demo_policy min_neighbors=1, a dispatch with ContextSessionId=demo_session
-- and token_ids including token_active (0021) will find prefix 0 at similarity=1.0
-- and recommend demo:entity:list as the next operation.
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
-- function_parameters — demo_auth / demo_users
-- Demo-only credentials for the JWT login scaffold.
--
-- Stored as a JSON array of {username, password_hash, role} objects.
-- password_hash values are bcrypt (cost 12) hashes of the demo passwords below.
-- These are PUBLIC demo credentials — not real business credentials.
--
--   demo_admin  / demo_admin_password   → role: admin
--   demo_public / demo_public_password  → role: public
--
-- To regenerate hashes (Python):
--   import bcrypt
--   bcrypt.hashpw(b'demo_admin_password', bcrypt.gensalt(rounds=12)).decode()
--
-- JWT config (not stored here): DEMO_JWT_SECRET / DEMO_JWT_ISSUER / DEMO_JWT_EXPIRY_HOURS
-- env vars consumed by AuthEndpoint and JwtGuard.
-- ---------------------------------------------------------------------------
INSERT INTO function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'demo_auth',
    'demo_users',
    '[{"username":"demo_admin","password_hash":"$2b$12$E5kCP8.xxEW.yYdCow49DebJ1sRmjx5ihWTnhJ32iViIoP7Seclx2","role":"admin"},{"username":"demo_public","password_hash":"$2b$12$8FMsfslui6GEEmi7YRb8UeuH1IOJcye0R3s/4fSYHmYJIzHrOwKo.","role":"public"}]',
    true
)
ON CONFLICT (function_name, parameter_key) DO NOTHING;


-- ---------------------------------------------------------------------------
-- function_parameters — demo_policy
-- Demo-scoped recommendation policy referenced by demo structure_maps via
-- context_route_policy_ref = 'demo_policy'.
-- Changing aggregation_limit or prefer_recent here changes the windowed
-- transition stats scope used when resolving demo route recommendations.
-- ---------------------------------------------------------------------------
INSERT INTO function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'context_route_recommendation_resolve',
    'demo_policy',
    '{"min_similarity":0.02,"top_k":20,"min_neighbors":1,"recent_days":null,"max_candidates_shown":3,"baseline_weight":0.4,"neighbor_weight":0.6,"transition_aggregation":{"aggregation_limit":10000,"prefer_recent":true,"recent_days":null}}',
    true
)
ON CONFLICT (function_name, parameter_key) DO UPDATE
    SET parameter_value = EXCLUDED.parameter_value,
        active          = EXCLUDED.active;
