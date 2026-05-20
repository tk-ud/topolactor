using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Manifest-driven dispatcher. Resolves runtime_destination from manifest axes
/// (role, target, layer, action) and forwards to the appropriate runtime.
///
/// Skeleton: currently delegates directly to RuntimeExecutor. When the manifest DB
/// is seeded with dispatcher_mapping and runtime_mapping, this class will resolve
/// the runtime_destination from stored manifest records instead of hardwiring the
/// executor. ManifestDispatcher must not own topology transform logic.
/// </summary>
public class ManifestDispatcher
{
    private readonly ILogger<ManifestDispatcher> _logger;
    private readonly RuntimeExecutor _runtimeExecutor;

    public ManifestDispatcher(
        ILogger<ManifestDispatcher> logger,
        RuntimeExecutor runtimeExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimeExecutor = runtimeExecutor ?? throw new ArgumentNullException(nameof(runtimeExecutor));
    }

    /// <summary>
    /// Resolves the runtime destination from manifest axes and dispatches the request.
    /// Skeleton: axes are available on the request but manifest lookup is not yet implemented;
    /// delegates to RuntimeExecutor directly.
    /// </summary>
    public Task<EndpointResponseDto> DispatchAsync(
        EndpointRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "ManifestDispatcher: resolving destination for Target={Target} Layer={Layer} Action={Action}",
            request?.Target, request?.Layer, request?.Action);

        return _runtimeExecutor.ExecuteAsync(request!, ct);
    }
}
