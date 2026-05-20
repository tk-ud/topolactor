using System.Text.Json;

namespace Topolactor.Schema;

/// <summary>
/// Inbound DTO from the caller/frontend. Represents a user operation request.
/// trigger_kind identifies the trigger origin (cron|hook|client) per SSOT minimal_event_shape.
/// role carries the JWT token claim for manifest axis resolution.
/// </summary>
public record EndpointRequestDto(
    string? OperationType,
    string? Target,
    string? Layer,
    string? Action,
    Guid? IdOrHubId,
    JsonElement? Payload,
    Dictionary<string, string>? Context,
    string? TriggerKind = null,
    string? Role = null
);

/// <summary>
/// Outbound DTO returned to the caller. Contains either an Emission or validation errors.
/// </summary>
public record EndpointResponseDto(
    bool Success,
    Emission? Emission,
    IReadOnlyList<ValidationError> Errors
);

/// <summary>
/// Internal runtime concept. Public only for C# accessibility consistency.
/// Must not be returned to the frontend or exposed through EndpointResponseDto.
/// Derived from EndpointRequestDto after input mapping.
/// Context route fields (ContextSessionId, ContextUserId, ContextTokenIds, ContextRecordId)
/// are extracted from EndpointRequestDto.Context for use by the recommendation resolver.
/// </summary>
public record OperationVector(
    string? Target,
    string? Layer,
    string? Action,
    string? AttractorKey,
    string? UserRole,
    JsonElement? Payload,
    string? RequestedProjection,
    // Context route recommendation fields — nullable, no guard enforcement
    string? ContextSessionId = null,
    string? ContextUserId = null,
    string? ContextTokenIds = null,     // comma-separated Guid list
    string? ContextRecordId = null,
    Guid? IdOrHubId = null,
    // trigger_kind from SSOT minimal_event_shape: cron | hook | client
    string? TriggerKind = null
);

/// <summary>
/// Internal runtime concept. Public only for C# accessibility consistency.
/// Must not be returned to the frontend, exposed through EndpointResponseDto,
/// or persisted as a business fact.
/// Holds intermediate resolved state as the runtime progresses through the pipeline.
/// ContextRouteRecommendation is populated by context_route_recommendation_resolve
/// and forwarded to EmissionBuilder.
/// </summary>
public record RuntimeWorkingShape(
    OperationVector? Vector,
    string? StructureMapId,
    Guid? PackageId,
    Guid? SchemaId,
    IReadOnlyList<string>? ComponentIds,
    object? PackageDef,
    object? SchemaDef,
    JsonElement? ResolvedData,
    IReadOnlyList<ValidationError>? Errors,
    // Raw JSONB from structure_maps.state_policy — used by ContextRouteRecommendationResolver
    // to resolve a scoped context_route_policy_ref instead of the global default_policy.
    string? StructureMapStatePolicyJson = null,
    ContextRouteRecommendationResult? ContextRouteRecommendation = null
);

/// <summary>
/// Validated output returned in the response. Contains resolved identifiers and data.
/// ContextRouteRecommendation carries next operation and token candidates derived
/// from the context route recommendation runtime. Status is always explicit —
/// InsufficientHistory when not enough history exists, never silently null.
/// </summary>
public record Emission(
    string? StructureMapId,
    Guid? PackageId,
    Guid? SchemaId,
    IReadOnlyList<string>? ComponentIds,
    JsonElement? Data,
    IReadOnlyList<ValidationError> Errors,
    ContextRouteRecommendationResult? ContextRouteRecommendation = null
);

/// <summary>
/// Result of attractor resolution: maps an attractor key to its structure map, package, and schema.
/// </summary>
public record AttractorResult(
    string AttractorKey,
    string StructureMapId,
    Guid PackageId,
    Guid SchemaId
);

/// <summary>
/// A structured validation error returned when resolution fails at any pipeline stage.
/// </summary>
public record ValidationError(
    string Code,
    string Message
);
