using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB tests for the durable post-notify queue boundary (logs.error_notify_queue).
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set.
///
/// Verifies: AFTER INSERT trigger enqueues a pending row; atomic claim (no double-claim) joins
/// logs.error; acknowledge transition; failed_retryable -> re-claim -> failed_terminal transitions;
/// failed rows are never deleted.
/// </summary>
[Collection("BackendErrorEvidenceLiveDb")]
public class BackendErrorNotifyQueueRepositoryLiveDbTests
{
    [Fact]
    public async Task Claim_IsAtomic_JoinsEvidence_AndAcknowledges()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var marker = "notify-claim-" + Guid.NewGuid().ToString("N");
        var appender = new NpgsqlBackendErrorEvidenceAppender(cs);
        var repo = new NpgsqlBackendErrorNotifyQueueRepository(cs);

        await appender.AppendAsync(MakeEvidence(marker, "NOTIFY_CLAIM_TEST"));
        try
        {
            var claims = await repo.ClaimPendingAsync(50, TimeSpan.Zero);
            var mine = Assert.Single(claims, c => c.BoundaryKey == marker);
            Assert.Equal("NOTIFY_CLAIM_TEST", mine.ErrorCode);
            Assert.Equal(BackendErrorOriginLayer.AbstractFunctionExecutor, mine.OriginLayer);
            Assert.Equal(1, mine.AttemptCount);
            Assert.Equal("processing", await StatusAsync(cs, mine.QueueId));

            // Atomic claim: an immediate re-claim must NOT return the already-claimed row.
            var second = await repo.ClaimPendingAsync(50, TimeSpan.FromMinutes(10));
            Assert.DoesNotContain(second, c => c.QueueId == mine.QueueId);

            await repo.AcknowledgeAsync(mine.QueueId);
            Assert.Equal("acknowledged", await StatusAsync(cs, mine.QueueId));
        }
        finally
        {
            await CleanupAsync(cs, marker);
        }
    }

    [Fact]
    public async Task Fail_RetryableThenTerminal_RowNeverDeleted()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var marker = "notify-fail-" + Guid.NewGuid().ToString("N");
        var appender = new NpgsqlBackendErrorEvidenceAppender(cs);
        var repo = new NpgsqlBackendErrorNotifyQueueRepository(cs);

        await appender.AppendAsync(MakeEvidence(marker, "NOTIFY_FAIL_TEST"));
        try
        {
            var first = Assert.Single(await repo.ClaimPendingAsync(50, TimeSpan.Zero), c => c.BoundaryKey == marker);
            await repo.FailAsync(first.QueueId, "EXTERNAL_PORT_RECORD_MISSING", terminal: false);
            Assert.Equal("failed_retryable", await StatusAsync(cs, first.QueueId));

            // With zero backoff, a failed_retryable row is reclaimable; attempt_count advances.
            var retry = Assert.Single(await repo.ClaimPendingAsync(50, TimeSpan.Zero), c => c.QueueId == first.QueueId);
            Assert.Equal(2, retry.AttemptCount);

            await repo.FailAsync(retry.QueueId, "EXHAUSTED", terminal: true);
            Assert.Equal("failed_terminal", await StatusAsync(cs, retry.QueueId));

            // Row preserved (durable failure evidence), not deleted.
            Assert.True(await ExistsAsync(cs, first.QueueId));
        }
        finally
        {
            await CleanupAsync(cs, marker);
        }
    }

    private static BackendErrorEvidence MakeEvidence(string marker, string code) => new(
        OriginLayer: BackendErrorOriginLayer.AbstractFunctionExecutor,
        BoundaryKey: marker,
        ErrorKind: BackendErrorKind.SystemError,
        ErrorCode: code,
        MessagePublic: "notify queue live test",
        StackHash: "sha256:" + marker,
        Severity: "error",
        Retryable: false,
        FunctionKey: "test.fn",
        RuntimeLane: "external_port_runtime");

    private static async Task<string> StatusAsync(string cs, Guid queueId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT status FROM logs.error_notify_queue WHERE queue_id = @id";
        cmd.Parameters.AddWithValue("id", queueId);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<bool> ExistsAsync(string cs, Guid queueId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM logs.error_notify_queue WHERE queue_id = @id";
        cmd.Parameters.AddWithValue("id", queueId);
        return (long)(await cmd.ExecuteScalarAsync())! == 1;
    }

    private static async Task CleanupAsync(string cs, string marker)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM logs.error_notify_queue
            WHERE error_id IN (SELECT error_id FROM logs.error WHERE boundary_key = @marker);
            DELETE FROM logs.error WHERE boundary_key = @marker;
            """;
        cmd.Parameters.AddWithValue("marker", marker);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }
}
