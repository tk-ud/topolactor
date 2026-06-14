using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Admin dispatch target override handler.
///
/// Holds the admin dispatch logic used when ManifestDispatcher
/// has no ManifestRepository configured (dev environments only).
///
/// ManifestDispatcher calls this only inside the null-repository branch.
/// When a ManifestRepository is configured (production), this class is not consulted —
/// manifest-driven destination selection is the sole authority.
///
/// Per Gap-1 completion: target_layer_action_destination_selection_is_moved_to_manifest_dispatcher.
/// </summary>
public class TargetDispatchOverride
{
    private readonly ILogger<TargetDispatchOverride> _logger;
    private readonly AdminRuntime _adminRuntime;

    public TargetDispatchOverride(
        ILogger<TargetDispatchOverride> logger,
        AdminRuntime adminRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _adminRuntime = adminRuntime ?? throw new ArgumentNullException(nameof(adminRuntime));
    }

    /// <summary>
    /// Early request validation for target overrides.
    /// Returns null — all validation is now handled downstream.
    /// </summary>
    public ValidationError? ValidateRequest(OperationVector vector) => null;

    /// <summary>
    /// Handles Step 10 data override for admin targets.
    /// Returns (handled: false, ...) when the target is not managed by this override.
    /// Returns (handled: true, ...) with data or error when the target is handled here.
    /// </summary>
    public async Task<(bool handled, JsonElement? data, ValidationError? error)> TryHandleAsync(
        OperationVector vector,
        CancellationToken ct = default)
    {
        if (IsAdmin(vector))
        {
            var result = await _adminRuntime.ExecuteDataAsync(vector, ct);
            return (true, result.data, result.error);
        }

        return (false, null, null);
    }

    private static bool IsAdmin(OperationVector v) =>
        string.Equals(v.Target, "admin", StringComparison.OrdinalIgnoreCase);
}
