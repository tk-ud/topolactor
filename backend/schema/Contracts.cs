using System.Text.Json;

namespace Topolactor.Schema;

/// <summary>
/// Inbound DTO from the caller/frontend. Represents a user operation request.
/// </summary>
public record EndpointRequestDto(
    string? OperationType,
    string? Target,
    string? Layer,
    string? Action,
    Guid? IdOrHubId,
    JsonElement? Payload,
    Dictionary<string, string>? Context
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
/// Internal resolved operation vector. Never returned to the frontend.
/// Derived from EndpointRequestDto after input mapping.
/// </summary>
internal record OperationVector(
    string? Target,
    string? Layer,
    string? Action,
    string? AttractorKey,
    string? UserRole,
    JsonElement? Payload,
    string? RequestedProjection
);

/// <summary>
/// Internal-only backend working shape used during a single execution pass.
/// Never returned to the frontend. Never persisted as a business fact.
/// Holds intermediate resolved state as the runtime progresses through the pipeline.
/// </summary>
internal record RuntimeWorkingShape(
    OperationVector? Vector,
    string? StructureMapId,
    Guid? PackageId,
    Guid? SchemaId,
    IReadOnlyList<string>? ComponentIds,
    object? PackageDef,
    object? SchemaDef,
    JsonElement? ResolvedData,
    IReadOnlyList<ValidationError>? Errors
);

/// <summary>
/// Validated output returned in the response. Contains resolved identifiers and data.
/// </summary>
public record Emission(
    string? StructureMapId,
    Guid? PackageId,
    Guid? SchemaId,
    IReadOnlyList<string>? ComponentIds,
    JsonElement? Data,
    IReadOnlyList<ValidationError> Errors
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
