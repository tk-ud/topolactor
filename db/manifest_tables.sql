-- =============================================================================
-- manifest_tables.sql
-- Manifest persistence table.
--
-- MIGRATION STATUS: compatibility-only surface.
--   Canonical wiring responsibility has been split:
--     - topology.wiring_physical_to_package: physical table → package wiring (new canonical path)
--     - hubs.topology_manifests: hub manifest grouping (new canonical path)
--   public.manifest is retained for compatibility with admin_import_snapshot FK
--   and existing admin edit flow. It is NOT the new canonical wiring authority.
--   Pending retirement once admin import is migrated to canonical tables.
--
-- ORIGINAL PURPOSE (compatibility context):
--   Manifest stores ID references and topology vectors only.
--   Actual data (package content, component definitions, etc.) lives in the
--   respective registry tables. Manifest is a wiring diagram, not a data store.
--
-- FLOW (SSOT: docs/design/runtime-orchestration-ssot.yaml manifest_contract):
--   manifest (draft) → admin edit → manifest (active)
--   manifest_dispatcher reads active manifest to resolve runtime_destination
--   frontend reads active manifest via dispatcher to resolve projection_definition
--
-- STATUS LIFECYCLE:
--   draft      — being edited by admin, not yet in use
--   active     — adopted, read by dispatcher and frontend projection
--   deprecated — no longer in use, retained for audit
--
-- topology jsonb[] ENTRY SHAPE (ID references and vectors only, no actual data):
--   { "type": "dispatcher_mapping", "role": "...", "target": "...", "layer": "...", "action": "..." }
--   { "type": "runtime_mapping", "triggerKind": "...", "target": "..." }
--   { "type": "ui_projection", "packageIds": ["uuid", ...], "layoutId": "uuid" }
--       packageIds → packages.package_id (UI Component Builder layer)
--   { "type": "projection_constructor_mapping", "constructorKey": "...", "packageIds": [...] }
--       packageIds → packages.package_id
--   { "type": "sse_projection", "eventKind": "...", "packageIds": [...] }
--       packageIds → packages.package_id
-- =============================================================================

CREATE TABLE IF NOT EXISTS manifest (
    manifest_id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    relation_registry_id  UUID        REFERENCES topology.relation_registry (relation_registry_id) ON DELETE RESTRICT,
    topology              JSONB[]     NOT NULL DEFAULT '{}',
    status                TEXT        NOT NULL DEFAULT 'draft',
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_manifest_status CHECK (status IN ('draft', 'active', 'deprecated'))
);

COMMENT ON TABLE manifest IS
    'Compatibility-only wiring table. Retained for admin_import_snapshot FK and '
    'existing admin edit flow. Canonical wiring path has been split: '
    'topology.wiring_physical_to_package (physical→package wiring) and '
    'hubs.topology_manifests (hub manifest grouping) are the new canonical surfaces. '
    'Pending retirement once admin import migrates to canonical tables. '
    'Stores dispatcher_mapping / runtime_mapping / ui_projection as topology vectors (ID refs only).';

COMMENT ON COLUMN manifest.relation_registry_id IS
    'The relation_registry entry this manifest applies to. '
    'NULL allowed for global/cross-relation manifests.';

COMMENT ON COLUMN manifest.topology IS
    'Array of JSONB wiring entries. Each entry carries a type field and ID '
    'references or vectors. No actual content data is stored here.';

COMMENT ON COLUMN manifest.status IS
    'Lifecycle status: '
    '  draft      — being edited by admin, not read by dispatcher or frontend. '
    '  active     — adopted, read by manifest_dispatcher and projection_constructor. '
    '  deprecated — retired, retained for audit only.';

CREATE INDEX IF NOT EXISTS idx_manifest_status_active
    ON manifest (status)
    WHERE status = 'active';

CREATE INDEX IF NOT EXISTS idx_manifest_relation_registry_id
    ON manifest (relation_registry_id);

CREATE INDEX IF NOT EXISTS idx_manifest_topology
    ON manifest USING GIN (topology);

-- ---------------------------------------------------------------------------
-- M6 self-hosted admin authoring persistence scaffold (integrated in schema.sql)
-- Manifest/schema/table/binding authority remains in existing manifest + schema_registry + structure_maps.
-- This section only stores intake snapshots, per-row records, and apply logs.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS topology.admin_import_snapshot (
    snapshot_id               UUID PRIMARY KEY,
    source_type               TEXT        NOT NULL CHECK (source_type IN ('csv', 'json')),
    file_name                 TEXT        NOT NULL,
    manifest_id               UUID        NOT NULL REFERENCES manifest(manifest_id),
    raw_header_jsonb          JSONB       NOT NULL DEFAULT '[]'::jsonb,
    raw_rows_jsonb            JSONB       NOT NULL DEFAULT '[]'::jsonb,
    validation_summary_jsonb  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS topology.admin_import_records (
    id                        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    manifest_id               UUID        NOT NULL REFERENCES manifest(manifest_id),
    snapshot_id               UUID        NOT NULL REFERENCES topology.admin_import_snapshot(snapshot_id),
    records                   JSONB       NOT NULL,
    status                    TEXT        NOT NULL CHECK (status IN ('valid', 'invalid')),
    validation_errors_jsonb   JSONB       NOT NULL DEFAULT '[]'::jsonb,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON COLUMN topology.admin_import_records.status IS
    'Manifest/schema conformity validation status for records JSONB; not business or hub lifecycle state.';

CREATE TABLE IF NOT EXISTS topology.admin_import_apply_log (
    apply_log_id              UUID PRIMARY KEY,
    snapshot_id               UUID        NOT NULL REFERENCES topology.admin_import_snapshot(snapshot_id),
    applied_record_count      INTEGER     NOT NULL DEFAULT 0,
    applied_diff_jsonb        JSONB       NOT NULL DEFAULT '{}'::jsonb,
    status                    TEXT        NOT NULL,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_admin_import_snapshot_manifest
    ON topology.admin_import_snapshot (manifest_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_admin_import_records_snapshot
    ON topology.admin_import_records (snapshot_id, status);

-- ---------------------------------------------------------------------------
-- topology.wiring_physical_to_package
-- Canonical wiring table mapping physical tables to topology packages.
-- Responsible for topology-side wiring (physical table → package resolution).
-- Distinct from manifest (hubs.topology_manifests groups hub manifests).
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.wiring_physical_to_package (
    wiring_id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    physical_table_id   BIGINT      NOT NULL REFERENCES topology.physical_tables (physical_table_id) ON DELETE RESTRICT,
    package_id          UUID        NOT NULL,
    wiring_def          JSONB       NOT NULL DEFAULT '{}'::jsonb,
    active              BOOLEAN     NOT NULL DEFAULT true,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE topology.wiring_physical_to_package IS
    'Canonical wiring table for physical table to package resolution. '
    'Maps topology.physical_tables entries to topology packages. '
    'Topology-side wiring authority distinct from hubs.topology_manifests.';

CREATE INDEX IF NOT EXISTS idx_wiring_physical_to_package_physical_table_id
    ON topology.wiring_physical_to_package (physical_table_id);
CREATE INDEX IF NOT EXISTS idx_wiring_physical_to_package_active
    ON topology.wiring_physical_to_package (active)
    WHERE active = true;

