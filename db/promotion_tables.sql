-- =============================================================================
-- promotion_tables.sql
-- Promotion policy tables: usage_metrics and promotion_candidates.
--
-- These tables support the data-driven promotion policy layer of the topology
-- system. Usage patterns are observed and recorded in usage_metrics; the
-- promotion engine analyses them and creates promotion_candidates that suggest
-- structural changes (indexes, generated columns, registry promotions, or
-- physical table promotions).
--
-- NOTE: Actual migration execution is out of scope for this skeleton.
--       promotion_candidates records are advisory suggestions only.
--       A separate migration/approval workflow is responsible for acting on
--       approved candidates.
-- =============================================================================


-- ---------------------------------------------------------------------------
-- usage_metrics
-- Records observed access patterns against topology tables and JSONB paths.
-- Used by the promotion engine to identify hot columns, frequent filters,
-- and aggregation targets that may warrant structural promotion.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.usage_metrics (
    metric_id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name         TEXT,                              -- e.g. 'entities', 'structure_maps'
    column_name        TEXT,                              -- physical column observed, if any
    jsonb_path         TEXT,                              -- JSONB path expression, e.g. '$.type'
    read_count         BIGINT      NOT NULL DEFAULT 0,   -- number of read accesses observed
    filter_count       BIGINT      NOT NULL DEFAULT 0,   -- number of WHERE/filter uses
    aggregation_count  BIGINT      NOT NULL DEFAULT 0,   -- number of GROUP BY / aggregate uses
    join_count         BIGINT      NOT NULL DEFAULT 0,   -- number of JOIN uses
    recorded_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE logs.usage_metrics IS
    'Records observed access patterns on topology tables and JSONB paths. '
    'Consumed by the promotion engine to generate promotion_candidates. '
    'Rows are advisory observations — no structural changes are made here. '
    'SQL Attention logs alignment: this table can be used as conditional logs.candidate pressure source '
    'when table/column/jsonb_path/axis pressure tracking is preserved.';

COMMENT ON COLUMN logs.usage_metrics.jsonb_path IS
    'JSONB path expression (e.g. ''$.category'', ''$.metadata.owner'') identifying '
    'the specific path within a JSONB column that was accessed. NULL for '
    'non-JSONB column observations.';

COMMENT ON COLUMN logs.usage_metrics.read_count IS
    'Cumulative count of read accesses (SELECT) observed for this '
    'table/column/path combination since recorded_at.';

COMMENT ON COLUMN logs.usage_metrics.filter_count IS
    'Cumulative count of times this path/column was used in a WHERE clause '
    'or filter expression.';

COMMENT ON COLUMN logs.usage_metrics.aggregation_count IS
    'Cumulative count of times this path/column appeared in GROUP BY, '
    'COUNT, SUM, or other aggregate contexts.';

COMMENT ON COLUMN logs.usage_metrics.join_count IS
    'Cumulative count of times this path/column was used as a JOIN key.';

CREATE INDEX IF NOT EXISTS idx_usage_metrics_table_name
    ON logs.usage_metrics (table_name);

CREATE INDEX IF NOT EXISTS idx_usage_metrics_recorded_at
    ON logs.usage_metrics (recorded_at DESC);


-- ---------------------------------------------------------------------------
-- promotion_candidates
-- Advisory records produced by the promotion engine.
-- Each candidate references a usage_metrics observation and carries a
-- suggested structural action plus a snapshot of the manifest at the time
-- of suggestion.
--
-- NOTE: Actual migration execution is out of scope for this skeleton.
--       Approved candidates must be acted on by a separate migration workflow.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.promotion_candidates (
    promotion_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    metric_id          UUID        REFERENCES logs.usage_metrics (metric_id) ON DELETE SET NULL,
    promotion_action   TEXT,                              -- see COMMENT below for valid values
    status             TEXT        NOT NULL DEFAULT 'pending'
                           CHECK (status IN ('pending', 'approved', 'rejected')),
    manifest_snapshot  JSONB       NOT NULL DEFAULT '{}', -- snapshot of topology manifest at suggestion time
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    reviewed_at        TIMESTAMPTZ,                       -- NULL until reviewed
    reviewed_by        TEXT                               -- identity of reviewer, if any
);

COMMENT ON TABLE logs.promotion_candidates IS
    'Advisory promotion suggestions produced by the promotion engine. '
    'References a usage_metrics observation and carries a structural action '
    'suggestion and topology manifest snapshot. '
    'IMPORTANT: Actual migration execution is out of scope for this skeleton. '
    'Approved candidates must be acted on by a separate migration/approval workflow. '
    'SQL Attention logs alignment: conditional logs.candidate source when status lifecycle '
    '(pending/approved/rejected) and pressure-axis traceability are preserved for audit/archive.';

COMMENT ON COLUMN logs.promotion_candidates.promotion_action IS
    'Suggested structural action. Expected values (non-exhaustive): '
    '  suggest_index                 — add a B-tree or GIN index '
    '  suggest_generated_column      — promote a JSONB path to a generated column '
    '  suggest_registry_promotion    — promote a JSONB value into a registry table '
    '  suggest_physical_table_promotion — promote a JSONB sub-document to its own table';

COMMENT ON COLUMN logs.promotion_candidates.manifest_snapshot IS
    'JSONB snapshot of the relevant portion of the topology manifest at the '
    'time this candidate was created. Preserved for audit and rollback context.';

COMMENT ON COLUMN logs.promotion_candidates.reviewed_at IS
    'Timestamp when a human or automated reviewer changed status from pending. '
    'NULL means the candidate has not yet been reviewed.';

CREATE INDEX IF NOT EXISTS idx_promotion_candidates_status
    ON logs.promotion_candidates (status);

CREATE INDEX IF NOT EXISTS idx_promotion_candidates_metric_id
    ON logs.promotion_candidates (metric_id);

CREATE INDEX IF NOT EXISTS idx_promotion_candidates_created_at
    ON logs.promotion_candidates (created_at DESC);
