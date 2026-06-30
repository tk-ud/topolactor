-- =============================================================================
-- backend_error_notify_hook_port_seed.sql
-- Error-notify hook_port seed for the backend system error evidence post-notify bridge.
--
-- SSOT: docs/design/backend-error-evidence-report-notify-ssot.yaml
--   (report_and_notify.listener_bridge / logger_sink_boundary /
--    admin_external_port_configuration_dependency).
--
-- The BackendErrorNotifyBridge dispatches a dispatchExternalPort hook trigger through
-- RuntimeTimelineScheduler -> ManifestDispatcher -> external_port_runtime. That runtime resolves
-- the active external hook_port by (hook_path, route_key) and its active hook_port policy by
-- (port_kind='hook_port', required_by_bundle). This seed provides that target so a dispatch reaches
-- an explicitly accepted boundary result and the durable queue row transitions to 'acknowledged'.
--
-- First-implementation sink (SSOT logger_sink_boundary): a single append_runtime_event_log step
-- records the notification as external-port consumer domain-event evidence. This is a hook dispatch
-- target/sink only — it does NOT replace durable queue state and is NOT the backend error evidence
-- store (logs.error remains the sole system error evidence). provider_kind / required_by_bundle are
-- data labels and never select a C# handler; no provider/bundle-specific branching is added.
-- credential_kind = 'none' (the logger sink needs no credential).
-- =============================================================================

INSERT INTO topology.external_hook_ports
    (hook_port_id, required_by_bundle, provider_kind, hook_path, route_key, credential_kind, reference_key, active)
VALUES
    ('00000000-0000-0000-0000-0000000e0001', 'backend_error_notify_bundle', 'backend_error_notify',
     '/hooks/error_notify', 'error_notify', 'none', NULL, true)
ON CONFLICT (hook_port_id) DO UPDATE
    SET hook_path = EXCLUDED.hook_path,
        route_key = EXCLUDED.route_key,
        credential_kind = EXCLUDED.credential_kind,
        active = EXCLUDED.active,
        updated_at = now();

INSERT INTO topology.external_port_policies
    (policy_id, policy_key, port_kind, required_by_bundle, active)
VALUES
    ('00000000-0000-0000-0000-0000000e0010', 'error_notify_hook_port_logger_sink', 'hook_port',
     'backend_error_notify_bundle', true)
ON CONFLICT (policy_key) DO UPDATE
    SET port_kind = EXCLUDED.port_kind,
        required_by_bundle = EXCLUDED.required_by_bundle,
        active = EXCLUDED.active,
        updated_at = now();

INSERT INTO topology.external_port_policy_steps
    (policy_step_id, policy_id, step_order, operation_key, step_config, active)
VALUES
    ('00000000-0000-0000-0000-0000000e0011', '00000000-0000-0000-0000-0000000e0010', 1,
     'append_runtime_event_log', '{"event_type":"backend_error_notify_delivered"}', true)
ON CONFLICT (policy_id, step_order) DO UPDATE
    SET operation_key = EXCLUDED.operation_key,
        step_config = EXCLUDED.step_config,
        active = EXCLUDED.active,
        updated_at = now();
