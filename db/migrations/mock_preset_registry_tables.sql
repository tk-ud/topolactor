-- =============================================================================
-- mock_preset_registry_tables.sql
-- Migration: topology.mock_preset_* tables for the Mock Preset Intake Compiler.
--
-- SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml
-- SCHEMA AUTHORITY: docs/design/db-schema.yaml (topology namespace)
--
-- Tables added:
--   topology.mock_preset_registry         — preset header and source snapshot
--   topology.mock_preset_object_mapping   — visual object → component/html/ignored mapping
--   topology.mock_preset_wiring_candidate — pending/confirmed wiring candidates
--   topology.mock_preset_compile_snapshot — deterministic compile output
--
-- IDEMPOTENT: all CREATE TABLE statements use IF NOT EXISTS.
--   Safe to re-run on an already-migrated database.
--
-- PRECONDITION: topology schema must exist (created by db/schema.sql).
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- topology.mock_preset_registry
-- Stores the reusable UI preset header and non-authoritative source snapshot.
-- source_kind: svg_xml | generic_xml | figma_json | ui_builder_canvas
-- status: draft | active | archived
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.mock_preset_registry (
    preset_id             UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    preset_key            TEXT        NOT NULL,
    preset_label          TEXT        NOT NULL,
    source_kind           TEXT        NOT NULL,
    source_hash           TEXT        NOT NULL DEFAULT '',
    source_snapshot_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    visual_tree_json      JSONB       NOT NULL DEFAULT '{}'::jsonb,
    status                TEXT        NOT NULL DEFAULT 'draft',
    created_by            UUID,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_mock_preset_registry_key UNIQUE (preset_key),
    CONSTRAINT ck_mock_preset_registry_status
        CHECK (status IN ('draft', 'active', 'archived')),
    CONSTRAINT ck_mock_preset_registry_source_kind
        CHECK (source_kind IN ('svg_xml', 'generic_xml', 'figma_json', 'ui_builder_canvas'))
);

-- ---------------------------------------------------------------------------
-- topology.mock_preset_object_mapping
-- Maps each visual source object to a Topolactor component/layout concept.
-- node_kind: catalog_component | structural_html | ignored
-- mapping_status: unassigned | mapped | ignored | unresolved
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.mock_preset_object_mapping (
    mapping_id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    preset_id               UUID        NOT NULL REFERENCES topology.mock_preset_registry (preset_id) ON DELETE CASCADE,
    source_object_id        TEXT        NOT NULL,
    node_id                 TEXT        NOT NULL,
    node_kind               TEXT        NOT NULL DEFAULT 'unassigned',
    component_key           TEXT,
    component_kind          TEXT,
    html_tag                TEXT,
    parent_source_object_id TEXT,
    slot_key                TEXT,
    order_index             INTEGER     NOT NULL DEFAULT 0,
    bbox_json               JSONB       NOT NULL DEFAULT '{}'::jsonb,
    text_json               JSONB       NOT NULL DEFAULT '{}'::jsonb,
    style_candidate_json    JSONB       NOT NULL DEFAULT '{}'::jsonb,
    mapping_status          TEXT        NOT NULL DEFAULT 'unassigned',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_mock_preset_object_mapping_source
        UNIQUE (preset_id, source_object_id),
    CONSTRAINT ck_mock_preset_object_mapping_node_kind
        CHECK (node_kind IN ('catalog_component', 'structural_html', 'ignored', 'unassigned')),
    CONSTRAINT ck_mock_preset_object_mapping_status
        CHECK (mapping_status IN ('unassigned', 'mapped', 'ignored', 'unresolved'))
);

-- ---------------------------------------------------------------------------
-- topology.mock_preset_wiring_candidate
-- Stores pending or confirmed wiring/data-binding candidates for mapped objects.
-- status: pending | confirmed | rejected
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.mock_preset_wiring_candidate (
    wiring_candidate_id UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    preset_id           UUID        NOT NULL REFERENCES topology.mock_preset_registry (preset_id) ON DELETE CASCADE,
    source_object_id    TEXT        NOT NULL,
    node_id             TEXT        NOT NULL,
    capability_tag      TEXT        NOT NULL,
    wiring_kind         TEXT,
    target_surface      TEXT,
    target_ref          TEXT,
    binding_json        JSONB       NOT NULL DEFAULT '{}'::jsonb,
    status              TEXT        NOT NULL DEFAULT 'pending',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_mock_preset_wiring_candidate_source_tag
        UNIQUE (preset_id, source_object_id, capability_tag),
    CONSTRAINT ck_mock_preset_wiring_candidate_status
        CHECK (status IN ('pending', 'confirmed', 'rejected'))
);

-- ---------------------------------------------------------------------------
-- topology.mock_preset_compile_snapshot
-- Stores the deterministic compile output for fast UIBuilder loading.
-- Compiled from: source_snapshot + object_mappings + confirmed_wiring_candidates.
-- Does NOT represent canonical active topology.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.mock_preset_compile_snapshot (
    compile_snapshot_id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    preset_id                        UUID        NOT NULL REFERENCES topology.mock_preset_registry (preset_id) ON DELETE CASCADE,
    compiler_version                 TEXT        NOT NULL DEFAULT '0.1.0',
    layout_patch_json                JSONB       NOT NULL DEFAULT '{}'::jsonb,
    package_membership_candidate_json JSONB      NOT NULL DEFAULT '{}'::jsonb,
    wiring_candidate_json            JSONB       NOT NULL DEFAULT '[]'::jsonb,
    style_candidate_json             JSONB       NOT NULL DEFAULT '[]'::jsonb,
    unresolved_json                  JSONB       NOT NULL DEFAULT '[]'::jsonb,
    compiled_at                      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------------------------
-- Indexes
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_mock_preset_registry_status
    ON topology.mock_preset_registry (status);
CREATE INDEX IF NOT EXISTS idx_mock_preset_registry_source_kind
    ON topology.mock_preset_registry (source_kind);
CREATE INDEX IF NOT EXISTS idx_mock_preset_object_mapping_preset
    ON topology.mock_preset_object_mapping (preset_id, order_index);
CREATE INDEX IF NOT EXISTS idx_mock_preset_object_mapping_status
    ON topology.mock_preset_object_mapping (preset_id, mapping_status);
CREATE INDEX IF NOT EXISTS idx_mock_preset_wiring_candidate_preset
    ON topology.mock_preset_wiring_candidate (preset_id);
CREATE INDEX IF NOT EXISTS idx_mock_preset_compile_snapshot_preset
    ON topology.mock_preset_compile_snapshot (preset_id, compiled_at DESC);

COMMIT;
