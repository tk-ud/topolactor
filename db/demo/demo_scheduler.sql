-- Demo scheduler seed.
-- Applied by demo/test compose AFTER canonical init (seed_empty.sql via 00-init.sql).
-- Do NOT include in seed_empty.sql or init.sql — these are demo-only entries.
--
-- Prerequisites (must exist from canonical init):
--   manifest 0000000000f0 (scheduler_jobs:list_settings, canonical seed_empty.sql entry)
--   topology.abstract_function_manifests, topology.scheduler_jobs, topology.scheduler_job_steps tables
--
-- NOTIFY path (proven by SchedulerJobRunnerTests):
--   SchedulerJobRunner cron poll → LoadActiveJobsAsync → TryExecuteJobAsync
--   → AbstractFunctionExecutor (demo.scheduler_projection) → projection_policy.notify_manifest_id
--   → DbNotifyRepository.NotifyAsync(manifestId=0000000000f0)
--   → DbNotifyListener LISTEN → RuntimeTimelineScheduler.EnqueueHookTrigger
--   → ManifestDispatcher (db_notify_projection_mapping → sse_projection_runtime)
--   → SseEventBroadcaster → frontend SSE event: { event: projection, data: {manifest_id:...} }

\set ON_ERROR_STOP on

-- ---------------------------------------------------------------------------
-- Abstract function manifest: demo.scheduler_projection (af11)
-- runtime_lane = scheduler_job_runtime; authority_scope = demo_scheduler_job
-- Primitive: projection — reads job_key and run_id from scheduler_context.
-- ---------------------------------------------------------------------------
INSERT INTO topology.abstract_function_manifests
    (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active)
VALUES
    ('00000000-0000-0000-0000-00000000af11', 'demo.scheduler_projection',
     'scheduler_job_runtime', 'demo_scheduler_job',
     '{"projection_result":"scheduler_projection"}',
     ARRAY['credential','credential_payload','decrypted_payload','plaintext_payload',
           'decrypted_credential_payload','token_response','token_body',
           'api_key','access_token','refresh_token','client_secret'],
     true)
ON CONFLICT (abstract_function_id) DO NOTHING;

-- Step: projection primitive (scheduler_context binding source, isolated from request payload)
INSERT INTO topology.abstract_function_steps
    (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active)
VALUES
    ('00000000-0000-0000-0000-00000000bf40', '00000000-0000-0000-0000-00000000af11', 1,
     'projection', '{}', 'scheduler_projection', true)
ON CONFLICT (abstract_function_step_id) DO NOTHING;

-- Input bindings: scheduler_context (not request payload — isolation boundary)
INSERT INTO topology.abstract_function_input_bindings
    (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active)
VALUES
    ('00000000-0000-0000-0000-00000000c050', '00000000-0000-0000-0000-00000000bf40',
     'job_key',      'scheduler_context', 'job_key',      true, false, true),
    ('00000000-0000-0000-0000-00000000c051', '00000000-0000-0000-0000-00000000bf40',
     'run_id',       'scheduler_context', 'run_id',       true, false, true),
    ('00000000-0000-0000-0000-00000000c052', '00000000-0000-0000-0000-00000000bf40',
     'trigger_kind', 'scheduler_context', 'trigger_kind', false, false, true)
ON CONFLICT (abstract_function_step_id, input_key) DO NOTHING;

-- Authority binding
INSERT INTO topology.abstract_function_authority_bindings
    (abstract_function_id, authority_kind, authority_ref, active)
VALUES
    ('00000000-0000-0000-0000-00000000af11', 'policy', 'demo_scheduler_job_policy', true)
ON CONFLICT (abstract_function_id, authority_kind, authority_ref) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Demo scheduler job: demo_schedule
-- schedule_policy_kind = cron: picked up by the SchedulerJobRunner poll loop.
-- trigger_kind = cron: identifies the trigger mechanism.
-- manual_run_allowed = true: can also be triggered via admin.
-- projection_policy.notify_manifest_id = 0000000000f0:
--   On successful completion, SchedulerJobRunner fires DB NOTIFY with this manifest_id.
--   DbNotifyListener routes it through the NOTIFY → SSE representative path.
-- ---------------------------------------------------------------------------
INSERT INTO topology.scheduler_jobs
    (scheduler_job_id, job_key, trigger_kind, schedule_policy_kind, manual_run_allowed,
     active, authority_scope, max_batch_size, lease_seconds,
     retry_policy, projection_policy, cron_expression, created_by)
VALUES
    ('00000000-0000-0000-0000-00000000c060', 'demo_schedule', 'cron', 'cron', true,
     true, 'demo_scheduler_job', 1, 60,
     '{"max_attempts":1,"backoff_seconds":0}',
     '{"allowed_result_keys":["scheduler_projection"],"notify_manifest_id":"00000000-0000-0000-0000-0000000000f0"}',
     '* * * * *', 'seed')
ON CONFLICT (scheduler_job_id) DO NOTHING;

-- Scheduler job step: step 1 → demo.scheduler_projection
INSERT INTO topology.scheduler_job_steps
    (scheduler_job_step_id, scheduler_job_id, step_order, abstract_function_key,
     input_binding, result_context_key, result_binding, on_error, active)
VALUES
    ('00000000-0000-0000-0000-00000000c061',
     '00000000-0000-0000-0000-00000000c060',
     1, 'demo.scheduler_projection',
     '{}', 'scheduler_projection', '{}', 'fail_run', true)
ON CONFLICT (scheduler_job_id, step_order) DO NOTHING;
