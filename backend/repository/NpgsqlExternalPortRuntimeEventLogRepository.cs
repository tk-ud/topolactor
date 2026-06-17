using Npgsql;
using Topolactor.Runtime;

namespace Topolactor.Repository;

public sealed class NpgsqlExternalPortRuntimeEventLogRepository : IExternalPortRuntimeEventLogRepository
{
    private readonly string _connectionString;

    public NpgsqlExternalPortRuntimeEventLogRepository(string connectionString)
        => _connectionString = connectionString;

    public async Task AppendAsync(
        string eventType,
        string? entityId,
        string? requiredByBundle,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.runtime_event_log (event_type, entity_id, required_by_bundle)
            VALUES (@event_type, @entity_id, @required_by_bundle)
            """;
        cmd.Parameters.AddWithValue("event_type", eventType);
        cmd.Parameters.AddWithValue("entity_id", (object?)entityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("required_by_bundle", (object?)requiredByBundle ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
