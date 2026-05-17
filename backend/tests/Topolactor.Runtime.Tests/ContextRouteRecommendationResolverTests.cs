using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ContextRouteRecommendationResolverTests
{
    private static ContextRouteRecommendationResolver CreateResolver(
        ContextRouteRepository? repo = null,
        TopologyRepository? topologyRepo = null) =>
        new(
            NullLogger<ContextRouteRecommendationResolver>.Instance,
            repo ?? new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy"),
            new ContextVectorBuilder(),
            new ContextNeighborSearch(),
            topologyRepo ?? new StubValidPolicyTopologyRepository());

    [Fact]
    public void BuildEventVector_MapsTokenValues_AndOmitsMissingTokens()
    {
        var tokenA = Guid.NewGuid();
        var tokenB = Guid.NewGuid();
        var tokenMissing = Guid.NewGuid();
        var builder = new ContextVectorBuilder();

        var vector = builder.BuildEventVector(
            [tokenA, tokenB, tokenMissing],
            new Dictionary<Guid, float> { [tokenA] = 1.0f, [tokenB] = -0.5f });

        Assert.Equal(1.0f, vector[tokenA]);
        Assert.Equal(-0.5f, vector[tokenB]);
        Assert.False(vector.ContainsKey(tokenMissing));
    }

    [Fact]
    public void BuildPrefixVector_SumsEventVectors()
    {
        var token = Guid.NewGuid();
        var builder = new ContextVectorBuilder();

        var prefix = builder.BuildPrefixVector([
            new Dictionary<Guid, float> { [token] = 1.0f },
            new Dictionary<Guid, float> { [token] = 0.5f }
        ]);

        Assert.Equal(1.5f, prefix[token], precision: 5);
    }

    [Fact]
    public void ComputeCosineSimilarity_ZeroNorm_ReturnsZero()
    {
        var token = Guid.NewGuid();
        var search = new ContextNeighborSearch();

        var sim = search.ComputeCosineSimilarity(
            new Dictionary<Guid, float>(), 0f,
            new Dictionary<Guid, float> { [token] = 1.0f }, 1.0f);

        Assert.Equal(0f, sim);
    }

    [Fact]
    public void FindNearestPrefixes_FiltersAndAppliesTopK()
    {
        var token = Guid.NewGuid();
        var search = new ContextNeighborSearch();
        var current = new Dictionary<Guid, float> { [token] = 1.0f };
        var candidates = Enumerable.Range(0, 10)
            .Select(i => new ContextPrefixVectorRecord(
                SessionId: Guid.NewGuid(),
                PrefixIndex: i,
                LastEventId: Guid.NewGuid(),
                SparseVector: new Dictionary<Guid, float> { [token] = 1.0f },
                L2Norm: 1.0f,
                UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-i)))
            .ToList();

        var results = search.FindNearestPrefixes(current, 1.0f, candidates, minSimilarity: 0.05f, topK: 3);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task ResolveAsync_MissingPolicy_ReturnsExplicitError()
    {
        var resolver = CreateResolver(topologyRepo: new StubMissingPolicyTopologyRepository());
        var result = await resolver.ResolveAsync(MakeShape(sessionId: Guid.NewGuid().ToString()));

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("CONTEXT_ROUTE_POLICY_NOT_FOUND", result.StatusDetail);
    }

    [Fact]
    public async Task ResolveAsync_NoSessionId_WithResolvedPolicy_ReturnsInsufficientHistory()
    {
        var resolver = CreateResolver();
        var result = await resolver.ResolveAsync(MakeShape(sessionId: null));

        Assert.Equal(RecommendationStatus.InsufficientHistory, result.Status);
        Assert.Equal("NO_SESSION_ID", result.StatusDetail);
    }

    [Fact]
    public async Task ResolveAsync_NoPrefixHistory_WithResolvedPolicy_ReturnsInsufficientHistory()
    {
        var resolver = CreateResolver();
        var result = await resolver.ResolveAsync(MakeShape(sessionId: Guid.NewGuid().ToString()));

        Assert.Equal(RecommendationStatus.InsufficientHistory, result.Status);
        Assert.Equal("NO_CONTEXT_HISTORY", result.StatusDetail);
    }

    [Fact]
    public async Task ResolveAsync_WithPrefixHistory_ReturnsNextOperationCandidate()
    {
        var token = Guid.NewGuid();
        var resolver = CreateResolver(repo: new StubPrefixRepository(token));
        var result = await resolver.ResolveAsync(MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            contextTokenIds: token.ToString()));

        Assert.Equal(RecommendationStatus.Ok, result.Status);
        Assert.Contains(result.NextOperations, c => c.Value == "action_next");
    }

    [Fact]
    public void ResolveNextOperations_MergesNeighborAndTransitionStats()
    {
        var resolver = CreateResolver();
        var policy = ContextRoutePolicyTestFixtures.ValidPolicy();
        var neighbors = new List<ContextNeighborResult>
        {
            new(Guid.NewGuid(), 0, 0.5f, "action_a", null)
        };
        var stats = new List<ContextTransitionStat>
        {
            new("prev", "action_b", 100, 90f, 0.9f)
        };

        var result = resolver.ResolveNextOperations(neighbors, stats, policy);

        Assert.Contains(result, r => r.Value == "action_a");
        Assert.Contains(result, r => r.Value == "action_b");
    }

    [Fact]
    public void ResolveNextTokens_VotesFromNextTokenIdsHint()
    {
        var resolver = CreateResolver();
        var policy = ContextRoutePolicyTestFixtures.ValidPolicy();
        var token = Guid.NewGuid();
        var neighbors = new List<ContextNeighborResult>
        {
            new(Guid.NewGuid(), 0, 0.9f, null, [token]),
            new(Guid.NewGuid(), 0, 0.7f, null, [token])
        };

        var result = resolver.ResolveNextTokens(neighbors, policy);

        Assert.Single(result);
        Assert.Equal(token.ToString(), result[0].Value);
    }

    private static RuntimeWorkingShape MakeShape(string? sessionId, string? contextTokenIds = null)
    {
        var vector = new OperationVector(
            Target: "default",
            Layer: "entity",
            Action: "search",
            AttractorKey: "default:entity:search",
            UserRole: null,
            Payload: null,
            RequestedProjection: null,
            ContextSessionId: sessionId,
            ContextUserId: null,
            ContextTokenIds: contextTokenIds,
            ContextRecordId: null);

        return new RuntimeWorkingShape(
            Vector: vector,
            StructureMapId: "test-map",
            PackageId: Guid.NewGuid(),
            SchemaId: Guid.NewGuid(),
            ComponentIds: [],
            PackageDef: null,
            SchemaDef: null,
            ResolvedData: null,
            Errors: []);
    }

    private sealed class StubPrefixRepository(Guid tokenId) : ContextRouteRepository(
        NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        public override Task<IReadOnlyList<ContextTokenRecord>> LoadActiveTokensAsync(
            IEnumerable<Guid> tokenIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ContextTokenRecord>>([
                new ContextTokenRecord(tokenId, "stub_token", null, 1.0f, "active")
            ]);

        public override Task<IReadOnlyList<ContextPrefixVectorRecord>> LoadRecentPrefixVectorsAsync(
            string? tableName,
            string? role,
            int maxDays,
            CancellationToken ct = default)
        {
            var vector = new Dictionary<Guid, float> { [tokenId] = 1.0f };
            IReadOnlyList<ContextPrefixVectorRecord> result = Enumerable.Range(0, 15)
                .Select(i => new ContextPrefixVectorRecord(
                    SessionId: Guid.NewGuid(),
                    PrefixIndex: i,
                    LastEventId: Guid.NewGuid(),
                    SparseVector: vector,
                    L2Norm: 1.0f,
                    UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-i),
                    NextOperation: "action_next",
                    NextTokenIdsHint: null))
                .ToList();
            return Task.FromResult(result);
        }
    }
}