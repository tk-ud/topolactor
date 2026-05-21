using Microsoft.Extensions.Logging;
using Topolactor.Schema;
using Topolactor.Scheduler;

namespace Topolactor.Endpoint;

/// <summary>
/// Intake boundary for existing-system forked change events.
/// Accepts minimal change shape and forwards as hook trigger through scheduler.
/// </summary>
public class LegacyChangeIntakeEndpoint
{
    private readonly ILogger<LegacyChangeIntakeEndpoint> _logger;
    private readonly RuntimeTimelineScheduler _scheduler;

    public LegacyChangeIntakeEndpoint(
        ILogger<LegacyChangeIntakeEndpoint> logger,
        RuntimeTimelineScheduler scheduler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    public LegacyChangeIntakeResponseDto Handle(LegacyChangeIntakeRequestDto request)
    {
        if (request is null)
        {
            return new LegacyChangeIntakeResponseDto(
                Accepted: false,
                QueueStatus: null,
                Errors: [new ValidationError("REQUEST_NULL", "Request must not be null.")]);
        }

        var result = _scheduler.EnqueueLegacyChangeTrigger(request);
        if (!result.Accepted)
        {
            return new LegacyChangeIntakeResponseDto(
                Accepted: false,
                QueueStatus: null,
                Errors: result.Errors);
        }

        _logger.LogInformation(
            "LegacyChangeIntake accepted table={TableName} row={RowId} operation={Operation}",
            request.TableName, request.RowId, request.Operation);

        return new LegacyChangeIntakeResponseDto(
            Accepted: true,
            QueueStatus: "hook_trigger_enqueued",
            Errors: []);
    }
}
