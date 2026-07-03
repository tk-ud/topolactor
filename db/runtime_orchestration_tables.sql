CREATE SCHEMA IF NOT EXISTS runtime_orchestration;

CREATE OR REPLACE FUNCTION runtime_orchestration.jsonb_numeric_add(left_json jsonb, right_json jsonb)
RETURNS jsonb LANGUAGE sql IMMUTABLE AS $$
  SELECT COALESCE(jsonb_object_agg(key, to_jsonb(COALESCE((left_json ->> key)::numeric, 0) + COALESCE((right_json ->> key)::numeric, 0))), '{}'::jsonb)
  FROM (SELECT key FROM jsonb_object_keys(left_json) key UNION SELECT key FROM jsonb_object_keys(right_json) key) keys;
$$;

CREATE TABLE IF NOT EXISTS runtime_orchestration.aggregate_trigger_definitions (
  trigger_definition_id UUID PRIMARY KEY,
  processing_function_id TEXT NOT NULL,
  operation_definition_id TEXT NOT NULL,
  accepted_event_schema_ref TEXT NOT NULL,
  allowed_source_kinds_json JSONB NOT NULL,
  materialization_policy_ref TEXT NOT NULL,
  trigger_source_kind TEXT NOT NULL CHECK (trigger_source_kind IN ('cron','hook','client')),
  trigger_source_detail_kind TEXT NOT NULL CHECK (trigger_source_detail_kind IN ('client_operation_event','hook_event','scheduled_cron_event','runtime_function_event')),
  execution_scope TEXT NOT NULL CHECK (execution_scope IN ('single_event','aggregate_key_partition','operation_instance','tenant_or_workspace_partition')),
  transaction_boundary TEXT NOT NULL CHECK (transaction_boundary IN ('event_append_only','event_append_and_aggregate_upsert','event_append_aggregate_upsert_and_materialization')),
  aggregate_target_binding_json JSONB NOT NULL,
  conflict_key_fields_json JSONB NOT NULL,
  delta_map_json JSONB NOT NULL,
  threshold_policy_json JSONB NOT NULL,
  materialization_target_binding_json JSONB NOT NULL,
  materialization_payload_map_json JSONB NOT NULL,
  approval_policy TEXT NOT NULL CHECK (approval_policy IN ('auto_materialize_when_threshold_passes','require_backend_approval_before_materialization','require_human_approval_before_materialization')),
  active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS runtime_orchestration.aggregate_trigger_event_evidence (
  trigger_definition_id UUID NOT NULL,
  source_kind TEXT NOT NULL,
  source_id TEXT NOT NULL,
  correlation_id TEXT NOT NULL,
  event_fingerprint TEXT NOT NULL,
  event_id TEXT NOT NULL,
  canonical_trigger_kind TEXT NOT NULL CHECK (canonical_trigger_kind IN ('cron','hook','client')),
  trigger_source_detail_kind TEXT NOT NULL CHECK (trigger_source_detail_kind <> 'scheduler_event'),
  event_payload JSONB NOT NULL,
  actor TEXT,
  source TEXT,
  observed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (source_kind, source_id, correlation_id, event_fingerprint)
);

CREATE TABLE IF NOT EXISTS runtime_orchestration.aggregate_trigger_current (
  trigger_definition_id UUID NOT NULL,
  conflict_key_hash TEXT NOT NULL,
  conflict_key TEXT NOT NULL,
  counters JSONB NOT NULL DEFAULT '{}'::jsonb,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (trigger_definition_id, conflict_key_hash)
);

CREATE TABLE IF NOT EXISTS runtime_orchestration.aggregate_trigger_materialization_evidence (
  materialization_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  trigger_definition_id UUID NOT NULL,
  conflict_key_hash TEXT NOT NULL,
  conflict_key TEXT NOT NULL,
  event_id TEXT NOT NULL,
  materialization_target_source TEXT NOT NULL,
  materialization_target_id TEXT NOT NULL,
  payload_fingerprint TEXT NOT NULL,
  materialization_payload_map_json JSONB NOT NULL,
  materialized_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (trigger_definition_id, materialization_target_source, materialization_target_id, conflict_key_hash, payload_fingerprint)
);
