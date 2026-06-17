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

CREATE TABLE IF NOT EXISTS topology.external_port_policy_steps (
    policy_step_id UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    policy_id      UUID        NOT NULL REFERENCES topology.external_port_policies (policy_id) ON DELETE CASCADE,
    step_order     INTEGER     NOT NULL CHECK (step_order > 0),
    operation_key  TEXT        NOT NULL CHECK (operation_key IN ('resolve_port_record','resolve_credential_reference','load_encrypted_credential_payload','decrypt_for_runtime_use','build_http_request','inject_authorization_header','send_http','capture_response','verify_signature_by_config','enqueue_scheduler_event','append_runtime_event_log','fail_close','acquire_refresh_lease','request_token_by_config','write_encrypted_credential_payload','update_token_hash','update_expires_at_and_version','release_refresh_lease','record_export_job','compute_checksum','record_file_artifact','write_manifest_record','authorize_signed_download')),
    step_config    JSONB       NOT NULL DEFAULT '{}'::jsonb,
    active         BOOLEAN     NOT NULL DEFAULT true,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (policy_id, step_order)
);

COMMENT ON TABLE topology.external_port_policies IS 'Seed-driven policy surface for external port generic primitive execution.';

-- Update operation_key CHECK constraint on existing databases to include file_storage domain keys.
ALTER TABLE topology.external_port_policy_steps
    DROP CONSTRAINT IF EXISTS external_port_policy_steps_operation_key_check;

ALTER TABLE topology.external_port_policy_steps
    ADD CONSTRAINT external_port_policy_steps_operation_key_check
    CHECK (operation_key IN (
        'resolve_port_record','resolve_credential_reference','load_encrypted_credential_payload',
        'decrypt_for_runtime_use','build_http_request','inject_authorization_header','send_http',
        'capture_response','verify_signature_by_config','enqueue_scheduler_event',
        'append_runtime_event_log','fail_close','acquire_refresh_lease','request_token_by_config',
        'write_encrypted_credential_payload','update_token_hash','update_expires_at_and_version',
        'release_refresh_lease',
        'record_export_job','compute_checksum','record_file_artifact',
        'write_manifest_record','authorize_signed_download'
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
COMMENT ON TABLE topology.external_port_policy_steps IS 'Ordered operation_key steps. operation_key values are constrained to external-port SSOT allowed values.';

-- ---------------------------------------------------------------------------
-- file_storage_bundle domain PostgreSQL functions.
-- Called via execute_db_function operation_key from ExternalPortPolicyStepExecutor.
-- These are abstract function boundary implementations; bundle-specific C# handler
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
    'Records or idempotently upserts a file_storage export job. Called via execute_db_function policy step.';

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
        export_job_id, file_name, file_type, storage_ref
    ) VALUES (
        p_export_job_id, p_file_name, p_file_type, p_storage_ref
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
    'Atomically inserts file_artifact and its checksum_record in a single transaction. Called via execute_db_function policy step.';

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
    'Writes or updates export package manifest record. Called via execute_db_function policy step.';

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
    'Creates a non-guessable authorization_key reference for signed download. authorization_key is opaque ref — not a signed URL. Called via execute_db_function policy step.';
