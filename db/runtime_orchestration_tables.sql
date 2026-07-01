CREATE SCHEMA IF NOT EXISTS runtime_orchestration;

CREATE OR REPLACE FUNCTION runtime_orchestration.jsonb_numeric_add(left_json jsonb, right_json jsonb)
RETURNS jsonb LANGUAGE sql IMMUTABLE AS $$
  SELECT COALESCE(jsonb_object_agg(key, to_jsonb(COALESCE((left_json ->> key)::numeric, 0) + COALESCE((right_json ->> key)::numeric, 0))), '{}'::jsonb)
  FROM (SELECT key FROM jsonb_object_keys(left_json) key UNION SELECT key FROM jsonb_object_keys(right_json) key) keys;
$$;

CREATE TABLE IF NOT EXISTS runtime_orchestration.aggregate_trigger_definition (
  definition_id UUID PRIMARY KEY,
  definition_json JSONB NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS runtime_orchestration.aggregate_trigger_event_log (
  definition_id UUID NOT NULL,
  event_id TEXT NOT NULL,
  trigger_kind TEXT NOT NULL CHECK (trigger_kind IN ('cron','hook','client')),
  source_detail_kind TEXT NOT NULL CHECK (source_detail_kind <> 'scheduler_event'),
  event_payload JSONB NOT NULL,
  actor TEXT,
  source TEXT,
  observed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (definition_id, event_id)
);

CREATE TABLE IF NOT EXISTS runtime_orchestration.aggregate_trigger_current (
  definition_id UUID NOT NULL,
  conflict_key TEXT NOT NULL,
  counters JSONB NOT NULL DEFAULT '{}'::jsonb,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (definition_id, conflict_key)
);

CREATE TABLE IF NOT EXISTS runtime_orchestration.aggregate_trigger_materialization_log (
  materialization_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  definition_id UUID NOT NULL,
  conflict_key TEXT NOT NULL,
  event_id TEXT NOT NULL,
  materialization_target_kind TEXT NOT NULL,
  materialization_target_id TEXT NOT NULL,
  payload_map JSONB NOT NULL,
  materialized_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (definition_id, conflict_key, materialization_target_kind, materialization_target_id)
);
