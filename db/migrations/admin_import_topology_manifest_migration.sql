-- =============================================================================
-- admin_import_topology_manifest_migration.sql
-- Migration: admin_import FK retirement from public.manifest to hubs.topology_manifests.
--
-- APPLIES TO: existing DBs that have topology.admin_import_snapshot and
--   topology.admin_import_records with manifest_id FK to public.manifest.
--
-- PRECONDITION: hubs.topology_manifests must exist (created by topology_tables.sql).
--   A default topology_manifest must exist to remap existing snapshot rows.
--   This migration does NOT auto-create topology_manifests rows; ensure at least
--   one exists before running if snapshot rows are present.
--
-- WHAT THIS DOES:
--   1. Renames admin_import_snapshot.manifest_id -> topology_manifest_id
--   2. Drops the old FK constraint to manifest(manifest_id)
--   3. Adds new FK constraint to hubs.topology_manifests(topology_manifest_id)
--   4. Renames admin_import_records.manifest_id -> topology_manifest_id
--   5. Drops the old FK constraint from admin_import_records
--   6. Drops old index and creates new index on topology_manifest_id
--
-- SAFE TO RUN: idempotent where possible; uses IF EXISTS guards.
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
    END IF;
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
    END IF;
END;
$$;

-- Step 6: Drop old index and create new index on topology_manifest_id
DROP INDEX IF EXISTS topology.idx_admin_import_snapshot_manifest;

CREATE INDEX IF NOT EXISTS idx_admin_import_snapshot_topology_manifest
    ON topology.admin_import_snapshot (topology_manifest_id, created_at DESC);

COMMIT;
