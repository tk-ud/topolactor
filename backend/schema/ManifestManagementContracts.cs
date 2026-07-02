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
    [property: JsonPropertyName("nullable")] bool Nullable,
    [property: JsonPropertyName("enumGroupId")] string? EnumGroupId = null
);

public record AdminManifestAggregationMeasureDto(
    [property: JsonPropertyName("column")] string Column,
    [property: JsonPropertyName("function")] string Function
);

public record AdminManifestAggregationBlockDto(
    [property: JsonPropertyName("sourceRef")] string SourceRef,
    [property: JsonPropertyName("aggregationKey")] string? AggregationKey,
    [property: JsonPropertyName("measures")] IReadOnlyList<AdminManifestAggregationMeasureDto> Measures,
    [property: JsonPropertyName("searchConditions")] IReadOnlyList<AdminManifestSearchConditionDto>? SearchConditions = null,
    [property: JsonPropertyName("havingConditions")] IReadOnlyList<AdminManifestHavingConditionDto>? HavingConditions = null
);

public record AdminManifestRelationIntentDto(
    [property: JsonPropertyName("localTableRef")] string? LocalTableRef,
    [property: JsonPropertyName("joinTableRef")] string JoinTableRef,
    [property: JsonPropertyName("localKey")] string LocalKey,
    [property: JsonPropertyName("remoteKey")] string RemoteKey,
    /// <summary>When set, joinTableRef/remoteKey resolve against this active manifest (not draft-only).</summary>
    [property: JsonPropertyName("remoteManifestId")] string? RemoteManifestId
);

public record AdminManifestRelationshipRemoteTargetTableDto(
    [property: JsonPropertyName("tableName")] string TableName,
    [property: JsonPropertyName("columns")] IReadOnlyList<AdminManifestScreenColumnDto> Columns
);

public record AdminManifestRelationshipRemoteTargetDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("manifestKey")] string? ManifestKey,
    [property: JsonPropertyName("logicalTables")] IReadOnlyList<AdminManifestRelationshipRemoteTargetTableDto> LogicalTables
);

public record AdminManifestListRelationshipRemoteTargetsRequestDto(
    [property: JsonPropertyName("excludeManifestId")] string? ExcludeManifestId
);

public record AdminManifestOperationEntityBindingDto(
    [property: JsonPropertyName("operationKind")] string OperationKind,
    [property: JsonPropertyName("entityTargetColumn")] string? EntityTargetColumn,
    [property: JsonPropertyName("entityTargetColumns")] IReadOnlyList<string>? EntityTargetColumns
);

public record AdminManifestConditionValueSourceDto(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("sampleValue")] string? SampleValue
);

public record AdminManifestSearchConditionDto(
    [property: JsonPropertyName("column")] string Column,
    [property: JsonPropertyName("operator")] string Operator,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("valueTo")] string? ValueTo,
    [property: JsonPropertyName("values")] IReadOnlyList<string>? Values,
    [property: JsonPropertyName("logicalConnector")] string? LogicalConnector,
    [property: JsonPropertyName("valueSource")] AdminManifestConditionValueSourceDto? ValueSource = null,
    [property: JsonPropertyName("valueToSource")] AdminManifestConditionValueSourceDto? ValueToSource = null
);

public record AdminManifestHavingConditionDto(
    [property: JsonPropertyName("column")] string Column,
    [property: JsonPropertyName("function")] string Function,
    [property: JsonPropertyName("operator")] string Operator,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("valueSource")] AdminManifestConditionValueSourceDto? ValueSource = null
);

public record AdminManifestLogicalTableDto(
    [property: JsonPropertyName("tableName")] string TableName,
    [property: JsonPropertyName("columns")] IReadOnlyList<AdminManifestScreenColumnDto> Columns
);

public record AdminManifestAssignScreenDataShapeRequestDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("tableRef")] string? TableRef,
    [property: JsonPropertyName("dbTableName")] string? DbTableName,
    [property: JsonPropertyName("importSchemaName")] string? ImportSchemaName,
    [property: JsonPropertyName("searchTargets")] IReadOnlyList<string>? SearchTargets,
    [property: JsonPropertyName("searchKeyColumns")] IReadOnlyList<string>? SearchKeyColumns,
    [property: JsonPropertyName("aggregationSpec")] string? AggregationSpec,
    [property: JsonPropertyName("aggregationKey")] string? AggregationKey,
    [property: JsonPropertyName("aggregationFunction")] string? AggregationFunction,
    [property: JsonPropertyName("aggregationColumns")] IReadOnlyList<string>? AggregationColumns,
    [property: JsonPropertyName("aggregationMeasures")] IReadOnlyList<AdminManifestAggregationMeasureDto>? AggregationMeasures,
    [property: JsonPropertyName("aggregationBlocks")] IReadOnlyList<AdminManifestAggregationBlockDto>? AggregationBlocks,
    [property: JsonPropertyName("displayColumns")] IReadOnlyList<string>? DisplayColumns,
    [property: JsonPropertyName("logicalTables")] IReadOnlyList<AdminManifestLogicalTableDto>? LogicalTables,
    [property: JsonPropertyName("columns")] IReadOnlyList<AdminManifestScreenColumnDto>? Columns,
    [property: JsonPropertyName("screenOperationKind")] string? ScreenOperationKind,
    [property: JsonPropertyName("screenOperationKinds")] IReadOnlyList<string>? ScreenOperationKinds,
    [property: JsonPropertyName("userFacingTopologyLabel")] string? UserFacingTopologyLabel,
    [property: JsonPropertyName("relationIntents")] IReadOnlyList<AdminManifestRelationIntentDto>? RelationIntents,
    [property: JsonPropertyName("operationEntityBindings")] IReadOnlyList<AdminManifestOperationEntityBindingDto>? OperationEntityBindings,
    [property: JsonPropertyName("initialDataRows")] IReadOnlyList<System.Text.Json.JsonElement>? InitialDataRows,
    [property: JsonPropertyName("searchConditions")] IReadOnlyList<AdminManifestSearchConditionDto>? SearchConditions,
    [property: JsonPropertyName("havingConditions")] IReadOnlyList<AdminManifestHavingConditionDto>? HavingConditions,
    [property: JsonPropertyName("displayColumnMode")] string? DisplayColumnMode,
    [property: JsonPropertyName("aggregateTriggerDefinitions")] IReadOnlyList<AggregateTriggerDefinition>? AggregateTriggerDefinitions = null
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
