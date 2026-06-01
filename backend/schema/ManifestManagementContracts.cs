using System.Text.Json;
using System.Text.Json.Serialization;

namespace Topolactor.Schema;

public record AdminManifestListItemDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("relationRegistryId")] string? RelationRegistryId,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("target")] string? Target,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("runtimeDestination")] string? RuntimeDestination,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt
);

public record AdminManifestDispatcherMappingDto(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("layer")] string Layer,
    [property: JsonPropertyName("action")] string Action
);

public record AdminManifestRuntimeMappingDto(
    [property: JsonPropertyName("runtimeDestination")] string RuntimeDestination
);

public record AdminManifestProjectionConstructorMappingDto(
    [property: JsonPropertyName("hasProjectionDefinition")] bool HasProjectionDefinition
);

public record AdminManifestTopologySummaryDto(
    [property: JsonPropertyName("dispatcherMapping")] AdminManifestDispatcherMappingDto? DispatcherMapping,
    [property: JsonPropertyName("runtimeMapping")] AdminManifestRuntimeMappingDto? RuntimeMapping,
    [property: JsonPropertyName("projectionConstructorMapping")] AdminManifestProjectionConstructorMappingDto? ProjectionConstructorMapping,
    [property: JsonPropertyName("entryTypes")] IReadOnlyList<string> EntryTypes
);

public record AdminManifestDetailDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("relationRegistryId")] string? RelationRegistryId,
    [property: JsonPropertyName("summary")] AdminManifestTopologySummaryDto Summary,
    [property: JsonPropertyName("topologyRawJson")] string TopologyRawJson,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt
);

public record AdminManifestValidationIssueDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("isBlocking")] bool IsBlocking
);

public record AdminManifestValidateResponseDto(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("isBlocking")] bool IsBlocking,
    [property: JsonPropertyName("issues")] IReadOnlyList<AdminManifestValidationIssueDto> Issues,
    [property: JsonPropertyName("summary")] AdminManifestTopologySummaryDto? Summary
);

public record AdminManifestDraftRequestDto(
    [property: JsonPropertyName("relationRegistryId")] string? RelationRegistryId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("layer")] string Layer,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("runtimeDestination")] string RuntimeDestination,
    [property: JsonPropertyName("projectionDefinition")] JsonElement? ProjectionDefinition,
    [property: JsonPropertyName("screenOperationKind")] string? ScreenOperationKind
);

public record AdminManifestAssignHubGroupingRequestDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("hubId")] string HubId,
    [property: JsonPropertyName("manifestKey")] string ManifestKey
);

public record AdminManifestScreenColumnDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("dataType")] string DataType,
    [property: JsonPropertyName("nullable")] bool Nullable
);

public record AdminManifestAssignScreenDataShapeRequestDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("tableRef")] string? TableRef,
    [property: JsonPropertyName("dbTableName")] string? DbTableName,
    [property: JsonPropertyName("importSchemaName")] string? ImportSchemaName,
    [property: JsonPropertyName("searchTargets")] IReadOnlyList<string>? SearchTargets,
    [property: JsonPropertyName("aggregationSpec")] string? AggregationSpec,
    [property: JsonPropertyName("columns")] IReadOnlyList<AdminManifestScreenColumnDto>? Columns,
    [property: JsonPropertyName("screenOperationKind")] string? ScreenOperationKind
);

public record AdminManifestUpdateDraftRequestDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("relationRegistryId")] string? RelationRegistryId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("layer")] string Layer,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("runtimeDestination")] string RuntimeDestination,
    [property: JsonPropertyName("projectionDefinition")] JsonElement? ProjectionDefinition
);

public record AdminManifestIdRequestDto(
    [property: JsonPropertyName("manifestId")] string ManifestId
);

public record AdminManifestListRequestDto(
    [property: JsonPropertyName("status")] string? Status
);

public record AdminManifestLifecycleResponseDto(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null
);
