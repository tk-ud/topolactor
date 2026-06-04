using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Maps an inbound EndpointRequestDto to an internal OperationVector.
/// Derives the AttractorKey from Target + Layer + Action.
/// No fallback logic: missing fields produce null fields in the vector,
/// which are caught downstream by RuntimeGuard.
/// </summary>
public class OperationVectorResolver
{
    /// <summary>
    /// Resolves an OperationVector from the given request.
    /// The AttractorKey is derived deterministically from Target:Layer:Action.
    /// </summary>
    public OperationVector Resolve(EndpointRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var attractorKey = DeriveAttractorKey(request.Target, request.Layer, request.Action);

        // Standard and context route fields sourced from Context if present
        string? userRole = null;
        string? requestedProjection = null;
        string? contextSessionId = null;
        string? contextUserId = null;
        string? contextTokenIds = null;
        string? contextRecordId = null;
        string? contextEnumGroupId = null;
        int? contextPrevEnumIndex = null;
        int? contextNextEnumIndex = null;

        if (request.Context is not null)
        {
            request.Context.TryGetValue("UserRole", out userRole);
            request.Context.TryGetValue("RequestedProjection", out requestedProjection);
            request.Context.TryGetValue("ContextSessionId", out contextSessionId);
            request.Context.TryGetValue("ContextUserId", out contextUserId);
            request.Context.TryGetValue("ContextTokenIds", out contextTokenIds);
            request.Context.TryGetValue("ContextRecordId", out contextRecordId);
            request.Context.TryGetValue("enum_group_id", out contextEnumGroupId);
            if (request.Context.TryGetValue("prev_enum_index", out var prevRaw) &&
                int.TryParse(prevRaw, out var prevIdx))
                contextPrevEnumIndex = prevIdx;
            if (request.Context.TryGetValue("next_enum_index", out var nextRaw) &&
                int.TryParse(nextRaw, out var nextIdx))
                contextNextEnumIndex = nextIdx;
        }

        // role from direct field takes precedence over Context["UserRole"]
        var resolvedRole = request.Role ?? userRole;

        return new OperationVector(
            Target: request.Target,
            Layer: request.Layer,
            Action: request.Action,
            AttractorKey: attractorKey,
            UserRole: resolvedRole,
            Payload: request.Payload,
            RequestedProjection: requestedProjection,
            ContextSessionId: contextSessionId,
            ContextUserId: contextUserId,
            ContextTokenIds: contextTokenIds,
            ContextRecordId: contextRecordId,
            IdOrHubId: request.IdOrHubId,
            ContextEnumGroupId: contextEnumGroupId,
            ContextPrevEnumIndex: contextPrevEnumIndex,
            ContextNextEnumIndex: contextNextEnumIndex,
            TriggerKind: request.TriggerKind
        );
    }

    /// <summary>
    /// Derives the attractor key. Returns null if any segment is missing.
    /// The key format is: "{target}:{layer}:{action}" (all lowercase).
    /// </summary>
    private static string? DeriveAttractorKey(string? target, string? layer, string? action)
    {
        if (string.IsNullOrWhiteSpace(target) ||
            string.IsNullOrWhiteSpace(layer) ||
            string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        return $"{target.Trim().ToLowerInvariant()}:{layer.Trim().ToLowerInvariant()}:{action.Trim().ToLowerInvariant()}";
    }
}
