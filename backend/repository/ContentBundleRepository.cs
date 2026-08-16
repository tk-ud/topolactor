using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

public record ContentEntityDraftRecord(
    Guid DraftId,
    Guid HubId,
    string EntityJsonb,
    IReadOnlyList<Guid> RelationIds,
    Guid? StateId,
    string Status,
    Guid? PromotedEntityId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record ContentBundleRefContext(
    bool HubExists,
    string? HubRelationName,
    IReadOnlyDictionary<Guid, string> RelationNames,
    IReadOnlyDictionary<Guid, string> StateNames
);

/// <summary>
/// Admin content bundle repository — browse converged topology data and manage entity drafts.
/// Distinct from TopologyRepository runtime reads.
/// </summary>
public abstract class ContentBundleRepository
{
    protected readonly ILogger<ContentBundleRepository> _logger;

    protected ContentBundleRepository(ILogger<ContentBundleRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract Task<IReadOnlyList<ContentBundleListItemDto>> ListContentHubsAsync(CancellationToken ct = default);
    public abstract Task<IReadOnlyList<ContentBundleListItemDto>> ListContentEntitiesAsync(CancellationToken ct = default);
    public abstract Task<IReadOnlyList<ContentBundleListItemDto>> ListContentRelationsAsync(CancellationToken ct = default);
    public abstract Task<IReadOnlyList<ContentBundleListItemDto>> ListContentHubRelationsAsync(CancellationToken ct = default);
    public abstract Task<IReadOnlyList<ContentBundleStateItemDto>> ListContentStatesAsync(CancellationToken ct = default);
    public abstract Task<ContentBundleEntityDetailDto?> LoadContentEntityAsync(Guid entityId, CancellationToken ct = default);
    public abstract Task<ContentBundleHubDetailDto?> LoadContentHubAsync(Guid hubId, CancellationToken ct = default);
    public abstract Task<ContentBundleRelationDetailDto?> LoadContentRelationAsync(Guid relationRegistryId, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<ContentBundleListItemDto>> SearchContentBundleAsync(
        string? keyword, string? kind, string? state, CancellationToken ct = default);

    public abstract Task<(ContentBundleDraftDetailDto? Draft, ValidationError? Error)> CreateEntityDraftAsync(
        Guid hubId,
        JsonElement entityJsonb,
        IReadOnlyList<Guid> relationIds,
        string stateName,
        CancellationToken ct = default);

    public abstract Task<(ContentBundleDraftDetailDto? Draft, ValidationError? Error)> UpdateEntityDraftAsync(
        Guid draftId,
        Guid hubId,
        JsonElement entityJsonb,
        IReadOnlyList<Guid> relationIds,
        string stateName,
        CancellationToken ct = default);

    public abstract Task<ContentEntityDraftRecord?> LoadDraftAsync(Guid draftId, CancellationToken ct = default);

    /// <summary>
    /// Lists content_entity_drafts with status='draft', ordered by created_at DESC.
    /// Used by the /demo draft preview surface to populate the draft selector.
    /// Returns empty list when no drafts exist — not an error.
    /// </summary>
    public abstract Task<IReadOnlyList<EntityDraftListItemDto>> ListEntityDraftsAsync(CancellationToken ct = default);

    public abstract Task<ContentBundleRefContext> LoadRefContextAsync(
        Guid hubId,
        IReadOnlyList<Guid> relationIds,
        string? stateName,
        CancellationToken ct = default);

    public abstract Task<(ContentBundleLifecycleResponseDto Response, ValidationError? Error)> PromoteDraftAsync(
        Guid draftId,
        CancellationToken ct = default);

    // Hub Navigation methods
    public abstract Task<IReadOnlyList<HubNavigationManifestItemDto>> ListTopologyManifestsAsync(CancellationToken ct = default);
    public abstract Task<IReadOnlyList<HubNavigationHubRelationItemDto>> ListHubRelationsByManifestAsync(Guid topologyManifestId, CancellationToken ct = default);
    public abstract Task<(HubNavigationLifecycleResponseDto Response, ValidationError? Error)> CreateHubRelationAsync(Guid topologyManifestId, Guid relatedHubId, int sequencePosition, CancellationToken ct = default);
    public abstract Task<(HubNavigationLifecycleResponseDto Response, ValidationError? Error)> UpdateHubRelationAsync(Guid hubRelationId, Guid relatedHubId, CancellationToken ct = default);
    public abstract Task<(HubNavigationLifecycleResponseDto Response, ValidationError? Error)> DeprecateHubRelationAsync(Guid hubRelationId, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<HubNavigationSequenceItemDto>> LoadHubNavigationSequenceAsync(Guid topologyManifestId, CancellationToken ct = default);
    public abstract Task<(HubNavigationReorderResponseDto Response, ValidationError? Error)> ReorderHubRelationsAsync(Guid topologyManifestId, IReadOnlyList<(Guid HubRelationId, int NewSequencePosition)> items, CancellationToken ct = default);

    /// <summary>
    /// Resolves the topology_manifest_id owning the single hub_relations row explicitly marked
    /// as the canonical default entry (relation_config.transition == "canonical_default_entry",
    /// status='active') — the means by which a bare/no-selection projection entry (no route, no
    /// manifest, no target_ref) resolves an initial manifest, distinct from
    /// LoadHubNavigationSequenceAsync's outbound "current hub relation" navigation links from an
    /// ALREADY-resolved manifest. This is deliberately NOT "any hub_relations row at
    /// sequence_position=1" (that would be an unscoped/ambiguous global index — many manifests
    /// have their own position-1 outbound relation) — only the explicitly marked row qualifies.
    /// Returns null when no row carries the marker (no canonical default entry configured — the
    /// caller must not guess or fall back to an arbitrary manifest). Throws
    /// InvalidOperationException("CANONICAL_DEFAULT_ENTRY_AMBIGUOUS: ...") when more than one row
    /// carries the marker — ambiguity is a configuration error, never silently resolved.
    /// </summary>
    public abstract Task<Guid?> ResolveCanonicalDefaultEntryManifestIdAsync(CancellationToken ct = default);

    /// <summary>
    /// production_projection_connectivity_invariant (docs/design/db-schema.yaml
    /// hub_relations.minimum_cardinality_completion_invariant): true only when
    /// topologyManifestId has at least one hub_relations row that is both status='active' AND
    /// resolves to exactly one active target topology_manifest -- the same resolution semantics
    /// LoadHubNavigationSequenceAsync already applies (reused here, not duplicated as a separate
    /// COUNT-only query, per the reusable-abstraction-first rule: zero relations, deprecated-only
    /// relations, and an active-but-unresolvable-target relation all correctly evaluate to false).
    /// </summary>
    public virtual async Task<bool> HasResolvableActiveHubRelationAsync(
        Guid topologyManifestId, CancellationToken ct = default)
    {
        var sequence = await LoadHubNavigationSequenceAsync(topologyManifestId, ct);
        return sequence.Any(item => item.TargetManifestId is not null);
    }
}
