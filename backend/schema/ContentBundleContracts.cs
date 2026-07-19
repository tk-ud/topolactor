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

// ---------------------------------------------------------------------------
// Hub Navigation DTOs
// ---------------------------------------------------------------------------

public record HubNavigationManifestItemDto(
    [property: JsonPropertyName("topologyManifestId")] string TopologyManifestId,
    [property: JsonPropertyName("manifestKey")] string ManifestKey,
    [property: JsonPropertyName("hubId")] string HubId,
    [property: JsonPropertyName("hasHubRelations")] bool HasHubRelations,
    [property: JsonPropertyName("hubRelationCount")] int HubRelationCount
);

public record HubNavigationHubRelationItemDto(
    [property: JsonPropertyName("hubRelationId")] string HubRelationId,
    [property: JsonPropertyName("topologyManifestId")] string TopologyManifestId,
    [property: JsonPropertyName("relatedHubId")] string RelatedHubId,
    [property: JsonPropertyName("relatedHubLabel")] string RelatedHubLabel,
    [property: JsonPropertyName("sequencePosition")] int SequencePosition,
    [property: JsonPropertyName("relationConfig")] string? RelationConfig,
    [property: JsonPropertyName("status")] string Status
);

public record HubNavigationLifecycleResponseDto(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("hubRelationId")] string? HubRelationId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null
);

public record HubNavigationGetRequestDto(
    [property: JsonPropertyName("topologyManifestId")] string TopologyManifestId
);

public record HubNavigationCreateRequestDto(
    [property: JsonPropertyName("topologyManifestId")] string TopologyManifestId,
    [property: JsonPropertyName("relatedHubId")] string RelatedHubId,
    [property: JsonPropertyName("sequencePosition")] int SequencePosition
);

public record HubNavigationUpdateRequestDto(
    [property: JsonPropertyName("hubRelationId")] string HubRelationId,
    [property: JsonPropertyName("relatedHubId")] string RelatedHubId
);

public record HubNavigationDeprecateRequestDto(
    [property: JsonPropertyName("hubRelationId")] string HubRelationId
);

/// <summary>
/// TargetManifestId is the navigable topology_manifest under RelatedHubId, resolved only when
/// exactly one active hubs.topology_manifests row exists for that hub — no implicit
/// oldest/first-match fallback (mirrors topology.physical_table_manifest_bindings'
/// no_implicit_join_nullable_fallback_or_oldest_manifest_fallback invariant). Null when zero or
/// multiple manifests are registered under the related hub; callers must treat null as
/// "not directly callable" rather than guessing a target.
/// </summary>
public record HubNavigationSequenceItemDto(
    [property: JsonPropertyName("hubRelationId")] string HubRelationId,
    [property: JsonPropertyName("topologyManifestId")] string TopologyManifestId,
    [property: JsonPropertyName("relatedHubId")] string RelatedHubId,
    [property: JsonPropertyName("relatedHubLabel")] string RelatedHubLabel,
    [property: JsonPropertyName("sequencePosition")] int SequencePosition,
    [property: JsonPropertyName("targetManifestId")] string? TargetManifestId = null
);

public record HubNavigationReorderItemDto(
    [property: JsonPropertyName("hubRelationId")] string HubRelationId,
    [property: JsonPropertyName("newSequencePosition")] int NewSequencePosition
);

public record HubNavigationReorderResponseDto(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null
);

/// <summary>
/// Read-only navigation link-list projection for authenticated surfaces that have no business
/// projection of their own (e.g. the Normal Dashboard landing page). Derived exclusively from the
/// existing hub_relation authority (HubNavigationResolver / ContentBundleRepository — the same
/// repository RuntimeExecutor's manifest-scoped NavigationSequence enrichment already reads), never
/// a new parallel authority. Links is an explicit empty array — never a fabricated placeholder —
/// when no canonical default entry manifest resolves or it has no active hub relations. This is
/// strictly read-only: hub_navigation mutations remain reachable only via the existing admin-gated
/// /dispatch lane, and a mutation-authority failure there must never be represented as (or
/// collapsed into) a successful empty/partial response from this projection.
/// </summary>
public record HubRelationNavigationLinksResponseDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("links")] IReadOnlyList<HubNavigationSequenceItemDto> Links,
    [property: JsonPropertyName("errors")] IReadOnlyList<ValidationError>? Errors = null,
    [property: JsonPropertyName("requestedSurface")] string? RequestedSurface = null,
    [property: JsonPropertyName("fallbackReason")] string? FallbackReason = null
);
