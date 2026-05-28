using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for SqlAttentionTopologyProjectionRuntime (SQLA-6 child projection boundary).
///
/// Coverage:
///   - Policy missing → MissingPolicy explicit failure (no silent fallback).
///   - Policy malformed → MalformedPolicy explicit failure (no silent fallback).
///   - DB unavailable → DbUnavailable explicit failure (no silent fallback).
///   - No evidence → NoEvidence (not an error; expected cold start).
///   - Evidence layer separation preserved (statistics/attention/phase_attention carried through).
///   - Fixed route / registry / topology not mutated (write boundary is read-only).
///   - Candidates are projection output only; no back-propagation to parent evidence layer.
/// </summary>
public class SqlAttentionTopologyProjectionRuntimeTests
{
    // ---------------------------------------------------------------------------
    // Stub helpers
    // ---------------------------------------------------------------------------

    private static SqlAttentionTopologyProjectionRuntime CreateRuntime(
        SqlAttentionLogsRepository? logsRepo = null,
        TopologyRepository? topologyRepo = null)
    {
        logsRepo ??= new SqlAttentionLogsRepository(
            NullLogger<SqlAttentionLogsRepository>.Instance, "dummy");
        topologyRepo ??= new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "dummy");
        return new SqlAttentionTopologyProjectionRuntime(
            NullLogger<SqlAttentionTopologyProjectionRuntime>.Instance,
            logsRepo,
            topologyRepo);
    }

    private static AttentionEvidenceRecord MakeEvidence(
        double neighborScore = 0.95,
        string scoreBand = "strong",
        string statisticsJson = """{"count":10}""",
        string vectorJson = """{"hub_a":0.8}""",
        string phaseVectorJson = """{"phase":0.3}""",
        string evidenceJson = """{"cosine_score":0.95}""")
    {
        return new AttentionEvidenceRecord(
            AttentionId:        Guid.NewGuid(),
            CurrentId:          Guid.NewGuid(),
            SourceSetId:        "test_source",
            HubId:              Guid.NewGuid(),
            AttractorKey:       "test_attractor",
            HubRelationId:      null,
            RelationRegistryId: Guid.NewGuid(),
            NeighborScore:      neighborScore,
            HitRank:            1,
            ScoreBand:          scoreBand,
            PermutationKey:     "perm_0",
            L2Norm:             2.5,
            VectorJson:         vectorJson,
            PhaseVectorJson:    phaseVectorJson,
            StatisticsJson:     statisticsJson,
            EmaScore:           null,
            EvidenceJson:       evidenceJson,
            CreatedAt:          DateTimeOffset.UtcNow
        );
    }

    // ---------------------------------------------------------------------------
    // Explicit failure tests (no silent fallback)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MissingPolicy_ReturnsMissingPolicyStatus()
    {
        // function_parameters row absent → MissingPolicy, no silent fallback.
        var runtime = CreateRuntime();

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.MissingPolicy, result.Status);
        Assert.NotNull(result.StatusDetail);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task MalformedPolicy_MissingKey_ReturnsMalformedPolicyStatus()
    {
        // Policy JSON present but missing required keys → MalformedPolicy.
        var topologyRepo = new StubTopologyRepositoryWithPolicy("""{"top_k": 10}""");
        var runtime = CreateRuntime(topologyRepo: topologyRepo);

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.MalformedPolicy, result.Status);
        Assert.NotNull(result.StatusDetail);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task MalformedPolicy_InvalidJson_ReturnsMalformedPolicyStatus()
    {
        // Policy JSON is not valid JSON → MalformedPolicy.
        var topologyRepo = new StubTopologyRepositoryWithPolicy("not-json{{{");
        var runtime = CreateRuntime(topologyRepo: topologyRepo);

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.MalformedPolicy, result.Status);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task MalformedPolicy_NonPositiveValue_ReturnsMalformedPolicyStatus()
    {
        // top_k = 0 is non-positive → MalformedPolicy.
        var topologyRepo = new StubTopologyRepositoryWithPolicy(
            """{"top_k": 0, "min_neighbor_score": 0.85, "recent_window_days": 30}""");
        var runtime = CreateRuntime(topologyRepo: topologyRepo);

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.MalformedPolicy, result.Status);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task DbUnavailable_ReturnsDbUnavailableStatus()
    {
        // logs.attention query throws → DbUnavailable explicit failure.
        var topologyRepo = new StubTopologyRepositoryWithPolicy(ValidPolicyJson);
        var logsRepo = new ThrowingAttentionLogsRepository();
        var runtime = CreateRuntime(logsRepo: logsRepo, topologyRepo: topologyRepo);

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.DbUnavailable, result.Status);
        Assert.NotNull(result.StatusDetail);
        Assert.Empty(result.Candidates);
    }

    // ---------------------------------------------------------------------------
    // NoEvidence (expected cold start)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task NoEvidence_ReturnsNoEvidenceStatus()
    {
        // Empty evidence list → NoEvidence (not an error).
        var topologyRepo = new StubTopologyRepositoryWithPolicy(ValidPolicyJson);
        var runtime = CreateRuntime(topologyRepo: topologyRepo);

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.NoEvidence, result.Status);
        Assert.Empty(result.Candidates);
    }

    // ---------------------------------------------------------------------------
    // Evidence → candidate projection
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ValidEvidence_ReturnsOkWithCandidates()
    {
        // Evidence rows present → Ok with non-empty candidates.
        var topologyRepo = new StubTopologyRepositoryWithPolicy(ValidPolicyJson);
        var logsRepo = new StubAttentionLogsRepository([MakeEvidence()]);
        var runtime = CreateRuntime(logsRepo: logsRepo, topologyRepo: topologyRepo);

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.Ok, result.Status);
        Assert.NotEmpty(result.Candidates);
    }

    [Fact]
    public async Task EvidenceLayerSeparation_AllThreeLayersPreservedOnCandidate()
    {
        // statistics / attention / phase_attention layers are carried through separately per SSOT.
        var stats   = """{"count": 42}""";
        var vec     = """{"hub_x": 0.9}""";
        var phase   = """{"w": 1.2}""";
        var evJson  = """{"cosine_score": 0.91}""";

        var topologyRepo = new StubTopologyRepositoryWithPolicy(ValidPolicyJson);
        var logsRepo = new StubAttentionLogsRepository([
            MakeEvidence(statisticsJson: stats, vectorJson: vec, phaseVectorJson: phase, evidenceJson: evJson)
        ]);
        var runtime = CreateRuntime(logsRepo: logsRepo, topologyRepo: topologyRepo);

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.Ok, result.Status);
        var candidate = Assert.Single(result.Candidates);

        Assert.Equal(stats,  candidate.StatisticsJson);
        Assert.Equal(vec,    candidate.VectorJson);
        Assert.Equal(phase,  candidate.PhaseVectorJson);
        Assert.Equal(evJson, candidate.EvidenceJson);
    }

    [Fact]
    public async Task AttentionScore_IsDisplayOnlyAndDoesNotCollapseEvidenceLayers()
    {
        // AttentionScore is a display/ranking helper; all three evidence layers must still be present separately.
        var topologyRepo = new StubTopologyRepositoryWithPolicy(ValidPolicyJson);
        var logsRepo = new StubAttentionLogsRepository([MakeEvidence(neighborScore: 0.95)]);
        var runtime = CreateRuntime(logsRepo: logsRepo, topologyRepo: topologyRepo);

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.Ok, result.Status);
        var candidate = Assert.Single(result.Candidates);

        // AttentionScore is present but evidence layers are not collapsed.
        Assert.True(candidate.AttentionScore > 0, "AttentionScore must be positive for a high-scoring hit.");
        Assert.NotNull(candidate.StatisticsJson);
        Assert.NotNull(candidate.VectorJson);
        Assert.NotNull(candidate.PhaseVectorJson);
        Assert.NotNull(candidate.EvidenceJson);
    }

    // ---------------------------------------------------------------------------
    // No mutation boundary (SSOT invariant)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ProjectAsync_DoesNotWriteToLogsAttention()
    {
        // ProjectAsync must not call WriteLogsAttentionAsync — it is read-only relative to evidence.
        var topologyRepo = new StubTopologyRepositoryWithPolicy(ValidPolicyJson);
        var trackingLogsRepo = new TrackingWriteAttentionLogsRepository([MakeEvidence()]);
        var runtime = CreateRuntime(logsRepo: trackingLogsRepo, topologyRepo: topologyRepo);

        var result = await runtime.ProjectAsync("source_set_1");

        Assert.Equal(TopologyProjectionStatus.Ok, result.Status);
        Assert.Equal(0, trackingLogsRepo.WriteLogsAttentionCallCount);
    }

    [Fact]
    public void ProjectCandidates_Static_DoesNotMutateInput()
    {
        // Static projection helper must not modify the input evidence list.
        var evidence = new List<AttentionEvidenceRecord> { MakeEvidence() };
        var originalCount = evidence.Count;
        var originalAttractorKey = evidence[0].AttractorKey;

        var candidates = SqlAttentionTopologyProjectionRuntime.ProjectCandidates(evidence);

        Assert.Equal(originalCount, evidence.Count);
        Assert.Equal(originalAttractorKey, evidence[0].AttractorKey);
        Assert.NotEmpty(candidates);
    }

    // ---------------------------------------------------------------------------
    // Test policy JSON
    // ---------------------------------------------------------------------------

    private const string ValidPolicyJson =
        """{"top_k": 10, "min_neighbor_score": 0.85, "recent_window_days": 30}""";

    // ---------------------------------------------------------------------------
    // Stub implementations
    // ---------------------------------------------------------------------------

    private sealed class StubTopologyRepositoryWithPolicy(string policyJson)
        : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
    {
        public override Task<string?> LoadFunctionParameterAsync(
            string functionName, string parameterKey, CancellationToken ct = default)
        {
            if (functionName == SqlAttentionTopologyProjectionRuntime.ProjectionFunctionName &&
                parameterKey == SqlAttentionTopologyProjectionRuntime.ProjectionPolicyKey)
                return Task.FromResult<string?>(policyJson);
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class StubAttentionLogsRepository(IReadOnlyList<AttentionEvidenceRecord> evidence)
        : SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "dummy")
    {
        public override Task<IReadOnlyList<AttentionEvidenceRecord>> LoadAttentionEvidenceForProjectionAsync(
            string sourceSetId, int topK, double minNeighborScore, int recentWindowDays, CancellationToken ct = default)
            => Task.FromResult(evidence);
    }

    private sealed class ThrowingAttentionLogsRepository()
        : SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "dummy")
    {
        public override Task<IReadOnlyList<AttentionEvidenceRecord>> LoadAttentionEvidenceForProjectionAsync(
            string sourceSetId, int topK, double minNeighborScore, int recentWindowDays, CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated DB unavailability.");
    }

    private sealed class TrackingWriteAttentionLogsRepository(IReadOnlyList<AttentionEvidenceRecord> evidence)
        : SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "dummy")
    {
        public int WriteLogsAttentionCallCount { get; private set; }

        public override Task<IReadOnlyList<AttentionEvidenceRecord>> LoadAttentionEvidenceForProjectionAsync(
            string sourceSetId, int topK, double minNeighborScore, int recentWindowDays, CancellationToken ct = default)
            => Task.FromResult(evidence);

        public override Task<int> WriteLogsAttentionAsync(
            IReadOnlyList<LogsAttentionWriteRequest> requests, CancellationToken ct = default)
        {
            WriteLogsAttentionCallCount++;
            return Task.FromResult(requests.Count);
        }
    }
}
