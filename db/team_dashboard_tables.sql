-- =============================================================================
-- team_dashboard_tables.sql
-- Canonical: topology.team_dashboard_note — the single shared Markdown/text note
-- backing the team-dashboard subBundle (admin-surface-topology-seed-conversion).
--
-- SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
--   surface_axes.admin.surfaces.team_dashboard / surface_axes.normal.surfaces.dashboard
-- SCHEMA AUTHORITY: docs/design/db-schema.yaml (topology namespace)
--
-- Design note: this is deliberately a lightweight, single-row physical table --
-- NOT topology.team_markdown_saved_view (that table backs the separate, heavier
-- Team Markdown Dashboard Saved View feature: templates, per-record bindings,
-- versioned completed_preset_seed_json lineage, card metadata, search index).
-- team-dashboard's canonical source is a plain Markdown-shaped text column on an
-- ordinary physical table, read/written through the SAME generic physical-table
-- registration (topology.physical_tables / topology.physical_table_manifest_bindings)
-- every other physical table in this schema uses -- no bespoke file/storage
-- mechanism. Reachable via the thin team_dashboard:get / team_dashboard:update
-- admin_runtime actions (backend/runtime/AdminRuntime.TeamDashboard.cs), the same
-- per-domain thin-CRUD-over-physical-table shape team_markdown/enum_dictionary/
-- credential_management already use -- not a new runtime lane.
--
-- BOOTSTRAP POLICY: part of the canonical fresh bootstrap sequence in db/init.sql.
-- Applied via docker-entrypoint-initdb.d/00-init.sql on fresh compose volume.
-- IDEMPOTENT: all CREATE TABLE statements use IF NOT EXISTS.
-- PRECONDITION: topology schema must exist (created by db/schema.sql).
-- =============================================================================

CREATE TABLE IF NOT EXISTS topology.team_dashboard_note (
    note_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    title         TEXT        NOT NULL DEFAULT 'Team Dashboard',
    body_markdown TEXT        NOT NULL DEFAULT '',
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMENT ON TABLE topology.team_dashboard_note IS
    'Single shared Markdown/text note backing the team-dashboard shared projection. '
    'Canonical data authority for team-dashboard -- read by both the admin (edit) and '
    'normal (read-only) surfaces via the same team_dashboard:get action.';
