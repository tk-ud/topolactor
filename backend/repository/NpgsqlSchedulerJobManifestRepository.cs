using System.Text.Json;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public sealed record SchedulerJobRecord(
    Guid SchedulerJobId,
    string JobKey,
    string TriggerKind,
    string SchedulePolicyKind,
    string? CronExpression,
    long? ScheduleIntervalSeconds,
    bool ManualRunAllowed,
    bool Active,
    string? InputTableRef,
    string? InputStatusColumn,
    string? InputStatusPendingValue,
    string? InputStatusProcessingValue,
    string? InputStatusCompletedValue,
    string? InputStatusFailedValue,
    int MaxBatchSize,
    int LeaseSeconds,
    string AuthorityScope,
    string? CredentialRequirementRef,
    string? ExternalPortRef,
    IReadOnlyDictionary<string, object?> ProjectionPolicy);

public sealed record SchedulerJobStepRecord(
    Guid SchedulerJobStepId,
    Guid SchedulerJobId,
    int StepOrder,
    string AbstractFunctionKey,
    string OnError,
    string? ResultContextKey,
    bool Active);

public sealed record SchedulerJobRunRecord(
    Guid SchedulerJobRunId,
    Guid SchedulerJobId,
    string JobKey,
    string RunStatus);

public interface ISchedulerJobManifestRepository
{
    Task<IReadOnlyList<SchedulerJobRecord>> LoadActiveJobsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SchedulerJobStepRecord>> LoadStepsAsync(Guid schedulerJobId, CancellationToken ct = default);
    Task<SchedulerJobRunRecord> CreateRunAsync(Guid schedulerJobId, string jobKey, string triggerKind, string schedulePolicyKind, string? inputRef, DateTimeOffset leaseUntil, CancellationToken ct = default);
    Task UpdateRunStatusAsync(Guid schedulerJobRunId, string runStatus, string? lastErrorJson, string? resultContextJson, CancellationToken ct = default);
    Task<IReadOnlyList<SchedulerJobRecord>> LoadSettingsProjectionAsync(CancellationToken ct = default);
}

public sealed class NpgsqlSchedulerJobManifestRepository : ISchedulerJobManifestRepository
{
    private readonly string _connectionString;

    public NpgsqlSchedulerJobManifestRepository(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<IReadOnlyList<SchedulerJobRecord>> LoadActiveJobsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var result = new List<SchedulerJobRecord>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT scheduler_job_id, job_key, trigger_kind, schedule_policy_kind,
                   cron_expression, schedule_interval_seconds, manual_run_allowed, active,
                   input_table_ref, input_status_column,
                   input_status_pending_value, input_status_processing_value,
                   input_status_completed_value, input_status_failed_value,
                   max_batch_size, lease_seconds, authority_scope,
                   credential_requirement_ref, external_port_ref, projection_policy
            FROM topology.scheduler_jobs
            WHERE active = true
            ORDER BY job_key ASC
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(MapRecord(reader));

        return result;
    }

    public async Task<IReadOnlyList<SchedulerJobStepRecord>> LoadStepsAsync(Guid schedulerJobId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var result = new List<SchedulerJobStepRecord>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT scheduler_job_step_id, scheduler_job_id, step_order,
                   abstract_function_key, on_error, result_context_key, active
            FROM topology.scheduler_job_steps
            WHERE scheduler_job_id = @jobId AND active = true
            ORDER BY step_order ASC
            """;
        cmd.Parameters.AddWithValue("jobId", schedulerJobId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new SchedulerJobStepRecord(
                reader.GetGuid(reader.GetOrdinal("scheduler_job_step_id")),
                reader.GetGuid(reader.GetOrdinal("scheduler_job_id")),
                reader.GetInt32(reader.GetOrdinal("step_order")),
                reader.GetString(reader.GetOrdinal("abstract_function_key")),
                reader.GetString(reader.GetOrdinal("on_error")),
                reader.IsDBNull(reader.GetOrdinal("result_context_key")) ? null : reader.GetString(reader.GetOrdinal("result_context_key")),
                reader.GetBoolean(reader.GetOrdinal("active"))));
        }

        return result;
    }

    public async Task<SchedulerJobRunRecord> CreateRunAsync(Guid schedulerJobId, string jobKey, string triggerKind, string schedulePolicyKind, string? inputRef, DateTimeOffset leaseUntil, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.scheduler_job_runs
                (scheduler_job_id, job_key, trigger_kind, schedule_policy_kind,
                 run_status, input_ref, lease_until, started_at)
            VALUES
                (@jobId, @jobKey, @triggerKind, @policyKind,
                 'queued', @inputRef, @leaseUntil, now())
            RETURNING scheduler_job_run_id, scheduler_job_id, job_key, run_status
            """;
        cmd.Parameters.AddWithValue("jobId", schedulerJobId);
        cmd.Parameters.AddWithValue("jobKey", jobKey);
        cmd.Parameters.AddWithValue("triggerKind", triggerKind);
        cmd.Parameters.AddWithValue("policyKind", schedulePolicyKind);
        cmd.Parameters.AddWithValue("inputRef", (object?)inputRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("leaseUntil", leaseUntil);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new SchedulerJobRunRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    public async Task UpdateRunStatusAsync(Guid schedulerJobRunId, string runStatus, string? lastErrorJson, string? resultContextJson, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE topology.scheduler_job_runs
            SET run_status    = @status,
                completed_at  = CASE WHEN @status IN ('completed','failed','cancelled','lease_expired') THEN now() ELSE completed_at END,
                last_error    = @lastError::jsonb,
                result_context= COALESCE(@resultContext::jsonb, result_context)
            WHERE scheduler_job_run_id = @runId
            """;
        cmd.Parameters.AddWithValue("runId", schedulerJobRunId);
        cmd.Parameters.AddWithValue("status", runStatus);
        cmd.Parameters.AddWithValue("lastError", (object?)lastErrorJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("resultContext", (object?)resultContextJson ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<SchedulerJobRecord>> LoadSettingsProjectionAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var result = new List<SchedulerJobRecord>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT scheduler_job_id, job_key, trigger_kind, schedule_policy_kind,
                   cron_expression, schedule_interval_seconds, manual_run_allowed, active,
                   input_table_ref, input_status_column,
                   input_status_pending_value, input_status_processing_value,
                   input_status_completed_value, input_status_failed_value,
                   max_batch_size, lease_seconds, authority_scope,
                   credential_requirement_ref, external_port_ref, projection_policy
            FROM topology.scheduler_jobs
            ORDER BY job_key ASC
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(MapRecord(reader));

        return result;
    }

    private static SchedulerJobRecord MapRecord(NpgsqlDataReader reader)
    {
        var projectionPolicyJson = reader.IsDBNull(reader.GetOrdinal("projection_policy"))
            ? "{}"
            : reader.GetString(reader.GetOrdinal("projection_policy"));
        var projectionPolicy = ParseJsonObject(projectionPolicyJson);

        return new SchedulerJobRecord(
            reader.GetGuid(reader.GetOrdinal("scheduler_job_id")),
            reader.GetString(reader.GetOrdinal("job_key")),
            reader.GetString(reader.GetOrdinal("trigger_kind")),
            reader.GetString(reader.GetOrdinal("schedule_policy_kind")),
            reader.IsDBNull(reader.GetOrdinal("cron_expression")) ? null : reader.GetString(reader.GetOrdinal("cron_expression")),
            reader.IsDBNull(reader.GetOrdinal("schedule_interval_seconds")) ? null : reader.GetInt64(reader.GetOrdinal("schedule_interval_seconds")),
            reader.GetBoolean(reader.GetOrdinal("manual_run_allowed")),
            reader.GetBoolean(reader.GetOrdinal("active")),
            reader.IsDBNull(reader.GetOrdinal("input_table_ref")) ? null : reader.GetString(reader.GetOrdinal("input_table_ref")),
            reader.IsDBNull(reader.GetOrdinal("input_status_column")) ? null : reader.GetString(reader.GetOrdinal("input_status_column")),
            reader.IsDBNull(reader.GetOrdinal("input_status_pending_value")) ? null : reader.GetString(reader.GetOrdinal("input_status_pending_value")),
            reader.IsDBNull(reader.GetOrdinal("input_status_processing_value")) ? null : reader.GetString(reader.GetOrdinal("input_status_processing_value")),
            reader.IsDBNull(reader.GetOrdinal("input_status_completed_value")) ? null : reader.GetString(reader.GetOrdinal("input_status_completed_value")),
            reader.IsDBNull(reader.GetOrdinal("input_status_failed_value")) ? null : reader.GetString(reader.GetOrdinal("input_status_failed_value")),
            reader.GetInt32(reader.GetOrdinal("max_batch_size")),
            reader.GetInt32(reader.GetOrdinal("lease_seconds")),
            reader.GetString(reader.GetOrdinal("authority_scope")),
            reader.IsDBNull(reader.GetOrdinal("credential_requirement_ref")) ? null : reader.GetString(reader.GetOrdinal("credential_requirement_ref")),
            reader.IsDBNull(reader.GetOrdinal("external_port_ref")) ? null : reader.GetString(reader.GetOrdinal("external_port_ref")),
            projectionPolicy);
    }

    private static IReadOnlyDictionary<string, object?> ParseJsonObject(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, object?>(StringComparer.Ordinal);

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
            result[property.Name] = property.Value.GetRawText();
        return result;
    }
}
