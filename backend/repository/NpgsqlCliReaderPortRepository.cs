using System.Text.Json;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public sealed class NpgsqlCliReaderPortRepository : CliReaderPortRepository
{
    private readonly string _connectionString;
    public NpgsqlCliReaderPortRepository(string connectionString) => _connectionString = string.IsNullOrWhiteSpace(connectionString) ? throw new ArgumentException("connectionString is required", nameof(connectionString)) : connectionString;

    public async Task<CliReaderPortConfig?> LoadPortAsync(string portKey, CancellationToken ct = default)
    {
        const string sql = """
            SELECT port_key, enabled, expires_at, allowed_roles, allowed_users, allowed_tables, allowed_columns,
                   allowed_filters, allowed_periods, row_scope, required_capabilities, audit_required, rate_limit_per_minute
            FROM topology.cli_reader_ports
            WHERE port_key = @port_key
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("port_key", portKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var allowedColumnsJson = reader.GetString(6);
        return new CliReaderPortConfig(
            reader.GetString(0),
            reader.GetBoolean(1),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
            ReadSet(reader.GetString(3)),
            ReadSet(reader.GetString(4)),
            ReadColumns(allowedColumnsJson),
            ReadSet(reader.GetString(7)),
            ReadSet(reader.GetString(8)),
            ReadMap(reader.GetString(9)),
            ReadSet(reader.GetString(10)),
            reader.GetBoolean(11),
            reader.IsDBNull(12) ? null : reader.GetInt32(12));
    }

    public Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(AuthorizedCliReaderQuery query, CancellationToken ct = default)
    {
        // The initial SubBundle provides the authorized read model boundary only.
        // Table data extraction remains repository-authorized and query-shaping only;
        // direct SQL/raw SQL from caller is never accepted.
        IReadOnlyList<Dictionary<string, object?>> rows =
        [
            query.Columns.ToDictionary(column => column, column => (object?)$"authorized:{query.Table}:{column}")
        ];
        return Task.FromResult(rows);
    }

    public async Task AppendRuntimeEventAsync(CliReaderPortRuntimeEvent runtimeEvent, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO topology.cli_reader_port_runtime_events
                (port_key, operation, user_id, roles, status, code, request_id, idempotency_key, scope_summary, observed_at)
            VALUES
                (@port_key, @operation, @user_id, @roles::jsonb, @status, @code, @request_id, @idempotency_key, @scope_summary, @observed_at)
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("port_key", runtimeEvent.PortKey);
        cmd.Parameters.AddWithValue("operation", runtimeEvent.Operation);
        cmd.Parameters.AddWithValue("user_id", (object?)runtimeEvent.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("roles", JsonSerializer.Serialize(runtimeEvent.Roles));
        cmd.Parameters.AddWithValue("status", runtimeEvent.Status);
        cmd.Parameters.AddWithValue("code", runtimeEvent.Code);
        cmd.Parameters.AddWithValue("request_id", (object?)runtimeEvent.RequestId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("idempotency_key", (object?)runtimeEvent.IdempotencyKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("scope_summary", runtimeEvent.ScopeSummary);
        cmd.Parameters.AddWithValue("observed_at", runtimeEvent.ObservedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static IReadOnlySet<string> ReadSet(string json) => JsonSerializer.Deserialize<string[]>(json)?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyDictionary<string, string> ReadMap(string json) => JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    private static IReadOnlyDictionary<string, IReadOnlySet<string>> ReadColumns(string json) => (JsonSerializer.Deserialize<Dictionary<string, string[]>>(json) ?? new()).ToDictionary(kvp => kvp.Key, kvp => (IReadOnlySet<string>)kvp.Value.ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
}
