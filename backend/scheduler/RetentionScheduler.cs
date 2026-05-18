using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Scheduler;

/// <summary>
/// Cron excitation trigger for the log retention package runtime.
///
/// Design boundary:
///   This class is the cron trigger layer — it wakes up on a schedule and calls
///   LogRetentionRuntime (the package runtime). All retention policy decisions
///   (cold_days, batch_size, archive_strategy, enabled, schedule_interval_hours)
///   live in function_parameters, not here.
///
///   In v1, package selection for the retention operation is trivially resolved
///   (LogRetentionRuntime is the only retention package). The trigger calls the
///   package runtime directly; it does not route through the user-facing dispatch
///   canonical route (stored_topology_data → user_operation → … → emission_or_projection)
///   because retention is a background maintenance operation, not a user dispatch.
///
/// This class contains no retention business logic.
/// Disabled / MissingPolicy / MalformedPolicy results are logged and retried;
/// the service does not crash on policy errors (the topology may not be seeded yet
/// at startup).
///
/// Bootstrap delay: waits RETENTION_STARTUP_DELAY_SECONDS (env, default 30) before
/// the first run to allow the database to be ready. When policy is missing or malformed,
/// retries after RETENTION_BOOTSTRAP_RETRY_HOURS (env, default 1).
/// </summary>
public class RetentionScheduler : BackgroundService
{
    private static readonly TimeSpan DefaultStartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultBootstrapRetry = TimeSpan.FromHours(1);

    private readonly ILogger<RetentionScheduler> _logger;
    private readonly LogRetentionRuntime _retentionRuntime;

    public RetentionScheduler(
        ILogger<RetentionScheduler> logger,
        LogRetentionRuntime retentionRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retentionRuntime = retentionRuntime ?? throw new ArgumentNullException(nameof(retentionRuntime));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupDelay = ParseEnvSeconds("RETENTION_STARTUP_DELAY_SECONDS", DefaultStartupDelay);
        _logger.LogInformation(
            "RetentionScheduler: starting — startup delay {Delay}.", startupDelay);
        await Task.Delay(startupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            RetentionRunResult result;
            try
            {
                result = await _retentionRuntime.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RetentionScheduler: unhandled exception from LogRetentionRuntime.");
                result = new RetentionRunResult(RetentionExecutionStatus.MalformedPolicy,
                    ex.Message, 0, DateTimeOffset.UtcNow, 0);
            }

            var nextInterval = result.ScheduleIntervalHours > 0
                ? TimeSpan.FromHours(result.ScheduleIntervalHours)
                : ParseEnvHours("RETENTION_BOOTSTRAP_RETRY_HOURS", DefaultBootstrapRetry);

            _logger.LogInformation(
                "RetentionScheduler: execution complete — status={Status} rows={Rows} nextIn={Next}.",
                result.Status, result.RowsAffected, nextInterval);

            try
            {
                await Task.Delay(nextInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("RetentionScheduler: stopped.");
    }

    private static TimeSpan ParseEnvSeconds(string envVar, TimeSpan fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(raw, out var v) && v > 0
            ? TimeSpan.FromSeconds(v)
            : fallback;
    }

    private static TimeSpan ParseEnvHours(string envVar, TimeSpan fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(raw, out var v) && v > 0
            ? TimeSpan.FromHours(v)
            : fallback;
    }
}
