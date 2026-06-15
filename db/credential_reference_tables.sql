-- =============================================================================
-- credential_reference_tables.sql
-- Credential reference metadata and credential audit event log.
-- SSOT: docs/design/runtime-bundle-secret-credential-ssot.yaml
--
-- SECURITY POLICY (enforce at all times):
--   This file creates tables that store REFERENCE METADATA ONLY.
--   No actual credential values, API keys, secret keys, connection strings,
--   private keys, endpoint URLs, or credential hashes are ever stored here.
--   Credential actual values are managed exclusively via the runtime secret store
--   (environment variables or secret manager API), never in the database.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- topology.credential_references
-- Stores credential reference metadata for the secret_credential_bundle.
-- Reference-only: no real credential value, hash, or endpoint URL ever stored.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.credential_references (
    credential_reference_id  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    reference_key            TEXT        NOT NULL UNIQUE,
    provider_kind            TEXT        NOT NULL,
    status                   TEXT        NOT NULL DEFAULT 'registered'
                                         CHECK (status IN ('registered', 'active', 'rotation_pending', 'revoked')),
    validation_status        TEXT        NOT NULL DEFAULT 'unchecked'
                                         CHECK (validation_status IN ('valid', 'invalid', 'unchecked')),
    last_validated_at        TIMESTAMPTZ,
    last_rotated_at          TIMESTAMPTZ,
    registered_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE topology.credential_references IS
    'Credential reference metadata for secret_credential_bundle. '
    'Reference identifiers, provider kind, and lifecycle status ONLY. '
    'No credential values, API keys, secret keys, connection strings, '
    'private keys, endpoint URLs, or credential hashes are stored here.';

COMMENT ON COLUMN topology.credential_references.reference_key IS
    'Logical name identifying this credential in the secret store. '
    'This is a reference key, never the actual credential value.';

COMMENT ON COLUMN topology.credential_references.provider_kind IS
    'Secret store provider type: e.g. env_var, aws_secrets_manager. '
    'Determines which store adapter resolves this credential at runtime.';

COMMENT ON COLUMN topology.credential_references.status IS
    'Lifecycle status: registered (awaiting first validation), active, rotation_pending, revoked.';

COMMENT ON COLUMN topology.credential_references.validation_status IS
    'Last known validation outcome: valid, invalid, or unchecked (never validated).';

CREATE INDEX IF NOT EXISTS idx_credential_references_status
    ON topology.credential_references (status);

CREATE INDEX IF NOT EXISTS idx_credential_references_provider_kind
    ON topology.credential_references (provider_kind);

-- ---------------------------------------------------------------------------
-- logs.credential_audit_log
-- Audit log for credential lifecycle events.
-- Stores REFERENCE IDENTIFIERS and OUTCOME only — no credential values.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS logs.credential_audit_log (
    audit_log_id             UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type               TEXT        NOT NULL,
    credential_reference_id  UUID        NOT NULL
                                         REFERENCES topology.credential_references(credential_reference_id)
                                         ON DELETE CASCADE,
    reference_key            TEXT        NOT NULL,
    outcome                  TEXT        NOT NULL,
    failure_code             TEXT,
    occurred_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE logs.credential_audit_log IS
    'Credential lifecycle audit events. Contains credential_reference_id and '
    'reference_key (logical name) plus outcome only. '
    'No credential values, hashes, or secret material stored here per SSOT audit_log_boundary.';

COMMENT ON COLUMN logs.credential_audit_log.event_type IS
    'Lifecycle event: credential_registered, credential_validated, credential_validation_failed, '
    'credential_rotated, credential_rotation_failed.';

COMMENT ON COLUMN logs.credential_audit_log.reference_key IS
    'Logical reference key name for human-readable audit context. Not the credential value.';

COMMENT ON COLUMN logs.credential_audit_log.outcome IS
    'Result of the event: success or failure.';

COMMENT ON COLUMN logs.credential_audit_log.failure_code IS
    'Structured failure code when outcome=failure, for explicit error tracking. '
    'Never contains credential value content.';

CREATE INDEX IF NOT EXISTS idx_credential_audit_log_reference_id
    ON logs.credential_audit_log (credential_reference_id);

CREATE INDEX IF NOT EXISTS idx_credential_audit_log_event_occurred
    ON logs.credential_audit_log (event_type, occurred_at DESC);
