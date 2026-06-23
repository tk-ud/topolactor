using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Package runtime for context_event log retention cleanup.
/// Dispatched through the scheduler job manifest substrate via the log_retention
/// abstract function primitive (LogRetentionPrimitiveAdapter). The former
/// RetentionScheduler BackgroundService has been absorbed into a seed scheduler job
/// manifest (log_retention_sweep, interval_seconds).
/// Loads retention policy entirely from function_parameters — no production defaults in code.
///
/// Design boundary:
///   The scheduler substrate (SchedulerJobRunner) is the domain-independent cron driver.
///   LogRetentionRuntime is the package runtime selected for the retention operation,
///   reached only through the log_retention primitive — the scheduler knows nothing
///   about retention policy, tables, or columns.
///   Package selection is trivially resolved in v1: there is one retention package.
///   Policy (cold_days, hot_days, batch_size, archive_strategy, enabled, schedule_interval_hours)
///   comes from function_parameters, not from runtime code.
///
/// Policy source: function_parameters (function_name='context_event_retention', parameter_key='retention_policy').
/// Supported archive_strategy values:
///   "delete"  — purge rows older than cold_days (and outside the hot_days window).
///   "archive" — move rows to context_event_cold cold storage before removing from the hot table.
///
/// hot_days: optional safety floor. If set, events within the hot window are never touched,
/// even if cold_days would otherwise include them. If present, must be a positive integer.
///
/// Explicit status on every execution:
///   Ok              — cleanup executed (rows_affected may be 0 if nothing expired)
///   Disabled        — policy.enabled = false; logged, not silently skipped
///   MissingPolicy   — no active function_parameters row found
///   MalformedPolicy — JSON parse failure, unsupported archive_strategy,
///                     any required integer field ≤ 0, or hot_days present but ≤ 0
/// </summary>
public class LogRetentionRuntime
{
    private const string FunctionName = "context_event_retention";
    private const string PolicyKey = "retention_policy";

    private readonly ILogger<LogRetentionRuntime> _logger;
    private readonly TopologyRepository _topologyRepository;
    private readonly ContextRouteRepository _contextRouteRepository;

    public LogRetentionRuntime(
        ILogger<LogRetentionRuntime> logger,
        TopologyRepository topologyRepository,
        ContextRouteRepository contextRouteRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _contextRouteRepository = contextRouteRepository ?? throw new ArgumentNullException(nameof(contextRouteRepository));
    }

    /// <summary>
    /// Executes one retention cycle. Returns explicit status — never silent fallback.
    /// </summary>
    public async Task<RetentionRunResult> ExecuteAsync(CancellationToken ct = default)
    {
        var executedAt = DateTimeOffset.UtcNow;

        var policyJson = await _topologyRepository.LoadFunctionParameterAsync(FunctionName, PolicyKey, ct);
        if (policyJson is null)
        {
            _logger.LogError(
                "LogRetentionRuntime: MissingPolicy — no active function_parameters row for '{Fn}/{Key}'.",
                FunctionName, PolicyKey);
            return Fail(RetentionExecutionStatus.MissingPolicy,
                $"No active function_parameters row for '{FunctionName}/{PolicyKey}'.",
                executedAt, 0);
        }

        ContextEventRetentionPolicy policy;
        try
        {
            policy = JsonSerializer.Deserialize<ContextEventRetentionPolicy>(policyJson)
                ?? throw new JsonException("Deserialized to null.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "LogRetentionRuntime: MalformedPolicy — '{Fn}/{Key}' could not be parsed.",
                FunctionName, PolicyKey);
            return Fail(RetentionExecutionStatus.MalformedPolicy,
                $"Policy JSON for '{FunctionName}/{PolicyKey}' could not be parsed: {ex.Message}",
                executedAt, 0);
        }

        // Explicit validation — deserialize defaults (0 for int, null for string) are not
        // valid production values and must not silently proceed.
        if (policy.ColdDays <= 0)
        {
            _logger.LogError(
                "LogRetentionRuntime: MalformedPolicy — cold_days={ColdDays} must be a positive integer.",
                policy.ColdDays);
            return Fail(RetentionExecutionStatus.MalformedPolicy,
                $"cold_days={policy.ColdDays} is invalid: must be a positive integer.",
                executedAt, 0);
        }

        if (policy.BatchSize <= 0)
        {
            _logger.LogError(
                "LogRetentionRuntime: MalformedPolicy — batch_size={BatchSize} must be a positive integer.",
                policy.BatchSize);
            return Fail(RetentionExecutionStatus.MalformedPolicy,
                $"batch_size={policy.BatchSize} is invalid: must be a positive integer.",
                executedAt, 0);
        }

        if (policy.ScheduleIntervalHours <= 0)
        {
            _logger.LogError(
                "LogRetentionRuntime: MalformedPolicy — schedule_interval_hours={Hours} must be a positive integer.",
                policy.ScheduleIntervalHours);
            return Fail(RetentionExecutionStatus.MalformedPolicy,
                $"schedule_interval_hours={policy.ScheduleIntervalHours} is invalid: must be a positive integer.",
                executedAt, 0);
        }

        if (string.IsNullOrWhiteSpace(policy.ArchiveStrategy))
        {
            _logger.LogError("LogRetentionRuntime: MalformedPolicy — archive_strategy is missing or empty.");
            return Fail(RetentionExecutionStatus.MalformedPolicy,
                "archive_strategy is missing or empty.",
                executedAt, policy.ScheduleIntervalHours);
        }

        if (policy.HotDays.HasValue && policy.HotDays.Value <= 0)
        {
            _logger.LogError(
                "LogRetentionRuntime: MalformedPolicy — hot_days={HotDays} must be a positive integer.",
                policy.HotDays);
            return Fail(RetentionExecutionStatus.MalformedPolicy,
                $"hot_days={policy.HotDays} is invalid: must be a positive integer.",
                executedAt, policy.ScheduleIntervalHours);
        }

        if (!policy.Enabled)
        {
            _logger.LogInformation(
                "LogRetentionRuntime: Disabled — policy.enabled=false for '{Fn}/{Key}'.",
                FunctionName, PolicyKey);
            return new RetentionRunResult(RetentionExecutionStatus.Disabled,
                "Retention is disabled by policy (enabled=false).",
                0, executedAt, policy.ScheduleIntervalHours);
        }

        if (string.Equals(policy.ArchiveStrategy, "delete", StringComparison.OrdinalIgnoreCase))
        {
            var rowsAffected = await _contextRouteRepository.DeleteOldContextEventsAsync(
                policy.ColdDays, policy.BatchSize, policy.HotDays, ct);

            _logger.LogInformation(
                "LogRetentionRuntime: Ok — deleted {Rows} context_event row(s) older than {ColdDays} days (batch_size={BatchSize}, hot_days={HotDays}).",
                rowsAffected, policy.ColdDays, policy.BatchSize, policy.HotDays);

            return new RetentionRunResult(RetentionExecutionStatus.Ok,
                $"Deleted {rowsAffected} row(s) older than {policy.ColdDays} days.",
                rowsAffected, executedAt, policy.ScheduleIntervalHours);
        }

        if (string.Equals(policy.ArchiveStrategy, "archive", StringComparison.OrdinalIgnoreCase))
        {
            var rowsAffected = await _contextRouteRepository.ArchiveOldContextEventsAsync(
                policy.ColdDays, policy.BatchSize, policy.HotDays, ct);

            _logger.LogInformation(
                "LogRetentionRuntime: Ok — archived {Rows} context_event row(s) older than {ColdDays} days to cold storage (batch_size={BatchSize}, hot_days={HotDays}).",
                rowsAffected, policy.ColdDays, policy.BatchSize, policy.HotDays);

            return new RetentionRunResult(RetentionExecutionStatus.Ok,
                $"Archived {rowsAffected} row(s) older than {policy.ColdDays} days to cold storage.",
                rowsAffected, executedAt, policy.ScheduleIntervalHours);
        }

        _logger.LogError(
            "LogRetentionRuntime: MalformedPolicy — archive_strategy='{Strategy}' is not supported.",
            policy.ArchiveStrategy);
        return Fail(RetentionExecutionStatus.MalformedPolicy,
            $"archive_strategy='{policy.ArchiveStrategy}' is not a supported strategy. Supported: 'delete', 'archive'.",
            executedAt, policy.ScheduleIntervalHours);
    }

    private static RetentionRunResult Fail(
        RetentionExecutionStatus status, string detail,
        DateTimeOffset executedAt, int scheduleIntervalHours) =>
        new(status, detail, 0, executedAt, scheduleIntervalHours);
}
