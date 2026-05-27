-- =============================================================================
-- ui_topology_tables.sql
-- UI topology tensor persistence tables.
--
-- PURPOSE:
--   Frontend components/packages are not topology entities while code-only.
--   They become topology tensor entities only after ID issuance and DB persistence.
--
-- FLOW (SSOT):
--   ui_component_bucket (unpackaged candidates)
--   -> package generator (issue component_id/package_id/layout_id/wiring_id)
--   -> persist to registry/package/layout/wiring/tensor tables
--   -> frontend projection reads persisted topology definitions
-- =============================================================================

CREATE TABLE IF NOT EXISTS ui_component_bucket (
    bucket_item_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    component_key     TEXT        NOT NULL,
    source_path       TEXT        NOT NULL,
    component_kind    TEXT        NOT NULL,
    status            TEXT        NOT NULL DEFAULT 'bucketed',
    metadata_json     JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_ui_component_bucket_key_path UNIQUE (component_key, source_path),
    CONSTRAINT ck_ui_component_bucket_status CHECK (status IN ('bucketed', 'packaging', 'promoted', 'archived'))
);

CREATE TABLE IF NOT EXISTS ui_component_registry (
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

CREATE TABLE IF NOT EXISTS ui_component_package (
    package_id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    package_key         TEXT        NOT NULL UNIQUE,
    package_kind        TEXT        NOT NULL,
    status              TEXT        NOT NULL DEFAULT 'active',
    package_schema_json JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_ui_component_package_status CHECK (status IN ('active', 'inactive', 'deprecated'))
);

CREATE TABLE IF NOT EXISTS ui_package_component_map (
    map_id               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    package_id           UUID        NOT NULL REFERENCES ui_component_package (package_id) ON DELETE CASCADE,
    component_id         UUID        NOT NULL REFERENCES ui_component_registry (component_id) ON DELETE CASCADE,
    slot_key             TEXT,
    order_index          INTEGER     NOT NULL DEFAULT 0,
    props_override_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_ui_package_component_map UNIQUE (package_id, component_id, slot_key)
);

CREATE TABLE IF NOT EXISTS ui_layout_registry (
    layout_id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    layout_key          TEXT        NOT NULL UNIQUE,
    layout_kind         TEXT        NOT NULL,
    layout_schema_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    css_token_refs      JSONB       NOT NULL DEFAULT '[]'::jsonb,
    responsive_token_refs JSONB     NOT NULL DEFAULT '{}'::jsonb,
    status              TEXT        NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_ui_layout_registry_status CHECK (status IN ('active', 'inactive', 'deprecated'))
);

CREATE TABLE IF NOT EXISTS ui_wiring_registry (
    wiring_id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    wiring_key          TEXT        NOT NULL UNIQUE,
    wiring_kind         TEXT        NOT NULL,
    target_surface      TEXT        NOT NULL,
    target_ref          TEXT,
    wiring_schema_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    status              TEXT        NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_ui_wiring_registry_status CHECK (status IN ('active', 'inactive', 'deprecated'))
);

CREATE TABLE IF NOT EXISTS ui_topology_tensor (
    tensor_id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    route_key              TEXT        NOT NULL,
    package_id             UUID        NOT NULL REFERENCES ui_component_package (package_id),
    layout_id              UUID        NOT NULL REFERENCES ui_layout_registry (layout_id),
    wiring_id              UUID        NOT NULL REFERENCES ui_wiring_registry (wiring_id),
    parent_tensor_id       UUID        REFERENCES ui_topology_tensor (tensor_id) ON DELETE SET NULL,
    slot_key               TEXT,
    order_index            INTEGER     NOT NULL DEFAULT 0,
    visibility_rule_json   JSONB       NOT NULL DEFAULT '{}'::jsonb,
    layout_patch_json      JSONB       NOT NULL DEFAULT '{}'::jsonb,
    css_token_refs         JSONB       NOT NULL DEFAULT '[]'::jsonb,
    responsive_token_refs  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    state_policy_json      JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_ui_topology_tensor_route_slot_order UNIQUE (route_key, package_id, layout_id, wiring_id, slot_key, order_index)
);

CREATE INDEX IF NOT EXISTS idx_ui_component_bucket_status ON ui_component_bucket (status);
CREATE INDEX IF NOT EXISTS idx_ui_component_registry_status ON ui_component_registry (status);
CREATE INDEX IF NOT EXISTS idx_ui_component_package_status ON ui_component_package (status);
CREATE INDEX IF NOT EXISTS idx_ui_package_component_map_package ON ui_package_component_map (package_id, order_index);
CREATE INDEX IF NOT EXISTS idx_ui_topology_tensor_route ON ui_topology_tensor (route_key, order_index);

-- =============================================================================
-- UI Component Builder
--
-- Concrete component authoring layer:
--   components — event-driven component definitions
--   design     — component styling definitions (classname + tailwind)
--   packages   — component+design bundles referenced by manifest topology entries
--
-- Authoring flow:
--   1. Define components with event bindings
--   2. Create designs (design.design.componentId is a soft reference for filtering;
--      "this design was created for component X")
--   3. Build packages: select a component, pick one or more designs →
--      each choice adds a {componentId, designId} entry to packages.layout
--   4. Admin registers packages.package_id in manifest topology (ui_projection)
-- =============================================================================

CREATE TABLE IF NOT EXISTS components (
    component_id   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    state          TEXT        NOT NULL DEFAULT 'active',
    name           TEXT        NOT NULL UNIQUE,
    event          JSONB       NOT NULL DEFAULT '[]'::jsonb,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_components_state CHECK (state IN ('draft', 'active', 'deprecated'))
);
-- event shape: [{key: string, value: string}, ...]

CREATE TABLE IF NOT EXISTS design (
    design_id      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name           TEXT        NOT NULL UNIQUE,
    design         JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);
-- design shape: {componentId: uuid, classname: string, tailwind: string, cssTokenRefs?: string[], responsiveTokenRefs?: {[breakpoint: string]: string[]}}
-- CSS vocabulary authority is docs/design/css-dictionary-ssot.yaml (static YAML); DB stores promoted token refs/draft state, not CSS dictionary registry rows.
-- componentId is a soft reference for filtering designs by component in the package editor.
-- The authoritative component+design binding lives in packages.layout.

CREATE TABLE IF NOT EXISTS packages (
    package_id     UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    state          TEXT        NOT NULL DEFAULT 'draft',
    name           TEXT        NOT NULL UNIQUE,
    layout         JSONB       NOT NULL DEFAULT '[]'::jsonb,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_packages_state CHECK (state IN ('draft', 'active', 'deprecated'))
);
-- layout shape: [{componentId: uuid, designId: uuid}, ...]
-- One component can appear multiple times paired with different designs.
-- manifest topology entries (ui_projection.packageIds) reference packages.package_id.

CREATE INDEX IF NOT EXISTS idx_components_state ON components (state);
CREATE INDEX IF NOT EXISTS idx_design_component ON design USING GIN (design);
CREATE INDEX IF NOT EXISTS idx_packages_state ON packages (state);
CREATE INDEX IF NOT EXISTS idx_packages_layout ON packages USING GIN (layout);
