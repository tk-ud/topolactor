using Microsoft.Extensions.Logging;
using Topolactor.Scheduler;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

/// <summary>
/// Thin boundary entry point. Receives an EndpointRequestDto and delegates to
/// RuntimeTimelineScheduler (trigger alignment) → ManifestDispatcher → RuntimeExecutor.
/// Contains no business logic.
/// </summary>
public class DispatchEndpoint
{
    private static readonly HashSet<string> _allowedTriggerKinds =
        new(StringComparer.OrdinalIgnoreCase) { "client", "hook", "cron" };

    private readonly ILogger<DispatchEndpoint> _logger;
    private readonly RuntimeTimelineScheduler _scheduler;

    public DispatchEndpoint(
        ILogger<DispatchEndpoint> logger,
        RuntimeTimelineScheduler scheduler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    /// <summary>
    /// Single entry point from the outside world into the runtime pipeline.
    /// Null guard is here; all other logic is owned by scheduler, dispatcher, and executor.
    /// </summary>
    public async Task<EndpointResponseDto> HandleAsync(
        EndpointRequestDto request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            _logger.LogWarning("Null request received at dispatch boundary.");
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new("REQUEST_NULL", "Request must not be null.")]);
        }

        if (request.TriggerKind is not null && !_allowedTriggerKinds.Contains(request.TriggerKind))
        {
            _logger.LogWarning(
                "Invalid trigger_kind '{TriggerKind}' at dispatch boundary. Allowed: client, hook, cron.",
                request.TriggerKind);
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError(
                    "TRIGGER_KIND_INVALID",
                    $"trigger_kind '{request.TriggerKind}' is not valid. Allowed values: client, hook, cron.")]);
        }

        _logger.LogDebug(
            "Dispatching request: OperationType={OperationType} Target={Target} Layer={Layer} Action={Action}",
            request.OperationType, request.Target, request.Layer, request.Action);

        return await _scheduler.AlignAndDispatchAsync(request, ct);
    }
}
