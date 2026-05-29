using System.Text.Json.Serialization;

namespace Topolactor.Schema;

public record AdminPromotionManifestTargetRefDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("schemaId")] string SchemaId,
    [property: JsonPropertyName("componentId")] string ComponentId
);

public record AdminPromotionManifestMetadataDto(
    [property: JsonPropertyName("manifestKey")] string ManifestKey,
    [property: JsonPropertyName("versionLabel")] string VersionLabel,
    [property: JsonPropertyName("disclosureText")] string DisclosureText,
    [property: JsonPropertyName("disclosureCategoryLabel")] string? DisclosureCategoryLabel,
    [property: JsonPropertyName("placementKey")] string PlacementKey,
    [property: JsonPropertyName("projectionSurfaceType")] string ProjectionSurfaceType,
    [property: JsonPropertyName("activationPolicyType")] string ActivationPolicyType,
    [property: JsonPropertyName("activationConditionExpression")] string? ActivationConditionExpression,
    [property: JsonPropertyName("targetTopologyRefs")] IReadOnlyList<AdminPromotionManifestTargetRefDto> TargetTopologyRefs
);

public record AdminPromotionManifestListItemDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("manifestKey")] string ManifestKey,
    [property: JsonPropertyName("versionLabel")] string VersionLabel,
    [property: JsonPropertyName("hasDisclosure")] bool HasDisclosure,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt
);

public record AdminPromotionManifestDetailDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("metadata")] AdminPromotionManifestMetadataDto? Metadata,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt
);

public record AdminPromotionManifestUpdateDraftRequestDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("manifestKey")] string ManifestKey,
    [property: JsonPropertyName("versionLabel")] string VersionLabel,
    [property: JsonPropertyName("disclosureText")] string DisclosureText,
    [property: JsonPropertyName("disclosureCategoryLabel")] string? DisclosureCategoryLabel,
    [property: JsonPropertyName("placementKey")] string PlacementKey,
    [property: JsonPropertyName("projectionSurfaceType")] string ProjectionSurfaceType,
    [property: JsonPropertyName("activationPolicyType")] string ActivationPolicyType,
    [property: JsonPropertyName("activationConditionExpression")] string? ActivationConditionExpression,
    [property: JsonPropertyName("targetTopologyRefs")] IReadOnlyList<AdminPromotionManifestTargetRefDto> TargetTopologyRefs
);

public record AdminPromotionManifestValidateResponseDto(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("isBlocking")] bool IsBlocking,
    [property: JsonPropertyName("issues")] IReadOnlyList<AdminManifestValidationIssueDto> Issues,
    [property: JsonPropertyName("metadata")] AdminPromotionManifestMetadataDto? Metadata
);
