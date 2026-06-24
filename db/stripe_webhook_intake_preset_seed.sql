-- =============================================================================
-- stripe-port-consumer bundle: webhook intake / verification / payment state lane.
--
-- This file is applied AFTER external_port_compat_absorption_seed.sql so the
-- stripe hook policy (e7) re-seed below replaces the PR#460 baseline steps and the
-- compatibility absorption of those steps. It implements the stripe_bundle hook
-- consumer entirely on the generic external_port_substrate lane:
--
--   hook_port_receive (/hooks/stripe) -> RuntimeTimelineScheduler.AlignAndDispatchAsync
--   -> ManifestDispatcher (external_port_runtime) -> ExternalPortDispatchRuntime
--   -> port record resolution -> credential requirement / reference_key resolution
--   -> verify_signature_by_config -> execute_abstract_function (call_postgres_function)
--   -> webhook_intake_snapshots / signature_verification_evidence / payment_state_projections
--   -> runtime_event_log + DB projection response.
--
-- No Stripe provider-specific client / runtime / handler / route / UI is added.
-- provider_kind / required_by_bundle remain DB data; they never select a C# runtime.
-- Physical writes are DB-driven (call_postgres_function -> topology.stripe_* functions);
-- table/column authority is manifest-defined, never payload-derived. Idempotency and the
-- verified-event payment authority gate live in the PostgreSQL functions.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Abstract function manifests af50-af54 (stripe_bundle).
-- runtime_lane=external_port_runtime, authority_scope=stripe_bundle.
-- projection_deny_keys block any credential / raw provider payload / endpoint leak.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af50', 'stripe.record_webhook_intake',
     'external_port_runtime', 'stripe_bundle', '{"result":"WebhookIntakeId"}',
     ARRAY['credential','stripe_secret_key','webhook_secret','stripe_signature','raw_provider_payload','endpoint','plaintext_payload','token'], true),
    ('00000000-0000-0000-0000-00000000af51', 'stripe.record_verification_success',
     'external_port_runtime', 'stripe_bundle', '{"result":"VerificationEvidenceId"}',
     ARRAY['credential','stripe_secret_key','webhook_secret','stripe_signature','raw_provider_payload','endpoint','plaintext_payload','token'], true),
    ('00000000-0000-0000-0000-00000000af52', 'stripe.record_payment_state',
     'external_port_runtime', 'stripe_bundle', '{"result":"PaymentStateId"}',
     ARRAY['credential','stripe_secret_key','webhook_secret','stripe_signature','raw_provider_payload','endpoint','plaintext_payload','token'], true),
    ('00000000-0000-0000-0000-00000000af53', 'stripe.record_ledger_binding',
     'external_port_runtime', 'stripe_bundle', '{"result":"LedgerBindingId"}',
     ARRAY['credential','stripe_secret_key','webhook_secret','stripe_signature','raw_provider_payload','endpoint','plaintext_payload','token'], true),
    ('00000000-0000-0000-0000-00000000af54', 'stripe.project_payment_state',
     'external_port_runtime', 'stripe_bundle', '{"result":"OutputProp"}',
     ARRAY['credential','stripe_secret_key','webhook_secret','stripe_signature','raw_provider_payload','endpoint','plaintext_payload','token'], true)
ON CONFLICT (abstract_function_id) DO UPDATE SET output_shape = EXCLUDED.output_shape;

-- ---------------------------------------------------------------------------
-- Abstract function steps (single call_postgres_function per manifest).
-- arguments is the ordered positional argument list passed to the PostgreSQL function.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf60', '00000000-0000-0000-0000-00000000af50', 1,
     'call_postgres_function',
     '{"function":"topology.stripe_record_webhook_intake","required_table_authority":"topology.webhook_intake_snapshots","arguments":["stripe_event_id","route_key","hook_path","payload_hash"]}',
     'WebhookIntakeId', true),
    ('00000000-0000-0000-0000-00000000bf61', '00000000-0000-0000-0000-00000000af51', 1,
     'call_postgres_function',
     '{"function":"topology.stripe_record_verification_success","required_table_authority":"topology.signature_verification_evidence","arguments":["stripe_event_id"]}',
     'VerificationEvidenceId', true),
    ('00000000-0000-0000-0000-00000000bf62', '00000000-0000-0000-0000-00000000af52', 1,
     'call_postgres_function',
     '{"function":"topology.stripe_record_payment_state","required_table_authority":"topology.payment_state_projections","arguments":["stripe_event_id","payment_state"]}',
     'PaymentStateId', true),
    ('00000000-0000-0000-0000-00000000bf63', '00000000-0000-0000-0000-00000000af53', 1,
     'call_postgres_function',
     '{"function":"topology.stripe_record_ledger_binding","required_table_authority":"topology.payment_state_projections","arguments":["stripe_event_id"]}',
     'LedgerBindingId', true),
    ('00000000-0000-0000-0000-00000000bf64', '00000000-0000-0000-0000-00000000af54', 1,
     'call_postgres_function',
     '{"function":"topology.stripe_project_payment_state","required_table_authority":"topology.payment_state_projections","arguments":["stripe_event_id"]}',
     'OutputProp', true)
ON CONFLICT (abstract_function_id, step_order) DO UPDATE
    SET step_config        = EXCLUDED.step_config,
        result_context_key = EXCLUDED.result_context_key,
        active             = EXCLUDED.active;

-- ---------------------------------------------------------------------------
-- Input bindings. stripe_event_id / payment_state / payload_hash come from the
-- dispatch_payload (payload binding source). route_key / hook_path are constant
-- bindings — never provider credential or endpoint material.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    -- af50 record_webhook_intake
    ('00000000-0000-0000-0000-00000000c0a0', '00000000-0000-0000-0000-00000000bf60', 'stripe_event_id', 'payload',   'stripe_event_id', true,  false, true),
    ('00000000-0000-0000-0000-00000000c0a1', '00000000-0000-0000-0000-00000000bf60', 'route_key',       'constant',  'stripe',          true,  false, true),
    ('00000000-0000-0000-0000-00000000c0a2', '00000000-0000-0000-0000-00000000bf60', 'hook_path',       'constant',  '/hooks/stripe',   true,  false, true),
    ('00000000-0000-0000-0000-00000000c0a3', '00000000-0000-0000-0000-00000000bf60', 'payload_hash',    'payload',   'payload_hash',    false, false, true),
    -- af51 record_verification_success
    ('00000000-0000-0000-0000-00000000c0a4', '00000000-0000-0000-0000-00000000bf61', 'stripe_event_id', 'payload',   'stripe_event_id', true,  false, true),
    -- af52 record_payment_state
    ('00000000-0000-0000-0000-00000000c0a5', '00000000-0000-0000-0000-00000000bf62', 'stripe_event_id', 'payload',   'stripe_event_id', true,  false, true),
    ('00000000-0000-0000-0000-00000000c0a6', '00000000-0000-0000-0000-00000000bf62', 'payment_state',   'payload',   'payment_state',   true,  false, true),
    -- af53 record_ledger_binding
    ('00000000-0000-0000-0000-00000000c0a7', '00000000-0000-0000-0000-00000000bf63', 'stripe_event_id', 'payload',   'stripe_event_id', true,  false, true),
    -- af54 project_payment_state
    ('00000000-0000-0000-0000-00000000c0a8', '00000000-0000-0000-0000-00000000bf64', 'stripe_event_id', 'payload',   'stripe_event_id', true,  false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Authority bindings: each manifest names the policy it serves
-- (stripe_hook_port_scheduler_boundary) and the physical tables it may touch.
-- This keeps the call_postgres_function table authority manifest-defined.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    ('00000000-0000-0000-0000-00000000af50', 'policy', 'stripe_hook_port_scheduler_boundary', true),
    ('00000000-0000-0000-0000-00000000af50', 'table',  'topology.webhook_intake_snapshots',   true),
    ('00000000-0000-0000-0000-00000000af50', 'table',  'topology.runtime_event_log',          true),
    ('00000000-0000-0000-0000-00000000af51', 'policy', 'stripe_hook_port_scheduler_boundary', true),
    ('00000000-0000-0000-0000-00000000af51', 'table',  'topology.signature_verification_evidence', true),
    ('00000000-0000-0000-0000-00000000af51', 'table',  'topology.webhook_intake_snapshots',   true),
    ('00000000-0000-0000-0000-00000000af51', 'table',  'topology.runtime_event_log',          true),
    ('00000000-0000-0000-0000-00000000af52', 'policy', 'stripe_hook_port_scheduler_boundary', true),
    ('00000000-0000-0000-0000-00000000af52', 'table',  'topology.payment_state_projections',  true),
    ('00000000-0000-0000-0000-00000000af52', 'table',  'topology.signature_verification_evidence', true),
    ('00000000-0000-0000-0000-00000000af52', 'table',  'topology.runtime_event_log',          true),
    ('00000000-0000-0000-0000-00000000af53', 'policy', 'stripe_hook_port_scheduler_boundary', true),
    ('00000000-0000-0000-0000-00000000af53', 'table',  'topology.payment_state_projections',  true),
    ('00000000-0000-0000-0000-00000000af53', 'table',  'topology.runtime_event_log',          true),
    ('00000000-0000-0000-0000-00000000af54', 'policy', 'stripe_hook_port_scheduler_boundary', true),
    ('00000000-0000-0000-0000-00000000af54', 'table',  'topology.payment_state_projections',  true),
    ('00000000-0000-0000-0000-00000000af54', 'table',  'topology.signature_verification_evidence', true),
    ('00000000-0000-0000-0000-00000000af54', 'table',  'topology.webhook_intake_snapshots',   true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO UPDATE
    SET active = EXCLUDED.active;

-- ---------------------------------------------------------------------------
-- Re-seed policy e7 (stripe_hook_port_scheduler_boundary): replace the PR#460
-- 5-step baseline (and its compatibility absorption) with the full hook lane.
-- The scheduler boundary for this hook lane is the outer
-- RuntimeTimelineScheduler.AlignAndDispatchAsync route (intake -> scheduler queue ->
-- ManifestDispatcher -> external_port_runtime), which is the REAL scheduler boundary
-- proven by ExternalPortDispatchRuntime tests. The in-policy enqueue_scheduler_event
-- step is retained only as a lane-continuity marker (via the absorbed scheduler_enqueue
-- abstract function); it is NOT treated as the completion basis.
--
-- Ordering enforces verify-before-trust: verify_signature_by_config (step 3) runs before
-- any intake / evidence / payment write. On verification failure the verify step records
-- verification_failure to runtime_event_log + signature_verification_evidence and throws;
-- no intake snapshot, no payment_state, no scheduler downstream.
-- ---------------------------------------------------------------------------
DELETE FROM topology.external_port_policy_steps
WHERE policy_id = '00000000-0000-0000-0000-0000000000e7';

INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, abstract_function_key, active)
VALUES
    ('00000000-0000-0000-0000-000000000431', '00000000-0000-0000-0000-0000000000e7', 1, 'resolve_port_record',          '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000432', '00000000-0000-0000-0000-0000000000e7', 2, 'resolve_credential_reference', '{}', NULL, true),
    ('00000000-0000-0000-0000-000000000433', '00000000-0000-0000-0000-0000000000e7', 3, 'verify_signature_by_config',
     '{"failure_event_type":"verification_failure","failure_evidence_table_ref":"topology.signature_verification_evidence","failure_status_value":"verification_failed"}', NULL, true),
    ('00000000-0000-0000-0000-000000000436', '00000000-0000-0000-0000-0000000000e7', 4, 'execute_abstract_function', '{}', 'stripe.record_webhook_intake',       true),
    ('00000000-0000-0000-0000-000000000437', '00000000-0000-0000-0000-0000000000e7', 5, 'execute_abstract_function', '{}', 'stripe.record_verification_success', true),
    ('00000000-0000-0000-0000-000000000434', '00000000-0000-0000-0000-0000000000e7', 6, 'enqueue_scheduler_event',   '{}', 'external_port.scheduler_enqueue.stripe', true),
    ('00000000-0000-0000-0000-000000000438', '00000000-0000-0000-0000-0000000000e7', 7, 'execute_abstract_function', '{}', 'stripe.record_payment_state',        true),
    ('00000000-0000-0000-0000-000000000439', '00000000-0000-0000-0000-0000000000e7', 8, 'execute_abstract_function', '{}', 'stripe.record_ledger_binding',       true),
    ('00000000-0000-0000-0000-00000000043a', '00000000-0000-0000-0000-0000000000e7', 9, 'execute_abstract_function', '{}', 'stripe.project_payment_state',       true)
ON CONFLICT (policy_id, step_order) DO UPDATE
    SET operation_key        = EXCLUDED.operation_key,
        step_config          = EXCLUDED.step_config,
        abstract_function_key = EXCLUDED.abstract_function_key,
        active               = EXCLUDED.active;

-- ---------------------------------------------------------------------------
-- Update topology_manifest a4 (stripe.hook_port.intake.payment.projection) with a
-- screen_data_shape binding so payment_state / verification_evidence are projected
-- from DB / seed / topology manifest (not handwritten emission). forbiddenProjectionFields
-- block credential / webhook secret / stripe signature / raw provider payload / endpoint /
-- token from any SSE / projection output.
-- ---------------------------------------------------------------------------
UPDATE hubs.topology_manifests
SET topology_jsonb = topology_jsonb || jsonb_build_object(
    'screen_data_shape', '{
        "type": "screen_data_shape",
        "topologySystemName": "stripe-webhook-intake-seed",
        "userFacingTopologyLabel": "Stripe Payment State & Verification",
        "tableRef": "topology.payment_state_projections",
        "dbTableName": "topology.payment_state_projections",
        "displayColumnMode": "selected",
        "forbiddenProjectionFields": ["credential", "stripe_secret_key", "webhook_secret", "stripe_signature", "raw_provider_payload", "endpoint", "plaintext_payload", "token"],
        "displayColumns": [
            "payment_state_projections.payment_state",
            "payment_state_projections.stripe_event_id",
            "payment_state_projections.projected_at",
            "signature_verification_evidence.verification_status",
            "webhook_intake_snapshots.route_key",
            "webhook_intake_snapshots.received_at"
        ],
        "logicalTables": [
            {"tableName": "webhook_intake_snapshots", "columns": [
                {"name": "webhook_intake_snapshot_id", "dataType": "uuid", "nullable": false},
                {"name": "required_by_bundle", "dataType": "text", "nullable": false},
                {"name": "route_key", "dataType": "text", "nullable": true},
                {"name": "hook_path", "dataType": "text", "nullable": true},
                {"name": "payload_hash", "dataType": "text", "nullable": false},
                {"name": "stripe_event_id", "dataType": "text", "nullable": true},
                {"name": "received_at", "dataType": "timestamptz", "nullable": false}
            ]},
            {"tableName": "signature_verification_evidence", "columns": [
                {"name": "signature_verification_evidence_id", "dataType": "uuid", "nullable": false},
                {"name": "webhook_intake_snapshot_id", "dataType": "uuid", "nullable": true},
                {"name": "required_by_bundle", "dataType": "text", "nullable": false},
                {"name": "verification_status", "dataType": "text", "nullable": false},
                {"name": "recorded_at", "dataType": "timestamptz", "nullable": false}
            ]},
            {"tableName": "payment_state_projections", "columns": [
                {"name": "payment_state_projection_id", "dataType": "uuid", "nullable": false},
                {"name": "webhook_intake_snapshot_id", "dataType": "uuid", "nullable": true},
                {"name": "payment_state", "dataType": "text", "nullable": false},
                {"name": "stripe_event_id", "dataType": "text", "nullable": true},
                {"name": "projected_at", "dataType": "timestamptz", "nullable": false}
            ]}
        ],
        "relationIntents": [
            {"localTableRef": "payment_state_projections", "joinTableRef": "webhook_intake_snapshots",
             "localKey": "webhook_intake_snapshot_id", "remoteKey": "webhook_intake_snapshot_id"},
            {"localTableRef": "signature_verification_evidence", "joinTableRef": "webhook_intake_snapshots",
             "localKey": "webhook_intake_snapshot_id", "remoteKey": "webhook_intake_snapshot_id"}
        ],
        "operationEntityBindings": [
            {"operationKind": "webhook_received",         "function": "topology.stripe_record_webhook_intake",       "entityTargetColumns": ["stripe_event_id", "route_key", "hook_path", "payload_hash"]},
            {"operationKind": "verification_success",     "function": "topology.stripe_record_verification_success", "entityTargetColumns": ["stripe_event_id"]},
            {"operationKind": "payment_state_projected",  "function": "topology.stripe_record_payment_state",        "entityTargetColumns": ["stripe_event_id", "payment_state"]},
            {"operationKind": "ledger_binding_completed", "function": "topology.stripe_record_ledger_binding",       "entityTargetColumns": ["stripe_event_id"]}
        ]
    }'::jsonb
),
    updated_at = now()
WHERE topology_manifest_id = '00000000-0000-0000-0000-0000000000a4';
