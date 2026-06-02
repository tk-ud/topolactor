using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

internal sealed class CanonicalSqlAttentionStubRepository : SqlAttentionLogsRepository
{
    public IReadOnlyList<WatchChangeCandidate> WatchCandidates { get; set; } = [];
    public IReadOnlyList<Guid> ManifestIds { get; set; } = [];
    public IReadOnlyList<HubRelationExplorationCandidate> Relations { get; set; } = [];
    public int ResolverCalls { get; private set; }
    public int RelationLoadCalls { get; private set; }
    public int LegacyCacheCalls { get; private set; }
    public List<AttentionGenerationAppendRequest> Generations { get; } = [];
    public Dictionary<Guid, AttentionLifecycleSource> LifecycleSources { get; } = [];
    public List<AttentionLifecycleAppendRequest> LifecycleAppends { get; } = [];

    public CanonicalSqlAttentionStubRepository() : base(NullLogger<SqlAttentionLogsRepository>.Instance, "dummy") { }

    public override Task<IReadOnlyList<WatchChangeCandidate>> LoadWatchCandidatesAsync(string sourceSetId, string basisWindow, CancellationToken ct = default) => Task.FromResult(WatchCandidates);
    public override Task<RelatedTopologyManifestResolution> ResolveRelatedTopologyManifestsAsync(WatchChangeCandidate candidate, CancellationToken ct = default)
    {
        ResolverCalls++;
        return Task.FromResult(new RelatedTopologyManifestResolution(candidate.CurrentId, ManifestIds, "[]"));
    }
    public override Task<IReadOnlyList<HubRelationExplorationCandidate>> LoadHubRelationExplorationCandidatesAsync(IReadOnlyList<Guid> topologyManifestIds, CancellationToken ct = default)
    {
        RelationLoadCalls++;
        return Task.FromResult(Relations);
    }
    public override Task<IReadOnlyList<HubCurrentCandidate>> LoadLegacyHubCurrentSupportCacheCandidatesAsync(string sourceSetId, string basisWindow, CancellationToken ct = default)
    {
        LegacyCacheCalls++;
        throw new InvalidOperationException("Canonical route must never load logs.hub_current support cache.");
    }
    public override Task<AttentionGenerationAppendResult> AppendAttentionGenerationAsync(AttentionGenerationAppendRequest request, CancellationToken ct = default)
    {
        Generations.Add(request);
        return Task.FromResult(new AttentionGenerationAppendResult(request.GenerationLineId, Guid.NewGuid(), Guid.NewGuid()));
    }
    public override Task<AttentionLifecycleSource?> LoadAttentionLifecycleSourceAsync(Guid attentionId, CancellationToken ct = default) => Task.FromResult(LifecycleSources.GetValueOrDefault(attentionId));
    public override Task<Guid> AppendAttentionLifecycleEvidenceAsync(AttentionLifecycleAppendRequest request, CancellationToken ct = default)
    {
        LifecycleAppends.Add(request);
        return Task.FromResult(Guid.NewGuid());
    }
}

public class SqlAttentionPhaseGenerationLineTests
{
    private static WatchChangeCandidate Changed(Guid? id = null) => ExplorationTestFactory.ChangeCandidate(currentId: id, l2Norm: 12.0, normLevel: "high");
    private static HubAttractorExplorationRuntime Runtime(CanonicalSqlAttentionStubRepository repo) => new(
        NullLogger<HubAttractorExplorationRuntime>.Instance,
        new StubExplorationPolicyTopologyRepository(ExplorationTestFactory.ValidPolicyJson(highPhaseLimit: 2)),
        repo);

    [Fact]
    public async Task ExploreAsync_ChangedCandidate_ResolvesManifestAndExploresHubRelationsWithoutCacheFallback()
    {
        var manifestId = Guid.NewGuid();
        var relationId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var repo = new CanonicalSqlAttentionStubRepository
        {
            ManifestIds = [manifestId],
            Relations = [new HubRelationExplorationCandidate(relationId, manifestId, hubId, Guid.NewGuid(), 1, 0.97, "{\"sql_attention_score\":0.97}")]
        };

        var result = await Runtime(repo).ExploreAsync([Changed()], "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        var hit = Assert.Single(result.Result!.Hits);
        Assert.Equal(relationId, hit.HubRelationId);
        Assert.Equal(manifestId, hit.TopologyManifestId);
        Assert.Equal(hubId, hit.HubId);
        Assert.Equal(1, repo.ResolverCalls);
        Assert.Equal(1, repo.RelationLoadCalls);
        Assert.Equal(0, repo.LegacyCacheCalls);
        using var phase = JsonDocument.Parse(hit.PhaseVectorJson);
        Assert.Equal("phaseAT", phase.RootElement.GetProperty("q_kind").GetString());
        Assert.False(phase.RootElement.GetProperty("q_is_draft").GetBoolean());
        Assert.Equal(relationId, phase.RootElement.GetProperty("x_hit_hub_relation_id").GetGuid());
        Assert.Equal(manifestId, phase.RootElement.GetProperty("y_topology_manifest_id").GetGuid());
        Assert.Equal(hubId, phase.RootElement.GetProperty("z_hub_id").GetGuid());
    }

    [Fact]
    public async Task ExploreAsync_NoChange_DoesNotInvokeResolverOrRelationExploration()
    {
        var repo = new CanonicalSqlAttentionStubRepository();
        var result = await Runtime(repo).ExploreAsync([ExplorationTestFactory.NoChangeCandidate()], "src", "7d");
        Assert.Equal(HubAttractorExplorationStatus.NoChange, result.Status);
        Assert.Equal(0, repo.ResolverCalls);
        Assert.Equal(0, repo.RelationLoadCalls);
        Assert.Equal(0, repo.LegacyCacheCalls);
    }

    [Fact]
    public async Task ExploreAsync_NoManifestBinding_ReturnsExplicitNoHitWithoutCacheFallback()
    {
        var repo = new CanonicalSqlAttentionStubRepository();
        var result = await Runtime(repo).ExploreAsync([Changed()], "src", "7d");
        Assert.Equal(HubAttractorExplorationStatus.NoRelatedTopologyManifest, result.Status);
        Assert.Equal(0, repo.RelationLoadCalls);
        Assert.Equal(0, repo.LegacyCacheCalls);
    }

    [Fact]
    public async Task Scheduler_CanonicalHit_AppendsSqlAtAndPhaseAtGenerationRequest()
    {
        var manifestId = Guid.NewGuid();
        var repo = new CanonicalSqlAttentionStubRepository
        {
            WatchCandidates = [Changed()],
            ManifestIds = [manifestId],
            Relations = [new HubRelationExplorationCandidate(Guid.NewGuid(), manifestId, Guid.NewGuid(), Guid.NewGuid(), 1, 0.96, "{\"sql_attention_score\":0.96}")]
        };
        var scheduler = new SqlAttentionScheduler(NullLogger<SqlAttentionScheduler>.Instance, repo, Runtime(repo));
        Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", "src");
        Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", "7d");
        try { await scheduler.RunOnceAsync(CancellationToken.None); }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
        var request = Assert.Single(repo.Generations);
        Assert.NotEqual(Guid.Empty, request.GenerationLineId);
        Assert.Equal(manifestId, request.TopologyManifestId);
        Assert.Equal(0, repo.LegacyCacheCalls);
    }

    [Fact]
    public async Task PromotionRuntime_RequiresExplicitDraftBeforeAdoption_AndAppendsRejectedEvidence()
    {
        var phaseId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var source = new AttentionLifecycleSource(phaseId, Guid.NewGuid(), "src", lineId, "phaseAT", "{\"q_is_draft\":false}", [], [], [], [], []);
        var repo = new CanonicalSqlAttentionStubRepository();
        repo.LifecycleSources[phaseId] = source;
        var runtime = new SqlAttentionEvidencePromotionRuntime(repo);

        var invalidAdopt = await runtime.ExecuteAsync(new AttentionLifecycleCommand(phaseId, AttentionLifecycleOperation.AdoptDraft, "user:test", "cmd-1"));
        Assert.False(invalidAdopt.Succeeded);
        Assert.Equal("INVALID_EVIDENCE_TRANSITION", invalidAdopt.Status);

        var draft = await runtime.ExecuteAsync(new AttentionLifecycleCommand(phaseId, AttentionLifecycleOperation.CreateDraft, "user:test", "cmd-2"));
        Assert.True(draft.Succeeded);
        var draftAppend = Assert.Single(repo.LifecycleAppends);
        Assert.Equal("draft_projection", draftAppend.EvidenceKind);
        Assert.Equal("draft", draftAppend.PromotionStatus);

        var draftId = draft.AttentionId!.Value;
        repo.LifecycleSources[draftId] = source with { AttentionId = draftId, EvidenceKind = "draft_projection" };
        var adopted = await runtime.ExecuteAsync(new AttentionLifecycleCommand(draftId, AttentionLifecycleOperation.AdoptDraft, "user:test", "cmd-3"));
        Assert.True(adopted.Succeeded);
        Assert.Equal("adopted", adopted.Status);

        var rejected = await runtime.ExecuteAsync(new AttentionLifecycleCommand(phaseId, AttentionLifecycleOperation.Reject, "user:test", "cmd-4"));
        Assert.True(rejected.Succeeded);
        Assert.Equal("rejected", rejected.Status);
        Assert.Contains(repo.LifecycleAppends, x => x.EvidenceKind == "rejection_result");
        Assert.All(repo.LifecycleAppends, x => Assert.Contains("no_automatic_topology_manifest_hub_relation_mutation", x.EvidenceJson));
    }
}
