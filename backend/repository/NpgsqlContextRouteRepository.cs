using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of ContextRouteRepository.
/// Replaces all in-memory skeleton stubs with real SQL operations against context_route_tables.sql.
///
/// Canonical tables used:
///   context_session, context_event, context_token_registry,
///   context_event_vector_cache, context_prefix_vector_cache, context_transition_stats
///
/// Transition stats aggregation (near-realtime pipeline):
///   When AppendContextEventAsync is called, the immediately preceding event in the same
///   session is queried and the (prev_operation → current_operation) transition stat is updated.
///   prob01 = count_hits / SUM(count_hits) over the same (prev, role, user_id) scope.
///   count_events mirrors SUM(count_hits) so the column always equals the scope total.
///   Runs as a 2-step transaction (upsert edge hits, then recompute scope totals) inline on each event append.
///
/// Wiring: inject NpgsqlContextRouteRepository wherever ContextRouteRepository is required
/// in production DI. Tests continue to use in-memory stubs via virtual method overrides.
/// </summary>
public class NpgsqlContextRouteRepository : ContextRouteRepository
{
    private readonly ILogger<NpgsqlContextRouteRepository> _npgsqlLogger;

    public NpgsqlContextRouteRepository(
        ILogger<NpgsqlContextRouteRepository> logger,
        string connectionString)
        : base(NullLogger<ContextRouteRepository>.Instance, connectionString)
    {
        _npgsqlLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ---------------------------------------------------------------------------
    // context_token_registry
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Loads active token records for the given token IDs from context_token_registry.
    /// Returns only status='active' rows. Missing tokens are treated as 0 in vectors.
    /// </summary>
    public override async Task<IReadOnlyList<ContextTokenRecord>> LoadActiveTokensAsync(
        IEnumerable<Guid> tokenIds,
        CancellationToken ct = default)
    {
        var ids = tokenIds.ToList();
        if (ids.Count == 0) return [];

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT token_id, label, \"group\", value, status " +
            "FROM context_token_registry " +
            "WHERE token_id = ANY(@ids) AND status = 'active'";
        cmd.Parameters.Add(
            new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            { Value = ids.ToArray() });

        var records = new List<ContextTokenRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            records.Add(new ContextTokenRecord(
                TokenId: reader.GetGuid(0),
                Label:   reader.GetString(1),
                Group:   reader.IsDBNull(2) ? null : reader.GetString(2),
                Value:   (float)reader.GetDouble(3),
                Status:  reader.GetString(4)
            ));
        }

        return records;
    }

    // ---------------------------------------------------------------------------
    // context_event
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Appends a context event to context_event (append-only).
    /// Also upserts context_session and runs the near-realtime transition stats update.
    /// </summary>
    public override async Task AppendContextEventAsync(
        ContextEventRecord ev, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ev);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Upsert session (create on first event, update last_seen_at on subsequent events)
        await using (var sessionCmd = conn.CreateCommand())
        {
            sessionCmd.CommandText =
                "INSERT INTO context_session (session_id, user_id, role, last_seen_at) " +
                "VALUES (@sessionId, @userId, @role, now()) " +
                "ON CONFLICT (session_id) DO UPDATE SET last_seen_at = now()";
            sessionCmd.Parameters.AddWithValue("sessionId", ev.SessionId);
            sessionCmd.Parameters.AddWithValue("userId",
                ev.UserId is not null ? (object)ev.UserId : DBNull.Value);
            sessionCmd.Parameters.AddWithValue("role",
                ev.Role is not null ? (object)ev.Role : DBNull.Value);
            await sessionCmd.ExecuteNonQueryAsync(ct);
        }

        // Append the event
        await using (var eventCmd = conn.CreateCommand())
        {
            eventCmd.CommandText =
                "INSERT INTO context_event " +
                "(event_id, session_id, user_id, role, table_name, record_id, operation, token_ids, " +
                " created_at, next_operation_hint, next_token_ids_hint) " +
                "VALUES (@eventId, @sessionId, @userId, @role, @tableName, @recordId, @operation, @tokenIds, " +
                "        @createdAt, @nextOp, @nextTokenIds)";

            eventCmd.Parameters.AddWithValue("eventId", ev.EventId);
            eventCmd.Parameters.AddWithValue("sessionId", ev.SessionId);
            eventCmd.Parameters.AddWithValue("userId",
                ev.UserId is not null ? (object)ev.UserId : DBNull.Value);
            eventCmd.Parameters.AddWithValue("role",
                ev.Role is not null ? (object)ev.Role : DBNull.Value);
            eventCmd.Parameters.AddWithValue("tableName",
                ev.TableName is not null ? (object)ev.TableName : DBNull.Value);
            eventCmd.Parameters.AddWithValue("recordId",
                ev.RecordId is not null ? (object)ev.RecordId : DBNull.Value);
            eventCmd.Parameters.AddWithValue("operation", ev.Operation);
            eventCmd.Parameters.Add(
                new NpgsqlParameter("tokenIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                { Value = ev.TokenIds.ToArray() });
            eventCmd.Parameters.AddWithValue("createdAt", ev.CreatedAt);
            eventCmd.Parameters.AddWithValue("nextOp",
                ev.NextOperationHint is not null ? (object)ev.NextOperationHint : DBNull.Value);
            if (ev.NextTokenIdsHint is not null)
                eventCmd.Parameters.Add(
                    new NpgsqlParameter("nextTokenIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                    { Value = ev.NextTokenIdsHint.ToArray() });
            else
                eventCmd.Parameters.AddWithValue("nextTokenIds", DBNull.Value);

            await eventCmd.ExecuteNonQueryAsync(ct);
        }

        // Near-realtime transition stats aggregation:
        // Find the preceding event in this session and upsert (prev_op → current_op) stat.
        await UpsertTransitionStatAsync(conn, ev, ct);
    }

    // ---------------------------------------------------------------------------
    // context_prefix_vector_cache
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Loads recent prefix vector cache entries for nearest-prefix cosine search.
    /// Joins with the following event to populate NextOperation and NextTokenIdsHint.
    /// </summary>
    public override async Task<IReadOnlyList<ContextPrefixVectorRecord>> LoadRecentPrefixVectorsAsync(
        string? tableName,
        string? role,
        int? maxDays,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();

        // Left-join with the event following last_event_id in the same session
        // to get next_operation and next_token_ids_hint without a second round-trip.
        cmd.CommandText =
            "SELECT cpvc.session_id, cpvc.prefix_index, cpvc.last_event_id, " +
            "       cpvc.vector_sparse::text, cpvc.l2_norm, cpvc.updated_at, " +
            "       next_ce.operation, next_ce.token_ids " +
            "FROM context_prefix_vector_cache cpvc " +
            "JOIN context_session cs ON cs.session_id = cpvc.session_id " +
            "LEFT JOIN LATERAL (" +
            "    SELECT ce2.operation, ce2.token_ids " +
            "    FROM context_event ce2 " +
            "    JOIN context_event last_ce ON last_ce.event_id = cpvc.last_event_id " +
            "    WHERE ce2.session_id = cpvc.session_id " +
            "      AND ce2.created_at > last_ce.created_at " +
            "    ORDER BY ce2.created_at ASC " +
            "    LIMIT 1 " +
            ") next_ce ON true " +
            "WHERE (@days::int IS NULL OR cpvc.updated_at >= now() - (@days * interval '1 day')) " +
            "  AND (@role::text IS NULL OR cs.role = @role) " +
            "  AND (@tableName::text IS NULL OR EXISTS (" +
            "        SELECT 1 FROM context_event ce3 " +
            "        WHERE ce3.event_id = cpvc.last_event_id AND ce3.table_name = @tableName" +
            "      )) " +
            "ORDER BY cpvc.updated_at DESC " +
            "LIMIT 2000";

        cmd.Parameters.Add(new NpgsqlParameter("days", System.Data.DbType.Int32)
        {
            Value = maxDays.HasValue ? (object)maxDays.Value : DBNull.Value
        });
        cmd.Parameters.AddWithValue("role", role is not null ? (object)role : DBNull.Value);
        cmd.Parameters.AddWithValue("tableName",
            tableName is not null ? (object)tableName : DBNull.Value);

        var records = new List<ContextPrefixVectorRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sparseJson = reader.GetString(3);
            var sparse = DeserializeSparseVector(sparseJson);

            Guid[]? nextTokenIds = null;
            if (!reader.IsDBNull(7))
                nextTokenIds = reader.GetValue(7) as Guid[];

            records.Add(new ContextPrefixVectorRecord(
                SessionId:          reader.GetGuid(0),
                PrefixIndex:        reader.GetInt32(1),
                LastEventId:        reader.GetGuid(2),
                SparseVector:       sparse,
                L2Norm:             (float)reader.GetDouble(4),
                UpdatedAt:          reader.GetDateTime(5),
                NextOperation:      reader.IsDBNull(6) ? null : reader.GetString(6),
                NextTokenIdsHint:   nextTokenIds?.ToList()
            ));
        }

        return records;
    }

    // ---------------------------------------------------------------------------
    // context_transition_stats
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Loads transition statistics for a given previous operation, ordered by prob01 desc.
    /// Fallback hierarchy: user_id → role → GLOBAL (all returned; resolver may filter).
    /// </summary>
    public override async Task<IReadOnlyList<ContextTransitionStat>> GetTransitionStatsAsync(
        string prevOperation,
        string? role,
        int candidateLimit,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT prev_operation, next_operation, count_events, count_hits, prob01 " +
            "FROM context_transition_stats " +
            "WHERE prev_operation = @prev " +
            "  AND (role = @role OR role = 'GLOBAL') " +
            "ORDER BY prob01 DESC " +
            "LIMIT @candidateLimit";
        cmd.Parameters.AddWithValue("prev", prevOperation);
        cmd.Parameters.AddWithValue("role", role ?? "GLOBAL");
        cmd.Parameters.AddWithValue("candidateLimit", candidateLimit);

        var stats = new List<ContextTransitionStat>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            stats.Add(new ContextTransitionStat(
                PrevOperation: reader.GetString(0),
                NextOperation: reader.GetString(1),
                CountEvents:   reader.GetInt32(2),
                CountHits:     (float)reader.GetDouble(3),
                Prob01:        (float)reader.GetDouble(4)
            ));
        }

        return stats;
    }

    /// <summary>
    /// Computes windowed transition stats directly from context_event raw rows.
    /// Uses aggregation_limit (count window) and optional recent_days (date window).
    /// For each source event, finds the next event in the same session via LATERAL.
    /// </summary>
    public override async Task<IReadOnlyList<ContextTransitionStat>> GetWindowedTransitionStatsAsync(
        string prevOperation,
        string? role,
        TransitionAggregationPolicy aggregationPolicy,
        int candidateLimit,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "WITH " +
            "  recent_events AS ( " +
            "    SELECT session_id, operation, created_at, " +
            "           COALESCE(role, 'GLOBAL') AS role, " +
            "           COALESCE(user_id, 'GLOBAL') AS user_id " +
            "    FROM context_event " +
            "    WHERE (@recentDays::int IS NULL OR created_at >= now() - (@recentDays * interval '1 day')) " +
            (aggregationPolicy.PreferRecent ? "    ORDER BY created_at DESC " : "    ORDER BY created_at ASC ") +
            "    LIMIT @aggregationLimit " +
            "  ), " +
            "  transitions AS ( " +
            "    SELECT " +
            "      re.operation   AS prev_op, " +
            "      re.role        AS role, " +
            "      re.user_id     AS user_id, " +
            "      next_e.operation AS next_op, " +
            "      COUNT(*)       AS hit_count " +
            "    FROM recent_events re " +
            "    JOIN LATERAL ( " +
            "      SELECT ce2.operation " +
            "      FROM context_event ce2 " +
            "      WHERE ce2.session_id = re.session_id " +
            "        AND ce2.created_at > re.created_at " +
            "      ORDER BY ce2.created_at ASC " +
            "      LIMIT 1 " +
            "    ) next_e ON TRUE " +
            "    GROUP BY re.operation, re.role, re.user_id, next_e.operation " +
            "  ), " +
            "  scope_totals AS ( " +
            "    SELECT prev_op, role, user_id, SUM(hit_count) AS total_hits " +
            "    FROM transitions " +
            "    GROUP BY prev_op, role, user_id " +
            "  ) " +
            "SELECT " +
            "  t.prev_op, " +
            "  t.next_op, " +
            "  t.hit_count::int    AS count_events, " +
            "  t.hit_count::float  AS count_hits, " +
            "  t.hit_count::float / NULLIF(st.total_hits, 0) AS prob01 " +
            "FROM transitions t " +
            "JOIN scope_totals st " +
            "  ON st.prev_op = t.prev_op AND st.role = t.role AND st.user_id = t.user_id " +
            "WHERE t.prev_op = @prevOp " +
            "  AND (t.role = @role OR t.role = 'GLOBAL') " +
            "ORDER BY prob01 DESC " +
            "LIMIT @candidateLimit";

        cmd.Parameters.AddWithValue("prevOp", prevOperation);
        cmd.Parameters.AddWithValue("role", role ?? "GLOBAL");
        cmd.Parameters.AddWithValue("aggregationLimit", aggregationPolicy.AggregationLimit);
        cmd.Parameters.AddWithValue("candidateLimit", candidateLimit);
        cmd.Parameters.Add(new NpgsqlParameter("recentDays", System.Data.DbType.Int32)
        {
            Value = aggregationPolicy.RecentDays.HasValue ? (object)aggregationPolicy.RecentDays.Value : DBNull.Value
        });

        var stats = new List<ContextTransitionStat>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            stats.Add(new ContextTransitionStat(
                PrevOperation: reader.GetString(0),
                NextOperation: reader.GetString(1),
                CountEvents:   reader.GetInt32(2),
                CountHits:     (float)reader.GetDouble(3),
                Prob01:        (float)reader.GetDouble(4)
            ));
        }

        return stats;
    }

    // ---------------------------------------------------------------------------
    // context_event_vector_cache
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Upserts a sparse event vector and l2_norm into context_event_vector_cache.
    /// vector_sparse is serialized as JSONB {token_id_string: float_value}.
    /// </summary>
    public override async Task UpsertEventVectorCacheAsync(
        ContextEventVector vec, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vec);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sparseJson = SerializeSparseVector(vec.SparseVector);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO context_event_vector_cache (event_id, vector_sparse, l2_norm, updated_at) " +
            "VALUES (@eventId, @sparse::jsonb, @norm, now()) " +
            "ON CONFLICT (event_id) DO UPDATE " +
            "  SET vector_sparse = EXCLUDED.vector_sparse, " +
            "      l2_norm = EXCLUDED.l2_norm, " +
            "      updated_at = now()";
        cmd.Parameters.AddWithValue("eventId", vec.EventId);
        cmd.Parameters.AddWithValue("sparse", sparseJson);
        cmd.Parameters.AddWithValue("norm", (double)vec.L2Norm);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------------------------------------------------------------------------
    // context_prefix_vector_cache
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Upserts a prefix vector cache entry into context_prefix_vector_cache.
    /// vector_sparse is the running SUM of event vectors up to prefix_index.
    /// </summary>
    public override async Task UpsertPrefixVectorCacheAsync(
        ContextPrefixVectorRecord vec, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vec);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sparseJson = SerializeSparseVector(vec.SparseVector);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO context_prefix_vector_cache " +
            "(session_id, prefix_index, last_event_id, vector_sparse, l2_norm, updated_at) " +
            "VALUES (@sessionId, @prefixIndex, @lastEventId, @sparse::jsonb, @norm, now()) " +
            "ON CONFLICT (session_id, prefix_index) DO UPDATE " +
            "  SET last_event_id = EXCLUDED.last_event_id, " +
            "      vector_sparse = EXCLUDED.vector_sparse, " +
            "      l2_norm = EXCLUDED.l2_norm, " +
            "      updated_at = now()";
        cmd.Parameters.AddWithValue("sessionId",   vec.SessionId);
        cmd.Parameters.AddWithValue("prefixIndex", vec.PrefixIndex);
        cmd.Parameters.AddWithValue("lastEventId", vec.LastEventId);
        cmd.Parameters.AddWithValue("sparse",      sparseJson);
        cmd.Parameters.AddWithValue("norm",        (double)vec.L2Norm);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------------------------------------------------------------------------
    // Near-realtime transition stats aggregation (called from AppendContextEventAsync)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Finds the preceding event in the same session and updates the (prev→current)
    /// transition stat as a true conditional proportion P(next | prev, role, user_id).
    ///
    /// Two-step transaction:
    ///   Step 1: Upsert (prev, next) edge — count_hits += 1.
    ///   Step 2: Recompute count_events = SUM(count_hits) and
    ///           prob01 = count_hits / SUM(count_hits) for ALL rows in the same (prev, role, user_id) scope.
    ///
    /// count_events mirrors SUM(count_hits) across the scope so that new edges get
    /// the correct denominator immediately on first insertion (avoids prob01=1.0 on new edges).
    /// </summary>
    private static async Task UpsertTransitionStatAsync(
        NpgsqlConnection conn,
        ContextEventRecord ev,
        CancellationToken ct)
    {
        // Find the preceding operation in this session
        string? prevOperation;
        await using (var prevCmd = conn.CreateCommand())
        {
            prevCmd.CommandText =
                "SELECT operation FROM context_event " +
                "WHERE session_id = @sessionId AND event_id != @eventId " +
                "ORDER BY created_at DESC " +
                "LIMIT 1";
            prevCmd.Parameters.AddWithValue("sessionId", ev.SessionId);
            prevCmd.Parameters.AddWithValue("eventId",   ev.EventId);
            var result = await prevCmd.ExecuteScalarAsync(ct);
            prevOperation = result is null or DBNull ? null : (string)result;
        }

        if (prevOperation is null)
            return; // First event in session — no transition to record.

        var role   = ev.Role   ?? "GLOBAL";
        var userId = ev.UserId ?? "GLOBAL";

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            // Step 1: Upsert (prev, next) edge — increment count_hits only.
            await using (var step1 = conn.CreateCommand())
            {
                step1.Transaction = tx;
                step1.CommandText =
                    "INSERT INTO context_transition_stats " +
                    "(prev_operation, next_operation, role, user_id, count_events, count_hits, prob01) " +
                    "VALUES (@prev, @next, @role, @userId, 0, 1.0, 0.0) " +
                    "ON CONFLICT (prev_operation, next_operation, role, user_id) DO UPDATE " +
                    "  SET count_hits = context_transition_stats.count_hits + 1, " +
                    "      updated_at = now()";
                step1.Parameters.AddWithValue("prev",   prevOperation);
                step1.Parameters.AddWithValue("next",   ev.Operation);
                step1.Parameters.AddWithValue("role",   role);
                step1.Parameters.AddWithValue("userId", userId);
                await step1.ExecuteNonQueryAsync(ct);
            }

            // Step 2: Recompute count_events = SUM(count_hits) and prob01 for ALL rows in scope.
            // Using a CTE to ensure the new edge's count_hits is included in the denominator,
            // which prevents prob01=1.0 on a newly inserted edge.
            await using (var step2 = conn.CreateCommand())
            {
                step2.Transaction = tx;
                step2.CommandText =
                    "WITH total AS ( " +
                    "  SELECT SUM(count_hits)::int AS total_hits " +
                    "  FROM context_transition_stats " +
                    "  WHERE prev_operation = @prev AND role = @role AND user_id = @userId " +
                    ") " +
                    "UPDATE context_transition_stats c " +
                    "SET count_events = total.total_hits, " +
                    "    prob01 = c.count_hits::float / NULLIF(total.total_hits, 0), " +
                    "    updated_at = now() " +
                    "FROM total " +
                    "WHERE c.prev_operation = @prev AND c.role = @role AND c.user_id = @userId";
                step2.Parameters.AddWithValue("prev",   prevOperation);
                step2.Parameters.AddWithValue("role",   role);
                step2.Parameters.AddWithValue("userId", userId);
                await step2.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ---------------------------------------------------------------------------
    // Serialization helpers
    // ---------------------------------------------------------------------------

    private static string SerializeSparseVector(IReadOnlyDictionary<Guid, float> vector)
    {
        var dict = vector.ToDictionary(
            kv => kv.Key.ToString(),
            kv => (double)kv.Value);
        return JsonSerializer.Serialize(dict);
    }

    private static IReadOnlyDictionary<Guid, float> DeserializeSparseVector(string json)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, double>>(json)
            ?? new Dictionary<string, double>();
        return dict
            .Where(kv => Guid.TryParse(kv.Key, out _))
            .ToDictionary(
                kv => Guid.Parse(kv.Key),
                kv => (float)kv.Value);
    }

    // ---------------------------------------------------------------------------
    // Retention — context_event cleanup
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Deletes context_event rows older than coldDays in a single batch of at most batchSize rows.
    /// Uses SKIP LOCKED to avoid contention with concurrent reads or appends.
    /// Returns the number of rows deleted.
    /// </summary>
    public override async Task<int> DeleteOldContextEventsAsync(
        int coldDays, int batchSize, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "DELETE FROM context_event " +
            "WHERE event_id IN (" +
            "  SELECT event_id FROM context_event " +
            "  WHERE created_at < NOW() - (@cold_days || ' days')::INTERVAL " +
            "  ORDER BY created_at ASC " +
            "  LIMIT @batch_size " +
            "  FOR UPDATE SKIP LOCKED" +
            ")";
        cmd.Parameters.AddWithValue("cold_days", coldDays);
        cmd.Parameters.AddWithValue("batch_size", batchSize);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        _npgsqlLogger.LogDebug(
            "NpgsqlContextRouteRepository.DeleteOldContextEventsAsync: deleted {Rows} row(s) older than {ColdDays} days.",
            rows, coldDays);
        return rows;
    }
}
