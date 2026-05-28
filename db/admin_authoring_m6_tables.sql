-- M6 self-hosted admin authoring MVP bundle 1
-- Bundle: admin_csv_json_import_validate_preview_apply

CREATE TABLE IF NOT EXISTS admin_authoring_manifest (
  manifest_id UUID PRIMARY KEY,
  manifest_key TEXT NOT NULL UNIQUE,
  target_table TEXT NOT NULL,
  field_definitions_jsonb JSONB NOT NULL,
  aggregation_rules_jsonb JSONB NOT NULL DEFAULT '{}'::jsonb,
  active BOOLEAN NOT NULL DEFAULT true,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS admin_import_snapshot (
  snapshot_id UUID PRIMARY KEY,
  source_type TEXT NOT NULL CHECK (source_type IN ('csv','json')),
  file_name TEXT NOT NULL,
  manifest_id UUID NOT NULL REFERENCES admin_authoring_manifest(manifest_id),
  raw_header_jsonb JSONB NOT NULL DEFAULT '[]'::jsonb,
  raw_rows_jsonb JSONB NOT NULL DEFAULT '[]'::jsonb,
  validation_summary_jsonb JSONB NOT NULL DEFAULT '{}'::jsonb,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS admin_import_records (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  manifest_id UUID NOT NULL REFERENCES admin_authoring_manifest(manifest_id),
  snapshot_id UUID NOT NULL REFERENCES admin_import_snapshot(snapshot_id),
  records JSONB NOT NULL,
  status TEXT NOT NULL CHECK (status IN ('valid','invalid')),
  validation_errors_jsonb JSONB NOT NULL DEFAULT '[]'::jsonb,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS admin_import_apply_log (
  apply_log_id UUID PRIMARY KEY,
  snapshot_id UUID NOT NULL REFERENCES admin_import_snapshot(snapshot_id),
  applied_record_count INTEGER NOT NULL DEFAULT 0,
  applied_diff_jsonb JSONB NOT NULL DEFAULT '{}'::jsonb,
  status TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_admin_import_snapshot_manifest
  ON admin_import_snapshot(manifest_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_admin_import_records_snapshot
  ON admin_import_records(snapshot_id, status);
