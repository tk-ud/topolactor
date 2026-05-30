using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// In-memory content bundle repository for admin content bundle unit tests.
/// Seeds demo hub/entity/relation data matching db/demo_seed.sql UUIDs.
/// </summary>
internal sealed class InMemoryContentBundleRepository : ContentBundleRepository
{
    public static readonly Guid DemoHubId = new("00000000-0000-0000-0000-000000000010");
    public static readonly Guid DemoRelationId = new("00000000-0000-0000-0000-000000000011");
    public static readonly Guid DemoEntityAlphaId = new("00000000-0000-0000-0000-000000000041");
    public static readonly Guid DemoHubRelationId = new("00000000-0000-0000-0000-000000000012");
    public static readonly Guid ActiveStateId = new("00000000-0000-0000-0000-000000000001");

    private readonly List<ContentEntityDraftRecord> _drafts = [];
    private readonly List<(Guid EntityId, Guid HubId, string Label, string StateName, IReadOnlyList<Guid> RelationIds, string EntityJsonb)> _entities =
    [
        (DemoEntityAlphaId, DemoHubId, "Alpha Entity", "active",
            [DemoRelationId],
            """{"label":"Alpha Entity","state":"active","hub_id":"00000000-0000-0000-0000-000000000010"}"""),
    ];

    public InMemoryContentBundleRepository() : base(NullLogger<ContentBundleRepository>.Instance) { }

    public override Task<IReadOnlyList<ContentBundleListItemDto>> ListContentHubsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ContentBundleListItemDto> items =
        [
            new(DemoHubId.ToString(), "hub", "Hub demo…", "active", DemoHubId.ToString(), null,
                "relation=demo_relation, state=active"),
        ];
        return Task.FromResult(items);
    }

    public override Task<IReadOnlyList<ContentBundleListItemDto>> ListContentEntitiesAsync(CancellationToken ct = default)
    {
        var items = _entities.Select(e => new ContentBundleListItemDto(
            e.EntityId.ToString(), "entity", e.Label, e.StateName, e.HubId.ToString(),
            e.RelationIds.Select(r => r.ToString()).ToList(),
            $"hub={e.HubId}, relations={e.RelationIds.Count}")).ToList();
        return Task.FromResult<IReadOnlyList<ContentBundleListItemDto>>(items);
    }

    public override Task<IReadOnlyList<ContentBundleListItemDto>> ListContentRelationsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ContentBundleListItemDto> items =
        [
            new(DemoRelationId.ToString(), "relation", "demo_relation", "active", null, null,
                "relation_registry name=demo_relation"),
        ];
        return Task.FromResult(items);
    }

    public override Task<IReadOnlyList<ContentBundleStateItemDto>> ListContentStatesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ContentBundleStateItemDto> items =
        [
            new(ActiveStateId.ToString(), "active", "system"),
            new(Guid.NewGuid().ToString(), "operating", "business"),
        ];
        return Task.FromResult(items);
    }

    public override Task<IReadOnlyList<ContentBundleListItemDto>> ListContentHubRelationsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ContentBundleListItemDto> items =
        [
            new(DemoHubRelationId.ToString(), "hub_relation", "demo_relation", "active",
                DemoHubId.ToString(), [DemoRelationId.ToString()],
                $"hub={DemoHubId}, relation={DemoRelationId}, seq=0"),
        ];
        return Task.FromResult(items);
    }

    public override Task<ContentBundleHubDetailDto?> LoadContentHubAsync(Guid hubId, CancellationToken ct = default)
    {
        if (hubId != DemoHubId) return Task.FromResult<ContentBundleHubDetailDto?>(null);
        var entityIds = _entities.Where(e => e.HubId == hubId).Select(e => e.EntityId.ToString()).ToList();
        return Task.FromResult<ContentBundleHubDetailDto?>(new ContentBundleHubDetailDto(
            DemoHubId.ToString(), "active", ActiveStateId.ToString(),
            DemoRelationId.ToString(), "demo_relation",
            entityIds.Count, 1, entityIds,
            $"hub with {entityIds.Count} entity(ies), 1 hub_relation(s)"));
    }

    public override Task<ContentBundleRelationDetailDto?> LoadContentRelationAsync(
        Guid relationRegistryId, CancellationToken ct = default)
    {
        if (relationRegistryId != DemoRelationId) return Task.FromResult<ContentBundleRelationDetailDto?>(null);
        var entityCount = _entities.Count(e => e.RelationIds.Contains(DemoRelationId));
        return Task.FromResult<ContentBundleRelationDetailDto?>(new ContentBundleRelationDetailDto(
            DemoRelationId.ToString(), "demo_relation", true, entityCount, 1,
            $"relation demo_relation — {entityCount} entity(ies), 1 hub_relation(s)"));
    }

    public override Task<ContentBundleEntityDetailDto?> LoadContentEntityAsync(Guid entityId, CancellationToken ct = default)
    {
        var e = _entities.FirstOrDefault(x => x.EntityId == entityId);
        if (e == default) return Task.FromResult<ContentBundleEntityDetailDto?>(null);

        return Task.FromResult<ContentBundleEntityDetailDto?>(new ContentBundleEntityDetailDto(
            e.EntityId.ToString(), e.Label, e.StateName, ActiveStateId.ToString(),
            e.HubId.ToString(), "demo_relation",
            e.RelationIds.Select(r => r.ToString()).ToList(), ["demo_relation"],
            e.EntityJsonb, $"entity {e.Label} in hub {e.HubId}"));
    }

    public override Task<IReadOnlyList<ContentBundleListItemDto>> SearchContentBundleAsync(
        string? keyword, string? kind, string? state, CancellationToken ct = default)
    {
        var all = new List<ContentBundleListItemDto>();
        all.AddRange(ListContentHubsAsync(ct).Result);
        all.AddRange(ListContentEntitiesAsync(ct).Result);
        all.AddRange(ListContentRelationsAsync(ct).Result);
        all.AddRange(ListContentHubRelationsAsync(ct).Result);

        IEnumerable<ContentBundleListItemDto> query = all;
        if (!string.IsNullOrWhiteSpace(kind))
            query = query.Where(i => i.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(i => i.State.Equals(state, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLowerInvariant();
            query = query.Where(i =>
                i.Label.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                i.Id.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
        return Task.FromResult<IReadOnlyList<ContentBundleListItemDto>>(query.ToList());
    }

    public override Task<(ContentBundleDraftDetailDto? Draft, ValidationError? Error)> CreateEntityDraftAsync(
        Guid hubId, JsonElement entityJsonb, IReadOnlyList<Guid> relationIds, string stateName, CancellationToken ct = default)
    {
        var refs = BuildRefContext(hubId, relationIds, stateName);
        var temp = new ContentEntityDraftRecord(
            Guid.Empty, hubId, entityJsonb.GetRawText(), relationIds,
            refs.StateNames.FirstOrDefault(kv => kv.Value.Equals(stateName, StringComparison.OrdinalIgnoreCase)).Key,
            "draft", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var validation = ContentBundleValidator.ValidateDraft(temp, refs);
        if (!validation.Valid)
        {
            var first = validation.Issues.First(i => i.IsBlocking);
            return Task.FromResult<(ContentBundleDraftDetailDto?, ValidationError?)>(
                (null, new ValidationError(first.Code, first.Message)));
        }

        var stateId = refs.StateNames.First(kv => kv.Value.Equals(stateName, StringComparison.OrdinalIgnoreCase)).Key;
        var draftId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _drafts.Add(new ContentEntityDraftRecord(
            draftId, hubId, entityJsonb.GetRawText(), relationIds, stateId, "draft", null, now, now));

        return Task.FromResult<(ContentBundleDraftDetailDto?, ValidationError?)>(
            (new ContentBundleDraftDetailDto(
                draftId.ToString(), "draft", hubId.ToString(), entityJsonb.GetRawText(),
                relationIds.Select(r => r.ToString()).ToList(), stateName, stateId.ToString(), null,
                now.ToString("o"), now.ToString("o")), null));
    }

    public override Task<(ContentBundleDraftDetailDto? Draft, ValidationError? Error)> UpdateEntityDraftAsync(
        Guid draftId, Guid hubId, JsonElement entityJsonb, IReadOnlyList<Guid> relationIds, string stateName,
        CancellationToken ct = default)
    {
        var existing = _drafts.FirstOrDefault(d => d.DraftId == draftId);
        if (existing is null)
            return Task.FromResult<(ContentBundleDraftDetailDto?, ValidationError?)>(
                (null, new ValidationError("DRAFT_NOT_FOUND", $"Draft {draftId} was not found.")));
        if (existing.Status != "draft")
            return Task.FromResult<(ContentBundleDraftDetailDto?, ValidationError?)>(
                (null, new ValidationError("DRAFT_NOT_EDITABLE", $"Draft {draftId} is not in draft status.")));

        var refs = BuildRefContext(hubId, relationIds, stateName);
        var temp = new ContentEntityDraftRecord(
            draftId, hubId, entityJsonb.GetRawText(), relationIds,
            refs.StateNames.FirstOrDefault(kv => kv.Value.Equals(stateName, StringComparison.OrdinalIgnoreCase)).Key,
            "draft", null, existing.CreatedAt, DateTimeOffset.UtcNow);

        var validation = ContentBundleValidator.ValidateDraft(temp, refs);
        if (!validation.Valid)
        {
            var first = validation.Issues.First(i => i.IsBlocking);
            return Task.FromResult<(ContentBundleDraftDetailDto?, ValidationError?)>(
                (null, new ValidationError(first.Code, first.Message)));
        }

        var stateId = refs.StateNames.First(kv => kv.Value.Equals(stateName, StringComparison.OrdinalIgnoreCase)).Key;
        var idx = _drafts.FindIndex(d => d.DraftId == draftId);
        var now = DateTimeOffset.UtcNow;
        _drafts[idx] = new ContentEntityDraftRecord(
            draftId, hubId, entityJsonb.GetRawText(), relationIds, stateId, "draft", null, existing.CreatedAt, now);

        return Task.FromResult<(ContentBundleDraftDetailDto?, ValidationError?)>(
            (new ContentBundleDraftDetailDto(
                draftId.ToString(), "draft", hubId.ToString(), entityJsonb.GetRawText(),
                relationIds.Select(r => r.ToString()).ToList(), stateName, stateId.ToString(), null,
                existing.CreatedAt.ToString("o"), now.ToString("o")), null));
    }

    public override Task<ContentEntityDraftRecord?> LoadDraftAsync(Guid draftId, CancellationToken ct = default)
        => Task.FromResult(_drafts.FirstOrDefault(d => d.DraftId == draftId));

    public override Task<ContentBundleRefContext> LoadRefContextAsync(
        Guid hubId, IReadOnlyList<Guid> relationIds, string? stateName, CancellationToken ct = default)
        => Task.FromResult(BuildRefContext(hubId, relationIds, stateName));

    public override Task<(ContentBundleLifecycleResponseDto Response, ValidationError? Error)> PromoteDraftAsync(
        Guid draftId, CancellationToken ct = default)
    {
        var draft = _drafts.FirstOrDefault(d => d.DraftId == draftId);
        if (draft is null)
        {
            return Task.FromResult<(ContentBundleLifecycleResponseDto, ValidationError?)>(
                (new ContentBundleLifecycleResponseDto(
                    false, draftId.ToString(), null, "draft", "Draft not found.", null, "DRAFT_NOT_FOUND"), null));
        }

        var stateName = draft.StateId is null ? null : "active";
        var refs = BuildRefContext(draft.HubId, draft.RelationIds, stateName);
        var validation = ContentBundleValidator.ValidateDraft(draft, refs);
        if (!validation.Valid)
        {
            var first = validation.Issues.First(i => i.IsBlocking);
            return Task.FromResult<(ContentBundleLifecycleResponseDto, ValidationError?)>(
                (new ContentBundleLifecycleResponseDto(
                    false, draftId.ToString(), null, draft.Status, first.Message, null, first.Code), null));
        }

        var entityId = Guid.NewGuid();
        string label = "Untitled";
        try
        {
            var json = JsonDocument.Parse(draft.EntityJsonb).RootElement;
            if (json.TryGetProperty("label", out var labelEl))
                label = labelEl.GetString() ?? label;
        }
        catch (JsonException) { /* handled by validation */ }

        _entities.Add((entityId, draft.HubId, label, stateName ?? "active", draft.RelationIds, draft.EntityJsonb));
        var idx = _drafts.FindIndex(d => d.DraftId == draftId);
        _drafts[idx] = draft with { Status = "promoted", PromotedEntityId = entityId, UpdatedAt = DateTimeOffset.UtcNow };

        var readback = LoadContentEntityAsync(entityId, ct).Result;
        return Task.FromResult<(ContentBundleLifecycleResponseDto, ValidationError?)>(
            (new ContentBundleLifecycleResponseDto(
                true, draftId.ToString(), entityId.ToString(), "promoted",
                "Draft promoted to active entity.", readback), null));
    }

    private static ContentBundleRefContext BuildRefContext(
        Guid hubId, IReadOnlyList<Guid> relationIds, string? stateName)
    {
        var hubExists = hubId == DemoHubId;
        var relationNames = new Dictionary<Guid, string>();
        foreach (var rid in relationIds)
        {
            if (rid == DemoRelationId) relationNames[rid] = "demo_relation";
        }

        var stateNames = new Dictionary<Guid, string> { [ActiveStateId] = "active" };
        return new ContentBundleRefContext(hubExists, "demo_relation", relationNames, stateNames);
    }
}
