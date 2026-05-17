-- =============================================================================
-- context_route_tables.sql
-- Context route recommendation runtime tables.
--
-- These tables support the abstract context route recommendation runtime:
--   context_token_registry  — discrete token dictionary (meaning direction)
--   context_token_binding   — optional domain/table scoping for token candidates
--   context_session         — implicit session grouping
--   context_event           — append-only operation event log (the only required log)
--   context_record_snapshot_cache — cache of current token_ids per record
--   context_event_vector_cache    — cache of sparse vector + l2_norm per event
--   context_prefix_vector_cache   — cache of aggregated prefix vector per session prefix
--   context_transition_stats      — workflow transition probability aggregate
--
-- Optional (isolated, not required for v1):
--   context_cluster         — optional cluster assignment for sessions
--   context_cluster_label   — optional human/LLM labels for clusters
--   context_drift_signal    — optional bollinger-band drift/spike detection output
--
-- CANONICAL FLOW INSERTION POINT:
--   ...
--   → component_expand
--   → context_route_recommendation_resolve   ← this layer
--   → emission_or_projection
-- =============================================================================


-- ---------------------------------------------------------------------------
-- context_token_registry
-- Discrete token dictionary.
-- token_id is the primary key used in context_event.token_ids.
-- value is the meaning direction component in [-1.0, 1.0] (cosine-compatible).
-- group/type is UI grouping only — not used in vector computation.
-- Missing tokens are treated as 0 in the sparse vector.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_token_registry (
    token_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    label       TEXT        NOT NULL,
    "group"     TEXT,                                       -- UI grouping tag; not used in computation
    value       FLOAT       NOT NULL DEFAULT 0.0,           -- meaning direction component [-1.0, 1.0]
    status      TEXT        NOT NULL DEFAULT 'active'
                            CHECK (status IN ('active', 'deprecated')),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (label, "group")
);

COMMENT ON TABLE context_token_registry IS
    'Discrete token dictionary for the context route recommendation runtime. '
    'value is the meaning direction component used in sparse cosine vectors. '
    'group/type is UI grouping only and is not used in computation. '
    'Missing tokens are treated as 0 in the sparse vector representation.';

COMMENT ON COLUMN context_token_registry.value IS
    'Meaning direction component. Recommended range [-1.0, 1.0]. '
    'Assigned by human discrete ordering; spacing need not be uniform. '
    'Example: {active=1.0, warning=0.0, critical=-1.0}.';

CREATE INDEX IF NOT EXISTS idx_ctr_status
    ON context_token_registry (status)
    WHERE status = 'active';


-- ---------------------------------------------------------------------------
-- context_token_binding
-- Optional domain/table scoping for token candidate filtering.
-- Maps which token groups apply to a given table/domain in the UI.
-- Not required for recommendation computation; used for candidate narrowing.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_token_binding (
    binding_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name  TEXT        NOT NULL,
    domain      TEXT,
    token_group TEXT        NOT NULL,                       -- binds by context_token_registry.group
    enabled     BOOLEAN     NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE context_token_binding IS
    'Optional binding: which token groups apply for a given table/domain. '
    'Used for candidate filtering in the UI. Not required for cosine computation.';

CREATE INDEX IF NOT EXISTS idx_ctb_table
    ON context_token_binding (table_name);

CREATE INDEX IF NOT EXISTS idx_ctb_domain
    ON context_token_binding (domain);


-- ---------------------------------------------------------------------------
-- context_session
-- Implicit session grouping.
-- No explicit end is required — sessions end implicitly.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_session (
    session_id      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         TEXT,
    role            TEXT,
    class           TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE context_session IS
    'Implicit session grouping for context route events. '
    'No explicit end required. last_seen_at is updated on each event.';

CREATE INDEX IF NOT EXISTS idx_cs_user_time
    ON context_session (user_id, last_seen_at);

CREATE INDEX IF NOT EXISTS idx_cs_role_time
    ON context_session (role, last_seen_at);


-- ---------------------------------------------------------------------------
-- context_event
-- Append-only operation event log. The only required log table.
-- Each event records the active token_ids snapshot at the time of the operation.
-- The ordered event sequence within a session defines the token_context.
-- Supervision signals (next_operation_hint, next_token_ids_hint) are optional.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_event (
    event_id                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id              UUID        NOT NULL REFERENCES context_session (session_id),
    user_id                 TEXT,
    role                    TEXT,
    class                   TEXT,
    table_name              TEXT        NOT NULL,
    record_id               TEXT,
    operation               TEXT        NOT NULL,           -- screen/action token
    token_ids               UUID[]      NOT NULL DEFAULT '{}',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- Supervision signals (optional)
    next_operation_hint     TEXT,
    next_token_ids_hint     UUID[]
);

COMMENT ON TABLE context_event IS
    'Append-only operation event log. The ordered sequence within a session '
    'defines the token_context. token_ids is the active enum snapshot used for '
    'sparse vector reconstruction. supervision signals are optional.';

COMMENT ON COLUMN context_event.token_ids IS
    'Active token snapshot at event time. Used to reconstruct the sparse event '
    'vector via context_token_registry.value. Missing tokens treated as 0.';

CREATE INDEX IF NOT EXISTS idx_ce_session_time
    ON context_event (session_id, created_at);

CREATE INDEX IF NOT EXISTS idx_ce_operation_time
    ON context_event (operation, created_at);

CREATE INDEX IF NOT EXISTS idx_ce_table_record
    ON context_event (table_name, record_id);

CREATE INDEX IF NOT EXISTS idx_ce_role_operation
    ON context_event (role, operation);

CREATE INDEX IF NOT EXISTS idx_ce_token_ids
    ON context_event USING GIN (token_ids);


-- ---------------------------------------------------------------------------
-- context_record_snapshot_cache
-- Cache of current token_ids for a record to avoid expensive joins at display time.
-- Rebuilt from context_event on record insert/update or token registry changes.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_record_snapshot_cache (
    table_name  TEXT        NOT NULL,
    record_id   TEXT        NOT NULL,
    token_ids   UUID[]      NOT NULL DEFAULT '{}',
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (table_name, record_id)
);

COMMENT ON TABLE context_record_snapshot_cache IS
    'Cache of current token_ids per record for fast display-time lookups. '
    'Rebuildable from context_event. Updated on record insert/update or enum change.';

CREATE INDEX IF NOT EXISTS idx_crsc_updated
    ON context_record_snapshot_cache (updated_at);


-- ---------------------------------------------------------------------------
-- context_event_vector_cache
-- Cache of sparse event vector and l2_norm per event for fast cosine computation.
-- vector_sparse is a JSONB map: {token_id: value, ...}.
-- Rebuilt from context_event + context_token_registry.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_event_vector_cache (
    event_id        UUID        PRIMARY KEY REFERENCES context_event (event_id),
    vector_sparse   JSONB       NOT NULL DEFAULT '{}',      -- {token_id: value, ...}
    l2_norm         FLOAT       NOT NULL DEFAULT 0.0,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE context_event_vector_cache IS
    'Cache of sparse event vector and l2_norm per event. '
    'vector_sparse maps token_id → value. Rebuilt from context_event + token_registry. '
    'l2_norm = sqrt(sum(value^2)) over non-zero entries.';

COMMENT ON COLUMN context_event_vector_cache.vector_sparse IS
    'Sparse vector as JSONB: {token_id_string: float_value}. '
    'Missing tokens are treated as 0 in cosine computation. '
    'Dense embeddings are explicitly excluded.';

CREATE INDEX IF NOT EXISTS idx_cevc_l2_norm
    ON context_event_vector_cache (l2_norm);


-- ---------------------------------------------------------------------------
-- context_prefix_vector_cache
-- Cache of aggregated prefix vector for (session_id, prefix_index).
-- prefix_index is the event order within the session (0..n-1).
-- prefix_vector = SUM(event_vectors) over events up to prefix_index.
-- Used for nearest-prefix cosine search.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_prefix_vector_cache (
    session_id      UUID        NOT NULL REFERENCES context_session (session_id),
    prefix_index    INT         NOT NULL,
    last_event_id   UUID        NOT NULL REFERENCES context_event (event_id),
    vector_sparse   JSONB       NOT NULL DEFAULT '{}',      -- SUM of event vectors up to prefix_index
    l2_norm         FLOAT       NOT NULL DEFAULT 0.0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (session_id, prefix_index)
);

COMMENT ON TABLE context_prefix_vector_cache IS
    'Cache of aggregated prefix vector per (session_id, prefix_index). '
    'prefix_vector = SUM(v_event) over events[0..prefix_index]. '
    'SUM chosen for low compute and statistical stability. '
    'Used as the primary candidate set for nearest-prefix cosine search.';

COMMENT ON COLUMN context_prefix_vector_cache.prefix_index IS
    'Event order within the session (0-based). '
    '0 = first event only; n-1 = full session vector.';

CREATE INDEX IF NOT EXISTS idx_cpvc_session
    ON context_prefix_vector_cache (session_id);

CREATE INDEX IF NOT EXISTS idx_cpvc_last_event
    ON context_prefix_vector_cache (last_event_id);

CREATE INDEX IF NOT EXISTS idx_cpvc_updated
    ON context_prefix_vector_cache (updated_at);


-- ---------------------------------------------------------------------------
-- context_transition_stats
-- Workflow transition probability aggregate.
-- Stores P(next_operation | prev_operation) segmented by role and user_id.
-- prob01 = count_hits / SUM(count_hits) over same (prev_operation, role, user_id) scope.
-- count_events mirrors SUM(count_hits) and is updated atomically with prob01.
-- Fallback hierarchy: user_id → role → GLOBAL.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_transition_stats (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    prev_operation  TEXT        NOT NULL,
    next_operation  TEXT        NOT NULL,
    role            TEXT        NOT NULL DEFAULT 'GLOBAL',
    user_id         TEXT        NOT NULL DEFAULT 'GLOBAL',
    count_events    INT         NOT NULL DEFAULT 0,
    count_hits      FLOAT       NOT NULL DEFAULT 0.0,
    prob01          FLOAT       NOT NULL DEFAULT 0.0,       -- count_hits / count_events (true proportion)
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (prev_operation, next_operation, role, user_id)
);

COMMENT ON TABLE context_transition_stats IS
    'Workflow transition probability: P(next_operation | prev_operation). '
    'Segmented by role and user_id with GLOBAL fallback. '
    'prob01 = count_hits / SUM(count_hits) over the same (prev_operation, role, user_id) scope. '
    'count_events mirrors the scope SUM(count_hits) and is kept in sync atomically.';

COMMENT ON COLUMN context_transition_stats.prob01 IS
    'Conditional proportion: count_hits / SUM(count_hits across scope). '
    'Recomputed for ALL rows in the same (prev, role, user_id) scope on every update, '
    'so new edges get the correct denominator immediately. '
    'Smoothing parameters, if required, must come from function_parameters — not hardcoded.';

COMMENT ON COLUMN context_transition_stats.count_events IS
    'Mirrors SUM(count_hits) across all next_operations in the same (prev, role, user_id) scope. '
    'Updated atomically with prob01. Not incremented independently.';

CREATE INDEX IF NOT EXISTS idx_cts_prev_role
    ON context_transition_stats (prev_operation, role, user_id);


-- =============================================================================
-- Optional tables (v1 deferred — isolated below this line)
-- These are not required for the v1 recommendation runtime.
-- Do not reference these tables from the core recommendation logic.
-- =============================================================================


-- ---------------------------------------------------------------------------
-- context_cluster (optional)
-- Cluster assignment for sessions/prefixes for interpretability.
-- External API/LLM is used ONLY to propose cluster label names.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_cluster (
    session_id  UUID        PRIMARY KEY REFERENCES context_session (session_id),
    cluster_id  TEXT        NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE context_cluster IS
    'Optional: cluster assignment for sessions for interpretability. '
    'Populated by periodic k-means clustering (monthly). '
    'External LLM is used ONLY for cluster labeling, not for runtime logic.';

CREATE INDEX IF NOT EXISTS idx_cclust_cluster_id
    ON context_cluster (cluster_id);


-- ---------------------------------------------------------------------------
-- context_cluster_label (optional)
-- Human/LLM-proposed labels for clusters.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_cluster_label (
    cluster_id      TEXT        PRIMARY KEY,
    label           TEXT        NOT NULL UNIQUE,
    description     TEXT,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE context_cluster_label IS
    'Optional: human or LLM-proposed labels for context clusters. '
    'Naming is a labeling layer only and is not required for runtime function.';


-- ---------------------------------------------------------------------------
-- context_drift_signal (optional)
-- Bollinger-band drift/spike detection output.
-- Not required for v1. Isolated for future expansion.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_drift_signal (
    signal_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    signal_type     TEXT        NOT NULL CHECK (signal_type IN ('spike', 'drift')),
    target          TEXT        NOT NULL,                   -- e.g. operation or token label
    role            TEXT        NOT NULL DEFAULT 'GLOBAL',
    detected_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    detail          JSONB       NOT NULL DEFAULT '{}'
);

COMMENT ON TABLE context_drift_signal IS
    'Optional: output of bollinger-band drift/spike detection. '
    'Records changes in recommendation tendency over time. '
    'Not required for v1 recommendation runtime.';

CREATE INDEX IF NOT EXISTS idx_cds_type_target
    ON context_drift_signal (signal_type, target, detected_at);
