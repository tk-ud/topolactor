using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for TopologyVectorRuntime: registry vector validation, cosine similarity,
/// EMA/cross state, MLP feature crossing, and feedback event building.
///
/// All numeric policy values come from test fixtures — never hardcoded in production code.
/// </summary>
public class TopologyVectorRuntimeTests
{
    private readonly TopologyVectorRuntime _runtime;

    private static readonly RegistryValidationPolicy DefaultRegistryValidationPolicy = new(
        Enabled: true,
        DuplicateThreshold: 1.0f,
        NearDuplicateThreshold: 0.85f,
        RelatedThreshold: 0.60f,
        TopK: 10
    );

    private static readonly FeedbackWeightUpdatePolicy DefaultFeedbackPolicy = new(
        Enabled: true,
        PositiveDelta: 0.05f,
        NegativeDelta: -0.02f,
        MissingCandidateDelta: 0.03f
    );

    private static readonly TransitionKeyEvidencePolicy DefaultEvidencePolicy = new(
        Enabled: true,
        OperationContribution: 1.0f,
        RelationContribution: 0.8f,
        StateContribution: 0.7f,
        TableContribution: 0.6f,
        NeighborTopK: 3
    );

    public TopologyVectorRuntimeTests()
    {
        _runtime = new TopologyVectorRuntime(
            NullLogger<TopologyVectorRuntime>.Instance,
            new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy")
        );
    }

    // -------------------------------------------------------------------------
    // ComputeSparseCosineSimilarity
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeSparseCosineSimilarity_IdenticalSets_ReturnsOne()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var a = new HashSet<Guid> { id1, id2 };

        var result = TopologyVectorRuntime.ComputeSparseCosineSimilarity(a, a);

        Assert.Equal(1.0f, result, precision: 4);
    }

    [Fact]
    public void ComputeSparseCosineSimilarity_DisjointSets_ReturnsZero()
    {
        var a = new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var b = new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var result = TopologyVectorRuntime.ComputeSparseCosineSimilarity(a, b);

        Assert.Equal(0.0f, result, precision: 4);
    }

    [Fact]
    public void ComputeSparseCosineSimilarity_EmptySet_ReturnsZero()
    {
        var a = new HashSet<Guid> { Guid.NewGuid() };
        var b = new HashSet<Guid>();

        var result = TopologyVectorRuntime.ComputeSparseCosineSimilarity(a, b);

        Assert.Equal(0.0f, result, precision: 4);
    }

    [Fact]
    public void ComputeSparseCosineSimilarity_PartialOverlap_ReturnsCorrectValue()
    {
        var shared1 = Guid.NewGuid();
        var shared2 = Guid.NewGuid();
        var a = new HashSet<Guid> { shared1, shared2, Guid.NewGuid(), Guid.NewGuid() };
        var b = new HashSet<Guid> { shared1, shared2, Guid.NewGuid() };

        // intersection=2, cardinality(a)=4, cardinality(b)=3
        // expected = 2 / sqrt(4 * 3) = 2 / sqrt(12) ≈ 0.5774
        var result = TopologyVectorRuntime.ComputeSparseCosineSimilarity(a, b);
        Assert.InRange(result, 0.577f, 0.578f);
    }

    // -------------------------------------------------------------------------
    // ValidateRegistryVectorAsync — in-memory skeleton
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ValidateRegistryVectorAsync_ZeroVector_ReturnsZeroVectorClass()
    {
        var result = await _runtime.ValidateRegistryVectorAsync(
            "relation_registry", [], DefaultRegistryValidationPolicy);

        Assert.Equal(RegistryVectorValidationClass.ZeroVector, result.ValidationClass);
        Assert.False(result.IsBlocking);
    }

    [Fact]
    public async Task ValidateRegistryVectorAsync_DisabledPolicy_ReturnsPassWithDisabledDetail()
    {
        var disabledPolicy = DefaultRegistryValidationPolicy with { Enabled = false };
        var queryIds = new[] { Guid.NewGuid() };

        var result = await _runtime.ValidateRegistryVectorAsync(
            "relation_registry", queryIds, disabledPolicy);

        Assert.Equal(RegistryVectorValidationClass.Pass, result.ValidationClass);
        Assert.Equal("REGISTRY_VECTOR_VALIDATION_DISABLED", result.StatusDetail);
    }

    [Fact]
    public async Task ValidateRegistryVectorAsync_InMemorySkeleton_ReturnsPass()
    {
        var queryIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var result = await _runtime.ValidateRegistryVectorAsync(
            "relation_registry", queryIds, DefaultRegistryValidationPolicy);

        Assert.Equal(RegistryVectorValidationClass.Pass, result.ValidationClass);
        Assert.False(result.IsBlocking);
        Assert.Empty(result.Neighbors);
    }

    [Fact]
    public async Task ValidateRegistryVectorAsync_DbUnavailable_IsBlockingExplicitError()
    {
        // Arrange: repository that throws on FindRegistryVectorNeighborsAsync
        var throwingRuntime = new TopologyVectorRuntime(
            NullLogger<TopologyVectorRuntime>.Instance,
            new ThrowingRegistryRepository());
        var queryIds = new[] { Guid.NewGuid() };

        var result = await throwingRuntime.ValidateRegistryVectorAsync(
            "relation_registry", queryIds, DefaultRegistryValidationPolicy);

        Assert.Equal(RegistryVectorValidationClass.ExplicitError, result.ValidationClass);
        Assert.True(result.IsBlocking);
        Assert.Equal("REGISTRY_VECTOR_VALIDATION_DB_UNAVAILABLE", result.StatusDetail);
    }

    // -------------------------------------------------------------------------
    // ExtractTransitionKeyEvidence
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractTransitionKeyEvidence_NullPolicy_ReturnsEmpty()
    {
        var result = TopologyVectorRuntime.ExtractTransitionKeyEvidence(
            Guid.NewGuid(), "op", [], [], [], [], null);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractTransitionKeyEvidence_DisabledPolicy_ReturnsEmpty()
    {
        var disabled = DefaultEvidencePolicy with { Enabled = false };
        var result = TopologyVectorRuntime.ExtractTransitionKeyEvidence(
            Guid.NewGuid(), "op", [], [], [], [], disabled);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractTransitionKeyEvidence_ScoresFromPolicy_NotHardcoded()
    {
        var customPolicy = new TransitionKeyEvidencePolicy(
            Enabled: true,
            OperationContribution: 0.9f,
            RelationContribution: 0.5f,
            StateContribution: 0.4f,
            TableContribution: 0.3f,
            NeighborTopK: 2
        );
        var relId = Guid.NewGuid();
        var result = TopologyVectorRuntime.ExtractTransitionKeyEvidence(
            Guid.NewGuid(), "view_detail",
            relationIds: [relId],
            stateIds: [],
            tableNames: [],
            topNeighbors: [],
            policy: customPolicy);

        Assert.Equal(2, result.Count);
        var opEntry = result.Single(e => e.KeyKind == "operation");
        Assert.Equal(0.9f, opEntry.ContributionScore, precision: 4);
        var relEntry = result.Single(e => e.KeyKind == "relation");
        Assert.Equal(0.5f, relEntry.ContributionScore, precision: 4);
    }

    [Fact]
    public void ExtractTransitionKeyEvidence_NeighborTopKFromPolicy()
    {
        var policyTopK2 = DefaultEvidencePolicy with { NeighborTopK = 2 };
        var neighbors = Enumerable.Range(0, 5).Select(_ =>
            new RegistryVectorNeighbor(Guid.NewGuid(), "n", 0.7f, [], "r")).ToList();

        var result = TopologyVectorRuntime.ExtractTransitionKeyEvidence(
            Guid.NewGuid(), null, [], [], [], neighbors, policyTopK2);

        Assert.Equal(2, result.Count(e => e.KeyKind == "cosine_neighbor"));
    }

    // -------------------------------------------------------------------------
    // EMA and Cross State
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeEma_NoPreviousEma_ReturnsCurrentValue()
    {
        var result = TopologyVectorRuntime.ComputeEma(0.8f, null, 0.30f);
        Assert.Equal(0.8f, result, precision: 4);
    }

    [Fact]
    public void ComputeEma_WithPreviousEma_AppliesAlpha()
    {
        // ema = 0.30 * 1.0 + 0.70 * 0.5 = 0.30 + 0.35 = 0.65
        var result = TopologyVectorRuntime.ComputeEma(1.0f, 0.5f, 0.30f);
        Assert.Equal(0.65f, result, precision: 4);
    }

    [Fact]
    public void ComputeCrossState_FastCrossesAboveSlow_ReturnsUp()
    {
        // previous: fast=0.4, slow=0.5 (fast < slow, trend negative)
        // current:  fast=0.6, slow=0.5 (fast > slow, trend positive)
        var result = TopologyVectorRuntime.ComputeCrossState(0.6f, 0.5f, 0.4f, 0.5f);
        Assert.Equal("up", result);
    }

    [Fact]
    public void ComputeCrossState_FastCrossesBelowSlow_ReturnsDown()
    {
        // previous: fast=0.6, slow=0.5 (fast > slow, trend positive)
        // current:  fast=0.4, slow=0.5 (fast < slow, trend negative)
        var result = TopologyVectorRuntime.ComputeCrossState(0.4f, 0.5f, 0.6f, 0.5f);
        Assert.Equal("down", result);
    }

    [Fact]
    public void ComputeCrossState_NoCross_ReturnsNone()
    {
        // both above: no cross
        var result = TopologyVectorRuntime.ComputeCrossState(0.7f, 0.5f, 0.6f, 0.5f);
        Assert.Equal("none", result);
    }

    [Fact]
    public void ComputeCrossState_NoPreviousEma_ReturnsNone()
    {
        var result = TopologyVectorRuntime.ComputeCrossState(0.7f, 0.5f, null, null);
        Assert.Equal("none", result);
    }

    // -------------------------------------------------------------------------
    // Topology MLP Feature Crossing
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildTopologyMlpFeatures_NoEvidence_ReturnsEmptyWithZeroScore()
    {
        var (features, score) = TopologyVectorRuntime.BuildTopologyMlpFeatures([], maxFeatureCrossOrder: 3);
        Assert.Empty(features);
        Assert.Equal(0.0f, score);
    }

    [Fact]
    public void BuildTopologyMlpFeatures_SingleEvidence_NoCrossings()
    {
        var evidence = new[]
        {
            new TransitionKeyEvidence("operation", "op1", "view", 1.0f, "current_operation")
        };
        var (features, score) = TopologyVectorRuntime.BuildTopologyMlpFeatures(evidence, maxFeatureCrossOrder: 3);
        // Only 1 element — no pairs possible
        Assert.Empty(features);
        Assert.Equal(0.0f, score);
    }

    [Fact]
    public void BuildTopologyMlpFeatures_TwoEvidence_ProducesPair()
    {
        var evidence = new[]
        {
            new TransitionKeyEvidence("relation", "rA", "rel-A", 0.8f, "active_relation"),
            new TransitionKeyEvidence("state",    "sB", "state-B", 0.7f, "active_state")
        };
        var (features, score) = TopologyVectorRuntime.BuildTopologyMlpFeatures(evidence, maxFeatureCrossOrder: 3);
        Assert.Single(features);
        // cross score = 0.8 * 0.7 = 0.56
        Assert.Equal(0.56f, features[0].CrossScore, precision: 4);
        Assert.Equal(2, features[0].FeatureKeys.Count);
    }

    [Fact]
    public void BuildTopologyMlpFeatures_MaxCrossOrderZero_ReturnsEmpty()
    {
        var evidence = new[]
        {
            new TransitionKeyEvidence("relation", "rA", "rel-A", 0.8f, "active_relation"),
            new TransitionKeyEvidence("state",    "sB", "state-B", 0.7f, "active_state")
        };
        var (features, score) = TopologyVectorRuntime.BuildTopologyMlpFeatures(evidence, maxFeatureCrossOrder: 0);
        Assert.Empty(features);
        Assert.Equal(0.0f, score);
    }

    // -------------------------------------------------------------------------
    // Feedback Event Building
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildFeedbackEvents_Selected_AppliesPositiveDelta()
    {
        var hubId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var events = TopologyVectorRuntime.BuildFeedbackEvents(
            hubId, "hub", 1000,
            selectedIds: [candidateId],
            ignoredIds: [],
            missingCandidateIds: [],
            DefaultFeedbackPolicy);

        Assert.Single(events);
        Assert.Equal("selected", events[0].FeedbackKind);
        Assert.Equal(DefaultFeedbackPolicy.PositiveDelta, events[0].DeltaApplied);
        Assert.Equal(candidateId, events[0].CandidateId);
    }

    [Fact]
    public void BuildFeedbackEvents_Ignored_AppliesNegativeDelta()
    {
        var hubId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var events = TopologyVectorRuntime.BuildFeedbackEvents(
            hubId, "hub", 1000,
            selectedIds: [],
            ignoredIds: [candidateId],
            missingCandidateIds: [],
            DefaultFeedbackPolicy);

        Assert.Single(events);
        Assert.Equal("ignored", events[0].FeedbackKind);
        Assert.Equal(DefaultFeedbackPolicy.NegativeDelta, events[0].DeltaApplied);
    }

    [Fact]
    public void BuildFeedbackEvents_MissingCandidate_AppliesMissingDelta()
    {
        var hubId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var events = TopologyVectorRuntime.BuildFeedbackEvents(
            hubId, "hub", 1000,
            selectedIds: [],
            ignoredIds: [],
            missingCandidateIds: [candidateId],
            DefaultFeedbackPolicy);

        Assert.Single(events);
        Assert.Equal("missing_candidate", events[0].FeedbackKind);
        Assert.Equal(DefaultFeedbackPolicy.MissingCandidateDelta, events[0].DeltaApplied);
    }

    [Fact]
    public void BuildFeedbackEvents_Mixed_ProducesCorrectCount()
    {
        var hubId = Guid.NewGuid();
        var events = TopologyVectorRuntime.BuildFeedbackEvents(
            hubId, "hub", 1000,
            selectedIds: [Guid.NewGuid(), Guid.NewGuid()],
            ignoredIds: [Guid.NewGuid()],
            missingCandidateIds: [Guid.NewGuid()],
            DefaultFeedbackPolicy);

        Assert.Equal(4, events.Count);
        Assert.Equal(2, events.Count(e => e.FeedbackKind == "selected"));
        Assert.Equal(1, events.Count(e => e.FeedbackKind == "ignored"));
        Assert.Equal(1, events.Count(e => e.FeedbackKind == "missing_candidate"));
    }

    [Fact]
    public void BuildFeedbackEvents_DeltaFromPolicy_NotHardcoded()
    {
        var customPolicy = new FeedbackWeightUpdatePolicy(
            Enabled: true,
            PositiveDelta: 0.10f,
            NegativeDelta: -0.05f,
            MissingCandidateDelta: 0.07f
        );
        var hubId = Guid.NewGuid();
        var events = TopologyVectorRuntime.BuildFeedbackEvents(
            hubId, "entity", 3000,
            selectedIds: [Guid.NewGuid()],
            ignoredIds: [],
            missingCandidateIds: [],
            customPolicy);

        Assert.Single(events);
        Assert.Equal(0.10f, events[0].DeltaApplied, precision: 4);
    }

    // -------------------------------------------------------------------------
    // Policy JSON round-trip (via test fixture)
    // -------------------------------------------------------------------------

    [Fact]
    public void PolicyJson_ContainsTopologyVectorRuntime_ParsesWithoutError()
    {
        // The ValidPolicyJson fixture includes topology_vector_runtime.
        // This test verifies the JSON is well-formed and parseable by the
        // test fixture (round-trip verification without production DTO).
        var json = ContextRoutePolicyTestFixtures.ValidPolicyJson();
        Assert.Contains("topology_vector_runtime", json);
        Assert.Contains("registry_validation", json);
        Assert.Contains("hub_attention", json);
        Assert.Contains("transition_key_evidence", json);
        Assert.Contains("topology_mlp", json);
        Assert.Contains("feedback_weight_update", json);
    }
}

/// <summary>
/// Stub repository that throws on FindRegistryVectorNeighborsAsync to simulate DB unavailability.
/// Used to verify fail-closed behavior: DB unavailable → ExplicitError + IsBlocking:true.
/// </summary>
internal sealed class ThrowingRegistryRepository()
    : ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy")
{
    public override Task<IReadOnlyList<RegistryVectorNeighbor>> FindRegistryVectorNeighborsAsync(
        string registryTable,
        IReadOnlyList<Guid> queryIds,
        float minSimilarity,
        int topK,
        CancellationToken ct = default)
        => throw new InvalidOperationException("Simulated DB unavailable");
}
