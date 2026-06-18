-- =============================================================================
-- ui_topology_tables.sql
-- UI topology tensor persistence tables.
--
-- CANONICAL SCHEMA PLACEMENT: all tables under topology.* schema.
-- Public-schema legacy names (ui_component_bucket, ui_topology_tensor, etc.)
-- have been migrated to topology.* canonical names. Migration DDL for existing
-- DBs: db/migrations/ui_topology_to_canonical_schema.sql
--
-- CANONICAL TABLE NAMES (per docs/design/db-schema.yaml canonical_schema_namespaces):
--   topology.components_bucket     (was: public.ui_component_bucket)
--   topology.components_style_design (was: public.design)
--   topology.components_layout_design (was: public.ui_layout_registry)
--   topology.components_package_design (was: public.packages)
--
-- SUPPORTING TABLES (moved to topology schema, retain ui_ prefix):
--   topology.ui_component_registry  (was: public.ui_component_registry)
--   topology.ui_component_package   (was: public.ui_component_package)
--   topology.ui_package_component_map (was: public.ui_package_component_map)
--   topology.ui_wiring_registry     (was: public.ui_wiring_registry)
--   topology.ui_topology_tensor     (was: public.ui_topology_tensor)
--   topology.ui_builder_components  (was: public.components)
--
-- PURPOSE:
--   Frontend components/packages are not topology entities while code-only.
--   They become topology tensor entities only after ID issuance and DB persistence.
--
-- FLOW (SSOT):
--   topology.components_bucket (unpackaged candidates)
--   -> package generator (issue component_id/package_id/layout_id/wiring_id)
--   -> persist to registry/package/layout/wiring/tensor tables
--   -> frontend projection reads persisted topology definitions
-- =============================================================================

CREATE TABLE IF NOT EXISTS topology.components_bucket (
    bucket_item_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    component_key     TEXT        NOT NULL,
    source_path       TEXT        NOT NULL,
    component_kind    TEXT        NOT NULL,
    status            TEXT        NOT NULL DEFAULT 'bucketed',
    metadata_json     JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_components_bucket_key_path UNIQUE (component_key, source_path),
    CONSTRAINT ck_components_bucket_status CHECK (status IN ('bucketed', 'packaging', 'promoted', 'archived'))
);

CREATE TABLE IF NOT EXISTS topology.ui_component_registry (
    component_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    component_key      TEXT        NOT NULL UNIQUE,
    component_kind     TEXT        NOT NULL,
    source_path        TEXT        NOT NULL,
    props_schema_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    status             TEXT        NOT NULL DEFAULT 'active',
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_ui_component_registry_status CHECK (status IN ('active', 'inactive', 'deprecated'))
);

CREATE TABLE IF NOT EXISTS topology.ui_component_package (
    package_id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    package_key         TEXT        NOT NULL UNIQUE,
    package_kind        TEXT        NOT NULL,
    status              TEXT        NOT NULL DEFAULT 'active',
    package_schema_json JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_ui_component_package_status CHECK (status IN ('active', 'inactive', 'deprecated'))
);

CREATE TABLE IF NOT EXISTS topology.ui_package_component_map (
    map_id               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    package_id           UUID        NOT NULL REFERENCES topology.ui_component_package (package_id) ON DELETE CASCADE,
    component_id         UUID        NOT NULL REFERENCES topology.ui_component_registry (component_id) ON DELETE CASCADE,
    slot_key             TEXT,
    order_index          INTEGER     NOT NULL DEFAULT 0,
    props_override_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- slot_key='default' is the canonical value when no named slot is used.
    -- NULLS NOT DISTINCT ensures a NULL slot_key still prevents duplicate (package_id, component_id) rows.
    CONSTRAINT uq_ui_package_component_map UNIQUE NULLS NOT DISTINCT (package_id, component_id, slot_key)
);

CREATE TABLE IF NOT EXISTS topology.components_layout_design (
    layout_id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    layout_key          TEXT        NOT NULL UNIQUE,
    layout_kind         TEXT        NOT NULL,
    layout_schema_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    css_token_refs      JSONB       NOT NULL DEFAULT '[]'::jsonb,
    responsive_token_refs JSONB     NOT NULL DEFAULT '{}'::jsonb,
    status              TEXT        NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_components_layout_design_status CHECK (status IN ('active', 'inactive', 'deprecated'))
);

-- target_surface allowed values mirror PACKAGE_WIRING_TARGET_SURFACES in frontend/lib/packageWiringOptions.ts.
-- wiring_kind is component-category-driven (open-ended); dispatch routing kind is stored in
-- layout_patch_json.nodes[].wiringKind (JSONB) and enforced at runtime via mapWiringKindToLayer/Action.
CREATE TABLE IF NOT EXISTS topology.ui_wiring_registry (
    wiring_id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    wiring_key          TEXT        NOT NULL UNIQUE,
    wiring_kind         TEXT        NOT NULL,
    target_surface      TEXT        NOT NULL,
    target_ref          TEXT,
    wiring_schema_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    status              TEXT        NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_ui_wiring_registry_status CHECK (status IN ('active', 'inactive', 'deprecated')),
    CONSTRAINT ck_ui_wiring_registry_target_surface CHECK (target_surface IN ('route', 'ui', 'manifest', 'external_port'))
);

CREATE TABLE IF NOT EXISTS topology.ui_topology_tensor (
    tensor_id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    route_key              TEXT        NOT NULL,
    package_id             UUID        NOT NULL REFERENCES topology.ui_component_package (package_id),
    layout_id              UUID        NOT NULL REFERENCES topology.components_layout_design (layout_id),
    wiring_id              UUID        NOT NULL REFERENCES topology.ui_wiring_registry (wiring_id),
    parent_tensor_id       UUID        REFERENCES topology.ui_topology_tensor (tensor_id) ON DELETE SET NULL,
    slot_key               TEXT,
    order_index            INTEGER     NOT NULL DEFAULT 0,
    visibility_rule_json   JSONB       NOT NULL DEFAULT '{}'::jsonb,
    layout_patch_json      JSONB       NOT NULL DEFAULT '{}'::jsonb,
    layout_draft_tmp_json  JSONB,
    css_token_refs         JSONB       NOT NULL DEFAULT '[]'::jsonb,
    responsive_token_refs  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    state_policy_json      JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- slot_key='default' is the canonical value for single-route promote (non-NULL).
    -- NULLS NOT DISTINCT prevents NULL slot_key from bypassing the uniqueness check.
    CONSTRAINT uq_ui_topology_tensor_route_slot_order UNIQUE NULLS NOT DISTINCT (route_key, package_id, layout_id, wiring_id, slot_key, order_index)
);

CREATE INDEX IF NOT EXISTS idx_components_bucket_status ON topology.components_bucket (status);
CREATE INDEX IF NOT EXISTS idx_ui_component_registry_status ON topology.ui_component_registry (status);
CREATE INDEX IF NOT EXISTS idx_ui_component_package_status ON topology.ui_component_package (status);
CREATE INDEX IF NOT EXISTS idx_ui_package_component_map_package ON topology.ui_package_component_map (package_id, order_index);
CREATE INDEX IF NOT EXISTS idx_ui_topology_tensor_route ON topology.ui_topology_tensor (route_key, order_index);

-- =============================================================================
-- UI Component Builder — canonical topology schema placement
--
-- Concrete component authoring layer:
--   topology.ui_builder_components — event-driven component definitions (was: public.components)
--   topology.components_style_design — component styling definitions (was: public.design)
--   topology.components_package_design — component+design bundles (was: public.packages)
--
-- Authoring flow:
--   1. Define components with event bindings (topology.ui_builder_components)
--   2. Create designs (topology.components_style_design)
--   3. Build packages: select a component, pick one or more designs →
--      each choice adds a {componentId, designId} entry to packages.layout
--   4. Admin registers packages.package_id in manifest topology (ui_projection)
-- =============================================================================

CREATE TABLE IF NOT EXISTS topology.ui_builder_components (
    component_id   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    state          TEXT        NOT NULL DEFAULT 'active',
    name           TEXT        NOT NULL UNIQUE,
    event          JSONB       NOT NULL DEFAULT '[]'::jsonb,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_ui_builder_components_state CHECK (state IN ('draft', 'active', 'deprecated'))
);
-- event shape: [{key: string, value: string}, ...]

CREATE TABLE IF NOT EXISTS topology.components_style_design (
    design_id      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name           TEXT        NOT NULL UNIQUE,
    design         JSONB       NOT NULL DEFAULT '{}'::jsonb,
    design_draft_tmp_json JSONB,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);
-- design shape: {componentId?: uuid, layoutNodeId?: string, classname: string, tailwind: string, cssTokenRefs?: string[], responsiveTokenRefs?: {[breakpoint: string]: string[]}, inlineText?: string, linkHref?: string, linkTarget?: string, reactionIntent?: string}
-- design_draft_tmp_json stores the selected canvas node design auto-save draft; component_style_design:upsert promotes design and clears this _tmp field.
-- CSS vocabulary authority is docs/design/css-dictionary-ssot.yaml (static YAML); DB stores promoted token refs/draft state, not CSS dictionary registry rows.
-- componentId is a soft reference for filtering designs by component in the package editor.
-- The authoritative component+design binding lives in topology.components_package_design.layout.

CREATE TABLE IF NOT EXISTS topology.components_package_design (
    package_id     UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    state          TEXT        NOT NULL DEFAULT 'draft',
    name           TEXT        NOT NULL UNIQUE,
    layout         JSONB       NOT NULL DEFAULT '[]'::jsonb,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_components_package_design_state CHECK (state IN ('draft', 'active', 'deprecated'))
);
-- layout shape: [{componentId?: uuid, layoutNodeId?: string, designId: uuid}, ...]
-- One component can appear multiple times paired with different designs.
-- manifest topology entries (ui_projection.packageIds) reference topology.components_package_design.package_id.

CREATE INDEX IF NOT EXISTS idx_ui_builder_components_state ON topology.ui_builder_components (state);
CREATE INDEX IF NOT EXISTS idx_components_style_design_component ON topology.components_style_design USING GIN (design);
CREATE INDEX IF NOT EXISTS idx_components_package_design_state ON topology.components_package_design (state);
CREATE INDEX IF NOT EXISTS idx_components_package_design_layout ON topology.components_package_design USING GIN (layout);
