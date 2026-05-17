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
        NeighborWeight:     0.5f
    );

    internal static string ValidPolicyJson() =>
        """{"min_similarity":0.05,"top_k":50,"min_neighbors":10,"recent_days":90,"max_candidates_shown":5,"baseline_weight":0.5,"neighbor_weight":0.5}""";
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