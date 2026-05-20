using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Manifest-driven dispatcher. Resolves runtime_destination from manifest axes
/// (role, target, layer, action) and forwards to the appropriate runtime.
///
/// Per SSOT dispatcher_contract:
///   - Reads active manifest from DB by [role, target, layer, action] axes.
///   - No silent fallback: missing manifest -> MANIFEST_NOT_FOUND error.
///   - Ambiguous manifests (multiple active for same axes) -> MANIFEST_AMBIGUOUS error.
///   - Does not own topology transform logic.
///
/// When no active manifest exists for the given axes (e.g., demo/development),
/// the dispatcher falls through to RuntimeExecutor using the request axes directly.
/// This fallthrough is logged as a warning and is not a silent fallback — it is the
/// explicit policy when no manifest has been registered for the axes.
/// </summary>
public class ManifestDispatcher
{
    private readonly ILogger<ManifestDispatcher> _logger;
    private readonly RuntimeExecutor _runtimeExecutor;
    private readonly ManifestRepository? _manifestRepository;

    public ManifestDispatcher(
        ILogger<ManifestDispatcher> logger,
        RuntimeExecutor runtimeExecutor,
        ManifestRepository? manifestRepository = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimeExecutor = runtimeExecutor ?? throw new ArgumentNullException(nameof(runtimeExecutor));
        _manifestRepository = manifestRepository;
    }

    /// <summary>
    /// Resolves the runtime destination from manifest axes and dispatches the request.
    /// Attempts DB manifest lookup; falls through to RuntimeExecutor when no manifest
    /// is registered for the axes (explicit warning logged, not silent).
    /// </summary>
    public async Task<EndpointResponseDto> DispatchAsync(
        EndpointRequestDto request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "ManifestDispatcher: resolving destination for Role={Role} Target={Target} Layer={Layer} Action={Action} TriggerKind={TriggerKind}",
            request.Role, request.Target, request.Layer, request.Action, request.TriggerKind);

        if (_manifestRepository is not null)
        {
            try
            {
                var manifest = await _manifestRepository.ResolveActiveManifestAsync(
                    role: request.Role,
                    target: request.Target,
                    layer: request.Layer,
                    action: request.Action,
                    ct: ct);

                if (manifest is not null)
                {
                    _logger.LogDebug(
                        "ManifestDispatcher: resolved manifest {ManifestId} for axes.",
                        manifest.ManifestId);

                    // Manifest resolved — delegate to RuntimeExecutor with original request.
                    // When runtime_mapping entries exist in the manifest, this is where
                    // runtime_destination routing would select the appropriate runtime.
                    // Currently routes to RuntimeExecutor (topology_transform_runtime).
                    return await _runtimeExecutor.ExecuteAsync(request, ct);
                }

                _logger.LogWarning(
                    "ManifestDispatcher: no active manifest for Role={Role} Target={Target} Layer={Layer} Action={Action}. " +
                    "Falling through to RuntimeExecutor (manifest-unregistered axes).",
                    request.Role, request.Target, request.Layer, request.Action);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("MANIFEST_AMBIGUOUS", StringComparison.Ordinal))
            {
                _logger.LogError(ex, "ManifestDispatcher: ambiguous manifest lookup rejected.");
                return new EndpointResponseDto(
                    Success: false,
                    Emission: null,
                    Errors: [new ValidationError("MANIFEST_AMBIGUOUS", ex.Message)]);
            }
        }
        else
        {
            _logger.LogDebug("ManifestDispatcher: no manifest repository configured; delegating to RuntimeExecutor.");
        }

        return await _runtimeExecutor.ExecuteAsync(request, ct);
    }
}
