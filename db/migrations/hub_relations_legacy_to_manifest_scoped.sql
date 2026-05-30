-- =============================================================================
-- hub_relations_legacy_to_manifest_scoped.sql
-- Data-preserving migration: legacy global-graph hubs.hub_relations
--   (hub_id / target_hub_id / relation_registry_id)
-- → manifest-scoped canonical shape
--   (topology_manifest_id / related_hub_id / sequence_position / relation_config / status)
--
-- Policy:
--   - Non-destructive: no table-level CASCADE drops.
--   - Idempotent: no-op when canonical shape is already present.
--   - For each legacy source hub_id: resolve exactly one active topology_manifest,
--     or create one when none exists.
--   - target_hub_id → related_hub_id (NULL target_hub_id fails explicitly).
--   - sequence_position is preserved from legacy rows.
--   - relation_registry_id is stored under relation_config.legacy (not a canonical column).
--   - Ambiguous cases (multiple active manifests per hub_id, duplicate sequence slots,
--     NULL target_hub_id) raise explicit exceptions — no silent fallback.
--
-- Run manually on existing DBs that still carry legacy hub_relations columns:
--   psql -d <database> -f db/migrations/hub_relations_legacy_to_manifest_scoped.sql
--
-- SSOT: docs/design/db-schema.yaml compatibility_history.hub_relations_legacy_global_graph
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Legacy / canonical shape detection (inspection surface)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION hubs.hub_relations_has_legacy_shape()
RETURNS boolean
LANGUAGE sql
STABLE
AS $$
    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'hubs'
          AND table_name = 'hub_relations'
          AND column_name = 'hub_id'
    );
$$;

COMMENT ON FUNCTION hubs.hub_relations_has_legacy_shape() IS
    'Returns true when hubs.hub_relations still carries legacy hub_id source-authority column.';

CREATE OR REPLACE FUNCTION hubs.hub_relations_has_canonical_shape()
RETURNS boolean
LANGUAGE sql
STABLE
AS $$
    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'hubs'
          AND table_name = 'hub_relations'
          AND column_name = 'topology_manifest_id'
    )
    AND NOT hubs.hub_relations_has_legacy_shape();
$$;

COMMENT ON FUNCTION hubs.hub_relations_has_canonical_shape() IS
    'Returns true when hubs.hub_relations matches manifest-scoped canonical columns only.';

CREATE OR REPLACE FUNCTION hubs.hub_relations_legacy_shape_row_count()
RETURNS bigint
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    IF NOT hubs.hub_relations_has_legacy_shape() THEN
        RETURN 0;
    END IF;

    RETURN (SELECT COUNT(*)::bigint FROM hubs.hub_relations);
END;
$$;

COMMENT ON FUNCTION hubs.hub_relations_legacy_shape_row_count() IS
    'Row count when legacy shape is detected; 0 when already canonical.';

-- ---------------------------------------------------------------------------
-- Post-migration validation (inspection surface)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION hubs.hub_relations_legacy_columns_absent()
RETURNS boolean
LANGUAGE sql
STABLE
AS $$
    SELECT NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'hubs'
          AND table_name = 'hub_relations'
          AND column_name IN ('hub_id', 'target_hub_id', 'relation_registry_id')
    );
$$;

COMMENT ON FUNCTION hubs.hub_relations_legacy_columns_absent() IS
    'Returns true when legacy hub_id / target_hub_id / relation_registry_id columns are absent.';

-- ---------------------------------------------------------------------------
-- Data-preserving migration
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    v_ambiguous_hub_id UUID;
    v_null_target_id UUID;
    v_dup_hub_id UUID;
    v_dup_sequence INTEGER;
BEGIN
    IF NOT hubs.hub_relations_has_legacy_shape() THEN
        RAISE NOTICE 'hub_relations migration skipped: canonical shape already present.';
        RETURN;
    END IF;

    RAISE NOTICE 'hub_relations migration starting: legacy shape detected (% row(s)).',
        hubs.hub_relations_legacy_shape_row_count();

    -- Explicit failure: NULL target_hub_id cannot map to required related_hub_id.
    SELECT hr.hub_relation_id
    INTO v_null_target_id
    FROM hubs.hub_relations hr
    WHERE hr.target_hub_id IS NULL
    LIMIT 1;

    IF v_null_target_id IS NOT NULL THEN
        RAISE EXCEPTION
            'UNMIGRATABLE_NULL_TARGET_HUB: hub_relation_id=% requires manual resolution before migration',
            v_null_target_id;
    END IF;

    -- Explicit failure: duplicate (hub_id, sequence_position) in legacy data.
    SELECT hr.hub_id, hr.sequence_position
    INTO v_dup_hub_id, v_dup_sequence
    FROM hubs.hub_relations hr
    GROUP BY hr.hub_id, hr.sequence_position
    HAVING COUNT(*) > 1
    LIMIT 1;

    IF v_dup_hub_id IS NOT NULL THEN
        RAISE EXCEPTION
            'AMBIGUOUS_LEGACY_SEQUENCE: duplicate (hub_id=%, sequence_position=%) in legacy hub_relations',
            v_dup_hub_id, v_dup_sequence;
    END IF;

    -- Explicit failure: multiple active topology_manifests for one legacy source hub_id.
    SELECT hr.hub_id
    INTO v_ambiguous_hub_id
    FROM (
        SELECT DISTINCT hub_id
        FROM hubs.hub_relations
    ) hr
    JOIN hubs.topology_manifests tm
      ON tm.hub_id = hr.hub_id
     AND tm.status = 'active'
    GROUP BY hr.hub_id
    HAVING COUNT(*) > 1
    LIMIT 1;

    IF v_ambiguous_hub_id IS NOT NULL THEN
        RAISE EXCEPTION
            'AMBIGUOUS_TOPOLOGY_MANIFEST: hub_id=% has multiple active hubs.topology_manifests rows; resolve manually',
            v_ambiguous_hub_id;
    END IF;

    -- Create one active manifest per source hub_id when none exists.
    INSERT INTO hubs.topology_manifests (hub_id, manifest_key, status, topology_jsonb)
    SELECT DISTINCT
        hr.hub_id,
        'legacy_hub_relations_migration',
        'active',
        jsonb_build_object(
            'migration', 'hub_relations_legacy_to_manifest_scoped',
            'note', 'Auto-created during legacy hub_relations migration'
        )
    FROM hubs.hub_relations hr
    WHERE NOT EXISTS (
        SELECT 1
        FROM hubs.topology_manifests tm
        WHERE tm.hub_id = hr.hub_id
          AND tm.status = 'active'
    );

    -- Stage canonical columns on the legacy table.
    ALTER TABLE hubs.hub_relations
        ADD COLUMN IF NOT EXISTS topology_manifest_id UUID;

    ALTER TABLE hubs.hub_relations
        ADD COLUMN IF NOT EXISTS related_hub_id UUID;

    ALTER TABLE hubs.hub_relations
        ADD COLUMN IF NOT EXISTS relation_config JSONB NOT NULL DEFAULT '{}'::jsonb;

    -- Backfill canonical columns from legacy authority columns.
    WITH manifest_for_hub AS (
        SELECT DISTINCT ON (hr.hub_id)
            hr.hub_id,
            tm.topology_manifest_id
        FROM hubs.hub_relations hr
        JOIN hubs.topology_manifests tm
          ON tm.hub_id = hr.hub_id
         AND tm.status = 'active'
        ORDER BY hr.hub_id, tm.created_at ASC, tm.topology_manifest_id ASC
    )
    UPDATE hubs.hub_relations hr
    SET
        topology_manifest_id = mfh.topology_manifest_id,
        related_hub_id = hr.target_hub_id,
        relation_config = CASE
            WHEN hr.relation_registry_id IS NOT NULL THEN
                jsonb_build_object(
                    'legacy', jsonb_build_object(
                        'relation_registry_id', hr.relation_registry_id,
                        'source_hub_id', hr.hub_id,
                        'migration', 'hub_relations_legacy_to_manifest_scoped'
                    )
                )
            ELSE '{}'::jsonb
        END
    FROM manifest_for_hub mfh
    WHERE hr.hub_id = mfh.hub_id;

    IF EXISTS (
        SELECT 1
        FROM hubs.hub_relations
        WHERE topology_manifest_id IS NULL
           OR related_hub_id IS NULL
    ) THEN
        RAISE EXCEPTION
            'MIGRATION_INCOMPLETE: topology_manifest_id or related_hub_id unresolved after backfill';
    END IF;

    IF EXISTS (
        SELECT topology_manifest_id, sequence_position
        FROM hubs.hub_relations
        GROUP BY topology_manifest_id, sequence_position
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'DUPLICATE_SEQUENCE_POSITION_AFTER_MIGRATION: canonical (topology_manifest_id, sequence_position) collision';
    END IF;

    -- Drop legacy indexes before column removal.
    DROP INDEX IF EXISTS hubs.idx_hub_relations_hub_id;
    DROP INDEX IF EXISTS hubs.idx_hub_relations_target_hub_id;
    DROP INDEX IF EXISTS hubs.idx_hub_relations_relation_registry_id;
    DROP INDEX IF EXISTS hubs.idx_hub_relations_sequence_position;

    ALTER TABLE hubs.hub_relations DROP COLUMN IF EXISTS hub_id;
    ALTER TABLE hubs.hub_relations DROP COLUMN IF EXISTS target_hub_id;
    ALTER TABLE hubs.hub_relations DROP COLUMN IF EXISTS relation_registry_id;

    ALTER TABLE hubs.hub_relations
        ALTER COLUMN topology_manifest_id SET NOT NULL;

    ALTER TABLE hubs.hub_relations
        ALTER COLUMN related_hub_id SET NOT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class t ON c.conrelid = t.oid
        JOIN pg_namespace n ON t.relnamespace = n.oid
        WHERE n.nspname = 'hubs'
          AND t.relname = 'hub_relations'
          AND c.contype = 'f'
          AND pg_get_constraintdef(c.oid) LIKE '%topology_manifest_id%'
    ) THEN
        ALTER TABLE hubs.hub_relations
            ADD CONSTRAINT hub_relations_topology_manifest_id_fkey
            FOREIGN KEY (topology_manifest_id)
            REFERENCES hubs.topology_manifests (topology_manifest_id)
            ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class t ON c.conrelid = t.oid
        JOIN pg_namespace n ON t.relnamespace = n.oid
        WHERE n.nspname = 'hubs'
          AND t.relname = 'hub_relations'
          AND c.contype = 'f'
          AND pg_get_constraintdef(c.oid) LIKE '%related_hub_id%'
    ) THEN
        ALTER TABLE hubs.hub_relations
            ADD CONSTRAINT hub_relations_related_hub_id_fkey
            FOREIGN KEY (related_hub_id)
            REFERENCES hubs.hub (hub_id)
            ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class t ON c.conrelid = t.oid
        JOIN pg_namespace n ON t.relnamespace = n.oid
        WHERE n.nspname = 'hubs'
          AND t.relname = 'hub_relations'
          AND c.contype = 'u'
          AND pg_get_constraintdef(c.oid) LIKE '%topology_manifest_id%'
          AND pg_get_constraintdef(c.oid) LIKE '%sequence_position%'
    ) THEN
        ALTER TABLE hubs.hub_relations
            ADD CONSTRAINT hub_relations_topology_manifest_id_sequence_position_key
            UNIQUE (topology_manifest_id, sequence_position);
    END IF;

    CREATE INDEX IF NOT EXISTS idx_hub_relations_topology_manifest_id
        ON hubs.hub_relations (topology_manifest_id);

    CREATE INDEX IF NOT EXISTS idx_hub_relations_related_hub_id
        ON hubs.hub_relations (related_hub_id);

    CREATE INDEX IF NOT EXISTS idx_hub_relations_sequence_position
        ON hubs.hub_relations (topology_manifest_id, sequence_position);

    IF NOT hubs.hub_relations_legacy_columns_absent() THEN
        RAISE EXCEPTION
            'MIGRATION_VALIDATION_FAILED: legacy columns hub_id/target_hub_id/relation_registry_id still present';
    END IF;

    RAISE NOTICE 'hub_relations migration complete: canonical manifest-scoped shape applied.';
END;
$$;
