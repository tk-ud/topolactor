using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

internal static class ContextRoutePolicyTestFixtures
{
    /// <summary>
    /// Valid policy values for tests only.
    /// Production policy values must come from stored topology data.
    /// </summary>
    internal static ContextRoutePolicy ValidPolicy() => new(
        MinSimilarity:      0.05f,
        TopK:               50,
        MinNeighbors:       10,
        RecentDays:         90,
        MaxCandidatesShown: 5,
        BaselineWeight:     0.5f,
        NeighborWeight:     0.5f,
        TransitionAggregation: new TransitionAggregationPolicy(
            AggregationLimit: 10000,
            PreferRecent:     true,
            RecentDays:       null
        )
    );

    internal static string ValidPolicyJson() =>
        """{"min_similarity":0.05,"top_k":50,"min_neighbors":10,"recent_days":90,"max_candidates_shown":5,"baseline_weight":0.5,"neighbor_weight":0.5,"transition_aggregation":{"aggregation_limit":10000,"prefer_recent":true,"recent_days":null},"topology_vector_runtime":{"enabled":true,"registry_validation":{"enabled":true,"duplicate_threshold":1.0,"near_duplicate_threshold":0.85,"related_threshold":0.60,"top_k":10},"hub_attention":{"enabled":true,"scope_limits":[1000,3000,10000],"ema_fast_alpha":0.30,"ema_slow_alpha":0.10,"max_update_candidates_per_event":10000},"transition_key_evidence":{"enabled":true,"operation_contribution":1.0,"relation_contribution":0.8,"state_contribution":0.7,"table_contribution":0.6,"neighbor_top_k":3},"topology_mlp":{"enabled":true,"max_feature_cross_order":3},"feedback_weight_update":{"enabled":true,"positive_delta":0.05,"negative_delta":-0.02,"missing_candidate_delta":0.03},"recommendation_blend":{"enabled":true,"scope_limit":1000,"attention_score_weight":1.0,"trend_weight":0.0,"statistics_weight":0.0}}}""";



    internal static string BlendOnlyPolicyJson() =>
        """{"min_similarity":0.05,"top_k":50,"min_neighbors":10,"recent_days":90,"max_candidates_shown":5,"baseline_weight":0.5,"neighbor_weight":0.5,"transition_aggregation":{"aggregation_limit":10000,"prefer_recent":true,"recent_days":null},"topology_vector_runtime":{"enabled":true,"hub_attention":{"enabled":false,"scope_limits":[1000],"ema_fast_alpha":0.30,"ema_slow_alpha":0.10,"max_update_candidates_per_event":10000},"transition_key_evidence":{"enabled":false,"operation_contribution":1.0,"relation_contribution":0.8,"state_contribution":0.7,"table_contribution":0.6,"neighbor_top_k":3},"recommendation_blend":{"enabled":true,"scope_limit":1000,"attention_score_weight":1.0,"trend_weight":0.0,"statistics_weight":0.0}}}""";

    /// <summary>
    /// Policy JSON with recent_days=null at top level and in transition_aggregation.
    /// Verifies that null recent_days propagates without fallback to a magic number.
    /// </summary>
    internal static string ValidPolicyJsonWithNullRecentDays() =>
        """{"min_similarity":0.05,"top_k":50,"min_neighbors":10,"recent_days":null,"max_candidates_shown":5,"baseline_weight":0.5,"neighbor_weight":0.5,"transition_aggregation":{"aggregation_limit":10000,"prefer_recent":true,"recent_days":null}}""";

    /// <summary>
    /// Policy JSON with prefer_recent=false.
    /// Verifies that prefer_recent=false does not cause a resolver error.
    /// </summary>
    internal static string ValidPolicyJsonWithPreferRecentFalse() =>
        """{"min_similarity":0.05,"top_k":50,"min_neighbors":10,"recent_days":90,"max_candidates_shown":5,"baseline_weight":0.5,"neighbor_weight":0.5,"transition_aggregation":{"aggregation_limit":10000,"prefer_recent":false,"recent_days":null}}""";
}

/// <summary>
/// Stub TopologyRepository that returns a valid policy JSON for the context route
/// recommendation function. This keeps test policy values in test fixtures rather
/// than production repository code.
/// </summary>
internal sealed class StubValidPolicyTopologyRepository()
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
{
    public override Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
        => Task.FromResult<string?>(ContextRoutePolicyTestFixtures.ValidPolicyJson());
}

/// <summary>
/// Stub TopologyRepository that returns null for LoadFunctionParameterAsync,
/// simulating a missing policy row in function_parameters.
/// Used to assert that policy-missing → ExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND").
/// </summary>
internal sealed class StubMissingPolicyTopologyRepository()
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
{
    public override Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}

/// <summary>
/// Stub TopologyRepository that returns ValidPolicyJson only when the expected
/// parameter key is used; returns null for any other key.
/// Used to assert that a context_route_policy_ref in state_policy overrides default_policy
/// and that the scoped key is actually passed through to the repository.
/// </summary>
internal sealed class StubScopedPolicyTopologyRepository(string expectedKey)
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
{
    public override Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
        => Task.FromResult<string?>(
            parameterKey == expectedKey ? ContextRoutePolicyTestFixtures.ValidPolicyJson() : null);
}

internal sealed class StubNullRecentDaysPolicyTopologyRepository()
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
{
    public override Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
        => Task.FromResult<string?>(ContextRoutePolicyTestFixtures.ValidPolicyJsonWithNullRecentDays());
}

internal sealed class StubPreferRecentFalsePolicyTopologyRepository()
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
{
    public override Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
        => Task.FromResult<string?>(ContextRoutePolicyTestFixtures.ValidPolicyJsonWithPreferRecentFalse());
}

internal sealed class StubBlendOnlyPolicyTopologyRepository()
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
{
    public override Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
        => Task.FromResult<string?>(ContextRoutePolicyTestFixtures.BlendOnlyPolicyJson());
}
