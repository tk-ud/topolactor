using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Runtime;

namespace Topolactor.Scheduler;

/// <summary>
/// DB-backed scheduler job manifest substrate runner.
///
/// Per SSOT scheduler_job_manifest_substrate:
///   Owns: due判定 / input lease / input row lifecycle / run ledger / ordered step
///         dispatch / result_binding output upsert / on_error + retry status transition.
///   Not owns: trigger_alignment (RuntimeTimelineScheduler) / domain job body / credential plaintext.
///
/// Security boundary:
///   - authority_scope, input_table_ref, output_table_ref, input/output columns, and status
///     values are seed/admin-authored manifest authority — never payload-derived.
///   - result_context written to DB is sanitized by projection_policy.allowed_result_keys.
///   - credential plaintext / token body / decrypted payload must NOT appear in run log.
///
/// on_error vocabulary (SSOT-aligned): fail_run | retry | skip_input | mark_input_failed.
///
/// Distinct from RuntimeTimelineScheduler's in-memory trigger alignment queue.
/// </summary>
public sealed class SchedulerJobRunner : BackgroundService
{
    private static readonly TimeSpan DefaultStartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(60);

    private readonly ILogger<SchedulerJobRunner> _logger;
    private readonly ISchedulerJobManifestRepository _manifestRepository;
    private readonly AbstractFunctionExecutor _abstractFunctionExecutor;
    private readonly DbNotifyRepository? _dbNotifyRepository;

    public SchedulerJobRunner(
        ILogger<SchedulerJobRunner> logger,
        ISchedulerJobManifestRepository manifestRepository,
        AbstractFunctionExecutor abstractFunctionExecutor,
        DbNotifyRepository? dbNotifyRepository = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _abstractFunctionExecutor = abstractFunctionExecutor ?? throw new ArgumentNullException(nameof(abstractFunctionExecutor));
        _dbNotifyRepository = dbNotifyRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupDelay = ParseEnvSeconds("SCHEDULER_JOB_RUNNER_STARTUP_DELAY_SECONDS", DefaultStartupDelay);
        _logger.LogInformation("SchedulerJobRunner: starting — startup delay {Delay}.", startupDelay);

        try { await Task.Delay(startupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        var pollInterval = ParseEnvSeconds("SCHEDULER_JOB_RUNNER_POLL_INTERVAL_SECONDS", DefaultPollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SchedulerJobRunner: unhandled exception in poll cycle.");
            }

            try { await Task.Delay(pollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("SchedulerJobRunner: stopped.");
    }

    internal async Task RunDueJobsAsync(CancellationToken ct)
    {
        var jobs = await _manifestRepository.LoadActiveJobsAsync(ct);
        _logger.LogDebug("SchedulerJobRunner: poll found {Count} active jobs.", jobs.Count);

        foreach (var job in jobs)
        {
            if (!IsJobDue(job))
            {
                _logger.LogDebug(
                    "SchedulerJobRunner: job={JobKey} policy={Policy} not due, skipping.",
                    job.JobKey, job.SchedulePolicyKind);
                continue;
            }

            try
            {
                await TryExecuteJobAsync(job, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SchedulerJobRunner: unhandled exception executing job={JobKey}.",
                    job.JobKey);
            }
        }
    }

    internal async Task TryExecuteJobAsync(SchedulerJobRecord job, CancellationToken ct)
    {
        var steps = await _manifestRepository.LoadStepsAsync(job.SchedulerJobId, ct);

        if (!job.HasInputSource)
        {
            await ExecuteInputlessRunAsync(job, steps, ct);
            return;
        }

        await ExecuteInputBatchAsync(job, steps, ct);
    }

    // ── Input-less run path (maintenance sweeps, projection/notify jobs) ────────
    private async Task ExecuteInputlessRunAsync(
        SchedulerJobRecord job, IReadOnlyList<SchedulerJobStepRecord> steps, CancellationToken ct)
    {
        var run = await CreateProcessingRunAsync(job, null, ct);

        var schedulerCtx = new SchedulerExecutionContext
        {
            SchedulerJobId = job.SchedulerJobId,
            JobKey = job.JobKey,
            SchedulerJobRunId = run.SchedulerJobRunId,
            TriggerKind = job.TriggerKind,
            SchedulePolicyKind = job.SchedulePolicyKind,
        };

        var (result, attempts) = await ExecuteStepChainWithRetryAsync(job, steps, schedulerCtx, ct);

        var runStatus = result.Outcome switch
        {
            StepChainOutcome.Completed => "completed",
            StepChainOutcome.Cancelled => "cancelled",
            _ => "failed",
        };

        var resultContextJson = BuildSanitizedResultContext(result.ResultContext, job.ProjectionPolicy);
        await _manifestRepository.UpdateRunStatusAsync(run.SchedulerJobRunId, runStatus, result.LastErrorJson, resultContextJson, ct);

        _logger.LogInformation(
            "SchedulerJobRunner: job={JobKey} run={RunId} completed status={Status} attempts={Attempts}.",
            job.JobKey, run.SchedulerJobRunId, runStatus, attempts);

        if (string.Equals(runStatus, "completed", StringComparison.Ordinal))
            await FireNotifyAsync(job.ProjectionPolicy, ct);
    }

    // ── Input-driven batch path (run-per-leased-input) ─────────────────────────
    private async Task ExecuteInputBatchAsync(
        SchedulerJobRecord job, IReadOnlyList<SchedulerJobStepRecord> steps, CancellationToken ct)
    {
        var leased = await _manifestRepository.LeaseDueInputRowsAsync(job, DateTimeOffset.UtcNow, ct);
        if (leased.Count == 0)
        {
            _logger.LogDebug("SchedulerJobRunner: job={JobKey} no due input rows leased.", job.JobKey);
            return;
        }

        _logger.LogInformation("SchedulerJobRunner: job={JobKey} leased {Count} input row(s).", job.JobKey, leased.Count);

        foreach (var input in leased)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessLeasedInputAsync(job, steps, input, ct);
        }
    }

    private async Task ProcessLeasedInputAsync(
        SchedulerJobRecord job, IReadOnlyList<SchedulerJobStepRecord> steps,
        SchedulerInputRow input, CancellationToken ct)
    {
        var run = await CreateProcessingRunAsync(job, input.InputId, ct);

        var schedulerCtx = new SchedulerExecutionContext
        {
            SchedulerJobId = job.SchedulerJobId,
            JobKey = job.JobKey,
            SchedulerJobRunId = run.SchedulerJobRunId,
            TriggerKind = job.TriggerKind,
            SchedulePolicyKind = job.SchedulePolicyKind,
            InputRef = input.InputId,
            InputId = input.InputId,
            InputRow = input.Columns,
        };

        var (result, attempts) = await ExecuteStepChainWithRetryAsync(job, steps, schedulerCtx, ct);

        // Resolve the final run status + input status value from the chain outcome.
        var (runStatus, inputStatusValue) = ResolveOutcomeStatuses(job, result.Outcome);

        if (inputStatusValue is not null)
            await _manifestRepository.UpdateInputRowStatusAsync(job, input.InputId, inputStatusValue, ct);

        var resultContextJson = BuildSanitizedResultContext(result.ResultContext, job.ProjectionPolicy);
        await _manifestRepository.UpdateRunStatusAsync(run.SchedulerJobRunId, runStatus, result.LastErrorJson, resultContextJson, ct);

        _logger.LogInformation(
            "SchedulerJobRunner: job={JobKey} run={RunId} input={InputId} status={Status} inputStatus={InputStatus} attempts={Attempts}.",
            job.JobKey, run.SchedulerJobRunId, input.InputId, runStatus, inputStatusValue, attempts);

        if (string.Equals(runStatus, "completed", StringComparison.Ordinal) && result.Outcome == StepChainOutcome.Completed)
            await FireNotifyAsync(job.ProjectionPolicy, ct);
    }

    private (string runStatus, string? inputStatusValue) ResolveOutcomeStatuses(SchedulerJobRecord job, StepChainOutcome outcome) =>
        outcome switch
        {
            StepChainOutcome.Completed       => ("completed", job.InputStatusCompletedValue),
            StepChainOutcome.SkipInput       => ("completed", job.InputStatusSkippedValue ?? job.InputStatusCompletedValue),
            StepChainOutcome.MarkInputFailed => ("completed", job.InputStatusFailedValue),
            StepChainOutcome.Retry           => ("failed", job.InputStatusRetryWaitValue ?? job.InputStatusFailedValue),
            StepChainOutcome.Cancelled       => ("cancelled", null),
            _                                => ("failed", job.InputStatusFailedValue), // FailRun
        };

    // ── Step chain execution with retry ────────────────────────────────────────
    private async Task<(StepChainResult result, int attempts)> ExecuteStepChainWithRetryAsync(
        SchedulerJobRecord job, IReadOnlyList<SchedulerJobStepRecord> steps,
        SchedulerExecutionContext schedulerCtx, CancellationToken ct)
    {
        var maxAttempts = job.RetryMaxAttempts > 0 ? job.RetryMaxAttempts : 1;
        var attempt = 0;
        StepChainResult result;

        do
        {
            attempt++;
            result = await ExecuteStepChainAsync(job, steps, schedulerCtx, ct);

            if (result.Outcome != StepChainOutcome.Retry || attempt >= maxAttempts)
                break;

            if (job.RetryBackoffSeconds > 0)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(job.RetryBackoffSeconds), ct); }
                catch (OperationCanceledException)
                {
                    return (new StepChainResult(StepChainOutcome.Cancelled, result.LastErrorJson, result.ResultContext), attempt);
                }
            }
        }
        while (true);

        return (result, attempt);
    }

    private async Task<StepChainResult> ExecuteStepChainAsync(
        SchedulerJobRecord job, IReadOnlyList<SchedulerJobStepRecord> steps,
        SchedulerExecutionContext schedulerCtx, CancellationToken ct)
    {
        var executionCtx = new AbstractFunctionExecutionContext(
            authorityScope: job.AuthorityScope,
            requiredRuntimeLane: "scheduler_job_runtime",
            schedulerContext: schedulerCtx);

        foreach (var step in steps.OrderBy(static s => s.StepOrder))
        {
            // Seed manifest-defined input bindings into result_context so the abstract
            // function (result_context source) can read leased-row / constant / prior values.
            SeedStepInputBindings(step, schedulerCtx, executionCtx);

            try
            {
                await _abstractFunctionExecutor.ExecuteAsync(step.AbstractFunctionKey, executionCtx, ct);

                // result_binding maps the accumulated result_context to a manifest-authorized
                // output upsert. Table/column authority is manifest-defined here.
                if (step.ResultBinding is { Kind: "output_upsert" } binding)
                    await ApplyOutputUpsertAsync(job, binding, executionCtx.ResultContext, ct);

                _logger.LogDebug(
                    "SchedulerJobRunner: job={JobKey} run={RunId} step={StepOrder} fn={FnKey} completed.",
                    job.JobKey, schedulerCtx.SchedulerJobRunId, step.StepOrder, step.AbstractFunctionKey);
            }
            catch (AbstractFunctionFailCloseException ex)
            {
                var outcome = MapOnError(step.OnError);
                var lastError = JsonSerializer.Serialize(new
                {
                    code = ex.Status,
                    message = ex.Message,
                    step = step.StepOrder,
                    on_error = step.OnError,
                });

                _logger.LogWarning(
                    "SchedulerJobRunner: job={JobKey} run={RunId} step={StepOrder} fn={FnKey} fail-close status={Status} on_error={OnError}.",
                    job.JobKey, schedulerCtx.SchedulerJobRunId, step.StepOrder, step.AbstractFunctionKey, ex.Status, step.OnError);

                return new StepChainResult(outcome, lastError, executionCtx.ResultContext);
            }
            catch (OperationCanceledException)
            {
                var lastError = JsonSerializer.Serialize(new { code = "CANCELLED", message = "Run cancelled by service stop." });
                return new StepChainResult(StepChainOutcome.Cancelled, lastError, executionCtx.ResultContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SchedulerJobRunner: job={JobKey} run={RunId} step={StepOrder} fn={FnKey} unexpected error.",
                    job.JobKey, schedulerCtx.SchedulerJobRunId, step.StepOrder, step.AbstractFunctionKey);

                var lastError = JsonSerializer.Serialize(new { code = "UNEXPECTED_ERROR", message = ex.Message, step = step.StepOrder });
                return new StepChainResult(StepChainOutcome.FailRun, lastError, executionCtx.ResultContext);
            }
        }

        return new StepChainResult(StepChainOutcome.Completed, null, executionCtx.ResultContext);
    }

    private void SeedStepInputBindings(
        SchedulerJobStepRecord step, SchedulerExecutionContext schedulerCtx, AbstractFunctionExecutionContext executionCtx)
    {
        foreach (var binding in step.InputBindings)
        {
            object? value = binding.Source switch
            {
                "input" => schedulerCtx.InputRow.TryGetValue(binding.Path, out var v) ? v : null,
                "constant" => binding.Path,
                "scheduler" => binding.Path switch
                {
                    "job_key" => schedulerCtx.JobKey,
                    "run_id" => schedulerCtx.SchedulerJobRunId.ToString(),
                    "input_id" => schedulerCtx.InputId,
                    _ => null,
                },
                "result_context" => executionCtx.ResultContext.TryGetValue(binding.Path, out var rv) ? rv : null,
                _ => null,
            };
            executionCtx.StoreResult(binding.ResultContextKey, value);
        }
    }

    private async Task ApplyOutputUpsertAsync(
        SchedulerJobRecord job, SchedulerStepResultBinding binding,
        IReadOnlyDictionary<string, object?> resultContext, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(job.OutputTableRef))
            throw new AbstractFunctionFailCloseException(
                AbstractFunctionFailCloseStatus.InvalidAuthority,
                "SCHEDULER_OUTPUT_UPSERT_NO_OUTPUT_TABLE_REF");

        if (binding.ColumnMap.Count == 0)
            throw new AbstractFunctionFailCloseException(
                AbstractFunctionFailCloseStatus.InvalidAuthority,
                "SCHEDULER_OUTPUT_UPSERT_COLUMN_MAP_MISSING");

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (column, path) in binding.ColumnMap)
            values[column] = ResolveResultValue(resultContext, path);

        await _manifestRepository.UpsertAuthorizedOutputAsync(job, binding, values, ct);
    }

    private static object? ResolveResultValue(IReadOnlyDictionary<string, object?> resultContext, string path)
    {
        var dot = path.IndexOf('.');
        if (dot < 0)
            return resultContext.TryGetValue(path, out var v) ? v : null;

        var head = path[..dot];
        var tail = path[(dot + 1)..];
        if (!resultContext.TryGetValue(head, out var nested)) return null;
        return nested switch
        {
            IReadOnlyDictionary<string, object?> dict => dict.TryGetValue(tail, out var nv) ? nv : null,
            IDictionary<string, object?> dict => dict.TryGetValue(tail, out var nv) ? nv : null,
            _ => null,
        };
    }

    private static StepChainOutcome MapOnError(string onError) => onError switch
    {
        "fail_run" => StepChainOutcome.FailRun,
        "retry" => StepChainOutcome.Retry,
        "skip_input" => StepChainOutcome.SkipInput,
        "mark_input_failed" => StepChainOutcome.MarkInputFailed,
        // No silent fallback: an unknown on_error vocabulary fails the run explicitly.
        _ => StepChainOutcome.FailRun,
    };

    private async Task<SchedulerJobRunRecord> CreateProcessingRunAsync(SchedulerJobRecord job, string? inputRef, CancellationToken ct)
    {
        var leaseUntil = DateTimeOffset.UtcNow.AddSeconds(job.LeaseSeconds);
        var run = await _manifestRepository.CreateRunAsync(
            job.SchedulerJobId, job.JobKey, job.TriggerKind, job.SchedulePolicyKind, inputRef, leaseUntil, ct);

        _logger.LogInformation(
            "SchedulerJobRunner: job={JobKey} run={RunId} created, status=processing.",
            job.JobKey, run.SchedulerJobRunId);

        await _manifestRepository.UpdateRunStatusAsync(run.SchedulerJobRunId, "processing", null, null, ct);
        return run;
    }

    private async Task FireNotifyAsync(IReadOnlyDictionary<string, object?> projectionPolicy, CancellationToken ct)
    {
        if (_dbNotifyRepository is null) return;
        if (!projectionPolicy.TryGetValue("notify_manifest_id", out var raw) || raw is not string rawJson) return;

        string? manifestIdStr;
        try { manifestIdStr = JsonSerializer.Deserialize<string>(rawJson); }
        catch { return; }

        if (!Guid.TryParse(manifestIdStr, out var manifestId)) return;

        await _dbNotifyRepository.NotifyAsync(null, null, manifestId, ct);
        _logger.LogDebug("SchedulerJobRunner: DB NOTIFY sent manifest_id={ManifestId}.", manifestId);
    }

    private static string? BuildSanitizedResultContext(
        IReadOnlyDictionary<string, object?> resultContext,
        IReadOnlyDictionary<string, object?> projectionPolicy)
    {
        if (resultContext.Count == 0) return null;

        if (!projectionPolicy.TryGetValue("allowed_result_keys", out var allowedRaw) || allowedRaw is not string keysJson)
            return null;

        string[]? allowedKeys;
        try { allowedKeys = JsonSerializer.Deserialize<string[]>(keysJson); }
        catch { return null; }

        if (allowedKeys is null || allowedKeys.Length == 0) return null;

        var sanitized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in allowedKeys)
        {
            if (resultContext.TryGetValue(key, out var val))
                sanitized[key] = val;
        }

        return sanitized.Count > 0 ? JsonSerializer.Serialize(sanitized) : null;
    }

    private static bool IsJobDue(SchedulerJobRecord job) =>
        job.SchedulePolicyKind switch
        {
            "manual_only" => false,
            "cron" => job.CronExpression is not null
                      && CronScheduleEvaluator.IsDue(job.CronExpression, DateTimeOffset.UtcNow)
                      && !HasRunThisSlot(job, DateTimeOffset.UtcNow),
            "interval_seconds" => IsIntervalDue(job),
            _ => false,
        };

    // Returns true when a completed run already exists in the current cron-minute slot,
    // preventing multiple executions within the same minute boundary.
    private static bool HasRunThisSlot(SchedulerJobRecord job, DateTimeOffset now) =>
        job.LastCompletedAt is { } last &&
        last.Year == now.Year && last.Month == now.Month && last.Day == now.Day &&
        last.Hour == now.Hour && last.Minute == now.Minute;

    private static bool IsIntervalDue(SchedulerJobRecord job)
    {
        if (job.ScheduleIntervalSeconds is not { } interval || interval <= 0) return false;
        if (job.LastCompletedAt is null) return true;
        return (DateTimeOffset.UtcNow - job.LastCompletedAt.Value).TotalSeconds >= interval;
    }

    private static TimeSpan ParseEnvSeconds(string envVar, TimeSpan fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(raw, out var v) && v > 0 ? TimeSpan.FromSeconds(v) : fallback;
    }

    private enum StepChainOutcome { Completed, FailRun, Retry, SkipInput, MarkInputFailed, Cancelled }

    private sealed record StepChainResult(
        StepChainOutcome Outcome,
        string? LastErrorJson,
        IReadOnlyDictionary<string, object?> ResultContext);
}
