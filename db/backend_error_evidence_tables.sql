-- =============================================================================
-- backend_error_evidence_tables.sql
-- Backend-wide system error evidence substrate (design contract implementation surface).
--
-- SSOT: docs/design/backend-error-evidence-report-notify-ssot.yaml
--
-- Scope of this file:
--   - logs.error            : append-only system error evidence (backend execution substrate failures).
--   - logs.error_report     : derived aggregation VIEW over logs.error (not replacement evidence).
--   - logs.error_notify_queue: durable post-insert notify queue row store.
--   - logs.error AFTER INSERT trigger -> durable queue row + pg_notify('logs_error_inserted') wake-up only.
--   - current.error_report_projection: read-side admin projection of unresolved system error reports.
--
-- Boundary notes (do not violate):
--   - logs.error is ONLY for backend execution substrate system errors. DTO failures,
--     ordinary auth rejects, business policy rejects, and user input mistakes never append here.
--   - The AFTER INSERT trigger creates a durable queue row and emits a pg_notify wake-up signal
--     only. It must NOT capture errors itself or call external providers directly.
--   - topology.runtime_event_log remains external-port consumer domain-event evidence and is not
--     repurposed as a backend-wide error log.
--   - CI Attention is not wired here; logs.error is not routed into CI Attention.
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS logs;
-- current schema: read-side derived projections consumed by admin (read-only, never authored).
CREATE SCHEMA IF NOT EXISTS current;

-- ---------------------------------------------------------------------------
-- logs.error
-- Append-only system error evidence. One row per classified backend substrate failure.
-- Append-only evidence, not a current projection: rows are never updated in place.
-- A business transaction rollback must not erase already-classified evidence — the appender
-- writes on its own connection so its insert is independent of the failing business transaction.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.error (
    error_id        UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    occurred_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    origin_layer    TEXT        NOT NULL,   -- e.g. abstract_function_executor, runtime_executor, manifest_dispatcher
    boundary_key    TEXT        NOT NULL,   -- specific boundary identity within the origin layer
    function_key    TEXT,                   -- abstract function key, when applicable
    runtime_lane    TEXT,                   -- runtime lane, when applicable
    primitive_key   TEXT,                   -- failing primitive adapter key, when applicable
    step_order      INTEGER,                -- failing step order, when applicable
    error_kind      TEXT        NOT NULL,   -- classifier vocabulary (system_error / diagnostic_gap_substrate)
    error_code      TEXT        NOT NULL,   -- stable machine code (e.g. ATTRACTOR_RESOLVE_FAILED)
    exception_type  TEXT,                   -- CLR exception type name, when sourced from an exception
    message_public  TEXT        NOT NULL,   -- redacted, bounded human-readable message
    stack_hash      TEXT        NOT NULL,   -- stable hash for grouping (no raw stack persisted)
    severity        TEXT        NOT NULL DEFAULT 'error',
    retryable       BOOLEAN     NOT NULL DEFAULT false,
    evidence_json   JSONB       NOT NULL DEFAULT '{}'::jsonb,  -- redacted, bounded evidence
    archive_policy  TEXT        NOT NULL DEFAULT 'required'
);

COMMENT ON TABLE logs.error IS
  'Append-only backend system error evidence. ONLY backend execution substrate failures: invariant break, dependency failure, unexpected exception, repository/db unavailable, malformed runtime authority, primitive adapter failure. DTO failure / ordinary auth reject / business policy reject / user input miss never append here.';

CREATE INDEX IF NOT EXISTS idx_logs_error_occurred_at
  ON logs.error (occurred_at DESC);
CREATE INDEX IF NOT EXISTS idx_logs_error_stack_hash
  ON logs.error (stack_hash);
CREATE INDEX IF NOT EXISTS idx_logs_error_code
  ON logs.error (error_code);
CREATE INDEX IF NOT EXISTS idx_logs_error_origin_boundary
  ON logs.error (origin_layer, boundary_key);
CREATE INDEX IF NOT EXISTS idx_logs_error_severity
  ON logs.error (severity);
CREATE INDEX IF NOT EXISTS idx_logs_error_report_group
  ON logs.error (stack_hash, error_code, origin_layer, boundary_key);

-- Append-only enforcement: reject in-place UPDATE so logs.error can never be edited as a
-- current projection. (Retention DELETE remains allowed under archive_policy.)
CREATE OR REPLACE FUNCTION logs.error_block_update()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'logs.error is append-only system error evidence; UPDATE is not permitted (error_id=%).', OLD.error_id;
END;
$$;

DROP TRIGGER IF EXISTS trg_logs_error_block_update ON logs.error;
CREATE TRIGGER trg_logs_error_block_update
    BEFORE UPDATE ON logs.error
    FOR EACH ROW
    EXECUTE FUNCTION logs.error_block_update();

-- ---------------------------------------------------------------------------
-- logs.error_notify_queue
-- Durable post-insert notify queue. The canonical wake-up record store: pg_notify is only a
-- best-effort wake-up signal, the durable queue row is the source of truth for a pending wake.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.error_notify_queue (
    queue_id        UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    error_id        UUID        NOT NULL REFERENCES logs.error (error_id),
    channel         TEXT        NOT NULL DEFAULT 'logs_error_inserted',
    enqueued_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- pending | processing | acknowledged | failed_retryable | failed_terminal
    status          TEXT        NOT NULL DEFAULT 'pending',
    attempt_count   INTEGER     NOT NULL DEFAULT 0,
    processing_at   TIMESTAMPTZ,
    last_attempt_at TIMESTAMPTZ,
    acknowledged_at TIMESTAMPTZ,
    failed_reason   TEXT,
    payload_json    JSONB       NOT NULL DEFAULT '{}'::jsonb
);

COMMENT ON TABLE logs.error_notify_queue IS
  'Durable wake-up queue for logs.error inserts and the canonical source of truth for the post-notify bridge. pg_notify is wake-up only. The backend bridge atomically claims pending/failed_retryable rows, joins logs.error to build a hook trigger payload, dispatches through RuntimeTimelineScheduler (never an external provider directly), then transitions the row to acknowledged / failed_retryable / failed_terminal. The DB trigger writes rows only and never calls external providers.';

CREATE INDEX IF NOT EXISTS idx_logs_error_notify_queue_status
  ON logs.error_notify_queue (status, enqueued_at);
CREATE INDEX IF NOT EXISTS idx_logs_error_notify_queue_error_id
  ON logs.error_notify_queue (error_id);
CREATE INDEX IF NOT EXISTS idx_logs_error_notify_queue_claimable
  ON logs.error_notify_queue (status, last_attempt_at);

-- ---------------------------------------------------------------------------
-- logs.notify_error_inserted (AFTER INSERT trigger on logs.error)
-- Post-insert hook ONLY: enqueues a durable wake-up row and emits a pg_notify wake-up signal.
-- It does not classify, capture, or route errors, and never calls external providers.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.notify_error_inserted()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_payload JSONB;
BEGIN
    v_payload := jsonb_build_object(
        'event_type', 'logs_error_inserted',
        'error_id', NEW.error_id,
        'origin_layer', NEW.origin_layer,
        'boundary_key', NEW.boundary_key,
        'error_code', NEW.error_code,
        'severity', NEW.severity,
        'stack_hash', NEW.stack_hash);

    INSERT INTO logs.error_notify_queue (error_id, channel, payload_json)
    VALUES (NEW.error_id, 'logs_error_inserted', v_payload);

    -- Wake-up signal only. The durable queue row above is the canonical record.
    PERFORM pg_notify('logs_error_inserted', v_payload::text);
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_notify_error_inserted ON logs.error;
CREATE TRIGGER trg_notify_error_inserted
    AFTER INSERT ON logs.error
    FOR EACH ROW
    EXECUTE FUNCTION logs.notify_error_inserted();

-- ---------------------------------------------------------------------------
-- logs.error_report
-- Derived aggregation over logs.error. NOT replacement evidence — a regenerable read-side
-- grouping by [stack_hash, error_code, origin_layer, boundary_key, function_key, primitive_key].
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW logs.error_report AS
SELECT
    stack_hash,
    error_code,
    origin_layer,
    boundary_key,
    function_key,
    primitive_key,
    count(*)                AS occurrence_count,
    min(occurred_at)        AS first_seen_at,
    max(occurred_at)        AS last_seen_at,
    max(severity)           AS max_severity,
    bool_or(retryable)      AS any_retryable
FROM logs.error
GROUP BY stack_hash, error_code, origin_layer, boundary_key, function_key, primitive_key;

COMMENT ON VIEW logs.error_report IS
  'Derived aggregation of logs.error grouped by stack_hash/error_code/origin_layer/boundary_key/function_key/primitive_key. Read-side, regenerable; not replacement evidence.';

-- ---------------------------------------------------------------------------
-- current.error_report_projection
-- Read-side admin projection of unresolved system error reports. Read-only: admin never edits
-- logs.error evidence rows through this surface. logs.error defines no resolution/close lane, so
-- every aggregated report is treated as unresolved until a future SSOT introduces resolution.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW current.error_report_projection AS
SELECT
    er.stack_hash,
    er.error_code,
    er.origin_layer,
    er.boundary_key,
    er.function_key,
    er.primitive_key,
    er.occurrence_count,
    er.first_seen_at,
    er.last_seen_at,
    er.max_severity,
    er.any_retryable
FROM logs.error_report er
ORDER BY er.last_seen_at DESC;

COMMENT ON VIEW current.error_report_projection IS
  'Read-side admin projection of unresolved backend system error reports. Derived from logs.error_report; read-only — the frontend/admin never edits logs.error evidence rows through this projection.';
