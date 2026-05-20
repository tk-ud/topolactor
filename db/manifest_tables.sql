-- =============================================================================
-- manifest_tables.sql
-- Manifest persistence table.
--
-- PURPOSE:
--   Manifest is the single source of wiring definitions for dispatcher_mapping,
--   runtime_mapping, ui_projection_definition, projection_constructor_mapping,
--   and sse_projection_definition.
--
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
--   { "type": "dispatcher_mapping", "role": "...", "runtime": "..." }
--   { "type": "runtime_mapping", "triggerKind": "...", "target": "..." }
--   { "type": "ui_projection", "packageIds": ["uuid", ...], "layoutId": "uuid" }
--   { "type": "projection_constructor_mapping", "constructorKey": "...", "packageIds": [...] }
--   { "type": "sse_projection", "eventKind": "...", "packageIds": [...] }
-- =============================================================================

CREATE TABLE IF NOT EXISTS manifest (
    manifest_id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    relation_registry_id  UUID        REFERENCES relation_registry (relation_registry_id) ON DELETE RESTRICT,
    topology              JSONB[]     NOT NULL DEFAULT '{}',
    status                TEXT        NOT NULL DEFAULT 'draft',
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_manifest_status CHECK (status IN ('draft', 'active', 'deprecated'))
);

COMMENT ON TABLE manifest IS
    'Wiring definition table. Stores dispatcher_mapping, runtime_mapping, '
    'ui_projection_definition, and projection_constructor_mapping as topology '
    'vectors (ID references only, no actual data). '
    'Managed by admin: draft -> active -> deprecated lifecycle.';

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
