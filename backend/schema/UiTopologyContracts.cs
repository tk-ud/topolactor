using System.Text.Json.Serialization;

namespace Topolactor.Schema;

/// <summary>
/// A single row from topology.components_bucket.
/// </summary>
public record UiComponentBucketItemDto(
    [property: JsonPropertyName("bucketItemId")]   string BucketItemId,
    [property: JsonPropertyName("componentKey")]   string ComponentKey,
    [property: JsonPropertyName("sourcePath")]     string SourcePath,
    [property: JsonPropertyName("componentKind")]  string ComponentKind,
    [property: JsonPropertyName("status")]         string Status
);

public record UiComponentBucketCreateRequestDto(
    [property: JsonPropertyName("componentKey")]   string ComponentKey,
    [property: JsonPropertyName("sourcePath")]     string SourcePath,
    [property: JsonPropertyName("componentKind")]  string ComponentKind,
    [property: JsonPropertyName("metadataJson")]   string? MetadataJson = null
);

public enum UiComponentBucketCreateCode
{
    Success,
    ConstraintViolation,
    MalformedMetadataJson,
    DbUnavailable,
}

public record UiComponentBucketCreateResult(
    UiComponentBucketCreateCode Code,
    UiComponentBucketRecord? Record,
    string? ErrorCode = null,
    string? Message = null
);

/// <summary>
/// Request to promote a single bucket item through the package generator pipeline.
/// bucketItemId must be the UUID primary key of a 'bucketed' row in ui_component_bucket.
/// routeKey is the route to associate in ui_topology_tensor.
/// </summary>
public record PackageGenerateRequestDto(
    [property: JsonPropertyName("bucketItemId")]  string BucketItemId,
    [property: JsonPropertyName("routeKey")]      string RouteKey
);

/// <summary>
/// Result code for PackageGeneratorRuntime.GenerateAsync / UiTopologyRepository.PromoteBucketItemAsync.
/// </summary>
public enum PackageGenerateCode
{
    Success,
    NotFound,
    NotBucketed,
    ConstraintViolation,
    DbUnavailable,
    PromotionFailed,
}

/// <summary>
/// Internal result of the promotion pipeline.
/// All IDs are non-null only when Code = Success.
/// </summary>
public record PackageGenerateResult(
    PackageGenerateCode Code,
    Guid? TensorId,
    Guid? ComponentId,
    Guid? PackageId,
    Guid? LayoutId,
    Guid? WiringId,
    string? ErrorCode = null,
    string? Message = null
);

/// <summary>
/// API response for POST /admin/package-generator/generate.
/// </summary>
public record PackageGenerateResponseDto(
    [property: JsonPropertyName("ok")]           bool Ok,
    [property: JsonPropertyName("tensorId")]     string? TensorId,
    [property: JsonPropertyName("componentId")]  string? ComponentId,
    [property: JsonPropertyName("packageId")]    string? PackageId,
    [property: JsonPropertyName("layoutId")]     string? LayoutId,
    [property: JsonPropertyName("wiringId")]     string? WiringId,
    [property: JsonPropertyName("message")]      string Message,
    [property: JsonPropertyName("errorCode")]    string? ErrorCode = null
);

/// <summary>
/// Internal record representing a row from ui_component_bucket.
/// </summary>
public record UiComponentBucketRecord(
    Guid BucketItemId,
    string ComponentKey,
    string SourcePath,
    string ComponentKind,
    string Status
);

public record LayoutPatchRequestDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("layoutId")] string LayoutId,
    [property: JsonPropertyName("routeKey")] string RouteKey,
    [property: JsonPropertyName("tensorPatchJson")] string? TensorPatchJson,
    [property: JsonPropertyName("cssTokenRefs")] IReadOnlyList<string>? CssTokenRefs,
    [property: JsonPropertyName("responsiveTokenRefs")] Dictionary<string, IReadOnlyList<string>>? ResponsiveTokenRefs
);

public record LayoutPatchResult(
    bool Ok,
    bool Valid,
    string LayoutId,
    string RouteKey,
    string TensorPatchJson,
    IReadOnlyList<string> CssTokenRefs,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ResponsiveTokenRefs,
    string Message
);

public record PromotedPaletteEntryDto(
    [property: JsonPropertyName("componentKey")] string ComponentKey,
    [property: JsonPropertyName("componentKind")] string ComponentKind,
    [property: JsonPropertyName("componentId")] string ComponentId,
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("layoutId")] string LayoutId,
    [property: JsonPropertyName("wiringId")] string WiringId,
    [property: JsonPropertyName("tensorId")] string TensorId,
    [property: JsonPropertyName("routeKey")] string RouteKey
);

/// <summary>
/// Distinct layout/route projection for admin UI selectors.
/// Source: ui_topology_tensor JOIN ui_layout_registry (DB projection only).
/// </summary>
public record LayoutCandidateDto(
    [property: JsonPropertyName("layoutId")] string LayoutId,
    [property: JsonPropertyName("layoutKey")] string LayoutKey,
    [property: JsonPropertyName("routeKey")] string RouteKey,
    [property: JsonPropertyName("layoutKind")] string LayoutKind,
    [property: JsonPropertyName("slotKeys")] IReadOnlyList<string> SlotKeys
);

public record AdminPackageListItemDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("packageKey")] string PackageKey,
    [property: JsonPropertyName("routeKey")] string? RouteKey,
    [property: JsonPropertyName("layoutId")] string? LayoutId,
    [property: JsonPropertyName("wiringId")] string? WiringId
);

public record AdminPackageComponentDto(
    [property: JsonPropertyName("componentId")] string ComponentId,
    [property: JsonPropertyName("componentKey")] string ComponentKey,
    [property: JsonPropertyName("componentKind")] string ComponentKind
);

public record ComponentStyleDesignListItemDto(
    [property: JsonPropertyName("designId")] string DesignId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("componentId")] string? ComponentId
);

public record ComponentStyleDesignUpsertRequestDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("componentId")] string ComponentId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("classname")] string? Classname,
    [property: JsonPropertyName("tailwind")] string? Tailwind,
    [property: JsonPropertyName("cssTokenRefs")] IReadOnlyList<string>? CssTokenRefs,
    [property: JsonPropertyName("reactionIntent")] string? ReactionIntent
);

public record AdminPackageWiringDto(
    [property: JsonPropertyName("wiringId")] string WiringId,
    [property: JsonPropertyName("wiringKey")] string WiringKey,
    [property: JsonPropertyName("wiringKind")] string WiringKind,
    [property: JsonPropertyName("targetSurface")] string TargetSurface,
    [property: JsonPropertyName("targetRef")] string? TargetRef
);

public record PackageWiringUpdateRequestDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("wiringId")] string WiringId,
    [property: JsonPropertyName("wiringKind")] string WiringKind,
    [property: JsonPropertyName("targetSurface")] string TargetSurface,
    [property: JsonPropertyName("targetRef")] string? TargetRef
);
