-- =============================================================================
-- schema.sql
-- Top-level entrypoint for the topolactor database schema.
--
-- CANONICAL FLOW:
--   stored_topology_data
--     → user_operation
--     → operation_vector
--     → attractor_resolve
--     → structure_map_resolve
--     → package_resolve
--     → schema_resolve
--     → component_expand
--     → emission_or_projection
--
-- TABLE CATEGORIES:
--
--   Topology definition tables (registries, structure_maps, function_parameters):
--     Define the shape and rules of the topology space. These are the
--     authoritative source of truth for how the system resolves operations.
--     Changes here alter topology behavior for all future operations.
--       master_registry, state_registry, relation_registry,
--       package_registry, schema_registry, component_registry,
--       registrar_entries, function_parameters,
--       structure_maps (from topology_tables.sql),
--       hub_relations (from topology_tables.sql)
--
--   Converged entity data tables (entities, hubs, diff_logs):
--     Hold the runtime-converged state produced by traversing the topology.
--     These are outputs of the canonical flow — not source-of-truth business
--     data. Data here reflects the current resolved projection of operations
--     against the topology definition.
--       hubs, entities (from topology_tables.sql)
--
--   Promotion policy tables (from promotion_tables.sql):
--     Track usage patterns and advisory structural change suggestions.
--       usage_metrics, promotion_candidates
--
-- HOW TO RUN (in order):
--   psql -d <database> -f db/schema.sql
--   psql -d <database> -f db/topology_tables.sql
--   psql -d <database> -f db/promotion_tables.sql
--   psql -d <database> -f db/seed_empty.sql
-- =============================================================================


-- ---------------------------------------------------------------------------
-- Extensions
-- ---------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";


-- ---------------------------------------------------------------------------
-- registrar_entries
-- Topology definition table.
-- The registrar is the meta-registry: it records which tables exist in the
-- topology space, their category, and whether they are active. This enables
-- the backend runtime to discover topology tables dynamically without
-- hard-coding table references.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS registrar_entries (
    registrar_entry_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT        NOT NULL UNIQUE,      -- canonical name for this entry
    table_ref           TEXT,                             -- physical table name, if applicable
    category            TEXT,                             -- e.g. 'registry', 'topology', 'promotion'
    active              BOOLEAN     NOT NULL DEFAULT true,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE registrar_entries IS
    'Topology definition table. Meta-registry recording all topology tables, '
    'their categories, and active status. Enables the backend runtime to '
    'discover topology structure dynamically.';

COMMENT ON COLUMN registrar_entries.table_ref IS
    'Physical PostgreSQL table name this entry refers to. May be NULL for '
    'logical/virtual registrar entries that do not map to a single table.';

CREATE INDEX IF NOT EXISTS idx_registrar_entries_active
    ON registrar_entries (active)
    WHERE active = true;


-- ---------------------------------------------------------------------------
-- master_registry
-- Topology definition table.
-- Top-level registry for master domain concepts. Categories here serve as
-- the anchoring taxonomy for relation_registry entries and structure_maps.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS master_registry (
    master_id   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT        NOT NULL,
    category    TEXT,                                     -- domain category label
    active      BOOLEAN     NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE master_registry IS
    'Topology definition table. Top-level registry for master domain concepts '
    'and categories. Anchors the taxonomy used by relation_registry and '
    'structure_maps during attractor resolution.';

CREATE INDEX IF NOT EXISTS idx_master_registry_active
    ON master_registry (active)
    WHERE active = true;


-- ---------------------------------------------------------------------------
-- state_registry
-- Topology definition table.
-- Defines named states within the topology. States are referenced by hubs,
-- entities, and structure_map state_policy blobs. The owner field identifies
-- whether a state is managed by the system, business logic, or is unowned.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS state_registry (
    state_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT        NOT NULL,
    owner       TEXT        CHECK (owner IN ('system', 'business', 'none')),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE state_registry IS
    'Topology definition table. Defines named operational states referenced '
    'by hubs, entities, and structure_map state_policy. '
    'owner distinguishes system-managed, business-managed, and unowned states.';

COMMENT ON COLUMN state_registry.owner IS
    'Ownership tier: '
    '  system   — managed by the abstract runtime (backend) '
    '  business — managed by business/product layer '
    '  none     — unowned, used as a null/default state placeholder';


-- ---------------------------------------------------------------------------
-- relation_registry
-- Topology definition table.
-- Defines named relations in the topology space. Relations are the primary
-- structuring mechanism — hubs, entities, and structure_maps all reference
-- relation_registry entries. master_ids links a relation to one or more
-- master_registry concepts. manifest_candidate flags relations eligible
-- for inclusion in topology manifests during promotion analysis.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS relation_registry (
    relation_registry_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                  TEXT        NOT NULL,
    master_ids            UUID[]      NOT NULL DEFAULT '{}',  -- linked master_registry ids
    category              TEXT,
    type                  TEXT,                               -- e.g. 'structural', 'semantic', 'temporal'
    "order"               INT         NOT NULL DEFAULT 0,     -- resolution ordering hint
    weight                NUMERIC     NOT NULL DEFAULT 1.0,   -- default traversal weight
    manifest_candidate    BOOLEAN     NOT NULL DEFAULT false, -- eligible for manifest promotion
    active                BOOLEAN     NOT NULL DEFAULT true,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE relation_registry IS
    'Topology definition table. Defines all named relations in the topology '
    'space. Relations structure hubs, entities, and structure_map resolution. '
    'manifest_candidate marks relations for inclusion in promotion manifests.';

COMMENT ON COLUMN relation_registry.master_ids IS
    'Array of master_registry ids this relation is associated with. '
    'Used during attractor_resolve to scope relation lookups.';

COMMENT ON COLUMN relation_registry."order" IS
    'Integer ordering hint used when multiple relations are resolved in sequence. '
    'Lower values are resolved first.';

COMMENT ON COLUMN relation_registry.manifest_candidate IS
    'When true, this relation is eligible for inclusion in topology manifests '
    'generated during the promotion_candidates analysis cycle.';

CREATE INDEX IF NOT EXISTS idx_relation_registry_active
    ON relation_registry (active)
    WHERE active = true;

CREATE INDEX IF NOT EXISTS idx_relation_registry_master_ids
    ON relation_registry USING GIN (master_ids);

CREATE INDEX IF NOT EXISTS idx_relation_registry_manifest_candidate
    ON relation_registry (manifest_candidate)
    WHERE manifest_candidate = true;


-- ---------------------------------------------------------------------------
-- package_registry
-- Topology definition table.
-- Packages are resolved during the package_resolve step of the canonical
-- flow. A package groups related schema and component definitions under a
-- versioned/typed bundle. package_def is a free-form JSONB definition blob.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS package_registry (
    package_id   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name         TEXT        NOT NULL,
    type         TEXT,                                    -- e.g. 'core', 'extension', 'external'
    package_def  JSONB       NOT NULL DEFAULT '{}',      -- package definition blob
    active       BOOLEAN     NOT NULL DEFAULT true,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE package_registry IS
    'Topology definition table. Packages are resolved during package_resolve. '
    'Each package groups schema and component definitions. package_def holds '
    'the authoritative package definition consumed by the backend runtime.';

COMMENT ON COLUMN package_registry.package_def IS
    'Free-form JSONB definition blob. Structure is defined by the backend '
    'runtime package resolver and may include version, dependencies, overrides.';

CREATE INDEX IF NOT EXISTS idx_package_registry_active
    ON package_registry (active)
    WHERE active = true;

CREATE INDEX IF NOT EXISTS idx_package_registry_package_def
    ON package_registry USING GIN (package_def);


-- ---------------------------------------------------------------------------
-- schema_registry
-- Topology definition table.
-- Schemas are resolved during the schema_resolve step. Each schema defines
-- the structure of converged entity payloads. schema_def is the authoritative
-- definition blob consumed by the component_expand step.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS schema_registry (
    schema_id   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT        NOT NULL,
    schema_def  JSONB       NOT NULL DEFAULT '{}',       -- schema definition blob
    active      BOOLEAN     NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE schema_registry IS
    'Topology definition table. Schemas define the structure of converged '
    'entity payloads. Resolved during schema_resolve; consumed by '
    'component_expand to validate and shape entity_jsonb output.';

COMMENT ON COLUMN schema_registry.schema_def IS
    'Authoritative schema definition blob. Structure is determined by the '
    'backend schema resolver and governs entity_jsonb shape in entities table.';

CREATE INDEX IF NOT EXISTS idx_schema_registry_active
    ON schema_registry (active)
    WHERE active = true;

CREATE INDEX IF NOT EXISTS idx_schema_registry_schema_def
    ON schema_registry USING GIN (schema_def);


-- ---------------------------------------------------------------------------
-- component_registry
-- Topology definition table.
-- Components are expanded during the component_expand step. Each component
-- is a discrete unit of topology behaviour — a reusable building block
-- referenced by structure_maps via component_ids.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS component_registry (
    component_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name            TEXT        NOT NULL,
    component_type  TEXT,                                -- e.g. 'renderer', 'validator', 'transformer'
    component_def   JSONB       NOT NULL DEFAULT '{}',  -- component definition blob
    active          BOOLEAN     NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE component_registry IS
    'Topology definition table. Components are discrete reusable units of '
    'topology behaviour expanded during component_expand. Referenced from '
    'structure_maps.component_ids in resolution order.';

COMMENT ON COLUMN component_registry.component_def IS
    'Component definition blob. Interpreted by the backend component expander; '
    'may contain renderer configs, validation rules, transformation specs, etc.';

CREATE INDEX IF NOT EXISTS idx_component_registry_active
    ON component_registry (active)
    WHERE active = true;

CREATE INDEX IF NOT EXISTS idx_component_registry_component_type
    ON component_registry (component_type);

CREATE INDEX IF NOT EXISTS idx_component_registry_component_def
    ON component_registry USING GIN (component_def);


-- ---------------------------------------------------------------------------
-- Included files
-- The following tables are defined in separate SQL files and should be
-- included here when running the full schema setup via psql's \i directive,
-- or executed individually in the order shown.
--
-- \i db/topology_tables.sql   -- hubs, entities, hub_relations, structure_maps
-- \i db/promotion_tables.sql  -- usage_metrics, promotion_candidates
--
-- Both files are kept separate for clarity:
--   topology_tables.sql  — topology definition + converged entity data tables
--   promotion_tables.sql — promotion policy tables
-- ---------------------------------------------------------------------------


-- ---------------------------------------------------------------------------
-- function_parameters
-- Topology definition table.
-- Stores named key/value configuration parameters scoped to backend runtime
-- functions. Enables data-driven parameterisation of the canonical flow steps
-- (attractor_resolve, structure_map_resolve, etc.) without code changes.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS function_parameters (
    function_parameter_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    function_name          TEXT        NOT NULL,          -- e.g. 'attractor_resolve', 'component_expand'
    parameter_key          TEXT        NOT NULL,          -- parameter name within that function
    parameter_value        JSONB,                         -- value (any JSONB scalar or object)
    active                 BOOLEAN     NOT NULL DEFAULT true,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_function_parameters_function_key
        UNIQUE (function_name, parameter_key)             -- one policy row per (function, key)
);

COMMENT ON TABLE function_parameters IS
    'Topology definition table. Stores named key/value configuration parameters '
    'for backend runtime functions in the canonical flow. Enables data-driven '
    'parameterisation of attractor_resolve, structure_map_resolve, package_resolve, '
    'schema_resolve, and component_expand without requiring code deployments.';

COMMENT ON COLUMN function_parameters.function_name IS
    'Identifies the canonical flow function this parameter applies to. '
    'Matches the function name used by the backend abstract runtime.';

COMMENT ON COLUMN function_parameters.parameter_value IS
    'JSONB value for this parameter. May be a scalar (string, number, bool), '
    'an array, or an object depending on what the target function expects.';

CREATE INDEX IF NOT EXISTS idx_function_parameters_function_name
    ON function_parameters (function_name);

CREATE INDEX IF NOT EXISTS idx_function_parameters_active
    ON function_parameters (active)
    WHERE active = true;
