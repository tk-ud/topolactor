using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Package runtime for context_event log retention cleanup.
/// Activated by the cron excitation trigger RetentionScheduler.
/// Loads retention policy entirely from function_parameters — no production defaults in code.
///
/// Design boundary:
///   RetentionScheduler is the cron excitation trigger.
///   LogRetentionRuntime is the package runtime selected for the retention operation.
///   Package selection is trivially resolved in v1: there is one retention package.
///   Policy (cold_days, batch_size, archive_strategy, enabled, schedule_interval_hours)
///   comes from function_parameters, not from runtime code.
///   This is intentionally a direct cron → package runtime path and does not route
///   through the user-facing dispatch canonical route
///   (stored_topology_data → user_operation → … → emission_or_projection).
///   Background maintenance operations are not user-facing dispatches.
///
/// Policy source: function_parameters (function_name='context_event_retention', parameter_key='retention_policy').
/// Supported archive_strategy values: "delete" (purge rows older than cold_days).
///
/// Explicit status on every execution:
///   Ok              — cleanup executed (rows_affected may be 0 if nothing expired)
///   Disabled        — policy.enabled = false; logged, not silently skipped
///   MissingPolicy   — no active function_parameters row found
///   MalformedPolicy — JSON parse failure, unsupported archive_strategy,
///                     or any required integer field ≤ 0
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

        if (!policy.Enabled)
        {
            _logger.LogInformation(
                "LogRetentionRuntime: Disabled — policy.enabled=false for '{Fn}/{Key}'.",
                FunctionName, PolicyKey);
            return new RetentionRunResult(RetentionExecutionStatus.Disabled,
                "Retention is disabled by policy (enabled=false).",
                0, executedAt, policy.ScheduleIntervalHours);
        }

        if (!string.Equals(policy.ArchiveStrategy, "delete", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "LogRetentionRuntime: MalformedPolicy — archive_strategy='{Strategy}' is not supported.",
                policy.ArchiveStrategy);
            return Fail(RetentionExecutionStatus.MalformedPolicy,
                $"archive_strategy='{policy.ArchiveStrategy}' is not a supported strategy. Supported: 'delete'.",
                executedAt, policy.ScheduleIntervalHours);
        }

        var rowsAffected = await _contextRouteRepository.DeleteOldContextEventsAsync(
            policy.ColdDays, policy.BatchSize, ct);

        _logger.LogInformation(
            "LogRetentionRuntime: Ok — deleted {Rows} context_event row(s) older than {ColdDays} days (batch_size={BatchSize}).",
            rowsAffected, policy.ColdDays, policy.BatchSize);

        return new RetentionRunResult(RetentionExecutionStatus.Ok,
            $"Deleted {rowsAffected} row(s) older than {policy.ColdDays} days.",
            rowsAffected, executedAt, policy.ScheduleIntervalHours);
    }

    private static RetentionRunResult Fail(
        RetentionExecutionStatus status, string detail,
        DateTimeOffset executedAt, int scheduleIntervalHours) =>
        new(status, detail, 0, executedAt, scheduleIntervalHours);
}
