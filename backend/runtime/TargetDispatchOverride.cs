using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Explicitly isolated non-canonical target dispatch bridge.
///
/// Holds the demo/entity and admin dispatch logic that previously lived inline in
/// RuntimeExecutor.ExecuteAsync. This class is the isolation boundary for Gap-1.
///
/// RuntimeExecutor calls this via ValidateRequest (early validation) and
/// TryHandleAsync (Step 10 data override). The canonical pipeline steps 1-9 still
/// run before TryHandleAsync is reached.
///
/// Known gap: pending full migration to manifest-driven ManifestDispatcher routing.
/// Per Gap-1: RuntimeExecutor_target_layer_action_dispatch_branches_are_removed_or_explicitly_isolated.
/// </summary>
public class TargetDispatchOverride
{
    private readonly ILogger<TargetDispatchOverride> _logger;
    private readonly TopologyRepository _topologyRepository;
    private readonly AdminRuntime _adminRuntime;

    public TargetDispatchOverride(
        ILogger<TargetDispatchOverride> logger,
        TopologyRepository topologyRepository,
        AdminRuntime adminRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _adminRuntime = adminRuntime ?? throw new ArgumentNullException(nameof(adminRuntime));
    }

    /// <summary>
    /// Early request validation for target overrides.
    /// Returns a ValidationError when demo/entity constraints are violated.
    /// Returns null when the target is not handled here or validation passes.
    /// </summary>
    public ValidationError? ValidateRequest(OperationVector vector)
    {
        if (!IsDemoEntity(vector))
            return null;

        var action = vector.Action?.ToLowerInvariant();
        if (action is not ("list" or "detail" or "create" or "advance"))
        {
            return new ValidationError("INVALID_OPERATION",
                "demo/entity supports only list, detail, create, advance");
        }

        if (action is "detail" or "create" or "advance")
        {
            if (vector.Payload is null ||
                !vector.Payload.Value.TryGetProperty("entityId", out var entityIdEl) ||
                string.IsNullOrWhiteSpace(entityIdEl.GetString()) ||
                !Guid.TryParse(entityIdEl.GetString(), out _))
            {
                return new ValidationError("INVALID_PAYLOAD", "entityId UUID is required");
            }
        }

        return null;
    }

    /// <summary>
    /// Handles Step 10 data override for demo/entity and admin targets.
    /// Returns (handled: false, ...) when the target is not managed by this override.
    /// Returns (handled: true, ...) with data or error when the target is handled here.
    /// </summary>
    public async Task<(bool handled, JsonElement? data, ValidationError? error)> TryHandleAsync(
        OperationVector vector,
        CancellationToken ct = default)
    {
        if (IsDemoEntity(vector))
        {
            var result = await ApplyDemoStateLoopAsync(vector, ct);
            return (true, result.data, result.error);
        }

        if (IsAdmin(vector))
        {
            var result = await _adminRuntime.ExecuteDataAsync(vector, ct);
            return (true, result.data, result.error);
        }

        return (false, null, null);
    }

    private static bool IsDemoEntity(OperationVector v) =>
        string.Equals(v.Target, "demo", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(v.Layer, "entity", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdmin(OperationVector v) =>
        string.Equals(v.Target, "admin", StringComparison.OrdinalIgnoreCase);

    private async Task<(JsonElement? data, ValidationError? error)> ApplyDemoStateLoopAsync(
        OperationVector vector,
        CancellationToken ct)
    {
        var action = vector.Action?.ToLowerInvariant();
        if (action is not ("list" or "detail" or "create" or "advance"))
        {
            return (null, new ValidationError("INVALID_OPERATION",
                "demo/entity supports only list, detail, create, advance"));
        }

        try
        {
            Guid? entityId = null;
            if (action is "detail" or "create" or "advance")
            {
                if (vector.Payload is null ||
                    !vector.Payload.Value.TryGetProperty("entityId", out var entityIdEl) ||
                    string.IsNullOrWhiteSpace(entityIdEl.GetString()) ||
                    !Guid.TryParse(entityIdEl.GetString(), out var parsedId))
                {
                    return (null, new ValidationError("INVALID_PAYLOAD", "entityId UUID is required"));
                }
                entityId = parsedId;
            }

            if (action is "create" or "advance")
            {
                var title = vector.Payload!.Value.TryGetProperty("title", out var titleEl)
                    ? titleEl.GetString()
                    : null;
                var apply = await _topologyRepository.ApplyDemoTransitionAsync(
                    entityId!.Value, action, title, ct);
                if (!apply.Success)
                    return (null, new ValidationError(
                        apply.ErrorCode ?? "TRANSITION_FAILED",
                        apply.ErrorMessage ?? "transition failed"));
            }

            var list = await _topologyRepository.LoadDemoEntityListAsync(ct);
            DemoEntityProjection? detail = null;
            if (action == "detail" || entityId is not null)
            {
                detail = await _topologyRepository.LoadDemoEntityDetailAsync(entityId!.Value, ct);
                if (detail is null)
                    return (null, new ValidationError("STATE_NOT_FOUND",
                        "requested entity does not exist"));
            }

            var history = detail is null
                ? Array.Empty<object>()
                : await _topologyRepository.LoadDemoTransitionHistoryAsync(detail.EntityId, ct);

            var data = JsonSerializer.SerializeToElement(new { items = list, detail, history });
            return (data, null);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("NOT_CONNECTED", StringComparison.Ordinal))
        {
            return (null, new ValidationError("REPOSITORY_NOT_CONNECTED", ex.Message));
        }
    }
}
