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
/// Request to promote multiple bucket items into a single package for a route.
/// All bucketItemIds must be UUIDs of rows in ui_component_bucket (bucketed or packaging status).
/// routeKey determines the package_key ({routeKey}:pkg) — 1 route = 1 package.
/// </summary>
public record PackageGenerateBatchRequestDto(
    [property: JsonPropertyName("routeKey")]      string RouteKey,
    [property: JsonPropertyName("bucketItemIds")] IReadOnlyList<string> BucketItemIds
);

/// <summary>
/// Request to remove component keys from a route package (canvas last-node delete sync).
/// </summary>
public record PackageDetachComponentsRequestDto(
    [property: JsonPropertyName("routeKey")]       string RouteKey,
    [property: JsonPropertyName("componentKeys")] IReadOnlyList<string> ComponentKeys
);

/// <summary>
/// Internal result of detach_package_components.
/// </summary>
public record PackageDetachComponentsResult(
    PackageGenerateCode Code,
    Guid? PackageId,
    IReadOnlyList<string> DetachedComponentKeys,
    string? ErrorCode = null,
    string? Message = null
);

/// <summary>
/// API response for package_generator:detach_package_components.
/// </summary>
public record PackageDetachComponentsResponseDto(
    [property: JsonPropertyName("ok")]                    bool Ok,
    [property: JsonPropertyName("packageId")]             string? PackageId,
    [property: JsonPropertyName("detachedComponentKeys")] IReadOnlyList<string> DetachedComponentKeys,
    [property: JsonPropertyName("message")]               string Message,
    [property: JsonPropertyName("errorCode")]              string? ErrorCode = null
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
/// Internal result of the batch promotion pipeline (promote_package).
/// ComponentIds and BucketItemIds are non-empty only when Code = Success.
/// </summary>
public record PackageGenerateBatchResult(
    PackageGenerateCode Code,
    Guid? TensorId,
    Guid? PackageId,
    Guid? LayoutId,
    Guid? WiringId,
    IReadOnlyList<Guid> ComponentIds,
    IReadOnlyList<Guid> BucketItemIds,
    string? ErrorCode = null,
    string? Message = null
);

/// <summary>
/// API response for package_generator:promote_package.
/// packageId is the single package created for this routeKey.
/// componentIds and bucketItemIds reflect the member set stored in package_schema_json.
/// </summary>
public record PackageGenerateBatchResponseDto(
    [property: JsonPropertyName("ok")]             bool Ok,
    [property: JsonPropertyName("tensorId")]       string? TensorId,
    [property: JsonPropertyName("packageId")]      string? PackageId,
    [property: JsonPropertyName("layoutId")]       string? LayoutId,
    [property: JsonPropertyName("wiringId")]       string? WiringId,
    [property: JsonPropertyName("bucketItemIds")]  IReadOnlyList<string> BucketItemIds,
    [property: JsonPropertyName("componentIds")]   IReadOnlyList<string> ComponentIds,
    [property: JsonPropertyName("message")]        string Message,
    [property: JsonPropertyName("errorCode")]      string? ErrorCode = null
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

/// <summary>Layout tensor node shape for SSOT v0.8.0 layout_editor (documentation contract).</summary>
public record LayoutPatchNodeDto(
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("nodeKind")] string? NodeKind,
    [property: JsonPropertyName("htmlTag")] string? HtmlTag,
    [property: JsonPropertyName("componentKey")] string? ComponentKey,
    [property: JsonPropertyName("parentNodeId")] string? ParentNodeId,
    [property: JsonPropertyName("slotKey")] string? SlotKey,
    [property: JsonPropertyName("orderIndex")] int? OrderIndex,
    [property: JsonPropertyName("layoutClassRefs")] IReadOnlyList<string>? LayoutClassRefs
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

/// <summary>Read-only layout_patch_json for UI Builder canvas hydrate (no mutation).
/// tensorPatchJson is the effective canvas state: layout_draft_tmp_json if present, else layout_patch_json.
/// hasTmpDraft=true means the returned JSON is the tmp auto-save (unsaved changes exist).</summary>
public record LayoutPatchDraftDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("layoutId")] string LayoutId,
    [property: JsonPropertyName("routeKey")] string RouteKey,
    [property: JsonPropertyName("tensorPatchJson")] string TensorPatchJson,
    [property: JsonPropertyName("found")] bool Found,
    [property: JsonPropertyName("hasTmpDraft")] bool HasTmpDraft = false
);

/// <summary>Payload for layout_patch:save_tmp — auto-saves canvas state before explicit apply.</summary>
public record LayoutPatchSaveTmpRequestDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("layoutId")] string LayoutId,
    [property: JsonPropertyName("routeKey")] string RouteKey,
    [property: JsonPropertyName("tmpJson")] string TmpJson
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
    [property: JsonPropertyName("wiringId")] string? WiringId,
    [property: JsonPropertyName("componentIds")] IReadOnlyList<string>? ComponentIds = null,
    [property: JsonPropertyName("bucketItemIds")] IReadOnlyList<string>? BucketItemIds = null
);

public record AdminPackageComponentDto(
    [property: JsonPropertyName("componentId")] string ComponentId,
    [property: JsonPropertyName("componentKey")] string ComponentKey,
    [property: JsonPropertyName("componentKind")] string ComponentKind
);

public record ComponentStyleDesignListItemDto(
    [property: JsonPropertyName("designId")] string DesignId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("componentId")] string? ComponentId,
    [property: JsonPropertyName("layoutNodeId")] string? LayoutNodeId = null,
    [property: JsonPropertyName("cssTokenRefs")] IReadOnlyList<string>? CssTokenRefs = null,
    [property: JsonPropertyName("responsiveTokenRefs")] Dictionary<string, IReadOnlyList<string>>? ResponsiveTokenRefs = null,
    [property: JsonPropertyName("inlineText")] string? InlineText = null,
    [property: JsonPropertyName("linkHref")] string? LinkHref = null,
    [property: JsonPropertyName("linkTarget")] string? LinkTarget = null,
    [property: JsonPropertyName("reactionIntent")] string? ReactionIntent = null,
    [property: JsonPropertyName("classname")] string? Classname = null,
    [property: JsonPropertyName("tailwind")] string? Tailwind = null,
    [property: JsonPropertyName("hasDesignTmpDraft")] bool HasDesignTmpDraft = false
);

public record ComponentStyleDesignUpsertRequestDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("componentId")] string? ComponentId,
    [property: JsonPropertyName("layoutNodeId")] string? LayoutNodeId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("classname")] string? Classname,
    [property: JsonPropertyName("tailwind")] string? Tailwind,
    [property: JsonPropertyName("cssTokenRefs")] IReadOnlyList<string>? CssTokenRefs,
    [property: JsonPropertyName("responsiveTokenRefs")] Dictionary<string, IReadOnlyList<string>>? ResponsiveTokenRefs,
    [property: JsonPropertyName("inlineText")] string? InlineText,
    [property: JsonPropertyName("linkHref")] string? LinkHref,
    [property: JsonPropertyName("linkTarget")] string? LinkTarget,
    [property: JsonPropertyName("reactionIntent")] string? ReactionIntent
);


/// <summary>Payload for component_style_design:save_tmp — auto-saves selected canvas node design before explicit upsert.</summary>
public record ComponentStyleDesignSaveTmpRequestDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("componentId")] string? ComponentId,
    [property: JsonPropertyName("layoutNodeId")] string? LayoutNodeId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("classname")] string? Classname,
    [property: JsonPropertyName("tailwind")] string? Tailwind,
    [property: JsonPropertyName("cssTokenRefs")] IReadOnlyList<string>? CssTokenRefs,
    [property: JsonPropertyName("responsiveTokenRefs")] Dictionary<string, IReadOnlyList<string>>? ResponsiveTokenRefs,
    [property: JsonPropertyName("inlineText")] string? InlineText,
    [property: JsonPropertyName("linkHref")] string? LinkHref,
    [property: JsonPropertyName("linkTarget")] string? LinkTarget,
    [property: JsonPropertyName("reactionIntent")] string? ReactionIntent
);

public record AdminPackageWiringDto(
    [property: JsonPropertyName("wiringId")] string WiringId,
    [property: JsonPropertyName("wiringKey")] string WiringKey,
    [property: JsonPropertyName("wiringKind")] string WiringKind,
    [property: JsonPropertyName("targetSurface")] string TargetSurface,
    [property: JsonPropertyName("targetRef")] string? TargetRef
);

public record ExternalPortAuthoringCandidateDto(
    [property: JsonPropertyName("portId")] string PortId,
    [property: JsonPropertyName("portKind")] string PortKind,
    [property: JsonPropertyName("providerKind")] string ProviderKind,
    [property: JsonPropertyName("credentialKind")] string CredentialKind,
    [property: JsonPropertyName("referenceKey")] string? ReferenceKey,
    [property: JsonPropertyName("requiredByBundle")] string RequiredByBundle,
    [property: JsonPropertyName("consumerBundleBinding")] string? ConsumerBundleBinding,
    [property: JsonPropertyName("urlOrEnvReference")] string? UrlOrEnvReference,
    [property: JsonPropertyName("hookPath")] string? HookPath,
    [property: JsonPropertyName("routeKey")] string? RouteKey,
    [property: JsonPropertyName("targetRef")] string TargetRef
);

public record InstanceOperationAuthoringCandidateDto(
    [property: JsonPropertyName("portKind")] string PortKind,
    [property: JsonPropertyName("instancePortId")] string InstancePortId,
    [property: JsonPropertyName("operationBindingKey")] string OperationBindingKey,
    [property: JsonPropertyName("operationKey")] string OperationKey,
    [property: JsonPropertyName("approvalStatus")] string ApprovalStatus,
    [property: JsonPropertyName("targetRef")] string TargetRef
);

public record PackageWiringUpdateRequestDto(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("wiringId")] string WiringId,
    [property: JsonPropertyName("wiringKind")] string WiringKind,
    [property: JsonPropertyName("targetSurface")] string TargetSurface,
    [property: JsonPropertyName("targetRef")] string? TargetRef
);
