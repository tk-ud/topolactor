-- =============================================================================
-- team_markdown_tables.sql
-- Canonical: topology.team_markdown_* tables for Team Markdown Dashboard Saved View.
--
-- SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
-- SCHEMA AUTHORITY: docs/design/db-schema.yaml (topology namespace)
--
-- Tables added:
--   topology.team_markdown_template_registry  — reusable Markdown template header/body
--   topology.team_markdown_saved_view          — searchable team dashboard saved Markdown projection
--   topology.team_markdown_saved_view_event    — append-only saved view activity log
--
-- Authority invariants:
--   - saved Markdown view is a projection; physical table / jsonb records are canonical data authority
--   - completed_preset_seed_json is required (NOT NULL) — validation gate for refresh/rebind/clone/projection
--   - card_metadata_json is required (NOT NULL) — search card display data
--   - rendered Markdown body is NOT the runtime SSOT
--   - refresh uses completed_preset_seed binding_json, not Markdown body parsing
--
-- BOOTSTRAP POLICY: part of the canonical fresh bootstrap sequence in db/init.sql.
-- Applied via docker-entrypoint-initdb.d/00-init.sql on fresh compose volume.
-- IDEMPOTENT: all CREATE TABLE statements use IF NOT EXISTS.
-- PRECONDITION: topology schema must exist (created by db/schema.sql).
-- =============================================================================


-- ---------------------------------------------------------------------------
-- topology.team_markdown_template_registry
-- Reusable Markdown template header and body.
-- status: draft | active | archived
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.team_markdown_template_registry (
    template_id             UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    template_key            TEXT        NOT NULL,
    template_label          TEXT        NOT NULL,
    template_markdown       TEXT        NOT NULL DEFAULT '',
    placeholder_schema_json JSONB       NOT NULL DEFAULT '{}'::jsonb,
    status                  TEXT        NOT NULL DEFAULT 'active',
    created_by              UUID,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_team_markdown_template_key UNIQUE (template_key),
    CONSTRAINT ck_team_markdown_template_status
        CHECK (status IN ('draft', 'active', 'archived'))
);

-- ---------------------------------------------------------------------------
-- topology.team_markdown_saved_view
-- Searchable team dashboard saved Markdown projection.
-- completed_preset_seed_json is the required rehydration/refresh/clone gate.
-- card_metadata_json stores dashboard card display data.
-- status: active | archived
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.team_markdown_saved_view (
    saved_view_id               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id                 UUID        NOT NULL REFERENCES topology.team_markdown_template_registry (template_id),
    title                       TEXT        NOT NULL,
    source_table_ref            TEXT        NOT NULL,
    source_record_ref           TEXT        NOT NULL,
    binding_json                JSONB       NOT NULL DEFAULT '{}'::jsonb,
    completed_preset_seed_json  JSONB       NOT NULL,
    rendered_markdown           TEXT        NOT NULL DEFAULT '',
    user_adjustment_patch_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    search_index_text           TEXT        NOT NULL DEFAULT '',
    card_metadata_json          JSONB       NOT NULL,
    status                      TEXT        NOT NULL DEFAULT 'active',
    created_by                  UUID,
    updated_by                  UUID,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_team_markdown_saved_view_status
        CHECK (status IN ('active', 'archived'))
);

-- ---------------------------------------------------------------------------
-- topology.team_markdown_saved_view_event
-- Append-only saved view activity log.
-- event_kind: create | update | refresh | archive | click_expand | copy | todo_candidate | clone | rebind
--           | update_confirmed_write | refresh_confirmed_write | clone_confirmed_write | rebind_confirmed_write
-- The *_confirmed_write kinds are the atomic mutation+evidence completion proof for the
-- preview -> validate -> explicit_confirm -> write -> event_log workflow (see
-- UpdateSavedViewWithDiffEvidenceAsync / UpdateSavedViewWithEventEvidenceAsync / CloneSavedViewWithEvidenceAsync).
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS topology.team_markdown_saved_view_event (
    event_id            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    saved_view_id       UUID        NOT NULL REFERENCES topology.team_markdown_saved_view (saved_view_id),
    event_kind          TEXT        NOT NULL,
    actor_id            UUID,
    event_payload_json  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_team_markdown_saved_view_event_kind
        CHECK (event_kind IN (
            'create', 'update', 'refresh', 'archive', 'click_expand', 'copy', 'todo_candidate', 'clone', 'rebind',
            'update_confirmed_write', 'refresh_confirmed_write', 'clone_confirmed_write', 'rebind_confirmed_write'
        ))
);

-- ---------------------------------------------------------------------------
-- Indexes
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_team_markdown_template_status
    ON topology.team_markdown_template_registry (status);
CREATE INDEX IF NOT EXISTS idx_team_markdown_template_key
    ON topology.team_markdown_template_registry (template_key);

CREATE INDEX IF NOT EXISTS idx_team_markdown_saved_view_status
    ON topology.team_markdown_saved_view (status);
CREATE INDEX IF NOT EXISTS idx_team_markdown_saved_view_template
    ON topology.team_markdown_saved_view (template_id);
CREATE INDEX IF NOT EXISTS idx_team_markdown_saved_view_source
    ON topology.team_markdown_saved_view (source_table_ref, source_record_ref);
CREATE INDEX IF NOT EXISTS idx_team_markdown_saved_view_updated
    ON topology.team_markdown_saved_view (updated_at DESC);

CREATE INDEX IF NOT EXISTS idx_team_markdown_saved_view_event_saved_view
    ON topology.team_markdown_saved_view_event (saved_view_id, created_at DESC);

-- ---------------------------------------------------------------------------
-- Preset catalog seed registration rows (existing preset ecosystem registry)
-- ---------------------------------------------------------------------------
INSERT INTO topology.team_markdown_template_registry
    (template_key, template_label, template_markdown, placeholder_schema_json, status)
VALUES
    (
        'md_viewer.team_markdown.saved_view.completed_seed.v1',
        'Team Markdown saved-view completed seed preset',
        '# {{title}}

**Owner:** {{owner}}

{{summary}}

{{optional_note}}',
        '{
          "preset_ecosystem_ref": "UIBuilder.preset_ecosystem",
          "component_key": "md_viewer.projection",
          "seed_registration_bundle": "preset_team_markdown_saved_view_seed",
          "placeholderKeys": ["title", "owner", "summary", "optional_note"],
          "requiredPlaceholderKeys": ["title", "owner", "summary"],
          "optionalPlaceholderKeys": ["optional_note"],
          "binding_resolution": "user_explicit_selection_only_no_ai_inference",
          "completed_preset_seed_json_required": true,
          "markdown_body_role": "template_seed_metadata_not_runtime_ssot"
        }'::jsonb,
        'active'
    )
ON CONFLICT (template_key) DO UPDATE
SET template_label = EXCLUDED.template_label,
    template_markdown = EXCLUDED.template_markdown,
    placeholder_schema_json = EXCLUDED.placeholder_schema_json,
    status = 'active',
    updated_at = now();

