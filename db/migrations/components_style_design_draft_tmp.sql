-- Add _tmp draft column for selected canvas node design auto-save.
-- SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract.draft_persistence_model
ALTER TABLE topology.components_style_design
  ADD COLUMN IF NOT EXISTS design_draft_tmp_json jsonb;
