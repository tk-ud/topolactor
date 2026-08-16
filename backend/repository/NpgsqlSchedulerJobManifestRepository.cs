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

    /// <summary>
    /// credential-management consumer_reference_binding (docs/design/admin-normal-surface-projection-
    /// seed-ssot.yaml surface_axes.admin.surfaces.credentials.categories.external_api_credential.
    /// consumer_reference_binding). Writes ONLY credential_requirement_ref / external_port_ref on the
    /// given scheduler_job_id, validated by existence against topology.external_credential_vault /
    /// external_access_ports / external_response_ports / external_hook_ports reference_key columns
    /// (never against payload-declared table/column authority). Never touches job_key, trigger_kind,
    /// schedule_policy_kind, step chain, or any other /admin/contents-authored field, and never writes
    /// the vault/port tables themselves.
    /// </summary>
    Task<SchedulerJobCredentialBindingResult> UpdateCredentialBindingAsync(
        Guid schedulerJobId, string? credentialRequirementRef, string? externalPortRef, CancellationToken ct = default);

    /// <summary>
    /// Read-only counterpart of <see cref="UpdateCredentialBindingAsync"/> — runs the identical
    /// scheduler_job/credential-vault/port existence checks without locking or writing anything.
    /// Used for the mutation_confirmation_contract preview/validate stage (payload.dryRun=true) so
    /// an invalid candidate is never reported "valid" without ever having been checked; the confirmed
    /// write path re-runs the identical checks itself rather than trusting this result as prior proof.
    /// Outcome.Updated here means "the candidate passes validation," not that any row changed.
    /// </summary>
    Task<SchedulerJobCredentialBindingResult> ValidateCredentialBindingAsync(
        Guid schedulerJobId, string? credentialRequirementRef, string? externalPortRef, CancellationToken ct = default);
}

public enum SchedulerJobCredentialBindingOutcome
{
    Updated,
    SchedulerJobNotFound,
    CredentialRequirementRefNotFound,
    ExternalPortRefNotFound,
}

public sealed record SchedulerJobCredentialBindingResult(
    SchedulerJobCredentialBindingOutcome Outcome,
    string? PreviousCredentialRequirementRef = null,
    string? PreviousExternalPortRef = null);

/// <summary>
/// Admin/seed-authored manifest draft. Holds only references and policy data —
/// never credential plaintext. Table/column/status authority is manifest data here.
/// Carries the full manifest: header + input source/lifecycle + output binding +
/// retry/projection policy + ordered steps[].
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
    string? ExternalPortRef)
{
    // Input source / lifecycle (manifest authority; never payload-derived).
    public string? InputTableRef { get; init; }
    public string InputIdColumn { get; init; } = "id";
    public string? InputStatusColumn { get; init; }
    public string? InputStatusPendingValue { get; init; }
    public string? InputStatusProcessingValue { get; init; }
    public string? InputStatusCompletedValue { get; init; }
    public string? InputStatusFailedValue { get; init; }
    public string? InputStatusSkippedValue { get; init; }
    public string? InputStatusRetryWaitValue { get; init; }
    public string? InputDueColumn { get; init; }

    // Output binding target.
    public string? OutputTableRef { get; init; }
    public string? Timezone { get; init; }

    // Raw JSON policy bodies (validated as objects by the parser).
    public string? RetryPolicyJson { get; init; }
    public string? ProjectionPolicyJson { get; init; }

    public IReadOnlyList<SchedulerJobStepDraft> Steps { get; init; } = Array.Empty<SchedulerJobStepDraft>();
}

/// <summary>One ordered step in an authored scheduler job manifest.</summary>
public sealed record SchedulerJobStepDraft(
    int StepOrder,
    string AbstractFunctionKey,
    string OnError,
    string? ResultContextKey,
    string InputBindingJson,
    string ResultBindingJson,
    string? AuthorityScope,
    bool Active);

public sealed class NpgsqlSchedulerJobManifestRepository : ISchedulerJobManifestRepository
{
    // Identifier guards. Table/column authority is manifest-defined; these guards ensure
    // the manifest-supplied identifiers are safe schema-qualified table / bare column names.
    private static readonly Regex TableRefPattern =
        new(@"^[a-z_][a-z0-9_]*\.[a-z_][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ColumnPattern =
        new(@"^[a-z_][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Schema-qualified table identifier guard (manifest authority; rejects raw SQL / injection).</summary>
    public static bool IsValidTableRef(string? value) => value is not null && TableRefPattern.IsMatch(value);

    /// <summary>Bare column identifier guard (manifest authority; rejects raw SQL / injection).</summary>
    public static bool IsValidColumn(string? value) => value is not null && ColumnPattern.IsMatch(value);

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
        await using var tx = await conn.BeginTransactionAsync(ct);

        Guid id;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO topology.scheduler_jobs
                    (job_key, trigger_kind, schedule_policy_kind, cron_expression,
                     schedule_interval_seconds, manual_run_allowed, active, authority_scope,
                     max_batch_size, lease_seconds, credential_requirement_ref, external_port_ref,
                     input_table_ref, input_id_column, input_status_column, input_due_column,
                     input_status_pending_value, input_status_processing_value,
                     input_status_completed_value, input_status_failed_value,
                     input_status_skipped_value, input_status_retry_wait_value,
                     output_table_ref, timezone, retry_policy, projection_policy,
                     created_by, updated_at)
                VALUES
                    (@jobKey, @triggerKind, @policyKind, @cron,
                     @interval, @manual, @active, @authority,
                     @batch, @lease, @credRef, @portRef,
                     @inputTable, @inputId, @inputStatusCol, @inputDueCol,
                     @pending, @processing, @completed, @failed, @skipped, @retryWait,
                     @outputTable, COALESCE(@timezone, 'UTC'),
                     COALESCE(@retryPolicy::jsonb, '{"max_attempts":3,"backoff_seconds":60}'::jsonb),
                     COALESCE(@projectionPolicy::jsonb, '{}'::jsonb),
                     'admin', now())
                RETURNING scheduler_job_id
                """;
            BindDraft(cmd, draft);
            id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        }

        await InsertStepsAsync(conn, tx, id, draft.Steps, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<bool> UpdateJobAsync(Guid schedulerJobId, SchedulerJobDraft draft, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        int affected;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            // Editable only while inactive (disabled) — active manifests are not edited in place.
            cmd.CommandText = """
                UPDATE topology.scheduler_jobs
                SET job_key = @jobKey, trigger_kind = @triggerKind, schedule_policy_kind = @policyKind,
                    cron_expression = @cron, schedule_interval_seconds = @interval,
                    manual_run_allowed = @manual, authority_scope = @authority,
                    max_batch_size = @batch, lease_seconds = @lease,
                    credential_requirement_ref = @credRef, external_port_ref = @portRef,
                    input_table_ref = @inputTable, input_id_column = @inputId,
                    input_status_column = @inputStatusCol, input_due_column = @inputDueCol,
                    input_status_pending_value = @pending, input_status_processing_value = @processing,
                    input_status_completed_value = @completed, input_status_failed_value = @failed,
                    input_status_skipped_value = @skipped, input_status_retry_wait_value = @retryWait,
                    output_table_ref = @outputTable, timezone = COALESCE(@timezone, 'UTC'),
                    retry_policy = COALESCE(@retryPolicy::jsonb, retry_policy),
                    projection_policy = COALESCE(@projectionPolicy::jsonb, projection_policy),
                    updated_at = now()
                WHERE scheduler_job_id = @id AND active = false
                """;
            cmd.Parameters.AddWithValue("id", schedulerJobId);
            BindDraft(cmd, draft);
            affected = await cmd.ExecuteNonQueryAsync(ct);
        }

        if (affected == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        // Replace the step chain atomically: clear existing steps, reinsert from the draft.
        await using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM topology.scheduler_job_steps WHERE scheduler_job_id = @id";
            del.Parameters.AddWithValue("id", schedulerJobId);
            await del.ExecuteNonQueryAsync(ct);
        }
        await InsertStepsAsync(conn, tx, schedulerJobId, draft.Steps, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private static async Task InsertStepsAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid jobId,
        IReadOnlyList<SchedulerJobStepDraft> steps, CancellationToken ct)
    {
        foreach (var step in steps)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO topology.scheduler_job_steps
                    (scheduler_job_id, step_order, abstract_function_key, input_binding,
                     result_context_key, result_binding, on_error, authority_scope, active)
                VALUES
                    (@jobId, @order, @fnKey, @inputBinding::jsonb,
                     @resultKey, @resultBinding::jsonb, @onError, @authority, @active)
                """;
            cmd.Parameters.AddWithValue("jobId", jobId);
            cmd.Parameters.AddWithValue("order", step.StepOrder);
            cmd.Parameters.AddWithValue("fnKey", step.AbstractFunctionKey);
            cmd.Parameters.AddWithValue("inputBinding", string.IsNullOrWhiteSpace(step.InputBindingJson) ? "{}" : step.InputBindingJson);
            cmd.Parameters.AddWithValue("resultKey", (object?)step.ResultContextKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("resultBinding", string.IsNullOrWhiteSpace(step.ResultBindingJson) ? "{}" : step.ResultBindingJson);
            cmd.Parameters.AddWithValue("onError", step.OnError);
            cmd.Parameters.AddWithValue("authority", (object?)step.AuthorityScope ?? DBNull.Value);
            cmd.Parameters.AddWithValue("active", step.Active);
            await cmd.ExecuteNonQueryAsync(ct);
        }
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

    public async Task<SchedulerJobCredentialBindingResult> UpdateCredentialBindingAsync(
        Guid schedulerJobId, string? credentialRequirementRef, string? externalPortRef, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        bool found;
        string? previousCredRef = null;
        string? previousPortRef = null;
        await using (var current = conn.CreateCommand())
        {
            current.Transaction = tx;
            current.CommandText =
                "SELECT credential_requirement_ref, external_port_ref FROM topology.scheduler_jobs " +
                "WHERE scheduler_job_id = @id FOR UPDATE";
            current.Parameters.AddWithValue("id", schedulerJobId);
            // Reader must be fully disposed before any further command (e.g. RollbackAsync below)
            // runs on the same connection -- Npgsql fails closed with
            // NpgsqlOperationInProgressException if a rollback is attempted while a reader from
            // this same connection is still open.
            await using (var reader = await current.ExecuteReaderAsync(ct))
            {
                found = await reader.ReadAsync(ct);
                if (found)
                {
                    previousCredRef = reader.IsDBNull(0) ? null : reader.GetString(0);
                    previousPortRef = reader.IsDBNull(1) ? null : reader.GetString(1);
                }
            }
        }
        if (!found)
        {
            await tx.RollbackAsync(ct);
            return new SchedulerJobCredentialBindingResult(SchedulerJobCredentialBindingOutcome.SchedulerJobNotFound);
        }

        if (credentialRequirementRef is not null)
        {
            await using var vaultCheck = conn.CreateCommand();
            vaultCheck.Transaction = tx;
            vaultCheck.CommandText =
                "SELECT 1 FROM topology.external_credential_vault WHERE reference_key = @ref";
            vaultCheck.Parameters.AddWithValue("ref", credentialRequirementRef);
            if (await vaultCheck.ExecuteScalarAsync(ct) is null)
            {
                await tx.RollbackAsync(ct);
                return new SchedulerJobCredentialBindingResult(
                    SchedulerJobCredentialBindingOutcome.CredentialRequirementRefNotFound);
            }
        }

        if (externalPortRef is not null)
        {
            await using var portCheck = conn.CreateCommand();
            portCheck.Transaction = tx;
            portCheck.CommandText = """
                SELECT 1 FROM topology.external_access_ports WHERE reference_key = @ref
                UNION ALL
                SELECT 1 FROM topology.external_response_ports WHERE reference_key = @ref
                UNION ALL
                SELECT 1 FROM topology.external_hook_ports WHERE reference_key = @ref
                LIMIT 1
                """;
            portCheck.Parameters.AddWithValue("ref", externalPortRef);
            if (await portCheck.ExecuteScalarAsync(ct) is null)
            {
                await tx.RollbackAsync(ct);
                return new SchedulerJobCredentialBindingResult(
                    SchedulerJobCredentialBindingOutcome.ExternalPortRefNotFound);
            }
        }

        await using (var update = conn.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText = """
                UPDATE topology.scheduler_jobs
                SET credential_requirement_ref = @credRef, external_port_ref = @portRef, updated_at = now()
                WHERE scheduler_job_id = @id
                """;
            update.Parameters.AddWithValue("id", schedulerJobId);
            update.Parameters.AddWithValue("credRef", (object?)credentialRequirementRef ?? DBNull.Value);
            update.Parameters.AddWithValue("portRef", (object?)externalPortRef ?? DBNull.Value);
            await update.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new SchedulerJobCredentialBindingResult(
            SchedulerJobCredentialBindingOutcome.Updated, previousCredRef, previousPortRef);
    }

    public async Task<SchedulerJobCredentialBindingResult> ValidateCredentialBindingAsync(
        Guid schedulerJobId, string? credentialRequirementRef, string? externalPortRef, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using (var jobCheck = conn.CreateCommand())
        {
            jobCheck.CommandText = "SELECT 1 FROM topology.scheduler_jobs WHERE scheduler_job_id = @id";
            jobCheck.Parameters.AddWithValue("id", schedulerJobId);
            if (await jobCheck.ExecuteScalarAsync(ct) is null)
                return new SchedulerJobCredentialBindingResult(SchedulerJobCredentialBindingOutcome.SchedulerJobNotFound);
        }

        if (credentialRequirementRef is not null)
        {
            await using var vaultCheck = conn.CreateCommand();
            vaultCheck.CommandText =
                "SELECT 1 FROM topology.external_credential_vault WHERE reference_key = @ref";
            vaultCheck.Parameters.AddWithValue("ref", credentialRequirementRef);
            if (await vaultCheck.ExecuteScalarAsync(ct) is null)
                return new SchedulerJobCredentialBindingResult(
                    SchedulerJobCredentialBindingOutcome.CredentialRequirementRefNotFound);
        }

        if (externalPortRef is not null)
        {
            await using var portCheck = conn.CreateCommand();
            portCheck.CommandText = """
                SELECT 1 FROM topology.external_access_ports WHERE reference_key = @ref
                UNION ALL
                SELECT 1 FROM topology.external_response_ports WHERE reference_key = @ref
                UNION ALL
                SELECT 1 FROM topology.external_hook_ports WHERE reference_key = @ref
                LIMIT 1
                """;
            portCheck.Parameters.AddWithValue("ref", externalPortRef);
            if (await portCheck.ExecuteScalarAsync(ct) is null)
                return new SchedulerJobCredentialBindingResult(
                    SchedulerJobCredentialBindingOutcome.ExternalPortRefNotFound);
        }

        return new SchedulerJobCredentialBindingResult(SchedulerJobCredentialBindingOutcome.Updated);
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
        cmd.Parameters.AddWithValue("inputTable", (object?)draft.InputTableRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("inputId", draft.InputIdColumn);
        cmd.Parameters.AddWithValue("inputStatusCol", (object?)draft.InputStatusColumn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("inputDueCol", (object?)draft.InputDueColumn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pending", (object?)draft.InputStatusPendingValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("processing", (object?)draft.InputStatusProcessingValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("completed", (object?)draft.InputStatusCompletedValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("failed", (object?)draft.InputStatusFailedValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("skipped", (object?)draft.InputStatusSkippedValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("retryWait", (object?)draft.InputStatusRetryWaitValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("outputTable", (object?)draft.OutputTableRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("timezone", (object?)draft.Timezone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("retryPolicy", (object?)draft.RetryPolicyJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("projectionPolicy", (object?)draft.ProjectionPolicyJson ?? DBNull.Value);
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
