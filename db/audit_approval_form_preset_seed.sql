-- =============================================================================
-- audit_approval_form_preset_seed.sql
-- audit_approval_bundle domain mutations and UI Builder preset.
-- Implements approval_request / approval_evidence / notification_evidence
-- through the existing generic external_port substrate lane.
-- Approval authority is UI/human explicit action only per SSOT.
-- Prohibited: notification provider-specific client, AI autonomous approval,
--             implicit approval, CLI/MCP approval trigger, silent audit log skip,
--             credential plaintext in evidence.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Abstract function manifests for audit_approval domain mutations (af13-af16).
-- af13 = record_request
-- af14 = record_grant_evidence  (constant: event_type='approval_granted')
-- af15 = record_reject_evidence (constant: event_type='approval_rejected')
-- af16 = record_notification_evidence
-- Called via execute_abstract_function → call_postgres_function primitive.
-- Not via execute_db_function. Approval authority: ui_human_explicit_action_only.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af13', 'audit_approval.record_request',
     'external_port_runtime', 'audit_approval_bundle',
     '{"step1_result":"ApprovalRequestId","step2_result":"OutputProp"}',
     ARRAY['credential','approval_secret_token','raw_provider_response'], true),
    ('00000000-0000-0000-0000-00000000af14', 'audit_approval.record_grant_evidence',
     'external_port_runtime', 'audit_approval_bundle',
     '{"step1_result":"ApprovalEvidenceId","step2_result":"OutputProp"}',
     ARRAY['credential','approval_secret_token','raw_provider_response'], true),
    ('00000000-0000-0000-0000-00000000af15', 'audit_approval.record_reject_evidence',
     'external_port_runtime', 'audit_approval_bundle',
     '{"step1_result":"ApprovalEvidenceId","step2_result":"OutputProp"}',
     ARRAY['credential','approval_secret_token','raw_provider_response'], true),
    ('00000000-0000-0000-0000-00000000af16', 'audit_approval.record_notification_evidence',
     'external_port_runtime', 'audit_approval_bundle',
     '{"result":"NotificationEvidenceId"}',
     ARRAY['credential','approval_secret_token','raw_provider_response'], true)
ON CONFLICT (abstract_function_id) DO UPDATE SET output_shape = EXCLUDED.output_shape;

-- Steps for af13: step 1 = call_postgres_function, step 2 = projection.
-- Steps for af14/af15: same shape; event_type/approval_status encoded as constant
--   bindings in each manifest (not via policy step_config).
-- Step for af16: single call_postgres_function step.
INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf42', '00000000-0000-0000-0000-00000000af13', 1,
     'call_postgres_function',
     '{"function":"topology.aa_record_approval_request","required_table_authority":"topology.audit_approval_requests","arguments":["idempotency_key","approval_subject","requester","response_port_ref"]}',
     'ApprovalRequestId', true),
    ('00000000-0000-0000-0000-00000000bf43', '00000000-0000-0000-0000-00000000af13', 2,
     'projection', '{}', 'OutputProp', true),
    ('00000000-0000-0000-0000-00000000bf44', '00000000-0000-0000-0000-00000000af14', 1,
     'call_postgres_function',
     '{"function":"topology.aa_record_approval_evidence","required_table_authority":"topology.audit_approval_evidence","arguments":["approval_request_id","event_type","approval_status"]}',
     'ApprovalEvidenceId', true),
    ('00000000-0000-0000-0000-00000000bf45', '00000000-0000-0000-0000-00000000af14', 2,
     'projection', '{}', 'OutputProp', true),
    ('00000000-0000-0000-0000-00000000bf46', '00000000-0000-0000-0000-00000000af15', 1,
     'call_postgres_function',
     '{"function":"topology.aa_record_approval_evidence","required_table_authority":"topology.audit_approval_evidence","arguments":["approval_request_id","event_type","approval_status"]}',
     'ApprovalEvidenceId', true),
    ('00000000-0000-0000-0000-00000000bf47', '00000000-0000-0000-0000-00000000af15', 2,
     'projection', '{}', 'OutputProp', true),
    ('00000000-0000-0000-0000-00000000bf48', '00000000-0000-0000-0000-00000000af16', 1,
     'call_postgres_function',
     '{"function":"topology.aa_record_notification_evidence","required_table_authority":"topology.audit_notification_evidence","arguments":["approval_request_id","notification_status","response_port_ref"]}',
     'NotificationEvidenceId', true)
ON CONFLICT (abstract_function_id, step_order) DO NOTHING;

-- Input bindings for af13 (bf42-bf43).
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    -- bf42 (af13 step 1): idempotency_key/approval_subject/requester from payload;
    --   response_port_ref from external_context (optional; resolved at runtime).
    ('00000000-0000-0000-0000-00000000c053', '00000000-0000-0000-0000-00000000bf42', 'idempotency_key',   'payload',          'idempotency_key',   true,  false, true),
    ('00000000-0000-0000-0000-00000000c054', '00000000-0000-0000-0000-00000000bf42', 'approval_subject',  'payload',          'approval_subject',  true,  false, true),
    ('00000000-0000-0000-0000-00000000c055', '00000000-0000-0000-0000-00000000bf42', 'requester',         'payload',          'requester',         true,  false, true),
    ('00000000-0000-0000-0000-00000000c056', '00000000-0000-0000-0000-00000000bf42', 'response_port_ref', 'external_context', 'response_port_ref', false, false, true),
    -- bf43 (af13 step 2 projection): approval_request_id from result_context → OutputProp.
    ('00000000-0000-0000-0000-00000000c057', '00000000-0000-0000-0000-00000000bf43', 'approval_request_id', 'result_context', 'ApprovalRequestId', true, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

-- Input bindings for af14 (bf44-bf45): grant evidence.
-- event_type and approval_status are constant bindings specific to this manifest.
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c058', '00000000-0000-0000-0000-00000000bf44', 'approval_request_id', 'payload',  'approval_request_id', true,  false, true),
    ('00000000-0000-0000-0000-00000000c059', '00000000-0000-0000-0000-00000000bf44', 'event_type',          'constant', 'approval_granted',    true,  false, true),
    ('00000000-0000-0000-0000-00000000c05a', '00000000-0000-0000-0000-00000000bf44', 'approval_status',     'constant', 'granted',             true,  false, true),
    -- bf45 (af14 step 2 projection)
    ('00000000-0000-0000-0000-00000000c05b', '00000000-0000-0000-0000-00000000bf45', 'approval_evidence_id', 'result_context', 'ApprovalEvidenceId', true, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

-- Input bindings for af15 (bf46-bf47): reject evidence.
-- event_type and approval_status are constant bindings specific to this manifest.
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c05c', '00000000-0000-0000-0000-00000000bf46', 'approval_request_id', 'payload',  'approval_request_id', true,  false, true),
    ('00000000-0000-0000-0000-00000000c05d', '00000000-0000-0000-0000-00000000bf46', 'event_type',          'constant', 'approval_rejected',   true,  false, true),
    ('00000000-0000-0000-0000-00000000c05e', '00000000-0000-0000-0000-00000000bf46', 'approval_status',     'constant', 'rejected',            true,  false, true),
    -- bf47 (af15 step 2 projection)
    ('00000000-0000-0000-0000-00000000c05f', '00000000-0000-0000-0000-00000000bf47', 'approval_evidence_id', 'result_context', 'ApprovalEvidenceId', true, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

-- Input bindings for af16 (bf48): notification evidence.
-- IDs c063-c065 (c060-c062 reserved for scheduler tables in other seeds).
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c063', '00000000-0000-0000-0000-00000000bf48', 'approval_request_id', 'payload',          'approval_request_id', true,  false, true),
    ('00000000-0000-0000-0000-00000000c064', '00000000-0000-0000-0000-00000000bf48', 'notification_status', 'external_context', 'notification_status', true,  false, true),
    ('00000000-0000-0000-0000-00000000c065', '00000000-0000-0000-0000-00000000bf48', 'response_port_ref',   'external_context', 'response_port_ref',   false, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    -- af13: called from policy ea (audit_approval_response_port_generic)
    ('00000000-0000-0000-0000-00000000af13', 'policy', 'audit_approval_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af13', 'table',  'topology.audit_approval_requests',     true),
    -- af14: called from grant policy (f0)
    ('00000000-0000-0000-0000-00000000af14', 'policy', 'audit_approval_grant_action_generic',  true),
    ('00000000-0000-0000-0000-00000000af14', 'table',  'topology.audit_approval_evidence',     true),
    -- af15: called from reject policy (f1)
    ('00000000-0000-0000-0000-00000000af15', 'policy', 'audit_approval_reject_action_generic', true),
    ('00000000-0000-0000-0000-00000000af15', 'table',  'topology.audit_approval_evidence',     true),
    -- af16: called from policy ea (response_port lane, after notification send)
    ('00000000-0000-0000-0000-00000000af16', 'policy', 'audit_approval_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af16', 'table',  'topology.audit_notification_evidence', true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO NOTHING;

-- ---------------------------------------------------------------------------
-- New response ports for grant and reject domain actions.
-- credential_kind='none': domain action only, no external HTTP call.
-- required_by_bundle disambiguates policy resolution from f07 (notification port).
-- ---------------------------------------------------------------------------
INSERT INTO topology.external_response_ports
    (response_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f0d', 'audit_approval_grant_action',  'domain_action', 'env:APPROVAL_DOMAIN_ACTION_SENTINEL', 'none', NULL, true),
    ('00000000-0000-0000-0000-000000000f0e', 'audit_approval_reject_action', 'domain_action', 'env:APPROVAL_DOMAIN_ACTION_SENTINEL', 'none', NULL, true)
ON CONFLICT (response_port_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- New policies for grant and reject domain actions.
-- required_by_bundle matches f0d/f0e response ports for canonical resolution.
-- ---------------------------------------------------------------------------
INSERT INTO topology.external_port_policies
    (policy_id, policy_key, port_kind, required_by_bundle, active)
VALUES
    ('00000000-0000-0000-0000-0000000000f0', 'audit_approval_grant_action_generic',  'response_port', 'audit_approval_grant_action',  true),
    ('00000000-0000-0000-0000-0000000000f1', 'audit_approval_reject_action_generic', 'response_port', 'audit_approval_reject_action', true)
ON CONFLICT (policy_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Update topology_manifest a7 (audit_approval.response_port.evidence.projection)
-- to include screen_data_shape binding for approval_request / approval_evidence /
-- notification_evidence tables. This satisfies the SSOT requirement for
-- physical_table_manifest_binding / audit_approval manifest / screen_data_shape
-- projection connection beyond the UI preset seed alone.
-- forbiddenProjectionFields blocks credential / approval_secret_token /
-- raw_provider_response from any SSE/projection output.
-- ---------------------------------------------------------------------------
UPDATE hubs.topology_manifests
SET topology_jsonb = topology_jsonb || jsonb_build_object(
    'screen_data_shape', '{
        "type": "screen_data_shape",
        "topologySystemName": "audit-approval-form-seed",
        "userFacingTopologyLabel": "Audit Approval",
        "tableRef": "topology.audit_approval_requests",
        "dbTableName": "topology.audit_approval_requests",
        "displayColumnMode": "selected",
        "forbiddenProjectionFields": ["credential", "approval_secret_token", "raw_provider_response"],
        "displayColumns": [
            "audit_approval_requests.idempotency_key",
            "audit_approval_requests.request_status",
            "audit_approval_requests.created_at",
            "audit_approval_requests.updated_at",
            "audit_approval_evidence.event_type",
            "audit_approval_evidence.approval_status",
            "audit_notification_evidence.notification_status"
        ],
        "logicalTables": [
            {"tableName": "audit_approval_requests", "columns": [
                {"name": "approval_request_id", "dataType": "uuid", "nullable": false},
                {"name": "idempotency_key", "dataType": "text", "nullable": true},
                {"name": "request_status", "dataType": "text", "nullable": false},
                {"name": "response_port_ref", "dataType": "text", "nullable": true},
                {"name": "evidence_json", "dataType": "jsonb", "nullable": true},
                {"name": "created_at", "dataType": "timestamptz", "nullable": false},
                {"name": "updated_at", "dataType": "timestamptz", "nullable": false}
            ]},
            {"tableName": "audit_approval_evidence", "columns": [
                {"name": "approval_evidence_id", "dataType": "uuid", "nullable": false},
                {"name": "approval_request_id", "dataType": "uuid", "nullable": false},
                {"name": "event_type", "dataType": "text", "nullable": false},
                {"name": "approval_status", "dataType": "text", "nullable": false},
                {"name": "evidence_json", "dataType": "jsonb", "nullable": true},
                {"name": "created_at", "dataType": "timestamptz", "nullable": false}
            ]},
            {"tableName": "audit_notification_evidence", "columns": [
                {"name": "notification_evidence_id", "dataType": "uuid", "nullable": false},
                {"name": "approval_request_id", "dataType": "uuid", "nullable": false},
                {"name": "notification_status", "dataType": "text", "nullable": false},
                {"name": "response_port_ref", "dataType": "text", "nullable": true},
                {"name": "created_at", "dataType": "timestamptz", "nullable": false}
            ]}
        ],
        "relationIntents": [
            {"localTableRef": "audit_approval_requests", "joinTableRef": "audit_approval_evidence",
             "localKey": "approval_request_id", "remoteKey": "approval_request_id"},
            {"localTableRef": "audit_approval_requests", "joinTableRef": "audit_notification_evidence",
             "localKey": "approval_request_id", "remoteKey": "approval_request_id"}
        ],
        "operationEntityBindings": [
            {"operationKind": "approval_requested",  "function": "topology.aa_record_approval_request",      "entityTargetColumns": ["idempotency_key", "approval_subject", "requester", "response_port_ref"]},
            {"operationKind": "approval_granted",    "function": "topology.aa_record_approval_evidence",     "entityTargetColumns": ["approval_request_id", "event_type", "approval_status"]},
            {"operationKind": "approval_rejected",   "function": "topology.aa_record_approval_evidence",     "entityTargetColumns": ["approval_request_id", "event_type", "approval_status"]},
            {"operationKind": "notification_sent",   "function": "topology.aa_record_notification_evidence", "entityTargetColumns": ["approval_request_id", "notification_status", "response_port_ref"]}
        ]
    }'::jsonb
),
    updated_at = now()
WHERE topology_manifest_id = '00000000-0000-0000-0000-0000000000a7';

-- ---------------------------------------------------------------------------
-- Re-seed policy ea (audit_approval_response_port_generic): expand 9 → 12 steps.
-- Inserts before notification send:
--   step 3: execute_abstract_function → af13 (record_request)
--   step 4: append_runtime_event_log  → approval_requested
-- Inserts after capture_response:
--   step 11: execute_abstract_function → af16 (record_notification_evidence)
-- Preserves compat abstract_function_key values for steps 463/464/465 that were
-- set by external_port_compat_absorption_seed.sql (which ran before this file).
-- ---------------------------------------------------------------------------
DELETE FROM topology.external_port_policy_steps
WHERE policy_id = '00000000-0000-0000-0000-0000000000ea';

INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000461', '00000000-0000-0000-0000-0000000000ea',  1, 'resolve_port_record',               '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-000000000462', '00000000-0000-0000-0000-0000000000ea',  2, 'resolve_credential_reference',      '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-00000000046a', '00000000-0000-0000-0000-0000000000ea',  3, 'execute_abstract_function',         '{}',                'audit_approval.record_request',               true),
    ('00000000-0000-0000-0000-00000000046b', '00000000-0000-0000-0000-0000000000ea',  4, 'append_runtime_event_log',          '{"event_type":"approval_requested","evidence_table_ref":"topology.audit_approval_evidence","projection_table_ref":"topology.audit_approval_evidence","status_value":"requested"}', NULL, true),
    ('00000000-0000-0000-0000-000000000463', '00000000-0000-0000-0000-0000000000ea',  5, 'load_encrypted_credential_payload', '{}',                'external_port.compat_build.audit_approval',   true),
    ('00000000-0000-0000-0000-000000000464', '00000000-0000-0000-0000-0000000000ea',  6, 'decrypt_for_runtime_use',           '{}',                'external_port.http.audit_approval_post',      true),
    ('00000000-0000-0000-0000-000000000465', '00000000-0000-0000-0000-0000000000ea',  7, 'build_http_request',                '{"method":"POST"}', 'external_port.event.audit_approval_sent',     true),
    ('00000000-0000-0000-0000-000000000466', '00000000-0000-0000-0000-0000000000ea',  8, 'inject_authorization_header',       '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-000000000467', '00000000-0000-0000-0000-0000000000ea',  9, 'send_http',                         '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-000000000468', '00000000-0000-0000-0000-0000000000ea', 10, 'capture_response',                  '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-00000000046c', '00000000-0000-0000-0000-0000000000ea', 11, 'execute_abstract_function',         '{}',                'audit_approval.record_notification_evidence', true),
    ('00000000-0000-0000-0000-000000000469', '00000000-0000-0000-0000-0000000000ea', 12, 'append_runtime_event_log',          '{"event_type":"approval_reviewed","evidence_table_ref":"topology.audit_approval_evidence","projection_table_ref":"topology.audit_approval_evidence","status_value":"reviewed"}', NULL, true)
ON CONFLICT (policy_id, step_order) DO UPDATE
    SET operation_key         = EXCLUDED.operation_key,
        step_config           = EXCLUDED.step_config,
        abstract_function_key = EXCLUDED.abstract_function_key,
        active                = EXCLUDED.active;

-- Policy steps for grant (f0) and reject (f1) domain-action policies.
-- execute_abstract_function references the manifest-specific function key.
-- Grant/reject semantics are encoded in constant input_bindings (not step_config).
INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    -- audit_approval_grant_action_generic (f0): 3 steps
    ('00000000-0000-0000-0000-0000000004f1', '00000000-0000-0000-0000-0000000000f0', 1,
     'resolve_port_record', '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004f2', '00000000-0000-0000-0000-0000000000f0', 2,
     'execute_abstract_function', '{}', 'audit_approval.record_grant_evidence', true),
    ('00000000-0000-0000-0000-0000000004f3', '00000000-0000-0000-0000-0000000000f0', 3,
     'append_runtime_event_log',
     '{"event_type":"approval_granted","evidence_table_ref":"topology.audit_approval_evidence","projection_table_ref":"topology.audit_approval_evidence","status_value":"granted"}',
     NULL, true),
    -- audit_approval_reject_action_generic (f1): 3 steps
    ('00000000-0000-0000-0000-0000000004f4', '00000000-0000-0000-0000-0000000000f1', 1,
     'resolve_port_record', '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004f5', '00000000-0000-0000-0000-0000000000f1', 2,
     'execute_abstract_function', '{}', 'audit_approval.record_reject_evidence', true),
    ('00000000-0000-0000-0000-0000000004f6', '00000000-0000-0000-0000-0000000000f1', 3,
     'append_runtime_event_log',
     '{"event_type":"approval_rejected","evidence_table_ref":"topology.audit_approval_evidence","projection_table_ref":"topology.audit_approval_evidence","status_value":"rejected"}',
     NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;

-- =============================================================================
-- UI Builder preset: audit_approval_approval_form.v1
-- Derived from physical_search_crud_aggregate.v1.
-- Three portTargetRef wiring candidates:
--   submit_request_button → f07 (response_port: notification + record_request)
--   approve_button        → f0d (response_port: domain_action grant)
--   reject_button         → f0e (response_port: domain_action reject)
-- No notification provider-specific client or credential plane is introduced.
-- Projection response: approval_request_id, approval_evidence_id (opaque ref).
-- Forbidden projection fields: credential, approval_secret_token, raw_provider_response.
-- =============================================================================
WITH upserted_preset AS (
    INSERT INTO topology.mock_preset_registry (
        preset_key, preset_label, source_kind, source_hash,
        source_snapshot_json, visual_tree_json, status
    )
    VALUES (
        'audit_approval_approval_form.v1',
        'Audit Approval form preset seed',
        'ui_builder_canvas',
        'audit_approval_approval_form.v1.seed.2026-06-24.r1',
        $$
        {
          "seedKind": "ui_builder_canvas_preset_seed",
          "presetKey": "audit_approval_approval_form.v1",
          "bundle": "audit-approval-port-consumer",
          "surface": "approval_request_and_evidence",
          "derivedFrom": "physical_search_crud_aggregate.v1",
          "componentImplementation": "none; existing CRUD/component catalog composition only",
          "runtimeMutationAuthority": "external port substrate via portTargetRef and execute_abstract_function policy steps (manifests af13-af16)",
          "credentialPlane": "external_port_substrate reference_key resolution only",
          "approvalAuthority": "ui_human_explicit_action_only",
          "forbiddenProjectionFields": ["credential", "approval_secret_token", "raw_provider_response"],
          "payloadFromResolver": "frontend/runtime/payloadFromResolver.ts",
          "projectionResponseFields": ["approval_request_id", "approval_evidence_id"],
          "note": "Three actions: submit (→f07 notification response_port records request + sends notification), approve (→f0d grant domain_action), reject (→f0e reject domain_action). AI autonomous approval, implicit approval, and CLI/MCP approval trigger are prohibited."
        }
        $$::jsonb,
        $$
        {"nodes":[
          {"nodeId":"approval_shell","componentKey":"section.alias","componentKind":"disclosure_structure/section","parentNodeId":null},
          {"nodeId":"idempotency_key_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"approval_shell"},
          {"nodeId":"approval_subject_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"approval_shell"},
          {"nodeId":"requester_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"approval_shell"},
          {"nodeId":"approval_request_id_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"approval_shell"},
          {"nodeId":"submit_request_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"approval_shell"},
          {"nodeId":"approve_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"approval_shell"},
          {"nodeId":"reject_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"approval_shell"},
          {"nodeId":"approval_result_json","componentKey":"json_viewer.template","componentKind":"data_display/json","parentNodeId":"approval_shell"}
        ]}
        $$::jsonb,
        'active'
    )
    ON CONFLICT (preset_key) DO UPDATE SET
        preset_label        = EXCLUDED.preset_label,
        source_kind         = EXCLUDED.source_kind,
        source_hash         = EXCLUDED.source_hash,
        source_snapshot_json = EXCLUDED.source_snapshot_json,
        visual_tree_json    = EXCLUDED.visual_tree_json,
        status              = EXCLUDED.status,
        updated_at          = now()
    RETURNING preset_id
), preset AS (
    SELECT preset_id FROM upserted_preset
), cleanup_compile AS (
    DELETE FROM topology.mock_preset_compile_snapshot WHERE preset_id = (SELECT preset_id FROM preset)
), cleanup_wiring AS (
    DELETE FROM topology.mock_preset_wiring_candidate WHERE preset_id = (SELECT preset_id FROM preset)
), cleanup_mapping AS (
    DELETE FROM topology.mock_preset_object_mapping WHERE preset_id = (SELECT preset_id FROM preset)
), inserted_mappings AS (
    INSERT INTO topology.mock_preset_object_mapping (
        preset_id, source_object_id, node_id, node_kind, component_key, component_kind,
        parent_source_object_id, slot_key, order_index, bbox_json, text_json,
        style_candidate_json, mapping_status
    )
    VALUES
        ((SELECT preset_id FROM preset), 'approval_shell', 'approval_shell', 'catalog_component', 'section.alias', 'disclosure_structure/section', NULL, 'root', 0,
         '{"x":0,"y":0,"width":1024,"height":900}'::jsonb,
         '{"title":"Approval Request","description":"Submit an approval request, then approve or reject via the UI. Approval authority is UI/human explicit action only. Configure portTargetRef payload mapping before apply."}'::jsonb,
         '{"layoutIntent":"crud_derived_shell"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'idempotency_key_input', 'idempotency_key_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'approval_shell', 'fields', 1,
         '{"x":24,"y":72,"width":456,"height":64}'::jsonb,
         '{"label":"Request ID (idempotency key)","required":true}'::jsonb,
         '{"layoutIntent":"idempotency_key_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'approval_subject_input', 'approval_subject_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'approval_shell', 'fields', 2,
         '{"x":496,"y":72,"width":456,"height":64}'::jsonb,
         '{"label":"Approval subject","required":true}'::jsonb,
         '{"layoutIntent":"approval_subject_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'requester_input', 'requester_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'approval_shell', 'fields', 3,
         '{"x":24,"y":152,"width":456,"height":64}'::jsonb,
         '{"label":"Requested by","required":true}'::jsonb,
         '{"layoutIntent":"requester_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'approval_request_id_input', 'approval_request_id_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'approval_shell', 'fields', 4,
         '{"x":496,"y":152,"width":456,"height":64}'::jsonb,
         '{"label":"Approval request ID (for approve/reject)","required":false,"hint":"Populated from submit result or entered manually"}'::jsonb,
         '{"layoutIntent":"approval_request_id_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'submit_request_button', 'submit_request_button', 'catalog_component', 'button.primitive', 'action/button', 'approval_shell', 'actions', 5,
         '{"x":24,"y":232,"width":200,"height":48}'::jsonb,
         '{"label":"Submit request","variant":"primary"}'::jsonb,
         '{"layoutIntent":"external_port_dispatch"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'approve_button', 'approve_button', 'catalog_component', 'button.primitive', 'action/button', 'approval_shell', 'actions', 6,
         '{"x":240,"y":232,"width":180,"height":48}'::jsonb,
         '{"label":"Approve","variant":"success"}'::jsonb,
         '{"layoutIntent":"external_port_dispatch"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'reject_button', 'reject_button', 'catalog_component', 'button.primitive', 'action/button', 'approval_shell', 'actions', 7,
         '{"x":436,"y":232,"width":180,"height":48}'::jsonb,
         '{"label":"Reject","variant":"danger"}'::jsonb,
         '{"layoutIntent":"external_port_dispatch"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'approval_result_json', 'approval_result_json', 'catalog_component', 'json_viewer.template', 'data_display/json', 'approval_shell', 'results', 8,
         '{"x":24,"y":296,"width":928,"height":568}'::jsonb,
         '{"title":"Approval result"}'::jsonb,
         '{"layoutIntent":"result_projection"}'::jsonb, 'mapped')
    RETURNING mapping_id
), inserted_wiring AS (
    INSERT INTO topology.mock_preset_wiring_candidate (
        preset_id, source_object_id, node_id, capability_tag,
        wiring_kind, target_surface, target_ref, binding_json, status
    )
    VALUES
        -- submit_request_button: dispatches to f07 (notification response_port).
        -- policy ea records the approval request (af13) then sends notification.
        ((SELECT preset_id FROM preset), 'submit_request_button', 'submit_request_button',
         'requires_external_port_audit_approval_request', 'dispatchExternalPort', 'external_port',
         'external-port:response_port:00000000-0000-0000-0000-000000000f07',
         $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f07","payloadFrom":{"idempotency_key":"node:idempotency_key_input.value","approval_subject":"node:approval_subject_input.value","requester":"node:requester_input.value"},"outputProp":"approvalRequestResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Dispatches to audit_approval notification response_port. Policy ea records approval_request (af13) then sends notification. Approval authority is UI/human explicit action only. No credential plaintext in response."}$$::jsonb,
         'pending'),
        -- approve_button: dispatches to f0d (grant domain_action response_port).
        -- policy f0 records grant evidence (af14: constant approval_granted) + event log.
        ((SELECT preset_id FROM preset), 'approve_button', 'approve_button',
         'requires_external_port_audit_approval_grant', 'dispatchExternalPort', 'external_port',
         'external-port:response_port:00000000-0000-0000-0000-000000000f0d',
         $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0d","payloadFrom":{"approval_request_id":"node:approval_request_id_input.value"},"outputProp":"approvalGrantResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Dispatches to audit_approval_grant_action response_port. Policy f0 records grant evidence via af14 (constant: approval_granted). Approval authority is UI/human explicit action only."}$$::jsonb,
         'pending'),
        -- reject_button: dispatches to f0e (reject domain_action response_port).
        -- policy f1 records reject evidence (af15: constant approval_rejected) + event log.
        ((SELECT preset_id FROM preset), 'reject_button', 'reject_button',
         'requires_external_port_audit_approval_reject', 'dispatchExternalPort', 'external_port',
         'external-port:response_port:00000000-0000-0000-0000-000000000f0e',
         $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0e","payloadFrom":{"approval_request_id":"node:approval_request_id_input.value"},"outputProp":"approvalRejectResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Dispatches to audit_approval_reject_action response_port. Policy f1 records reject evidence via af15 (constant: approval_rejected). Approval authority is UI/human explicit action only."}$$::jsonb,
         'pending')
    RETURNING wiring_candidate_id
)
INSERT INTO topology.mock_preset_compile_snapshot (
    preset_id, compiler_version, layout_patch_json,
    package_membership_candidate_json, wiring_candidate_json,
    style_candidate_json, unresolved_json
)
VALUES (
    (SELECT preset_id FROM preset),
    'audit-approval-form-seed.v1',
    $${"nodes":[
      {"nodeId":"approval_shell","nodeKind":"catalog_component","componentKey":"section.alias","componentKind":"disclosure_structure/section","isDraftOnly":false,"slotKey":"root","orderIndex":0,"parentNodeId":null,"x":0,"y":0,"width":1024,"height":900,"propsJson":"{\"title\":\"Approval Request\",\"description\":\"Submit, approve, or reject via UI. Approval authority is UI/human explicit action only. Configure portTargetRef payload mapping before apply.\"}"},
      {"nodeId":"idempotency_key_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":1,"parentNodeId":"approval_shell","x":24,"y":72,"width":456,"height":64,"propsJson":"{\"label\":\"Request ID (idempotency key)\",\"required\":true}"},
      {"nodeId":"approval_subject_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":2,"parentNodeId":"approval_shell","x":496,"y":72,"width":456,"height":64,"propsJson":"{\"label\":\"Approval subject\",\"required\":true}"},
      {"nodeId":"requester_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":3,"parentNodeId":"approval_shell","x":24,"y":152,"width":456,"height":64,"propsJson":"{\"label\":\"Requested by\",\"required\":true}"},
      {"nodeId":"approval_request_id_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":4,"parentNodeId":"approval_shell","x":496,"y":152,"width":456,"height":64,"propsJson":"{\"label\":\"Approval request ID (for approve/reject)\",\"required\":false,\"hint\":\"Populated from submit result or entered manually\"}"},
      {"nodeId":"submit_request_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":5,"parentNodeId":"approval_shell","x":24,"y":232,"width":200,"height":48,"propsJson":"{\"label\":\"Submit request\",\"variant\":\"primary\"}"},
      {"nodeId":"approve_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":6,"parentNodeId":"approval_shell","x":240,"y":232,"width":180,"height":48,"propsJson":"{\"label\":\"Approve\",\"variant\":\"success\"}"},
      {"nodeId":"reject_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":7,"parentNodeId":"approval_shell","x":436,"y":232,"width":180,"height":48,"propsJson":"{\"label\":\"Reject\",\"variant\":\"danger\"}"},
      {"nodeId":"approval_result_json","nodeKind":"catalog_component","componentKey":"json_viewer.template","componentKind":"data_display/json","isDraftOnly":false,"slotKey":"results","orderIndex":8,"parentNodeId":"approval_shell","x":24,"y":296,"width":928,"height":568,"propsJson":"{\"title\":\"Approval result\"}","propBindings":{"data":{"source":"emission.data.approvalRequestResult"}}}
    ],"layoutClassRefs":[]}$$::jsonb,
    $${"activeTopologyWrite":false,"bindTarget":"selected_route_package_tmp_canvas_draft","derivedFrom":"physical_search_crud_aggregate.v1","requiresHumanAdjustmentBeforeApply":true}$$::jsonb,
    $$
    [
      {"nodeId":"submit_request_button","sourceObjectId":"submit_request_button","capabilityTag":"requires_external_port_audit_approval_request","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f07","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f07","payloadFrom":{"idempotency_key":"node:idempotency_key_input.value","approval_subject":"node:approval_subject_input.value","requester":"node:requester_input.value"},"outputProp":"approvalRequestResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}},
      {"nodeId":"approve_button","sourceObjectId":"approve_button","capabilityTag":"requires_external_port_audit_approval_grant","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0d","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0d","payloadFrom":{"approval_request_id":"node:approval_request_id_input.value"},"outputProp":"approvalGrantResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}},
      {"nodeId":"reject_button","sourceObjectId":"reject_button","capabilityTag":"requires_external_port_audit_approval_reject","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0e","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0e","payloadFrom":{"approval_request_id":"node:approval_request_id_input.value"},"outputProp":"approvalRejectResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}}
    ]
    $$::jsonb,
    $$[{"nodeId":"approval_shell","styleIntent":"crud_derived_approval_shell"},{"nodeId":"approval_result_json","styleIntent":"result_projection_panel"}]$$::jsonb,
    $$[]$$::jsonb
);

-- ── physical_table_manifest_bindings ──────────────────────────────────────────
-- Bind the three audit_approval physical tables to manifest a7 explicitly in
-- this domain seed file. topology.physical_tables rows are seeded in
-- seed_empty.sql; this upsert is the canonical audit_approval bundle evidence
-- for the physical_table_manifest_binding → screen_data_shape connection.
INSERT INTO topology.physical_table_manifest_bindings
    (physical_table_id, topology_manifest_id, active, binding_evidence_json)
SELECT pt.physical_table_id,
       '00000000-0000-0000-0000-0000000000a7'::uuid,
       true,
       jsonb_build_object(
           'source', 'external-port-consumer-completion',
           'bundle', pt.category,
           'uiBuilderPreset', 'physical_search_crud_aggregate.v1',
           'portTargetRefLane', true,
           'credentialProjection', 'reference_only')
FROM topology.physical_tables pt
WHERE pt.table_ref IN (
    'topology.audit_approval_requests',
    'topology.audit_approval_evidence',
    'topology.audit_notification_evidence')
ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE
    SET active = true,
        binding_evidence_json = EXCLUDED.binding_evidence_json,
        updated_at = now();
