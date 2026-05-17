-- =============================================================================
-- context_route_config.sql
-- SSOT registry table for context route recommendation engine parameters.
--
-- Each row is one tuning parameter.  All values used by
-- ContextRouteRecommendationResolver are stored here — no numeric literals
-- exist in the runtime code.
--
-- Editable via the admin UI (/admin/context-route-config).
-- Loaded by ContextRouteConfigRepository at runtime.
-- If this table is empty, ContextRouteConfigRepository returns MissingPolicy — no silent fallback.
-- The INSERT below seeds initial values; they are the canonical source, not a C# Default static.
--
-- Key reference (from enum-session-cos-route-theory spec):
--   min_similarity       — neighborhood_inference.min_similarity
--   top_k                — neighborhood_inference.k_nearest_sessions
--   min_neighbors        — limits.thresholds.min_neighbors
--   recent_days          — limits.performance.similarity_search_scope.X_days
--   max_candidates_shown — limits.thresholds.max_candidates_shown
--   baseline_weight      — recommend_section_2_next_action.weights.baseline_weight
--   neighbor_weight      — recommend_section_2_next_action.weights.neighbor_weight
-- =============================================================================

CREATE TABLE IF NOT EXISTS context_route_config (
    config_key      TEXT        PRIMARY KEY,
    float_value     FLOAT,
    int_value       INT,
    description     TEXT        NOT NULL DEFAULT '',
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (float_value IS NOT NULL OR int_value IS NOT NULL)
);

COMMENT ON TABLE context_route_config IS
    'SSOT registry for context route recommendation engine tuning parameters. '
    'Editable via admin UI. Loaded by ContextRouteConfigRepository. '
    'Empty table → MissingPolicy (explicit error) — no silent hardcoded fallback.';

COMMENT ON COLUMN context_route_config.config_key IS
    'Stable identifier matching the ContextRouteConfig record field name (snake_case).';

COMMENT ON COLUMN context_route_config.float_value IS
    'Used for float parameters (min_similarity, baseline_weight, neighbor_weight). NULL for int params.';

COMMENT ON COLUMN context_route_config.int_value IS
    'Used for int parameters (top_k, min_neighbors, recent_days, max_candidates_shown). NULL for float params.';

-- Bootstrap seed values — this table is the canonical SSOT; no C# Default mirrors these
INSERT INTO context_route_config (config_key, float_value, int_value, description) VALUES
    ('min_similarity',
     0.05, NULL,
     'Minimum cosine similarity for a prefix candidate to qualify as a neighbor.'),

    ('top_k',
     NULL, 50,
     'Maximum prefix candidates retrieved per cosine search (limits DB scan scope).'),

    ('min_neighbors',
     NULL, 10,
     'Minimum qualifying neighbors required before emitting recommendations.'),

    ('recent_days',
     NULL, 90,
     'History window in days for prefix vector candidate retrieval.'),

    ('max_candidates_shown',
     NULL, 5,
     'Maximum recommendation candidates in output (next_operations and next_tokens).'),

    ('baseline_weight',
     0.5, NULL,
     'Scoring weight for transition stat baseline. baseline_weight + neighbor_weight must equal 1.0.'),

    ('neighbor_weight',
     0.5, NULL,
     'Scoring weight for neighbor vote contribution. neighbor_weight + baseline_weight must equal 1.0.')

ON CONFLICT (config_key) DO NOTHING;
