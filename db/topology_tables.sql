-- =============================================================================
-- topology_tables.sql
-- Topology meaning space tables and converged entity data tables.
--
-- TABLE CATEGORIES:
--   Topology meaning space tables: hubs.hub, hubs.hub_relations
--     hubs.hub: topology meaning space / pseudo-RDB physical table group / join
--       definition owner. Each hub defines one meaning space with a canonical
--       relation jsonb join definition payload.
--     hubs.hub_relations: fixed hub sequence / UI transition order / topology
--       meaning space sequence. sequence_position is the sequence authority.
--       Not a weighted binding table.
--
--   Topology definition tables: structure_maps
--     Binds attractor_keys to resolution chains (package → schema → components).
--     The canonical flow traverses structure_maps to resolve operation vectors.
--
--   Converged entity data tables: entities
--     Hold the runtime-converged state of entities in the topology.
--     Data here is the result of attractor resolution + structure_map
--     resolution applied to raw operation vectors. It is NOT source-of-truth
--     business data — it is the converged projection of topology traversal.
-- =============================================================================


-- ---------------------------------------------------------------------------
-- hubs
-- Topology meaning space / pseudo-RDB physical table group / join definition owner.
-- A hub defines one topology meaning space — a grouping point that owns the
-- canonical join definition for its attractor resolution space. Each hub carries
-- a relation_registry anchor, a state reference, and the join definition payload
-- in relation jsonb. Hubs are populated by the attractor_resolve step.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS hubs.hub (
    hub_id                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    relation_registry_id  UUID,                          -- which relation definition this hub belongs to
    state_id              UUID,                          -- current state from state_registry
    relation              JSONB       NOT NULL DEFAULT '{}',  -- canonical join definition payload
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE hubs.hub IS
    'Topology meaning space / pseudo-RDB physical table group / join definition owner. '
    'Each hub defines one topology meaning space: relation_registry anchor + state + '
    'relation jsonb join definition. Populated by attractor_resolve. '
    'Not source-of-truth business data.';

COMMENT ON COLUMN hubs.hub.relation_registry_id IS
    'References relation_registry.relation_registry_id — the relation definition '
    'this hub is anchored to. FK not enforced here; registry is the authority.';

COMMENT ON COLUMN hubs.hub.state_id IS
    'References state_registry.state_id — the current operational state of this hub.';

COMMENT ON COLUMN hubs.hub.relation IS
    'Canonical join definition payload for this hub. Shape: '
    '{ "id": "...", "relationKey": "...", "joinType": "inner|left|...", "conditions": [...] }. '
    'This column makes hubs.hub the join definition owner in the topology meaning space.';


-- ---------------------------------------------------------------------------
-- entities
-- Converged entity data table.
-- An entity is a resolved data node within a hub. entity_jsonb holds the
-- converged payload produced by schema_resolve + component_expand.
-- relation_ids tracks which relation_registry entries this entity participates in.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.entities (
    entity_id     UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id        UUID        NOT NULL REFERENCES hubs.hub (hub_id) ON DELETE CASCADE,
    entity_jsonb  JSONB       NOT NULL DEFAULT '{}',     -- converged payload
    relation_ids  UUID[]      NOT NULL DEFAULT '{}',     -- participating relation_registry ids
    state_id      UUID,                                  -- current state from state_registry
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE topology.entities IS
    'Converged entity data. Each entity is a resolved data node within a hub and meaning/projection payload surface, '
    'populated by schema_resolve + component_expand in the canonical flow. '
    'entity_jsonb is the converged projection — not raw business input.';

COMMENT ON COLUMN topology.entities.entity_jsonb IS
    'Converged payload produced by schema_resolve and component_expand steps. '
    'Structure is governed by the schema_registry entry resolved for this entity.';

COMMENT ON COLUMN topology.entities.relation_ids IS
    'Array of relation_registry_ids this entity participates in. Maintained by '
    'the attractor_resolve step; used for hub linkage and manifest resolution.';

CREATE INDEX IF NOT EXISTS idx_entities_hub_id
    ON topology.entities (hub_id);

CREATE INDEX IF NOT EXISTS idx_entities_state_id
    ON topology.entities (state_id);

CREATE INDEX IF NOT EXISTS idx_entities_entity_jsonb
    ON topology.entities USING GIN (entity_jsonb);

CREATE INDEX IF NOT EXISTS idx_entities_relation_ids
    ON topology.entities USING GIN (relation_ids);


-- ---------------------------------------------------------------------------
-- hubs.topology_manifests
-- Child of hubs.hub. Manifest grouping surface for the Phase Attention y ID-space.
-- Groups topology manifests associated with a hub, providing the canonical
-- manifest grouping reference for Phase Attention ID-space semantics.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS hubs.topology_manifests (
    topology_manifest_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id                UUID        NOT NULL REFERENCES hubs.hub (hub_id) ON DELETE CASCADE,
    manifest_key          TEXT        NOT NULL,
    status                TEXT        NOT NULL DEFAULT 'active'
                          CHECK (status IN ('active', 'deprecated')),
    topology_jsonb        JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE hubs.topology_manifests IS
    'Child of hubs.hub. Hub-side manifest grouping surface. Canonical manifest reference for Phase Attention '
    'ID-space semantics (y = topology_manifest_id; z is the registered hub_id). '
    'Parent of hubs.hub_relations. Not a wiring table; topology.wiring_physical_to_package owns package wiring.';

CREATE INDEX IF NOT EXISTS idx_topology_manifests_hub_id
    ON hubs.topology_manifests (hub_id);

CREATE INDEX IF NOT EXISTS idx_topology_manifests_status
    ON hubs.topology_manifests (status)
    WHERE status = 'active';


-- ---------------------------------------------------------------------------
-- topology.physical_table_manifest_bindings
-- Explicit SQL Attention resolver association. No implicit/oldest manifest fallback.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.physical_table_manifest_bindings (
    binding_id            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    physical_table_id     BIGINT      NOT NULL REFERENCES topology.physical_tables (physical_table_id) ON DELETE RESTRICT,
    topology_manifest_id  UUID        NOT NULL REFERENCES hubs.topology_manifests (topology_manifest_id) ON DELETE RESTRICT,
    active                BOOLEAN     NOT NULL DEFAULT true,
    binding_evidence_json JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (physical_table_id, topology_manifest_id)
);

COMMENT ON TABLE topology.physical_table_manifest_bindings IS
    'Explicit physical table to hub topology manifest association for SQL Attention resolver. '
    'No implicit join, nullable fallback, or oldest-row fallback is permitted.';

CREATE INDEX IF NOT EXISTS idx_physical_table_manifest_bindings_physical
    ON topology.physical_table_manifest_bindings (physical_table_id)
    WHERE active = true;

CREATE INDEX IF NOT EXISTS idx_physical_table_manifest_bindings_manifest
    ON topology.physical_table_manifest_bindings (topology_manifest_id)
    WHERE active = true;


-- ---------------------------------------------------------------------------
-- hub_relations
-- Child of hubs.topology_manifests. Manifest-scoped hub sequence / UI transition order.
-- Source hub is derived through topology_manifest_id -> hubs.topology_manifests.hub_id.
-- sequence_position is the sequence authority. Not a global hub-to-hub relation graph.
--
-- Bootstrap-only: CREATE TABLE IF NOT EXISTS applies on fresh DB only.
-- Existing DBs with legacy hub_relations (hub_id / target_hub_id / relation_registry_id)
-- require an explicit data-preserving migration:
--   db/migrations/hub_relations_legacy_to_manifest_scoped.sql
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS hubs.hub_relations (
    hub_relation_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    topology_manifest_id  UUID        NOT NULL REFERENCES hubs.topology_manifests (topology_manifest_id) ON DELETE CASCADE,
    related_hub_id        UUID        NOT NULL REFERENCES hubs.hub (hub_id) ON DELETE CASCADE,
    sequence_position     INTEGER     NOT NULL,
    relation_config       JSONB       NOT NULL DEFAULT '{}'::jsonb,
    status                TEXT        NOT NULL DEFAULT 'active'
                          CHECK (status IN ('active', 'deprecated')),
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (topology_manifest_id, sequence_position)
);

COMMENT ON TABLE hubs.hub_relations IS
    'Child of hubs.topology_manifests. Manifest-scoped hub sequence / UI transition order. '
    'related_hub_id is the sequenced hub entry. sequence_position is the sequence authority. '
    'Source hub is derived via topology_manifests.hub_id, not hub_relations.hub_id. '
    'Canonical SQL Attention exploration field. Phase Attention x uses hit hub_relation_id identity; aggregate counts are deprecated support-cache statistics only.';

COMMENT ON COLUMN hubs.hub_relations.topology_manifest_id IS
    'Parent topology manifest scope. Source hub authority flows through topology_manifests.hub_id.';

COMMENT ON COLUMN hubs.hub_relations.related_hub_id IS
    'Sequenced hub entry within the manifest scope.';

COMMENT ON COLUMN hubs.hub_relations.sequence_position IS
    'Sequence authority within topology_manifest_id. Lower value = earlier in sequence.';

COMMENT ON COLUMN hubs.hub_relations.relation_config IS
    'Optional sequence metadata. Not the canonical join definition owner.';

COMMENT ON COLUMN hubs.hub_relations.status IS
    'Lifecycle status of this sequence entry. active = in canonical sequence; '
    'deprecated = removed from sequence but retained for audit.';

CREATE INDEX IF NOT EXISTS idx_hub_relations_topology_manifest_id
    ON hubs.hub_relations (topology_manifest_id);

CREATE INDEX IF NOT EXISTS idx_hub_relations_related_hub_id
    ON hubs.hub_relations (related_hub_id);

CREATE INDEX IF NOT EXISTS idx_hub_relations_sequence_position
    ON hubs.hub_relations (topology_manifest_id, sequence_position);


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
CREATE TABLE IF NOT EXISTS topology.structure_maps (
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

COMMENT ON TABLE topology.structure_maps IS
    'Topology definition table. Binds attractor_keys to resolution chains '
    '(package → schema → components). The canonical flow traverses structure_maps '
    'to convert operation vectors into emissions or projections. '
    'This is topology definition, not converged entity data.';

COMMENT ON COLUMN topology.structure_maps.attractor_key IS
    'The key matched during the attractor_resolve step. Should correspond to '
    'entries in the relation_registry or a domain-defined attractor namespace.';

COMMENT ON COLUMN topology.structure_maps.state_policy IS
    'JSONB policy blob. Encodes state-conditional resolution rules, e.g. '
    'which schema_id applies under a given state_id, or component overrides.';

COMMENT ON COLUMN topology.structure_maps.component_ids IS
    'Ordered array of component_registry ids to expand during component_expand. '
    'Order determines expansion sequence in the canonical flow.';

CREATE INDEX IF NOT EXISTS idx_structure_maps_attractor_key
    ON topology.structure_maps (attractor_key);

CREATE INDEX IF NOT EXISTS idx_structure_maps_package_id
    ON topology.structure_maps (package_id);

CREATE INDEX IF NOT EXISTS idx_structure_maps_schema_id
    ON topology.structure_maps (schema_id);

CREATE INDEX IF NOT EXISTS idx_structure_maps_relation_registry_id
    ON topology.structure_maps (relation_registry_id);

CREATE INDEX IF NOT EXISTS idx_structure_maps_active
    ON topology.structure_maps (active)
    WHERE active = true;

CREATE INDEX IF NOT EXISTS idx_structure_maps_component_ids
    ON topology.structure_maps USING GIN (component_ids);

-- ---------------------------------------------------------------------------
-- topology_edit_log
-- Converged entity data table (append-only audit).
-- Records topology mutations as an append-only edit diff log (domain-scope operation logs).
-- Distinct from demo_state_transitions (which records state machine transitions).
-- Used for runtime audit, recommendation feedback, and persistence tracing.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.topology_edit_log (
    log_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    target_table TEXT        NOT NULL,   -- attractor_key or domain scope identifier
    target_id    TEXT,                   -- record primary key being edited (nullable for creates/lists)
    operation    TEXT        NOT NULL,   -- action name (e.g. "create", "update", "advance")
    before_json  JSONB,                  -- state before the operation (null for creates)
    after_json   JSONB,                  -- state after the operation (null for deletes)
    diff_json    JSONB,                  -- diff between before and after (null when not computed)
    actor        TEXT,                   -- user_id or service identifier
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE topology.topology_edit_log IS
    'Append-only audit log for topology mutations. Distinct from demo_state_transitions '
    'which records state machine transitions. Each row is immutable once inserted. '
    'Used for runtime audit, recommendation feedback, and persistence tracing. '
    'SQL Attention logs SSOT alignment note: this table is not canonical logs.diff because target_table '
    'is currently an attractor/domain scope identifier, not a physical table identity (tableid). '
    'logs.diff reuse requires explicit physical table identity mapping/column.';

COMMENT ON COLUMN topology.topology_edit_log.target_table IS
    'Attractor key or domain scope identifier (e.g. default:entity:create). '
    'Not a literal DB table name; identifies the topology operation scope. '
    'Therefore this column does not satisfy logs.diff.tableid (physical table identity) semantics by itself.';

COMMENT ON COLUMN topology.topology_edit_log.diff_json IS
    'JSON diff between before_json and after_json. Null when not computed '
    '(e.g. on first-version logging before before-state capture is available under runtime contract).';

CREATE INDEX IF NOT EXISTS idx_topology_edit_log_target
    ON topology.topology_edit_log (target_table, target_id);

CREATE INDEX IF NOT EXISTS idx_topology_edit_log_created_at
    ON topology.topology_edit_log (created_at DESC);


CREATE TABLE IF NOT EXISTS topology.demo_state_transitions (
    transition_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_id UUID NOT NULL REFERENCES topology.entities(entity_id) ON DELETE CASCADE,
    action TEXT NOT NULL,
    before_state TEXT,
    after_state TEXT,
    diff_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    event_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_demo_state_transitions_entity_created
    ON topology.demo_state_transitions (entity_id, created_at DESC);


-- ---------------------------------------------------------------------------
-- content_entity_drafts
-- Admin registration staging for entity drafts.
-- Draft rows are NOT visible to runtime browse — promote writes to entities.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.content_entity_drafts (
    draft_id            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id              UUID        NOT NULL,
    entity_jsonb        JSONB       NOT NULL DEFAULT '{}',
    relation_ids        UUID[]      NOT NULL DEFAULT '{}',
    state_id            UUID,
    status              TEXT        NOT NULL DEFAULT 'draft'
                        CHECK (status IN ('draft', 'promoted')),
    promoted_entity_id  UUID,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE topology.content_entity_drafts IS
    'Admin registration staging for entity drafts. Not visible to runtime until promoted to entities.';

CREATE INDEX IF NOT EXISTS idx_content_entity_drafts_status
    ON topology.content_entity_drafts (status)
    WHERE status = 'draft';