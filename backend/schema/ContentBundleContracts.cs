using System.Text.Json;
using System.Text.Json.Serialization;

namespace Topolactor.Schema;

public record ContentBundleListItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("hubId")] string? HubId,
    [property: JsonPropertyName("relationIds")] IReadOnlyList<string>? RelationIds,
    [property: JsonPropertyName("summary")] string Summary
);

public record ContentBundleHubDetailDto(
    [property: JsonPropertyName("hubId")] string HubId,
    [property: JsonPropertyName("stateName")] string StateName,
    [property: JsonPropertyName("stateId")] string? StateId,
    [property: JsonPropertyName("relationRegistryId")] string? RelationRegistryId,
    [property: JsonPropertyName("relationLabel")] string? RelationLabel,
    [property: JsonPropertyName("entityCount")] int EntityCount,
    [property: JsonPropertyName("hubRelationCount")] int HubRelationCount,
    [property: JsonPropertyName("entityIds")] IReadOnlyList<string> EntityIds,
    [property: JsonPropertyName("summary")] string Summary
);

public record ContentBundleRelationDetailDto(
    [property: JsonPropertyName("relationRegistryId")] string RelationRegistryId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("active")] bool Active,
    [property: JsonPropertyName("entityCount")] int EntityCount,
    [property: JsonPropertyName("hubRelationCount")] int HubRelationCount,
    [property: JsonPropertyName("summary")] string Summary
);

public record ContentBundleEntityDetailDto(
    [property: JsonPropertyName("entityId")] string EntityId,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("stateName")] string StateName,
    [property: JsonPropertyName("stateId")] string? StateId,
    [property: JsonPropertyName("hubId")] string HubId,
    [property: JsonPropertyName("hubLabel")] string HubLabel,
    [property: JsonPropertyName("relationIds")] IReadOnlyList<string> RelationIds,
    [property: JsonPropertyName("relationLabels")] IReadOnlyList<string> RelationLabels,
    [property: JsonPropertyName("entityJsonb")] string EntityJsonb,
    [property: JsonPropertyName("summary")] string Summary
);

public record ContentBundleStateItemDto(
    [property: JsonPropertyName("stateId")] string StateId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("owner")] string? Owner
);

public record ContentBundleDraftRequestDto(
    [property: JsonPropertyName("hubId")] string HubId,
    [property: JsonPropertyName("entityJsonb")] JsonElement EntityJsonb,
    [property: JsonPropertyName("relationIds")] IReadOnlyList<string> RelationIds,
    [property: JsonPropertyName("stateName")] string StateName
);

public record ContentBundleUpdateDraftRequestDto(
    [property: JsonPropertyName("draftId")] string DraftId,
    [property: JsonPropertyName("hubId")] string HubId,
    [property: JsonPropertyName("entityJsonb")] JsonElement EntityJsonb,
    [property: JsonPropertyName("relationIds")] IReadOnlyList<string> RelationIds,
    [property: JsonPropertyName("stateName")] string StateName
);

public record ContentBundleDraftDetailDto(
    [property: JsonPropertyName("draftId")] string DraftId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("hubId")] string HubId,
    [property: JsonPropertyName("entityJsonb")] string EntityJsonb,
    [property: JsonPropertyName("relationIds")] IReadOnlyList<string> RelationIds,
    [property: JsonPropertyName("stateName")] string? StateName,
    [property: JsonPropertyName("stateId")] string? StateId,
    [property: JsonPropertyName("promotedEntityId")] string? PromotedEntityId,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt
);

public record ContentBundleValidationIssueDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("isBlocking")] bool IsBlocking
);

public record ContentBundleValidateResponseDto(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("isBlocking")] bool IsBlocking,
    [property: JsonPropertyName("issues")] IReadOnlyList<ContentBundleValidationIssueDto> Issues
);

public record ContentBundlePreviewResponseDto(
    [property: JsonPropertyName("draftId")] string DraftId,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("hubId")] string HubId,
    [property: JsonPropertyName("hubLabel")] string? HubLabel,
    [property: JsonPropertyName("relationIds")] IReadOnlyList<string> RelationIds,
    [property: JsonPropertyName("relationLabels")] IReadOnlyList<string> RelationLabels,
    [property: JsonPropertyName("stateName")] string? StateName,
    [property: JsonPropertyName("entityJsonb")] string EntityJsonb,
    [property: JsonPropertyName("validation")] ContentBundleValidateResponseDto Validation,
    [property: JsonPropertyName("canPromote")] bool CanPromote
);

public record ContentBundleLifecycleResponseDto(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("draftId")] string DraftId,
    [property: JsonPropertyName("entityId")] string? EntityId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("readback")] ContentBundleEntityDetailDto? Readback,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null
);

public record ContentBundleSearchRequestDto(
    [property: JsonPropertyName("keyword")] string? Keyword,
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("state")] string? State
);

public record ContentBundleDraftIdRequestDto(
    [property: JsonPropertyName("draftId")] string DraftId
);

public record ContentBundleEntityIdRequestDto(
    [property: JsonPropertyName("entityId")] string EntityId
);

public record ContentBundleHubIdRequestDto(
    [property: JsonPropertyName("hubId")] string HubId
);

public record ContentBundleRelationIdRequestDto(
    [property: JsonPropertyName("relationRegistryId")] string RelationRegistryId
);
