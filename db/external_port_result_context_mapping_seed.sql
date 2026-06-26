-- =============================================================================
-- External-port abstract-function result → external_context mapping seed.
--
-- Declares, per abstract_function_step, which step result is mirrored into a
-- writable external_context property (topology.abstract_function_steps.external_context_key).
-- This replaces the former C# PascalCase write-side switch (ApplyResultMirror) with a
-- manifest-declared, authority-checked mapping: the runtime applies only these declared
-- keys and fail-closes on unknown / unauthorized / secret-bearing / type-mismatch writes.
-- external_context_key uses the snake_case external_context binding vocabulary and is
-- constrained by the abstract_function_steps CHECK constraint (fixed writable allowlist).
--
-- Applied after seed_empty.sql, the compat absorption seed, and all consumer preset seeds
-- so it covers every manifest step regardless of which file inserted it. Idempotent.
-- =============================================================================

-- The mirror target is derived 1:1 from the legacy result_context_key the step used to
-- mirror under, preserving prior behavior while making the mapping data-defined.
UPDATE topology.abstract_function_steps
SET external_context_key = CASE result_context_key
        WHEN 'ExportJobId'      THEN 'export_job_id'
        WHEN 'FileArtifactId'   THEN 'file_artifact_id'
        WHEN 'ChecksumValue'    THEN 'checksum_value'
        WHEN 'AuthorizationKey' THEN 'authorization_key'
        WHEN 'OutputProp'       THEN 'output_prop'
    END,
    updated_at = now()
WHERE result_context_key IN ('ExportJobId','FileArtifactId','ChecksumValue','AuthorizationKey','OutputProp')
  AND external_context_key IS DISTINCT FROM CASE result_context_key
        WHEN 'ExportJobId'      THEN 'export_job_id'
        WHEN 'FileArtifactId'   THEN 'file_artifact_id'
        WHEN 'ChecksumValue'    THEN 'checksum_value'
        WHEN 'AuthorizationKey' THEN 'authorization_key'
        WHEN 'OutputProp'       THEN 'output_prop'
    END;
