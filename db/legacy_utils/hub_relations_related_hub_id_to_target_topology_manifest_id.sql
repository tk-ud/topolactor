-- =============================================================================
-- hub_relations_related_hub_id_to_target_topology_manifest_id.sql
-- Data-preserving utility: brings an EXISTING database's hubs.hub_relations up to the canonical
-- direct target reference (target_topology_manifest_id -> hubs.topology_manifests), retiring
-- related_hub_id from target-resolution authority into a legacy compatibility mirror.
--
-- Policy (docs/design/db-schema.yaml hub_relations.target_reference_canonical_contract
-- .legacy_ambiguous_row_migration_disposition):
--   (1) A row whose related_hub_id resolves to EXACTLY ONE active hubs.topology_manifests row
--       backfills target_topology_manifest_id to that manifest. status is untouched.
--   (2) A row whose related_hub_id resolves to ZERO or MULTIPLE active manifests is NOT backfilled:
--       target_topology_manifest_id stays NULL, status is untouched (never auto-deprecated), and
--       the row is classified and reported for explicit admin remediation
--       (hub_navigation:update re-point, or hub_navigation:deprecate).
--   (3) The status='active' NOT NULL CHECK constraint
--       (hub_relations_active_target_topology_manifest_required) is added only once every active
--       row carries a target; while any active row is still NULL it is reported as deferred.
--   (4) Every source topology_manifest_id whose active rows are ALL still unresolved is reported
--       (already a navigation orphan under canonical_forward_resolvability_definition).
--   - related_hub_id is never rewritten and never dropped; no target is guessed / oldest / first.
--   - Idempotent: re-running only re-classifies rows still NULL and re-checks the constraint.
--
-- Run manually on existing DBs created before target_topology_manifest_id existed:
--   psql -d <database> -v ON_ERROR_STOP=1 -f db/legacy_utils/hub_relations_related_hub_id_to_target_topology_manifest_id.sql
--
-- NOT part of db/init.sql's fresh-bootstrap chain: a fresh docker compose -v bootstrap creates the
-- column/FK/CHECK directly in db/topology_tables.sql and db/seed_empty.sql authors every row with
-- target_topology_manifest_id set (docs/design/db-schema.yaml bootstrap_policy).
--
-- SSOT: docs/design/db-schema.yaml compatibility_history
--       .hub_relations_related_hub_id_to_target_topology_manifest_id_transition
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Shape: canonical column, FK and index (no-op when already present)
-- ---------------------------------------------------------------------------
ALTER TABLE hubs.hub_relations
    ADD COLUMN IF NOT EXISTS target_topology_manifest_id UUID;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
        WHERE c.conrelid = 'hubs.hub_relations'::regclass
          AND c.contype = 'f'
          AND a.attname = 'target_topology_manifest_id'
    ) THEN
        ALTER TABLE hubs.hub_relations
            ADD CONSTRAINT hub_relations_target_topology_manifest_id_fkey
            FOREIGN KEY (target_topology_manifest_id)
            REFERENCES hubs.topology_manifests (topology_manifest_id) ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_hub_relations_target_topology_manifest_id
    ON hubs.hub_relations (target_topology_manifest_id);

-- ---------------------------------------------------------------------------
-- Classification (inspection surface): every row still lacking a target
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION hubs.hub_relations_target_manifest_classification()
RETURNS TABLE (
    hub_relation_id                       UUID,
    topology_manifest_id                  UUID,
    related_hub_id                        UUID,
    status                                TEXT,
    active_target_manifest_count          BIGINT,
    candidate_target_topology_manifest_id UUID,
    classification                        TEXT
)
LANGUAGE sql
STABLE
AS $$
    SELECT hr.hub_relation_id,
           hr.topology_manifest_id,
           hr.related_hub_id,
           hr.status,
           COALESCE(t.active_count, 0),
           CASE WHEN t.active_count = 1 THEN t.only_manifest_id END,
           CASE
               WHEN COALESCE(t.active_count, 0) = 0 THEN 'zero_active_target_manifest'
               WHEN t.active_count = 1 THEN 'exactly_one_active_target_manifest'
               ELSE 'multiple_active_target_manifests'
           END
    FROM hubs.hub_relations hr
    LEFT JOIN LATERAL (
        SELECT COUNT(*) AS active_count,
               -- Only read when active_count = 1, where the single row is the only candidate.
               MIN(tm.topology_manifest_id::text)::uuid AS only_manifest_id
        FROM hubs.topology_manifests tm
        WHERE tm.hub_id = hr.related_hub_id
          AND tm.status = 'active'
    ) t ON true
    WHERE hr.target_topology_manifest_id IS NULL
    ORDER BY hr.topology_manifest_id, hr.sequence_position;
$$;

COMMENT ON FUNCTION hubs.hub_relations_target_manifest_classification() IS
    'Classifies every hubs.hub_relations row whose target_topology_manifest_id is NULL by how many '
    'active hubs.topology_manifests rows its related_hub_id owns: exactly_one (backfillable), zero or '
    'multiple (left NULL for explicit admin remediation, never guessed).';

-- ---------------------------------------------------------------------------
-- Backfill: exactly-one rows only; status and related_hub_id untouched
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION hubs.hub_relations_backfill_target_topology_manifest_id()
RETURNS BIGINT
LANGUAGE plpgsql
AS $$
DECLARE
    updated_count BIGINT;
BEGIN
    UPDATE hubs.hub_relations hr
    SET target_topology_manifest_id = c.candidate_target_topology_manifest_id,
        updated_at = now()
    FROM hubs.hub_relations_target_manifest_classification() c
    WHERE hr.hub_relation_id = c.hub_relation_id
      AND c.classification = 'exactly_one_active_target_manifest';
    GET DIAGNOSTICS updated_count = ROW_COUNT;
    RETURN updated_count;
END;
$$;

COMMENT ON FUNCTION hubs.hub_relations_backfill_target_topology_manifest_id() IS
    'Backfills target_topology_manifest_id only where related_hub_id resolves to exactly one active '
    'topology_manifest. Never changes status or related_hub_id; zero/multiple rows stay NULL.';

-- ---------------------------------------------------------------------------
-- Report: sources whose active rows are ALL still unresolved (navigation orphans)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION hubs.hub_relations_unresolved_active_sources()
RETURNS TABLE (topology_manifest_id UUID, unresolved_active_row_count BIGINT)
LANGUAGE sql
STABLE
AS $$
    SELECT hr.topology_manifest_id, COUNT(*)
    FROM hubs.hub_relations hr
    WHERE hr.status = 'active'
    GROUP BY hr.topology_manifest_id
    HAVING COUNT(*) FILTER (WHERE hr.target_topology_manifest_id IS NOT NULL) = 0
    ORDER BY hr.topology_manifest_id;
$$;

COMMENT ON FUNCTION hubs.hub_relations_unresolved_active_sources() IS
    'Source topology_manifest_ids whose every active hub_relations row still has a NULL '
    'target_topology_manifest_id -- already navigation orphans pending explicit admin remediation.';

-- ---------------------------------------------------------------------------
-- Constraint: enable status='active' target requirement only when nothing is unresolved
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION hubs.hub_relations_try_enable_active_target_required()
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'hubs.hub_relations'::regclass
          AND conname = 'hub_relations_active_target_topology_manifest_required'
    ) THEN
        RETURN true;
    END IF;

    IF EXISTS (
        SELECT 1 FROM hubs.hub_relations
        WHERE status = 'active' AND target_topology_manifest_id IS NULL
    ) THEN
        RETURN false;
    END IF;

    ALTER TABLE hubs.hub_relations
        ADD CONSTRAINT hub_relations_active_target_topology_manifest_required
        CHECK (status <> 'active' OR target_topology_manifest_id IS NOT NULL);
    RETURN true;
END;
$$;

COMMENT ON FUNCTION hubs.hub_relations_try_enable_active_target_required() IS
    'Adds hub_relations_active_target_topology_manifest_required only when no status=''active'' row '
    'has a NULL target_topology_manifest_id. Returns whether the constraint is in force afterwards.';

-- ---------------------------------------------------------------------------
-- Run: backfill, report, conditionally enable the constraint
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    backfilled BIGINT;
    r RECORD;
    constraint_enabled BOOLEAN;
BEGIN
    backfilled := hubs.hub_relations_backfill_target_topology_manifest_id();
    RAISE NOTICE 'hub_relations target backfill: % row(s) resolved via exactly one active target manifest', backfilled;

    FOR r IN SELECT * FROM hubs.hub_relations_target_manifest_classification() LOOP
        RAISE NOTICE 'hub_relations target UNRESOLVED (admin remediation required): hub_relation_id=% source_topology_manifest_id=% related_hub_id=% status=% classification=% active_target_manifest_count=%',
            r.hub_relation_id, r.topology_manifest_id, r.related_hub_id, r.status,
            r.classification, r.active_target_manifest_count;
    END LOOP;

    FOR r IN SELECT * FROM hubs.hub_relations_unresolved_active_sources() LOOP
        RAISE NOTICE 'hub_relations navigation orphan source: topology_manifest_id=% has % active row(s), none with a resolved target',
            r.topology_manifest_id, r.unresolved_active_row_count;
    END LOOP;

    constraint_enabled := hubs.hub_relations_try_enable_active_target_required();
    IF constraint_enabled THEN
        RAISE NOTICE 'hub_relations_active_target_topology_manifest_required: in force';
    ELSE
        RAISE NOTICE 'hub_relations_active_target_topology_manifest_required: DEFERRED -- active rows with NULL target_topology_manifest_id remain; re-run after admin remediation';
    END IF;
END $$;
