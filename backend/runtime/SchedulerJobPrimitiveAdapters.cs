using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Abstract function primitive that absorbs the log-retention cron body into the
/// scheduler job manifest substrate.
///
/// Representative existing cron absorption:
///   The former RetentionScheduler BackgroundService is replaced by a seed scheduler
///   job manifest (interval_seconds) whose single step dispatches the abstract function
///   key system.log_retention. This primitive forwards to LogRetentionRuntime — the
///   retention domain body stays in the package runtime, never in the scheduler substrate.
///
/// Boundary:
///   - The scheduler substrate knows nothing about retention policy, tables, or columns.
///   - retention policy is loaded by LogRetentionRuntime from function_parameters.
///   - the result projected to the run ledger is sanitized status + rows_affected only.
///   - MissingPolicy / MalformedPolicy fail-close explicitly (no silent fallback).
/// </summary>
public sealed class LogRetentionPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly ILogger<LogRetentionPrimitiveAdapter> _logger;
    private readonly LogRetentionRuntime _retentionRuntime;

    public LogRetentionPrimitiveAdapter(
        ILogger<LogRetentionPrimitiveAdapter> logger,
        LogRetentionRuntime retentionRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retentionRuntime = retentionRuntime ?? throw new ArgumentNullException(nameof(retentionRuntime));
    }

    public string PrimitiveKey => "log_retention";

    public async Task<object?> ExecuteAsync(
        AbstractFunctionStep step,
        IReadOnlyDictionary<string, object?> inputs,
        AbstractFunctionExecutionContext context,
        CancellationToken ct = default)
    {
        var result = await _retentionRuntime.ExecuteAsync(ct);

        switch (result.Status)
        {
            case RetentionExecutionStatus.MissingPolicy:
                throw new AbstractFunctionFailCloseException(
                    AbstractFunctionFailCloseStatus.MissingAuthority,
                    "LOG_RETENTION_MISSING_POLICY");
            case RetentionExecutionStatus.MalformedPolicy:
                throw new AbstractFunctionFailCloseException(
                    AbstractFunctionFailCloseStatus.InvalidAuthority,
                    "LOG_RETENTION_MALFORMED_POLICY");
        }

        _logger.LogInformation(
            "LogRetentionPrimitiveAdapter: status={Status} rows={Rows}.",
            result.Status, result.RowsAffected);

        // Sanitized projection only — no secret material, no raw policy body.
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = result.Status.ToString(),
            ["rows_affected"] = result.RowsAffected,
        };
    }
}
