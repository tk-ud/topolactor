using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public partial class ContextRouteRecommendationResolverTests
{
    private static ContextVectorBuilder VectorBuilder() => new();
    private static ContextNeighborSearch NeighborSearch() => new();

    private static ContextRouteRecommendationResolver CreateResolver(
        ContextRouteRepository? repo = null,
        TopologyRepository? topologyRepo = null,
        SystemOperationCiRuntime? ciRuntime = null)
    {
        repo ??= new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy");
        // Default: use an explicit test fixture policy, not production repository seed values.
        topologyRepo ??= new StubValidPolicyTopologyRepository();
        ciRuntime ??= new SystemOperationCiRuntime(
            NullLogger<SystemOperationCiRuntime>.Instance, repo);
        return new ContextRouteRecommendationResolver(
            NullLogger<ContextRouteRecommendationResolver>.Instance,
            repo,
            VectorBuilder(),
            NeighborSearch(),
            topologyRepo,
            ciRuntime);
    }

    // --- ContextVectorBuilder tests ---

    [Fact]
    public void BuildMultiHotVector_MapsTokenIdsToOnePointZero()
    {
        var builder = VectorBuilder();
        var tokenA = Guid.NewGuid();
        var tokenB = Guid.NewGuid();

        var vector = builder.BuildMultiHotVector([tokenA, tokenB]);

        Assert.Equal(1.0f, vector[tokenA]);
        Assert.Equal(1.0f, vector[tokenB]);
        Assert.Equal(2, vector.Count);
    }

    [Fact]
    public void BuildMultiHotVector_EmptyTokenIds_ReturnsEmptyVector()
    {
        var builder = VectorBuilder();
        var vector = builder.BuildMultiHotVector([]);
        Assert.Empty(vector);
    }

    [Fact]
    public void BuildPrefixVector_SumsMultiHotEventVectors()
    {
        var builder = VectorBuilder();
        var tokenA = Guid.NewGuid();
        var tokenB = Guid.NewGuid();

        var ev1 = new Dictionary<Guid, float> { [tokenA] = 1.0f };
        var ev2 = new Dictionary<Guid, float> { [tokenA] = 1.0f, [tokenB] = 1.0f };

        var prefix = builder.BuildPrefixVector([ev1, ev2]);

        Assert.Equal(2.0f, prefix[tokenA], precision: 5); // token A appears in both events
        Assert.Equal(1.0f, prefix[tokenB], precision: 5); // token B appears in ev2 only
    }

    [Fact]
    public void BuildPrefixVector_EmptyInputs_ReturnsEmptyVector()
    {
        var builder = VectorBuilder();
        var prefix = builder.BuildPrefixVector([]);
        Assert.Empty(prefix);
    }

    [Fact]
    public void ComputeL2Norm_MultiHotThreeTokens_ReturnsSqrt3()
    {
        var builder = VectorBuilder();
        var tokenA = Guid.NewGuid();
        var tokenB = Guid.NewGuid();
        var tokenC = Guid.NewGuid();
        var vector = new Dictionary<Guid, float> { [tokenA] = 1.0f, [tokenB] = 1.0f, [tokenC] = 1.0f };

        var norm = builder.ComputeL2Norm(vector);

        Assert.Equal((float)Math.Sqrt(3), norm, precision: 5); // sqrt(1+1+1) = sqrt(3)
    }

    [Fact]
    public void ComputeL2Norm_EmptyVector_ReturnsZero()
    {
        var builder = VectorBuilder();
        var norm = builder.ComputeL2Norm(new Dictionary<Guid, float>());
        Assert.Equal(0f, norm);
    }

    // --- ContextNeighborSearch tests ---

    [Fact]
    public void ComputeCosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var search = NeighborSearch();
        var tokenA = Guid.NewGuid();
        var tokenB = Guid.NewGuid();

        var a = new Dictionary<Guid, float> { [tokenA] = 1.0f };
        var b = new Dictionary<Guid, float> { [tokenB] = 1.0f };

        var sim = search.ComputeCosineSimilarity(a, 1.0f, b, 1.0f);

        Assert.Equal(0f, sim);
    }

    [Fact]
    public void ComputeCosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var search = NeighborSearch();
        var tokenA = Guid.NewGuid();

        var a = new Dictionary<Guid, float> { [tokenA] = 1.0f };
        var norm = 1.0f;

        var sim = search.ComputeCosineSimilarity(a, norm, a, norm);

        Assert.Equal(1.0f, sim, precision: 5);
    }

    [Fact]
    public void ComputeCosineSimilarity_ZeroNormA_ReturnsZero()
    {
        var search = NeighborSearch();
        var tokenA = Guid.NewGuid();

        var a = new Dictionary<Guid, float>();
        var b = new Dictionary<Guid, float> { [tokenA] = 1.0f };

        var sim = search.ComputeCosineSimilarity(a, 0f, b, 1.0f);

        Assert.Equal(0f, sim);
    }

    [Fact]
    public void ComputeCosineSimilarity_ZeroNormB_ReturnsZero()
    {
        var search = NeighborSearch();
        var tokenA = Guid.NewGuid();

        var a = new Dictionary<Guid, float> { [tokenA] = 1.0f };
        var b = new Dictionary<Guid, float>();

        var sim = search.ComputeCosineSimilarity(a, 1.0f, b, 0f);

        Assert.Equal(0f, sim);
    }

    [Fact]
    public void FindNearestPrefixes_BelowMinSimilarity_FilteredOut()
    {
        var search = NeighborSearch();
        var tokenA = Guid.NewGuid();
        var tokenB = Guid.NewGuid();

        var current = new Dictionary<Guid, float> { [tokenA] = 1.0f };
        var currentNorm = 1.0f;

        var candidate = new ContextPrefixVectorRecord(
            SessionId: Guid.NewGuid(),
            PrefixIndex: 0,
            LastEventId: Guid.NewGuid(),
            SparseVector: new Dictionary<Guid, float> { [tokenB] = 1.0f },
            L2Norm: 1.0f,
            UpdatedAt: DateTimeOffset.UtcNow
        );

        var results = search.FindNearestPrefixes(current, currentNorm, [candidate], minSimilarity: 0.05f, topK: 10);

        Assert.Empty(results); // orthogonal → similarity=0 < 0.05
    }

    [Fact]
    public void FindNearestPrefixes_ZeroNormCurrentVector_ReturnsEmpty()
    {
        var search = NeighborSearch();
        var tokenA = Guid.NewGuid();

        var candidate = new ContextPrefixVectorRecord(
            SessionId: Guid.NewGuid(),
            PrefixIndex: 0,
            LastEventId: Guid.NewGuid(),
            SparseVector: new Dictionary<Guid, float> { [tokenA] = 1.0f },
            L2Norm: 1.0f,
            UpdatedAt: DateTimeOffset.UtcNow
        );

        var results = search.FindNearestPrefixes(
            new Dictionary<Guid, float>(), 0f, [candidate], 0.05f, 10);

        Assert.Empty(results);
    }

    [Fact]
    public void FindNearestPrefixes_TopKEnforced()
    {
        var search = NeighborSearch();
        var tokenA = Guid.NewGuid();

        var current = new Dictionary<Guid, float> { [tokenA] = 1.0f };

        var candidates = Enumerable.Range(0, 10)
            .Select(i => new ContextPrefixVectorRecord(
                SessionId: Guid.NewGuid(),
                PrefixIndex: i,
                LastEventId: Guid.NewGuid(),
                SparseVector: new Dictionary<Guid, float> { [tokenA] = 1.0f },
                L2Norm: 1.0f,
                UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-i)
            ))
            .ToList();

        var results = search.FindNearestPrefixes(current, 1.0f, candidates, 0.05f, topK: 3);

        Assert.Equal(3, results.Count);
    }

    // --- ContextRouteRecommendationResolver tests ---

    [Fact]
    public async Task ResolveAsync_NoSessionId_ReturnsInsufficientHistory()
    {
        var resolver = CreateResolver();
        var shape = MakeShape(sessionId: null);

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.InsufficientHistory, result.Status);
        Assert.NotNull(result.StatusDetail);
        Assert.Empty(result.NextOperations);
        Assert.Empty(result.NextTokens);
    }

    [Fact]
    public async Task ResolveAsync_NoContextHistory_ReturnsInsufficientHistory()
    {
        var resolver = CreateResolver();
        var shape = MakeShape(sessionId: Guid.NewGuid().ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.InsufficientHistory, result.Status);
        Assert.Equal("NO_CONTEXT_HISTORY", result.StatusDetail);
        Assert.Empty(result.NextOperations);
        Assert.Empty(result.NextTokens);
    }

    [Fact]
    public async Task ResolveAsync_MissingPolicy_ReturnsExplicitError_NotDefault()
    {
        // Policy-missing must surface as ExplicitError — no silent fallback to hardcoded values.
        var resolver = CreateResolver(topologyRepo: new StubMissingPolicyTopologyRepository());
        var shape = MakeShape(sessionId: Guid.NewGuid().ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("CONTEXT_ROUTE_POLICY_NOT_FOUND", result.StatusDetail);
        Assert.Empty(result.NextOperations);
        Assert.Empty(result.NextTokens);
    }

    [Fact]
    public async Task ResolveAsync_NoSilentFallback_StatusIsAlwaysExplicit()
    {
        var resolver = CreateResolver();

        // No session, no tokens, no history
        var shape = MakeShape(sessionId: null);
        var result = await resolver.ResolveAsync(shape);

        // Status must never be null — always explicit
        Assert.True(
            result.Status == RecommendationStatus.InsufficientHistory ||
            result.Status == RecommendationStatus.ExplicitError ||
            result.Status == RecommendationStatus.Ok,
            "Status must be one of the three explicit values."
        );
    }

    [Fact]
    public void ResolveNextOperations_EmptyNeighbors_ReturnsEmpty()
    {
        var resolver = CreateResolver();
        var policy = ContextRoutePolicyTestFixtures.ValidPolicy();
        var result = resolver.ResolveNextOperations([], [], policy);
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveNextOperations_NeighborVoting_RanksCorrectly()
    {
        var resolver = CreateResolver();
        var policy = ContextRoutePolicyTestFixtures.ValidPolicy();

        var neighbors = new List<ContextNeighborResult>
        {
            new(Guid.NewGuid(), 0, 0.9f, "action_a", null),
            new(Guid.NewGuid(), 0, 0.8f, "action_a", null),
            new(Guid.NewGuid(), 0, 0.5f, "action_b", null),
        };

        var result = resolver.ResolveNextOperations(neighbors, [], policy);

        Assert.NotEmpty(result);
        Assert.Equal("action_a", result[0].Value); // higher total sim wins
    }

    [Fact]
    public void ResolveNextOperations_TransitionStatsMerged()
    {
        var resolver = CreateResolver();
        var policy = ContextRoutePolicyTestFixtures.ValidPolicy();

        var neighbors = new List<ContextNeighborResult>
        {
            new(Guid.NewGuid(), 0, 0.5f, "action_a", null),
        };

        var stats = new List<ContextTransitionStat>
        {
            new("prev", "action_b", 100, 90f, 0.9f),
        };

        var result = resolver.ResolveNextOperations(neighbors, stats, policy);

        // Both action_a (from neighbors) and action_b (from stats) should appear
        Assert.Contains(result, r => r.Value == "action_a");
        Assert.Contains(result, r => r.Value == "action_b");
    }

    [Fact]
    public void ResolveNextTokens_EmptyNeighbors_ReturnsEmpty()
    {
        var resolver = CreateResolver();
        var policy = ContextRoutePolicyTestFixtures.ValidPolicy();
        var result = resolver.ResolveNextTokens([], policy);
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveNextTokens_VotesFromNextTokenIdsHint()
    {
        var resolver = CreateResolver();
        var policy = ContextRoutePolicyTestFixtures.ValidPolicy();
        var tokenId = Guid.NewGuid();

        var neighbors = new List<ContextNeighborResult>
        {
            new(Guid.NewGuid(), 0, 0.9f, null, [tokenId]),
            new(Guid.NewGuid(), 0, 0.7f, null, [tokenId]),
        };

        var result = resolver.ResolveNextTokens(neighbors, policy);

        Assert.Single(result);
        Assert.Equal(tokenId.ToString(), result[0].Value);
        Assert.Equal(0.9f + 0.7f, result[0].Score, precision: 5);
    }

    // --- RuntimeExecutor integration: emission includes recommendation ---

    [Fact]
    public async Task ExecuteAsync_EmissionAlwaysContainsRecommendation()
    {
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        // Recommendation is always present — InsufficientHistory when no session context
        Assert.NotNull(response.Emission!.ContextRouteRecommendation);
        Assert.Equal(
            RecommendationStatus.InsufficientHistory,
            response.Emission.ContextRouteRecommendation!.Status);
    }

    [Fact]
    public async Task ExecuteAsync_RecommendationDoesNotAbortPipeline()
    {
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        // Recommendation InsufficientHistory must NOT add ValidationErrors to the pipeline
        Assert.True(response.Success);
        Assert.Empty(response.Errors);
    }

    // --- Frontend isolation: no calculation logic in frontend types ---

    [Fact]
    public void FrontendIsolation_RecommendationIsDataOnly()
    {
        // ContextRouteRecommendationResult is a pure data record — no calculation methods.
        // Calculation is done only in ContextVectorBuilder, ContextNeighborSearch,
        // and ContextRouteRecommendationResolver (all backend-only).
        var result = new ContextRouteRecommendationResult(
            NextOperations: [],
            NextTokens: [],
            NearestPrefixSessionIds: [],
            ContributingTokens: [],
            Status: RecommendationStatus.InsufficientHistory,
            StatusDetail: "test"
        );

        Assert.Equal(RecommendationStatus.InsufficientHistory, result.Status);
        Assert.Empty(result.NextOperations);
    }

    // --- End-to-end: next operation candidates flow from loaded prefix history ---

    [Fact]
    public async Task ResolveAsync_WithPrefixHistory_ReturnsOkWithNextOperationCandidates()
    {
        // Stub repository returns 15 prefix candidates each carrying NextOperation="action_next"
        // with matching sparse vectors (cosine similarity = 1.0) → passes MinNeighbors=10 gate
        var tokenId = Guid.NewGuid();
        var stub = new StubPrefixRepository(tokenId);
        var resolver = CreateResolver(stub);

        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            contextTokenIds: tokenId.ToString()
        );

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.Ok, result.Status);
        Assert.NotEmpty(result.NextOperations);
        Assert.Contains(result.NextOperations, c => c.Value == "action_next");
    }

    // --- Policy scope tests ---

    [Fact]
    public async Task ResolveAsync_MalformedStatePolicyJson_ReturnsExplicitError()
    {
        var resolver = CreateResolver();
        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            statePolicyJson: "{ not valid json !!!"
        );

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("CONTEXT_ROUTE_STATE_POLICY_INVALID", result.StatusDetail);
    }

    [Fact]
    public async Task ResolveAsync_EmptyPolicyRef_ReturnsExplicitError()
    {
        var resolver = CreateResolver();
        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            statePolicyJson: """{"context_route_policy_ref": ""}"""
        );

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("CONTEXT_ROUTE_POLICY_REF_INVALID", result.StatusDetail);
    }

    [Fact]
    public async Task ResolveAsync_ScopedPolicyRef_UsesKeyFromStatePolicy()
    {
        // Scoped key "customer_portal_policy" is present in state_policy → must be passed to repo.
        // StubScopedPolicyTopologyRepository returns valid policy only for "customer_portal_policy".
        const string scopedKey = "customer_portal_policy";
        var resolver = CreateResolver(topologyRepo: new StubScopedPolicyTopologyRepository(scopedKey));
        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            statePolicyJson: $$$"""{"context_route_policy_ref": "{{{scopedKey}}}"}"""
        );

        var result = await resolver.ResolveAsync(shape);

        // Policy loaded successfully → InsufficientHistory (no history), not ExplicitError.
        Assert.NotEqual(RecommendationStatus.ExplicitError, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_ScopedPolicyRef_MissingInRepo_ReturnsExplicitError()
    {
        // Scoped key "unknown_policy" not in repo → ExplicitError(CONTEXT_ROUTE_POLICY_NOT_FOUND).
        var resolver = CreateResolver(topologyRepo: new StubScopedPolicyTopologyRepository("other_policy"));
        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            statePolicyJson: """{"context_route_policy_ref": "unknown_policy"}"""
        );

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("CONTEXT_ROUTE_POLICY_NOT_FOUND", result.StatusDetail);
    }

    // --- Hub attention identity tests ---

    [Fact]
    public async Task ResolveAsync_WithHubId_HubAttentionWritesUseHubId()
    {
        // sessionId and hubId are different Guids so we can verify which was actually used.
        var sessionId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        Assert.NotEqual(sessionId, hubId);

        var tokenId = Guid.NewGuid();
        var tracking = new HubAttentionTrackingRepository(tokenId);
        var resolver = CreateResolver(tracking, new StubValidPolicyTopologyRepository());

        var shape = MakeShape(
            sessionId: sessionId.ToString(),
            contextTokenIds: tokenId.ToString(),
            hubId: hubId);

        await resolver.ResolveAsync(shape);

        // All three hub attention DB calls must use hubId, not sessionId.
        Assert.NotEmpty(tracking.LoadHubIds);
        Assert.All(tracking.LoadHubIds, id => Assert.Equal(hubId, id));

        Assert.NotEmpty(tracking.UpsertHubIds);
        Assert.All(tracking.UpsertHubIds, id => Assert.Equal(hubId, id));

        Assert.NotEmpty(tracking.RecalculateHubIds);
        Assert.All(tracking.RecalculateHubIds, id => Assert.Equal(hubId, id));

        Assert.DoesNotContain(sessionId, tracking.LoadHubIds);
        Assert.DoesNotContain(sessionId, tracking.UpsertHubIds);
        Assert.DoesNotContain(sessionId, tracking.RecalculateHubIds);
    }

    [Fact]
    public async Task ResolveAsync_WithoutHubId_HubAttentionCurrentNotWritten()
    {
        // Hub attention must be skipped when IdOrHubId is null — hub attention is
        // hub-entity-scoped; no hub entity means no hub attention current write.
        // This holds even when session, tokenIds, and hub_attention policy are all present.
        var tokenId = Guid.NewGuid();
        var tracking = new HubAttentionTrackingRepository(tokenId);
        var resolver = CreateResolver(tracking, new StubValidPolicyTopologyRepository());

        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            contextTokenIds: tokenId.ToString(),
            hubId: null);

        await resolver.ResolveAsync(shape);

        Assert.Empty(tracking.LoadHubIds);
        Assert.Empty(tracking.UpsertHubIds);
        Assert.Empty(tracking.RecalculateHubIds);
    }

    // --- A11: failure path tests ---

    [Fact]
    public async Task ResolveAsync_AppendContextEventThrows_ReturnsExplicitError()
    {
        // AppendContextEventAsync failure must return ExplicitError, not a recommendation result.
        var repo = new AppendThrowingRepository();
        var resolver = CreateResolver(repo, new StubBlendOnlyPolicyTopologyRepository());
        var shape = MakeShape(sessionId: Guid.NewGuid().ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("CONTEXT_EVENT_APPEND_FAILED", result.StatusDetail);
        Assert.Empty(result.NextOperations);
        Assert.Empty(result.NextTokens);
    }

    [Fact]
    public async Task ResolveAsync_TransitionStatsThrows_ReturnsExplicitError()
    {
        // Transition stats query failure must return ExplicitError — no fallback to empty stats.
        // Uses a stub that returns 15 matching prefix candidates (enough to exceed MinNeighbors=10)
        // so the stats query path is actually reached, then throws.
        var tokenId = Guid.NewGuid();
        var repo = new TransitionStatsThrowingRepository(tokenId);
        var resolver = CreateResolver(repo, new StubValidPolicyTopologyRepository());

        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            contextTokenIds: tokenId.ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("TRANSITION_STATS_QUERY_FAILED", result.StatusDetail);
        Assert.Empty(result.NextOperations);
        Assert.Empty(result.NextTokens);
    }

    [Fact]
    public async Task ResolveAsync_TvrExtensionHubAttentionUpdateThrows_ReturnsExplicitError()
    {
        // TVR extension / hub attention current update failure must return ExplicitError.
        // Hub attention is hub-entity-scoped; hubId must be provided to enter the write path.
        var tokenId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var repo = new TvrExtensionThrowingRepository();
        var resolver = CreateResolver(repo, new StubValidPolicyTopologyRepository());

        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            contextTokenIds: tokenId.ToString(),
            hubId: hubId);

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("TVR_EXTENSION_FAILED", result.StatusDetail);
        Assert.Empty(result.NextOperations);
        Assert.Empty(result.NextTokens);
    }

    // --- Helper ---

    private static RuntimeWorkingShape MakeShape(
        string? sessionId,
        string? contextTokenIds = null,
        string? statePolicyJson = null,
        Guid? hubId = null)
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
            ContextRecordId: null,
            IdOrHubId: hubId
        );

        return new RuntimeWorkingShape(
            Vector: vector,
            StructureMapId: "test-map",
            PackageId: Guid.NewGuid(),
            SchemaId: Guid.NewGuid(),
            ComponentIds: [],
            PackageDef: null,
            SchemaDef: null,
            ResolvedData: null,
            Errors: [],
            StructureMapStatePolicyJson: statePolicyJson
        );
    }

    [Fact]
    public async Task ResolveAsync_PolicyWithNullRecentDays_DoesNotReturnExplicitError()
    {
        // Null recent_days must not produce ExplicitError. The resolver should
        // pass null to LoadRecentPrefixVectorsAsync without substituting a fallback.
        // With empty prefix history, result is InsufficientHistory, not ExplicitError.
        var resolver = CreateResolver(topologyRepo: new StubNullRecentDaysPolicyTopologyRepository());
        var shape = MakeShape(sessionId: Guid.NewGuid().ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.NotEqual(RecommendationStatus.ExplicitError, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_PolicyWithPreferRecentFalse_DoesNotReturnExplicitError()
    {
        // prefer_recent=false must not produce ExplicitError.
        // With empty prefix history, result is InsufficientHistory, not ExplicitError.
        var resolver = CreateResolver(topologyRepo: new StubPreferRecentFalsePolicyTopologyRepository());
        var shape = MakeShape(sessionId: Guid.NewGuid().ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.NotEqual(RecommendationStatus.ExplicitError, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_AppendContextEvent_CalledAfterLoadRecentPrefixVectors()
    {
        // AppendContextEventAsync must run AFTER LoadRecentPrefixVectorsAsync.
        // The prefix LATERAL JOIN resolves next_operation from the event following
        // last_event_id in the session. Appending first would inject the current
        // dispatch as the successor of the most-recent prefix entry, corrupting
        // next_operation for that prefix.
        var tokenId = Guid.NewGuid();
        var tracking = new OrderTrackingRepository(tokenId);
        var resolver = CreateResolver(
            repo: tracking,
            topologyRepo: new StubValidPolicyTopologyRepository());

        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            contextTokenIds: tokenId.ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.Ok, result.Status);
        Assert.True(tracking.LoadPrefixSequence > 0, "LoadRecentPrefixVectorsAsync must have been called.");
        Assert.True(tracking.AppendSequence > 0, "AppendContextEventAsync must have been called.");
        Assert.True(
            tracking.LoadPrefixSequence < tracking.AppendSequence,
            $"LoadRecentPrefixVectorsAsync (seq={tracking.LoadPrefixSequence}) must be called " +
            $"before AppendContextEventAsync (seq={tracking.AppendSequence}).");
    }

    [Fact]
    public async Task ResolveAsync_NoContextHistory_AppendContextEventStillCalled()
    {
        // When prefix candidate history is empty (NO_CONTEXT_HISTORY), the current
        // dispatch must still be appended so cold-start history can grow.
        var repo = new AppendTrackingNoPrefixRepository();
        var resolver = CreateResolver(
            repo: repo,
            topologyRepo: new StubValidPolicyTopologyRepository());

        var shape = MakeShape(sessionId: Guid.NewGuid().ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.InsufficientHistory, result.Status);
        Assert.Equal("NO_CONTEXT_HISTORY", result.StatusDetail);
        Assert.True(repo.WasAppendCalled, "AppendContextEventAsync must be called even on NO_CONTEXT_HISTORY.");
    }

    [Fact]
    public async Task ResolveAsync_InsufficientContextHistory_AppendContextEventStillCalled()
    {
        // When prefix candidates exist but no neighbor passes min_similarity
        // (INSUFFICIENT_CONTEXT_HISTORY), the current dispatch must still be appended.
        // Uses OrderTrackingRepository (15 candidates) with an empty event vector
        // (no contextTokenIds → norm=0 → all cosine similarities = 0 → 0 neighbors < 10).
        var tokenId = Guid.NewGuid();
        var tracking = new OrderTrackingRepository(tokenId);
        var resolver = CreateResolver(
            repo: tracking,
            topologyRepo: new StubValidPolicyTopologyRepository());

        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            contextTokenIds: null); // empty event vector → norm=0 → no neighbors pass min_similarity

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.InsufficientHistory, result.Status);
        Assert.Equal("INSUFFICIENT_CONTEXT_HISTORY", result.StatusDetail);
        Assert.True(tracking.AppendSequence > 0, "AppendContextEventAsync must be called even on INSUFFICIENT_CONTEXT_HISTORY.");
        Assert.True(
            tracking.LoadPrefixSequence < tracking.AppendSequence,
            "LoadRecentPrefixVectorsAsync must still run before AppendContextEventAsync.");
    }

    // --- SystemOperationCiRuntime wiring tests ---

    [Fact]
    public async Task ResolveAsync_BlockingEvidenceCi_ReturnsTvrExtensionFailed()
    {
        // Blocking CI finding on evidence integrity must propagate as TVR_EXTENSION_FAILED.
        var resolver = CreateResolver(ciRuntime: new StubBlockingEvidenceCiRuntime());

        var shape = MakeShape(sessionId: Guid.NewGuid().ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("TVR_EXTENSION_FAILED", result.StatusDetail);
    }

    [Fact]
    public async Task ResolveAsync_GapEvidenceCi_RecommendationContinues()
    {
        // Gap CI finding must not fail the recommendation — recommendation continues.
        // With no prefix candidates, result is InsufficientHistory (not ExplicitError).
        var resolver = CreateResolver(ciRuntime: new StubGapEvidenceCiRuntime());

        var shape = MakeShape(sessionId: Guid.NewGuid().ToString());

        var result = await resolver.ResolveAsync(shape);

        Assert.NotEqual(RecommendationStatus.ExplicitError, result.Status);
        Assert.NotEqual("TVR_EXTENSION_FAILED", result.StatusDetail);
    }

    [Fact]
    public async Task ResolveAsync_NanEmaFastExistingRecord_ReturnsTvrExtensionFailed()
    {
        // Existing hub attention record with NaN ema_fast causes ComputeEma to produce NaN,
        // which InspectHubAttentionAfterUpdate detects as Blocking → TVR_EXTENSION_FAILED.
        var tokenId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var repo = new NanEmaFastExistingRepository(tokenId, hubId);
        var resolver = CreateResolver(
            repo: repo,
            topologyRepo: new StubValidPolicyTopologyRepository(),
            ciRuntime: new SystemOperationCiRuntime(
                NullLogger<SystemOperationCiRuntime>.Instance, repo));

        var shape = MakeShape(
            sessionId: Guid.NewGuid().ToString(),
            contextTokenIds: tokenId.ToString(),
            hubId: hubId);

        var result = await resolver.ResolveAsync(shape);

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("TVR_EXTENSION_FAILED", result.StatusDetail);
    }

    /// <summary>
    /// Stub repository that returns a fixed token record and 15 prefix candidates
    /// all carrying NextOperation="action_next" with a known sparse vector.
    /// </summary>
    private sealed class StubPrefixRepository(Guid tokenId) : ContextRouteRepository(
        NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        public override Task<IReadOnlyList<ContextTokenRecord>> LoadActiveTokensAsync(
            IEnumerable<Guid> tokenIds,
            CancellationToken ct = default)
        {
            IReadOnlyList<ContextTokenRecord> result =
                [new ContextTokenRecord(tokenId, "stub_token", null, 1.0f, "active")];
            return Task.FromResult(result);
        }

        public override Task<IReadOnlyList<ContextPrefixVectorRecord>> LoadRecentPrefixVectorsAsync(
            string? tableName,
            string? role,
            int? maxDays,
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
                    NextTokenIdsHint: null
                ))
                .ToList();
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Stub repository that tracks the call sequence of LoadRecentPrefixVectorsAsync
    /// and AppendContextEventAsync to verify ordering guarantees in ResolveAsync.
    /// </summary>
    private sealed class OrderTrackingRepository(Guid tokenId) : ContextRouteRepository(
        NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        private int _seq;
        public int LoadPrefixSequence { get; private set; }
        public int AppendSequence { get; private set; }

        public override Task<IReadOnlyList<ContextTokenRecord>> LoadActiveTokensAsync(
            IEnumerable<Guid> tokenIds,
            CancellationToken ct = default)
        {
            IReadOnlyList<ContextTokenRecord> result =
                [new ContextTokenRecord(tokenId, "stub_token", null, 1.0f, "active")];
            return Task.FromResult(result);
        }

        public override Task<IReadOnlyList<ContextPrefixVectorRecord>> LoadRecentPrefixVectorsAsync(
            string? tableName,
            string? role,
            int? maxDays,
            CancellationToken ct = default)
        {
            LoadPrefixSequence = Interlocked.Increment(ref _seq);
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
                    NextTokenIdsHint: null
                ))
                .ToList();
            return Task.FromResult(result);
        }

        public override Task AppendContextEventAsync(ContextEventRecord ev, CancellationToken ct = default)
        {
            AppendSequence = Interlocked.Increment(ref _seq);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stub repository with no prefix candidates (base class returns empty) that tracks
    /// whether AppendContextEventAsync was called. Used to verify that cold-start
    /// dispatches (NO_CONTEXT_HISTORY) are still recorded for future history growth.
    /// </summary>
    private sealed class AppendTrackingNoPrefixRepository() : ContextRouteRepository(
        NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        public bool WasAppendCalled { get; private set; }

        public override Task AppendContextEventAsync(ContextEventRecord ev, CancellationToken ct = default)
        {
            WasAppendCalled = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stub repository that throws on AppendContextEventAsync to simulate a persistence failure.
    /// Used to verify that AppendContextEventAsync failure returns ExplicitError.
    /// </summary>
    private sealed class AppendThrowingRepository() : ContextRouteRepository(
        NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        public override Task AppendContextEventAsync(ContextEventRecord ev, CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated DB constraint failure on context_event append.");
    }

    /// <summary>
    /// Stub repository that returns 15 matching prefix candidates (enough to exceed MinNeighbors=10)
    /// but throws on both transition stats query methods to simulate a DB failure in stats aggregation.
    /// Used to verify that stats query failure returns ExplicitError with no fallback to empty stats.
    /// </summary>
    private sealed class TransitionStatsThrowingRepository(Guid tokenId) : ContextRouteRepository(
        NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        public override Task<IReadOnlyList<ContextTokenRecord>> LoadActiveTokensAsync(
            IEnumerable<Guid> tokenIds,
            CancellationToken ct = default)
        {
            IReadOnlyList<ContextTokenRecord> result =
                [new ContextTokenRecord(tokenId, "stub_token", null, 1.0f, "active")];
            return Task.FromResult(result);
        }

        public override Task<IReadOnlyList<ContextPrefixVectorRecord>> LoadRecentPrefixVectorsAsync(
            string? tableName,
            string? role,
            int? maxDays,
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
                    NextTokenIdsHint: null
                ))
                .ToList();
            return Task.FromResult(result);
        }

        public override Task<IReadOnlyList<ContextTransitionStat>> GetTransitionStatsAsync(
            string prevOperation,
            string? role,
            int candidateLimit,
            CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated DB error in transition stats query.");

        public override Task<IReadOnlyList<ContextTransitionStat>> GetWindowedTransitionStatsAsync(
            string prevOperation,
            string? role,
            TransitionAggregationPolicy aggregationPolicy,
            int candidateLimit,
            CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated DB error in windowed transition stats query.");
    }

    /// <summary>
    /// Stub repository that throws on UpsertHubAttentionCurrentAsync to simulate a hub attention
    /// DB constraint failure inside the TVR extension path.
    /// Used to verify that TVR extension failure returns ExplicitError.
    /// </summary>
    private sealed class TvrExtensionThrowingRepository() : ContextRouteRepository(
        NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        public override Task UpsertHubAttentionCurrentAsync(
            HubAttentionCurrentRecord record,
            CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated DB constraint failure in hub attention upsert.");
    }

    /// <summary>
    /// Tracks the hubId passed to LoadHubAttentionCurrentAsync, UpsertHubAttentionCurrentAsync,
    /// and RecalculateHubAttentionRanksAsync. Used to verify hub-entity-scoped identity.
    /// </summary>
    private sealed class HubAttentionTrackingRepository(Guid tokenId) : ContextRouteRepository(
        NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        public List<Guid> LoadHubIds { get; } = new();
        public List<Guid> UpsertHubIds { get; } = new();
        public List<Guid> RecalculateHubIds { get; } = new();

        public override Task<IReadOnlyList<ContextTokenRecord>> LoadActiveTokensAsync(
            IEnumerable<Guid> tokenIds,
            CancellationToken ct = default)
        {
            IReadOnlyList<ContextTokenRecord> result =
                [new ContextTokenRecord(tokenId, "stub_token", null, 1.0f, "active")];
            return Task.FromResult(result);
        }

        public override Task<IReadOnlyList<HubAttentionCurrentRecord>> LoadHubAttentionCurrentAsync(
            Guid hubId,
            int scopeLimit,
            CancellationToken ct = default)
        {
            LoadHubIds.Add(hubId);
            return Task.FromResult<IReadOnlyList<HubAttentionCurrentRecord>>([]);
        }

        public override Task UpsertHubAttentionCurrentAsync(
            HubAttentionCurrentRecord record,
            CancellationToken ct = default)
        {
            UpsertHubIds.Add(record.HubId);
            return Task.CompletedTask;
        }

        public override Task RecalculateHubAttentionRanksAsync(
            Guid hubId,
            int scopeLimit,
            CancellationToken ct = default)
        {
            RecalculateHubIds.Add(hubId);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stub SystemOperationCiRuntime whose InspectEvidenceIntegrity always returns Blocking.
    /// Used to verify that a Blocking CI result propagates as TVR_EXTENSION_FAILED.
    /// </summary>
    private sealed class StubBlockingEvidenceCiRuntime()
        : SystemOperationCiRuntime(
            NullLogger<SystemOperationCiRuntime>.Instance,
            new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy"))
    {
        public override SystemCiDiagnosticResult InspectEvidenceIntegrity(
            string? currentOperation,
            IReadOnlyList<TransitionKeyEvidence> evidence)
            => new(
                InspectionTarget: "evidence_integrity",
                InspectionKind:   SystemCiInspectionKind.EventDriven,
                OverallStatus:    SystemCiStatus.Blocking,
                Findings:         [new SystemCiFinding("STUB_BLOCKING", SystemCiStatus.Blocking, "stub blocking finding")],
                InspectedAt:      DateTimeOffset.UtcNow
            );
    }

    /// <summary>
    /// Stub SystemOperationCiRuntime whose InspectEvidenceIntegrity always returns Gap.
    /// Used to verify that a Gap CI result does not fail the recommendation.
    /// </summary>
    private sealed class StubGapEvidenceCiRuntime()
        : SystemOperationCiRuntime(
            NullLogger<SystemOperationCiRuntime>.Instance,
            new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy"))
    {
        public override SystemCiDiagnosticResult InspectEvidenceIntegrity(
            string? currentOperation,
            IReadOnlyList<TransitionKeyEvidence> evidence)
            => new(
                InspectionTarget: "evidence_integrity",
                InspectionKind:   SystemCiInspectionKind.EventDriven,
                OverallStatus:    SystemCiStatus.Gap,
                Findings:         [new SystemCiFinding("STUB_GAP", SystemCiStatus.Gap, "stub gap finding")],
                InspectedAt:      DateTimeOffset.UtcNow
            );
    }

    /// <summary>
    /// Stub repository that returns an existing hub attention record with NaN ema_fast.
    /// ComputeEma with NaN as prior produces NaN, triggering EMA_FAST_NOT_FINITE Blocking finding.
    /// </summary>
    private sealed class NanEmaFastExistingRepository(Guid tokenId, Guid hubId)
        : ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        public override Task<IReadOnlyList<HubAttentionCurrentRecord>> LoadHubAttentionCurrentAsync(
            Guid hId,
            int scopeLimit,
            CancellationToken ct = default)
        {
            IReadOnlyList<HubAttentionCurrentRecord> result =
            [
                new HubAttentionCurrentRecord(
                    HubId:                hubId,
                    TargetTable:          "context_token_registry",
                    CandidateKind:        "token",
                    CandidateId:          tokenId,
                    ScopeLimit:           scopeLimit,
                    BaseProbability:      null,
                    CosineSimilarity:     null,
                    StaticRelationWeight: null,
                    StatisticalWeight:    1.0f,
                    MlpFeatureScore:      null,
                    FeedbackAdjustment:   0.0f,
                    EmaFast:              float.NaN,
                    EmaSlow:              0.1f,
                    Trend:                0.0f,
                    CrossState:           "none",
                    AttentionScore:       1.0f,
                    Rank:                 1,
                    EvidenceJson:         "[]",
                    MlpFeatureJson:       "[]",
                    UpdatedAt:            DateTimeOffset.UtcNow)
            ];
            return Task.FromResult(result);
        }
    }
}

public partial class ContextRouteRecommendationResolverTests
{
    [Fact]
    public void ResolveNextTokens_WithRecommendationBlend_ReordersByCurrentAttention()
    {
        var resolver = CreateResolver();
        var tokenA = Guid.NewGuid();
        var tokenB = Guid.NewGuid();

        var neighbors = new List<ContextNeighborResult>
        {
            new(Guid.NewGuid(), 0, 0.9f, null, [tokenA]),
            new(Guid.NewGuid(), 0, 0.8f, null, [tokenA]),
            new(Guid.NewGuid(), 0, 0.7f, null, [tokenB]),
            new(Guid.NewGuid(), 0, 0.6f, null, [tokenB]),
            new(Guid.NewGuid(), 0, 0.5f, null, [tokenB]),
        };

        var policy = new ContextRoutePolicy(
            0.05f, 50, 1, 90, 5, 0.5f, 0.5f, null,
            new TopologyVectorRuntimePolicy(
                true, null, null, null, null, null,
                new RecommendationBlendPolicy(true, 1000, 1.0f, 0.0f, 0.0f)));

        var map = new Dictionary<string, HubAttentionCurrentRecord>
        {
            [tokenA.ToString()] = new(Guid.NewGuid(), "context_token_registry", "token", tokenA, 1000, null, null, null, 1.0f, null, 0f, 0f, 0f, 0f, null, 20.0f, null, "[]", "[]", DateTimeOffset.UtcNow),
            [tokenB.ToString()] = new(Guid.NewGuid(), "context_token_registry", "token", tokenB, 1000, null, null, null, 1.0f, null, 0f, 0f, 0f, 0f, null, 0.0f, null, "[]", "[]", DateTimeOffset.UtcNow)
        };

        var result = resolver.ResolveNextTokens(neighbors, policy, map);
        Assert.Equal(tokenA.ToString(), result[0].Value);
    }

    [Fact]
    public async Task ResolveAsync_NoHubId_SkipsBlendRead_AndKeepsBaseline()
    {
        var tokenId = Guid.NewGuid();
        var repo = new CountingBlendRepository(tokenId);
        var resolver = CreateResolver(repo, new StubValidPolicyTopologyRepository());

        var result = await resolver.ResolveAsync(MakeShape(Guid.NewGuid().ToString(), tokenId.ToString(), hubId: null));

        Assert.Equal(RecommendationStatus.Ok, result.Status);
        Assert.Equal(0, repo.LoadHubAttentionCalls);
    }


    [Fact]
    public async Task ResolveAsync_BlendReadThrows_ReturnsExplicitErrorRecommendationBlendQueryFailed()
    {
        var tokenId = Guid.NewGuid();
        var repo = new ThrowingBlendReadRepository(tokenId);
        var resolver = CreateResolver(repo, new StubBlendOnlyPolicyTopologyRepository());

        var result = await resolver.ResolveAsync(MakeShape(Guid.NewGuid().ToString(), tokenId.ToString(), hubId: Guid.NewGuid()));

        Assert.Equal(RecommendationStatus.ExplicitError, result.Status);
        Assert.Equal("RECOMMENDATION_BLEND_QUERY_FAILED", result.StatusDetail);
    }

    [Fact]
    public async Task ResolveAsync_BlendCurrentMissing_KeepsBaselineRecommendation()
    {
        var tokenId = Guid.NewGuid();
        var repo = new MissingBlendRowsRepository(tokenId);
        var resolver = CreateResolver(repo, new StubValidPolicyTopologyRepository());

        var result = await resolver.ResolveAsync(MakeShape(Guid.NewGuid().ToString(), tokenId.ToString(), hubId: Guid.NewGuid()));

        Assert.Equal(RecommendationStatus.Ok, result.Status);
        Assert.NotEmpty(result.NextTokens);
    }


    private class CountingBlendRepository(Guid tokenId) : ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy")
    {
        public int LoadHubAttentionCalls { get; private set; }
        public override Task<IReadOnlyList<ContextTokenRecord>> LoadActiveTokensAsync(IEnumerable<Guid> tokenIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ContextTokenRecord>>([new ContextTokenRecord(tokenId, "stub_token", null, 1.0f, "active")]);
        public override Task<IReadOnlyList<ContextPrefixVectorRecord>> LoadRecentPrefixVectorsAsync(string? tableName, string? role, int? maxDays, CancellationToken ct = default)
        {
            var vector = new Dictionary<Guid, float> { [tokenId] = 1.0f };
            IReadOnlyList<ContextPrefixVectorRecord> result = Enumerable.Range(0, 15)
                .Select(i => new ContextPrefixVectorRecord(Guid.NewGuid(), i, Guid.NewGuid(), vector, 1.0f, DateTimeOffset.UtcNow, "action_next", [tokenId]))
                .ToList();
            return Task.FromResult(result);
        }
        public override Task AppendContextEventAsync(ContextEventRecord e, CancellationToken ct = default) => Task.CompletedTask;
        public override Task<IReadOnlyList<ContextTransitionStat>> GetWindowedTransitionStatsAsync(string prevOperation, string? role, TransitionAggregationPolicy policy, int maxCandidatesShown, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ContextTransitionStat>>([]);
        public override Task<IReadOnlyList<HubAttentionCurrentRecord>> LoadHubAttentionCurrentAsync(Guid hubId, int scopeLimit, CancellationToken ct = default)
        {
            LoadHubAttentionCalls++;
            return Task.FromResult<IReadOnlyList<HubAttentionCurrentRecord>>([]);
        }
    }

    private sealed class MissingBlendRowsRepository(Guid tokenId) : CountingBlendRepository(tokenId) { }

    private sealed class ThrowingBlendReadRepository(Guid tokenId) : CountingBlendRepository(tokenId)
    {
        public override Task<IReadOnlyList<HubAttentionCurrentRecord>> LoadHubAttentionCurrentAsync(Guid hubId, int scopeLimit, CancellationToken ct = default)
            => Task.FromException<IReadOnlyList<HubAttentionCurrentRecord>>(new InvalidOperationException("blend read failure"));
    }

}
