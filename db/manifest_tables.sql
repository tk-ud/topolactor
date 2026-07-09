-- =============================================================================
-- manifest_tables.sql
-- Manifest persistence table.
--
-- MIGRATION STATUS: compatibility-only surface.
--   Canonical wiring responsibility has been split:
--     - topology.wiring_physical_to_package: physical table → package wiring (new canonical path)
--     - hubs.topology_manifests: hub manifest grouping (new canonical path)
--   public.manifest is retained for the admin edit flow only. It is NOT the new
--   canonical wiring authority.
--   admin_import_snapshot FK has been migrated to hubs.topology_manifests
--   (see db/legacy_utils/admin_import_topology_manifest_migration.sql).
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
--   { "type": "ui_projection", "packageIds": ["uuid", ...], "layoutId": "uuid",
--       "wiringId": "uuid", "tensorId": "uuid" }
--       packageIds → topology.components_package_design.package_id
--           (docs/design/db-schema.yaml packages/components_package_design:
--           role component_design_bundle_referenced_by_manifest, with
--           manifest_reference: manifest.topology[ui_projection].packageIds --
--           this is the SAME package authority the pre-existing
--           projection_constructor_mapping / sse_projection entries below
--           reference. NOT topology.ui_component_package -- that is a
--           DIFFERENT identity, role component_group_bundle, required only by
--           topology.ui_topology_tensor.package_id's own FK constraint; it is
--           never a manifest.packageIds target.)
--       layoutId   → topology.components_layout_design.layout_id
--       wiringId   → topology.ui_wiring_registry.wiring_id
--       tensorId   → topology.ui_topology_tensor.tensor_id
--           (the referenced tensor row's OWN internal package_id column
--           references topology.ui_component_package, a separate id from
--           this entry's packageIds -- see the component_group_bundle note
--           above; the two are related but not the same value.)
--       wiringId/tensorId are optional refs (only present when the projection
--       has wiring/runtimeInteractions content); packageIds/layoutId remain
--       the minimum ui_projection shape. Actual UI-entity payload (form/field/
--       action/layout structure) lives in the referenced package/layout/
--       design/wiring/tensor tables, never inline in this jsonb[] entry -- see
--       docs/design/react-schema-topology-seed-translator-ssot.yaml
--       storage_adoption_contract.adoption_candidate_separation_contract.
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
    'Compatibility-only wiring table. Retained for admin edit flow only. '
    'admin_import_snapshot FK has been migrated to hubs.topology_manifests. '
    'Canonical wiring path: topology.wiring_physical_to_package (physical→package) '
    'and hubs.topology_manifests (hub manifest grouping). '
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
--
-- MIGRATION COMPLETED (admin_import FK retirement):
--   admin_import_snapshot.topology_manifest_id FK references hubs.topology_manifests (canonical).
--   admin_import_records.topology_manifest_id is a soft reference (no FK constraint) via snapshot linkage.
--   manifest(manifest_id) FK has been removed from both tables.
--   Migration DDL: db/legacy_utils/admin_import_topology_manifest_migration.sql
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS topology.admin_import_snapshot (
    snapshot_id               UUID PRIMARY KEY,
    source_type               TEXT        NOT NULL CHECK (source_type IN ('csv', 'json')),
    file_name                 TEXT        NOT NULL,
    topology_manifest_id      UUID        NOT NULL REFERENCES hubs.topology_manifests(topology_manifest_id),
    raw_header_jsonb          JSONB       NOT NULL DEFAULT '[]'::jsonb,
    raw_rows_jsonb            JSONB       NOT NULL DEFAULT '[]'::jsonb,
    validation_summary_jsonb  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS topology.admin_import_records (
    id                        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    topology_manifest_id      UUID        NOT NULL,
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

CREATE INDEX IF NOT EXISTS idx_admin_import_snapshot_topology_manifest
    ON topology.admin_import_snapshot (topology_manifest_id, created_at DESC);
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

