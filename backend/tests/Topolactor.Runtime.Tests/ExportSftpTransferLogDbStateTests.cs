using System.Text.Json;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

/// <summary>
/// Live PostgreSQL state and projection tests for topology.sftp_transfer_log.
/// Proves that runtime dispatch writes transfer_initiated/transfer_completed/
/// transfer_failed/checksum_mismatch/retry_attempted as individual rows with
/// the correct transfer_status, and that LoadProjectionAsync returns safe fields
/// only (no credential/host/user/private_key/endpoint/remote_path/signed_url).
///
/// SSOT: docs/design/runtime-bundle-export-sftp-implementation-ssot.yaml
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class ExportSftpTransferLogDbStateTests
{
    private static readonly string[] DeniedProjectionFields =
        ["credential", "host", "user", "username", "private_key", "endpoint", "remote_path", "signed_url", "raw_provider_response"];

    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException("TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }

    [Fact]
    public async Task TransferInitiatedAndCompleted_WriteSeparateRowsWithCorrectStatus()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var (jobId, artifactId, manifestId, checksumId) = await InsertPrerequisitesAsync(cs, "sha256:test-checksum-match");
        var dispatchId = Guid.NewGuid().ToString("N");
        var repo = new NpgsqlExternalPortConsumerEvidenceRepository(cs);
        var context = BuildContext(dispatchId, jobId);
        var config = BuildStepConfig();

        try
        {
            await repo.AppendEvidenceAsync("topology.sftp_transfer_log", "transfer_initiated", dispatchId, "export_sftp_bundle", context, config);
            var initiatedRow = await QueryLatestTransferLogAsync(cs, dispatchId);
            Assert.NotNull(initiatedRow);
            Assert.Equal("transfer_initiated", initiatedRow!.Value.status);
            Assert.Equal(jobId, initiatedRow.Value.exportJobId);

            await repo.AppendEvidenceAsync("topology.sftp_transfer_log", "transfer_completed", dispatchId, "export_sftp_bundle", context, BuildStepConfig("transfer_completed"));
            var rows = await QueryAllTransferLogsAsync(cs, dispatchId);
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, r => r.status == "transfer_initiated");
            Assert.Contains(rows, r => r.status == "transfer_completed");
        }
        finally
        {
            await CleanupAsync(cs, jobId);
        }
    }

    [Fact]
    public async Task TransferFailed_WritesRowWithFailureStatus()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var jobId = Guid.NewGuid();
        await InsertExportJobOnlyAsync(cs, jobId);
        var dispatchId = Guid.NewGuid().ToString("N");
        var repo = new NpgsqlExternalPortConsumerEvidenceRepository(cs);
        var context = BuildContext(dispatchId, jobId);
        var failConfig = BuildStepConfig("transfer_failed", failureReason: "SFTP_TRANSFER_EXPORT_JOB_MANIFEST_CHECKSUM_REQUIRED");

        try
        {
            await repo.AppendEvidenceAsync("topology.sftp_transfer_log", "transfer_failed", dispatchId, "export_sftp_bundle", context, failConfig);
            var row = await QueryLatestTransferLogAsync(cs, dispatchId);
            Assert.NotNull(row);
            Assert.Equal("transfer_failed", row!.Value.status);
            Assert.Equal(jobId, row.Value.exportJobId);
        }
        finally
        {
            await CleanupAsync(cs, jobId);
        }
    }

    [Fact]
    public async Task ChecksumMismatch_WritesRowWithMismatchStatus()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var (jobId, _, _, _) = await InsertPrerequisitesAsync(cs, checksumFileValue: "sha256:file-hash", manifestChecksum: "sha256:manifest-different");
        var dispatchId = Guid.NewGuid().ToString("N");
        var repo = new NpgsqlExternalPortConsumerEvidenceRepository(cs);
        var context = BuildContext(dispatchId, jobId);
        var mismatchConfig = BuildStepConfig("checksum_mismatch", failureReason: "checksum_mismatch");

        try
        {
            await repo.AppendEvidenceAsync("topology.sftp_transfer_log", "checksum_mismatch", dispatchId, "export_sftp_bundle", context, mismatchConfig);
            var row = await QueryLatestTransferLogAsync(cs, dispatchId);
            Assert.NotNull(row);
            Assert.Equal("checksum_mismatch", row!.Value.status);
        }
        finally
        {
            await CleanupAsync(cs, jobId);
        }
    }

    [Fact]
    public async Task RetryAttempted_WritesRowWithRetryStatus()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var (jobId, _, _, _) = await InsertPrerequisitesAsync(cs, "sha256:retry-hash");
        var dispatchId = Guid.NewGuid().ToString("N");
        var repo = new NpgsqlExternalPortConsumerEvidenceRepository(cs);
        var context = BuildContext(dispatchId, jobId);
        var retryConfig = BuildStepConfig("retry_attempted");

        try
        {
            await repo.AppendEvidenceAsync("topology.sftp_transfer_log", "retry_attempted", dispatchId, "export_sftp_bundle", context, retryConfig);
            var row = await QueryLatestTransferLogAsync(cs, dispatchId);
            Assert.NotNull(row);
            Assert.Equal("retry_attempted", row!.Value.status);
        }
        finally
        {
            await CleanupAsync(cs, jobId);
        }
    }

    [Fact]
    public async Task LoadProjection_ReturnsSafeFieldsOnlyNoDeniedFields()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var (jobId, _, _, _) = await InsertPrerequisitesAsync(cs, "sha256:proj-hash");
        var dispatchId = Guid.NewGuid().ToString("N");
        var repo = new NpgsqlExternalPortConsumerEvidenceRepository(cs);
        var context = BuildContext(dispatchId, jobId);

        try
        {
            await repo.AppendEvidenceAsync("topology.sftp_transfer_log", "transfer_completed", dispatchId, "export_sftp_bundle", context, BuildStepConfig("transfer_completed"));

            var rows = await repo.LoadProjectionAsync("topology.sftp_transfer_log", "export_sftp_bundle", dispatchId);
            Assert.NotEmpty(rows);

            var row = rows[0];
            Assert.Equal("transfer_completed", row.Status);
            foreach (var denied in DeniedProjectionFields)
                Assert.False(row.Evidence.ContainsKey(denied), $"Evidence must not contain '{denied}'");

            Assert.True(row.Evidence.ContainsKey("event_type") || row.Status is not null,
                "Projection must expose at least event_type or status");
        }
        finally
        {
            await CleanupAsync(cs, jobId);
        }
    }

    private static ExternalPortExecutionContext BuildContext(string dispatchId, Guid jobId) => new()
    {
        DispatchId = dispatchId,
        RequiredByBundle = "export_sftp_bundle",
        PortKind = "response_port",
        ExportJobId = jobId,
        RequestPayload = JsonSerializer.SerializeToElement(new { export_job_id = jobId.ToString() })
    };

    private static IReadOnlyDictionary<string, string> BuildStepConfig(
        string? statusValue = null,
        string? failureReason = null)
    {
        var d = new Dictionary<string, string>
        {
            ["evidence_table_ref"] = "topology.sftp_transfer_log",
            ["projection_table_ref"] = "topology.sftp_transfer_log",
            ["initiated_event_type"] = "transfer_initiated",
            ["completed_event_type"] = "transfer_completed",
            ["failed_event_type"] = "transfer_failed",
            ["checksum_mismatch_event_type"] = "checksum_mismatch",
            ["retry_event_type"] = "retry_attempted",
            ["retry_policy_ref"] = "scheduler_retry_requested"
        };
        if (statusValue is not null) d["status_value"] = statusValue;
        if (failureReason is not null) d["failure_reason"] = failureReason;
        return d;
    }

    private static async Task<(Guid jobId, Guid artifactId, Guid manifestId, Guid checksumId)> InsertPrerequisitesAsync(
        string cs,
        string checksumFileValue = "sha256:test",
        string? manifestChecksum = null)
    {
        var jobId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var checksumId = Guid.NewGuid();
        var idempotencyKey = $"test-{jobId:N}";
        manifestChecksum ??= checksumFileValue;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO topology.export_jobs
                    (export_job_id, requested_by, idempotency_key, status)
                VALUES (@id, 'test', @key, 'completed')
                """;
            cmd.Parameters.AddWithValue("id", jobId);
            cmd.Parameters.AddWithValue("key", idempotencyKey);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO topology.file_artifacts
                    (file_artifact_id, export_job_id, file_name, file_type, storage_ref, checksum_value)
                VALUES (@id, @jobId, 'test.csv', 'csv', 'env:TEST_STORAGE_REF', @checksum)
                """;
            cmd.Parameters.AddWithValue("id", artifactId);
            cmd.Parameters.AddWithValue("jobId", jobId);
            cmd.Parameters.AddWithValue("checksum", checksumFileValue);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO topology.export_manifests
                    (manifest_id, export_job_id, generated_by, checksum)
                VALUES (@id, @jobId, 'test', @checksum)
                """;
            cmd.Parameters.AddWithValue("id", manifestId);
            cmd.Parameters.AddWithValue("jobId", jobId);
            cmd.Parameters.AddWithValue("checksum", manifestChecksum);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO topology.file_checksum_records
                    (checksum_record_id, export_job_id, file_artifact_id, checksum_value, verification_status)
                VALUES (@id, @jobId, @artifactId, @checksum, 'verified')
                """;
            cmd.Parameters.AddWithValue("id", checksumId);
            cmd.Parameters.AddWithValue("jobId", jobId);
            cmd.Parameters.AddWithValue("artifactId", artifactId);
            cmd.Parameters.AddWithValue("checksum", checksumFileValue);
            await cmd.ExecuteNonQueryAsync();
        }

        return (jobId, artifactId, manifestId, checksumId);
    }

    private static async Task InsertExportJobOnlyAsync(string cs, Guid jobId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.export_jobs
                (export_job_id, requested_by, idempotency_key, status)
            VALUES (@id, 'test', @key, 'completed')
            """;
        cmd.Parameters.AddWithValue("id", jobId);
        cmd.Parameters.AddWithValue("key", $"test-failed-{jobId:N}");
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<(string status, Guid exportJobId)?> QueryLatestTransferLogAsync(string cs, string dispatchId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT transfer_status, export_job_id
            FROM topology.sftp_transfer_log
            WHERE evidence_json->>'dispatch_id' = @dispatchId
            ORDER BY recorded_at DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("dispatchId", dispatchId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return (reader.GetString(0), reader.GetGuid(1));
    }

    private static async Task<IReadOnlyList<(string status, Guid exportJobId)>> QueryAllTransferLogsAsync(string cs, string dispatchId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT transfer_status, export_job_id
            FROM topology.sftp_transfer_log
            WHERE evidence_json->>'dispatch_id' = @dispatchId
            ORDER BY recorded_at
            """;
        cmd.Parameters.AddWithValue("dispatchId", dispatchId);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<(string, Guid)>();
        while (await reader.ReadAsync())
            rows.Add((reader.GetString(0), reader.GetGuid(1)));
        return rows;
    }

    private static async Task CleanupAsync(string cs, Guid jobId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM topology.export_jobs WHERE export_job_id = @id";
        cmd.Parameters.AddWithValue("id", jobId);
        await cmd.ExecuteNonQueryAsync();
    }
}
