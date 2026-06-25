-- =============================================================================
-- sql_attention_logs_tables.sql
-- SQL Attention physical tables (design contract implementation surface)
--
-- Scope of this file:
--   - Implement hub-attractor physical schema: logs.current / logs.hub_current / logs.attention.
--   - Implement indexes/constraints for query and linkage contracts.
-- Boundary note:
--   - This file defines static schema/constraint contracts.
--   - Runtime orchestration and function routes are governed by runtime boundaries, not by progress notes in DDL.
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS logs;

-- ---------------------------------------------------------------------------
-- logs.diff
-- Physical table lifecycle mutation pressure source for logs.current refresh.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.diff (
    diff_id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_set_id              TEXT        NOT NULL,
    basis_window               TEXT        NOT NULL,
    physical_table_id          TEXT        NOT NULL,
    physical_table_name        TEXT        NOT NULL,
    record_id                  TEXT        NOT NULL,
    operation_kind             TEXT        NOT NULL,
    before_state_or_diff_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    after_state_or_diff_json   JSONB       NOT NULL DEFAULT '{}'::jsonb,
    observed_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    actor_or_source            TEXT,
    archive_policy             TEXT        NOT NULL DEFAULT 'required'
);

COMMENT ON TABLE logs.diff IS
  'Physical table lifecycle mutation pressure source. Canonical input for logs.current refresh/watch.';

CREATE INDEX IF NOT EXISTS idx_logs_diff_source_window
  ON logs.diff (source_set_id, basis_window);
CREATE INDEX IF NOT EXISTS idx_logs_diff_table
  ON logs.diff (physical_table_id, physical_table_name);
CREATE INDEX IF NOT EXISTS idx_logs_diff_operation_kind
  ON logs.diff (operation_kind);
CREATE INDEX IF NOT EXISTS idx_logs_diff_record_history
  ON logs.diff (physical_table_id, record_id, observed_at, diff_id);

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
-- logs.hub_current
-- Optional derived support cache. Not the canonical SQL Attention exploration field.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.hub_current (
    hub_current_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_set_id        TEXT        NOT NULL,
    hub_id                UUID,
    attractor_key         TEXT        NOT NULL,
    hub_relation_id       UUID,
    relation_registry_id  UUID,
    basis_window           TEXT        NOT NULL,
    tensor_basis_json     JSONB       NOT NULL DEFAULT '{}'::jsonb,
    attractor_vector_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    population_count       BIGINT      NOT NULL DEFAULT 0,
    population_recordcount BIGINT      NOT NULL DEFAULT 0,
    axis_population_json   JSONB       NOT NULL DEFAULT '{}'::jsonb,
    axis_z_score_json      JSONB       NOT NULL DEFAULT '{}'::jsonb,
    phase_basis_json       JSONB       NOT NULL DEFAULT '{}'::jsonb,
    evaluated_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_logs_hub_current_basis
      UNIQUE (source_set_id, basis_window, attractor_key, relation_registry_id)
);

COMMENT ON TABLE logs.hub_current IS
  'Optional derived support cache. Not the canonical SQL Attention hubs.hub_relations exploration field; not adopted state; no hub/topology mutation.';

COMMENT ON COLUMN logs.hub_current.tensor_basis_json IS
  'Deprecated diagnostics/support-cache projection basis only. This is not the canonical hubs.hub_relations exploration field, topology payload, or mutation surface.';

-- ---------------------------------------------------------------------------
-- logs.attention
-- Append-only SQLAT / phaseAT evidence generation log. q phaseAT rows are never Draft.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.attention (
    attention_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    current_id            UUID        NOT NULL REFERENCES logs.current(current_id) ON DELETE RESTRICT,
    hub_current_id        UUID        REFERENCES logs.hub_current(hub_current_id) ON DELETE RESTRICT,
    source_set_id         TEXT        NOT NULL,
    evidence_kind         TEXT        NOT NULL DEFAULT 'sql_attention_hit'
                                  CHECK (evidence_kind IN ('sql_attention_hit', 'phaseAT', 'draft_projection', 'adoption_result', 'rejection_result')),
    generation_line_id    UUID        NOT NULL DEFAULT gen_random_uuid(),
    source_attention_id   UUID        REFERENCES logs.attention(attention_id) ON DELETE RESTRICT,
    source_current_id     UUID        NOT NULL REFERENCES logs.current(current_id) ON DELETE RESTRICT,
    source_topology_manifest_ids UUID[] NOT NULL DEFAULT '{}',
    hit_hub_relation_ids  UUID[]      NOT NULL DEFAULT '{}',
    expanded_hub_relation_ids UUID[]  NOT NULL DEFAULT '{}',
    expanded_topology_manifest_ids UUID[] NOT NULL DEFAULT '{}',
    expanded_hub_ids      UUID[]      NOT NULL DEFAULT '{}',
    phase_status          TEXT        NOT NULL DEFAULT 'not_applicable'
                                  CHECK (phase_status IN ('not_applicable', 'evidence', 'draft_projection', 'adoption_result', 'rejection_result')),
    promotion_status      TEXT        NOT NULL DEFAULT 'not_requested'
                                  CHECK (promotion_status IN ('not_requested', 'draft', 'adopted', 'rejected')),
    actor_or_source       TEXT,
    command_id            TEXT,
    statistics_json       JSONB       NOT NULL DEFAULT '{}'::jsonb,
    ema_score             DOUBLE PRECISION,
    l2_norm               DOUBLE PRECISION NOT NULL DEFAULT 0,
    vector_json           JSONB       NOT NULL DEFAULT '{}'::jsonb,
    phase_vector_json     JSONB       NOT NULL DEFAULT '{}'::jsonb,
    permutation_key       TEXT,
    hub_id                UUID,
    attractor_key         TEXT        NOT NULL,
    hub_relation_id       UUID,
    relation_registry_id  UUID,
    neighbor_score        DOUBLE PRECISION NOT NULL DEFAULT 0,
    hit_rank              INTEGER,
    score_band            TEXT        NOT NULL DEFAULT 'evidence_only',
    evidence_json         JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    archive_policy        TEXT        NOT NULL DEFAULT 'required'
);

COMMENT ON TABLE logs.attention IS
  'Append-only SQLAT / phaseAT evidence generation log. q phaseAT rows are evidence, never Draft. Canonical exploration field is hubs.hub_relations; hub_current linkage is optional derived support-cache lineage only. No automatic topology/registry/manifest/hub_relation mutation.';

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
CREATE INDEX IF NOT EXISTS idx_logs_attention_hub_id
  ON logs.attention (hub_id);
CREATE INDEX IF NOT EXISTS idx_logs_attention_attractor_key
  ON logs.attention (attractor_key);
CREATE INDEX IF NOT EXISTS idx_logs_attention_created_at
  ON logs.attention (created_at);
CREATE INDEX IF NOT EXISTS idx_logs_attention_l2_norm
  ON logs.attention (l2_norm);
CREATE INDEX IF NOT EXISTS idx_logs_attention_neighbor_score
  ON logs.attention (neighbor_score);


CREATE INDEX IF NOT EXISTS idx_logs_hub_current_hub_id
  ON logs.hub_current (hub_id);
CREATE INDEX IF NOT EXISTS idx_logs_hub_current_source_window
  ON logs.hub_current (source_set_id, basis_window);

CREATE INDEX IF NOT EXISTS idx_logs_hub_current_updated_at
  ON logs.hub_current (updated_at);
CREATE INDEX IF NOT EXISTS idx_logs_attention_hub_current_id
  ON logs.attention (hub_current_id);
CREATE INDEX IF NOT EXISTS idx_logs_attention_score_band
  ON logs.attention (score_band);
CREATE INDEX IF NOT EXISTS idx_logs_attention_generation_line
  ON logs.attention (generation_line_id, created_at);
CREATE INDEX IF NOT EXISTS idx_logs_attention_source_attention_id
  ON logs.attention (source_attention_id);
CREATE INDEX IF NOT EXISTS idx_logs_attention_evidence_kind
  ON logs.attention (evidence_kind, created_at);

CREATE INDEX IF NOT EXISTS idx_logs_hub_current_attractor_key
  ON logs.hub_current (attractor_key);
CREATE INDEX IF NOT EXISTS idx_logs_hub_current_relation_registry_id
  ON logs.hub_current (relation_registry_id);

-- ---------------------------------------------------------------------------
-- policy resolver and logs.current refresh / l2 norm watch implementation
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION logs.resolve_sql_attention_watch_policy(
    p_policy_function_name TEXT DEFAULT 'sql_attention_logs_watch',
    p_policy_parameter_key TEXT DEFAULT 'default_policy'
)
RETURNS TABLE (
    top_n INTEGER,
    delta_threshold DOUBLE PRECISION,
    norm_level_high DOUBLE PRECISION,
    norm_level_medium DOUBLE PRECISION
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_policy JSONB;
BEGIN
    SELECT fp.parameter_value
      INTO v_policy
      FROM topology.function_parameters fp
     WHERE fp.function_name = p_policy_function_name
       AND fp.parameter_key = p_policy_parameter_key
       AND fp.active = true
     ORDER BY fp.created_at DESC
     LIMIT 1;

    IF v_policy IS NULL THEN
        RAISE EXCEPTION
            'SQL Attention watch policy missing: function_name=% parameter_key=%',
            p_policy_function_name, p_policy_parameter_key;
    END IF;

    IF (v_policy ? 'top_n') IS FALSE
       OR (v_policy ? 'delta_threshold') IS FALSE
       OR (v_policy ? 'norm_level_high') IS FALSE
       OR (v_policy ? 'norm_level_medium') IS FALSE THEN
        RAISE EXCEPTION
            'SQL Attention watch policy keys missing. required=[top_n, delta_threshold, norm_level_high, norm_level_medium], value=%',
            v_policy;
    END IF;

    top_n := (v_policy->>'top_n')::INTEGER;
    delta_threshold := (v_policy->>'delta_threshold')::DOUBLE PRECISION;
    norm_level_high := (v_policy->>'norm_level_high')::DOUBLE PRECISION;
    norm_level_medium := (v_policy->>'norm_level_medium')::DOUBLE PRECISION;

    IF top_n <= 0 THEN
        RAISE EXCEPTION 'Invalid top_n in SQL Attention watch policy: %', top_n;
    END IF;
    IF delta_threshold < 0 THEN
        RAISE EXCEPTION 'Invalid delta_threshold in SQL Attention watch policy: %', delta_threshold;
    END IF;

    RETURN NEXT;
END;
$$;

-- ---------------------------------------------------------------------------
-- Canonical SQL Attention related topology manifest resolver.
-- Uses only explicit topology.physical_table_manifest_bindings; no implicit or
-- oldest-row fallback is permitted. Empty result is an explicit no-hit boundary.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.resolve_related_topology_manifests(
    p_current_id UUID,
    p_physical_table_id TEXT,
    p_physical_table_name TEXT
)
RETURNS TABLE (
    topology_manifest_id UUID,
    resolver_evidence_json JSONB
)
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    v_identity_count INTEGER;
BEGIN
    SELECT COUNT(*)
      INTO v_identity_count
      FROM topology.physical_tables pt
     WHERE pt.active = true
       AND (pt.physical_table_id::TEXT = p_physical_table_id OR pt.table_ref = p_physical_table_name);

    IF v_identity_count > 1 THEN
      RAISE EXCEPTION 'AMBIGUOUS_PHYSICAL_TABLE_IDENTITY: physical_table_id=% physical_table_name=% matched % active catalog rows',
        p_physical_table_id, p_physical_table_name, v_identity_count;
    END IF;

    RETURN QUERY
    SELECT
      b.topology_manifest_id,
      jsonb_build_object(
        'resolver', 'explicit_physical_table_manifest_binding',
        'current_id', p_current_id,
        'physical_table_id', pt.physical_table_id,
        'physical_table_ref', pt.table_ref,
        'binding_id', b.binding_id,
        'binding_evidence_json', b.binding_evidence_json,
        'no_implicit_fallback', true
      ) AS resolver_evidence_json
    FROM topology.physical_tables pt
    JOIN topology.physical_table_manifest_bindings b
      ON b.physical_table_id = pt.physical_table_id
     AND b.active = true
    JOIN hubs.topology_manifests tm
      ON tm.topology_manifest_id = b.topology_manifest_id
     AND tm.status = 'active'
    WHERE pt.active = true
      AND (pt.physical_table_id::TEXT = p_physical_table_id OR pt.table_ref = p_physical_table_name)
    ORDER BY b.topology_manifest_id;
END;
$$;

COMMENT ON FUNCTION logs.resolve_related_topology_manifests(UUID, TEXT, TEXT) IS
  'Explicit physical table to active topology_manifest_id[] resolver for canonical SQL Attention. Empty result means explicit no-hit; never falls back to oldest manifest or logs.hub_current.';

-- ---------------------------------------------------------------------------
-- Deprecated phase_vector compatibility helper.
-- Canonical Phase Attention is ID-space evidence:
--   w = l2_norm
--   x = SQL Attention hit hub_relation_id
--   y = topology_manifest_id that contains x
--   z = hub_id registered by y
--   i/j/k = expanded hub_relation_id[] / topology_manifest_id[] / hub_id[]
--   q = logs.attention.phaseAT append-only evidence row, never Draft
-- Step 3 retains the legacy scalar signature only for compatibility. Its count and
-- movement parameters are emitted under explicitly deprecated support statistics;
-- they never populate canonical x/y/z/i/j/k. The canonical runtime separately emits
-- resolved payloads after topology_manifest_id[] resolution and hubs.hub_relations exploration.
-- No mutation/migration/promotion is triggered from this function.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.generate_attention_phase_vector(
    p_l2_norm DOUBLE PRECISION,
    p_hub_relations_count BIGINT,
    p_hub_count BIGINT,
    p_topology_manifests_count BIGINT,
    p_axis_move_i DOUBLE PRECISION,
    p_axis_move_j DOUBLE PRECISION,
    p_axis_move_k DOUBLE PRECISION,
    p_vector_basis_json JSONB DEFAULT '{}'::jsonb,
    p_vector_keys_json JSONB DEFAULT '[]'::jsonb,
    p_phase_basis_json JSONB DEFAULT '{}'::jsonb
)
RETURNS JSONB
LANGUAGE sql
AS $$
    SELECT jsonb_build_object(
        'q_kind', 'phaseAT',
        'q_is_draft', false,
        'generation_status', 'deprecated_support_cache_diagnostics_only',
        'pending_reason', 'legacy support-cache helper is deprecated; canonical phaseAT generation is emitted by manifest-scoped hubs.hub_relations exploration',
        'canonical_exploration_field', 'hubs.hub_relations',
        'legacy_support_cache_source', 'logs.hub_current',
        'meaning_boundary', jsonb_build_object(
            'w', 'l2_norm',
            'x', 'hit_hub_relation_id',
            'y', 'topology_manifest_id',
            'z', 'hub_id',
            'ijk', 'expanded ID arrays',
            'q', 'logs.attention.phaseAT append-only evidence row',
            'q_is_draft', false,
            'legacy_count_scalar_axes_deprecated', true,
            'no_automatic_topology_mutation', true
        ),
        'w_l2_norm', COALESCE(p_l2_norm, 0),
        'x_hit_hub_relation_id', NULL,
        'y_topology_manifest_id', NULL,
        'z_hub_id', NULL,
        'i_expanded_hub_relation_ids', '[]'::jsonb,
        'j_expanded_topology_manifest_ids', '[]'::jsonb,
        'k_expanded_hub_ids', '[]'::jsonb,
        'q_phaseAT_payload', jsonb_build_object(
            'status', 'deprecated_support_cache_diagnostics_only',
            'evidence_only', true,
            'is_draft', false
        ),
        'legacy_support_cache_statistics', jsonb_build_object(
            'hub_relations_count', COALESCE(p_hub_relations_count, 0),
            'hub_count', COALESCE(p_hub_count, 0),
            'topology_manifests_count', COALESCE(p_topology_manifests_count, 0)
        ),
        'legacy_axis_movement_observations', jsonb_build_object(
            'i', COALESCE(p_axis_move_i, 0),
            'j', COALESCE(p_axis_move_j, 0),
            'k', COALESCE(p_axis_move_k, 0)
        ),
        'generated_from', 'legacy_logs_hub_current_support_cache_diagnostics',
        'vector_keys', COALESCE(p_vector_keys_json, '[]'::jsonb),
        'vector_basis_json', COALESCE(p_vector_basis_json, '{}'::jsonb),
        'phase_basis_json', COALESCE(p_phase_basis_json, '{}'::jsonb)
    );
$$;

-- ---------------------------------------------------------------------------
-- logs.hub_current refresh function
-- Refreshes optional logs.hub_current support-cache population/recordcount basis from
-- logs.attention append evidence. This cache is not the SQL Attention exploration field.
-- axis_population_json retains deprecated support statistics only:
--   hub_relations_count, hub_count, topology_manifests_count
-- These count scalars are not canonical Phase Attention x/y/z ID-space values.
-- axis_z_score_json(i/j/k) retains deprecated unobserved movement placeholders only.
-- neighbor_score statistics are not written into i/j/k to avoid movement-semantic masquerade.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.refresh_hub_current(
    p_source_set_id TEXT,
    p_basis_window TEXT
)
RETURNS TABLE (
    hub_current_id UUID,
    attractor_key TEXT,
    population_count BIGINT,
    population_recordcount BIGINT,
    axis_population_json JSONB,
    axis_z_score_json JSONB,
    refreshed_at TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    WITH attention_axis AS (
      SELECT
        a.hub_current_id,
        COUNT(*)::BIGINT AS population_count,
        COUNT(DISTINCT a.current_id)::BIGINT AS population_recordcount,
        COALESCE(SUM(a.neighbor_score), 0.0)::DOUBLE PRECISION AS neighbor_score_sum,
        COALESCE(AVG(a.l2_norm), 0.0)::DOUBLE PRECISION AS l2_norm_avg
      FROM logs.attention a
      JOIN logs.hub_current h ON h.hub_current_id = a.hub_current_id
      WHERE h.source_set_id = p_source_set_id
        AND h.basis_window = p_basis_window
      GROUP BY a.hub_current_id
    ),
    hub_axis AS (
      SELECT
        h.hub_current_id,
        COALESCE(
          (SELECT COUNT(*)::BIGINT
           FROM hubs.hub_relations hr
           JOIN hubs.topology_manifests tm ON tm.topology_manifest_id = hr.topology_manifest_id
           WHERE tm.hub_id = h.hub_id),
          0
        ) AS hub_relations_count,
        (SELECT COUNT(*)::BIGINT FROM hubs.hub) AS hub_count,
        COALESCE(
          (SELECT COUNT(*)::BIGINT FROM hubs.topology_manifests tm WHERE tm.hub_id = h.hub_id),
          0
        ) AS topology_manifests_count
      FROM logs.hub_current h
      WHERE h.source_set_id = p_source_set_id
        AND h.basis_window = p_basis_window
    ),
    vector_terms AS (
      SELECT
        a.hub_current_id,
        e.key,
        SUM((e.value)::DOUBLE PRECISION) AS v
      FROM logs.attention a
      JOIN logs.hub_current h ON h.hub_current_id = a.hub_current_id
      LEFT JOIN LATERAL jsonb_each_text(COALESCE(a.vector_json, '{}'::jsonb)) e ON TRUE
      WHERE h.source_set_id = p_source_set_id
        AND h.basis_window = p_basis_window
      GROUP BY a.hub_current_id, e.key
    ),
    vector_basis AS (
      SELECT
        vt.hub_current_id,
        COALESCE(jsonb_object_agg(vt.key, to_jsonb(vt.v)) FILTER (WHERE vt.key IS NOT NULL), '{}'::jsonb) AS attractor_vector_json
      FROM vector_terms vt
      GROUP BY vt.hub_current_id
    ),
    applied AS (
      UPDATE logs.hub_current h
         SET population_count = aa.population_count,
             population_recordcount = aa.population_recordcount,
             axis_population_json = jsonb_build_object(
                'hub_relations_count', COALESCE(ha.hub_relations_count, 0),
                'hub_count', COALESCE(ha.hub_count, 0),
                'topology_manifests_count', COALESCE(ha.topology_manifests_count, 0)
             ),
             axis_z_score_json = jsonb_build_object(
                'i', 0,
                'j', 0,
                'k', 0
             ),
             phase_basis_json = jsonb_build_object(
                'basis_source', 'logs.attention',
                'phase_movement_source', 'not_manifest_or_policy_cap',
                'generated_from', 'logs.attention.vector_json',
                'axis_movement_observed', false,
                'axis_movement_note', 'i/j/k are zero placeholders until explicit movement observation is implemented'
             ),
             attractor_vector_json = COALESCE(vb.attractor_vector_json, '{}'::jsonb),
             evaluated_at = now(),
             updated_at = now()
      FROM attention_axis aa
      LEFT JOIN hub_axis ha ON ha.hub_current_id = aa.hub_current_id
      LEFT JOIN vector_basis vb ON vb.hub_current_id = aa.hub_current_id
      WHERE h.hub_current_id = aa.hub_current_id
      RETURNING h.hub_current_id, h.attractor_key, h.population_count, h.population_recordcount,
                h.axis_population_json, h.axis_z_score_json, h.updated_at
    )
    SELECT ap.hub_current_id, ap.attractor_key, ap.population_count, ap.population_recordcount,
           ap.axis_population_json, ap.axis_z_score_json, ap.updated_at
    FROM applied ap;
END;
$$;

-- SQL Attention trigger source only: refresh logs.current from logs.diff, calculate
-- l2_norm/rank/level change detection, and return changed topN candidates. This function
-- does not resolve manifests, explore hubs.hub_relations, or generate phaseAT evidence.
CREATE OR REPLACE FUNCTION logs.refresh_logs_current_watch(
    p_source_set_id TEXT,
    p_basis_window TEXT,
    p_policy_function_name TEXT DEFAULT 'sql_attention_logs_watch',
    p_policy_parameter_key TEXT DEFAULT 'default_policy'
)
RETURNS TABLE (
    current_id UUID,
    physical_table_id TEXT,
    physical_table_name TEXT,
    norm_rank INTEGER,
    previous_norm_level TEXT,
    norm_level TEXT,
    change_detected BOOLEAN,
    change_reason TEXT,
    l2_norm DOUBLE PRECISION,
    basis_vector_json JSONB
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_policy RECORD;
BEGIN
    SELECT *
      INTO v_policy
      FROM logs.resolve_sql_attention_watch_policy(
        p_policy_function_name,
        p_policy_parameter_key
      );

    CREATE TEMP TABLE IF NOT EXISTS tmp_logs_current_before_topn (
        current_id UUID PRIMARY KEY,
        norm_rank INTEGER,
        norm_level TEXT,
        l2_norm DOUBLE PRECISION
    ) ON COMMIT DROP;
    TRUNCATE tmp_logs_current_before_topn;

    INSERT INTO tmp_logs_current_before_topn(current_id, norm_rank, norm_level, l2_norm)
    SELECT c.current_id, c.norm_rank, c.norm_level, c.l2_norm
      FROM logs.current c
     WHERE c.source_set_id = p_source_set_id
       AND c.basis_window = p_basis_window
       AND c.norm_rank IS NOT NULL
       AND c.norm_rank <= v_policy.top_n;

    WITH aggregated AS (
      SELECT
        d.source_set_id,
        d.basis_window,
        d.physical_table_id::TEXT AS physical_table_id,
        d.physical_table_name::TEXT AS physical_table_name,
        jsonb_build_object(
          'diff_count', COUNT(*)::BIGINT,
          'operation_kind_count',
          COALESCE(jsonb_object_agg(d.operation_kind, op_count), '{}'::jsonb)
        ) AS basis_vector_json,
        jsonb_build_object(
          'diff', jsonb_build_object(
            'count_total', COUNT(*)::BIGINT,
            'recordcount_total',
            COUNT(DISTINCT d.record_id)
          )
        ) AS pressure_matrix_json,
        COUNT(*)::BIGINT AS count_total,
        COUNT(DISTINCT d.record_id)::BIGINT AS recordcount_total
      FROM logs.diff d
      JOIN (
        SELECT d2.source_set_id, d2.basis_window, d2.physical_table_id, d2.operation_kind, COUNT(*) AS op_count
          FROM logs.diff d2
         WHERE d2.source_set_id = p_source_set_id
           AND d2.basis_window = p_basis_window
         GROUP BY d2.source_set_id, d2.basis_window, d2.physical_table_id, d2.operation_kind
      ) op ON op.source_set_id = d.source_set_id
          AND op.basis_window = d.basis_window
          AND op.physical_table_id = d.physical_table_id
          AND op.operation_kind = d.operation_kind
      WHERE d.source_set_id = p_source_set_id
        AND d.basis_window = p_basis_window
      GROUP BY d.source_set_id, d.basis_window, d.physical_table_id, d.physical_table_name
    )
    INSERT INTO logs.current (
        source_set_id, basis_window, physical_table_id, physical_table_name,
        basis_vector_json, pressure_matrix_json, count_total, recordcount_total,
        l2_norm, dirty, evaluated_at, updated_at
    )
    SELECT
        a.source_set_id, a.basis_window, a.physical_table_id, a.physical_table_name,
        a.basis_vector_json, a.pressure_matrix_json, a.count_total, a.recordcount_total,
        sqrt(power(a.count_total::DOUBLE PRECISION, 2.0) + power(a.recordcount_total::DOUBLE PRECISION, 2.0)) AS l2_norm,
        true, now(), now()
    FROM aggregated a
    ON CONFLICT ON CONSTRAINT uq_logs_current_source_table
    DO UPDATE SET
        physical_table_name = EXCLUDED.physical_table_name,
        basis_vector_json = EXCLUDED.basis_vector_json,
        pressure_matrix_json = EXCLUDED.pressure_matrix_json,
        count_total = EXCLUDED.count_total,
        recordcount_total = EXCLUDED.recordcount_total,
        l2_norm = EXCLUDED.l2_norm,
        dirty = true,
        evaluated_at = now(),
        updated_at = now();

    WITH ranked AS (
      SELECT
        c.current_id,
        DENSE_RANK() OVER (ORDER BY c.l2_norm DESC, c.updated_at DESC, c.current_id ASC) AS new_rank,
        CASE
          WHEN c.l2_norm >= v_policy.norm_level_high THEN 'high'
          WHEN c.l2_norm >= v_policy.norm_level_medium THEN 'medium'
          ELSE 'low'
        END AS new_level
      FROM logs.current c
      WHERE c.source_set_id = p_source_set_id
        AND c.basis_window = p_basis_window
    )
    UPDATE logs.current c
       SET previous_norm_level = c.norm_level,
           norm_rank = r.new_rank,
           norm_level = r.new_level,
           evaluated_at = now(),
           updated_at = now()
    FROM ranked r
    WHERE c.current_id = r.current_id;

    CREATE TEMP TABLE IF NOT EXISTS tmp_logs_current_after_topn (
        current_id UUID PRIMARY KEY,
        norm_rank INTEGER,
        norm_level TEXT,
        l2_norm DOUBLE PRECISION
    ) ON COMMIT DROP;
    TRUNCATE tmp_logs_current_after_topn;
    INSERT INTO tmp_logs_current_after_topn(current_id, norm_rank, norm_level, l2_norm)
    SELECT c.current_id, c.norm_rank, c.norm_level, c.l2_norm
      FROM logs.current c
     WHERE c.source_set_id = p_source_set_id
       AND c.basis_window = p_basis_window
       AND c.norm_rank IS NOT NULL
       AND c.norm_rank <= v_policy.top_n;

    CREATE TEMP TABLE IF NOT EXISTS tmp_logs_current_watch_reasons (
        current_id UUID PRIMARY KEY,
        reason TEXT
    ) ON COMMIT DROP;
    TRUNCATE tmp_logs_current_watch_reasons;

    INSERT INTO tmp_logs_current_watch_reasons(current_id, reason)
    SELECT
      COALESCE(a.current_id, b.current_id) AS current_id,
      CASE
        WHEN b.current_id IS NULL THEN 'membership_entered_topn'
        WHEN a.current_id IS NULL THEN 'membership_left_topn'
        WHEN b.norm_rank <> a.norm_rank THEN 'order_changed'
        WHEN COALESCE(b.norm_level, '') <> COALESCE(a.norm_level, '') THEN 'level_changed'
        WHEN abs(COALESCE(a.l2_norm, 0) - COALESCE(b.l2_norm, 0)) > v_policy.delta_threshold THEN 'delta_threshold_exceeded'
        ELSE 'no_change'
      END AS reason
    FROM tmp_logs_current_before_topn b
    FULL OUTER JOIN tmp_logs_current_after_topn a ON a.current_id = b.current_id;

    RETURN QUERY
    WITH applied AS (
      UPDATE logs.current c
         SET level_changed = (r.reason IS NOT NULL AND r.reason <> 'no_change'),
             dirty = (r.reason IS NOT NULL AND r.reason <> 'no_change'),
             evaluated_at = now(),
             updated_at = now()
      FROM tmp_logs_current_watch_reasons r
      WHERE c.current_id = r.current_id
      RETURNING c.current_id, c.physical_table_id, c.physical_table_name, c.norm_rank, c.previous_norm_level, c.norm_level,
                c.l2_norm, c.basis_vector_json
    )
    SELECT ap.current_id, ap.physical_table_id, ap.physical_table_name, ap.norm_rank, ap.previous_norm_level, ap.norm_level,
           true AS change_detected,
           rs.reason AS change_reason,
           ap.l2_norm, ap.basis_vector_json
      FROM applied ap
      JOIN tmp_logs_current_watch_reasons rs ON rs.current_id = ap.current_id
     WHERE rs.reason IS NOT NULL
       AND rs.reason <> 'no_change';
END;
$$;

-- =============================================================================
-- manifest_topology_key_expansion_draft_lane
-- SSOT: docs/design/sql-attention-logs-ssot.yaml#manifest_topology_key_expansion_draft_lane
--       docs/design/sql-attention-logs-ssot.md (SQL Attention manifest topology key expansion draft lane)
--
-- SQL-only evidence consumer lane. It consumes logs.attention SQLAT evidence
-- (source=sql_attention), extracts high-pressure discrete Keys from the
-- SQL-Attention-explored hub_relation neighborhood (Key extraction space only),
-- expands those Keys across the FULL registered manifest topology space, and
-- inserts insert-only draft candidate JSONB (authority) + a human-readable
-- Markdown projection. An AFTER INSERT trigger emits a structured DB NOTIFY.
--
-- Boundary invariants (enforced by CHECK constraints + guard tests):
--   - draft candidate JSONB is the authority record (Markdown is not authority).
--   - insert-only: no UPDATE / DELETE on these tables, no UPDATE/DELETE on logs.attention.
--   - candidate inference is SQL-driven; C# is orchestration / notification bridge only.
--   - no active manifest / topology registry / hub_relation / runtime route mutation.
--   - no auto-apply / auto-promote.
--   - Markdown body is plain human-readable text: no raw HTML / island markup /
--     CSS class authority / executable script / promotion instruction as authority.
--   - SQL does not generate UI placement; the UI decides display surface.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- logs.sql_attention_draft_candidate
-- Insert-only draft candidate authority record. The JSONB payload is canonical;
-- Markdown projection is a derived human-readable view only.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.sql_attention_draft_candidate (
    candidate_id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_set_id          TEXT        NOT NULL,
    basis_window           TEXT,
    candidate_type         TEXT        NOT NULL
        CHECK (candidate_type IN ('relationship_axis_candidate', 'meaning_projection_candidate', 'aggregate_projection_candidate')),
    source                 TEXT        NOT NULL DEFAULT 'sql_attention'
        CHECK (source = 'sql_attention'),
    candidate_lane         TEXT        NOT NULL DEFAULT 'manifest_topology_key_expansion_draft_lane'
        CHECK (candidate_lane = 'manifest_topology_key_expansion_draft_lane'),
    status                 TEXT        NOT NULL DEFAULT 'draft'
        CHECK (status = 'draft'),
    source_evidence_refs   JSONB       NOT NULL DEFAULT '[]'::jsonb,
    high_pressure_key      JSONB       NOT NULL DEFAULT '{}'::jsonb,
    hit_manifest_refs      JSONB       NOT NULL DEFAULT '[]'::jsonb,
    hit_table_refs         JSONB       NOT NULL DEFAULT '[]'::jsonb,
    common_axis_candidates JSONB       NOT NULL DEFAULT '[]'::jsonb,
    candidate_columns      JSONB       NOT NULL DEFAULT '[]'::jsonb,
    score                  DOUBLE PRECISION NOT NULL DEFAULT 0,
    candidate_payload_json JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    archive_policy         TEXT        NOT NULL DEFAULT 'required'
);

COMMENT ON TABLE logs.sql_attention_draft_candidate IS
  'Insert-only manifest_topology_key_expansion_draft_lane draft candidate authority record. '
  'source=sql_attention, candidate_lane=manifest_topology_key_expansion_draft_lane, status=draft. '
  'JSONB payload is the authority; Markdown projection is a derived human-readable view. '
  'No auto-apply / auto-promote; adoption/promotion/placement remain explicit operations.';

CREATE INDEX IF NOT EXISTS idx_sql_attention_draft_candidate_source_set
  ON logs.sql_attention_draft_candidate (source_set_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_sql_attention_draft_candidate_lane
  ON logs.sql_attention_draft_candidate (candidate_lane, status);
CREATE INDEX IF NOT EXISTS idx_sql_attention_draft_candidate_type
  ON logs.sql_attention_draft_candidate (candidate_type);

-- ---------------------------------------------------------------------------
-- logs.sql_attention_draft_markdown_projection
-- Insert-only human-readable Markdown projection of a draft candidate.
-- Authority remains the draft candidate JSONB. Recommended display surface is
-- the Markdown viewer / team markdown dashboard saved-view; placement is a UI
-- decision, never decided by SQL.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.sql_attention_draft_markdown_projection (
    markdown_projection_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    candidate_id           UUID        NOT NULL REFERENCES logs.sql_attention_draft_candidate (candidate_id) ON DELETE RESTRICT,
    source_set_id          TEXT        NOT NULL,
    candidate_lane         TEXT        NOT NULL DEFAULT 'manifest_topology_key_expansion_draft_lane'
        CHECK (candidate_lane = 'manifest_topology_key_expansion_draft_lane'),
    rendered_markdown      TEXT        NOT NULL DEFAULT '',
    markdown_meta_json     JSONB       NOT NULL DEFAULT '{}'::jsonb,
    recommended_surface    TEXT        NOT NULL DEFAULT 'team_markdown_dashboard_saved_view',
    created_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    archive_policy         TEXT        NOT NULL DEFAULT 'required'
);

COMMENT ON TABLE logs.sql_attention_draft_markdown_projection IS
  'Insert-only human-readable Markdown projection for a SQL Attention draft candidate. '
  'Authority is logs.sql_attention_draft_candidate JSONB, not this rendered_markdown body. '
  'Markdown is review/search/dashboard projection only; it is not runtime SSOT, topology '
  'promotion authority, event wiring authority, or UI placement authority.';

CREATE INDEX IF NOT EXISTS idx_sql_attention_draft_markdown_candidate
  ON logs.sql_attention_draft_markdown_projection (candidate_id);
CREATE INDEX IF NOT EXISTS idx_sql_attention_draft_markdown_source_set
  ON logs.sql_attention_draft_markdown_projection (source_set_id, created_at DESC);

-- ---------------------------------------------------------------------------
-- logs.sql_attention_jsonb_leaves
-- Recursive scalar-leaf walker for topology / relation JSONB. Returns each
-- scalar leaf as (key_name, key_value, value_kind). Used to discover discrete
-- Keys and to search the manifest topology space. Builtins only (load-order safe).
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.sql_attention_jsonb_leaves(p_doc JSONB)
RETURNS TABLE (key_name TEXT, key_value TEXT, value_kind TEXT)
LANGUAGE sql
IMMUTABLE
AS $$
    WITH RECURSIVE walk(k, v) AS (
        SELECT NULL::text, COALESCE(p_doc, '{}'::jsonb)
      UNION ALL
        SELECT child.k, child.v
        FROM walk
        CROSS JOIN LATERAL (
            SELECT e.key AS k, e.value AS v
              FROM jsonb_each(walk.v) e
             WHERE jsonb_typeof(walk.v) = 'object'
          UNION ALL
            SELECT walk.k AS k, a.value AS v
              FROM jsonb_array_elements(walk.v) a
             WHERE jsonb_typeof(walk.v) = 'array'
        ) child
    )
    SELECT k AS key_name,
           (v #>> '{}') AS key_value,
           jsonb_typeof(v) AS value_kind
      FROM walk
     WHERE k IS NOT NULL
       AND jsonb_typeof(v) IN ('string', 'number', 'boolean');
$$;

COMMENT ON FUNCTION logs.sql_attention_jsonb_leaves(JSONB) IS
  'Recursive scalar-leaf walker for JSONB. Returns (key_name, key_value, value_kind) for each scalar leaf, descending objects and arrays.';

-- ---------------------------------------------------------------------------
-- logs.resolve_sql_attention_key_expansion_policy
-- Data-defined scoring/dampening policy resolver. Fail-close on missing policy
-- or missing required keys. No hidden literals in the lane scoring path.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.resolve_sql_attention_key_expansion_policy(
    p_policy_function_name TEXT DEFAULT 'sql_attention_manifest_topology_key_expansion',
    p_policy_parameter_key TEXT DEFAULT 'default_policy'
)
RETURNS JSONB
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    v_policy JSONB;
BEGIN
    SELECT fp.parameter_value
      INTO v_policy
      FROM topology.function_parameters fp
     WHERE fp.function_name = p_policy_function_name
       AND fp.parameter_key = p_policy_parameter_key
       AND fp.active = true
     ORDER BY fp.created_at DESC
     LIMIT 1;

    IF v_policy IS NULL THEN
        RAISE EXCEPTION
            'SQL Attention key expansion policy missing: function_name=% parameter_key=%',
            p_policy_function_name, p_policy_parameter_key;
    END IF;

    IF NOT (
        v_policy ? 'max_keys'
        AND v_policy ? 'max_candidates'
        AND v_policy ? 'min_candidate_score'
        AND v_policy ? 'discrete_key_name_patterns'
        AND v_policy ? 'generic_column_names'
        AND v_policy ? 'id_column_suffixes'
        AND v_policy ? 'score_weights'
    ) THEN
        RAISE EXCEPTION
            'SQL Attention key expansion policy keys missing. required=[max_keys, max_candidates, min_candidate_score, discrete_key_name_patterns, generic_column_names, id_column_suffixes, score_weights], value=%',
            v_policy;
    END IF;

    RETURN v_policy;
END;
$$;

-- ---------------------------------------------------------------------------
-- logs.extract_sql_attention_high_pressure_keys
-- Key extraction space = SQL-Attention-explored hub_relation neighborhood only.
-- Reads logs.attention SQLAT evidence (source=sql_attention), resolves the
-- neighborhood manifests, and extracts high-pressure discrete Keys from the
-- neighborhood manifest topology JSONB and hub_relation config JSONB.
-- This function never mines the full schema space; full-space expansion is a
-- separate compile step that depends on these extracted Keys (no SQLAT evidence
-- => no Keys => no candidates).
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.extract_sql_attention_high_pressure_keys(
    p_source_set_id TEXT,
    p_basis_window TEXT DEFAULT NULL,
    p_policy_function_name TEXT DEFAULT 'sql_attention_manifest_topology_key_expansion',
    p_policy_parameter_key TEXT DEFAULT 'default_policy'
)
RETURNS TABLE (
    key_name TEXT,
    key_type TEXT,
    key_value TEXT,
    key_pressure DOUBLE PRECISION,
    neighborhood_manifest_ids UUID[],
    source_evidence_refs JSONB
)
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    v_policy JSONB := logs.resolve_sql_attention_key_expansion_policy(p_policy_function_name, p_policy_parameter_key);
    v_max_keys INT := (v_policy->>'max_keys')::int;
    v_min_pressure DOUBLE PRECISION := COALESCE((v_policy->>'min_key_pressure')::double precision, 1.0);
    v_patterns TEXT := array_to_string(ARRAY(SELECT jsonb_array_elements_text(v_policy->'discrete_key_name_patterns')), '|');
BEGIN
    RETURN QUERY
    WITH sqlat AS (
        SELECT a.attention_id,
               a.source_topology_manifest_ids,
               a.expanded_topology_manifest_ids,
               a.hit_hub_relation_ids
          FROM logs.attention a
         WHERE a.source_set_id = p_source_set_id
           AND a.evidence_kind = 'sql_attention_hit'
    ),
    nbhd AS (
        SELECT DISTINCT s.attention_id, m AS topology_manifest_id
          FROM sqlat s,
               unnest(s.source_topology_manifest_ids || s.expanded_topology_manifest_ids) AS m
         WHERE m IS NOT NULL
        UNION
        SELECT DISTINCT s.attention_id, hr.topology_manifest_id
          FROM sqlat s,
               unnest(s.hit_hub_relation_ids) AS rel
          JOIN hubs.hub_relations hr ON hr.hub_relation_id = rel
    ),
    nbhd_docs AS (
        SELECT n.attention_id, n.topology_manifest_id, tm.topology_jsonb AS doc
          FROM nbhd n
          JOIN hubs.topology_manifests tm ON tm.topology_manifest_id = n.topology_manifest_id
        UNION ALL
        SELECT n.attention_id, n.topology_manifest_id, hr.relation_config AS doc
          FROM nbhd n
          JOIN hubs.hub_relations hr ON hr.topology_manifest_id = n.topology_manifest_id
    ),
    leaves AS (
        SELECT d.attention_id, d.topology_manifest_id, l.key_name, l.key_value, l.value_kind
          FROM nbhd_docs d
          CROSS JOIN LATERAL logs.sql_attention_jsonb_leaves(d.doc) l
         WHERE l.key_name IS NOT NULL
           AND l.key_value IS NOT NULL
           AND length(l.key_value) <= 128
           AND (
                 (v_patterns <> '' AND l.key_name ~* v_patterns)
                 OR l.value_kind = 'boolean'
               )
    ),
    agg AS (
        SELECT l.key_name,
               l.value_kind AS key_type,
               l.key_value,
               COUNT(*)::double precision AS key_pressure,
               array_agg(DISTINCT l.topology_manifest_id) AS neighborhood_manifest_ids,
               jsonb_agg(DISTINCT l.attention_id) AS source_evidence_refs
          FROM leaves l
         GROUP BY l.key_name, l.value_kind, l.key_value
    )
    SELECT a.key_name, a.key_type, a.key_value, a.key_pressure,
           a.neighborhood_manifest_ids, a.source_evidence_refs
      FROM agg a
     WHERE a.key_pressure >= v_min_pressure
     ORDER BY a.key_pressure DESC, a.key_name
     LIMIT v_max_keys;
END;
$$;

COMMENT ON FUNCTION logs.extract_sql_attention_high_pressure_keys(TEXT, TEXT, TEXT, TEXT) IS
  'Extracts high-pressure discrete Keys from the SQL-Attention-explored hub_relation neighborhood only. '
  'Depends on logs.attention SQLAT evidence; no SQLAT evidence yields no Keys (no full-space schema mining without evidence).';

-- ---------------------------------------------------------------------------
-- logs.compile_sql_attention_manifest_topology_draft_candidates
-- Full-space expansion + hit-set re-aggregation + scoring.
-- Reads extracted high-pressure Keys and expands them across the FULL registered
-- manifest topology space (topology manifest JSONB, screen data shape JSONB
-- embedded in topology_jsonb, logical tables/columns via schema_registry +
-- physical_tables, enum group/discrete-value metadata, physical_table_manifest
-- bindings). Re-aggregates same-name / same-type / common axis, enum group match,
-- value overlap, logs.diff pressure, logs.attention pressure, table-ref reuse,
-- and manifest reuse. Scoring is NOT raw count: it applies routine high-frequency
-- value dampening, lift / pressure delta, ID-column axis handling, and generic
-- column dampening from the data-defined policy. Read-only (no insert).
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.compile_sql_attention_manifest_topology_draft_candidates(
    p_source_set_id TEXT,
    p_basis_window TEXT DEFAULT NULL,
    p_policy_function_name TEXT DEFAULT 'sql_attention_manifest_topology_key_expansion',
    p_policy_parameter_key TEXT DEFAULT 'default_policy'
)
RETURNS TABLE (
    candidate_type TEXT,
    high_pressure_key JSONB,
    source_evidence_refs JSONB,
    hit_manifest_refs JSONB,
    hit_table_refs JSONB,
    common_axis_candidates JSONB,
    candidate_columns JSONB,
    score DOUBLE PRECISION,
    candidate_payload_json JSONB
)
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    v_policy JSONB := logs.resolve_sql_attention_key_expansion_policy(p_policy_function_name, p_policy_parameter_key);
    v_w JSONB := v_policy->'score_weights';
    v_generic TEXT := array_to_string(ARRAY(SELECT '(^|[_.])' || x || '($|[_.])' FROM jsonb_array_elements_text(v_policy->'generic_column_names') x), '|');
    v_idsfx TEXT := array_to_string(ARRAY(SELECT regexp_replace(x, '([.^$*+?()\[\]{}|\\])', '\\\1', 'g') || '$' FROM jsonb_array_elements_text(v_policy->'id_column_suffixes') x), '|');
    v_min_score DOUBLE PRECISION := (v_policy->>'min_candidate_score')::double precision;
    v_max_candidates INT := (v_policy->>'max_candidates')::int;
    v_routine_fraction DOUBLE PRECISION := COALESCE((v_policy->>'routine_value_manifest_fraction_dampen')::double precision, 0.6);
    v_routine_dampen DOUBLE PRECISION := COALESCE((v_policy->>'routine_value_dampen_factor')::double precision, 0.4);
    v_generic_dampen DOUBLE PRECISION := COALESCE((v_policy->>'generic_column_dampen_factor')::double precision, 0.5);
    v_lift_min DOUBLE PRECISION := COALESCE((v_policy->>'lift_min')::double precision, 1.0);
    v_total_manifests DOUBLE PRECISION;
    -- score weights (data-defined; fail-soft to 0 weight when absent)
    w_same_name DOUBLE PRECISION := COALESCE((v_w->>'same_name_axis')::double precision, 0);
    w_same_type DOUBLE PRECISION := COALESCE((v_w->>'same_type_axis')::double precision, 0);
    w_common DOUBLE PRECISION := COALESCE((v_w->>'common_axis')::double precision, 0);
    w_enum DOUBLE PRECISION := COALESCE((v_w->>'enum_group_match')::double precision, 0);
    w_value_overlap DOUBLE PRECISION := COALESCE((v_w->>'value_overlap')::double precision, 0);
    w_diff DOUBLE PRECISION := COALESCE((v_w->>'logs_diff_pressure')::double precision, 0);
    w_attn DOUBLE PRECISION := COALESCE((v_w->>'logs_attention_pressure')::double precision, 0);
    w_table_reuse DOUBLE PRECISION := COALESCE((v_w->>'table_ref_reuse')::double precision, 0);
    w_manifest_reuse DOUBLE PRECISION := COALESCE((v_w->>'manifest_reuse')::double precision, 0);
    w_id_axis DOUBLE PRECISION := COALESCE((v_w->>'id_axis')::double precision, 0);
    w_lift DOUBLE PRECISION := COALESCE((v_w->>'lift')::double precision, 0);
BEGIN
    SELECT GREATEST(COUNT(*), 1)::double precision
      INTO v_total_manifests
      FROM hubs.topology_manifests
     WHERE status = 'active';

    RETURN QUERY
    WITH keys AS (
        SELECT * FROM logs.extract_sql_attention_high_pressure_keys(
            p_source_set_id, p_basis_window, p_policy_function_name, p_policy_parameter_key)
    ),
    -- FULL-space manifest topology leaves (topology manifest + screen data shape JSONB).
    manifest_leaves AS (
        SELECT tm.topology_manifest_id, tm.manifest_key, l.key_name, l.key_value, l.value_kind
          FROM hubs.topology_manifests tm
          CROSS JOIN LATERAL logs.sql_attention_jsonb_leaves(tm.topology_jsonb) l
         WHERE tm.status = 'active'
    ),
    -- FULL-space logical column space (schema_registry schema_def leaves).
    schema_leaves AS (
        SELECT sr.schema_id, sr.name AS schema_name, l.key_name
          FROM topology.schema_registry sr
          CROSS JOIN LATERAL logs.sql_attention_jsonb_leaves(sr.schema_def) l
         WHERE sr.active = true
           AND l.key_name IS NOT NULL
    ),
    -- per-key manifest hit set (same-name OR value overlap across full space)
    mhit AS (
        SELECT k.key_name, k.key_value,
               ml.topology_manifest_id, ml.manifest_key,
               bool_or(ml.key_name = k.key_name) AS same_name,
               bool_or(ml.value_kind = k.key_type) AS same_type,
               bool_or(ml.key_name = k.key_name AND ml.value_kind = k.key_type) AS common_axis,
               bool_or(ml.key_value = k.key_value) AS value_overlap
          FROM keys k
          JOIN manifest_leaves ml
            ON ml.key_name = k.key_name OR ml.key_value = k.key_value
         GROUP BY k.key_name, k.key_value, ml.topology_manifest_id, ml.manifest_key
    ),
    mhit_agg AS (
        SELECT key_name, key_value,
               COUNT(*) FILTER (WHERE same_name)    AS same_name_axis_count,
               COUNT(*) FILTER (WHERE same_type)    AS same_type_axis_count,
               COUNT(*) FILTER (WHERE common_axis)  AS common_axis_count,
               COUNT(*) FILTER (WHERE value_overlap) AS value_overlap_count,
               COUNT(*)                              AS manifest_reuse,
               jsonb_agg(DISTINCT jsonb_build_object(
                   'topology_manifest_id', topology_manifest_id,
                   'manifest_key', manifest_key)) AS hit_manifest_refs,
               array_agg(DISTINCT topology_manifest_id) AS manifest_ids
          FROM mhit
         GROUP BY key_name, key_value
    ),
    schema_agg AS (
        SELECT k.key_name,
               COUNT(DISTINCT sl.schema_id) AS schema_hit_count,
               jsonb_agg(DISTINCT jsonb_build_object(
                   'schema_id', sl.schema_id,
                   'schema_name', sl.schema_name,
                   'column', sl.key_name)) AS candidate_columns
          FROM keys k
          JOIN schema_leaves sl ON sl.key_name = k.key_name
         GROUP BY k.key_name
    ),
    enum_agg AS (
        SELECT k.key_name, k.key_value,
               COUNT(DISTINCT g.group_id) AS enum_group_match,
               jsonb_agg(DISTINCT jsonb_build_object('group_id', g.group_id, 'group_name', g.group_name)) AS enum_groups
          FROM keys k
          JOIN enum.groups g
            ON g.group_name ILIKE '%' || k.key_name || '%'
            OR EXISTS (
                 SELECT 1
                   FROM enum.group_items gi
                   JOIN enum.items it ON it.index_num = gi.enum_index_num
                  WHERE gi.group_id = g.group_id
                    AND it.name = k.key_value)
         GROUP BY k.key_name, k.key_value
    ),
    table_agg AS (
        SELECT ma.key_name, ma.key_value,
               COUNT(DISTINCT b.physical_table_id) AS table_ref_reuse,
               jsonb_agg(DISTINCT jsonb_build_object(
                   'physical_table_id', b.physical_table_id,
                   'table_ref', pt.table_ref)) AS hit_table_refs,
               COALESCE(SUM(dpr.diff_pressure), 0)::double precision AS logs_diff_pressure
          FROM mhit_agg ma
          JOIN topology.physical_table_manifest_bindings b
            ON b.topology_manifest_id = ANY(ma.manifest_ids) AND b.active = true
          JOIN topology.physical_tables pt ON pt.physical_table_id = b.physical_table_id
          LEFT JOIN LATERAL (
              SELECT COUNT(*)::double precision AS diff_pressure
                FROM logs.diff d
               WHERE d.physical_table_id = pt.physical_table_id::text
                  OR d.physical_table_name = pt.table_ref
          ) dpr ON true
         GROUP BY ma.key_name, ma.key_value
    ),
    attn_agg AS (
        SELECT ma.key_name, ma.key_value,
               COUNT(*)::double precision AS logs_attention_pressure
          FROM mhit_agg ma
          JOIN logs.attention a
            ON a.source_topology_manifest_ids && ma.manifest_ids
         GROUP BY ma.key_name, ma.key_value
    ),
    scored AS (
        SELECT
            k.key_name, k.key_type, k.key_value, k.key_pressure, k.source_evidence_refs,
            COALESCE(ma.same_name_axis_count, 0)  AS same_name_axis_count,
            COALESCE(ma.same_type_axis_count, 0)  AS same_type_axis_count,
            COALESCE(ma.common_axis_count, 0)     AS common_axis_count,
            COALESCE(ma.value_overlap_count, 0)   AS value_overlap_count,
            COALESCE(ma.manifest_reuse, 0)        AS manifest_reuse,
            COALESCE(ma.hit_manifest_refs, '[]'::jsonb) AS hit_manifest_refs,
            COALESCE(sa.candidate_columns, '[]'::jsonb) AS candidate_columns,
            COALESCE(sa.schema_hit_count, 0)      AS schema_hit_count,
            COALESCE(ea.enum_group_match, 0)      AS enum_group_match,
            COALESCE(ea.enum_groups, '[]'::jsonb) AS enum_groups,
            COALESCE(ta.table_ref_reuse, 0)       AS table_ref_reuse,
            COALESCE(ta.hit_table_refs, '[]'::jsonb) AS hit_table_refs,
            COALESCE(ta.logs_diff_pressure, 0)    AS logs_diff_pressure,
            COALESCE(an.logs_attention_pressure, 0) AS logs_attention_pressure,
            (k.key_name ~* v_idsfx)               AS is_id_axis,
            (v_generic <> '' AND k.key_name ~* v_generic) AS is_generic,
            (COALESCE(ma.value_overlap_count, 0)::double precision / v_total_manifests) AS routine_fraction,
            (k.key_pressure / (1.0 + COALESCE(ma.value_overlap_count, 0)::double precision)) AS lift
          FROM keys k
          LEFT JOIN mhit_agg ma ON ma.key_name = k.key_name AND ma.key_value = k.key_value
          LEFT JOIN schema_agg sa ON sa.key_name = k.key_name
          LEFT JOIN enum_agg ea ON ea.key_name = k.key_name AND ea.key_value = k.key_value
          LEFT JOIN table_agg ta ON ta.key_name = k.key_name AND ta.key_value = k.key_value
          LEFT JOIN attn_agg an ON an.key_name = k.key_name AND an.key_value = k.key_value
    ),
    final AS (
        SELECT s.*,
            -- ID columns are axis / dimension candidates, never primary display text.
            CASE
                WHEN s.is_id_axis THEN 'relationship_axis_candidate'
                WHEN s.enum_group_match > 0 OR s.value_overlap_count > 0 THEN 'meaning_projection_candidate'
                ELSE 'aggregate_projection_candidate'
            END AS candidate_type,
            (
              (
                w_same_name      * s.same_name_axis_count
              + w_same_type      * s.same_type_axis_count
              + w_common         * s.common_axis_count
              + w_enum           * s.enum_group_match
              + w_value_overlap  * s.value_overlap_count
              + w_diff           * ln(1.0 + s.logs_diff_pressure)
              + w_attn           * ln(1.0 + s.logs_attention_pressure)
              + w_table_reuse    * s.table_ref_reuse
              + w_manifest_reuse * s.manifest_reuse
              + (CASE WHEN s.is_id_axis THEN w_id_axis ELSE 0 END)
              + (CASE WHEN s.lift >= v_lift_min THEN w_lift * s.lift ELSE 0 END)
              )
              -- routine high-frequency value dampening
              * (CASE WHEN s.routine_fraction > v_routine_fraction THEN v_routine_dampen ELSE 1.0 END)
              -- generic column dampening
              * (CASE WHEN s.is_generic THEN v_generic_dampen ELSE 1.0 END)
            ) AS score
          FROM scored s
    )
    SELECT
        f.candidate_type,
        jsonb_build_object(
            'key_name', f.key_name,
            'key_type', f.key_type,
            'key_value', f.key_value,
            'key_pressure', f.key_pressure,
            'is_id_axis', f.is_id_axis,
            'is_generic', f.is_generic) AS high_pressure_key,
        f.source_evidence_refs,
        f.hit_manifest_refs,
        f.hit_table_refs,
        (CASE WHEN f.common_axis_count > 0 OR f.same_name_axis_count > 0
              THEN f.hit_manifest_refs ELSE '[]'::jsonb END) AS common_axis_candidates,
        f.candidate_columns,
        f.score,
        jsonb_build_object(
            'source', 'sql_attention',
            'candidate_lane', 'manifest_topology_key_expansion_draft_lane',
            'status', 'draft',
            'candidate_type', f.candidate_type,
            'high_pressure_key', jsonb_build_object(
                'key_name', f.key_name, 'key_type', f.key_type, 'key_value', f.key_value,
                'key_pressure', f.key_pressure, 'is_id_axis', f.is_id_axis, 'is_generic', f.is_generic),
            'source_evidence_refs', f.source_evidence_refs,
            'hit_manifest_refs', f.hit_manifest_refs,
            'hit_table_refs', f.hit_table_refs,
            'common_axis_candidates', (CASE WHEN f.common_axis_count > 0 OR f.same_name_axis_count > 0
                                            THEN f.hit_manifest_refs ELSE '[]'::jsonb END),
            'candidate_columns', f.candidate_columns,
            'enum_groups', f.enum_groups,
            'aggregation', jsonb_build_object(
                'same_name_axis', f.same_name_axis_count,
                'same_type_axis', f.same_type_axis_count,
                'same_name_and_same_type_common_axis', f.common_axis_count,
                'enum_group_match', f.enum_group_match,
                'value_overlap', f.value_overlap_count,
                'logs_diff_pressure', f.logs_diff_pressure,
                'logs_attention_pressure', f.logs_attention_pressure,
                'table_ref_reuse', f.table_ref_reuse,
                'manifest_reuse', f.manifest_reuse,
                'schema_hit_count', f.schema_hit_count),
            'scoring', jsonb_build_object(
                'score', f.score,
                'lift', f.lift,
                'routine_fraction', f.routine_fraction,
                'routine_dampened', (f.routine_fraction > v_routine_fraction),
                'generic_dampened', f.is_generic,
                'id_axis_treated_as_dimension', f.is_id_axis,
                'raw_count_only', false),
            'score', f.score,
            'status_note', 'draft candidate JSONB is authority; markdown is human-readable projection only') AS candidate_payload_json
      FROM final f
     WHERE f.score >= v_min_score
     ORDER BY f.score DESC, f.key_name
     LIMIT v_max_candidates;
END;
$$;

COMMENT ON FUNCTION logs.compile_sql_attention_manifest_topology_draft_candidates(TEXT, TEXT, TEXT, TEXT) IS
  'Expands extracted high-pressure Keys across the FULL registered manifest topology space and re-aggregates common-axis / column-pressure hit sets with dampening + lift scoring. Read-only candidate inference (SQL-driven, not C# hardcoded).';

-- ---------------------------------------------------------------------------
-- logs.render_sql_attention_draft_candidate_markdown
-- SQL-generated human-readable Markdown projection body. PLAIN markdown only:
-- headings, bullet lists, inline code. No raw HTML, no island markup, no CSS
-- class authority, no executable script, no promotion instruction as authority.
-- The UI decides display surface; this body is review/search text only.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.render_sql_attention_draft_candidate_markdown(
    p_candidate_id UUID,
    p_candidate_type TEXT,
    p_high_pressure_key JSONB,
    p_hit_manifest_refs JSONB,
    p_common_axis_candidates JSONB,
    p_candidate_columns JSONB,
    p_score DOUBLE PRECISION
)
RETURNS TEXT
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
    v_md TEXT;
    v_manifest_lines TEXT;
    v_column_lines TEXT;
BEGIN
    SELECT string_agg('- manifest `' || COALESCE(e->>'manifest_key', e->>'topology_manifest_id') || '`', E'\n')
      INTO v_manifest_lines
      FROM jsonb_array_elements(COALESCE(p_hit_manifest_refs, '[]'::jsonb)) e;

    SELECT string_agg('- column `' || COALESCE(e->>'column', '') || '` (' || COALESCE(e->>'schema_name', '') || ')', E'\n')
      INTO v_column_lines
      FROM jsonb_array_elements(COALESCE(p_candidate_columns, '[]'::jsonb)) e;

    v_md :=
        '# SQL Attention draft candidate' || E'\n\n'
     || '- Candidate type: ' || COALESCE(p_candidate_type, '') || E'\n'
     || '- Source: sql_attention' || E'\n'
     || '- Candidate lane: manifest_topology_key_expansion_draft_lane' || E'\n'
     || '- Status: draft (review only — not adopted, not promoted, not placed)' || E'\n'
     || '- Candidate id: ' || p_candidate_id::text || E'\n'
     || '- Score: ' || round(p_score::numeric, 4)::text || E'\n\n'
     || '## High-pressure key' || E'\n\n'
     || '- Key name: `' || COALESCE(p_high_pressure_key->>'key_name', '') || '`' || E'\n'
     || '- Key type: ' || COALESCE(p_high_pressure_key->>'key_type', '') || E'\n'
     || '- Key value: `' || COALESCE(p_high_pressure_key->>'key_value', '') || '`' || E'\n\n'
     || '## Hit manifest topology' || E'\n\n'
     || COALESCE(v_manifest_lines, '- (none)') || E'\n\n'
     || '## Candidate columns' || E'\n\n'
     || COALESCE(v_column_lines, '- (none)') || E'\n\n'
     || '> Authority is the draft candidate JSONB record. This Markdown is a human-readable '
     || 'review/search projection only; it does not decide topology promotion or UI placement.';

    RETURN v_md;
END;
$$;

-- ---------------------------------------------------------------------------
-- logs.insert_sql_attention_draft_candidate
-- Insert-only writer for one compiled draft candidate. Inserts the authority
-- JSONB record, renders + inserts the Markdown projection, and (via AFTER INSERT
-- trigger on the projection) emits the structured DB NOTIFY. No mutation of
-- active manifests / topology registry / hub_relations / runtime routes.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.insert_sql_attention_draft_candidate(
    p_source_set_id TEXT,
    p_basis_window TEXT,
    p_candidate_type TEXT,
    p_source_evidence_refs JSONB,
    p_high_pressure_key JSONB,
    p_hit_manifest_refs JSONB,
    p_hit_table_refs JSONB,
    p_common_axis_candidates JSONB,
    p_candidate_columns JSONB,
    p_score DOUBLE PRECISION,
    p_candidate_payload_json JSONB
)
RETURNS TABLE (candidate_id UUID, markdown_projection_id UUID)
LANGUAGE plpgsql
AS $$
DECLARE
    v_candidate_id UUID := gen_random_uuid();
    v_markdown_id UUID;
    v_markdown TEXT;
BEGIN
    INSERT INTO logs.sql_attention_draft_candidate (
        candidate_id, source_set_id, basis_window, candidate_type,
        source, candidate_lane, status,
        source_evidence_refs, high_pressure_key, hit_manifest_refs, hit_table_refs,
        common_axis_candidates, candidate_columns, score, candidate_payload_json,
        archive_policy
    ) VALUES (
        v_candidate_id, p_source_set_id, p_basis_window, p_candidate_type,
        'sql_attention', 'manifest_topology_key_expansion_draft_lane', 'draft',
        COALESCE(p_source_evidence_refs, '[]'::jsonb),
        COALESCE(p_high_pressure_key, '{}'::jsonb),
        COALESCE(p_hit_manifest_refs, '[]'::jsonb),
        COALESCE(p_hit_table_refs, '[]'::jsonb),
        COALESCE(p_common_axis_candidates, '[]'::jsonb),
        COALESCE(p_candidate_columns, '[]'::jsonb),
        COALESCE(p_score, 0),
        COALESCE(p_candidate_payload_json, '{}'::jsonb),
        'required'
    );

    v_markdown := logs.render_sql_attention_draft_candidate_markdown(
        v_candidate_id, p_candidate_type, p_high_pressure_key,
        p_hit_manifest_refs, p_common_axis_candidates, p_candidate_columns, p_score);

    INSERT INTO logs.sql_attention_draft_markdown_projection (
        candidate_id, source_set_id, candidate_lane, rendered_markdown,
        markdown_meta_json, recommended_surface, archive_policy
    ) VALUES (
        v_candidate_id, p_source_set_id, 'manifest_topology_key_expansion_draft_lane', v_markdown,
        jsonb_build_object(
            'authority_record', 'logs.sql_attention_draft_candidate',
            'markdown_is_authority', false,
            'rendered_by', 'sql'),
        'team_markdown_dashboard_saved_view', 'required'
    )
    RETURNING logs.sql_attention_draft_markdown_projection.markdown_projection_id
    INTO v_markdown_id;

    candidate_id := v_candidate_id;
    markdown_projection_id := v_markdown_id;
    RETURN NEXT;
END;
$$;

COMMENT ON FUNCTION logs.insert_sql_attention_draft_candidate(TEXT, TEXT, TEXT, JSONB, JSONB, JSONB, JSONB, JSONB, JSONB, DOUBLE PRECISION, JSONB) IS
  'Insert-only writer: inserts draft candidate authority JSONB + rendered Markdown projection. AFTER INSERT trigger emits structured DB NOTIFY. No auto-apply/auto-promote/active-manifest mutation.';

-- ---------------------------------------------------------------------------
-- logs.run_sql_attention_manifest_topology_key_expansion_draft_lane
-- Orchestrating SQL entry point invoked by the C# scheduler bridge. Compiles
-- candidates (SQL-driven inference) and inserts each one insert-only. C# never
-- performs candidate inference; it only calls this function and bridges NOTIFY.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.run_sql_attention_manifest_topology_key_expansion_draft_lane(
    p_source_set_id TEXT,
    p_basis_window TEXT DEFAULT NULL,
    p_policy_function_name TEXT DEFAULT 'sql_attention_manifest_topology_key_expansion',
    p_policy_parameter_key TEXT DEFAULT 'default_policy'
)
RETURNS TABLE (
    candidate_id UUID,
    markdown_projection_id UUID,
    candidate_type TEXT,
    candidate_lane TEXT,
    score DOUBLE PRECISION
)
LANGUAGE plpgsql
AS $$
DECLARE
    r RECORD;
    ins RECORD;
BEGIN
    FOR r IN
        SELECT * FROM logs.compile_sql_attention_manifest_topology_draft_candidates(
            p_source_set_id, p_basis_window, p_policy_function_name, p_policy_parameter_key)
    LOOP
        SELECT * INTO ins
          FROM logs.insert_sql_attention_draft_candidate(
            p_source_set_id, p_basis_window, r.candidate_type,
            r.source_evidence_refs, r.high_pressure_key, r.hit_manifest_refs,
            r.hit_table_refs, r.common_axis_candidates, r.candidate_columns,
            r.score, r.candidate_payload_json);

        candidate_id := ins.candidate_id;
        markdown_projection_id := ins.markdown_projection_id;
        candidate_type := r.candidate_type;
        candidate_lane := 'manifest_topology_key_expansion_draft_lane';
        score := r.score;
        RETURN NEXT;
    END LOOP;
END;
$$;

COMMENT ON FUNCTION logs.run_sql_attention_manifest_topology_key_expansion_draft_lane(TEXT, TEXT, TEXT, TEXT) IS
  'C#-invoked orchestrating entry point: SQLAT evidence -> Key extraction -> full-space expansion -> insert-only draft candidate + Markdown projection + DB NOTIFY. Candidate inference is fully SQL.';

-- ---------------------------------------------------------------------------
-- logs.notify_sql_attention_draft_candidate_created (AFTER INSERT trigger)
-- Emits structured-JSON DB NOTIFY on the dedicated sql_attention_draft_candidate
-- channel. UI placement is never decided here; the payload is a signal only.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION logs.notify_sql_attention_draft_candidate_created()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_payload JSONB;
BEGIN
    v_payload := jsonb_build_object(
        'event_type', 'sql_attention_draft_candidate_created',
        'source', 'sql_attention',
        'candidate_id', NEW.candidate_id,
        'candidate_lane', NEW.candidate_lane,
        'markdown_projection_id', NEW.markdown_projection_id,
        'source_set_id', NEW.source_set_id);

    PERFORM pg_notify('sql_attention_draft_candidate', v_payload::text);
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_notify_sql_attention_draft_candidate_created
    ON logs.sql_attention_draft_markdown_projection;
CREATE TRIGGER trg_notify_sql_attention_draft_candidate_created
    AFTER INSERT ON logs.sql_attention_draft_markdown_projection
    FOR EACH ROW
    EXECUTE FUNCTION logs.notify_sql_attention_draft_candidate_created();
