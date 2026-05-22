using Npgsql;

namespace Topolactor.Repository;

/// <summary>
/// Canonical apply boundary for SeedRuntime import.
/// Registers/promotes seed runtime declarations into manifest as active wiring entries.
/// </summary>
public class SeedImportApplyRepository
{
    private readonly string _connectionString;

    public SeedImportApplyRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<SeedImportApplyResult> ApplyRuntimeDeclarationAsync(
        string target,
        string layer,
        string action,
        string runtimeDestination,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var normalizedTarget = target.Trim().ToLowerInvariant();
        var normalizedLayer = layer.Trim().ToLowerInvariant();
        var normalizedAction = action.Trim().ToLowerInvariant();

        const string existingSql = """
SELECT rt.runtime_destination
FROM manifest m
CROSS JOIN LATERAL (
    SELECT t->>'target' AS target, t->>'layer' AS layer, t->>'action' AS action
    FROM unnest(m.topology) t
    WHERE t->>'type' = 'dispatcher_mapping'
    LIMIT 1
) dm
CROSS JOIN LATERAL (
    SELECT t->>'runtime_destination' AS runtime_destination
    FROM unnest(m.topology) t
    WHERE t->>'type' = 'runtime_mapping'
    LIMIT 1
) rt
WHERE m.status = 'active'
  AND lower(dm.target) = @target
  AND lower(dm.layer) = @layer
  AND lower(dm.action) = @action
LIMIT 1;
""";

        await using var existingCmd = new NpgsqlCommand(existingSql, conn);
        existingCmd.Parameters.AddWithValue("target", normalizedTarget);
        existingCmd.Parameters.AddWithValue("layer", normalizedLayer);
        existingCmd.Parameters.AddWithValue("action", normalizedAction);
        var existing = await existingCmd.ExecuteScalarAsync(ct);

        if (existing is string existingDestination)
        {
            if (string.Equals(existingDestination, runtimeDestination, StringComparison.OrdinalIgnoreCase))
                return new SeedImportApplyResult(SeedImportApplyStatus.AlreadyApplied, null);

            return new SeedImportApplyResult(
                SeedImportApplyStatus.Conflict,
                $"Existing active mapping target={normalizedTarget} layer={normalizedLayer} action={normalizedAction} has runtime_destination={existingDestination}, requested={runtimeDestination}.");
        }

        const string insertSql = """
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES (
    gen_random_uuid(),
    NULL,
    ARRAY[
      jsonb_build_object('type','dispatcher_mapping','target',@target,'layer',@layer,'action',@action),
      jsonb_build_object('type','runtime_mapping','runtime_destination',@runtime_destination)
    ]::jsonb[],
    'active'
);
""";

        await using var cmd = new NpgsqlCommand(insertSql, conn);
        cmd.Parameters.AddWithValue("target", normalizedTarget);
        cmd.Parameters.AddWithValue("layer", normalizedLayer);
        cmd.Parameters.AddWithValue("action", normalizedAction);
        cmd.Parameters.AddWithValue("runtime_destination", runtimeDestination);

        try
        {
            var affected = await cmd.ExecuteNonQueryAsync(ct);
            if (affected > 0)
                return new SeedImportApplyResult(SeedImportApplyStatus.Inserted, null);

            return new SeedImportApplyResult(SeedImportApplyStatus.Failed, "Insert did not affect any row.");
        }
        catch (Exception ex)
        {
            return new SeedImportApplyResult(SeedImportApplyStatus.Failed, ex.Message);
        }
    }
}

public enum SeedImportApplyStatus
{
    Inserted,
    AlreadyApplied,
    Conflict,
    Failed
}

public record SeedImportApplyResult(SeedImportApplyStatus Status, string? Message);
