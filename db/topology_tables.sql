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
--   db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql
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
    layout_id             UUID,                           -- soft ref to topology.components_layout_design (FK added via migration)
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

COMMENT ON COLUMN topology.structure_maps.layout_id IS
    'Optional reference to the admin-authored layout in topology.components_layout_design. '
    'When set, layout_id is forwarded through EmissionBuilder into Emission.LayoutId '
    'and returned to the frontend as emission.layoutId. Null is valid (no layout bound).';

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
    'Append-only audit log for topology mutations. Each row is immutable once inserted. '
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

-- ---------------------------------------------------------------------------
-- topology.external_credential_vault
-- DB guarded credential vault for external port credential attachments.
-- Stores hash metadata and encrypted payload only; plaintext tokens never enter
-- seed SQL, topology projections, manifests, audit logs, or runtime event logs.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.external_credential_vault (
    credential_vault_id      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    provider_kind            TEXT        NOT NULL,
    required_by_bundle       TEXT        NOT NULL,
    token_kind               TEXT        NOT NULL,
    token_hash               TEXT,
    encrypted_payload        BYTEA,
    encryption_key_reference TEXT,
    expires_at               TIMESTAMPTZ,
    refresh_before_seconds   INTEGER     NOT NULL DEFAULT 300 CHECK (refresh_before_seconds >= 0),
    version                  INTEGER     NOT NULL DEFAULT 1 CHECK (version > 0),
    locked_until             TIMESTAMPTZ,
    active                   BOOLEAN     NOT NULL DEFAULT true,
    reference_key            TEXT,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_external_credential_vault_encrypted_payload_reference
        CHECK (encrypted_payload IS NULL OR encryption_key_reference IS NOT NULL)
);

-- Add reference_key column to existing tables (idempotent for re-runs)
ALTER TABLE topology.external_credential_vault
    ADD COLUMN IF NOT EXISTS reference_key TEXT;

CREATE INDEX IF NOT EXISTS idx_external_credential_vault_provider_bundle
    ON topology.external_credential_vault (provider_kind, required_by_bundle)
    WHERE active = true;

CREATE INDEX IF NOT EXISTS idx_external_credential_vault_token_hash
    ON topology.external_credential_vault (token_hash)
    WHERE active = true AND token_hash IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS idx_external_credential_vault_reference_key
    ON topology.external_credential_vault (reference_key)
    WHERE reference_key IS NOT NULL;

COMMENT ON TABLE topology.external_credential_vault IS
    'DB guarded external credential vault for external_port_substrate port record attachments. '
    'Contains token_hash and encrypted_payload only; plaintext credential values are prohibited in DB seed, UI projection, SSOT, manifests, audit logs, and runtime_event_log. '
    'External credentials are not stored in auth.credentials.';

COMMENT ON COLUMN topology.external_credential_vault.encrypted_payload IS
    'Encrypted runtime-only payload for provider re-presentation cases such as OAuth refresh_token rotation. Never expose through UI/projection/logs.';

-- ---------------------------------------------------------------------------
-- topology.external_credential_refresh_attempt
-- Lease/attempt surface for the generic refresher primitive.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.external_credential_refresh_attempt (
    credential_refresh_attempt_id UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    credential_vault_id           UUID        NOT NULL REFERENCES topology.external_credential_vault (credential_vault_id) ON DELETE CASCADE,
    lease_owner                   TEXT        NOT NULL,
    locked_until                  TIMESTAMPTZ NOT NULL,
    attempt_status                TEXT        NOT NULL DEFAULT 'acquired'
                                  CHECK (attempt_status IN ('acquired', 'succeeded', 'failed', 'released')),
    failure_code                  TEXT,
    created_at                    TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_external_credential_refresh_attempt_active
    ON topology.external_credential_refresh_attempt (credential_vault_id, locked_until)
    WHERE attempt_status = 'acquired';

COMMENT ON TABLE topology.external_credential_refresh_attempt IS
    'Generic external credential refresh lease/attempt surface. Provider-specific refresh handlers and external public TokenStore surfaces are prohibited.';

-- ---------------------------------------------------------------------------
-- topology.external_access_ports / response_ports / hook_ports
-- DB seed-driven external port record surfaces. These rows carry only routing,
-- provider classification, credential requirement references, and projection
-- context. Plaintext credential values are prohibited.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.external_access_ports (
    access_port_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    required_by_bundle   TEXT        NOT NULL,
    provider_kind        TEXT        NOT NULL,
    url_or_env_reference TEXT        NOT NULL,
    credential_kind      TEXT        NOT NULL CHECK (credential_kind IN ('auth', 'external', 'none')),
    reference_key        TEXT,
    active               BOOLEAN     NOT NULL DEFAULT true,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_external_access_ports_reference_key CHECK (credential_kind = 'none' OR reference_key IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS topology.external_response_ports (
    response_port_id     UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    required_by_bundle   TEXT        NOT NULL,
    provider_kind        TEXT        NOT NULL,
    url_or_env_reference TEXT        NOT NULL,
    credential_kind      TEXT        NOT NULL CHECK (credential_kind IN ('auth', 'external', 'none')),
    reference_key        TEXT,
    active               BOOLEAN     NOT NULL DEFAULT true,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_external_response_ports_reference_key CHECK (credential_kind = 'none' OR reference_key IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS topology.external_hook_ports (
    hook_port_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    required_by_bundle TEXT        NOT NULL,
    provider_kind      TEXT        NOT NULL,
    hook_path          TEXT        NOT NULL,
    header_key         TEXT,
    route_key          TEXT        NOT NULL,
    credential_kind    TEXT        NOT NULL CHECK (credential_kind IN ('auth', 'external', 'none')),
    reference_key      TEXT,
    active             BOOLEAN     NOT NULL DEFAULT true,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_external_hook_ports_reference_key CHECK (credential_kind = 'none' OR reference_key IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS idx_external_access_ports_bundle_provider ON topology.external_access_ports (required_by_bundle, provider_kind) WHERE active = true;
CREATE INDEX IF NOT EXISTS idx_external_response_ports_bundle_provider ON topology.external_response_ports (required_by_bundle, provider_kind) WHERE active = true;
CREATE INDEX IF NOT EXISTS idx_external_hook_ports_route ON topology.external_hook_ports (hook_path, route_key, provider_kind) WHERE active = true;

COMMENT ON TABLE topology.external_access_ports IS 'Seed-driven access_port records for external_port_substrate. provider_kind is data only; no plaintext credentials.';
COMMENT ON TABLE topology.external_response_ports IS 'Seed-driven response_port records for external_port_substrate. provider_kind is data only; no plaintext credentials.';
COMMENT ON TABLE topology.external_hook_ports IS 'Seed-driven hook_port records. Webhook handling must enqueue scheduler events and must not directly execute runtime.';

CREATE TABLE IF NOT EXISTS topology.external_port_policies (
    policy_id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    policy_key         TEXT        NOT NULL UNIQUE,
    port_kind          TEXT        NOT NULL CHECK (port_kind IN ('access_port', 'response_port', 'hook_port')),
    required_by_bundle TEXT        NOT NULL,
    active             BOOLEAN     NOT NULL DEFAULT true,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);


-- ---------------------------------------------------------------------------
-- Abstract function manifest substrate.
-- Manifest rows are the authority for execute_abstract_function runtime lane,
-- primitive ordering, input bindings, projection allow/deny metadata, and
-- policy binding. Payloads may provide scalar values only; table/column/output
-- authority must be expressed here or by physical table binding seed authority.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.abstract_function_manifests (
    abstract_function_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    function_key         TEXT NOT NULL UNIQUE,
    runtime_lane         TEXT NOT NULL CHECK (runtime_lane IN ('external_port_runtime', 'admin_runtime', 'runtime_executor', 'scheduler_job_runtime')),
    authority_scope      TEXT NOT NULL,
    output_shape         JSONB NOT NULL DEFAULT '{}'::jsonb,
    projection_deny_keys TEXT[] NOT NULL DEFAULT ARRAY[]::text[],
    active              BOOLEAN NOT NULL DEFAULT true,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS topology.abstract_function_steps (
    abstract_function_step_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    abstract_function_id      UUID NOT NULL REFERENCES topology.abstract_function_manifests (abstract_function_id) ON DELETE CASCADE,
    step_order                INTEGER NOT NULL CHECK (step_order > 0),
    primitive_key             TEXT NOT NULL,
    step_config               JSONB NOT NULL DEFAULT '{}'::jsonb,
    result_context_key        TEXT,
    active                    BOOLEAN NOT NULL DEFAULT true,
    is_compensation_step      BOOLEAN NOT NULL DEFAULT false,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (abstract_function_id, step_order)
);

CREATE TABLE IF NOT EXISTS topology.abstract_function_input_bindings (
    input_binding_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    abstract_function_step_id UUID NOT NULL REFERENCES topology.abstract_function_steps (abstract_function_step_id) ON DELETE CASCADE,
    input_key                 TEXT NOT NULL,
    binding_source            TEXT NOT NULL CHECK (binding_source IN ('payload','result_context','constant','external_context','manifest_authority','physical_table_binding','route_context','step_config','runtime_context','scheduler_context','scheduler_input')),
    binding_path              TEXT NOT NULL,
    required                  BOOLEAN NOT NULL DEFAULT true,
    secret                    BOOLEAN NOT NULL DEFAULT false,
    active                    BOOLEAN NOT NULL DEFAULT true,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (abstract_function_step_id, input_key)
);

CREATE TABLE IF NOT EXISTS topology.abstract_function_authority_bindings (
    authority_binding_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    abstract_function_id UUID NOT NULL REFERENCES topology.abstract_function_manifests (abstract_function_id) ON DELETE CASCADE,
    authority_kind       TEXT NOT NULL CHECK (authority_kind IN ('table','column','join','output','policy')),
    authority_ref        TEXT NOT NULL,
    active               BOOLEAN NOT NULL DEFAULT true,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (abstract_function_id, authority_kind, authority_ref)
);

CREATE INDEX IF NOT EXISTS idx_abstract_function_steps_manifest
    ON topology.abstract_function_steps (abstract_function_id, step_order) WHERE active = true;

CREATE INDEX IF NOT EXISTS idx_abstract_function_bindings_step
    ON topology.abstract_function_input_bindings (abstract_function_step_id) WHERE active = true;

COMMENT ON TABLE topology.abstract_function_manifests IS 'Manifest authority for backend execute_abstract_function. Frontend payloads do not provide table, column, join, output, or secret projection authority.';
COMMENT ON TABLE topology.abstract_function_steps IS 'Ordered generic primitive steps for execute_abstract_function; primitive_key is data and must resolve through the backend generic primitive registry.';
COMMENT ON TABLE topology.abstract_function_input_bindings IS 'Typed input binding from payload/context/result/manifest/physical binding authority. Raw SQL and payload-derived table authority are prohibited.';
COMMENT ON TABLE topology.abstract_function_authority_bindings IS 'Table/column/join/output/policy authority for abstract functions. Used to fail-close missing or invalid authority.';

CREATE TABLE IF NOT EXISTS topology.external_port_policy_steps (
    policy_step_id UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    policy_id      UUID        NOT NULL REFERENCES topology.external_port_policies (policy_id) ON DELETE CASCADE,
    step_order     INTEGER     NOT NULL CHECK (step_order > 0),
    operation_key  TEXT        NOT NULL CHECK (operation_key IN ('resolve_port_record','resolve_credential_reference','load_encrypted_credential_payload','decrypt_for_runtime_use','build_http_request','inject_authorization_header','send_http','capture_response','execute_db_function','verify_signature_by_config','enqueue_scheduler_event','append_runtime_event_log','fail_close','acquire_refresh_lease','request_token_by_config','write_encrypted_credential_payload','update_token_hash','update_expires_at_and_version','release_refresh_lease','compute_checksum','execute_abstract_function')),
    step_config    JSONB       NOT NULL DEFAULT '{}'::jsonb,
    abstract_function_key TEXT REFERENCES topology.abstract_function_manifests (function_key),
    active         BOOLEAN     NOT NULL DEFAULT true,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (policy_id, step_order)
);

COMMENT ON TABLE topology.external_port_policies IS 'Seed-driven policy surface for external port generic primitive execution.';

-- Update operation_key CHECK constraint to sync with SSOT operation_key_allowed_values.
-- execute_abstract_function is the primary abstract function boundary for consumer bundle domain mutations
-- (manifests af01-af07 dispatch topology.fs_* PostgreSQL functions via call_postgres_function primitive).
-- execute_db_function remains in the constraint for schema compatibility; NpgsqlExternalPortDbFunctionRepository
-- is now a compatibility stub (no concrete fs_* methods); no new operations should use this path.
-- compute_checksum is a bundle-specific key handled by FileStorageBundleStepHandler.
ALTER TABLE topology.external_port_policy_steps
    DROP CONSTRAINT IF EXISTS external_port_policy_steps_operation_key_check;

ALTER TABLE topology.external_port_policy_steps
    ADD CONSTRAINT external_port_policy_steps_operation_key_check
    CHECK (operation_key IN (
        'resolve_port_record','resolve_credential_reference','load_encrypted_credential_payload',
        'decrypt_for_runtime_use','build_http_request','inject_authorization_header','send_http',
        'capture_response','execute_db_function','verify_signature_by_config','enqueue_scheduler_event',
        'append_runtime_event_log','fail_close','acquire_refresh_lease','request_token_by_config',
        'write_encrypted_credential_payload','update_token_hash','update_expires_at_and_version',
        'release_refresh_lease',
        'compute_checksum','execute_abstract_function',
        'record_transfer_lifecycle_evidence'
    ));

-- ---------------------------------------------------------------------------
-- File storage bundle physical tables.
-- These are the domain-side metadata tables for export_job / file_artifact /
-- checksum / manifest / signed download authorization tracking.
-- Plaintext credentials, bucket names, endpoints, and actual signed URLs are
-- prohibited. storage_ref is env-var reference identifier only.
-- authorization_key is a non-guessable reference identifier, NOT a signed URL.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.export_jobs (
    export_job_id     UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    port_id           UUID,
    port_kind         TEXT        NOT NULL DEFAULT 'access_port'
                                  CHECK (port_kind IN ('access_port', 'response_port')),
    requested_by      TEXT        NOT NULL,
    requested_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    period            TEXT,
    target_scope      TEXT,
    export_format     TEXT,
    status            TEXT        NOT NULL DEFAULT 'initiated'
                                  CHECK (status IN ('initiated', 'in_progress', 'completed', 'failed')),
    source_record_ids JSONB       NOT NULL DEFAULT '[]'::jsonb,
    idempotency_key   TEXT        NOT NULL UNIQUE,
    checksum          TEXT,
    manifest_path     TEXT,
    completed_at      TIMESTAMPTZ,
    failed_at         TIMESTAMPTZ,
    failure_code      TEXT,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_export_jobs_status
    ON topology.export_jobs (status)
    WHERE status IN ('initiated', 'in_progress');

CREATE INDEX IF NOT EXISTS idx_export_jobs_requested_at
    ON topology.export_jobs (requested_at DESC);

COMMENT ON TABLE topology.export_jobs IS
    'File storage bundle export job tracking. storage_ref and authorization_key are '
    'env-var reference identifiers only; plaintext credentials, bucket names, and '
    'actual signed URLs are prohibited.';

CREATE TABLE IF NOT EXISTS topology.file_artifacts (
    file_artifact_id   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    export_job_id      UUID        NOT NULL REFERENCES topology.export_jobs (export_job_id) ON DELETE CASCADE,
    file_name          TEXT        NOT NULL,
    file_type          TEXT        NOT NULL
                                   CHECK (file_type IN ('pdf', 'csv', 'json', 'zip', 'receipt_image', 'manifest_json')),
    storage_ref        TEXT        NOT NULL,
    byte_size          BIGINT,
    checksum_value     TEXT        NOT NULL,
    checksum_record_id UUID,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_file_artifacts_export_job
    ON topology.file_artifacts (export_job_id);

COMMENT ON TABLE topology.file_artifacts IS
    'File artifact metadata for file_storage_bundle. storage_ref is env-var reference identifier only; '
    'plaintext storage URL/path is prohibited.';

CREATE TABLE IF NOT EXISTS topology.file_checksum_records (
    checksum_record_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    export_job_id       UUID        NOT NULL REFERENCES topology.export_jobs (export_job_id) ON DELETE CASCADE,
    file_artifact_id    UUID        NOT NULL REFERENCES topology.file_artifacts (file_artifact_id) ON DELETE CASCADE,
    algorithm           TEXT        NOT NULL DEFAULT 'sha256',
    checksum_value      TEXT        NOT NULL,
    verified_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    verification_status TEXT        NOT NULL DEFAULT 'pending'
                                    CHECK (verification_status IN ('pending', 'verified', 'failed')),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_file_checksum_export_job
    ON topology.file_checksum_records (export_job_id);

COMMENT ON TABLE topology.file_checksum_records IS
    'Checksum integrity records for file_storage_bundle. Required for all export_job file artifacts.';

CREATE TABLE IF NOT EXISTS topology.export_manifests (
    manifest_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    export_job_id     UUID        NOT NULL UNIQUE REFERENCES topology.export_jobs (export_job_id) ON DELETE CASCADE,
    manifest_version  TEXT        NOT NULL DEFAULT '1.0',
    generated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    generated_by      TEXT        NOT NULL,
    period            TEXT,
    export_format     TEXT,
    checksum          TEXT,
    file_artifact_ids JSONB       NOT NULL DEFAULT '[]'::jsonb,
    manifest_jsonb    JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE topology.export_manifests IS
    'Export package manifest records for file_storage_bundle. Required for all export_job packages.';

CREATE TABLE IF NOT EXISTS topology.signed_download_authorizations (
    authorization_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    file_artifact_id  UUID        NOT NULL REFERENCES topology.file_artifacts (file_artifact_id) ON DELETE CASCADE,
    authorized_by     TEXT        NOT NULL,
    authorized_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at        TIMESTAMPTZ NOT NULL,
    authorization_key TEXT        NOT NULL UNIQUE,
    used_at           TIMESTAMPTZ,
    status            TEXT        NOT NULL DEFAULT 'active'
                                  CHECK (status IN ('active', 'used', 'expired', 'revoked')),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_signed_download_auth_artifact
    ON topology.signed_download_authorizations (file_artifact_id);

CREATE INDEX IF NOT EXISTS idx_signed_download_auth_key
    ON topology.signed_download_authorizations (authorization_key)
    WHERE status = 'active';

COMMENT ON TABLE topology.signed_download_authorizations IS
    'Signed download authorization records for file_storage_bundle. authorization_key is a non-guessable '
    'reference identifier; actual signed URL value is managed by runtime secret store and is prohibited '
    'from this table.';

CREATE TABLE IF NOT EXISTS topology.record_file_attachment_bindings (
    attachment_binding_id UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    record_table_ref      TEXT        NOT NULL,
    record_id             TEXT        NOT NULL,
    file_artifact_id      UUID        NOT NULL REFERENCES topology.file_artifacts (file_artifact_id) ON DELETE CASCADE,
    relation_kind         TEXT        NOT NULL DEFAULT 'attachment',
    metadata_jsonb        JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_by           TEXT        NOT NULL DEFAULT 'system',
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (record_table_ref, record_id, file_artifact_id, relation_kind)
);

CREATE INDEX IF NOT EXISTS idx_record_file_attachment_record
    ON topology.record_file_attachment_bindings (record_table_ref, record_id);

CREATE INDEX IF NOT EXISTS idx_record_file_attachment_artifact
    ON topology.record_file_attachment_bindings (file_artifact_id);

COMMENT ON TABLE topology.record_file_attachment_bindings IS
    'Binding surface that attaches arbitrary records to existing file_artifacts. '
    'This is not a standalone attachment artifact store and contains no credential, storage path, bucket, endpoint, or signed URL values.';
COMMENT ON TABLE topology.external_port_policy_steps IS 'Ordered operation_key steps. operation_key values are constrained to external-port SSOT allowed values.';

-- ---------------------------------------------------------------------------
-- Runtime event log.
-- Evidence surface for append_runtime_event_log policy step operation.
-- Records consumer bundle domain events (export_job_initiated, file_write_completed,
-- checksum_verified, signed_url_generated, etc.) WITHOUT credential, signed URL,
-- bucket name, or storage endpoint values.
-- entity_id is an opaque reference identifier (UUID string or reference key).
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.runtime_event_log (
    event_log_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type         TEXT        NOT NULL,
    entity_id          TEXT,
    required_by_bundle TEXT,
    logged_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_runtime_event_log_event_type
    ON topology.runtime_event_log (event_type);

CREATE INDEX IF NOT EXISTS idx_runtime_event_log_logged_at
    ON topology.runtime_event_log (logged_at DESC);

COMMENT ON TABLE topology.runtime_event_log IS
    'Append-only runtime evidence log for external port consumer bundle domain events. '
    'Prohibited: plaintext credential, signed URL, bucket name, storage endpoint, encrypted payload. '
    'entity_id is opaque reference identifier only.';

-- ---------------------------------------------------------------------------
-- external_port_substrate consumer bundle evidence tables.
-- These physical tables are generic evidence/projection surfaces only. Provider
-- credentials, endpoint values, tokens, and signed URLs are never stored here.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.email_drafts (
    email_draft_id      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_record_id  UUID,
    delivery_status     TEXT        NOT NULL DEFAULT 'draft',
    response_port_ref   TEXT,
    evidence_json       JSONB       NOT NULL DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS topology.email_approval_records (
    approval_record_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    email_draft_id      UUID,
    approval_status     TEXT        NOT NULL DEFAULT 'pending',
    reviewed_by         TEXT,
    reviewed_at         TIMESTAMPTZ,
    evidence_json       JSONB       NOT NULL DEFAULT '{}'
);

CREATE TABLE IF NOT EXISTS topology.email_delivery_evidence (
    delivery_evidence_id UUID       PRIMARY KEY DEFAULT gen_random_uuid(),
    email_draft_id       UUID,
    event_type           TEXT       NOT NULL,
    delivery_status      TEXT       NOT NULL,
    evidence_json        JSONB      NOT NULL DEFAULT '{}',
    recorded_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS topology.webhook_intake_snapshots (
    webhook_intake_snapshot_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    required_by_bundle         TEXT NOT NULL,
    route_key                  TEXT,
    hook_path                  TEXT,
    payload_hash               TEXT NOT NULL,
    scheduler_event_ref        TEXT,
    received_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    evidence_json              JSONB NOT NULL DEFAULT '{}'
);

CREATE TABLE IF NOT EXISTS topology.signature_verification_evidence (
    signature_verification_evidence_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    webhook_intake_snapshot_id         UUID,
    required_by_bundle                 TEXT NOT NULL,
    verification_status                TEXT NOT NULL,
    evidence_json                      JSONB NOT NULL DEFAULT '{}',
    recorded_at                        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS topology.payment_state_projections (
    payment_state_projection_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    webhook_intake_snapshot_id  UUID,
    payment_state               TEXT NOT NULL,
    evidence_json               JSONB NOT NULL DEFAULT '{}',
    projected_at                TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS topology.scheduler_external_event_evidence (
    scheduler_event_evidence_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    required_by_bundle          TEXT NOT NULL DEFAULT 'job_scheduler_bundle',
    trigger_ref                 TEXT,
    scheduler_status            TEXT NOT NULL DEFAULT 'scheduler_enqueued',
    evidence_json               JSONB NOT NULL DEFAULT '{}',
    recorded_at                 TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS topology.audit_approval_requests (
    approval_request_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    request_status      TEXT NOT NULL DEFAULT 'pending',
    response_port_ref   TEXT,
    evidence_json       JSONB NOT NULL DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS topology.audit_approval_evidence (
    approval_evidence_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_request_id  UUID,
    event_type           TEXT NOT NULL,
    approval_status      TEXT NOT NULL,
    evidence_json        JSONB NOT NULL DEFAULT '{}',
    recorded_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS topology.audit_notification_evidence (
    notification_evidence_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_request_id      UUID,
    notification_status      TEXT NOT NULL,
    response_port_ref        TEXT,
    evidence_json            JSONB NOT NULL DEFAULT '{}',
    recorded_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS topology.sftp_transfer_log (
    sftp_transfer_log_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    export_job_id        UUID REFERENCES topology.export_jobs (export_job_id) ON DELETE CASCADE,
    file_artifact_id     UUID REFERENCES topology.file_artifacts (file_artifact_id) ON DELETE SET NULL,
    manifest_id          UUID REFERENCES topology.export_manifests (manifest_id) ON DELETE SET NULL,
    checksum_record_id   UUID REFERENCES topology.file_checksum_records (checksum_record_id) ON DELETE SET NULL,
    transfer_status      TEXT NOT NULL DEFAULT 'pending'
                         CHECK (transfer_status IN (
                            'pending',
                            'transfer_initiated',
                            'transfer_completed',
                            'transfer_failed',
                            'checksum_mismatch',
                            'retry_attempted'
                         )),
    failure_reason       TEXT,
    checksum_before      TEXT,
    checksum_after       TEXT,
    manifest_ref         TEXT,
    response_port_ref    TEXT,
    retry_evidence_json  JSONB NOT NULL DEFAULT '{}'::jsonb,
    evidence_json        JSONB NOT NULL DEFAULT '{}',
    recorded_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sftp_transfer_log_export_job
    ON topology.sftp_transfer_log (export_job_id);

CREATE INDEX IF NOT EXISTS idx_sftp_transfer_log_status
    ON topology.sftp_transfer_log (transfer_status);

COMMENT ON TABLE topology.sftp_transfer_log IS
    'Export/SFTP transfer evidence and safe projection table. Depends on file_storage_bundle export_jobs, file_artifacts, export_manifests, and file_checksum_records. Prohibited: plaintext credential, host, user, private key, endpoint, remote_path, signed_url, or raw provider response.';

-- ---------------------------------------------------------------------------
-- file_storage_bundle domain PostgreSQL functions.
-- Called via execute_abstract_function → abstract function manifests af01-af07
-- → call_postgres_function primitive adapter (not via execute_db_function directly).
-- These are opaque DB adapter implementations; bundle-specific C# handler
-- code (FileStorageBundleStepHandler) must NOT call these directly.
-- storage_ref / authorization_key are opaque reference identifiers — never plaintext.
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION topology.fs_record_export_job(
    p_idempotency_key    TEXT,
    p_required_by_bundle TEXT,
    p_port_id            UUID,
    p_port_kind          TEXT,
    p_requested_by       TEXT,
    p_export_format      TEXT DEFAULT NULL,
    p_period             TEXT DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE v_id UUID;
BEGIN
    INSERT INTO topology.export_jobs (
        idempotency_key, port_id, port_kind, requested_by,
        export_format, period, status
    ) VALUES (
        p_idempotency_key, p_port_id, p_port_kind, p_requested_by,
        p_export_format, p_period, 'in_progress'
    )
    ON CONFLICT (idempotency_key) DO UPDATE SET updated_at = now()
    RETURNING export_job_id INTO v_id;
    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION topology.fs_record_export_job IS
    'Records or idempotently upserts a file_storage export job. Called via execute_abstract_function manifest / call_postgres_function primitive step.';

CREATE OR REPLACE FUNCTION topology.fs_record_file_artifact(
    p_export_job_id   UUID,
    p_file_name       TEXT,
    p_file_type       TEXT,
    p_storage_ref     TEXT,
    p_checksum_value  TEXT
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE
    v_artifact_id UUID;
    v_checksum_id UUID;
BEGIN
    INSERT INTO topology.file_artifacts (
        export_job_id, file_name, file_type, storage_ref, checksum_value
    ) VALUES (
        p_export_job_id, p_file_name, p_file_type, p_storage_ref, p_checksum_value
    )
    RETURNING file_artifact_id INTO v_artifact_id;

    INSERT INTO topology.file_checksum_records (
        export_job_id, file_artifact_id, algorithm, checksum_value, verification_status
    ) VALUES (
        p_export_job_id, v_artifact_id, 'sha256', p_checksum_value, 'verified'
    )
    RETURNING checksum_record_id INTO v_checksum_id;

    UPDATE topology.file_artifacts
       SET checksum_record_id = v_checksum_id, updated_at = now()
     WHERE file_artifact_id = v_artifact_id;

    RETURN v_artifact_id;
END;
$$;

COMMENT ON FUNCTION topology.fs_record_file_artifact IS
    'Atomically inserts file_artifact and its checksum_record in a single transaction. Called via execute_abstract_function manifest / call_postgres_function primitive step.';

CREATE OR REPLACE FUNCTION topology.fs_write_manifest_record(
    p_export_job_id    UUID,
    p_file_artifact_id UUID,
    p_requested_by     TEXT DEFAULT 'system',
    p_period           TEXT DEFAULT NULL,
    p_export_format    TEXT DEFAULT NULL,
    p_checksum_value   TEXT DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE v_id UUID;
BEGIN
    INSERT INTO topology.export_manifests (
        export_job_id, manifest_version, generated_by, period,
        export_format, manifest_jsonb
    ) VALUES (
        p_export_job_id, '1.0', p_requested_by, p_period,
        p_export_format,
        jsonb_build_object(
            'file_artifact_ids', jsonb_build_array(p_file_artifact_id::text),
            'checksum', p_checksum_value
        )
    )
    ON CONFLICT (export_job_id) DO UPDATE
        SET manifest_jsonb = EXCLUDED.manifest_jsonb,
            updated_at = now()
    RETURNING manifest_id INTO v_id;
    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION topology.fs_write_manifest_record IS
    'Writes or updates export package manifest record. Called via execute_abstract_function manifest / call_postgres_function primitive step.';

CREATE OR REPLACE FUNCTION topology.fs_authorize_signed_download(
    p_file_artifact_id UUID,
    p_authorized_by    TEXT DEFAULT 'system'
) RETURNS TEXT
LANGUAGE plpgsql AS $$
DECLARE v_key TEXT;
BEGIN
    v_key := 'auth-ref:' || replace(p_file_artifact_id::text, '-', '') || ':' || replace(gen_random_uuid()::text, '-', '');
    INSERT INTO topology.signed_download_authorizations (
        file_artifact_id, authorized_by, expires_at, authorization_key
    ) VALUES (
        p_file_artifact_id, p_authorized_by, now() + INTERVAL '1 hour', v_key
    );
    RETURN v_key;
END;
$$;

COMMENT ON FUNCTION topology.fs_authorize_signed_download IS
    'Creates a non-guessable authorization_key reference for signed download. authorization_key is opaque ref — not a signed URL. Called via execute_abstract_function manifest / call_postgres_function primitive step.';

CREATE OR REPLACE FUNCTION topology.fs_bind_record_file_attachment(
    p_record_table_ref TEXT,
    p_record_id        TEXT,
    p_file_artifact_id UUID,
    p_relation_kind    TEXT DEFAULT 'attachment',
    p_created_by       TEXT DEFAULT 'system',
    p_metadata_jsonb   JSONB DEFAULT '{}'::jsonb
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE v_id UUID;
BEGIN
    IF p_record_table_ref IS NULL OR btrim(p_record_table_ref) = '' THEN
        RAISE EXCEPTION 'RECORD_TABLE_REF_REQUIRED';
    END IF;
    IF p_record_id IS NULL OR btrim(p_record_id) = '' THEN
        RAISE EXCEPTION 'RECORD_ID_REQUIRED';
    END IF;

    INSERT INTO topology.record_file_attachment_bindings (
        record_table_ref, record_id, file_artifact_id, relation_kind, created_by, metadata_jsonb
    ) VALUES (
        p_record_table_ref, p_record_id, p_file_artifact_id, COALESCE(NULLIF(p_relation_kind, ''), 'attachment'), p_created_by, COALESCE(p_metadata_jsonb, '{}'::jsonb)
    )
    ON CONFLICT (record_table_ref, record_id, file_artifact_id, relation_kind)
    DO UPDATE SET metadata_jsonb = EXCLUDED.metadata_jsonb, updated_at = now()
    RETURNING attachment_binding_id INTO v_id;
    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION topology.fs_bind_record_file_attachment IS
    'Binds an arbitrary record reference to an existing file_artifact via execute_abstract_function manifest af05 / call_postgres_function primitive; does not create a credential or signed URL projection.';

CREATE OR REPLACE FUNCTION topology.fs_list_record_file_attachments(
    p_record_table_ref TEXT,
    p_record_id        TEXT
) RETURNS JSONB
LANGUAGE plpgsql AS $$
BEGIN
    RETURN (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'attachment_binding_id', b.attachment_binding_id,
            'record_table_ref', b.record_table_ref,
            'record_id', b.record_id,
            'file_artifact_id', b.file_artifact_id,
            'file_name', a.file_name,
            'file_type', a.file_type,
            'byte_size', a.byte_size,
            'checksum_value', a.checksum_value,
            'relation_kind', b.relation_kind,
            'metadata', b.metadata_jsonb,
            'created_at', b.created_at
        ) ORDER BY b.created_at DESC), '[]'::jsonb)
        FROM topology.record_file_attachment_bindings b
        JOIN topology.file_artifacts a ON a.file_artifact_id = b.file_artifact_id
        WHERE b.record_table_ref = p_record_table_ref
          AND b.record_id = p_record_id
    );
END;
$$;

COMMENT ON FUNCTION topology.fs_list_record_file_attachments IS
    'Lists attachment metadata for a record. Projection intentionally excludes storage_ref, authorization_key, signed URLs, and credentials.';

CREATE OR REPLACE FUNCTION topology.fs_unbind_record_file_attachment(
    p_record_table_ref TEXT,
    p_record_id        TEXT,
    p_file_artifact_id UUID,
    p_relation_kind    TEXT DEFAULT 'attachment'
) RETURNS INTEGER
LANGUAGE plpgsql AS $$
DECLARE v_count INTEGER;
BEGIN
    DELETE FROM topology.record_file_attachment_bindings
    WHERE record_table_ref = p_record_table_ref
      AND record_id = p_record_id
      AND file_artifact_id = p_file_artifact_id
      AND relation_kind = COALESCE(NULLIF(p_relation_kind, ''), 'attachment');
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END;
$$;

COMMENT ON FUNCTION topology.fs_unbind_record_file_attachment IS
    'Removes a record-to-file_artifact binding via execute_abstract_function manifest af07 / call_postgres_function primitive.';


-- ---------------------------------------------------------------------------
-- audit_approval_bundle domain PostgreSQL functions.
-- Called via execute_abstract_function → abstract function manifests af13-af15
-- → call_postgres_function primitive adapter (not via execute_db_function directly).
-- Approval authority is ui_human_explicit_action_only per runtime-bundle-audit-approval-ssot.
-- Prohibited: AI autonomous approval, implicit approval, silent audit log skip,
--             CLI/MCP approval trigger, credential plaintext in evidence.
-- ---------------------------------------------------------------------------

-- idempotency_key column for audit_approval_requests (added idempotently).
ALTER TABLE topology.audit_approval_requests
    ADD COLUMN IF NOT EXISTS idempotency_key TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS idx_audit_approval_requests_idempotency_key
    ON topology.audit_approval_requests (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE OR REPLACE FUNCTION topology.aa_record_approval_request(
    p_idempotency_key    TEXT,
    p_approval_subject   TEXT,
    p_requester          TEXT,
    p_response_port_ref  TEXT DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE v_id UUID;
BEGIN
    INSERT INTO topology.audit_approval_requests (
        idempotency_key, request_status, response_port_ref, evidence_json
    ) VALUES (
        p_idempotency_key,
        'pending',
        p_response_port_ref,
        jsonb_build_object(
            'approval_subject', p_approval_subject,
            'requester', p_requester
        )
    )
    ON CONFLICT (idempotency_key) DO UPDATE
        SET updated_at = now()
    RETURNING approval_request_id INTO v_id;
    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION topology.aa_record_approval_request IS
    'Records or idempotently upserts an audit_approval request. Called via execute_abstract_function manifest af13 / call_postgres_function primitive. Approval authority is UI/human explicit action only.';

CREATE OR REPLACE FUNCTION topology.aa_record_approval_evidence(
    p_approval_request_id UUID,
    p_event_type          TEXT,
    p_approval_status     TEXT
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE v_id UUID;
BEGIN
    INSERT INTO topology.audit_approval_evidence (
        approval_request_id, event_type, approval_status, evidence_json
    ) VALUES (
        p_approval_request_id,
        p_event_type,
        p_approval_status,
        jsonb_build_object('recorded_by', 'audit_approval_bundle')
    )
    RETURNING approval_evidence_id INTO v_id;

    UPDATE topology.audit_approval_requests
       SET request_status = p_approval_status,
           updated_at     = now()
     WHERE approval_request_id = p_approval_request_id;

    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION topology.aa_record_approval_evidence IS
    'Records audit_approval evidence (granted/rejected) and updates request_status. Called via execute_abstract_function manifest af14 / call_postgres_function primitive. Prohibited: AI autonomous approval, implicit approval.';

CREATE OR REPLACE FUNCTION topology.aa_record_notification_evidence(
    p_approval_request_id UUID,
    p_notification_status TEXT,
    p_response_port_ref   TEXT DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE v_id UUID;
BEGIN
    INSERT INTO topology.audit_notification_evidence (
        approval_request_id, notification_status, response_port_ref, evidence_json
    ) VALUES (
        p_approval_request_id,
        p_notification_status,
        p_response_port_ref,
        jsonb_build_object('notification_bundle', 'audit_approval_bundle')
    )
    RETURNING notification_evidence_id INTO v_id;
    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION topology.aa_record_notification_evidence IS
    'Records notification dispatch evidence for an audit_approval request. Called via execute_abstract_function manifest af15 / call_postgres_function primitive. Prohibited: credential plaintext in evidence.';


-- ---------------------------------------------------------------------------
-- email_bundle domain PostgreSQL functions.
-- Called via execute_abstract_function → abstract function manifests af40-af43
-- → call_postgres_function primitive adapter (not via execute_db_function directly).
-- Email send authority is ui_approval_then_backend_dispatch per
-- runtime-bundle-email-ssot. Send is a side effect only AFTER explicit human approval.
-- Prohibited: AI autonomous send, implicit approval, CLI/MCP send, silent failure
--             fallback, silent retry, SMTP host/port/api key/raw provider response
--             stored in DB/projection/log.
-- ---------------------------------------------------------------------------

-- dispatch_idempotency_key + recipient/subject reference columns (added idempotently).
ALTER TABLE topology.email_drafts
    ADD COLUMN IF NOT EXISTS dispatch_idempotency_key TEXT;
ALTER TABLE topology.email_drafts
    ADD COLUMN IF NOT EXISTS recipient_ref TEXT;
ALTER TABLE topology.email_drafts
    ADD COLUMN IF NOT EXISTS subject_ref TEXT;

-- Plain unique index (NULLs are distinct in PostgreSQL): supports ON CONFLICT
-- (dispatch_idempotency_key) as arbiter while still permitting null-keyed rows.
CREATE UNIQUE INDEX IF NOT EXISTS idx_email_drafts_dispatch_idempotency_key
    ON topology.email_drafts (dispatch_idempotency_key);

-- em_record_draft: creates or idempotently upserts an email draft.
-- dispatch_idempotency_key prevents duplicate drafts for the same logical send.
CREATE OR REPLACE FUNCTION topology.em_record_draft(
    p_dispatch_idempotency_key TEXT,
    p_recipient_ref            TEXT,
    p_subject_ref              TEXT,
    p_response_port_ref        TEXT DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE v_id UUID;
BEGIN
    IF p_dispatch_idempotency_key IS NULL OR btrim(p_dispatch_idempotency_key) = '' THEN
        RAISE EXCEPTION 'EMAIL_DISPATCH_IDEMPOTENCY_KEY_REQUIRED';
    END IF;
    INSERT INTO topology.email_drafts (
        dispatch_idempotency_key, recipient_ref, subject_ref, response_port_ref,
        delivery_status, evidence_json
    ) VALUES (
        p_dispatch_idempotency_key, p_recipient_ref, p_subject_ref, p_response_port_ref,
        'draft',
        jsonb_build_object('recipient_ref', p_recipient_ref, 'subject_ref', p_subject_ref)
    )
    ON CONFLICT (dispatch_idempotency_key) DO UPDATE
        SET recipient_ref = EXCLUDED.recipient_ref,
            subject_ref   = EXCLUDED.subject_ref,
            updated_at    = now()
    RETURNING email_draft_id INTO v_id;
    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION topology.em_record_draft IS
    'Records or idempotently upserts an email draft keyed by dispatch_idempotency_key. Called via execute_abstract_function manifest af40 / call_postgres_function primitive. recipient_ref/subject_ref are business content references; no SMTP credential/host/port stored.';

-- em_record_approval: records an explicit human approval/rejection for a draft.
-- approval_status='approved' is the precondition for any subsequent send.
CREATE OR REPLACE FUNCTION topology.em_record_approval(
    p_email_draft_id  UUID,
    p_approval_status TEXT,
    p_reviewed_by     TEXT DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE v_id UUID;
BEGIN
    IF p_email_draft_id IS NULL THEN
        RAISE EXCEPTION 'EMAIL_APPROVAL_DRAFT_ID_REQUIRED';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM topology.email_drafts WHERE email_draft_id = p_email_draft_id) THEN
        RAISE EXCEPTION 'EMAIL_APPROVAL_DRAFT_NOT_FOUND';
    END IF;
    INSERT INTO topology.email_approval_records (
        email_draft_id, approval_status, reviewed_by, reviewed_at, evidence_json
    ) VALUES (
        p_email_draft_id, p_approval_status, p_reviewed_by, now(),
        jsonb_build_object('recorded_by', 'email_bundle')
    )
    RETURNING approval_record_id INTO v_id;

    UPDATE topology.email_drafts
       SET approval_record_id = v_id,
           delivery_status    = CASE WHEN p_approval_status = 'approved' THEN 'approved' ELSE delivery_status END,
           updated_at         = now()
     WHERE email_draft_id = p_email_draft_id;
    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION topology.em_record_approval IS
    'Records explicit human approval/rejection for an email draft (af41 / call_postgres_function). Approval authority is UI/human explicit action only. Prohibited: AI autonomous approval, implicit approval.';

-- em_record_dispatch_initiated: approval gate + double-send guard before send_http.
-- Fail-closes (RAISE) when the draft is unapproved or already dispatched/delivered.
CREATE OR REPLACE FUNCTION topology.em_record_dispatch_initiated(
    p_email_draft_id UUID
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE
    v_status   TEXT;
    v_approved BOOLEAN;
BEGIN
    IF p_email_draft_id IS NULL THEN
        RAISE EXCEPTION 'EMAIL_DISPATCH_DRAFT_ID_REQUIRED';
    END IF;
    SELECT delivery_status INTO v_status
      FROM topology.email_drafts WHERE email_draft_id = p_email_draft_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'EMAIL_DISPATCH_DRAFT_NOT_FOUND';
    END IF;
    -- Approval gate: explicit human approval is required before any send.
    SELECT EXISTS (
        SELECT 1 FROM topology.email_approval_records
        WHERE email_draft_id = p_email_draft_id AND approval_status = 'approved'
    ) INTO v_approved;
    IF NOT v_approved THEN
        RAISE EXCEPTION 'EMAIL_DISPATCH_NOT_APPROVED';
    END IF;
    -- Idempotency: prevent double send for the same draft.
    IF v_status IN ('dispatch_initiated', 'delivered') THEN
        RAISE EXCEPTION 'EMAIL_DISPATCH_ALREADY_INITIATED';
    END IF;
    UPDATE topology.email_drafts
       SET delivery_status = 'dispatch_initiated', updated_at = now()
     WHERE email_draft_id = p_email_draft_id;
    RETURN p_email_draft_id;
END;
$$;

COMMENT ON FUNCTION topology.em_record_dispatch_initiated IS
    'Approval gate and double-send guard executed before send_http (af42 / call_postgres_function). Fail-closes when draft is unapproved (EMAIL_DISPATCH_NOT_APPROVED) or already dispatched (EMAIL_DISPATCH_ALREADY_INITIATED). No silent fallback.';

-- em_record_send_evidence: records explicit send_success / send_failure delivery evidence.
-- p_send_result is the runtime-derived call status ('sent' for 2xx, otherwise failed).
-- Failure is recorded as an explicit failure event — never a silent fallback or retry.
CREATE OR REPLACE FUNCTION topology.em_record_send_evidence(
    p_email_draft_id UUID,
    p_send_result    TEXT
) RETURNS UUID
LANGUAGE plpgsql AS $$
DECLARE
    v_id              UUID;
    v_event_type      TEXT;
    v_delivery_status TEXT;
BEGIN
    IF p_email_draft_id IS NULL THEN
        RAISE EXCEPTION 'EMAIL_SEND_EVIDENCE_DRAFT_ID_REQUIRED';
    END IF;
    IF p_send_result = 'sent' THEN
        v_event_type      := 'send_success';
        v_delivery_status := 'delivered';
    ELSE
        v_event_type      := 'send_failure';
        v_delivery_status := 'failed';
    END IF;
    INSERT INTO topology.email_delivery_evidence (
        email_draft_id, event_type, delivery_status, evidence_json
    ) VALUES (
        p_email_draft_id, v_event_type, v_delivery_status,
        jsonb_build_object('recorded_by', 'email_bundle')
    )
    RETURNING delivery_evidence_id INTO v_id;

    UPDATE topology.email_drafts
       SET delivery_status = v_delivery_status, updated_at = now()
     WHERE email_draft_id = p_email_draft_id;
    RETURN v_id;
END;
$$;

COMMENT ON FUNCTION topology.em_record_send_evidence IS
    'Records explicit send_success/send_failure delivery evidence after capture_response (af43 / call_postgres_function). send_failure is an explicit failure record; no silent fallback or silent retry. Prohibited: raw provider response in evidence_json.';


-- =============================================================================
-- Scheduler job manifest substrate tables
-- Canonical DB surface for scheduler_job_manifest_substrate bundle.
-- schedule_policy_kind owns cron/interval/due-column/manual-only logic;
-- trigger_kind is cron|hook|client per runtime-orchestration-ssot.
-- Table/column/output authority comes from manifest seed, never from payload.
-- Credential plaintext never stored here; only reference keys are allowed.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- topology.scheduler_jobs
-- Scheduler job manifest header and authority scope.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.scheduler_jobs (
    scheduler_job_id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_key                       TEXT        NOT NULL UNIQUE,
    trigger_kind                  TEXT        NOT NULL DEFAULT 'cron',
    schedule_policy_kind          TEXT        NOT NULL DEFAULT 'manual_only',
    cron_expression               TEXT,
    schedule_interval_seconds     BIGINT,
    timezone                      TEXT        NOT NULL DEFAULT 'UTC',
    manual_run_allowed            BOOLEAN     NOT NULL DEFAULT false,
    active                        BOOLEAN     NOT NULL DEFAULT true,
    input_table_ref               TEXT,
    input_id_column               TEXT        NOT NULL DEFAULT 'id',
    input_status_column           TEXT,
    input_status_pending_value    TEXT,
    input_status_processing_value TEXT,
    input_status_completed_value  TEXT,
    input_status_failed_value     TEXT,
    input_status_skipped_value    TEXT,
    input_status_retry_wait_value TEXT,
    input_due_column              TEXT,
    output_table_ref              TEXT,
    max_batch_size                INTEGER     NOT NULL DEFAULT 10,
    lease_seconds                 INTEGER     NOT NULL DEFAULT 300,
    retry_policy                  JSONB       NOT NULL DEFAULT '{"max_attempts":3,"backoff_seconds":60}',
    authority_scope               TEXT        NOT NULL,
    credential_requirement_ref    TEXT,
    external_port_ref             TEXT,
    projection_policy             JSONB       NOT NULL DEFAULT '{}',
    created_by                    TEXT        NOT NULL DEFAULT 'seed',
    created_at                    TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                    TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE topology.scheduler_jobs IS
    'Scheduler job manifest header. authority_scope, input_table_ref, output_table_ref, '
    'and status values are seed/admin-authored manifest authority — not payload-derived. '
    'trigger_kind is cron|hook|client per runtime-orchestration-ssot. '
    'schedule_policy_kind owns due-selection policy (cron_expression|interval_seconds|due_column|manual_only). '
    'credential_requirement_ref and external_port_ref are references only — no plaintext credentials stored.';


-- ---------------------------------------------------------------------------
-- topology.scheduler_job_steps
-- Ordered abstract function step chain for a scheduler job.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.scheduler_job_steps (
    scheduler_job_step_id UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    scheduler_job_id      UUID        NOT NULL REFERENCES topology.scheduler_jobs (scheduler_job_id) ON DELETE CASCADE,
    step_order            INTEGER     NOT NULL,
    abstract_function_key TEXT        NOT NULL,
    input_binding         JSONB       NOT NULL DEFAULT '{}',
    result_context_key    TEXT,
    result_binding        JSONB       NOT NULL DEFAULT '{}',
    on_error              TEXT        NOT NULL DEFAULT 'fail_run',
    authority_scope       TEXT,
    active                BOOLEAN     NOT NULL DEFAULT true,
    UNIQUE (scheduler_job_id, step_order)
);

COMMENT ON TABLE topology.scheduler_job_steps IS
    'Ordered abstract function step chain for a scheduler job. '
    'abstract_function_key resolves through AbstractFunctionExecutor — not a C# function name array. '
    'on_error is fail_run|retry|skip_input|mark_input_failed. '
    'input_binding is manifest-defined binding from scheduler_context, constant, or prior result_context.';


-- ---------------------------------------------------------------------------
-- topology.scheduler_job_runs
-- Run ledger and input lease status for each job execution.
-- run_log contains references, status, and sanitized result_context only.
-- Secret material, token body, and plaintext credentials must not be stored here.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.scheduler_job_runs (
    scheduler_job_run_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    scheduler_job_id      UUID        NOT NULL REFERENCES topology.scheduler_jobs (scheduler_job_id),
    job_key               TEXT        NOT NULL,
    trigger_kind          TEXT        NOT NULL DEFAULT 'cron',
    schedule_policy_kind  TEXT        NOT NULL DEFAULT 'manual_only',
    run_status            TEXT        NOT NULL DEFAULT 'queued',
    input_ref             TEXT,
    input_status_before   TEXT,
    input_status_after    TEXT,
    attempt_count         INTEGER     NOT NULL DEFAULT 1,
    lease_until           TIMESTAMPTZ,
    started_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at          TIMESTAMPTZ,
    last_error            JSONB,
    result_context        JSONB       NOT NULL DEFAULT '{}'
);

COMMENT ON TABLE topology.scheduler_job_runs IS
    'Run ledger for scheduler job executions. '
    'run_status: queued|processing|completed|failed|partially_failed|cancelled|lease_expired. '
    'result_context stores sanitized result allowed by projection_policy only. '
    'Secret material, token body, and plaintext credentials must never be stored in result_context.';


-- ---------------------------------------------------------------------------
-- topology.scheduler_job_run_steps
-- Optional per-step ledger for debuggable ordered execution.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.scheduler_job_run_steps (
    scheduler_job_run_step_id UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    scheduler_job_run_id      UUID        NOT NULL REFERENCES topology.scheduler_job_runs (scheduler_job_run_id) ON DELETE CASCADE,
    scheduler_job_step_id     UUID        NOT NULL REFERENCES topology.scheduler_job_steps (scheduler_job_step_id),
    step_order                INTEGER     NOT NULL,
    abstract_function_key     TEXT        NOT NULL,
    run_step_status           TEXT        NOT NULL DEFAULT 'queued',
    attempt_count             INTEGER     NOT NULL DEFAULT 0,
    started_at                TIMESTAMPTZ,
    completed_at              TIMESTAMPTZ,
    last_error                JSONB,
    result_context_key        TEXT,
    result_context_delta      JSONB       NOT NULL DEFAULT '{}'
);

COMMENT ON TABLE topology.scheduler_job_run_steps IS
    'Per-step run ledger for scheduler job execution. '
    'run_step_status: queued|running|succeeded|failed|skipped. '
    'result_context_delta stores sanitized delta allowed by projection_policy. '
    'Secret material must not appear in result_context_delta or last_error.';
