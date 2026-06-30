using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB tests for the backend system error evidence substrate.
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set, matching the repository live-DB
/// test convention. Requires db/backend_error_evidence_tables.sql applied to the target database.
///
/// Verifies: append -> logs.error row; AFTER INSERT trigger -> logs.error_notify_queue durable row;
/// logs.error_report derived aggregation; current.error_report_projection read-side projection;
/// redaction/bounding of evidence_json and message_public.
/// </summary>
public class BackendErrorEvidenceAppenderLiveDbTests
{
    [Fact]
    public async Task AppendAsync_WritesEvidence_EnqueuesNotify_AndAggregatesReport()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var marker = "live-test-" + Guid.NewGuid().ToString("N");
        var appender = new NpgsqlBackendErrorEvidenceAppender(cs);

        var evidence = new BackendErrorEvidence(
            OriginLayer: BackendErrorOriginLayer.AbstractFunctionExecutor,
            BoundaryKey: marker,
            ErrorKind: BackendErrorKind.SystemError,
            ErrorCode: "LIVE_TEST_SYSTEM_ERROR",
            MessagePublic: new string('x', 5000), // exceeds the 2000-char bound -> truncated
            StackHash: "sha256:" + marker,
            Severity: "error",
            Retryable: false,
            FunctionKey: "test.fn",
            RuntimeLane: "external_port_runtime",
            EvidenceJson: new Dictionary<string, string>
            {
                ["safe_key"] = "safe_value",
                ["access_token"] = "super-secret-token", // secret-bearing -> redacted
            });

        try
        {
            await appender.AppendAsync(evidence);

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();

            // 1) logs.error row exists, message bounded, secret redacted, safe key preserved.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT length(message_public) AS msg_len,
                           evidence_json->>'access_token' AS tok,
                           evidence_json->>'safe_key' AS safe
                    FROM logs.error WHERE boundary_key = @marker
                    """;
                cmd.Parameters.AddWithValue("marker", marker);
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync(), "logs.error row must exist");
                Assert.Equal(2000, reader.GetInt32(0));
                Assert.Equal("[redacted]", reader.GetString(1));
                Assert.Equal("safe_value", reader.GetString(2));
            }

            // 2) AFTER INSERT trigger enqueued a durable notify queue row.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT count(*) FROM logs.error_notify_queue q
                    JOIN logs.error e ON e.error_id = q.error_id
                    WHERE e.boundary_key = @marker AND q.channel = 'logs_error_inserted'
                    """;
                cmd.Parameters.AddWithValue("marker", marker);
                Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
            }

            // 3) logs.error_report derived aggregation surfaces the report.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT occurrence_count FROM logs.error_report
                    WHERE boundary_key = @marker AND error_code = 'LIVE_TEST_SYSTEM_ERROR'
                    """;
                cmd.Parameters.AddWithValue("marker", marker);
                Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
            }

            // 4) current.error_report_projection read-side projection reads the unresolved report.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM current.error_report_projection WHERE boundary_key = @marker";
                cmd.Parameters.AddWithValue("marker", marker);
                Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            await CleanupAsync(cs, marker);
        }
    }

    [Fact]
    public async Task LogsError_IsAppendOnly_UpdateRejected()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var marker = "live-append-only-" + Guid.NewGuid().ToString("N");
        var appender = new NpgsqlBackendErrorEvidenceAppender(cs);
        await appender.AppendAsync(new BackendErrorEvidence(
            OriginLayer: BackendErrorOriginLayer.RuntimeExecutor,
            BoundaryKey: marker,
            ErrorKind: BackendErrorKind.SystemError,
            ErrorCode: "LIVE_APPEND_ONLY",
            MessagePublic: "append-only test",
            StackHash: "sha256:" + marker,
            Severity: "error",
            Retryable: false));

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE logs.error SET severity = 'warning' WHERE boundary_key = @marker";
            cmd.Parameters.AddWithValue("marker", marker);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("append-only", ex.MessageText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupAsync(cs, marker);
        }
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
