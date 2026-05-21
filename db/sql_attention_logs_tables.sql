-- =============================================================================
-- sql_attention_logs_tables.sql
-- SQL Attention physical tables (design contract implementation surface)
--
-- Scope of this file:
--   - Implement registry-aware physical schema: logs.current / logs.registry_current / logs.attention.
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
-- Physical log-pressure current (regenerable projection/cache). topN physical items × signal axes basis (initial axes: table/column/ui).
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
  'Physical current for log-pressure aggregation. Regenerable projection/cache, not append-only archive evidence.';



-- ---------------------------------------------------------------------------
-- logs.registry_current
-- Registry-side population/phase-basis current for z-score and phase distance (N×N exploration plane).
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.registry_current (
    registry_current_id    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    registry_kind          TEXT        NOT NULL,
    registry_table         TEXT        NOT NULL,
    registry_id            TEXT        NOT NULL,
    basis_window           TEXT        NOT NULL,
    matrix_shape           TEXT,
    registry_matrix_json   JSONB       NOT NULL DEFAULT '{}'::jsonb,
    population_count       BIGINT      NOT NULL DEFAULT 0,
    population_recordcount BIGINT      NOT NULL DEFAULT 0,
    axis_population_json   JSONB       NOT NULL DEFAULT '{}'::jsonb,
    axis_z_score_json      JSONB       NOT NULL DEFAULT '{}'::jsonb,
    phase_basis_json       JSONB       NOT NULL DEFAULT '{}'::jsonb,
    evaluated_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_logs_registry_current_basis
      UNIQUE (registry_kind, registry_table, registry_id, basis_window)
);

COMMENT ON TABLE logs.registry_current IS
  'Registry-side population current / phase-basis current for z-score and phase movement distance. Projection cache only; no adoption/mutation.';

COMMENT ON COLUMN logs.registry_current.registry_matrix_json IS
  'Registry exploration plane cache. Relation-registry calculation-local zero padding metadata, when needed, should remain in evidence_json/metadata and not as dedicated persistent padding columns.';

-- ---------------------------------------------------------------------------
-- logs.attention
-- Append-only evidence log for physical current × registry exploration plane.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.attention (
    attention_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    current_id            UUID        NOT NULL REFERENCES logs.current(current_id) ON DELETE RESTRICT,
    registry_current_id   UUID        NOT NULL REFERENCES logs.registry_current(registry_current_id) ON DELETE RESTRICT,
    source_set_id         TEXT        NOT NULL,
    statistics_json       JSONB       NOT NULL DEFAULT '{}'::jsonb,
    ema_score             DOUBLE PRECISION,
    l2_norm               DOUBLE PRECISION NOT NULL DEFAULT 0,
    vector_json           JSONB       NOT NULL DEFAULT '{}'::jsonb,
    phase_vector_json     JSONB       NOT NULL DEFAULT '{}'::jsonb,
    permutation_key       TEXT,
    registry_kind         TEXT        NOT NULL,
    registry_table        TEXT        NOT NULL,
    registry_id           TEXT        NOT NULL,
    neighbor_score        DOUBLE PRECISION NOT NULL DEFAULT 0,
    hit_rank              INTEGER,
    score_band            TEXT        NOT NULL DEFAULT 'evidence_only',
    evidence_json         JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    archive_policy        TEXT        NOT NULL DEFAULT 'required'
);

COMMENT ON TABLE logs.attention IS
  'Registry-exploration evidence log linked to physical logs.current. Append-only/archive-required; keeps statistics, attention, and phase-attention meanings separated.';

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
CREATE INDEX IF NOT EXISTS idx_logs_attention_registry_table
  ON logs.attention (registry_kind, registry_table);
CREATE INDEX IF NOT EXISTS idx_logs_attention_created_at
  ON logs.attention (created_at);
CREATE INDEX IF NOT EXISTS idx_logs_attention_l2_norm
  ON logs.attention (l2_norm);
CREATE INDEX IF NOT EXISTS idx_logs_attention_neighbor_score
  ON logs.attention (neighbor_score);


CREATE INDEX IF NOT EXISTS idx_logs_registry_current_registry_ref
  ON logs.registry_current (registry_kind, registry_id);
CREATE INDEX IF NOT EXISTS idx_logs_registry_current_registry_table_window
  ON logs.registry_current (registry_kind, registry_table, basis_window);

CREATE INDEX IF NOT EXISTS idx_logs_registry_current_updated_at
  ON logs.registry_current (updated_at);
CREATE INDEX IF NOT EXISTS idx_logs_attention_registry_current_id
  ON logs.attention (registry_current_id);
CREATE INDEX IF NOT EXISTS idx_logs_attention_score_band
  ON logs.attention (score_band);
