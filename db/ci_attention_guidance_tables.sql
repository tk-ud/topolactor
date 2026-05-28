CREATE TABLE IF NOT EXISTS logs.ci_attention_guidance_current (
  fragment_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  source_kind TEXT NOT NULL CHECK (source_kind IN ('entity_diff','draft_diff','runtime_event','manual_check')),
  source_id TEXT NOT NULL,
  source_table_or_surface TEXT NOT NULL,
  target_kind TEXT NOT NULL CHECK (target_kind IN (
    'hub','registry','topology','schema','component','layout','wiring','manifest',
    'relation','package','workflow_action','entity','authoring_surface'
  )),
  target_key TEXT NOT NULL,
  diff_or_event_or_entity_id TEXT NULL,
  scope TEXT NOT NULL,
  route TEXT NULL,
  authoring_surface TEXT NOT NULL,
  kind TEXT NOT NULL CHECK (kind IN ('missing_input','valid_candidate','structural_violation','break_boundary')),
  status TEXT NOT NULL CHECK (status IN ('not_checked','active','resolved','dismissed')),
  severity TEXT NOT NULL CHECK (severity IN ('info','warning','error')),
  blocks_promotion BOOLEAN NOT NULL DEFAULT false,
  message TEXT NOT NULL,
  actionable_guidance TEXT NOT NULL,
  evidence_json JSONB NOT NULL DEFAULT '{}'::jsonb,
  checked_at TIMESTAMPTZ NULL,
  resolved_at TIMESTAMPTZ NULL,
  dismissed_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CONSTRAINT ci_attention_guidance_resolved_time_ck CHECK ((status <> 'resolved') OR resolved_at IS NOT NULL),
  CONSTRAINT ci_attention_guidance_dismissed_time_ck CHECK ((status <> 'dismissed') OR dismissed_at IS NOT NULL)
);

COMMENT ON TABLE logs.ci_attention_guidance_current IS
'Read-only guidance/evidence fragment current projection. Not lock authority; backend promotion gate may consume as eligibility input.';
COMMENT ON COLUMN logs.ci_attention_guidance_current.blocks_promotion IS
'Backend promotion gate input hint only. This column itself is not authority and does not grant frontend lock behavior.';

CREATE UNIQUE INDEX IF NOT EXISTS uq_ci_attention_guidance_current_identity
ON logs.ci_attention_guidance_current (source_kind, source_id, target_kind, target_key, authoring_surface, kind);

CREATE TABLE IF NOT EXISTS logs.ci_attention_guidance_history (
  history_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  fragment_id UUID NOT NULL REFERENCES logs.ci_attention_guidance_current(fragment_id) ON DELETE CASCADE,
  source_kind TEXT NOT NULL,
  source_id TEXT NOT NULL,
  source_table_or_surface TEXT NOT NULL,
  target_kind TEXT NOT NULL,
  target_key TEXT NOT NULL,
  diff_or_event_or_entity_id TEXT NULL,
  scope TEXT NOT NULL,
  route TEXT NULL,
  authoring_surface TEXT NOT NULL,
  kind TEXT NOT NULL,
  status TEXT NOT NULL,
  severity TEXT NOT NULL,
  blocks_promotion BOOLEAN NOT NULL,
  message TEXT NOT NULL,
  actionable_guidance TEXT NOT NULL,
  evidence_json JSONB NOT NULL DEFAULT '{}'::jsonb,
  checked_at TIMESTAMPTZ NULL,
  resolved_at TIMESTAMPTZ NULL,
  dismissed_at TIMESTAMPTZ NULL,
  snapshot_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
