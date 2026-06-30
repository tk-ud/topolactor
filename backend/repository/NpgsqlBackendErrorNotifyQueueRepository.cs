using Npgsql;
using NpgsqlTypes;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Npgsql implementation of the durable logs.error_notify_queue claim/ack/fail boundary.
///
/// SSOT: docs/design/backend-error-evidence-report-notify-ssot.yaml (report_and_notify.notify_queue).
///
/// The durable queue row — not the pg_notify wake-up — is the source of truth. Claiming is atomic
/// (FOR UPDATE SKIP LOCKED) so concurrent bridge instances never double-process a row, and each claim
/// joins logs.error so the bridge builds the hook trigger payload from authoritative evidence fields.
/// </summary>
public sealed class NpgsqlBackendErrorNotifyQueueRepository : IBackendErrorNotifyQueueRepository
{
    private const int MaxReasonLength = 1000;

    private readonly string _connectionString;

    public NpgsqlBackendErrorNotifyQueueRepository(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<IReadOnlyList<BackendErrorNotifyClaim>> ClaimPendingAsync(
        int batchSize, TimeSpan retryBackoff, CancellationToken ct = default)
    {
        var batch = Math.Clamp(batchSize, 1, 500);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            WITH claimed AS (
                UPDATE logs.error_notify_queue q
                SET status = 'processing',
                    processing_at = now(),
                    last_attempt_at = now(),
                    attempt_count = q.attempt_count + 1
                WHERE q.queue_id IN (
                    SELECT queue_id FROM logs.error_notify_queue
                    WHERE status = 'pending'
                       OR (status = 'failed_retryable'
                           AND (last_attempt_at IS NULL
                                OR last_attempt_at < now() - make_interval(secs => @backoffSeconds)))
                    ORDER BY enqueued_at
                    FOR UPDATE SKIP LOCKED
                    LIMIT @batch
                )
                RETURNING q.queue_id, q.error_id, q.attempt_count
            )
            SELECT c.queue_id, c.error_id, c.attempt_count,
                   e.origin_layer, e.boundary_key, e.error_code, e.severity, e.stack_hash
            FROM claimed c
            JOIN logs.error e ON e.error_id = c.error_id
            """;
        cmd.Parameters.AddWithValue("batch", batch);
        cmd.Parameters.AddWithValue("backoffSeconds", Math.Max(0, retryBackoff.TotalSeconds));

        var claims = new List<BackendErrorNotifyClaim>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            claims.Add(new BackendErrorNotifyClaim(
                QueueId: reader.GetGuid(0),
                ErrorId: reader.GetGuid(1),
                AttemptCount: reader.GetInt32(2),
                OriginLayer: reader.GetString(3),
                BoundaryKey: reader.GetString(4),
                ErrorCode: reader.GetString(5),
                Severity: reader.GetString(6),
                StackHash: reader.GetString(7)));
        }
        return claims;
    }

    public async Task AcknowledgeAsync(Guid queueId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE logs.error_notify_queue
            SET status = 'acknowledged', acknowledged_at = now()
            WHERE queue_id = @id AND status = 'processing'
            """;
        cmd.Parameters.AddWithValue("id", queueId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task FailAsync(Guid queueId, string reason, bool terminal, CancellationToken ct = default)
    {
        var status = terminal
            ? BackendErrorNotifyQueueStatus.FailedTerminal
            : BackendErrorNotifyQueueStatus.FailedRetryable;
        var boundedReason = string.IsNullOrEmpty(reason)
            ? string.Empty
            : (reason.Length <= MaxReasonLength ? reason : reason[..MaxReasonLength]);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        // Never delete the row — durable failure evidence is preserved.
        cmd.CommandText = """
            UPDATE logs.error_notify_queue
            SET status = @status, failed_reason = @reason
            WHERE queue_id = @id AND status = 'processing'
            """;
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("reason", NpgsqlDbType.Text, boundedReason);
        cmd.Parameters.AddWithValue("id", queueId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
