-- Remove erroneous dispatcher_mapping from bundle manifest 092.
-- Manifest 07e is the sole dispatch route for assign_screen_data_shape.
-- Duplicate active manifests cause MANIFEST_AMBIGUOUS at Step 1 assign_screen_data_shape.

UPDATE manifest
SET topology = ARRAY(
  SELECT elem
  FROM unnest(topology) AS elem
  WHERE NOT (
    elem->>'type' = 'dispatcher_mapping'
    AND elem->>'role' = 'admin'
    AND elem->>'target' = 'admin'
    AND elem->>'layer' = 'manifest'
    AND elem->>'action' = 'assign_screen_data_shape'
  )
)::jsonb[],
updated_at = now()
WHERE manifest_id = '00000000-0000-0000-0000-000000000092'::uuid
  AND EXISTS (
    SELECT 1
    FROM unnest(topology) AS elem
    WHERE elem->>'type' = 'dispatcher_mapping'
      AND elem->>'layer' = 'manifest'
      AND elem->>'action' = 'assign_screen_data_shape'
  );
