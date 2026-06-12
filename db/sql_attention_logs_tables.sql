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
