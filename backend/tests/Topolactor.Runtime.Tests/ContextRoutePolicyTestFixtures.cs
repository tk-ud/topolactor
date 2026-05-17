using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

internal static class ContextRoutePolicyTestFixtures
{
    /// <summary>
    /// Returns a valid ContextRoutePolicy matching the seed JSON in TopologyRepository.
    /// Use this in tests that exercise ResolveNextOperations / ResolveNextTokens directly.
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
