using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Package runtime for context_event log retention cleanup.
/// Activated by a cron excitation trigger (RetentionScheduler).
/// Loads retention policy entirely from function_parameters — no production defaults in code.
///
/// Policy source: function_parameters (function_name='context_event_retention', parameter_key='retention_policy').
/// Supported archive_strategy values: "delete" (purge rows older than cold_days).
///
/// Explicit status on every execution:
///   Ok              — cleanup executed (rows_affected may be 0 if nothing expired)
///   Disabled        — policy.enabled = false; logged, not silently skipped
///   MissingPolicy   — no active function_parameters row found
///   MalformedPolicy — JSON parse failure or unknown archive_strategy
///
/// Canonical excitation route:
///   cron trigger → trigger context → RuntimeExecutor → select package → LogRetentionRuntime
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
            return new RetentionRunResult(RetentionExecutionStatus.MissingPolicy,
                $"No active function_parameters row for '{FunctionName}/{PolicyKey}'.",
                0, executedAt, 0);
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
            return new RetentionRunResult(RetentionExecutionStatus.MalformedPolicy,
                $"Policy JSON for '{FunctionName}/{PolicyKey}' could not be parsed: {ex.Message}",
                0, executedAt, 0);
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
            return new RetentionRunResult(RetentionExecutionStatus.MalformedPolicy,
                $"archive_strategy='{policy.ArchiveStrategy}' is not a supported strategy. Supported: 'delete'.",
                0, executedAt, policy.ScheduleIntervalHours);
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
}
