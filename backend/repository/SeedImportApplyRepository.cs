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

    public async Task<bool> ApplyRuntimeDeclarationAsync(
        string target,
        string layer,
        string action,
        string runtimeDestination,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
SELECT gen_random_uuid(), NULL,
       ARRAY[
         jsonb_build_object('type','dispatcher_mapping','target',@target,'layer',@layer,'action',@action),
         jsonb_build_object('type','runtime_mapping','runtime_destination',@runtime_destination)
       ]::jsonb[],
       'active'
WHERE NOT EXISTS (
  SELECT 1
  FROM manifest m
  WHERE m.status = 'active'
    AND EXISTS (
      SELECT 1 FROM unnest(m.topology) t
      WHERE t->>'type' = 'dispatcher_mapping'
        AND t->>'target' = @target
        AND t->>'layer' = @layer
        AND t->>'action' = @action
    )
);
""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("target", target);
        cmd.Parameters.AddWithValue("layer", layer);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("runtime_destination", runtimeDestination);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }
}
