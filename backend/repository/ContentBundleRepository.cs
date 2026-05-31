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
    public abstract Task<(HubNavigationLifecycleResponseDto Response, ValidationError? Error)> UpdateHubRelationAsync(Guid hubRelationId, Guid relatedHubId, int sequencePosition, CancellationToken ct = default);
    public abstract Task<(HubNavigationLifecycleResponseDto Response, ValidationError? Error)> DeprecateHubRelationAsync(Guid hubRelationId, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<HubNavigationSequenceItemDto>> LoadHubNavigationSequenceAsync(Guid topologyManifestId, CancellationToken ct = default);
    public abstract Task<(HubNavigationReorderResponseDto Response, ValidationError? Error)> ReorderHubRelationsAsync(Guid topologyManifestId, IReadOnlyList<(Guid HubRelationId, int NewSequencePosition)> items, CancellationToken ct = default);
}
