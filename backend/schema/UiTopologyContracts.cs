using System.Text.Json.Serialization;

namespace Topolactor.Schema;

/// <summary>
/// A single row from ui_component_bucket.
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
