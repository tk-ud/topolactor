-- auth_users_master_roster_state.sql
-- Idempotent migration for admin master roster user state columns.
-- SSOT: docs/design/admin-master-roster-management-ssot.yaml

ALTER TABLE auth.users ADD COLUMN IF NOT EXISTS approve BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE auth.users ADD COLUMN IF NOT EXISTS status TEXT;
ALTER TABLE auth.users ADD COLUMN IF NOT EXISTS suspended_from TIMESTAMPTZ;
ALTER TABLE auth.users ADD COLUMN IF NOT EXISTS suspended_until TIMESTAMPTZ;
ALTER TABLE auth.users ADD COLUMN IF NOT EXISTS state_note TEXT;
