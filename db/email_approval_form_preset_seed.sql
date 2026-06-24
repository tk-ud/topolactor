-- =============================================================================
-- email_approval_form_preset_seed.sql
-- email_bundle domain mutations and UI Builder preset.
-- Implements draft / approval / dispatch / delivery_evidence through the existing
-- generic external_port substrate lane (response_port → credential resolution →
-- send_http/capture_response → evidence/runtime_event_log/projection).
-- Email send authority is ui_approval_then_backend_dispatch per SSOT:
--   draft → preview → explicit human approval → backend dispatch (send) → delivery_log.
-- Prohibited: SMTP provider-specific client/runtime, AI autonomous send,
--             implicit approval, CLI/MCP send, silent send failure fallback,
--             silent retry, SMTP host/port/api key/raw provider response in
--             DB/UI/projection/log.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Abstract function manifests for email domain mutations (af40-af43).
-- af40 = record_draft
-- af41 = record_approval            (constant: approval_status='approved')
-- af42 = record_dispatch_initiated  (approval gate + double-send guard before send)
-- af43 = record_send_evidence       (send_result from external_context → success/failure)
-- Called via execute_abstract_function → call_postgres_function primitive.
-- Not via execute_db_function. Send authority: ui_approval_then_backend_dispatch.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af40', 'email.record_draft',
     'external_port_runtime', 'email_bundle',
     '{"step1_result":"EmailDraftId","step2_result":"OutputProp"}',
     ARRAY['credential','smtp_host','smtp_port','smtp_api_key','sender_credential','raw_provider_response','plaintext_payload','token'], true),
    ('00000000-0000-0000-0000-00000000af41', 'email.record_approval',
     'external_port_runtime', 'email_bundle',
     '{"step1_result":"ApprovalRecordId","step2_result":"OutputProp"}',
     ARRAY['credential','smtp_host','smtp_port','smtp_api_key','sender_credential','raw_provider_response','plaintext_payload','token'], true),
    ('00000000-0000-0000-0000-00000000af42', 'email.record_dispatch_initiated',
     'external_port_runtime', 'email_bundle',
     '{"result":"DispatchInitiatedId"}',
     ARRAY['credential','smtp_host','smtp_port','smtp_api_key','sender_credential','raw_provider_response','plaintext_payload','token'], true),
    ('00000000-0000-0000-0000-00000000af43', 'email.record_send_evidence',
     'external_port_runtime', 'email_bundle',
     '{"result":"SendEvidenceId"}',
     ARRAY['credential','smtp_host','smtp_port','smtp_api_key','sender_credential','raw_provider_response','plaintext_payload','token'], true)
ON CONFLICT (abstract_function_id) DO UPDATE SET output_shape = EXCLUDED.output_shape;

-- Steps for af40/af41: step 1 = call_postgres_function, step 2 = projection.
-- Steps for af42/af43: single call_postgres_function step (no projection — invoked
--   inside the response_port send lane, result stored to result_context only).
INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf57', '00000000-0000-0000-0000-00000000af40', 1,
     'call_postgres_function',
     '{"function":"topology.em_record_draft","required_table_authority":"topology.email_drafts","arguments":["dispatch_idempotency_key","recipient_ref","subject_ref","response_port_ref"]}',
     'EmailDraftId', true),
    ('00000000-0000-0000-0000-00000000bf58', '00000000-0000-0000-0000-00000000af40', 2,
     'projection', '{}', 'OutputProp', true),
    ('00000000-0000-0000-0000-00000000bf59', '00000000-0000-0000-0000-00000000af41', 1,
     'call_postgres_function',
     '{"function":"topology.em_record_approval","required_table_authority":"topology.email_approval_records","arguments":["email_draft_id","approval_status","reviewed_by"]}',
     'ApprovalRecordId', true),
    ('00000000-0000-0000-0000-00000000bf5a', '00000000-0000-0000-0000-00000000af41', 2,
     'projection', '{}', 'OutputProp', true),
    ('00000000-0000-0000-0000-00000000bf5b', '00000000-0000-0000-0000-00000000af42', 1,
     'call_postgres_function',
     '{"function":"topology.em_record_dispatch_initiated","required_table_authority":"topology.email_drafts","arguments":["email_draft_id"]}',
     'DispatchInitiatedId', true),
    ('00000000-0000-0000-0000-00000000bf5c', '00000000-0000-0000-0000-00000000af43', 1,
     'call_postgres_function',
     '{"function":"topology.em_record_send_evidence","required_table_authority":"topology.email_delivery_evidence","arguments":["email_draft_id","send_result"]}',
     'SendEvidenceId', true)
ON CONFLICT (abstract_function_id, step_order) DO NOTHING;

-- Input bindings for af40 (bf57-bf58): record_draft.
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c081', '00000000-0000-0000-0000-00000000bf57', 'dispatch_idempotency_key', 'payload',          'dispatch_idempotency_key', true,  false, true),
    ('00000000-0000-0000-0000-00000000c082', '00000000-0000-0000-0000-00000000bf57', 'recipient_ref',            'payload',          'recipient_ref',            true,  false, true),
    ('00000000-0000-0000-0000-00000000c083', '00000000-0000-0000-0000-00000000bf57', 'subject_ref',              'payload',          'subject_ref',              true,  false, true),
    ('00000000-0000-0000-0000-00000000c084', '00000000-0000-0000-0000-00000000bf57', 'response_port_ref',        'external_context', 'response_port_ref',        false, false, true),
    -- bf58 (af40 step 2 projection): email_draft_id from result_context → OutputProp.
    ('00000000-0000-0000-0000-00000000c085', '00000000-0000-0000-0000-00000000bf58', 'email_draft_id',           'result_context',   'EmailDraftId',             true,  false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

-- Input bindings for af41 (bf59-bf5a): record_approval.
-- approval_status='approved' is a constant binding specific to this manifest;
-- approval authority is UI/human explicit action only.
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c086', '00000000-0000-0000-0000-00000000bf59', 'email_draft_id',  'payload',  'email_draft_id', true,  false, true),
    ('00000000-0000-0000-0000-00000000c087', '00000000-0000-0000-0000-00000000bf59', 'approval_status', 'constant', 'approved',       true,  false, true),
    ('00000000-0000-0000-0000-00000000c088', '00000000-0000-0000-0000-00000000bf59', 'reviewed_by',     'payload',  'reviewed_by',    false, false, true),
    -- bf5a (af41 step 2 projection)
    ('00000000-0000-0000-0000-00000000c089', '00000000-0000-0000-0000-00000000bf5a', 'approval_record_id', 'result_context', 'ApprovalRecordId', true, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

-- Input bindings for af42 (bf5b): record_dispatch_initiated (approval gate guard).
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c08a', '00000000-0000-0000-0000-00000000bf5b', 'email_draft_id', 'payload', 'email_draft_id', true, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

-- Input bindings for af43 (bf5c): record_send_evidence.
-- send_result is derived at runtime from the HTTP response captured after send_http
-- (external_context 'notification_status': 'sent' for 2xx, 'failed' otherwise).
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c08b', '00000000-0000-0000-0000-00000000bf5c', 'email_draft_id', 'payload',          'email_draft_id',     true, false, true),
    ('00000000-0000-0000-0000-00000000c08c', '00000000-0000-0000-0000-00000000bf5c', 'send_result',    'external_context', 'notification_status', true, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    -- af40: called from email_draft_action_generic policy (f2)
    ('00000000-0000-0000-0000-00000000af40', 'policy', 'email_draft_action_generic',    true),
    ('00000000-0000-0000-0000-00000000af40', 'table',  'topology.email_drafts',         true),
    -- af41: called from email_approval_action_generic policy (f3)
    ('00000000-0000-0000-0000-00000000af41', 'policy', 'email_approval_action_generic', true),
    ('00000000-0000-0000-0000-00000000af41', 'table',  'topology.email_approval_records', true),
    ('00000000-0000-0000-0000-00000000af41', 'table',  'topology.email_drafts',         true),
    -- af42: called from email_response_port_generic send lane (e6)
    ('00000000-0000-0000-0000-00000000af42', 'policy', 'email_response_port_generic',   true),
    ('00000000-0000-0000-0000-00000000af42', 'table',  'topology.email_drafts',         true),
    -- af43: called from email_response_port_generic send lane (e6)
    ('00000000-0000-0000-0000-00000000af43', 'policy', 'email_response_port_generic',   true),
    ('00000000-0000-0000-0000-00000000af43', 'table',  'topology.email_delivery_evidence', true),
    ('00000000-0000-0000-0000-00000000af43', 'table',  'topology.email_drafts',         true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO NOTHING;

-- ---------------------------------------------------------------------------
-- New response ports for draft and approval domain actions.
-- credential_kind='none': domain action only, no external HTTP call.
-- required_by_bundle disambiguates policy resolution from f03 (smtp send port).
-- ---------------------------------------------------------------------------
INSERT INTO topology.external_response_ports
    (response_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000f11', 'email_draft_action',    'domain_action', 'env:EMAIL_DOMAIN_ACTION_SENTINEL', 'none', NULL, true),
    ('00000000-0000-0000-0000-000000000f12', 'email_approval_action', 'domain_action', 'env:EMAIL_DOMAIN_ACTION_SENTINEL', 'none', NULL, true)
ON CONFLICT (response_port_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- New policies for draft and approval domain actions.
-- required_by_bundle matches f11/f12 response ports for canonical resolution.
-- ---------------------------------------------------------------------------
INSERT INTO topology.external_port_policies
    (policy_id, policy_key, port_kind, required_by_bundle, active)
VALUES
    ('00000000-0000-0000-0000-0000000000f2', 'email_draft_action_generic',    'response_port', 'email_draft_action',    true),
    ('00000000-0000-0000-0000-0000000000f3', 'email_approval_action_generic', 'response_port', 'email_approval_action', true)
ON CONFLICT (policy_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Update topology_manifest a3 (email.response_port.approval.delivery.projection)
-- to include screen_data_shape binding for email_drafts / email_approval_records /
-- email_delivery_evidence tables. This satisfies the SSOT requirement for
-- physical_table_manifest_binding / email manifest / screen_data_shape projection
-- connection beyond the UI preset seed alone.
-- forbiddenProjectionFields blocks credential / smtp host/port/api key /
-- raw_provider_response / plaintext payload / token from any SSE/projection output.
-- ---------------------------------------------------------------------------
UPDATE hubs.topology_manifests
SET topology_jsonb = topology_jsonb || jsonb_build_object(
    'screen_data_shape', '{
        "type": "screen_data_shape",
        "topologySystemName": "email-approval-form-seed",
        "userFacingTopologyLabel": "Email Approval & Delivery",
        "tableRef": "topology.email_drafts",
        "dbTableName": "topology.email_drafts",
        "displayColumnMode": "selected",
        "forbiddenProjectionFields": ["credential", "smtp_host", "smtp_port", "smtp_api_key", "sender_credential", "raw_provider_response", "plaintext_payload", "token"],
        "displayColumns": [
            "email_drafts.dispatch_idempotency_key",
            "email_drafts.recipient_ref",
            "email_drafts.subject_ref",
            "email_drafts.delivery_status",
            "email_drafts.created_at",
            "email_drafts.updated_at",
            "email_approval_records.approval_status",
            "email_approval_records.reviewed_by",
            "email_delivery_evidence.event_type",
            "email_delivery_evidence.delivery_status"
        ],
        "logicalTables": [
            {"tableName": "email_drafts", "columns": [
                {"name": "email_draft_id", "dataType": "uuid", "nullable": false},
                {"name": "dispatch_idempotency_key", "dataType": "text", "nullable": true},
                {"name": "recipient_ref", "dataType": "text", "nullable": true},
                {"name": "subject_ref", "dataType": "text", "nullable": true},
                {"name": "approval_record_id", "dataType": "uuid", "nullable": true},
                {"name": "delivery_status", "dataType": "text", "nullable": false},
                {"name": "response_port_ref", "dataType": "text", "nullable": true},
                {"name": "created_at", "dataType": "timestamptz", "nullable": false},
                {"name": "updated_at", "dataType": "timestamptz", "nullable": false}
            ]},
            {"tableName": "email_approval_records", "columns": [
                {"name": "approval_record_id", "dataType": "uuid", "nullable": false},
                {"name": "email_draft_id", "dataType": "uuid", "nullable": false},
                {"name": "approval_status", "dataType": "text", "nullable": false},
                {"name": "reviewed_by", "dataType": "text", "nullable": true},
                {"name": "reviewed_at", "dataType": "timestamptz", "nullable": true}
            ]},
            {"tableName": "email_delivery_evidence", "columns": [
                {"name": "delivery_evidence_id", "dataType": "uuid", "nullable": false},
                {"name": "email_draft_id", "dataType": "uuid", "nullable": false},
                {"name": "event_type", "dataType": "text", "nullable": false},
                {"name": "delivery_status", "dataType": "text", "nullable": false},
                {"name": "recorded_at", "dataType": "timestamptz", "nullable": false}
            ]}
        ],
        "relationIntents": [
            {"localTableRef": "email_drafts", "joinTableRef": "email_approval_records",
             "localKey": "email_draft_id", "remoteKey": "email_draft_id"},
            {"localTableRef": "email_drafts", "joinTableRef": "email_delivery_evidence",
             "localKey": "email_draft_id", "remoteKey": "email_draft_id"}
        ],
        "operationEntityBindings": [
            {"operationKind": "draft_recorded",      "function": "topology.em_record_draft",              "entityTargetColumns": ["dispatch_idempotency_key", "recipient_ref", "subject_ref", "response_port_ref"]},
            {"operationKind": "approval_recorded",   "function": "topology.em_record_approval",           "entityTargetColumns": ["email_draft_id", "approval_status", "reviewed_by"]},
            {"operationKind": "dispatch_initiated",  "function": "topology.em_record_dispatch_initiated", "entityTargetColumns": ["email_draft_id"]},
            {"operationKind": "send_recorded",       "function": "topology.em_record_send_evidence",      "entityTargetColumns": ["email_draft_id", "send_result"]}
        ]
    }'::jsonb
),
    updated_at = now()
WHERE topology_manifest_id = '00000000-0000-0000-0000-0000000000a3';

-- ---------------------------------------------------------------------------
-- Re-seed policy e6 (email_response_port_generic): expand the PR#460 baseline
-- 9-step send lane to 12 steps that record approval-gated dispatch + delivery
-- evidence through the generic abstract function lane.
--   step  3: execute_abstract_function → af42 (record_dispatch_initiated)
--            — fail-closes when the draft is unapproved or already dispatched.
--   step  4: append_runtime_event_log  → dispatch_initiated (+ delivery evidence)
--   step 11: execute_abstract_function → af43 (record_send_evidence)
--            — records send_success/send_failure derived from the HTTP response.
--   step 12: append_runtime_event_log  → send_dispatched (+ delivery projection)
-- Preserves the compat abstract_function_key values for steps 423/424/425 set by
-- external_port_compat_absorption_seed.sql (which ran before this file).
-- Send only ever occurs AFTER explicit human approval (af42 gate before send_http).
-- ---------------------------------------------------------------------------
DELETE FROM topology.external_port_policy_steps
WHERE policy_id = '00000000-0000-0000-0000-0000000000e6';

INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000421', '00000000-0000-0000-0000-0000000000e6',  1, 'resolve_port_record',               '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-000000000422', '00000000-0000-0000-0000-0000000000e6',  2, 'resolve_credential_reference',      '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-00000000042a', '00000000-0000-0000-0000-0000000000e6',  3, 'execute_abstract_function',         '{}',                'email.record_dispatch_initiated',             true),
    ('00000000-0000-0000-0000-00000000042b', '00000000-0000-0000-0000-0000000000e6',  4, 'append_runtime_event_log',          '{"event_type":"dispatch_initiated","evidence_table_ref":"topology.email_delivery_evidence","projection_table_ref":"topology.email_delivery_evidence","status_value":"dispatch_initiated"}', NULL, true),
    ('00000000-0000-0000-0000-000000000423', '00000000-0000-0000-0000-0000000000e6',  5, 'load_encrypted_credential_payload', '{}',                'external_port.compat_build.email',            true),
    ('00000000-0000-0000-0000-000000000424', '00000000-0000-0000-0000-0000000000e6',  6, 'decrypt_for_runtime_use',           '{}',                'external_port.http.email_post',               true),
    ('00000000-0000-0000-0000-000000000425', '00000000-0000-0000-0000-0000000000e6',  7, 'build_http_request',                '{"method":"POST"}', 'external_port.event.email_response_completed', true),
    ('00000000-0000-0000-0000-000000000426', '00000000-0000-0000-0000-0000000000e6',  8, 'inject_authorization_header',       '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-000000000427', '00000000-0000-0000-0000-0000000000e6',  9, 'send_http',                         '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-000000000428', '00000000-0000-0000-0000-0000000000e6', 10, 'capture_response',                  '{}',                NULL,                                          true),
    ('00000000-0000-0000-0000-00000000042c', '00000000-0000-0000-0000-0000000000e6', 11, 'execute_abstract_function',         '{}',                'email.record_send_evidence',                  true),
    ('00000000-0000-0000-0000-000000000429', '00000000-0000-0000-0000-0000000000e6', 12, 'append_runtime_event_log',          '{"event_type":"send_dispatched","projection_table_ref":"topology.email_delivery_evidence","status_value":"send_dispatched"}', NULL, true)
ON CONFLICT (policy_id, step_order) DO UPDATE
    SET operation_key         = EXCLUDED.operation_key,
        step_config           = EXCLUDED.step_config,
        abstract_function_key = EXCLUDED.abstract_function_key,
        active                = EXCLUDED.active;

-- Policy steps for draft (f2) and approval (f3) domain-action policies.
-- execute_abstract_function references the manifest-specific function key.
-- Approval semantics ('approved') are encoded in constant input_bindings (af41).
INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    -- email_draft_action_generic (f2): 3 steps
    ('00000000-0000-0000-0000-0000000004f7', '00000000-0000-0000-0000-0000000000f2', 1,
     'resolve_port_record', '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004f8', '00000000-0000-0000-0000-0000000000f2', 2,
     'execute_abstract_function', '{}', 'email.record_draft', true),
    ('00000000-0000-0000-0000-0000000004f9', '00000000-0000-0000-0000-0000000000f2', 3,
     'append_runtime_event_log',
     '{"event_type":"draft_recorded","status_value":"draft"}',
     NULL, true),
    -- email_approval_action_generic (f3): 3 steps
    ('00000000-0000-0000-0000-0000000004fa', '00000000-0000-0000-0000-0000000000f3', 1,
     'resolve_port_record', '{}', NULL, true),
    ('00000000-0000-0000-0000-0000000004fb', '00000000-0000-0000-0000-0000000000f3', 2,
     'execute_abstract_function', '{}', 'email.record_approval', true),
    ('00000000-0000-0000-0000-0000000004fc', '00000000-0000-0000-0000-0000000000f3', 3,
     'append_runtime_event_log',
     '{"event_type":"approval_recorded","evidence_table_ref":"topology.email_delivery_evidence","projection_table_ref":"topology.email_delivery_evidence","status_value":"approved"}',
     NULL, true)
ON CONFLICT (policy_id, step_order) DO NOTHING;

-- =============================================================================
-- UI Builder preset: email_approval_form.v1
-- Derived from physical_search_crud_aggregate.v1.
-- Three portTargetRef wiring candidates:
--   create_draft_button → f11 (response_port: domain_action email_draft)
--   approve_button      → f12 (response_port: domain_action email_approval)
--   send_button         → f03 (response_port: smtp send lane e6, after approval)
-- No SMTP provider-specific client or credential plane is introduced.
-- Projection response: email_draft_id, approval_record_id, delivery_status (opaque).
-- Forbidden projection fields: credential, smtp host/port/api key, raw provider response.
-- =============================================================================
WITH upserted_preset AS (
    INSERT INTO topology.mock_preset_registry (
        preset_key, preset_label, source_kind, source_hash,
        source_snapshot_json, visual_tree_json, status
    )
    VALUES (
        'email_approval_form.v1',
        'Email Approval & Delivery form preset seed',
        'ui_builder_canvas',
        'email_approval_form.v1.seed.2026-06-24.r1',
        $$
        {
          "seedKind": "ui_builder_canvas_preset_seed",
          "presetKey": "email_approval_form.v1",
          "bundle": "email-port-consumer",
          "surface": "email_draft_approval_and_delivery",
          "derivedFrom": "physical_search_crud_aggregate.v1",
          "componentImplementation": "none; existing CRUD/component catalog composition only",
          "runtimeMutationAuthority": "external port substrate via portTargetRef and execute_abstract_function policy steps (manifests af40-af43)",
          "credentialPlane": "external_port_substrate reference_key resolution only",
          "sendAuthority": "ui_approval_then_backend_dispatch",
          "forbiddenProjectionFields": ["credential", "smtp_host", "smtp_port", "smtp_api_key", "sender_credential", "raw_provider_response", "plaintext_payload", "token"],
          "payloadFromResolver": "frontend/runtime/payloadFromResolver.ts",
          "projectionResponseFields": ["email_draft_id", "approval_record_id", "delivery_status"],
          "note": "Three actions: create draft (->f11 records draft via af40), approve (->f12 records approval via af41, constant approved), send (->f03 smtp send lane e6: af42 approval-gate guard -> send_http -> af43 send_success/send_failure). AI autonomous send, implicit approval, and CLI/MCP send are prohibited. Email send only after explicit human approval."
        }
        $$::jsonb,
        $$
        {"nodes":[
          {"nodeId":"email_shell","componentKey":"section.alias","componentKind":"disclosure_structure/section","parentNodeId":null},
          {"nodeId":"dispatch_idempotency_key_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"email_shell"},
          {"nodeId":"recipient_ref_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"email_shell"},
          {"nodeId":"subject_ref_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"email_shell"},
          {"nodeId":"email_draft_id_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"email_shell"},
          {"nodeId":"reviewed_by_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"email_shell"},
          {"nodeId":"create_draft_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"email_shell"},
          {"nodeId":"approve_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"email_shell"},
          {"nodeId":"send_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"email_shell"},
          {"nodeId":"email_result_json","componentKey":"json_viewer.template","componentKind":"data_display/json","parentNodeId":"email_shell"}
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
        ((SELECT preset_id FROM preset), 'email_shell', 'email_shell', 'catalog_component', 'section.alias', 'disclosure_structure/section', NULL, 'root', 0,
         '{"x":0,"y":0,"width":1024,"height":960}'::jsonb,
         '{"title":"Email Draft, Approval & Delivery","description":"Create a draft, approve it via the UI, then send. Email send authority is UI/human explicit approval then backend dispatch. Configure portTargetRef payload mapping before apply."}'::jsonb,
         '{"layoutIntent":"crud_derived_shell"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'dispatch_idempotency_key_input', 'dispatch_idempotency_key_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'email_shell', 'fields', 1,
         '{"x":24,"y":72,"width":456,"height":64}'::jsonb,
         '{"label":"Dispatch ID (idempotency key)","required":true}'::jsonb,
         '{"layoutIntent":"dispatch_idempotency_key_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'recipient_ref_input', 'recipient_ref_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'email_shell', 'fields', 2,
         '{"x":496,"y":72,"width":456,"height":64}'::jsonb,
         '{"label":"Recipient","required":true}'::jsonb,
         '{"layoutIntent":"recipient_ref_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'subject_ref_input', 'subject_ref_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'email_shell', 'fields', 3,
         '{"x":24,"y":152,"width":456,"height":64}'::jsonb,
         '{"label":"Subject","required":true}'::jsonb,
         '{"layoutIntent":"subject_ref_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'email_draft_id_input', 'email_draft_id_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'email_shell', 'fields', 4,
         '{"x":496,"y":152,"width":456,"height":64}'::jsonb,
         '{"label":"Email draft ID (for approve/send)","required":false,"hint":"Populated from create-draft result or entered manually"}'::jsonb,
         '{"layoutIntent":"email_draft_id_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'reviewed_by_input', 'reviewed_by_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'email_shell', 'fields', 5,
         '{"x":24,"y":232,"width":456,"height":64}'::jsonb,
         '{"label":"Reviewed by (approver)","required":false}'::jsonb,
         '{"layoutIntent":"reviewed_by_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'create_draft_button', 'create_draft_button', 'catalog_component', 'button.primitive', 'action/button', 'email_shell', 'actions', 6,
         '{"x":24,"y":312,"width":200,"height":48}'::jsonb,
         '{"label":"Create draft","variant":"primary"}'::jsonb,
         '{"layoutIntent":"external_port_dispatch"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'approve_button', 'approve_button', 'catalog_component', 'button.primitive', 'action/button', 'email_shell', 'actions', 7,
         '{"x":240,"y":312,"width":180,"height":48}'::jsonb,
         '{"label":"Approve","variant":"success"}'::jsonb,
         '{"layoutIntent":"external_port_dispatch"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'send_button', 'send_button', 'catalog_component', 'button.primitive', 'action/button', 'email_shell', 'actions', 8,
         '{"x":436,"y":312,"width":180,"height":48}'::jsonb,
         '{"label":"Send (after approval)","variant":"danger"}'::jsonb,
         '{"layoutIntent":"external_port_dispatch"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'email_result_json', 'email_result_json', 'catalog_component', 'json_viewer.template', 'data_display/json', 'email_shell', 'results', 9,
         '{"x":24,"y":376,"width":928,"height":552}'::jsonb,
         '{"title":"Email result"}'::jsonb,
         '{"layoutIntent":"result_projection"}'::jsonb, 'mapped')
    RETURNING mapping_id
), inserted_wiring AS (
    INSERT INTO topology.mock_preset_wiring_candidate (
        preset_id, source_object_id, node_id, capability_tag,
        wiring_kind, target_surface, target_ref, binding_json, status
    )
    VALUES
        -- create_draft_button: dispatches to f11 (email_draft_action response_port).
        -- policy f2 records the email draft (af40).
        ((SELECT preset_id FROM preset), 'create_draft_button', 'create_draft_button',
         'requires_external_port_email_draft', 'dispatchExternalPort', 'external_port',
         'external-port:response_port:00000000-0000-0000-0000-000000000f11',
         $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f11","payloadFrom":{"dispatch_idempotency_key":"node:dispatch_idempotency_key_input.value","recipient_ref":"node:recipient_ref_input.value","subject_ref":"node:subject_ref_input.value"},"outputProp":"emailDraftResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Dispatches to email_draft_action response_port. Policy f2 records the draft (af40). No credential plaintext or SMTP endpoint in payload or response."}$$::jsonb,
         'pending'),
        -- approve_button: dispatches to f12 (email_approval_action response_port).
        -- policy f3 records approval evidence (af41: constant approved) + event log.
        ((SELECT preset_id FROM preset), 'approve_button', 'approve_button',
         'requires_external_port_email_approval', 'dispatchExternalPort', 'external_port',
         'external-port:response_port:00000000-0000-0000-0000-000000000f12',
         $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f12","payloadFrom":{"email_draft_id":"node:email_draft_id_input.value","reviewed_by":"node:reviewed_by_input.value"},"outputProp":"emailApprovalResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Dispatches to email_approval_action response_port. Policy f3 records approval via af41 (constant: approved). Approval authority is UI/human explicit action only."}$$::jsonb,
         'pending'),
        -- send_button: dispatches to f03 (smtp response_port send lane e6).
        -- policy e6 runs af42 approval-gate guard, sends, then records af43 evidence.
        ((SELECT preset_id FROM preset), 'send_button', 'send_button',
         'requires_external_port_email_send', 'dispatchExternalPort', 'external_port',
         'external-port:response_port:00000000-0000-0000-0000-000000000f03',
         $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f03","payloadFrom":{"email_draft_id":"node:email_draft_id_input.value","dispatch_idempotency_key":"node:dispatch_idempotency_key_input.value"},"outputProp":"emailSendResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Dispatches to email smtp response_port send lane (e6). af42 fail-closes unless the draft is already approved; double send is prevented. send_success/send_failure recorded via af43. No silent fallback."}$$::jsonb,
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
    'email-approval-form-seed.v1',
    $${"nodes":[
      {"nodeId":"email_shell","nodeKind":"catalog_component","componentKey":"section.alias","componentKind":"disclosure_structure/section","isDraftOnly":false,"slotKey":"root","orderIndex":0,"parentNodeId":null,"x":0,"y":0,"width":1024,"height":960,"propsJson":"{\"title\":\"Email Draft, Approval & Delivery\",\"description\":\"Create, approve, then send via UI. Email send authority is UI/human explicit approval then backend dispatch. Configure portTargetRef payload mapping before apply.\"}"},
      {"nodeId":"dispatch_idempotency_key_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":1,"parentNodeId":"email_shell","x":24,"y":72,"width":456,"height":64,"propsJson":"{\"label\":\"Dispatch ID (idempotency key)\",\"required\":true}"},
      {"nodeId":"recipient_ref_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":2,"parentNodeId":"email_shell","x":496,"y":72,"width":456,"height":64,"propsJson":"{\"label\":\"Recipient\",\"required\":true}"},
      {"nodeId":"subject_ref_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":3,"parentNodeId":"email_shell","x":24,"y":152,"width":456,"height":64,"propsJson":"{\"label\":\"Subject\",\"required\":true}"},
      {"nodeId":"email_draft_id_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":4,"parentNodeId":"email_shell","x":496,"y":152,"width":456,"height":64,"propsJson":"{\"label\":\"Email draft ID (for approve/send)\",\"required\":false,\"hint\":\"Populated from create-draft result or entered manually\"}"},
      {"nodeId":"reviewed_by_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":5,"parentNodeId":"email_shell","x":24,"y":232,"width":456,"height":64,"propsJson":"{\"label\":\"Reviewed by (approver)\",\"required\":false}"},
      {"nodeId":"create_draft_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":6,"parentNodeId":"email_shell","x":24,"y":312,"width":200,"height":48,"propsJson":"{\"label\":\"Create draft\",\"variant\":\"primary\"}"},
      {"nodeId":"approve_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":7,"parentNodeId":"email_shell","x":240,"y":312,"width":180,"height":48,"propsJson":"{\"label\":\"Approve\",\"variant\":\"success\"}"},
      {"nodeId":"send_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":8,"parentNodeId":"email_shell","x":436,"y":312,"width":180,"height":48,"propsJson":"{\"label\":\"Send (after approval)\",\"variant\":\"danger\"}"},
      {"nodeId":"email_result_json","nodeKind":"catalog_component","componentKey":"json_viewer.template","componentKind":"data_display/json","isDraftOnly":false,"slotKey":"results","orderIndex":9,"parentNodeId":"email_shell","x":24,"y":376,"width":928,"height":552,"propsJson":"{\"title\":\"Email result\"}","propBindings":{"data":{"source":"emission.data.emailDraftResult"}}}
    ],"layoutClassRefs":[]}$$::jsonb,
    $${"activeTopologyWrite":false,"bindTarget":"selected_route_package_tmp_canvas_draft","derivedFrom":"physical_search_crud_aggregate.v1","requiresHumanAdjustmentBeforeApply":true}$$::jsonb,
    $$
    [
      {"nodeId":"create_draft_button","sourceObjectId":"create_draft_button","capabilityTag":"requires_external_port_email_draft","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f11","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f11","payloadFrom":{"dispatch_idempotency_key":"node:dispatch_idempotency_key_input.value","recipient_ref":"node:recipient_ref_input.value","subject_ref":"node:subject_ref_input.value"},"outputProp":"emailDraftResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}},
      {"nodeId":"approve_button","sourceObjectId":"approve_button","capabilityTag":"requires_external_port_email_approval","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f12","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f12","payloadFrom":{"email_draft_id":"node:email_draft_id_input.value","reviewed_by":"node:reviewed_by_input.value"},"outputProp":"emailApprovalResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}},
      {"nodeId":"send_button","sourceObjectId":"send_button","capabilityTag":"requires_external_port_email_send","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f03","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f03","payloadFrom":{"email_draft_id":"node:email_draft_id_input.value","dispatch_idempotency_key":"node:dispatch_idempotency_key_input.value"},"outputProp":"emailSendResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}}
    ]
    $$::jsonb,
    $$[{"nodeId":"email_shell","styleIntent":"crud_derived_email_shell"},{"nodeId":"email_result_json","styleIntent":"result_projection_panel"}]$$::jsonb,
    $$[]$$::jsonb
);

-- ── email_bundle physical table catalog ────────────────────────────────────────
-- Registers the three domain tables for draft / approval_record / delivery_evidence.
-- Idempotent: ON CONFLICT (table_ref) DO UPDATE.
INSERT INTO topology.physical_tables (table_ref, schema_name, category, active)
VALUES
    ('topology.email_drafts',            'topology', 'email_bundle', true),
    ('topology.email_approval_records',  'topology', 'email_bundle', true),
    ('topology.email_delivery_evidence', 'topology', 'email_bundle', true)
ON CONFLICT (table_ref) DO UPDATE
    SET schema_name = EXCLUDED.schema_name,
        category    = EXCLUDED.category,
        active      = EXCLUDED.active;

-- ── physical_table_manifest_bindings ──────────────────────────────────────────
-- Bind the three email tables to manifest a3
-- (email.response_port.approval.delivery.projection). Idempotent upsert with
-- form-preset-seed provenance and manifestKey evidence.
INSERT INTO topology.physical_table_manifest_bindings
    (physical_table_id, topology_manifest_id, active, binding_evidence_json)
SELECT pt.physical_table_id,
       '00000000-0000-0000-0000-0000000000a3'::uuid,
       true,
       jsonb_build_object(
           'bundle',      'email_bundle',
           'source',      'email-bundle-form-preset-seed',
           'manifestKey', 'email.response_port.approval.delivery.projection')
FROM topology.physical_tables pt
WHERE pt.table_ref IN (
    'topology.email_drafts',
    'topology.email_approval_records',
    'topology.email_delivery_evidence')
ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE
    SET active                = true,
        binding_evidence_json = EXCLUDED.binding_evidence_json,
        updated_at            = now();
