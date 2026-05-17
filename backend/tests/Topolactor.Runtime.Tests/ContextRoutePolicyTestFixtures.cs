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
        """{"min_similarity":0.05,"top_k":50,"min_neighbors":10,"recent_days":90,"max_candidates_shown":5,"baseline_weight":0.5,"neighbor_weight":0.5,"transition_aggregation":{"aggregation_limit":10000,"prefer_recent":true,"recent_days":null}}""";
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