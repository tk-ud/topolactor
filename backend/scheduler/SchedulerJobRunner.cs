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
///   Owns: due判定 / lease / run ledger / step dispatch / status transition
///   Not owns: trigger_alignment (RuntimeTimelineScheduler) / domain job body / credential plaintext
///
/// Security boundary:
///   - authority_scope, input_table_ref, output_table_ref, and status values are
///     seed/admin-authored manifest authority — not payload-derived.
///   - result_context written to DB is sanitized by projection_policy.allowed_result_keys.
///   - credential plaintext / token body / decrypted payload must NOT appear in run log.
///
/// Distinct from RuntimeTimelineScheduler's in-memory trigger alignment queue.
/// RuntimeTimelineScheduler is NOT modified by this class; this runner reads from DB
/// and dispatches via AbstractFunctionExecutor, not ManifestDispatcher.
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
        var leaseUntil = DateTimeOffset.UtcNow.AddSeconds(job.LeaseSeconds);
        var run = await _manifestRepository.CreateRunAsync(
            job.SchedulerJobId, job.JobKey, job.TriggerKind, job.SchedulePolicyKind,
            null, leaseUntil, ct);

        _logger.LogInformation(
            "SchedulerJobRunner: job={JobKey} run={RunId} created, status=processing.",
            job.JobKey, run.SchedulerJobRunId);

        await _manifestRepository.UpdateRunStatusAsync(run.SchedulerJobRunId, "processing", null, null, ct);

        var steps = await _manifestRepository.LoadStepsAsync(job.SchedulerJobId, ct);

        var schedulerCtx = new SchedulerExecutionContext
        {
            SchedulerJobId = job.SchedulerJobId,
            JobKey = job.JobKey,
            SchedulerJobRunId = run.SchedulerJobRunId,
            TriggerKind = job.TriggerKind,
            SchedulePolicyKind = job.SchedulePolicyKind,
        };

        var executionCtx = new AbstractFunctionExecutionContext(
            authorityScope: job.AuthorityScope,
            requiredRuntimeLane: "scheduler_job_runtime",
            schedulerContext: schedulerCtx);

        var runStatus = "completed";
        string? lastErrorJson = null;

        foreach (var step in steps.OrderBy(static s => s.StepOrder))
        {
            try
            {
                await _abstractFunctionExecutor.ExecuteAsync(step.AbstractFunctionKey, executionCtx, ct);
                _logger.LogDebug(
                    "SchedulerJobRunner: job={JobKey} run={RunId} step={StepOrder} fn={FnKey} completed.",
                    job.JobKey, run.SchedulerJobRunId, step.StepOrder, step.AbstractFunctionKey);
            }
            catch (AbstractFunctionFailCloseException ex)
            {
                _logger.LogWarning(
                    "SchedulerJobRunner: job={JobKey} run={RunId} step={StepOrder} fn={FnKey} fail-close status={Status} message={Message}.",
                    job.JobKey, run.SchedulerJobRunId, step.StepOrder, step.AbstractFunctionKey, ex.Status, ex.Message);

                if (string.Equals(step.OnError, "skip_step", StringComparison.Ordinal))
                {
                    lastErrorJson = JsonSerializer.Serialize(new { code = ex.Status, message = ex.Message, step = step.StepOrder, skipped = true });
                    continue;
                }

                lastErrorJson = JsonSerializer.Serialize(new { code = ex.Status, message = ex.Message, step = step.StepOrder });
                runStatus = "failed";
                break;
            }
            catch (OperationCanceledException)
            {
                lastErrorJson = JsonSerializer.Serialize(new { code = "CANCELLED", message = "Run cancelled by service stop." });
                runStatus = "cancelled";
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SchedulerJobRunner: job={JobKey} run={RunId} step={StepOrder} fn={FnKey} unexpected error.",
                    job.JobKey, run.SchedulerJobRunId, step.StepOrder, step.AbstractFunctionKey);

                lastErrorJson = JsonSerializer.Serialize(new { code = "UNEXPECTED_ERROR", message = ex.Message, step = step.StepOrder });
                runStatus = "failed";
                break;
            }
        }

        var resultContextJson = BuildSanitizedResultContext(executionCtx.ResultContext, job.ProjectionPolicy);

        await _manifestRepository.UpdateRunStatusAsync(
            run.SchedulerJobRunId, runStatus, lastErrorJson, resultContextJson, ct);

        _logger.LogInformation(
            "SchedulerJobRunner: job={JobKey} run={RunId} completed status={Status}.",
            job.JobKey, run.SchedulerJobRunId, runStatus);

        if (string.Equals(runStatus, "completed", StringComparison.Ordinal))
            await FireNotifyAsync(job.ProjectionPolicy, ct);
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
}
