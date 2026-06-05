-- Add _tmp draft column for canvas auto-save state.
-- SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract.draft_persistence_model
ALTER TABLE topology.ui_topology_tensor
  ADD COLUMN IF NOT EXISTS layout_draft_tmp_json jsonb;
