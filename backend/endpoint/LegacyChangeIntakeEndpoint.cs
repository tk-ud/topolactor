using Microsoft.Extensions.Logging;
using Topolactor.Schema;
using Topolactor.Scheduler;

namespace Topolactor.Endpoint;

/// <summary>
/// Intake boundary for existing-system forked change events.
/// Accepts minimal change shape and forwards as hook trigger through scheduler.
/// Role is supplied from JWT claim — never from request body.
/// </summary>
public class ExistingSystemChangeIntakeEndpoint
{
    private readonly ILogger<ExistingSystemChangeIntakeEndpoint> _logger;
    private readonly RuntimeTimelineScheduler _scheduler;

    public ExistingSystemChangeIntakeEndpoint(
        ILogger<ExistingSystemChangeIntakeEndpoint> logger,
        RuntimeTimelineScheduler scheduler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    public ExistingSystemChangeIntakeResponseDto Handle(
        ExistingSystemChangeIntakeRequestDto request,
        string roleFromJwtClaim)
    {
        if (request is null)
        {
            return new ExistingSystemChangeIntakeResponseDto(
                Accepted: false,
                QueueStatus: null,
                Errors: [new ValidationError("REQUEST_NULL", "Request must not be null.")]);
        }

        if (string.IsNullOrWhiteSpace(roleFromJwtClaim))
        {
            return new ExistingSystemChangeIntakeResponseDto(
                Accepted: false,
                QueueStatus: null,
                Errors: [new ValidationError("ROLE_CLAIM_REQUIRED", "JWT role claim is required for existing-system intake.")]);
        }

        var result = _scheduler.EnqueueExistingSystemChangeTrigger(request, roleFromJwtClaim);
        if (!result.Accepted)
        {
            return new ExistingSystemChangeIntakeResponseDto(
                Accepted: false,
                QueueStatus: null,
                Errors: result.Errors);
        }

        _logger.LogInformation(
            "ExistingSystemChangeIntake accepted table={TableName} row={RowId} operation={Operation} role={Role}",
            request.TableName, request.RowId, request.Operation, roleFromJwtClaim);

        return new ExistingSystemChangeIntakeResponseDto(
            Accepted: true,
            QueueStatus: "hook_trigger_enqueued",
            Errors: []);
    }
}
