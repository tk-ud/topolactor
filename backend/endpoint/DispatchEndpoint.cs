using Microsoft.Extensions.Logging;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

/// <summary>
/// Thin boundary entry point. Receives an EndpointRequestDto, delegates entirely
/// to RuntimeExecutor, and returns the response. Contains no business logic.
/// </summary>
public class DispatchEndpoint
{
    private readonly ILogger<DispatchEndpoint> _logger;
    private readonly RuntimeExecutor _runtimeExecutor;

    public DispatchEndpoint(
        ILogger<DispatchEndpoint> logger,
        RuntimeExecutor runtimeExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimeExecutor = runtimeExecutor ?? throw new ArgumentNullException(nameof(runtimeExecutor));
    }

    /// <summary>
    /// Handles an inbound request by delegating to the runtime executor.
    /// This method is the single entry point from the outside world into the runtime.
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

        _logger.LogDebug(
            "Dispatching request: OperationType={OperationType} Target={Target} Layer={Layer} Action={Action}",
            request.OperationType, request.Target, request.Layer, request.Action);

        return await _runtimeExecutor.ExecuteAsync(request, ct);
    }
}
