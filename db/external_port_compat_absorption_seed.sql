-- =============================================================================
-- External port compatibility operation absorption seed
--
-- Canonicalizes legacy ExternalPortPolicyStep compatibility operation entries by
-- binding each compatibility step to an abstract_function manifest. This file is
-- applied after seed_empty.sql so existing rows are upgraded without resurrecting
-- implementation-first policy bodies.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Abstract function manifests for compatibility-entry absorption.
-- -----------------------------------------------------------------------------
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    -- no-op build adapters: build_http_request is retained only as a compatibility entry.
    ('00000000-0000-0000-0000-00000000af20', 'external_port.compat_build.external_seed',       'external_port_runtime', 'external-port-substrate-seed-coding', '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af21', 'external_port.compat_build.file_storage',        'external_port_runtime', 'file_storage_bundle',                  '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af22', 'external_port.compat_build.email',               'external_port_runtime', 'email_bundle',                         '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af23', 'external_port.compat_build.audit_approval',      'external_port_runtime', 'audit_approval_bundle',                '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),

    -- canonical http_request primitive manifests used by send_http compatibility entries.
    ('00000000-0000-0000-0000-00000000af24', 'external_port.http.generic_get',                 'external_port_runtime', 'external-port-substrate-seed-coding', '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af25', 'external_port.http.generic_post',                'external_port_runtime', 'external-port-substrate-seed-coding', '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af26', 'external_port.http.file_storage_put',            'external_port_runtime', 'file_storage_bundle',                  '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af27', 'external_port.http.email_post',                  'external_port_runtime', 'email_bundle',                         '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af28', 'external_port.http.audit_approval_post',         'external_port_runtime', 'audit_approval_bundle',                '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),

    -- scheduler enqueue primitive manifests.
    ('00000000-0000-0000-0000-00000000af29', 'external_port.scheduler_enqueue.external_seed',  'external_port_runtime', 'external-port-substrate-seed-coding', '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af2a', 'external_port.scheduler_enqueue.stripe',         'external_port_runtime', 'stripe_bundle',                       '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af2b', 'external_port.scheduler_enqueue.webhook_inbox',  'external_port_runtime', 'webhook_inbox_bundle',                '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af2c', 'external_port.scheduler_enqueue.job_scheduler',  'external_port_runtime', 'job_scheduler_bundle',                 '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),

    -- event_log primitive manifests.
    ('00000000-0000-0000-0000-00000000af2d', 'external_port.event.external_seed',              'external_port_runtime', 'external-port-substrate-seed-coding', '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af2e', 'external_port.event.checksum_verified',          'external_port_runtime', 'file_storage_bundle',                  '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af2f', 'external_port.event.export_job_initiated',       'external_port_runtime', 'file_storage_bundle',                  '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af30', 'external_port.event.file_write_completed',       'external_port_runtime', 'file_storage_bundle',                  '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af31', 'external_port.event.signed_url_generated',       'external_port_runtime', 'file_storage_bundle',                  '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af32', 'external_port.event.email_response_completed',   'external_port_runtime', 'email_bundle',                         '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af33', 'external_port.event.stripe_hook_received',       'external_port_runtime', 'stripe_bundle',                        '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af34', 'external_port.event.webhook_inbox_received',     'external_port_runtime', 'webhook_inbox_bundle',                 '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af35', 'external_port.event.job_scheduler_access_seen',  'external_port_runtime', 'job_scheduler_bundle',                  '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af36', 'external_port.event.job_scheduler_hook_seen',    'external_port_runtime', 'job_scheduler_bundle',                  '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af37', 'external_port.event.audit_approval_sent',        'external_port_runtime', 'audit_approval_bundle',                 '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af38', 'external_port.event.export_sftp_completed',      'external_port_runtime', 'export_sftp_bundle',                    '{}', ARRAY['credential','signed_url','bucket','endpoint','storage_path','storage_ref','raw_storage_ref'], true),
    ('00000000-0000-0000-0000-00000000af39', 'external_port.event.credential_refreshed',       'external_port_runtime', 'external_credential_vault_refresh',     '{}', ARRAY['credential','credential_payload','decrypted_payload','plaintext_payload','decrypted_credential_payload','token_response','token_body'], true)
ON CONFLICT (abstract_function_id) DO UPDATE
    SET function_key         = EXCLUDED.function_key,
        runtime_lane         = EXCLUDED.runtime_lane,
        authority_scope      = EXCLUDED.authority_scope,
        output_shape         = EXCLUDED.output_shape,
        projection_deny_keys = EXCLUDED.projection_deny_keys,
        active               = EXCLUDED.active,
        updated_at           = now();

INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    -- no-op projection primitive: explicit compatibility adapter placeholder, no runtime side effect.
    ('00000000-0000-0000-0000-00000000bf20', '00000000-0000-0000-0000-00000000af20', 1, 'projection', '{}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf21', '00000000-0000-0000-0000-00000000af21', 1, 'projection', '{}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf22', '00000000-0000-0000-0000-00000000af22', 1, 'projection', '{}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf23', '00000000-0000-0000-0000-00000000af23', 1, 'projection', '{}', NULL, true),

    ('00000000-0000-0000-0000-00000000bf24', '00000000-0000-0000-0000-00000000af24', 1, 'http_request', '{"url":"env:EXTERNAL_PORT_GENERIC_HTTP_GET_ENDPOINT_REF","method":"GET"}', 'http_response', true),
    ('00000000-0000-0000-0000-00000000bf25', '00000000-0000-0000-0000-00000000af25', 1, 'http_request', '{"url":"env:EXTERNAL_PORT_GENERIC_HTTP_POST_ENDPOINT_REF","method":"POST"}', 'http_response', true),
    ('00000000-0000-0000-0000-00000000bf26', '00000000-0000-0000-0000-00000000af26', 1, 'http_request', '{"url":"env:FILE_STORAGE_ENDPOINT_REF","method":"PUT"}', 'http_response', true),
    ('00000000-0000-0000-0000-00000000bf27', '00000000-0000-0000-0000-00000000af27', 1, 'http_request', '{"url":"env:SMTP_HOST_REF","method":"POST"}', 'http_response', true),
    ('00000000-0000-0000-0000-00000000bf28', '00000000-0000-0000-0000-00000000af28', 1, 'http_request', '{"url":"env:APPROVAL_NOTIFICATION_ENDPOINT_REF","method":"POST"}', 'http_response', true),

    ('00000000-0000-0000-0000-00000000bf29', '00000000-0000-0000-0000-00000000af29', 1, 'scheduler_enqueue', '{}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf2a', '00000000-0000-0000-0000-00000000af2a', 1, 'scheduler_enqueue', '{}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf2b', '00000000-0000-0000-0000-00000000af2b', 1, 'scheduler_enqueue', '{}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf2c', '00000000-0000-0000-0000-00000000af2c', 1, 'scheduler_enqueue', '{}', NULL, true),

    ('00000000-0000-0000-0000-00000000bf2d', '00000000-0000-0000-0000-00000000af2d', 1, 'event_log', '{"event_type":"external_port_seed_event"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf2e', '00000000-0000-0000-0000-00000000af2e', 1, 'event_log', '{"event_type":"checksum_verified","entity_ref_key":"ChecksumValue"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf2f', '00000000-0000-0000-0000-00000000af2f', 1, 'event_log', '{"event_type":"export_job_initiated","entity_ref_key":"ExportJobId"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf30', '00000000-0000-0000-0000-00000000af30', 1, 'event_log', '{"event_type":"file_write_completed","entity_ref_key":"FileArtifactId"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf31', '00000000-0000-0000-0000-00000000af31', 1, 'event_log', '{"event_type":"signed_url_generated","entity_ref_key":"AuthorizationKey"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf32', '00000000-0000-0000-0000-00000000af32', 1, 'event_log', '{"event_type":"email_response_completed"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf33', '00000000-0000-0000-0000-00000000af33', 1, 'event_log', '{"event_type":"stripe_hook_received"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf34', '00000000-0000-0000-0000-00000000af34', 1, 'event_log', '{"event_type":"webhook_inbox_received"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf35', '00000000-0000-0000-0000-00000000af35', 1, 'event_log', '{"event_type":"job_scheduler_access_seen"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf36', '00000000-0000-0000-0000-00000000af36', 1, 'event_log', '{"event_type":"job_scheduler_hook_seen"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf37', '00000000-0000-0000-0000-00000000af37', 1, 'event_log', '{"event_type":"audit_approval_sent"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf38', '00000000-0000-0000-0000-00000000af38', 1, 'event_log', '{"event_type":"export_sftp_completed"}', NULL, true),
    ('00000000-0000-0000-0000-00000000bf39', '00000000-0000-0000-0000-00000000af39', 1, 'event_log', '{"event_type":"credential_token_refreshed"}', NULL, true)
ON CONFLICT (abstract_function_id, step_order) DO UPDATE
    SET primitive_key       = EXCLUDED.primitive_key,
        step_config         = EXCLUDED.step_config,
        result_context_key  = EXCLUDED.result_context_key,
        active              = EXCLUDED.active,
        updated_at          = now();

-- -----------------------------------------------------------------------------
-- Authority bindings: each abstract function explicitly names the policy keys it
-- may serve. This keeps compatibility entries from becoming generic backdoors.
-- -----------------------------------------------------------------------------
INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    ('00000000-0000-0000-0000-00000000af20', 'policy', 'external_access_port_generic_http', true),
    ('00000000-0000-0000-0000-00000000af20', 'policy', 'external_response_port_generic_http', true),
    ('00000000-0000-0000-0000-00000000af21', 'policy', 'file_storage_access_port_generic', true),
    ('00000000-0000-0000-0000-00000000af21', 'policy', 'file_storage_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af22', 'policy', 'email_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af23', 'policy', 'audit_approval_response_port_generic', true),

    ('00000000-0000-0000-0000-00000000af24', 'policy', 'external_access_port_generic_http', true),
    ('00000000-0000-0000-0000-00000000af25', 'policy', 'external_response_port_generic_http', true),
    ('00000000-0000-0000-0000-00000000af26', 'policy', 'file_storage_access_port_generic', true),
    ('00000000-0000-0000-0000-00000000af26', 'policy', 'file_storage_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af27', 'policy', 'email_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af28', 'policy', 'audit_approval_response_port_generic', true),

    ('00000000-0000-0000-0000-00000000af29', 'policy', 'external_hook_port_scheduler_boundary', true),
    ('00000000-0000-0000-0000-00000000af2a', 'policy', 'stripe_hook_port_scheduler_boundary', true),
    ('00000000-0000-0000-0000-00000000af2b', 'policy', 'webhook_inbox_hook_port_scheduler', true),
    ('00000000-0000-0000-0000-00000000af2c', 'policy', 'job_scheduler_hook_port_enqueue', true),

    ('00000000-0000-0000-0000-00000000af2d', 'policy', 'external_hook_port_scheduler_boundary', true),
    ('00000000-0000-0000-0000-00000000af2e', 'policy', 'file_storage_access_port_generic', true),
    ('00000000-0000-0000-0000-00000000af2e', 'policy', 'file_storage_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af2f', 'policy', 'file_storage_access_port_generic', true),
    ('00000000-0000-0000-0000-00000000af2f', 'policy', 'file_storage_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af30', 'policy', 'file_storage_access_port_generic', true),
    ('00000000-0000-0000-0000-00000000af30', 'policy', 'file_storage_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af31', 'policy', 'file_storage_access_port_generic', true),
    ('00000000-0000-0000-0000-00000000af31', 'policy', 'file_storage_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af32', 'policy', 'email_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af33', 'policy', 'stripe_hook_port_scheduler_boundary', true),
    ('00000000-0000-0000-0000-00000000af34', 'policy', 'webhook_inbox_hook_port_scheduler', true),
    ('00000000-0000-0000-0000-00000000af35', 'policy', 'job_scheduler_access_port_generic', true),
    ('00000000-0000-0000-0000-00000000af36', 'policy', 'job_scheduler_hook_port_enqueue', true),
    ('00000000-0000-0000-0000-00000000af37', 'policy', 'audit_approval_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af38', 'policy', 'export_sftp_response_port_generic', true),
    ('00000000-0000-0000-0000-00000000af39', 'policy', 'external_credential_vault_refresh', true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO UPDATE
    SET active = EXCLUDED.active,
        updated_at = now();

-- -----------------------------------------------------------------------------
-- Upgrade legacy compatibility operation rows with explicit abstract_function_key.
-- Operation keys remain compatibility entries for migration, but behavior authority
-- is now owned by the abstract function manifests above.
-- -----------------------------------------------------------------------------
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.compat_build.external_seed'      WHERE policy_step_id IN ('00000000-0000-0000-0000-000000000103','00000000-0000-0000-0000-000000000203');
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.compat_build.file_storage'       WHERE policy_step_id IN ('00000000-0000-0000-0000-000000000405','00000000-0000-0000-0000-000000000415');
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.compat_build.email'              WHERE policy_step_id IN ('00000000-0000-0000-0000-000000000423');
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.compat_build.audit_approval'     WHERE policy_step_id IN ('00000000-0000-0000-0000-000000000463');

UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.http.generic_get'                WHERE policy_step_id = '00000000-0000-0000-0000-000000000104';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.http.generic_post'               WHERE policy_step_id = '00000000-0000-0000-0000-000000000204';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.http.file_storage_put'           WHERE policy_step_id IN ('00000000-0000-0000-0000-0000000004a1','00000000-0000-0000-0000-0000000004b1');
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.http.email_post'                 WHERE policy_step_id = '00000000-0000-0000-0000-000000000424';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.http.audit_approval_post'        WHERE policy_step_id = '00000000-0000-0000-0000-000000000464';

UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.scheduler_enqueue.external_seed' WHERE policy_step_id = '00000000-0000-0000-0000-000000000304';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.scheduler_enqueue.stripe'        WHERE policy_step_id = '00000000-0000-0000-0000-000000000434';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.scheduler_enqueue.webhook_inbox' WHERE policy_step_id = '00000000-0000-0000-0000-000000000444';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.scheduler_enqueue.job_scheduler' WHERE policy_step_id = '00000000-0000-0000-0000-000000000482';

UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.external_seed'             WHERE policy_step_id = '00000000-0000-0000-0000-000000000305';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.checksum_verified'         WHERE policy_step_id IN ('00000000-0000-0000-0000-000000000490','00000000-0000-0000-0000-000000000494');
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.export_job_initiated'      WHERE policy_step_id IN ('00000000-0000-0000-0000-000000000491','00000000-0000-0000-0000-000000000495');
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.file_write_completed'      WHERE policy_step_id IN ('00000000-0000-0000-0000-000000000492','00000000-0000-0000-0000-000000000496');
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.signed_url_generated'      WHERE policy_step_id IN ('00000000-0000-0000-0000-000000000493','00000000-0000-0000-0000-000000000497');
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.email_response_completed'  WHERE policy_step_id = '00000000-0000-0000-0000-000000000425';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.stripe_hook_received'      WHERE policy_step_id = '00000000-0000-0000-0000-000000000435';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.webhook_inbox_received'    WHERE policy_step_id = '00000000-0000-0000-0000-000000000445';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.job_scheduler_access_seen' WHERE policy_step_id = '00000000-0000-0000-0000-000000000453';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.job_scheduler_hook_seen'   WHERE policy_step_id = '00000000-0000-0000-0000-000000000483';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.audit_approval_sent'       WHERE policy_step_id = '00000000-0000-0000-0000-000000000465';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.export_sftp_completed'     WHERE policy_step_id = '00000000-0000-0000-0000-000000000473';
UPDATE topology.external_port_policy_steps SET abstract_function_key = 'external_port.event.credential_refreshed'      WHERE policy_step_id = '00000000-0000-0000-0000-000000000506';
