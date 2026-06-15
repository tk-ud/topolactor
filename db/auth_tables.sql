-- =============================================================================
-- auth_tables.sql
-- Canonical auth identity, credential, session, and refresh token store.
-- SSOT: docs/design/auth-db-session-credential-ssot.yaml
-- Master roster state columns: docs/design/admin-master-roster-management-ssot.yaml
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS auth;

CREATE TABLE IF NOT EXISTS auth.users (
    user_id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    username          TEXT        NOT NULL UNIQUE,
    active            BOOLEAN     NOT NULL DEFAULT true,
    approve           BOOLEAN     NOT NULL DEFAULT false,
    status            TEXT,
    suspended_from    TIMESTAMPTZ,
    suspended_until   TIMESTAMPTZ,
    state_note        TEXT,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS auth.credentials (
    credential_id   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID        NOT NULL REFERENCES auth.users(user_id) ON DELETE CASCADE,
    password_hash   TEXT        NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_auth_credentials_user UNIQUE (user_id)
);

CREATE TABLE IF NOT EXISTS auth.sessions (
    session_id   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      UUID        NOT NULL REFERENCES auth.users(user_id) ON DELETE CASCADE,
    realm        TEXT        NOT NULL,
    audience     TEXT        NOT NULL,
    expires_at   TIMESTAMPTZ NOT NULL,
    revoked_at   TIMESTAMPTZ,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_auth_sessions_user_active
    ON auth.sessions (user_id)
    WHERE revoked_at IS NULL;

CREATE TABLE IF NOT EXISTS auth.refresh_tokens (
    refresh_token_id UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id       UUID        NOT NULL REFERENCES auth.sessions(session_id) ON DELETE CASCADE,
    token_hash       TEXT        NOT NULL,
    expires_at       TIMESTAMPTZ NOT NULL,
    revoked_at       TIMESTAMPTZ,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_auth_refresh_tokens_hash
    ON auth.refresh_tokens (token_hash)
    WHERE revoked_at IS NULL;

CREATE TABLE IF NOT EXISTS auth.login_events (
    login_event_id UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id        UUID        REFERENCES auth.users(user_id) ON DELETE SET NULL,
    realm          TEXT        NOT NULL,
    success        BOOLEAN     NOT NULL,
    failure_code   TEXT,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS auth.roles (
    role_name   TEXT        PRIMARY KEY,
    description TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS auth.scopes (
    scope_name  TEXT        PRIMARY KEY,
    description TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS auth.grants (
    grant_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID        NOT NULL REFERENCES auth.users(user_id) ON DELETE CASCADE,
    role_name   TEXT        NOT NULL REFERENCES auth.roles(role_name),
    realm       TEXT        NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_auth_grants_user_role_realm UNIQUE (user_id, role_name, realm)
);

COMMENT ON SCHEMA auth IS
    'Canonical auth store. Password hashes and refresh token hashes only — never topology/manifest.';
