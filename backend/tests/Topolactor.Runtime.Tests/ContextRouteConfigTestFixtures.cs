using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Test-only fixtures for ContextRouteConfig.
/// Production code must never reference these.
/// </summary>
internal static class ContextRouteConfigTestFixtures
{
    /// <summary>
    /// Returns a valid ContextRouteConfig for use in tests that call
    /// ResolveNextOperations / ResolveNextTokens directly and need an explicit config.
    /// Values match the context_route_config.sql seed rows and the skeleton _seedConfig.
    /// </summary>
    internal static ContextRouteConfig ValidConfig() => new(
        MinSimilarity: 0.05f,
        TopK: 50,
        MinNeighbors: 10,
        RecentDays: 90,
        MaxCandidatesShown: 5,
        BaselineWeight: 0.5f,
        NeighborWeight: 0.5f
    );
}

/// <summary>
/// Stub ContextRouteConfigRepository that always returns MissingPolicy.
/// Used to verify that policy-missing produces an explicit error, not a fallback.
/// </summary>
internal sealed class StubMissingConfigRepository()
    : ContextRouteConfigRepository(NullLogger<ContextRouteConfigRepository>.Instance, "dummy")
{
    public override Task<ConfigLoadResult> LoadConfigAsync(CancellationToken ct = default)
        => Task.FromResult<ConfigLoadResult>(new ConfigLoadResult.MissingPolicy());
}
