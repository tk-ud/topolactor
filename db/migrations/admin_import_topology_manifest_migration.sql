-- =============================================================================
-- admin_import_topology_manifest_migration.sql
-- Migration: admin_import FK retirement from public.manifest to hubs.topology_manifests.
--
-- APPLIES TO: existing DBs that have topology.admin_import_snapshot and
--   topology.admin_import_records with manifest_id FK to public.manifest.
--   Safe to run on fresh DBs (all steps guarded by IF EXISTS / column check).
--
-- PRECONDITION: hubs.topology_manifests must exist (created by topology_tables.sql).
--
-- REMAP POLICY:
--   Existing admin_import_snapshot rows hold manifest_id values from public.manifest,
--   not hubs.topology_manifests topology_manifest_id values.  Before the FK to
--   hubs.topology_manifests can be added, any orphaned rows must be remapped.
--
--   Policy: remap orphaned rows to the OLDEST topology_manifest in hubs.topology_manifests
--   (by created_at ASC), as a safe default grouping anchor.  This is explicit behaviour;
--   if no topology_manifest exists and orphaned rows are present, the migration raises
--   an explicit error requiring the operator to insert a topology_manifest first.
--
--   admin_import_records: renamed manifest_id -> topology_manifest_id but kept as a
--   soft reference (no FK), linked via snapshot_id.  No remap is needed on records.
--
-- IDEMPOTENT: all steps use IF EXISTS / column-existence guards.
--   Re-running on an already-migrated DB is safe (no-op on idempotent steps).
-- =============================================================================

BEGIN;

-- Step 1: Rename manifest_id -> topology_manifest_id on admin_import_snapshot
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'topology'
          AND table_name = 'admin_import_snapshot'
          AND column_name = 'manifest_id'
    ) THEN
        ALTER TABLE topology.admin_import_snapshot
            RENAME COLUMN manifest_id TO topology_manifest_id;
        RAISE NOTICE 'Renamed admin_import_snapshot.manifest_id -> topology_manifest_id';
    END IF;
END;
$$;

-- Step 2: Drop old FK constraint on admin_import_snapshot (manifest reference)
DO $$
DECLARE
    constraint_name text;
BEGIN
    SELECT conname INTO constraint_name
    FROM pg_constraint
    WHERE conrelid = 'topology.admin_import_snapshot'::regclass
      AND contype = 'f'
      AND confrelid = 'manifest'::regclass
    LIMIT 1;

    IF constraint_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE topology.admin_import_snapshot DROP CONSTRAINT %I', constraint_name);
        RAISE NOTICE 'Dropped old FK %', constraint_name;
    END IF;
END;
$$;

-- Step 2b: Remap orphaned topology_manifest_id values to default topology_manifest.
--
-- After the rename in Step 1 the column values are still old manifest_id UUIDs from
-- public.manifest.  These UUIDs do not exist in hubs.topology_manifests.  Any such
-- row would violate the new FK constraint added in Step 3.
--
-- Remap policy: UPDATE orphaned rows to the oldest hubs.topology_manifests row.
-- Explicit failure when no topology_manifests row exists and orphaned rows are present.
DO $$
DECLARE
    orphaned_count        integer;
    default_manifest_id   uuid;
BEGIN
    -- Count snapshot rows whose current topology_manifest_id is not in hubs.topology_manifests.
    -- Skip this check if the column does not yet exist (idempotent run before Step 1 applied).
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'topology'
          AND table_name = 'admin_import_snapshot'
          AND column_name = 'topology_manifest_id'
    ) THEN
        RETURN;  -- column not present yet; nothing to remap
    END IF;

    SELECT COUNT(*) INTO orphaned_count
    FROM topology.admin_import_snapshot s
    WHERE NOT EXISTS (
        SELECT 1 FROM hubs.topology_manifests tm
        WHERE tm.topology_manifest_id = s.topology_manifest_id
    );

    IF orphaned_count = 0 THEN
        RAISE NOTICE 'No orphaned admin_import_snapshot rows; remap step skipped.';
        RETURN;
    END IF;

    -- Find the oldest topology_manifest as the remap anchor.
    SELECT topology_manifest_id INTO default_manifest_id
    FROM hubs.topology_manifests
    ORDER BY created_at ASC
    LIMIT 1;

    IF default_manifest_id IS NULL THEN
        RAISE EXCEPTION
            'admin_import_topology_manifest_migration: % admin_import_snapshot row(s) have '
            'orphaned manifest_id values that cannot be remapped because hubs.topology_manifests '
            'is empty.  Insert at least one topology_manifest row before running this migration.',
            orphaned_count;
    END IF;

    UPDATE topology.admin_import_snapshot
    SET topology_manifest_id = default_manifest_id
    WHERE NOT EXISTS (
        SELECT 1 FROM hubs.topology_manifests tm
        WHERE tm.topology_manifest_id = topology.admin_import_snapshot.topology_manifest_id
    );

    RAISE NOTICE 'Remapped % orphaned admin_import_snapshot row(s) to default topology_manifest %',
        orphaned_count, default_manifest_id;
END;
$$;

-- Step 3: Add FK constraint to hubs.topology_manifests on admin_import_snapshot
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'topology.admin_import_snapshot'::regclass
          AND contype = 'f'
          AND confrelid = 'hubs.topology_manifests'::regclass
    ) THEN
        ALTER TABLE topology.admin_import_snapshot
            ADD CONSTRAINT admin_import_snapshot_topology_manifest_id_fkey
            FOREIGN KEY (topology_manifest_id)
            REFERENCES hubs.topology_manifests(topology_manifest_id);
        RAISE NOTICE 'Added FK admin_import_snapshot.topology_manifest_id -> hubs.topology_manifests';
    END IF;
END;
$$;

-- Step 4: Rename manifest_id -> topology_manifest_id on admin_import_records
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'topology'
          AND table_name = 'admin_import_records'
          AND column_name = 'manifest_id'
    ) THEN
        ALTER TABLE topology.admin_import_records
            RENAME COLUMN manifest_id TO topology_manifest_id;
        RAISE NOTICE 'Renamed admin_import_records.manifest_id -> topology_manifest_id';
    END IF;
END;
$$;

-- Step 5: Drop old FK constraint on admin_import_records (manifest reference)
DO $$
DECLARE
    constraint_name text;
BEGIN
    SELECT conname INTO constraint_name
    FROM pg_constraint
    WHERE conrelid = 'topology.admin_import_records'::regclass
      AND contype = 'f'
      AND confrelid = 'manifest'::regclass
    LIMIT 1;

    IF constraint_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE topology.admin_import_records DROP CONSTRAINT %I', constraint_name);
        RAISE NOTICE 'Dropped old FK %', constraint_name;
    END IF;
END;
$$;
-- Note: admin_import_records.topology_manifest_id remains a soft reference (no FK added).
-- Linkage is maintained via snapshot_id FK to admin_import_snapshot which carries the FK.

-- Step 6: Drop old index and create new index on topology_manifest_id
DROP INDEX IF EXISTS topology.idx_admin_import_snapshot_manifest;

CREATE INDEX IF NOT EXISTS idx_admin_import_snapshot_topology_manifest
    ON topology.admin_import_snapshot (topology_manifest_id, created_at DESC);

COMMIT;
