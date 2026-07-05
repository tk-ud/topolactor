-- Deprecate Step-1-incomplete contents drafts (contentsType stamped but no topologySystemName).
-- These shells accumulated when assign_screen_data_shape failed after create_new_topology_draft.

UPDATE manifest
SET status = 'deprecated',
    updated_at = now()
WHERE status = 'draft'
  AND EXISTS (
    SELECT 1
    FROM unnest(topology) AS elem
    WHERE elem->>'type' = 'screen_data_shape'
      AND elem->>'contentsType' = 'contents'
      AND COALESCE(elem->>'topologySystemName', '') = ''
  );
