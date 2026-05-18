-- =============================================================================
-- topology_tables.sql
-- Topology definition tables and converged entity data tables.
--
-- TABLE CATEGORIES:
--   Topology definition tables: structure_maps, hub_relations
--     These define the shape of the topology space — how entities relate,
--     which attractors map to which packages/schemas/components, and the
--     weight/policy configuration that governs resolution.
--
--   Converged entity data tables: hubs, entities
--     These hold the runtime-converged state of entities in the topology.
--     Data here is the result of attractor resolution + structure_map
--     resolution applied to raw operation vectors. It is NOT source-of-truth
--     business data — it is the converged projection of topology traversal.
-- =============================================================================


-- ---------------------------------------------------------------------------
-- hubs
-- Converged entity data table.
-- A hub is a resolved grouping point in the topology space. Each hub is
-- anchored to a relation_registry entry and carries a state reference.
-- Hubs are populated by the attractor_resolve step in the canonical flow.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS hubs (
    hub_id                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    relation_registry_id  UUID,                          -- which relation definition this hub belongs to
    state_id              UUID,                          -- current state from state_registry
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE hubs IS
    'Converged entity data. Hubs are resolved grouping points in the topology '
    'space, populated during attractor_resolve. Not source-of-truth business data.';

COMMENT ON COLUMN hubs.relation_registry_id IS
    'References relation_registry.relation_registry_id — the relation definition '
    'this hub is anchored to. FK not enforced here; registry is the authority.';

COMMENT ON COLUMN hubs.state_id IS
    'References state_registry.state_id — the current operational state of this hub.';


-- ---------------------------------------------------------------------------
-- entities
-- Converged entity data table.
-- An entity is a resolved data node within a hub. entity_jsonb holds the
-- converged payload produced by schema_resolve + component_expand.
-- relation_ids tracks which relation_registry entries this entity participates in.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS entities (
    entity_id     UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id        UUID        NOT NULL REFERENCES hubs (hub_id) ON DELETE CASCADE,
    entity_jsonb  JSONB       NOT NULL DEFAULT '{}',     -- converged payload
    relation_ids  UUID[]      NOT NULL DEFAULT '{}',     -- participating relation_registry ids
    state_id      UUID,                                  -- current state from state_registry
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE entities IS
    'Converged entity data. Each entity is a resolved data node within a hub, '
    'populated by schema_resolve + component_expand in the canonical flow. '
    'entity_jsonb is the converged projection — not raw business input.';

COMMENT ON COLUMN entities.entity_jsonb IS
    'Converged payload produced by schema_resolve and component_expand steps. '
    'Structure is governed by the schema_registry entry resolved for this entity.';

COMMENT ON COLUMN entities.relation_ids IS
    'Array of relation_registry_ids this entity participates in. Maintained by '
    'the attractor_resolve step; used for hub linkage and manifest resolution.';

CREATE INDEX IF NOT EXISTS idx_entities_hub_id
    ON entities (hub_id);

CREATE INDEX IF NOT EXISTS idx_entities_state_id
    ON entities (state_id);

CREATE INDEX IF NOT EXISTS idx_entities_entity_jsonb
    ON entities USING GIN (entity_jsonb);

CREATE INDEX IF NOT EXISTS idx_entities_relation_ids
    ON entities USING GIN (relation_ids);


-- ---------------------------------------------------------------------------
-- hub_relations
-- Topology definition table.
-- Defines weighted relation bindings between hubs and relation_registry entries.
-- This is part of the topology definition — it configures how hubs connect
-- through the relation graph, not the converged data itself.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS hub_relations (
    hub_relation_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id                UUID        NOT NULL REFERENCES hubs (hub_id) ON DELETE CASCADE,
    relation_registry_id  UUID,                          -- which relation definition applies
    weight                NUMERIC     NOT NULL DEFAULT 1.0,  -- traversal weight for attractor resolution
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE hub_relations IS
    'Topology definition table. Configures weighted relation bindings between '
    'hubs and relation_registry entries. Governs attractor resolution traversal '
    'weights. Distinct from converged entity data.';

COMMENT ON COLUMN hub_relations.weight IS
    'Traversal weight used during attractor_resolve. Higher weight increases '
    'priority of this relation binding when resolving structure_maps.';

CREATE INDEX IF NOT EXISTS idx_hub_relations_hub_id
    ON hub_relations (hub_id);

CREATE INDEX IF NOT EXISTS idx_hub_relations_relation_registry_id
    ON hub_relations (relation_registry_id);


-- ---------------------------------------------------------------------------
-- structure_maps
-- Topology definition table.
-- A structure_map binds an attractor_key to a resolution chain:
--   package → schema → components
-- It is the central topology definition artifact — the canonical flow
-- traverses structure_maps to determine how a user_operation vector is
-- resolved into an emission or projection.
-- state_policy is a jsonb policy blob that governs state-dependent behavior.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS structure_maps (
    structure_map_id      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                  TEXT        NOT NULL,
    attractor_key         TEXT        NOT NULL,           -- key matched during attractor_resolve
    package_id            UUID,                           -- references package_registry
    schema_id             UUID,                           -- references schema_registry
    component_ids         UUID[]      NOT NULL DEFAULT '{}',  -- references component_registry entries
    relation_registry_id  UUID,                           -- scoping relation for this map
    state_policy          JSONB       NOT NULL DEFAULT '{}',  -- state-conditional resolution rules
    active                BOOLEAN     NOT NULL DEFAULT true,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE structure_maps IS
    'Topology definition table. Binds attractor_keys to resolution chains '
    '(package → schema → components). The canonical flow traverses structure_maps '
    'to convert operation vectors into emissions or projections. '
    'This is topology definition, not converged entity data.';

COMMENT ON COLUMN structure_maps.attractor_key IS
    'The key matched during the attractor_resolve step. Should correspond to '
    'entries in the relation_registry or a domain-defined attractor namespace.';

COMMENT ON COLUMN structure_maps.state_policy IS
    'JSONB policy blob. Encodes state-conditional resolution rules, e.g. '
    'which schema_id applies under a given state_id, or component overrides.';

COMMENT ON COLUMN structure_maps.component_ids IS
    'Ordered array of component_registry ids to expand during component_expand. '
    'Order determines expansion sequence in the canonical flow.';

CREATE INDEX IF NOT EXISTS idx_structure_maps_attractor_key
    ON structure_maps (attractor_key);

CREATE INDEX IF NOT EXISTS idx_structure_maps_package_id
    ON structure_maps (package_id);

CREATE INDEX IF NOT EXISTS idx_structure_maps_schema_id
    ON structure_maps (schema_id);

CREATE INDEX IF NOT EXISTS idx_structure_maps_relation_registry_id
    ON structure_maps (relation_registry_id);

CREATE INDEX IF NOT EXISTS idx_structure_maps_active
    ON structure_maps (active)
    WHERE active = true;

CREATE INDEX IF NOT EXISTS idx_structure_maps_component_ids
    ON structure_maps USING GIN (component_ids);

CREATE TABLE IF NOT EXISTS demo_state_transitions (
    transition_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_id UUID NOT NULL REFERENCES entities(entity_id) ON DELETE CASCADE,
    action TEXT NOT NULL,
    before_state TEXT,
    after_state TEXT,
    diff_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    event_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_demo_state_transitions_entity_created
    ON demo_state_transitions (entity_id, created_at DESC);
