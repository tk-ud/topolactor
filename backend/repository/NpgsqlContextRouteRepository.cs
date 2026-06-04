using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using Topolactor.Runtime;
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
    // context_token_registry — admin operations
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns all tokens from context_token_registry regardless of status.
    /// Ordered by created_at ASC so seed tokens appear first.
    /// </summary>
    public override async Task<IReadOnlyList<ContextTokenRecord>> ListAllContextTokensAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT token_id, label, \"group\", value, status " +
            "FROM context_token_registry " +
            "ORDER BY created_at ASC";

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

    /// <summary>
    /// Inserts a new token into context_token_registry with status='active'.
    /// Returns CreateTokenResult.Success with the new tokenId on success.
    /// Returns CreateTokenResult.Conflict when UNIQUE(label, "group") is violated (Postgres 23505).
    /// Other DB exceptions are rethrown.
    /// value must be in [-1.0, 1.0]; caller is responsible for range validation.
    /// </summary>
    public override async Task<CreateTokenResult> CreateContextTokenAsync(
        string label, string? group, float value, CancellationToken ct = default)
    {
        var tokenId = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO context_token_registry (token_id, label, \"group\", value, status) " +
            "VALUES (@tokenId, @label, @group, @value, 'active')";
        cmd.Parameters.AddWithValue("tokenId", tokenId);
        cmd.Parameters.AddWithValue("label",   label);
        cmd.Parameters.AddWithValue("group",   group is not null ? (object)group : DBNull.Value);
        cmd.Parameters.AddWithValue("value",   (double)value);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            return new CreateTokenResult(CreateTokenCode.Conflict, null);
        }

        return new CreateTokenResult(CreateTokenCode.Success, tokenId);
    }

    /// <summary>
    /// Sets context_token_registry status to 'deprecated' for the given tokenId.
    /// Idempotent: already-deprecated tokens are accepted.
    /// Returns true when the token was found, false when it does not exist.
    /// </summary>
    public override async Task<bool> DeprecateContextTokenAsync(
        Guid tokenId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE context_token_registry SET status = 'deprecated' " +
            "WHERE token_id = @tokenId";
        cmd.Parameters.AddWithValue("tokenId", tokenId);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    // ---------------------------------------------------------------------------
    // context_token_registry — runtime read
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


    public override async Task<bool> AppendComponentOperationEventLogAsync(
        ComponentOperationEventLogRecord ev, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ev);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO component_operation_event_log " +
            "(component_id, package_id, layout_id, wiring_id, event_type, payload, actor_or_source, occurred_at, idempotency_key) " +
            "VALUES (@componentId, @packageId, @layoutId, @wiringId, @eventType, @payload::jsonb, @actor, @occurredAt, @idempotencyKey) " +
            "ON CONFLICT (idempotency_key) DO NOTHING";

        cmd.Parameters.AddWithValue("componentId", ev.ComponentId);
        cmd.Parameters.AddWithValue("packageId", (object?)ev.PackageId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("layoutId", (object?)ev.LayoutId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("wiringId", (object?)ev.WiringId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("eventType", ev.EventType);
        cmd.Parameters.AddWithValue("payload", ev.PayloadJson);
        cmd.Parameters.AddWithValue("actor", ev.ActorOrSource);
        cmd.Parameters.AddWithValue("occurredAt", ev.OccurredAt);
        cmd.Parameters.AddWithValue("idempotencyKey", ev.IdempotencyKey);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
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
    /// If hotDays is specified, events within the hot window (created_at >= NOW() - hotDays) are excluded.
    /// FK-safe and cache-consistent: collects affected session_ids, drops all
    /// context_prefix_vector_cache rows for those sessions, then deletes
    /// context_event_vector_cache and context_event rows.
    ///
    /// Why full-session prefix cache invalidation is required:
    ///   prefix_vector = SUM(event_vectors[0..prefix_index]).
    ///   Deleting any event from a session makes ALL prefix cache rows for that session
    ///   stale (every cumulative SUM that included the deleted event is now wrong),
    ///   not only the row where last_event_id references the deleted event.
    ///   The cache is rebuildable from context_event, so dropping it is safe.
    ///
    /// FK constraints honoured:
    ///   context_event_vector_cache.event_id       → context_event.event_id
    ///   context_prefix_vector_cache.last_event_id → context_event.event_id
    ///
    /// SKIP LOCKED on the candidate selection avoids blocking concurrent event appends.
    /// Returns the number of context_event rows deleted.
    /// </summary>
    public override async Task<int> DeleteOldContextEventsAsync(
        int coldDays, int batchSize, int? hotDays = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var txn = await conn.BeginTransactionAsync(ct);

        try
        {
            // Step 1: collect event_ids and session_ids for the candidate batch (locked, skip if busy)
            await using var selectCmd = conn.CreateCommand();
            selectCmd.Transaction = txn;
            selectCmd.CommandText =
                "SELECT event_id, session_id FROM context_event " +
                "WHERE created_at < NOW() - (@cold_days || ' days')::INTERVAL " +
                "  AND (@hot_days::int IS NULL OR created_at < NOW() - (@hot_days || ' days')::INTERVAL) " +
                "ORDER BY created_at ASC " +
                "LIMIT @batch_size " +
                "FOR UPDATE SKIP LOCKED";
            selectCmd.Parameters.AddWithValue("cold_days", coldDays);
            selectCmd.Parameters.AddWithValue("batch_size", batchSize);
            selectCmd.Parameters.Add(new NpgsqlParameter("hot_days", System.Data.DbType.Int32)
            {
                Value = hotDays.HasValue ? (object)hotDays.Value : DBNull.Value
            });

            var eventIds = new List<Guid>();
            var sessionIds = new HashSet<Guid>();
            await using (var reader = await selectCmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    eventIds.Add(reader.GetGuid(0));
                    sessionIds.Add(reader.GetGuid(1));
                }
            }

            if (eventIds.Count == 0)
            {
                await txn.CommitAsync(ct);
                return 0;
            }

            // Step 2: drop ALL prefix cache rows for affected sessions.
            // prefix_vector = SUM(event_vectors[0..prefix_index]): deleting any event
            // from a session invalidates every prefix cache row that covered it, not only
            // the row where last_event_id points to a deleted event.
            await using var delPrefixCmd = conn.CreateCommand();
            delPrefixCmd.Transaction = txn;
            delPrefixCmd.CommandText =
                "DELETE FROM context_prefix_vector_cache WHERE session_id = ANY(@sessions)";
            delPrefixCmd.Parameters.AddWithValue("sessions", sessionIds.ToArray());
            await delPrefixCmd.ExecuteNonQueryAsync(ct);

            // Step 3: delete event vector cache rows
            await using var delVecCmd = conn.CreateCommand();
            delVecCmd.Transaction = txn;
            delVecCmd.CommandText =
                "DELETE FROM context_event_vector_cache WHERE event_id = ANY(@ids)";
            delVecCmd.Parameters.AddWithValue("ids", eventIds.ToArray());
            await delVecCmd.ExecuteNonQueryAsync(ct);

            // Step 4: delete the parent context_event rows
            await using var delEvtCmd = conn.CreateCommand();
            delEvtCmd.Transaction = txn;
            delEvtCmd.CommandText =
                "DELETE FROM context_event WHERE event_id = ANY(@ids)";
            delEvtCmd.Parameters.AddWithValue("ids", eventIds.ToArray());
            var rows = await delEvtCmd.ExecuteNonQueryAsync(ct);

            await txn.CommitAsync(ct);

            _npgsqlLogger.LogDebug(
                "NpgsqlContextRouteRepository.DeleteOldContextEventsAsync: deleted {Rows} context_event row(s) older than {ColdDays} days (hot_days={HotDays}).",
                rows, coldDays, hotDays);
            return rows;
        }
        catch
        {
            await txn.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Moves context_event rows older than coldDays to context_event_cold (cold storage).
    /// If hotDays is specified, events within the hot window (created_at >= NOW() - hotDays) are excluded.
    /// FK-safe and cache-consistent: collects affected session_ids, drops all
    /// context_prefix_vector_cache rows for those sessions, then deletes
    /// context_event_vector_cache and context_event rows after archiving.
    ///
    /// Archive order within a transaction:
    ///   1. Select candidates (FOR UPDATE SKIP LOCKED).
    ///   2. INSERT INTO context_event_cold (ON CONFLICT DO NOTHING for idempotency).
    ///   3. DROP prefix cache rows for affected sessions.
    ///   4. DELETE context_event_vector_cache rows.
    ///   5. DELETE context_event rows.
    ///
    /// context_event_cold has no FK to context_event so the insert may precede the delete.
    /// Returns the number of context_event rows archived and removed from the hot table.
    /// </summary>
    public override async Task<int> ArchiveOldContextEventsAsync(
        int coldDays, int batchSize, int? hotDays = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var txn = await conn.BeginTransactionAsync(ct);

        try
        {
            // Step 1: collect candidates (locked, skip if busy)
            await using var selectCmd = conn.CreateCommand();
            selectCmd.Transaction = txn;
            selectCmd.CommandText =
                "SELECT event_id, session_id, user_id, role, class, table_name, record_id, " +
                "       operation, token_ids, created_at, next_operation_hint, next_token_ids_hint " +
                "FROM context_event " +
                "WHERE created_at < NOW() - (@cold_days || ' days')::INTERVAL " +
                "  AND (@hot_days::int IS NULL OR created_at < NOW() - (@hot_days || ' days')::INTERVAL) " +
                "ORDER BY created_at ASC " +
                "LIMIT @batch_size " +
                "FOR UPDATE SKIP LOCKED";
            selectCmd.Parameters.AddWithValue("cold_days", coldDays);
            selectCmd.Parameters.AddWithValue("batch_size", batchSize);
            selectCmd.Parameters.Add(new NpgsqlParameter("hot_days", System.Data.DbType.Int32)
            {
                Value = hotDays.HasValue ? (object)hotDays.Value : DBNull.Value
            });

            var eventIds = new List<Guid>();
            var sessionIds = new HashSet<Guid>();
            var archiveRows = new List<(Guid EventId, Guid SessionId, object? UserId, object? Role,
                object? Class, string TableName, object? RecordId, string Operation,
                Guid[] TokenIds, DateTime CreatedAt, object? NextOpHint, object? NextTokenIdsHint)>();

            await using (var reader = await selectCmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var eventId   = reader.GetGuid(0);
                    var sessionId = reader.GetGuid(1);
                    eventIds.Add(eventId);
                    sessionIds.Add(sessionId);
                    archiveRows.Add((
                        EventId:            eventId,
                        SessionId:          sessionId,
                        UserId:             reader.IsDBNull(2)  ? DBNull.Value : reader.GetString(2),
                        Role:               reader.IsDBNull(3)  ? DBNull.Value : reader.GetString(3),
                        Class:              reader.IsDBNull(4)  ? DBNull.Value : reader.GetString(4),
                        TableName:          reader.GetString(5),
                        RecordId:           reader.IsDBNull(6)  ? DBNull.Value : reader.GetString(6),
                        Operation:          reader.GetString(7),
                        TokenIds:           reader.GetValue(8) as Guid[] ?? [],
                        CreatedAt:          reader.GetDateTime(9),
                        NextOpHint:         reader.IsDBNull(10) ? DBNull.Value : reader.GetString(10),
                        NextTokenIdsHint:   reader.IsDBNull(11) ? DBNull.Value : reader.GetValue(11)
                    ));
                }
            }

            if (eventIds.Count == 0)
            {
                await txn.CommitAsync(ct);
                return 0;
            }

            // Step 2: INSERT INTO context_event_cold (ON CONFLICT DO NOTHING for idempotency)
            foreach (var row in archiveRows)
            {
                await using var archCmd = conn.CreateCommand();
                archCmd.Transaction = txn;
                archCmd.CommandText =
                    "INSERT INTO context_event_cold " +
                    "(event_id, session_id, user_id, role, class, table_name, record_id, operation, " +
                    " token_ids, created_at, next_operation_hint, next_token_ids_hint) " +
                    "VALUES (@eventId, @sessionId, @userId, @role, @class, @tableName, @recordId, @operation, " +
                    "        @tokenIds, @createdAt, @nextOp, @nextTokenIds) " +
                    "ON CONFLICT (event_id) DO NOTHING";
                archCmd.Parameters.AddWithValue("eventId",    row.EventId);
                archCmd.Parameters.AddWithValue("sessionId",  row.SessionId);
                archCmd.Parameters.AddWithValue("userId",     row.UserId ?? DBNull.Value);
                archCmd.Parameters.AddWithValue("role",       row.Role ?? DBNull.Value);
                archCmd.Parameters.AddWithValue("class",      row.Class ?? DBNull.Value);
                archCmd.Parameters.AddWithValue("tableName",  row.TableName);
                archCmd.Parameters.AddWithValue("recordId",   row.RecordId ?? DBNull.Value);
                archCmd.Parameters.AddWithValue("operation",  row.Operation);
                archCmd.Parameters.Add(
                    new NpgsqlParameter("tokenIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                    { Value = row.TokenIds });
                archCmd.Parameters.AddWithValue("createdAt",      row.CreatedAt);
                archCmd.Parameters.AddWithValue("nextOp",         row.NextOpHint ?? DBNull.Value);
                archCmd.Parameters.AddWithValue("nextTokenIds",   row.NextTokenIdsHint ?? DBNull.Value);
                await archCmd.ExecuteNonQueryAsync(ct);
            }

            // Step 3: drop ALL prefix cache rows for affected sessions
            await using var delPrefixCmd = conn.CreateCommand();
            delPrefixCmd.Transaction = txn;
            delPrefixCmd.CommandText =
                "DELETE FROM context_prefix_vector_cache WHERE session_id = ANY(@sessions)";
            delPrefixCmd.Parameters.AddWithValue("sessions", sessionIds.ToArray());
            await delPrefixCmd.ExecuteNonQueryAsync(ct);

            // Step 4: delete event vector cache rows
            await using var delVecCmd = conn.CreateCommand();
            delVecCmd.Transaction = txn;
            delVecCmd.CommandText =
                "DELETE FROM context_event_vector_cache WHERE event_id = ANY(@ids)";
            delVecCmd.Parameters.AddWithValue("ids", eventIds.ToArray());
            await delVecCmd.ExecuteNonQueryAsync(ct);

            // Step 5: delete the parent context_event rows
            await using var delEvtCmd = conn.CreateCommand();
            delEvtCmd.Transaction = txn;
            delEvtCmd.CommandText =
                "DELETE FROM context_event WHERE event_id = ANY(@ids)";
            delEvtCmd.Parameters.AddWithValue("ids", eventIds.ToArray());
            var rows = await delEvtCmd.ExecuteNonQueryAsync(ct);

            await txn.CommitAsync(ct);

            _npgsqlLogger.LogDebug(
                "NpgsqlContextRouteRepository.ArchiveOldContextEventsAsync: archived {Rows} context_event row(s) older than {ColdDays} days to cold storage (hot_days={HotDays}).",
                rows, coldDays, hotDays);
            return rows;
        }
        catch
        {
            await txn.RollbackAsync(ct);
            throw;
        }
    }

    // ---------------------------------------------------------------------------
    // Topology Vector Runtime — Registry Vector Neighbors
    // ---------------------------------------------------------------------------

    private static readonly IReadOnlyDictionary<string, (string IdCol, string NameExpr, string ArrayCol)> _registryVectorTableMap =
        new Dictionary<string, (string, string, string)>
        {
            ["topology.relation_registry"] = ("relation_registry_id", "name",          "master_ids"),
            ["topology.entities"]          = ("entity_id",            "entity_id::text","relation_ids"),
            ["topology.structure_maps"]    = ("structure_map_id",      "name",          "component_ids"),
        };

    /// <summary>
    /// Fetches existing registry rows whose UUID array overlaps with queryIds (GIN filter),
    /// then computes multi-hot cosine similarity in-process.
    /// Returns rows with cosine >= minSimilarity ordered by cosine DESC, limited to topK.
    ///
    /// Throws InvalidOperationException for unknown registryTable — caller (TopologyVectorRuntime)
    /// catches and returns ExplicitError with IsBlocking:true.
    /// </summary>
    public override async Task<IReadOnlyList<RegistryVectorNeighbor>> FindRegistryVectorNeighborsAsync(
        string registryTable,
        IReadOnlyList<Guid> queryIds,
        float minSimilarity,
        int topK,
        CancellationToken ct = default)
    {
        if (!_registryVectorTableMap.TryGetValue(registryTable, out var cols))
            throw new InvalidOperationException(
                $"NpgsqlContextRouteRepository.FindRegistryVectorNeighborsAsync: unsupported registry table '{registryTable}'.");

        var (idCol, nameExpr, arrayCol) = cols;
        var queryArray = queryIds.ToArray();
        var querySet = new HashSet<Guid>(queryIds);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT {idCol}, {nameExpr}, {arrayCol} " +
            $"FROM {registryTable} " +
            $"WHERE {arrayCol} && @queryIds " +
            $"  AND cardinality({arrayCol}) > 0";
        cmd.Parameters.Add(new NpgsqlParameter("queryIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = queryArray
        });

        var candidates = new List<(Guid RegistryId, string Name, HashSet<Guid> Ids)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var registryId = reader.GetGuid(0);
                var name = reader.GetString(1);
                var ids = reader.GetValue(2) as Guid[] ?? [];
                candidates.Add((registryId, name, new HashSet<Guid>(ids)));
            }
        }

        var results = new List<RegistryVectorNeighbor>(candidates.Count);
        foreach (var (registryId, name, existingSet) in candidates)
        {
            var cosine = TopologyVectorRuntime.ComputeSparseCosineSimilarity(querySet, existingSet);
            if (cosine < minSimilarity)
                continue;
            var matchedIds = existingSet.Where(id => querySet.Contains(id)).ToList();
            results.Add(new RegistryVectorNeighbor(
                RegistryId: registryId,
                Name: name,
                CosineScore: cosine,
                MatchedIds: matchedIds,
                Reason: "cosine_neighbor"
            ));
        }

        results.Sort((a, b) => b.CosineScore.CompareTo(a.CosineScore));
        return results.Count <= topK ? results : results.Take(topK).ToList();
    }

    // ---------------------------------------------------------------------------
    // Topology Vector Runtime — Hub Attention Current
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Loads hub attention recommendation current rows for the given hub_id and scope_limit.
    /// Ordered by rank ASC NULLS LAST, attention_score DESC NULLS LAST.
    /// Returns empty list when no rows exist (cold start).
    /// </summary>
    public override async Task<IReadOnlyList<HubAttentionCurrentRecord>> LoadHubAttentionCurrentAsync(
        Guid hubId,
        int scopeLimit,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT hub_id, target_table, candidate_kind, candidate_id, scope_limit, " +
            "       base_probability, cosine_similarity, static_relation_weight, statistical_weight, " +
            "       mlp_feature_score, feedback_adjustment, " +
            "       ema_fast_30, ema_slow_10, trend, cross_state, " +
            "       attention_score, rank, evidence_json, mlp_feature_json, updated_at " +
            "FROM context_hub_recommendation_current " +
            "WHERE hub_id = @hubId AND scope_limit = @scopeLimit " +
            "ORDER BY rank ASC NULLS LAST, attention_score DESC NULLS LAST";
        cmd.Parameters.AddWithValue("hubId",      hubId);
        cmd.Parameters.AddWithValue("scopeLimit", scopeLimit);

        var records = new List<HubAttentionCurrentRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            records.Add(new HubAttentionCurrentRecord(
                HubId:                reader.GetGuid(0),
                TargetTable:          reader.GetString(1),
                CandidateKind:        reader.GetString(2),
                CandidateId:          reader.GetGuid(3),
                ScopeLimit:           reader.GetInt32(4),
                BaseProbability:      reader.IsDBNull(5)  ? null : (float)reader.GetDouble(5),
                CosineSimilarity:     reader.IsDBNull(6)  ? null : (float)reader.GetDouble(6),
                StaticRelationWeight: reader.IsDBNull(7)  ? null : (float)reader.GetDouble(7),
                StatisticalWeight:    reader.IsDBNull(8)  ? null : (float)reader.GetDouble(8),
                MlpFeatureScore:      reader.IsDBNull(9)  ? null : (float)reader.GetDouble(9),
                FeedbackAdjustment:   (float)reader.GetDouble(10),
                EmaFast:              reader.IsDBNull(11) ? null : (float)reader.GetDouble(11),
                EmaSlow:              reader.IsDBNull(12) ? null : (float)reader.GetDouble(12),
                Trend:                reader.IsDBNull(13) ? null : (float)reader.GetDouble(13),
                CrossState:           reader.IsDBNull(14) ? null : reader.GetString(14),
                AttentionScore:       reader.IsDBNull(15) ? null : (float)reader.GetDouble(15),
                Rank:                 reader.IsDBNull(16) ? null : reader.GetInt32(16),
                EvidenceJson:         reader.GetString(17),
                MlpFeatureJson:       reader.GetString(18),
                UpdatedAt:            reader.GetDateTime(19)
            ));
        }

        return records;
    }

    /// <summary>
    /// Upserts a hub attention current row into context_hub_recommendation_current.
    /// ON CONFLICT (hub_id, target_table, candidate_kind, candidate_id, scope_limit) DO UPDATE.
    /// All mutable fields are updated on conflict. updated_at is refreshed to now().
    /// </summary>
    public override async Task UpsertHubAttentionCurrentAsync(
        HubAttentionCurrentRecord record,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO context_hub_recommendation_current " +
            "(hub_id, target_table, candidate_kind, candidate_id, scope_limit, " +
            " base_probability, cosine_similarity, static_relation_weight, statistical_weight, " +
            " mlp_feature_score, feedback_adjustment, " +
            " ema_fast_30, ema_slow_10, trend, cross_state, " +
            " attention_score, rank, evidence_json, mlp_feature_json, updated_at) " +
            "VALUES " +
            "(@hubId, @targetTable, @candidateKind, @candidateId, @scopeLimit, " +
            " @baseProbability, @cosineSimilarity, @staticRelationWeight, @statisticalWeight, " +
            " @mlpFeatureScore, @feedbackAdjustment, " +
            " @emaFast, @emaSlow, @trend, @crossState, " +
            " @attentionScore, @rank, @evidenceJson::jsonb, @mlpFeatureJson::jsonb, now()) " +
            "ON CONFLICT (hub_id, target_table, candidate_kind, candidate_id, scope_limit) DO UPDATE SET " +
            "  base_probability        = EXCLUDED.base_probability, " +
            "  cosine_similarity       = EXCLUDED.cosine_similarity, " +
            "  static_relation_weight  = EXCLUDED.static_relation_weight, " +
            "  statistical_weight      = EXCLUDED.statistical_weight, " +
            "  mlp_feature_score       = EXCLUDED.mlp_feature_score, " +
            "  feedback_adjustment     = EXCLUDED.feedback_adjustment, " +
            "  ema_fast_30             = EXCLUDED.ema_fast_30, " +
            "  ema_slow_10             = EXCLUDED.ema_slow_10, " +
            "  trend                   = EXCLUDED.trend, " +
            "  cross_state             = EXCLUDED.cross_state, " +
            "  attention_score         = EXCLUDED.attention_score, " +
            "  rank                    = EXCLUDED.rank, " +
            "  evidence_json           = EXCLUDED.evidence_json, " +
            "  mlp_feature_json        = EXCLUDED.mlp_feature_json, " +
            "  updated_at              = now()";

        AddNullableDouble(cmd, "baseProbability",       record.BaseProbability);
        AddNullableDouble(cmd, "cosineSimilarity",      record.CosineSimilarity);
        AddNullableDouble(cmd, "staticRelationWeight",  record.StaticRelationWeight);
        AddNullableDouble(cmd, "statisticalWeight",     record.StatisticalWeight);
        AddNullableDouble(cmd, "mlpFeatureScore",       record.MlpFeatureScore);
        AddNullableDouble(cmd, "emaFast",               record.EmaFast);
        AddNullableDouble(cmd, "emaSlow",               record.EmaSlow);
        AddNullableDouble(cmd, "trend",                 record.Trend);
        AddNullableDouble(cmd, "attentionScore",        record.AttentionScore);
        AddNullableInt(cmd,    "rank",                  record.Rank);
        cmd.Parameters.AddWithValue("hubId",             record.HubId);
        cmd.Parameters.AddWithValue("targetTable",       record.TargetTable);
        cmd.Parameters.AddWithValue("candidateKind",     record.CandidateKind);
        cmd.Parameters.AddWithValue("candidateId",       record.CandidateId);
        cmd.Parameters.AddWithValue("scopeLimit",        record.ScopeLimit);
        cmd.Parameters.AddWithValue("feedbackAdjustment",(double)record.FeedbackAdjustment);
        cmd.Parameters.AddWithValue("crossState",        record.CrossState is not null ? (object)record.CrossState : DBNull.Value);
        cmd.Parameters.AddWithValue("evidenceJson",      record.EvidenceJson);
        cmd.Parameters.AddWithValue("mlpFeatureJson",    record.MlpFeatureJson);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Recalculates rank for all rows with the given hub_id and scope_limit,
    /// ordered by attention_score DESC NULLS LAST. Runs after a batch of upserts.
    /// </summary>
    public override async Task RecalculateHubAttentionRanksAsync(
        Guid hubId,
        int scopeLimit,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE context_hub_recommendation_current AS t " +
            "SET rank = sub.rn " +
            "FROM ( " +
            "    SELECT hub_id, target_table, candidate_kind, candidate_id, scope_limit, " +
            "           ROW_NUMBER() OVER (ORDER BY COALESCE(attention_score, 0.0) DESC) AS rn " +
            "    FROM context_hub_recommendation_current " +
            "    WHERE hub_id = @hubId AND scope_limit = @scopeLimit " +
            ") AS sub " +
            "WHERE t.hub_id = sub.hub_id " +
            "  AND t.target_table = sub.target_table " +
            "  AND t.candidate_kind = sub.candidate_kind " +
            "  AND t.candidate_id = sub.candidate_id " +
            "  AND t.scope_limit = sub.scope_limit";
        cmd.Parameters.AddWithValue("hubId",      hubId);
        cmd.Parameters.AddWithValue("scopeLimit", scopeLimit);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------------------------------------------------------------------------
    // Topology Vector Runtime — Feedback Weight Update
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Appends each feedback event to context_hub_feedback_event (append-only) and
    /// applies the delta to feedback_adjustment in context_hub_recommendation_current.
    /// Both operations run in a single transaction.
    ///
    /// Returns the count of feedback events inserted.
    /// UPDATE to context_hub_recommendation_current is best-effort: 0 rows updated when
    /// the candidate is not yet in current (no error; current may not exist yet for this candidate).
    /// </summary>
    public override async Task<int> ApplyFeedbackWeightUpdateAsync(
        IReadOnlyList<HubFeedbackEvent> events,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
            return 0;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var txn = await conn.BeginTransactionAsync(ct);

        try
        {
            var inserted = 0;
            foreach (var ev in events)
            {
                await using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = txn;
                insertCmd.CommandText =
                    "INSERT INTO context_hub_feedback_event " +
                    "(hub_id, target_table, candidate_id, candidate_kind, scope_limit, feedback_kind, delta_applied) " +
                    "VALUES (@hubId, @targetTable, @candidateId, @candidateKind, @scopeLimit, @feedbackKind, @deltaApplied)";
                insertCmd.Parameters.AddWithValue("hubId",         ev.HubId);
                insertCmd.Parameters.AddWithValue("targetTable",   ev.TargetTable);
                insertCmd.Parameters.AddWithValue("candidateId",   ev.CandidateId);
                insertCmd.Parameters.AddWithValue("candidateKind", ev.CandidateKind);
                insertCmd.Parameters.AddWithValue("scopeLimit",    ev.ScopeLimit);
                insertCmd.Parameters.AddWithValue("feedbackKind",  ev.FeedbackKind);
                insertCmd.Parameters.AddWithValue("deltaApplied",  (double)ev.DeltaApplied);
                await insertCmd.ExecuteNonQueryAsync(ct);
                inserted++;

                await using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = txn;
                updateCmd.CommandText =
                    "UPDATE context_hub_recommendation_current " +
                    "SET feedback_adjustment = feedback_adjustment + @delta, " +
                    "    updated_at = now() " +
                    "WHERE hub_id = @hubId " +
                    "  AND target_table = @targetTable " +
                    "  AND candidate_id = @candidateId " +
                    "  AND candidate_kind = @candidateKind " +
                    "  AND scope_limit = @scopeLimit";
                updateCmd.Parameters.AddWithValue("delta",         (double)ev.DeltaApplied);
                updateCmd.Parameters.AddWithValue("hubId",         ev.HubId);
                updateCmd.Parameters.AddWithValue("targetTable",   ev.TargetTable);
                updateCmd.Parameters.AddWithValue("candidateId",   ev.CandidateId);
                updateCmd.Parameters.AddWithValue("candidateKind", ev.CandidateKind);
                updateCmd.Parameters.AddWithValue("scopeLimit",    ev.ScopeLimit);
                await updateCmd.ExecuteNonQueryAsync(ct);
            }

            await txn.CommitAsync(ct);

            _npgsqlLogger.LogDebug(
                "NpgsqlContextRouteRepository.ApplyFeedbackWeightUpdateAsync: applied {Count} feedback event(s).",
                inserted);
            return inserted;
        }
        catch
        {
            await txn.RollbackAsync(ct);
            throw;
        }
    }

    // ---------------------------------------------------------------------------
    // System Operation CI — read-only inspection surfaces
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Loads lightweight hub attention summaries for system CI cron inspection.
    /// Queries context_hub_recommendation_current for key fields only.
    /// HasEvidence: true when jsonb_array_length(evidence_json) > 0 and evidence_json is an array.
    /// </summary>
    public override async Task<IReadOnlyList<HubAttentionCiSummary>> LoadHubAttentionSummaryForCiAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        // CASE guards jsonb_array_length — AND does not short-circuit in PostgreSQL,
        // so calling jsonb_array_length on the default non-array JSONB '{}' would error.
        cmd.CommandText =
            "SELECT hub_id, candidate_id, scope_limit, " +
            "       attention_score, ema_fast_30, ema_slow_10, " +
            "       CASE WHEN jsonb_typeof(evidence_json) = 'array' " +
            "            THEN jsonb_array_length(evidence_json) > 0 " +
            "            ELSE false " +
            "       END AS has_evidence, " +
            "       updated_at " +
            "FROM context_hub_recommendation_current " +
            "ORDER BY updated_at DESC";

        var records = new List<HubAttentionCiSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            records.Add(new HubAttentionCiSummary(
                HubId:          reader.GetGuid(0),
                CandidateId:    reader.GetGuid(1),
                ScopeLimit:     reader.GetInt32(2),
                AttentionScore: reader.IsDBNull(3) ? null : (float)reader.GetDouble(3),
                EmaFast:        reader.IsDBNull(4) ? null : (float)reader.GetDouble(4),
                EmaSlow:        reader.IsDBNull(5) ? null : (float)reader.GetDouble(5),
                HasEvidence:    reader.GetBoolean(6),
                UpdatedAt:      reader.GetFieldValue<DateTimeOffset>(7)
            ));
        }

        return records;
    }

    /// <summary>
    /// Returns the total count of rows in context_event.
    /// Used by system CI rebuildability check.
    /// </summary>
    public override async Task<long> CountContextEventsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM context_event";
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is long l ? l : Convert.ToInt64(result);
    }

    /// <summary>
    /// Returns the total count of rows in context_hub_feedback_event.
    /// Used by system CI rebuildability check as secondary event source.
    /// </summary>
    public override async Task<long> CountFeedbackEventsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM context_hub_feedback_event";
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is long l ? l : Convert.ToInt64(result);
    }

    /// <summary>
    /// Loads a lightweight registry token summary for system CI inspection.
    /// Counts active tokens and active tokens not referenced by any hub attention record.
    /// </summary>
    public override async Task<RegistryTokenCiSummary> LoadRegistryTokenSummaryForCiAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT " +
            "  (SELECT COUNT(*) FROM context_token_registry WHERE status = 'active') AS total_active_tokens, " +
            "  (SELECT COUNT(*) FROM context_token_registry " +
            "   WHERE status = 'active' " +
            "     AND token_id NOT IN (" +
            "         SELECT DISTINCT candidate_id " +
            "         FROM context_hub_recommendation_current " +
            "         WHERE candidate_kind = 'token'" +
            "     )) AS unreferenced_token_count";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new RegistryTokenCiSummary(TotalActiveTokens: 0, UnreferencedTokenCount: 0);

        return new RegistryTokenCiSummary(
            TotalActiveTokens:      Convert.ToInt32(reader.GetValue(0)),
            UnreferencedTokenCount: Convert.ToInt32(reader.GetValue(1))
        );
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private static void AddNullableDouble(NpgsqlCommand cmd, string name, float? value)
    {
        var p = new NpgsqlParameter(name, System.Data.DbType.Double)
        {
            Value = value.HasValue ? (object)(double)value.Value : DBNull.Value
        };
        cmd.Parameters.Add(p);
    }

    private static void AddNullableInt(NpgsqlCommand cmd, string name, int? value)
    {
        var p = new NpgsqlParameter(name, System.Data.DbType.Int32)
        {
            Value = value.HasValue ? (object)value.Value : DBNull.Value
        };
        cmd.Parameters.Add(p);
    }
}
