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
INSERT INTO topologys.state_registry (state_id, name, owner)
VALUES
    (gen_random_uuid(), 'active',    'system'),
    (gen_random_uuid(), 'operating', 'business'),
    (gen_random_uuid(), 'archived',  'system')
ON CONFLICT DO NOTHING;


-- ---------------------------------------------------------------------------
-- relation_registry defaults
-- ---------------------------------------------------------------------------
INSERT INTO topologys.relation_registry (
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
INSERT INTO topologys.package_registry (package_id, name, type, package_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'default_package', 'core', '{}', true
)
ON CONFLICT (package_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- schema_registry — deterministic ID so structure_maps can reference it
-- ---------------------------------------------------------------------------
INSERT INTO topologys.schema_registry (schema_id, name, schema_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000002',
    'default_schema', '{"fields":[{"key":"label","type":"text","label":"Label"}]}', true
)
ON CONFLICT (schema_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- component_registry — deterministic ID so structure_maps can reference it
-- ---------------------------------------------------------------------------
INSERT INTO topologys.component_registry (component_id, name, component_type, component_def, active)
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
INSERT INTO topologys.structure_maps (
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
-- Admin manifests — active runtime routes for admin target routes.
--
-- dispatcher_mapping has no role field: requests with role=null (wildcard)
-- match these manifests. runtime_destination=admin_runtime routes to
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
            '{"type":"dispatcher_mapping","target":"admin","layer":"seed_runtime","action":"save"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000051',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"seed_runtime","action":"load"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000052',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"seed_runtime","action":"validate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000053',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"seed_runtime","action":"preview"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000054',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"seed_runtime","action":"import"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000055',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"context_token_registry","action":"list"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000056',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"context_token_registry","action":"create"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000057',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"context_token_registry","action":"deprecate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000058',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"registry_vector","action":"validate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-000000000059',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"ui_component_bucket","action":"list"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005a',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"package_generator","action":"generate"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005b',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"system_ci","action":"list_targets"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"admin_runtime"}'::jsonb
        ]::jsonb[],
        'active'
    ),
    (
        '00000000-0000-0000-0000-00000000005c',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","target":"admin","layer":"system_ci","action":"inspect"}'::jsonb,
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
INSERT INTO topologys.package_registry (package_id, name, type, package_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000020',
    'admin_package', 'admin', '{}', true
)
ON CONFLICT (package_id) DO NOTHING;

INSERT INTO topologys.schema_registry (schema_id, name, schema_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000021',
    'admin_schema', '{}', true
)
ON CONFLICT (schema_id) DO NOTHING;

INSERT INTO topologys.component_registry (component_id, name, component_type, component_def, active)
VALUES (
    '00000000-0000-0000-0000-000000000022',
    'admin_component', 'admin',
    '{}', true
)
ON CONFLICT (component_id) DO NOTHING;

-- structure_maps for admin attractor keys.
-- attractor_key format: "{target}:{layer}:{action}" (lowercase, matches OperationVectorResolver output).
INSERT INTO topologys.structure_maps (
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
    )
ON CONFLICT (structure_map_id) DO NOTHING;


-- ---------------------------------------------------------------------------
-- Seed Runtime admin structure_maps (Issue #84)
-- attractor_key format: "admin:seed_runtime:{action}" (lowercase)
-- All operations share the admin package / schema / component topology nodes.
-- ---------------------------------------------------------------------------
INSERT INTO topologys.structure_maps (
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
-- ui_component_bucket bootstrap seed (Issue #86 registration closure)
-- Canonical bootstrap: seed catalog entries that still require DB registration.
-- Re-runnable via unique (component_key, source_path).
-- ---------------------------------------------------------------------------
INSERT INTO ui_component_bucket (component_key, source_path, component_kind, status, metadata_json)
VALUES
    ('button.primitive','frontend/components/Button.tsx','action/button','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"action","visualRole":"button","capabilityTags":["emits_event","requires_event_binding","accepts_design"]}}'::jsonb),
    ('input.primitive','frontend/components/Input.tsx','form_input/input','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","requires_event_binding","accepts_design"]}}'::jsonb),
    ('table.primitive','frontend/components/Table.tsx','data_display/table','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"data_viewer","visualRole":"table","capabilityTags":["selectable","accepts_design"]}}'::jsonb),
    ('card.primitive','frontend/components/Card.tsx','display/card','bucketed','{"classification":{"runtimeConnected":true,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"primitive","semanticRole":"display","visualRole":"card","capabilityTags":["accepts_children","accepts_design"]}}'::jsonb),
    ('form_field.template','frontend/components/FormField.tsx','form_input/form_field','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"input","visualRole":"field","capabilityTags":["accepts_children","error_display","accepts_design"]}}'::jsonb),
    ('select.template','frontend/components/Select.tsx','form_input/select','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('checkbox.template','frontend/components/Checkbox.tsx','form_input/checkbox','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('badge.template','frontend/components/Badge.tsx','display/badge','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"display","visualRole":"badge","capabilityTags":["accepts_design"]}}'::jsonb),
    ('status_badge.template','frontend/components/Badge.tsx','display/status_badge','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"badge","capabilityTags":["accepts_design"]}}'::jsonb),
    ('alert.template','frontend/components/Alert.tsx','display/alert','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"alert","capabilityTags":["accepts_design","error_display"]}}'::jsonb),
    ('loading_state.template','frontend/components/LoadingState.tsx','feedback/loading','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"panel","capabilityTags":["loading_display","accepts_design"]}}'::jsonb),
    ('empty_state.template','frontend/components/EmptyState.tsx','feedback/empty','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"panel","capabilityTags":["accepts_design"]}}'::jsonb),
    ('error_state.template','frontend/components/ErrorState.tsx','feedback/error','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"feedback","visualRole":"panel","capabilityTags":["error_display","accepts_design"]}}'::jsonb),
    ('json_viewer.template','frontend/components/JsonViewer.tsx','data_display/json','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"data_viewer","visualRole":"json_viewer","capabilityTags":["displays_json","accepts_design"]}}'::jsonb),
    ('admin_page_shell.template','frontend/components/AdminPageShell.tsx','shell/admin_page','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"layout_shell","visualRole":"page_shell","capabilityTags":["accepts_children","accepts_actions","admin_only","accepts_layout"]}}'::jsonb),
    ('admin_section.template','frontend/components/AdminSection.tsx','shell/admin_section','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_children","accepts_actions","admin_only","accepts_layout"]}}'::jsonb),
    ('validation_result_panel.template','frontend/components/ValidationResultPanel.tsx','validation/result','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"validation","visualRole":"panel","capabilityTags":["displays_backend_result","accepts_design"]}}'::jsonb),
    ('textarea.template','frontend/components/Textarea.tsx','form_input/textarea_template','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('tabs.template','frontend/components/Tabs.tsx','disclosure/tabs','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"navigation","visualRole":"tabs","capabilityTags":["selectable","accepts_design"]}}'::jsonb),
    ('modal.template','frontend/components/Modal.tsx','disclosure/modal','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"display","visualRole":"modal","capabilityTags":["accepts_children","accepts_actions","accepts_design"]}}'::jsonb),
    ('tree.template','frontend/components/Tree.tsx','data_display/tree','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"navigation","visualRole":"tree","capabilityTags":["recursive","selectable","accepts_design"]}}'::jsonb),
    ('tree_node.template','frontend/components/Tree.tsx','data_display/tree_node','bucketed','{"classification":{"runtimeConnected":false,"registrationRequired":true,"lifecycleStatus":"code_only_drift","componentFamily":"template","semanticRole":"data_viewer","visualRole":"tree","capabilityTags":["recursive","selectable","accepts_design"]}}'::jsonb)
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
INSERT INTO topologys.function_parameters (function_name, parameter_key, parameter_value, active)
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
INSERT INTO topologys.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES (
    'context_event_retention',
    'retention_policy',
    '{"hot_days":90,"cold_days":365,"archive_strategy":"delete","batch_size":1000,"enabled":true,"schedule_interval_hours":24}',
    true
)
ON CONFLICT (function_name, parameter_key) DO NOTHING;
