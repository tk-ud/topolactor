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
-- Discrete token dictionary. Each row is a topology vocabulary basis element.
-- token_id is the primary key used in context_event.token_ids (multi-hot observation).
-- value is a human-assigned ordering reference; it is NOT used in recommendation computation.
-- Recommendation uses multi-hot token ID presence (1.0 per token), not the value field.
-- group/type is UI grouping only — not used in vector computation.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_token_registry (
    token_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    label       TEXT        NOT NULL,
    "group"     TEXT,                                       -- UI grouping tag; not used in computation
    value       FLOAT       NOT NULL DEFAULT 0.0,           -- ordering reference only; not used in recommendation
    status      TEXT        NOT NULL DEFAULT 'active'
                            CHECK (status IN ('active', 'deprecated')),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (label, "group")
);

COMMENT ON TABLE context_token_registry IS
    'Discrete token dictionary for the context route recommendation runtime. '
    'Each row is a topology vocabulary basis element (registryId axis). '
    'Recommendation uses multi-hot token ID presence (1.0 per token) — not the value field. '
    'value is a human-assigned ordering reference, kept for UI display and audit. '
    'group is UI grouping only and is not used in computation.';

COMMENT ON COLUMN context_token_registry.value IS
    'Human-assigned ordering reference. Recommended range [-1.0, 1.0]. '
    'NOT used in recommendation computation — token ID presence is the topology observation. '
    'Kept for UI display and potential future use.';

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
-- Append-only operation event log. The only required log table for context-route recommendation.
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
    'Append-only operation event log for context-route recommendation. The ordered sequence within a session '
    'defines the token_context. token_ids is the active enum snapshot used for '
    'sparse vector reconstruction. supervision signals are optional. '
    'SQL Attention logs alignment: context_event is a conditional logs.ui_operation-like signal source '
    '(component/operation usage pressure), not canonical logs.current or logs.attention.';

COMMENT ON COLUMN context_event.token_ids IS
    'Active token snapshot at event time. Used to reconstruct the sparse event '
    'multi-hot vector by token presence (1.0), not token value weighting. Missing tokens are 0.';

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
-- Rebuildable projection cache of multi-hot event vector and l2_norm per event.
-- vector_sparse is a JSONB map: {token_id: 1.0, ...} (multi-hot presence, not value-weighted).
-- NOT a source of truth. Rebuilt from context_event token_ids.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_event_vector_cache (
    event_id        UUID        PRIMARY KEY REFERENCES context_event (event_id),
    vector_sparse   JSONB       NOT NULL DEFAULT '{}',      -- {token_id: 1.0, ...} multi-hot
    l2_norm         FLOAT       NOT NULL DEFAULT 0.0,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE context_event_vector_cache IS
    'Rebuildable projection cache of multi-hot event vector and l2_norm per event. NOT SoT. '
    'vector_sparse maps token_id → 1.0 (multi-hot). Rebuilt from context_event token_ids. '
    'l2_norm = sqrt(cardinality(token_ids)) for a multi-hot vector.';

COMMENT ON COLUMN context_event_vector_cache.vector_sparse IS
    'Multi-hot sparse vector as JSONB: {token_id_string: 1.0}. '
    'Token presence = 1.0, absence = omitted (treated as 0). '
    'NOT a semantic source of truth — rebuildable projection cache only.';

CREATE INDEX IF NOT EXISTS idx_cevc_l2_norm
    ON context_event_vector_cache (l2_norm);


-- ---------------------------------------------------------------------------
-- context_prefix_vector_cache
-- Rebuildable projection cache of aggregated prefix vector for (session_id, prefix_index).
-- prefix_index is the event order within the session (0..n-1).
-- prefix_vector = SUM(multi-hot event_vectors) over events up to prefix_index.
-- NOT a source of truth. Used for nearest-prefix topology neighborhood search.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_prefix_vector_cache (
    session_id      UUID        NOT NULL REFERENCES context_session (session_id),
    prefix_index    INT         NOT NULL,
    last_event_id   UUID        NOT NULL REFERENCES context_event (event_id),
    vector_sparse   JSONB       NOT NULL DEFAULT '{}',      -- SUM of multi-hot event vectors up to prefix_index
    l2_norm         FLOAT       NOT NULL DEFAULT 0.0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (session_id, prefix_index)
);

COMMENT ON TABLE context_prefix_vector_cache IS
    'Rebuildable projection cache of aggregated multi-hot prefix vector per (session_id, prefix_index). NOT SoT. '
    'prefix_vector = SUM(multi-hot v_event) over events[0..prefix_index]. '
    'SUM chosen for low compute and statistical stability. '
    'Used as candidate set for nearest-prefix topology neighborhood search.';

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


-- ---------------------------------------------------------------------------
-- context_enum_transition_event
-- Append-only enum item transition log (state_pressure lane signal source).
-- Not mixed with context_event operation transitions.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_enum_transition_event (
    event_id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    enum_group_id     UUID        NOT NULL,
    prev_enum_index   INT         NOT NULL,
    next_enum_index   INT         NOT NULL,
    role              TEXT        NOT NULL DEFAULT 'GLOBAL',
    user_id           TEXT        NOT NULL DEFAULT 'GLOBAL',
    session_id        UUID        NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE context_enum_transition_event IS
    'Append-only enum item transition observations for state_pressure recommendation lane. '
    'Logical enum_transition_logs surface; separate from context_transition_stats operation transitions.';

CREATE INDEX IF NOT EXISTS idx_cete_group_prev_role
    ON context_enum_transition_event (enum_group_id, prev_enum_index, role, user_id, created_at);


-- ---------------------------------------------------------------------------
-- context_enum_transition_stats
-- Enum item transition probability: P(next_enum_index | prev_enum_index, enum_group_id).
-- Segmented by role and user_id with GLOBAL fallback. Must not be used for operation transitions.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_enum_transition_stats (
    id                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    enum_group_id     UUID        NOT NULL,
    prev_enum_index   INT         NOT NULL,
    next_enum_index   INT         NOT NULL,
    role              TEXT        NOT NULL DEFAULT 'GLOBAL',
    user_id           TEXT        NOT NULL DEFAULT 'GLOBAL',
    count_events      INT         NOT NULL DEFAULT 0,
    count_hits        FLOAT       NOT NULL DEFAULT 0.0,
    prob01            FLOAT       NOT NULL DEFAULT 0.0,
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (enum_group_id, prev_enum_index, next_enum_index, role, user_id)
);

COMMENT ON TABLE context_enum_transition_stats IS
    'Enum transition probability within enum_group. state_pressure lane only. '
    'prob01 = count_hits / SUM(count_hits) over same (enum_group_id, prev_enum_index, role, user_id) scope.';

CREATE INDEX IF NOT EXISTS idx_cets_group_prev_role
    ON context_enum_transition_stats (enum_group_id, prev_enum_index, role, user_id);


-- ---------------------------------------------------------------------------
-- context_event_cold
-- Cold storage for context events moved from context_event when
-- archive_strategy="archive" is configured in the retention policy.
-- Rows are inserted here before being deleted from context_event.
-- No FK to context_event (records are moved, not copied).
-- No FK to context_session (session may still be active).
-- archived_at records when the row was moved to cold storage.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_event_cold (
    event_id                UUID        PRIMARY KEY,
    session_id              UUID        NOT NULL,
    user_id                 TEXT,
    role                    TEXT,
    class                   TEXT,
    table_name              TEXT        NOT NULL,
    record_id               TEXT,
    operation               TEXT        NOT NULL,
    token_ids               UUID[]      NOT NULL DEFAULT '{}',
    created_at              TIMESTAMPTZ NOT NULL,
    next_operation_hint     TEXT,
    next_token_ids_hint     UUID[],
    archived_at             TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE context_event_cold IS
    'Cold storage for archived context events. '
    'Rows are moved here from context_event when archive_strategy="archive". '
    'No FK to context_event or context_session — moved records are self-contained. '
    'archived_at records when the row was promoted to cold storage.';

CREATE INDEX IF NOT EXISTS idx_cec_session
    ON context_event_cold (session_id);

CREATE INDEX IF NOT EXISTS idx_cec_created_at
    ON context_event_cold (created_at);

CREATE INDEX IF NOT EXISTS idx_cec_archived_at
    ON context_event_cold (archived_at);


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

-- =============================================================================
-- Topology Vector Runtime tables
-- context_hub_recommendation_current — rebuildable materialized current for hub attention
-- context_hub_feedback_event         — append-only feedback weight update event log
--
-- These tables support the Topology Vector Runtime extension:
--   Registry Vector Validation, Hub Attention Recommendation,
--   Topology MLP feature crossing, Feedback Weight Update.
--
-- CANONICAL FLOW INSERTION POINT:
--   ...
--   → context_route_recommendation_resolve   ← topology vector runtime integrated here
--   → emission_or_projection
-- =============================================================================


-- ---------------------------------------------------------------------------
-- context_hub_recommendation_current
-- Rebuildable materialized current for hub attention recommendations.
-- NOT a source of truth — topology definition + context_hub_feedback_event are authoritative.
-- Rebuilt from topology data on demand.
--
-- scope_limit: valid values are defined in function_parameters
--   topology_vector_runtime.hub_attention.scope_limits (policy is the enumeration authority).
--   DB enforces scope_limit > 0 only. Policy controls which values are used at runtime.
--   Example default values: 1000 = UI instant, 3000 = normal, 10000 = hub-scoped wide.
--   Adding new scope_limit values requires a policy update only (no DDL migration needed).
--
-- candidate_kind: topology vocabulary. Current topology vocabulary:
--   'registry','hub','entity','relation','operation','token'.
--   Adding new kinds requires a DDL migration to extend the CHECK constraint.
--
-- evidence_json: transition key evidence (relation/cosine/stat/EMA/cross)
-- mlp_feature_json: feature crossing contribution breakdown
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_hub_recommendation_current (
    hub_id                  UUID        NOT NULL,
    target_table            TEXT        NOT NULL,
    candidate_kind          TEXT        NOT NULL
                            CHECK (candidate_kind IN ('registry','hub','entity','relation','operation','token')),
    candidate_id            UUID        NOT NULL,
    scope_limit             INT         NOT NULL
                            CHECK (scope_limit > 0),
    base_probability        FLOAT,
    cosine_similarity       FLOAT,
    static_relation_weight  FLOAT,
    statistical_weight      FLOAT,
    mlp_feature_score       FLOAT,
    feedback_adjustment     FLOAT       NOT NULL DEFAULT 0.0,
    ema_fast_30             FLOAT,
    ema_slow_10             FLOAT,
    trend                   FLOAT,
    cross_state             TEXT        CHECK (cross_state IN ('up','down','none')),
    attention_score         FLOAT,
    rank                    INT,
    evidence_json           JSONB       NOT NULL DEFAULT '{}',
    mlp_feature_json        JSONB       NOT NULL DEFAULT '{}',
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (hub_id, target_table, candidate_kind, candidate_id, scope_limit)
);

COMMENT ON TABLE context_hub_recommendation_current IS
    'Rebuildable materialized current for hub attention recommendations. '
    'NOT a source of truth. Rebuilt from topology data + context_hub_feedback_event. '
    'This table is not SQL Attention logs.current calculation basis; hub recommendation current and '
    'SQL Attention logs.current must be treated as separate semantics. '
    'scope_limit IN (1000, 3000, 10000): isolated by unique key — no cross-limit mixing. '
    'evidence_json: transition key evidence. mlp_feature_json: feature crossing breakdown.';

COMMENT ON COLUMN context_hub_recommendation_current.scope_limit IS
    'Recommendation scope limit. Valid values are policy-defined in '
    'function_parameters topology_vector_runtime.hub_attention.scope_limits. '
    'DB enforces scope_limit > 0 only; policy is the enumeration authority. '
    'Default policy values: 1000 = UI instant; 3000 = normal; 10000 = hub-scoped wide. '
    'Rows with different scope_limit values are isolated by the unique key.';

COMMENT ON COLUMN context_hub_recommendation_current.ema_fast_30 IS
    'EMA with fast alpha (from policy: topology_vector_runtime.hub_attention.ema_fast_alpha). '
    'Alpha value is read from function_parameters — not hardcoded.';

COMMENT ON COLUMN context_hub_recommendation_current.ema_slow_10 IS
    'EMA with slow alpha (from policy: topology_vector_runtime.hub_attention.ema_slow_alpha). '
    'Alpha value is read from function_parameters — not hardcoded.';

COMMENT ON COLUMN context_hub_recommendation_current.trend IS
    'ema_fast_30 - ema_slow_10. Positive = fast rising above slow (upward trend).';

COMMENT ON COLUMN context_hub_recommendation_current.cross_state IS
    'Cross detection: up = fast crossed above slow; down = fast crossed below slow; none = no cross.';

COMMENT ON COLUMN context_hub_recommendation_current.attention_score IS
    'Composite score: static_relation_weight + cosine_similarity + statistical_weight '
    '+ mlp_feature_score + feedback_adjustment.';

COMMENT ON COLUMN context_hub_recommendation_current.evidence_json IS
    'Transition key evidence JSON. Keys: relation, cosine, stat, ema, cross, key_entities. '
    'Answers: why this candidate for this hub?';

COMMENT ON COLUMN context_hub_recommendation_current.mlp_feature_json IS
    'Topology MLP feature crossing breakdown. '
    'Records which feature combinations contributed to mlp_feature_score. '
    'Answers: which key combinations drove this score?';

COMMENT ON COLUMN context_hub_recommendation_current.feedback_adjustment IS
    'Cumulative feedback adjustment from context_hub_feedback_event. '
    'Applied as positive_delta / negative_delta / missing_candidate_delta per feedback event. '
    'Delta values read from function_parameters — not hardcoded. '
    'Rebuildable from context_hub_feedback_event aggregate.';

CREATE INDEX IF NOT EXISTS idx_chrc_hub_scope
    ON context_hub_recommendation_current (hub_id, scope_limit);

CREATE INDEX IF NOT EXISTS idx_chrc_hub_rank
    ON context_hub_recommendation_current (hub_id, scope_limit, rank);

CREATE INDEX IF NOT EXISTS idx_chrc_updated
    ON context_hub_recommendation_current (updated_at);


-- ---------------------------------------------------------------------------
-- context_hub_feedback_event
-- Append-only feedback weight update event log.
-- Records selected / ignored / missing_candidate differences per hub per candidate.
-- Used to rebuild feedback_adjustment in context_hub_recommendation_current.
-- Never updated or deleted — aggregate current is rebuildable from this log.
--
-- scope_limit: policy-defined; DB enforces scope_limit > 0 only (matches context_hub_recommendation_current).
-- candidate_kind: topology vocabulary CHECK (extending requires DDL migration).
-- feedback_kind: topology vocabulary CHECK ('selected','ignored','missing_candidate').
--   Extending feedback_kind requires a DDL migration.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS context_hub_feedback_event (
    feedback_id     UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id          UUID        NOT NULL,
    target_table    TEXT        NOT NULL,
    candidate_id    UUID        NOT NULL,
    candidate_kind  TEXT        NOT NULL
                    CHECK (candidate_kind IN ('registry','hub','entity','relation','operation','token')),
    scope_limit     INT         NOT NULL
                    CHECK (scope_limit > 0),
    feedback_kind   TEXT        NOT NULL
                    CHECK (feedback_kind IN ('selected','ignored','missing_candidate')),
    delta_applied   FLOAT       NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE context_hub_feedback_event IS
    'Append-only feedback weight update event log. '
    'Records selected / ignored / missing_candidate differences per hub/candidate/target_table. '
    'target_table must match context_hub_recommendation_current.target_table so that '
    'feedback_adjustment is applied to the exact PK row '
    '(hub_id, target_table, candidate_kind, candidate_id, scope_limit). '
    'feedback_kind: selected → positive_delta; ignored → negative_delta; '
    'missing_candidate → missing_candidate_delta (from policy — not hardcoded). '
    'Aggregate current (feedback_adjustment) in context_hub_recommendation_current '
    'is rebuildable from this log.';

COMMENT ON COLUMN context_hub_feedback_event.target_table IS
    'Target table name matching context_hub_recommendation_current.target_table. '
    'Required to scope feedback_adjustment update to the exact PK row. '
    'Prevents erroneous cross-target_table application when the same candidate_id '
    'appears under multiple target_tables for the same hub.';

COMMENT ON COLUMN context_hub_feedback_event.delta_applied IS
    'Actual delta applied from topology_vector_runtime.feedback_weight_update policy '
    'at the time this event was created. Stored for audit/rebuild purposes. '
    'Policy values: positive_delta, negative_delta, missing_candidate_delta.';

CREATE INDEX IF NOT EXISTS idx_chfe_hub_candidate
    ON context_hub_feedback_event (hub_id, target_table, candidate_id, scope_limit);

CREATE INDEX IF NOT EXISTS idx_chfe_created
    ON context_hub_feedback_event (created_at);


-- component_operation_event_log
-- frontend_component_event_log_lane canonical append-only log with required_identity preservation.
CREATE TABLE IF NOT EXISTS component_operation_event_log (
    event_log_id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    component_id      TEXT NOT NULL,
    package_id        TEXT NULL,
    layout_id         TEXT NULL,
    wiring_id         TEXT NULL,
    event_type        TEXT NOT NULL,
    payload           JSONB NOT NULL,
    actor_or_source   TEXT NOT NULL,
    occurred_at       TIMESTAMPTZ NOT NULL,
    idempotency_key   TEXT NOT NULL UNIQUE,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);


ALTER TABLE component_operation_event_log
  ADD COLUMN IF NOT EXISTS wiring_id TEXT NULL;

CREATE INDEX IF NOT EXISTS idx_component_operation_event_log_occurred_at
  ON component_operation_event_log (occurred_at);
