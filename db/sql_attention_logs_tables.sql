-- =============================================================================
-- sql_attention_logs_tables.sql
-- SQL Attention physical tables (design contract implementation surface)
--
-- Scope of this file:
--   - Implement physical schema for logs.current and logs.attention.
--   - Implement indexes/constraints for query and linkage contracts.
--
-- Out of scope:
--   - refresh_logs_current implementation
--   - l2 norm watch implementation
--   - DB trigger implementation
--   - scheduler/runtime registry-neighbor exploration implementation
--   - phase_vector generation implementation
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS logs;

-- ---------------------------------------------------------------------------
-- logs.current
-- Regenerable projection/cache for SQL Attention calculation basis.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.current (
    current_id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_set_id         TEXT        NOT NULL,
    basis_window          TEXT        NOT NULL,
    physical_table_id     TEXT        NOT NULL,
    physical_table_name   TEXT        NOT NULL,
    basis_vector_json     JSONB       NOT NULL DEFAULT '{}'::jsonb,
    pressure_matrix_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    count_total           BIGINT      NOT NULL DEFAULT 0,
    recordcount_total     BIGINT      NOT NULL DEFAULT 0,
    l2_norm               DOUBLE PRECISION NOT NULL DEFAULT 0,
    norm_rank             INTEGER,
    norm_level            TEXT,
    previous_norm_level   TEXT,
    level_changed         BOOLEAN     NOT NULL DEFAULT false,
    dirty                 BOOLEAN     NOT NULL DEFAULT false,
    evaluated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_logs_current_source_table
      UNIQUE (source_set_id, basis_window, physical_table_id)
);

COMMENT ON TABLE logs.current IS
  'SQL Attention calculation basis memo. Regenerable projection/cache, not append-only archive evidence.';

-- ---------------------------------------------------------------------------
-- logs.attention
-- Append-only / archive-required evidence log.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.attention (
    attention_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    current_id            UUID        NOT NULL REFERENCES logs.current(current_id) ON DELETE RESTRICT,
    source_set_id         TEXT        NOT NULL,
    statistics_json       JSONB       NOT NULL DEFAULT '{}'::jsonb,
    ema_score             DOUBLE PRECISION,
    l2_norm               DOUBLE PRECISION,
    vector_json           JSONB       NOT NULL DEFAULT '{}'::jsonb,
    phase_vector_json     JSONB       NOT NULL DEFAULT '{}'::jsonb,
    permutation_key       TEXT,
    registry_kind         TEXT,
    registry_table        TEXT,
    registry_id           TEXT,
    neighbor_score        DOUBLE PRECISION,
    hit_rank              INTEGER,
    evidence_json         JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    archive_policy        TEXT        NOT NULL DEFAULT 'required'
);

COMMENT ON TABLE logs.attention IS
  'SQL Attention evidence log. Append-only/archive-required. Keeps statistics, attention, and phase-attention meanings separated.';

CREATE INDEX IF NOT EXISTS idx_logs_current_source_table
  ON logs.current (source_set_id, physical_table_id);
CREATE INDEX IF NOT EXISTS idx_logs_current_level_rank
  ON logs.current (level_changed, norm_rank);
CREATE INDEX IF NOT EXISTS idx_logs_current_updated_at
  ON logs.current (updated_at);

CREATE INDEX IF NOT EXISTS idx_logs_attention_current_id
  ON logs.attention (current_id);
CREATE INDEX IF NOT EXISTS idx_logs_attention_source_set_id
  ON logs.attention (source_set_id);
CREATE INDEX IF NOT EXISTS idx_logs_attention_registry_ref
  ON logs.attention (registry_kind, registry_id);
CREATE INDEX IF NOT EXISTS idx_logs_attention_created_at
  ON logs.attention (created_at);
CREATE INDEX IF NOT EXISTS idx_logs_attention_l2_norm
  ON logs.attention (l2_norm);
CREATE INDEX IF NOT EXISTS idx_logs_attention_neighbor_score
  ON logs.attention (neighbor_score);
