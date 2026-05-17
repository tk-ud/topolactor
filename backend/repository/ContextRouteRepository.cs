using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public class ContextRouteRepository
{
    private readonly ILogger<ContextRouteRepository> _logger;
    private readonly string _connectionString;

    public ContextRouteRepository(ILogger<ContextRouteRepository> logger, string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task AppendContextEventAsync(ContextEventRecord ev, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ev);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"""
            INSERT INTO context_event
            (event_id, session_id, user_id, role, table_name, record_id, operation, token_ids, created_at, next_operation_hint, next_token_ids_hint)
            VALUES
            (@eventId, @sessionId, @userId, @role, @tableName, @recordId, @operation, @tokenIds, @createdAt, @nextOperationHint, @nextTokenIdsHint);
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("eventId", ev.EventId);
        cmd.Parameters.AddWithValue("sessionId", ev.SessionId);
        cmd.Parameters.AddWithValue("userId", (object?)ev.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("role", (object?)ev.Role ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tableName", ev.TableName ?? string.Empty);
        cmd.Parameters.AddWithValue("recordId", (object?)ev.RecordId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("operation", ev.Operation);
        cmd.Parameters.AddWithValue("tokenIds", ev.TokenIds.ToArray());
        cmd.Parameters.AddWithValue("createdAt", ev.CreatedAt);
        cmd.Parameters.AddWithValue("nextOperationHint", (object?)ev.NextOperationHint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("nextTokenIdsHint", ev.NextTokenIdsHint is null ? DBNull.Value : ev.NextTokenIdsHint.ToArray());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public virtual async Task<IReadOnlyList<ContextTokenRecord>> LoadActiveTokensAsync(IEnumerable<Guid> tokenIds, CancellationToken ct = default)
    {
        var ids = tokenIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"""
            SELECT token_id, label, "group", value, status
            FROM context_token_registry
            WHERE status = 'active' AND token_id = ANY(@tokenIds);
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tokenIds", ids);
        var results = new List<ContextTokenRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ContextTokenRecord(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetFloat(3), reader.GetString(4)));
        }
        return results;
    }

    public virtual async Task<IReadOnlyList<ContextPrefixVectorRecord>> LoadRecentPrefixVectorsAsync(string? tableName, string? role, int maxDays, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"""
            SELECT p.session_id, p.prefix_index, p.last_event_id, p.vector_sparse::text, p.l2_norm, p.updated_at,
                   e2.operation, e2.next_token_ids_hint
            FROM context_prefix_vector_cache p
            JOIN context_event e ON e.event_id = p.last_event_id
            LEFT JOIN LATERAL (
                SELECT operation, next_token_ids_hint
                FROM context_event nx
                WHERE nx.session_id = p.session_id AND nx.created_at > e.created_at
                ORDER BY nx.created_at ASC
                LIMIT 1
            ) e2 ON true
            WHERE p.updated_at >= now() - (@maxDays || ' days')::interval
              AND (@tableName IS NULL OR e.table_name = @tableName)
              AND (@role IS NULL OR e.role = @role)
            ORDER BY p.updated_at DESC;
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("maxDays", maxDays);
        cmd.Parameters.AddWithValue("tableName", (object?)tableName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("role", (object?)role ?? DBNull.Value);
        var list = new List<ContextPrefixVectorRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sparse = JsonSerializer.Deserialize<Dictionary<Guid, float>>(reader.GetString(3)) ?? new();
            var hints = reader.IsDBNull(7) ? null : (IReadOnlyList<Guid>)reader.GetFieldValue<Guid[]>(7);
            list.Add(new ContextPrefixVectorRecord(reader.GetGuid(0), reader.GetInt32(1), reader.GetGuid(2), sparse, reader.GetFloat(4), reader.GetFieldValue<DateTimeOffset>(5), reader.IsDBNull(6)?null:reader.GetString(6), hints));
        }
        return list;
    }

    public async Task<IReadOnlyList<ContextTransitionStat>> GetTransitionStatsAsync(string prevOperation, string? role, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"""
            SELECT prev_operation, next_operation, count_events, count_hits, prob01
            FROM context_transition_stats
            WHERE prev_operation = @prevOperation
              AND (role = COALESCE(@role, 'GLOBAL') OR role = 'GLOBAL')
            ORDER BY role = COALESCE(@role, 'GLOBAL') DESC, prob01 DESC;
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("prevOperation", prevOperation);
        cmd.Parameters.AddWithValue("role", (object?)role ?? DBNull.Value);
        var stats = new List<ContextTransitionStat>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            stats.Add(new ContextTransitionStat(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetFloat(3), reader.GetFloat(4)));
        return stats;
    }

    public async Task UpsertEventVectorCacheAsync(ContextEventVector vec, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vec);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"""
            INSERT INTO context_event_vector_cache (event_id, vector_sparse, l2_norm, updated_at)
            VALUES (@eventId, @vectorSparse::jsonb, @l2Norm, now())
            ON CONFLICT (event_id)
            DO UPDATE SET vector_sparse = EXCLUDED.vector_sparse, l2_norm = EXCLUDED.l2_norm, updated_at = now();
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("eventId", vec.EventId);
        cmd.Parameters.AddWithValue("vectorSparse", JsonSerializer.Serialize(vec.SparseVector));
        cmd.Parameters.AddWithValue("l2Norm", vec.L2Norm);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertPrefixVectorCacheAsync(ContextPrefixVectorRecord vec, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vec);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"""
            INSERT INTO context_prefix_vector_cache (session_id, prefix_index, last_event_id, vector_sparse, l2_norm, created_at, updated_at)
            VALUES (@sessionId, @prefixIndex, @lastEventId, @vectorSparse::jsonb, @l2Norm, now(), now())
            ON CONFLICT (session_id, prefix_index)
            DO UPDATE SET last_event_id = EXCLUDED.last_event_id, vector_sparse = EXCLUDED.vector_sparse, l2_norm = EXCLUDED.l2_norm, updated_at = now();
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("sessionId", vec.SessionId);
        cmd.Parameters.AddWithValue("prefixIndex", vec.PrefixIndex);
        cmd.Parameters.AddWithValue("lastEventId", vec.LastEventId);
        cmd.Parameters.AddWithValue("vectorSparse", JsonSerializer.Serialize(vec.SparseVector));
        cmd.Parameters.AddWithValue("l2Norm", vec.L2Norm);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
