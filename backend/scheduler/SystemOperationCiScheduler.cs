using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Scheduler;

/// <summary>
/// Cron excitation trigger for system operation CI continuity inspections.
///
/// Design boundary:
///   This class is the cron trigger layer — it wakes up on a schedule and calls
///   SystemOperationCiRuntime's three continuity inspection methods. All inspection
///   logic and invariant thresholds live in SystemOperationCiRuntime, not here.
///
///   This trigger does not route through the user-facing dispatch canonical route
///   (stored_topology_data -> user_operation -> ... -> emission_or_projection) because
///   continuity inspection is a background maintenance operation, not a user dispatch.
///
/// Inspection methods called:
///   InspectHubAttentionContinuityAsync  — checks hub attention score/EMA finiteness
///   InspectCurrentRebuildabilityAsync   — checks rebuild source availability
///   InspectRegistryContinuityAsync      — checks for orphaned registry tokens
///
/// Failure handling:
///   Each inspection is independent. A repository exception in one is logged as Error
///   and does not block the remaining inspections. Service does not crash on inspection
///   failures. OperationCanceledException is re-thrown for clean shutdown.
///
/// Log severity:
///   Pass:     LogInformation — no action required.
///   Gap:      LogWarning    — reportable diagnostic; caller may continue.
///   Blocking: LogError      — system invariant violated; requires investigation.
///
/// Bootstrap delay: waits SYSTEM_CI_STARTUP_DELAY_SECONDS (env, 1-3600, default 30) before
/// the first run to allow the database to be ready.
/// Run interval: SYSTEM_CI_INTERVAL_HOURS (env, 1-24, default 1) between runs.
///
/// Diagnostic results are emitted via structured logging only — not persisted to DB.
/// </summary>
public class SystemOperationCiScheduler : BackgroundService
{
    private static readonly TimeSpan DefaultStartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(1);

    private readonly ILogger<SystemOperationCiScheduler> _logger;
    private readonly SystemOperationCiRuntime _ciRuntime;
    private readonly OutputLaneRouter? _outputLaneRouter;

    public SystemOperationCiScheduler(
        ILogger<SystemOperationCiScheduler> logger,
        SystemOperationCiRuntime ciRuntime,
        OutputLaneRouter? outputLaneRouter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ciRuntime = ciRuntime ?? throw new ArgumentNullException(nameof(ciRuntime));
        _outputLaneRouter = outputLaneRouter;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var laneConsumerTask = RunAttractorSignalConsumerAsync(stoppingToken);

        var startupDelay = ParseEnvSeconds("SYSTEM_CI_STARTUP_DELAY_SECONDS", DefaultStartupDelay);
        _logger.LogInformation(
            "SystemOperationCiScheduler: starting — startup delay {Delay}.", startupDelay);

        try
        {
            await Task.Delay(startupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SystemOperationCiScheduler: stopped during startup delay.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunInspectionsAsync(stoppingToken);

            var interval = ParseEnvHours("SYSTEM_CI_INTERVAL_HOURS", DefaultInterval);
            _logger.LogInformation(
                "SystemOperationCiScheduler: run complete — next run in {Interval}.", interval);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await laneConsumerTask;
        _logger.LogInformation("SystemOperationCiScheduler: stopped.");
    }

    // internal for test access (compiled into the same test assembly via Compile Include)
    internal async Task RunInspectionsAsync(CancellationToken ct)
    {
        await RunSingleInspectionAsync(
            () => _ciRuntime.InspectHubAttentionContinuityAsync(ct),
            "InspectHubAttentionContinuityAsync",
            ct);

        await RunSingleInspectionAsync(
            () => _ciRuntime.InspectCurrentRebuildabilityAsync(ct),
            "InspectCurrentRebuildabilityAsync",
            ct);

        await RunSingleInspectionAsync(
            () => _ciRuntime.InspectRegistryContinuityAsync(ct),
            "InspectRegistryContinuityAsync",
            ct);
    }

    private async Task RunSingleInspectionAsync(
        Func<Task<SystemCiDiagnosticResult>> inspect,
        string methodName,
        CancellationToken ct)
    {
        try
        {
            var result = await inspect();
            LogResult(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SystemOperationCiScheduler: {Method} failed — inspection skipped for this run.",
                methodName);
        }
    }

    private async Task RunAttractorSignalConsumerAsync(CancellationToken ct)
    {
        if (_outputLaneRouter is null)
        {
            _logger.LogDebug("SystemOperationCiScheduler: attractor signal consumer disabled (no OutputLaneRouter).");
            return;
        }

        await foreach (var signal in _outputLaneRouter.RebuildSignalChannel.Reader.ReadAllAsync(ct))
        {
            _logger.LogInformation(
                "SystemOperationCiScheduler: received registry_attractor_update signal AttractorKey={AttractorKey} ManifestId={ManifestId}.",
                signal.AttractorKey,
                signal.ManifestId);

            await RunSingleInspectionAsync(
                () => _ciRuntime.InspectCurrentRebuildabilityAsync(ct),
                "InspectCurrentRebuildabilityAsync(signal)",
                ct);
        }
    }

    private void LogResult(SystemCiDiagnosticResult result)
    {
        if (result.OverallStatus == SystemCiStatus.Pass)
        {
            _logger.LogInformation(
                "SystemOperationCiScheduler: target={Target} kind={Kind} " +
                "status=Pass findings=0 inspectedAt={InspectedAt}.",
                result.InspectionTarget, result.InspectionKind, result.InspectedAt);
            return;
        }

        _logger.LogInformation(
            "SystemOperationCiScheduler: target={Target} kind={Kind} " +
            "status={Status} findings={FindingCount} inspectedAt={InspectedAt}.",
            result.InspectionTarget, result.InspectionKind,
            result.OverallStatus, result.Findings.Count, result.InspectedAt);

        foreach (var f in result.Findings)
        {
            if (f.Status == SystemCiStatus.Blocking)
            {
                _logger.LogError(
                    "SystemOperationCiScheduler: BLOCKING target={Target} " +
                    "check={Check} detail={Detail} targetId={TargetId}.",
                    result.InspectionTarget, f.CheckName, f.Detail, f.TargetId);
            }
            else
            {
                _logger.LogWarning(
                    "SystemOperationCiScheduler: GAP target={Target} " +
                    "check={Check} detail={Detail} targetId={TargetId}.",
                    result.InspectionTarget, f.CheckName, f.Detail, f.TargetId);
            }
        }
    }

    // Valid range: 1-3600 seconds (1 second to 1 hour). Outside range -> fallback.
    internal static TimeSpan ParseEnvSeconds(string envVar, TimeSpan fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(raw, out var v) && v >= 1 && v <= 3600
            ? TimeSpan.FromSeconds(v)
            : fallback;
    }

    // Valid range: 1-24 hours. Outside range -> fallback.
    internal static TimeSpan ParseEnvHours(string envVar, TimeSpan fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(raw, out var v) && v >= 1 && v <= 24
            ? TimeSpan.FromHours(v)
            : fallback;
    }
}
