-- =============================================================================
-- legacy_mirror_tables.sql
--
-- Observation-layer mirror for existing-system island embedding.
-- This surface is NOT source-of-truth and must not take write authority by default.
--
-- Minimal mirror shape:
--   table_name, row_id, row_state, column_data_jsonb, relation_hint_jsonb, captured_at
--
-- Minimal change event intake archive:
--   table_name, row_id, operation, changed_data_jsonb/diff_jsonb, actor/source/occurred_at
-- =============================================================================

CREATE TABLE IF NOT EXISTS legacy_table_snapshot (
    snapshot_id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name            TEXT        NOT NULL,
    row_id                TEXT        NOT NULL,
    row_state             TEXT        NOT NULL DEFAULT 'active',
    column_data_jsonb     JSONB       NOT NULL,
    relation_hint_jsonb   JSONB,
    captured_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    mirror_mode           TEXT        NOT NULL DEFAULT 'READ_ONLY_MIRROR',
    CONSTRAINT uq_legacy_table_snapshot_identity UNIQUE (table_name, row_id),
    CONSTRAINT ck_legacy_table_snapshot_mirror_mode CHECK (
      mirror_mode IN ('READ_ONLY_MIRROR', 'SHADOW_WRITE_MIRROR', 'DUAL_WRITE_GUARDED', 'PROMOTED_AUTHORITY')
    )
);

COMMENT ON TABLE legacy_table_snapshot IS
  'Existing physical table snapshot mirror. Observation layer only; not canonical source-of-truth.';

COMMENT ON COLUMN legacy_table_snapshot.table_name IS
  'Registry resolution key candidate for legacy surface onboarding.';

CREATE INDEX IF NOT EXISTS idx_legacy_table_snapshot_table_name
  ON legacy_table_snapshot (table_name);

CREATE INDEX IF NOT EXISTS idx_legacy_table_snapshot_captured_at
  ON legacy_table_snapshot (captured_at DESC);

CREATE INDEX IF NOT EXISTS idx_legacy_table_snapshot_column_data_jsonb
  ON legacy_table_snapshot USING GIN (column_data_jsonb);

CREATE TABLE IF NOT EXISTS legacy_change_event (
    event_id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name            TEXT        NOT NULL,
    row_id                TEXT        NOT NULL,
    operation             TEXT        NOT NULL,
    changed_data_jsonb    JSONB,
    diff_jsonb            JSONB,
    actor                 TEXT,
    source                TEXT,
    occurred_at           TIMESTAMPTZ,
    received_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_legacy_change_event_operation CHECK (
      operation IN ('create', 'update', 'delete', 'transition')
    ),
    CONSTRAINT ck_legacy_change_event_payload_presence CHECK (
      changed_data_jsonb IS NOT NULL OR diff_jsonb IS NOT NULL
    )
);

COMMENT ON TABLE legacy_change_event IS
  'Minimal intake archive for API-forked legacy change events used for hook/event dispatch.';

CREATE INDEX IF NOT EXISTS idx_legacy_change_event_table_row
  ON legacy_change_event (table_name, row_id, received_at DESC);
