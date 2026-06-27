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
            ReadSet(reader.GetString(5)),
            ReadColumns(allowedColumnsJson),
            ReadSet(reader.GetString(7)),
            ReadSet(reader.GetString(8)),
            ReadMap(reader.GetString(9)),
            ReadSet(reader.GetString(10)),
            reader.GetBoolean(11),
            reader.IsDBNull(12) ? null : reader.GetInt32(12));
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(AuthorizedCliReaderQuery query, CancellationToken ct = default)
    {
        var tableSql = QuoteQualifiedIdentifier(query.Table);
        var columnSql = query.Columns.Select(QuoteIdentifier).ToArray();
        var where = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        var index = 0;

        foreach (var (filter, value) in query.Filters)
        {
            where.Add($"{QuoteIdentifier(filter)} = @p{index}");
            parameters.Add(new NpgsqlParameter($"p{index}", value));
            index++;
        }

        if (!string.IsNullOrWhiteSpace(query.Period))
        {
            // Period authority is resolved by the runtime. The repository keeps the value
            // as metadata for future snapshot windows and does not interpolate it into SQL.
        }

        if (!string.IsNullOrWhiteSpace(query.RowScope))
        {
            var parts = query.RowScope.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                throw new InvalidOperationException("CLI_READER_ROW_SCOPE_INVALID");
            where.Add($"{QuoteIdentifier(parts[0])} = @p{index}");
            parameters.Add(new NpgsqlParameter($"p{index}", parts[1]));
            index++;
        }

        var whereSql = where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where);
        var sql = query.Operation.ToLowerInvariant() switch
        {
            "aggregate" => $"SELECT COUNT(*)::bigint AS count FROM {tableSql}{whereSql}",
            "analyze" => $"SELECT COUNT(*)::bigint AS row_count FROM {tableSql}{whereSql}",
            "validate" => $"SELECT 1 AS valid FROM {tableSql}{whereSql} LIMIT 1",
            _ => $"SELECT {string.Join(", ", columnSql)} FROM {tableSql}{whereSql} LIMIT 100"
        };

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
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

    private static string QuoteQualifiedIdentifier(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2) throw new InvalidOperationException("CLI_READER_TABLE_IDENTIFIER_INVALID");
        return string.Join(".", parts.Select(QuoteIdentifier));
    }

    private static string QuoteIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new InvalidOperationException("CLI_READER_IDENTIFIER_INVALID");
        return $"\"{value}\"";
    }

    private static IReadOnlySet<string> ReadSet(string json) => JsonSerializer.Deserialize<string[]>(json)?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyDictionary<string, string> ReadMap(string json) => JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    private static IReadOnlyDictionary<string, IReadOnlySet<string>> ReadColumns(string json) => (JsonSerializer.Deserialize<Dictionary<string, string[]>>(json) ?? new()).ToDictionary(kvp => kvp.Key, kvp => (IReadOnlySet<string>)kvp.Value.ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
}
