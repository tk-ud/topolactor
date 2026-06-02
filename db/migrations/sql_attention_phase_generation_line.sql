-- =============================================================================
-- sql_attention_phase_generation_line.sql
-- Step 4 data-preserving migration for canonical SQL Attention relation exploration
-- and append-only SQLAT -> phaseAT -> Draft/adoption/rejection lineage.
-- No automatic topology/manifest/hub_relation mutation is performed.
-- =============================================================================

CREATE TABLE IF NOT EXISTS topology.physical_table_manifest_bindings (
    binding_id            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    physical_table_id     BIGINT      NOT NULL REFERENCES topology.physical_tables (physical_table_id) ON DELETE RESTRICT,
    topology_manifest_id  UUID        NOT NULL REFERENCES hubs.topology_manifests (topology_manifest_id) ON DELETE RESTRICT,
    active                BOOLEAN     NOT NULL DEFAULT true,
    binding_evidence_json JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (physical_table_id, topology_manifest_id)
);

COMMENT ON TABLE topology.physical_table_manifest_bindings IS
    'Explicit physical table to hub topology manifest association for SQL Attention resolver. No implicit/oldest-row fallback.';

CREATE INDEX IF NOT EXISTS idx_physical_table_manifest_bindings_physical
    ON topology.physical_table_manifest_bindings (physical_table_id) WHERE active = true;
CREATE INDEX IF NOT EXISTS idx_physical_table_manifest_bindings_manifest
    ON topology.physical_table_manifest_bindings (topology_manifest_id) WHERE active = true;

ALTER TABLE logs.attention ALTER COLUMN hub_current_id DROP NOT NULL;
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS evidence_kind TEXT;
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS generation_line_id UUID;
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS source_attention_id UUID;
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS source_current_id UUID;
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS source_topology_manifest_ids UUID[];
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS hit_hub_relation_ids UUID[];
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS expanded_hub_relation_ids UUID[];
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS expanded_topology_manifest_ids UUID[];
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS expanded_hub_ids UUID[];
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS phase_status TEXT;
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS promotion_status TEXT;
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS actor_or_source TEXT;
ALTER TABLE logs.attention ADD COLUMN IF NOT EXISTS command_id TEXT;

UPDATE logs.attention
SET evidence_kind = COALESCE(evidence_kind, 'sql_attention_hit'),
    generation_line_id = COALESCE(generation_line_id, attention_id),
    source_current_id = COALESCE(source_current_id, current_id),
    source_topology_manifest_ids = COALESCE(source_topology_manifest_ids, '{}'),
    hit_hub_relation_ids = COALESCE(hit_hub_relation_ids, CASE WHEN hub_relation_id IS NULL THEN '{}'::UUID[] ELSE ARRAY[hub_relation_id] END),
    expanded_hub_relation_ids = COALESCE(expanded_hub_relation_ids, '{}'),
    expanded_topology_manifest_ids = COALESCE(expanded_topology_manifest_ids, '{}'),
    expanded_hub_ids = COALESCE(expanded_hub_ids, '{}'),
    phase_status = COALESCE(phase_status, 'not_applicable'),
    promotion_status = COALESCE(promotion_status, 'not_requested');

ALTER TABLE logs.attention ALTER COLUMN evidence_kind SET DEFAULT 'sql_attention_hit';
ALTER TABLE logs.attention ALTER COLUMN evidence_kind SET NOT NULL;
ALTER TABLE logs.attention ALTER COLUMN generation_line_id SET DEFAULT gen_random_uuid();
ALTER TABLE logs.attention ALTER COLUMN generation_line_id SET NOT NULL;
ALTER TABLE logs.attention ALTER COLUMN source_current_id SET NOT NULL;
ALTER TABLE logs.attention ALTER COLUMN source_topology_manifest_ids SET DEFAULT '{}';
ALTER TABLE logs.attention ALTER COLUMN source_topology_manifest_ids SET NOT NULL;
ALTER TABLE logs.attention ALTER COLUMN hit_hub_relation_ids SET DEFAULT '{}';
ALTER TABLE logs.attention ALTER COLUMN hit_hub_relation_ids SET NOT NULL;
ALTER TABLE logs.attention ALTER COLUMN expanded_hub_relation_ids SET DEFAULT '{}';
ALTER TABLE logs.attention ALTER COLUMN expanded_hub_relation_ids SET NOT NULL;
ALTER TABLE logs.attention ALTER COLUMN expanded_topology_manifest_ids SET DEFAULT '{}';
ALTER TABLE logs.attention ALTER COLUMN expanded_topology_manifest_ids SET NOT NULL;
ALTER TABLE logs.attention ALTER COLUMN expanded_hub_ids SET DEFAULT '{}';
ALTER TABLE logs.attention ALTER COLUMN expanded_hub_ids SET NOT NULL;
ALTER TABLE logs.attention ALTER COLUMN phase_status SET DEFAULT 'not_applicable';
ALTER TABLE logs.attention ALTER COLUMN phase_status SET NOT NULL;
ALTER TABLE logs.attention ALTER COLUMN promotion_status SET DEFAULT 'not_requested';
ALTER TABLE logs.attention ALTER COLUMN promotion_status SET NOT NULL;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_logs_attention_source_attention') THEN
    ALTER TABLE logs.attention ADD CONSTRAINT fk_logs_attention_source_attention FOREIGN KEY (source_attention_id) REFERENCES logs.attention(attention_id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_logs_attention_source_current') THEN
    ALTER TABLE logs.attention ADD CONSTRAINT fk_logs_attention_source_current FOREIGN KEY (source_current_id) REFERENCES logs.current(current_id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_logs_attention_evidence_kind') THEN
    ALTER TABLE logs.attention ADD CONSTRAINT ck_logs_attention_evidence_kind CHECK (evidence_kind IN ('sql_attention_hit', 'phaseAT', 'draft_projection', 'adoption_result', 'rejection_result'));
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_logs_attention_phase_status') THEN
    ALTER TABLE logs.attention ADD CONSTRAINT ck_logs_attention_phase_status CHECK (phase_status IN ('not_applicable', 'evidence', 'draft_projection', 'adoption_result', 'rejection_result'));
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_logs_attention_promotion_status') THEN
    ALTER TABLE logs.attention ADD CONSTRAINT ck_logs_attention_promotion_status CHECK (promotion_status IN ('not_requested', 'draft', 'adopted', 'rejected'));
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_logs_attention_generation_line ON logs.attention (generation_line_id, created_at);
CREATE INDEX IF NOT EXISTS idx_logs_attention_source_attention_id ON logs.attention (source_attention_id);
CREATE INDEX IF NOT EXISTS idx_logs_attention_evidence_kind ON logs.attention (evidence_kind, created_at);

-- Keep the existing-database migration aligned with the fresh bootstrap resolver.
CREATE OR REPLACE FUNCTION logs.resolve_related_topology_manifests(
    p_current_id          UUID,
    p_physical_table_id   TEXT,
    p_physical_table_name TEXT
)
RETURNS TABLE (
    topology_manifest_id  UUID,
    resolver_evidence_json JSONB
)
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_identity_count INTEGER;
BEGIN
    SELECT COUNT(*)
    INTO v_identity_count
    FROM topology.physical_tables pt
    WHERE pt.active = true
      AND (
          pt.physical_table_id::TEXT = p_physical_table_id
          OR pt.table_ref = p_physical_table_name
      );

    IF v_identity_count > 1 THEN
        RAISE EXCEPTION 'AMBIGUOUS_PHYSICAL_TABLE_IDENTITY current_id=% physical_table_id=% physical_table_name=%',
            p_current_id, p_physical_table_id, p_physical_table_name;
    END IF;

    RETURN QUERY
    SELECT
        b.topology_manifest_id,
        jsonb_build_object(
            'resolver_kind', 'explicit_physical_table_manifest_binding',
            'source_current_id', p_current_id,
            'physical_table_id', pt.physical_table_id,
            'physical_table_name', pt.table_ref,
            'binding_id', b.binding_id,
            'binding_evidence', b.binding_evidence_json
        )
    FROM topology.physical_tables pt
    JOIN topology.physical_table_manifest_bindings b
      ON b.physical_table_id = pt.physical_table_id
     AND b.active = true
    JOIN hubs.topology_manifests tm
      ON tm.topology_manifest_id = b.topology_manifest_id
     AND tm.status = 'active'
    WHERE pt.active = true
      AND (
          pt.physical_table_id::TEXT = p_physical_table_id
          OR pt.table_ref = p_physical_table_name
      )
    ORDER BY b.topology_manifest_id;
END;
$function$;

COMMENT ON FUNCTION logs.resolve_related_topology_manifests(UUID, TEXT, TEXT) IS
    'Resolve explicit active physical table -> topology manifest bindings for SQL Attention. Returns no rows when unresolved and never falls back to an implicit or oldest-row join.';
