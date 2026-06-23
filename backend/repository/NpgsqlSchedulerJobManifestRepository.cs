using System.Text.Json;
using System.Text.RegularExpressions;
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
    IReadOnlyDictionary<string, object?> ProjectionPolicy)
{
    public DateTimeOffset? LastCompletedAt { get; init; }

    // Input lease / lifecycle authority (manifest-defined, never payload-derived).
    public string InputIdColumn { get; init; } = "id";
    public string? InputDueColumn { get; init; }
    public string? InputStatusSkippedValue { get; init; }
    public string? InputStatusRetryWaitValue { get; init; }

    // Authorized output upsert target (manifest-defined).
    public string? OutputTableRef { get; init; }

    // Retry policy (manifest-defined; sourced from retry_policy JSONB).
    public int RetryMaxAttempts { get; init; } = 1;
    public int RetryBackoffSeconds { get; init; } = 0;

    public string Timezone { get; init; } = "UTC";

    /// <summary>True when this job leases rows from a manifest-authorized input table.</summary>
    public bool HasInputSource =>
        !string.IsNullOrWhiteSpace(InputTableRef)
        && !string.IsNullOrWhiteSpace(InputStatusColumn)
        && !string.IsNullOrWhiteSpace(InputStatusPendingValue)
        && !string.IsNullOrWhiteSpace(InputStatusProcessingValue);
}

public sealed record SchedulerJobStepRecord(
    Guid SchedulerJobStepId,
    Guid SchedulerJobId,
    int StepOrder,
    string AbstractFunctionKey,
    string OnError,
    string? ResultContextKey,
    bool Active)
{
    /// <summary>
    /// Manifest-defined binding map: result_context_key -> { source, path }.
    /// Resolved by the scheduler before the step runs and seeded into the
    /// execution context's result_context so the abstract function can read it.
    /// Sources: input (leased row column), scheduler (job identity), constant, result_context.
    /// </summary>
    public IReadOnlyList<SchedulerStepInputBinding> InputBindings { get; init; } =
        Array.Empty<SchedulerStepInputBinding>();

    /// <summary>
    /// Manifest-defined mapping from result_context to a manifest-authorized output upsert.
    /// Null when the step performs no scheduler-level output binding.
    /// </summary>
    public SchedulerStepResultBinding? ResultBinding { get; init; }

    public string? AuthorityScope { get; init; }
}

/// <summary>One manifest-defined input binding for a scheduler job step.</summary>
public sealed record SchedulerStepInputBinding(string ResultContextKey, string Source, string Path);

/// <summary>
/// Manifest-defined result binding describing how a step result maps to a
/// manifest-authorized output upsert. Table/column authority comes from the
/// manifest only; runtime values come from result_context.
/// </summary>
public sealed record SchedulerStepResultBinding(
    string Kind,
    string ResultContextKey,
    IReadOnlyList<string> ConflictColumns,
    IReadOnlyDictionary<string, string> ColumnMap);

/// <summary>A leased input row: identifier plus manifest-authorized column values.</summary>
public sealed record SchedulerInputRow(string InputId, IReadOnlyDictionary<string, object?> Columns);

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

    /// <summary>
    /// Lease up to max_batch_size due+pending rows from the manifest-authorized input table,
    /// atomically transitioning them to the processing status value. Returns the leased rows.
    /// Table/column authority comes from the manifest record only — never from payload.
    /// </summary>
    Task<IReadOnlyList<SchedulerInputRow>> LeaseDueInputRowsAsync(SchedulerJobRecord job, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Set the manifest-authorized status column of one input row to a manifest-defined status value.</summary>
    Task UpdateInputRowStatusAsync(SchedulerJobRecord job, string inputId, string newStatusValue, CancellationToken ct = default);

    /// <summary>Persist a manifest-authorized output upsert from a step result binding.</summary>
    Task UpsertAuthorizedOutputAsync(SchedulerJobRecord job, SchedulerStepResultBinding binding, IReadOnlyDictionary<string, object?> values, CancellationToken ct = default);

    Task<IReadOnlyList<SchedulerJobRecord>> LoadSettingsProjectionAsync(CancellationToken ct = default);

    // ── admin.contents authoring surface ──────────────────────────────────────
    Task<Guid> CreateJobAsync(SchedulerJobDraft draft, CancellationToken ct = default);
    Task<bool> UpdateJobAsync(Guid schedulerJobId, SchedulerJobDraft draft, CancellationToken ct = default);
    Task<bool> SetJobActiveAsync(Guid schedulerJobId, bool active, CancellationToken ct = default);
}

/// <summary>
/// Admin/seed-authored manifest draft. Holds only references and policy data —
/// never credential plaintext. Table/column/status authority is manifest data here.
/// </summary>
public sealed record SchedulerJobDraft(
    string JobKey,
    string TriggerKind,
    string SchedulePolicyKind,
    string? CronExpression,
    long? ScheduleIntervalSeconds,
    bool ManualRunAllowed,
    bool Active,
    string AuthorityScope,
    int MaxBatchSize,
    int LeaseSeconds,
    string? CredentialRequirementRef,
    string? ExternalPortRef);

public sealed class NpgsqlSchedulerJobManifestRepository : ISchedulerJobManifestRepository
{
    // Identifier guards. Table/column authority is manifest-defined; these guards ensure
    // the manifest-supplied identifiers are safe schema-qualified table / bare column names.
    private static readonly Regex TableRefPattern =
        new(@"^[a-z_][a-z0-9_]*\.[a-z_][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ColumnPattern =
        new(@"^[a-z_][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _connectionString;

    public NpgsqlSchedulerJobManifestRepository(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<IReadOnlyList<SchedulerJobRecord>> LoadActiveJobsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var result = new List<SchedulerJobRecord>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns + """

            ,(SELECT MAX(r.completed_at)
                    FROM topology.scheduler_job_runs r
                    WHERE r.scheduler_job_id = j.scheduler_job_id
                      AND r.run_status = 'completed') AS last_completed_at
            FROM topology.scheduler_jobs j
            WHERE j.active = true
            ORDER BY j.job_key ASC
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var lastCompletedAtOrd = reader.GetOrdinal("last_completed_at");
            DateTimeOffset? lastCompletedAt = reader.IsDBNull(lastCompletedAtOrd)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(lastCompletedAtOrd);
            result.Add(MapRecord(reader) with { LastCompletedAt = lastCompletedAt });
        }

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
                   abstract_function_key, on_error, result_context_key, active,
                   input_binding, result_binding, authority_scope
            FROM topology.scheduler_job_steps
            WHERE scheduler_job_id = @jobId AND active = true
            ORDER BY step_order ASC
            """;
        cmd.Parameters.AddWithValue("jobId", schedulerJobId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var inputBindingJson = reader.IsDBNull(reader.GetOrdinal("input_binding")) ? "{}" : reader.GetString(reader.GetOrdinal("input_binding"));
            var resultBindingJson = reader.IsDBNull(reader.GetOrdinal("result_binding")) ? "{}" : reader.GetString(reader.GetOrdinal("result_binding"));
            result.Add(new SchedulerJobStepRecord(
                reader.GetGuid(reader.GetOrdinal("scheduler_job_step_id")),
                reader.GetGuid(reader.GetOrdinal("scheduler_job_id")),
                reader.GetInt32(reader.GetOrdinal("step_order")),
                reader.GetString(reader.GetOrdinal("abstract_function_key")),
                reader.GetString(reader.GetOrdinal("on_error")),
                reader.IsDBNull(reader.GetOrdinal("result_context_key")) ? null : reader.GetString(reader.GetOrdinal("result_context_key")),
                reader.GetBoolean(reader.GetOrdinal("active")))
            {
                InputBindings = ParseInputBindings(inputBindingJson),
                ResultBinding = ParseResultBinding(resultBindingJson),
                AuthorityScope = reader.IsDBNull(reader.GetOrdinal("authority_scope")) ? null : reader.GetString(reader.GetOrdinal("authority_scope")),
            });
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
                completed_at  = CASE WHEN @status IN ('completed','failed','partially_failed','cancelled','lease_expired') THEN now() ELSE completed_at END,
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

    public async Task<IReadOnlyList<SchedulerInputRow>> LeaseDueInputRowsAsync(SchedulerJobRecord job, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!job.HasInputSource) return Array.Empty<SchedulerInputRow>();

        var table = RequireTableRef(job.InputTableRef!, "input_table_ref");
        var idColumn = RequireColumn(job.InputIdColumn, "input_id_column");
        var statusColumn = RequireColumn(job.InputStatusColumn!, "input_status_column");
        var dueColumn = job.InputDueColumn is { Length: > 0 } ? RequireColumn(job.InputDueColumn, "input_due_column") : null;
        var batch = job.MaxBatchSize > 0 ? job.MaxBatchSize : 1;

        var dueClause = dueColumn is null ? string.Empty : $" AND ({dueColumn} IS NULL OR {dueColumn} <= @now)";

        // Atomic lease: select due+pending ids FOR UPDATE SKIP LOCKED, flip them to processing,
        // and return the affected rows. Identifiers are manifest-authorized (guarded above).
        var sql = $"""
            WITH leased AS (
                SELECT {idColumn} AS lease_id
                FROM {table}
                WHERE {statusColumn} = @pending{dueClause}
                ORDER BY {idColumn}
                LIMIT @batch
                FOR UPDATE SKIP LOCKED
            )
            UPDATE {table} t
            SET {statusColumn} = @processing
            FROM leased
            WHERE t.{idColumn} = leased.lease_id
            RETURNING t.{idColumn} AS input_id, to_jsonb(t) AS row_json
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("pending", job.InputStatusPendingValue!);
        cmd.Parameters.AddWithValue("processing", job.InputStatusProcessingValue!);
        cmd.Parameters.AddWithValue("batch", batch);
        if (dueColumn is not null) cmd.Parameters.AddWithValue("now", now);

        var rows = new List<SchedulerInputRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var inputId = Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            var rowJson = reader.IsDBNull(1) ? "{}" : reader.GetString(1);
            rows.Add(new SchedulerInputRow(inputId, ParseTypedJsonObject(rowJson)));
        }

        return rows;
    }

    public async Task UpdateInputRowStatusAsync(SchedulerJobRecord job, string inputId, string newStatusValue, CancellationToken ct = default)
    {
        if (!job.HasInputSource) return;
        var table = RequireTableRef(job.InputTableRef!, "input_table_ref");
        var idColumn = RequireColumn(job.InputIdColumn, "input_id_column");
        var statusColumn = RequireColumn(job.InputStatusColumn!, "input_status_column");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {table} SET {statusColumn} = @status WHERE {idColumn}::text = @id";
        cmd.Parameters.AddWithValue("status", newStatusValue);
        cmd.Parameters.AddWithValue("id", inputId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertAuthorizedOutputAsync(SchedulerJobRecord job, SchedulerStepResultBinding binding, IReadOnlyDictionary<string, object?> values, CancellationToken ct = default)
    {
        var table = RequireTableRef(job.OutputTableRef ?? string.Empty, "output_table_ref");
        if (values.Count == 0) return;

        // Column authority comes from the manifest result_binding column_map keys (guarded).
        var columns = values.Keys.Select(c => RequireColumn(c, "output_column")).ToList();
        var conflict = binding.ConflictColumns.Select(c => RequireColumn(c, "conflict_column")).ToList();

        var paramNames = columns.Select((_, i) => $"@v{i}").ToList();
        var assignments = columns.Where(c => !conflict.Contains(c)).Select(c => $"{c} = EXCLUDED.{c}").ToList();

        var conflictClause = conflict.Count == 0
            ? string.Empty
            : assignments.Count == 0
                ? $" ON CONFLICT ({string.Join(", ", conflict)}) DO NOTHING"
                : $" ON CONFLICT ({string.Join(", ", conflict)}) DO UPDATE SET {string.Join(", ", assignments)}";

        var sql = $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)}){conflictClause}";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        for (var i = 0; i < columns.Count; i++)
            cmd.Parameters.AddWithValue($"v{i}", values[columns[i]] ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<SchedulerJobRecord>> LoadSettingsProjectionAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var result = new List<SchedulerJobRecord>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns + """

            FROM topology.scheduler_jobs j
            ORDER BY j.job_key ASC
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(MapRecord(reader));

        return result;
    }

    public async Task<Guid> CreateJobAsync(SchedulerJobDraft draft, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.scheduler_jobs
                (job_key, trigger_kind, schedule_policy_kind, cron_expression,
                 schedule_interval_seconds, manual_run_allowed, active, authority_scope,
                 max_batch_size, lease_seconds, credential_requirement_ref, external_port_ref,
                 created_by, updated_at)
            VALUES
                (@jobKey, @triggerKind, @policyKind, @cron,
                 @interval, @manual, @active, @authority,
                 @batch, @lease, @credRef, @portRef,
                 'admin', now())
            RETURNING scheduler_job_id
            """;
        BindDraft(cmd, draft);
        var id = await cmd.ExecuteScalarAsync(ct);
        return (Guid)id!;
    }

    public async Task<bool> UpdateJobAsync(Guid schedulerJobId, SchedulerJobDraft draft, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        // Editable only while inactive (disabled) or draft — active manifests are not edited in place.
        cmd.CommandText = """
            UPDATE topology.scheduler_jobs
            SET job_key = @jobKey, trigger_kind = @triggerKind, schedule_policy_kind = @policyKind,
                cron_expression = @cron, schedule_interval_seconds = @interval,
                manual_run_allowed = @manual, authority_scope = @authority,
                max_batch_size = @batch, lease_seconds = @lease,
                credential_requirement_ref = @credRef, external_port_ref = @portRef,
                updated_at = now()
            WHERE scheduler_job_id = @id AND active = false
            """;
        cmd.Parameters.AddWithValue("id", schedulerJobId);
        BindDraft(cmd, draft);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> SetJobActiveAsync(Guid schedulerJobId, bool active, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE topology.scheduler_jobs
            SET active = @active, updated_at = now()
            WHERE scheduler_job_id = @id
            """;
        cmd.Parameters.AddWithValue("id", schedulerJobId);
        cmd.Parameters.AddWithValue("active", active);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    private static void BindDraft(NpgsqlCommand cmd, SchedulerJobDraft draft)
    {
        cmd.Parameters.AddWithValue("jobKey", draft.JobKey);
        cmd.Parameters.AddWithValue("triggerKind", draft.TriggerKind);
        cmd.Parameters.AddWithValue("policyKind", draft.SchedulePolicyKind);
        cmd.Parameters.AddWithValue("cron", (object?)draft.CronExpression ?? DBNull.Value);
        cmd.Parameters.AddWithValue("interval", (object?)draft.ScheduleIntervalSeconds ?? DBNull.Value);
        cmd.Parameters.AddWithValue("manual", draft.ManualRunAllowed);
        cmd.Parameters.AddWithValue("active", draft.Active);
        cmd.Parameters.AddWithValue("authority", draft.AuthorityScope);
        cmd.Parameters.AddWithValue("batch", draft.MaxBatchSize);
        cmd.Parameters.AddWithValue("lease", draft.LeaseSeconds);
        cmd.Parameters.AddWithValue("credRef", (object?)draft.CredentialRequirementRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("portRef", (object?)draft.ExternalPortRef ?? DBNull.Value);
    }

    private const string SelectColumns = """
            SELECT j.scheduler_job_id, j.job_key, j.trigger_kind, j.schedule_policy_kind,
                   j.cron_expression, j.schedule_interval_seconds, j.manual_run_allowed, j.active,
                   j.input_table_ref, j.input_id_column, j.input_status_column, j.input_due_column,
                   j.input_status_pending_value, j.input_status_processing_value,
                   j.input_status_completed_value, j.input_status_failed_value,
                   j.input_status_skipped_value, j.input_status_retry_wait_value,
                   j.output_table_ref, j.timezone,
                   j.max_batch_size, j.lease_seconds, j.retry_policy, j.authority_scope,
                   j.credential_requirement_ref, j.external_port_ref, j.projection_policy
            """;

    private static SchedulerJobRecord MapRecord(NpgsqlDataReader reader)
    {
        var projectionPolicyJson = reader.IsDBNull(reader.GetOrdinal("projection_policy"))
            ? "{}"
            : reader.GetString(reader.GetOrdinal("projection_policy"));
        var projectionPolicy = ParseJsonObject(projectionPolicyJson);

        var retryPolicyJson = reader.IsDBNull(reader.GetOrdinal("retry_policy")) ? "{}" : reader.GetString(reader.GetOrdinal("retry_policy"));
        var (maxAttempts, backoffSeconds) = ParseRetryPolicy(retryPolicyJson);

        string? Str(string col) => reader.IsDBNull(reader.GetOrdinal(col)) ? null : reader.GetString(reader.GetOrdinal(col));

        return new SchedulerJobRecord(
            reader.GetGuid(reader.GetOrdinal("scheduler_job_id")),
            reader.GetString(reader.GetOrdinal("job_key")),
            reader.GetString(reader.GetOrdinal("trigger_kind")),
            reader.GetString(reader.GetOrdinal("schedule_policy_kind")),
            Str("cron_expression"),
            reader.IsDBNull(reader.GetOrdinal("schedule_interval_seconds")) ? null : reader.GetInt64(reader.GetOrdinal("schedule_interval_seconds")),
            reader.GetBoolean(reader.GetOrdinal("manual_run_allowed")),
            reader.GetBoolean(reader.GetOrdinal("active")),
            Str("input_table_ref"),
            Str("input_status_column"),
            Str("input_status_pending_value"),
            Str("input_status_processing_value"),
            Str("input_status_completed_value"),
            Str("input_status_failed_value"),
            reader.GetInt32(reader.GetOrdinal("max_batch_size")),
            reader.GetInt32(reader.GetOrdinal("lease_seconds")),
            reader.GetString(reader.GetOrdinal("authority_scope")),
            Str("credential_requirement_ref"),
            Str("external_port_ref"),
            projectionPolicy)
        {
            InputIdColumn = Str("input_id_column") ?? "id",
            InputDueColumn = Str("input_due_column"),
            InputStatusSkippedValue = Str("input_status_skipped_value"),
            InputStatusRetryWaitValue = Str("input_status_retry_wait_value"),
            OutputTableRef = Str("output_table_ref"),
            Timezone = Str("timezone") ?? "UTC",
            RetryMaxAttempts = maxAttempts,
            RetryBackoffSeconds = backoffSeconds,
        };
    }

    private static (int maxAttempts, int backoffSeconds) ParseRetryPolicy(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return (1, 0);
            var max = doc.RootElement.TryGetProperty("max_attempts", out var m) && m.TryGetInt32(out var mv) && mv > 0 ? mv : 1;
            var backoff = doc.RootElement.TryGetProperty("backoff_seconds", out var b) && b.TryGetInt32(out var bv) && bv >= 0 ? bv : 0;
            return (max, backoff);
        }
        catch (JsonException) { return (1, 0); }
    }

    private static IReadOnlyList<SchedulerStepInputBinding> ParseInputBindings(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return Array.Empty<SchedulerStepInputBinding>();
            var list = new List<SchedulerStepInputBinding>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                var source = prop.Value.TryGetProperty("source", out var s) ? s.GetString() : null;
                var path = prop.Value.TryGetProperty("path", out var p) ? p.GetString() : null;
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(path)) continue;
                list.Add(new SchedulerStepInputBinding(prop.Name, source!, path!));
            }
            return list;
        }
        catch (JsonException) { return Array.Empty<SchedulerStepInputBinding>(); }
    }

    private static SchedulerStepResultBinding? ParseResultBinding(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("kind", out var kindEl)) return null;
            var kind = kindEl.GetString();
            if (string.IsNullOrWhiteSpace(kind)) return null;

            var resultKey = doc.RootElement.TryGetProperty("result_context_key", out var rk) ? rk.GetString() ?? string.Empty : string.Empty;
            var conflict = new List<string>();
            if (doc.RootElement.TryGetProperty("conflict_columns", out var cc) && cc.ValueKind == JsonValueKind.Array)
                conflict.AddRange(cc.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Where(v => v.Length > 0));
            var columnMap = new Dictionary<string, string>(StringComparer.Ordinal);
            if (doc.RootElement.TryGetProperty("column_map", out var cm) && cm.ValueKind == JsonValueKind.Object)
                foreach (var entry in cm.EnumerateObject())
                    columnMap[entry.Name] = entry.Value.GetString() ?? string.Empty;

            return new SchedulerStepResultBinding(kind!, resultKey, conflict, columnMap);
        }
        catch (JsonException) { return null; }
    }

    private static string RequireTableRef(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || !TableRefPattern.IsMatch(value))
            throw new InvalidOperationException($"SCHEDULER_MANIFEST_TABLE_REF_INVALID: {field}='{value}'");
        return value;
    }

    private static string RequireColumn(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || !ColumnPattern.IsMatch(value))
            throw new InvalidOperationException($"SCHEDULER_MANIFEST_COLUMN_INVALID: {field}='{value}'");
        return value;
    }

    // projection_policy retains raw-JSON-text values (the runner deserializes
    // allowed_result_keys / notify_manifest_id from raw JSON text).
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

    // Input row columns are parsed into native CLR values for binding/upsert use.
    private static IReadOnlyDictionary<string, object?> ParseTypedJsonObject(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, object?>(StringComparer.Ordinal);

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number when property.Value.TryGetInt64(out var l) => l,
                JsonValueKind.Number => property.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText(),
            };
        return result;
    }
}
