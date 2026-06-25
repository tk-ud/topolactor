using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// manifest_topology_key_expansion_draft_lane bridge boundary tests.
///
/// Verifies the C# side stays orchestration-only:
///   - the repository bridge runs the SQL-only lane and carries no candidate inference;
///   - the in-memory double validates inputs and returns an explicit empty list (no silent fallback);
///   - the scheduler invokes the draft lane only as a post-stage after SQLAT evidence is appended.
/// </summary>
[Collection("SqlAttentionSchedulerEnvVarTests")]
public class SqlAttentionDraftLaneTests
{
    /// <summary>Captures the draft-lane bridge call and returns a canned candidate set.</summary>
    private sealed class DraftLaneStubRepository : SqlAttentionLogsRepository
    {
        public IReadOnlyList<WatchChangeCandidate> WatchCandidates { get; set; } = [];
        public IReadOnlyList<Guid> ManifestIds { get; set; } = [];
        public IReadOnlyList<HubRelationExplorationCandidate> Relations { get; set; } = [];
        public List<AttentionGenerationAppendRequest> Generations { get; } = [];
        public int CompileCalls { get; private set; }
        public string? LastSourceSetId { get; private set; }
        public string? LastBasisWindow { get; private set; }
        public IReadOnlyList<SqlAttentionDraftCandidateRef> Drafts { get; set; } = [];

        public DraftLaneStubRepository() : base(NullLogger<SqlAttentionLogsRepository>.Instance, "dummy") { }

        public override Task<IReadOnlyList<WatchChangeCandidate>> LoadWatchCandidatesAsync(string sourceSetId, string basisWindow, CancellationToken ct = default) =>
            Task.FromResult(WatchCandidates);

        public override Task<RelatedTopologyManifestResolution> ResolveRelatedTopologyManifestsAsync(WatchChangeCandidate candidate, CancellationToken ct = default) =>
            Task.FromResult(new RelatedTopologyManifestResolution(candidate.CurrentId, ManifestIds, "[]"));

        public override Task<IReadOnlyList<HubRelationExplorationCandidate>> LoadHubRelationExplorationCandidatesAsync(IReadOnlyList<Guid> topologyManifestIds, CancellationToken ct = default) =>
            Task.FromResult(Relations);

        public override Task<AttentionGenerationAppendResult> AppendAttentionGenerationAsync(AttentionGenerationAppendRequest request, CancellationToken ct = default)
        {
            Generations.Add(request);
            return Task.FromResult(new AttentionGenerationAppendResult(request.GenerationLineId, Guid.NewGuid(), Guid.NewGuid()));
        }

        public override Task<IReadOnlyList<SqlAttentionDraftCandidateRef>> CompileManifestTopologyDraftCandidatesAsync(
            string sourceSetId, string basisWindow, CancellationToken ct = default)
        {
            CompileCalls++;
            LastSourceSetId = sourceSetId;
            LastBasisWindow = basisWindow;
            return Task.FromResult(Drafts);
        }
    }

    private static WatchChangeCandidate Changed() =>
        ExplorationTestFactory.ChangeCandidate(l2Norm: 12.0, normLevel: "high");

    private static HubAttractorExplorationRuntime Runtime(SqlAttentionLogsRepository repo) => new(
        NullLogger<HubAttractorExplorationRuntime>.Instance,
        new StubExplorationPolicyTopologyRepository(ExplorationTestFactory.ValidPolicyJson(highPhaseLimit: 2)),
        repo);

    [Fact]
    public async Task InMemoryRepository_CompileDraftCandidates_ValidatesInputs()
    {
        var repo = new SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "dummy");
        await Assert.ThrowsAsync<ArgumentException>(() => repo.CompileManifestTopologyDraftCandidatesAsync("", "w1"));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.CompileManifestTopologyDraftCandidatesAsync("ss1", " "));
    }

    [Fact]
    public async Task InMemoryRepository_CompileDraftCandidates_ReturnsExplicitEmptyList()
    {
        var repo = new SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "dummy");
        var result = await repo.CompileManifestTopologyDraftCandidatesAsync("ss1", "w1");
        Assert.Empty(result);
    }

    [Fact]
    public async Task Scheduler_AfterSqlAtEvidence_InvokesDraftLanePostStage()
    {
        var manifestId = Guid.NewGuid();
        var repo = new DraftLaneStubRepository
        {
            WatchCandidates = [Changed()],
            ManifestIds = [manifestId],
            Relations = [new HubRelationExplorationCandidate(Guid.NewGuid(), manifestId, Guid.NewGuid(), Guid.NewGuid(), 1, 0.96, "{\"sql_attention_score\":0.96}")],
            Drafts =
            [
                new SqlAttentionDraftCandidateRef(
                    Guid.NewGuid(), Guid.NewGuid(), "meaning_projection_candidate",
                    "manifest_topology_key_expansion_draft_lane", 1.5)
            ]
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

        // Evidence was appended, then the draft lane ran as the post-stage with the same identity.
        Assert.Single(repo.Generations);
        Assert.Equal(1, repo.CompileCalls);
        Assert.Equal("src", repo.LastSourceSetId);
        Assert.Equal("7d", repo.LastBasisWindow);
    }

    [Fact]
    public async Task Scheduler_NoChangeCandidates_DoesNotInvokeDraftLane()
    {
        var repo = new DraftLaneStubRepository
        {
            WatchCandidates = [ExplorationTestFactory.NoChangeCandidate()]
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

        // No SQLAT evidence appended this run => the draft lane is not invoked.
        Assert.Empty(repo.Generations);
        Assert.Equal(0, repo.CompileCalls);
    }
}
