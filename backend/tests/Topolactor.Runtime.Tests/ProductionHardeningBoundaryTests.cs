using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies Gap-11 (topology_repository production hardening) and
/// Gap-12 (admin_contracts finalization) explicit-failure boundaries.
///
/// Gap-11: TopologyRepository base class demo methods throw TOPOLOGY_REPOSITORY_NOT_CONNECTED
///         rather than returning placeholder data.
///         NpgsqlTopologyRepository resolves hub_id and relation_id exclusively via
///         LoadFunctionParameterAsync("demo_entity_defaults", ...) with no seeded fallback.
///         Missing parameter throws DEMO_ENTITY_DEFAULT_HUB_ID_NOT_CONFIGURED or
///         DEMO_ENTITY_DEFAULT_RELATION_ID_NOT_CONFIGURED — verified by FakeNpgsql tests below.
///
/// Gap-12: ContextRouteRepository.CreateContextTokenAsync (base class) throws
///         CONTEXT_TOKEN_REGISTRY_NOT_CONNECTED instead of returning the removed
///         CreateTokenCode.NotConnected placeholder. Production contract is
///         Success | Conflict only.
/// </summary>
public class ProductionHardeningBoundaryTests
{
    // ─── Gap-11: Topology Repository Explicit Failure Boundary ───────────────

    [Fact]
    public async Task TopologyRepository_LoadDemoEntityList_ThrowsWhenNotConnected()
    {
        var repo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.LoadDemoEntityListAsync());
        Assert.Equal("TOPOLOGY_REPOSITORY_NOT_CONNECTED", ex.Message);
    }

    [Fact]
    public async Task TopologyRepository_LoadDemoEntityDetail_ThrowsWhenNotConnected()
    {
        var repo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.LoadDemoEntityDetailAsync(Guid.NewGuid()));
        Assert.Equal("TOPOLOGY_REPOSITORY_NOT_CONNECTED", ex.Message);
    }

    [Fact]
    public async Task TopologyRepository_ApplyDemoTransition_ThrowsWhenNotConnected()
    {
        var repo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.ApplyDemoTransitionAsync(Guid.NewGuid(), "create", "title"));
        Assert.Equal("TOPOLOGY_REPOSITORY_NOT_CONNECTED", ex.Message);
    }

    [Fact]
    public async Task TopologyRepository_LoadDemoTransitionHistory_ThrowsWhenNotConnected()
    {
        var repo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.LoadDemoTransitionHistoryAsync(Guid.NewGuid()));
        Assert.Equal("TOPOLOGY_REPOSITORY_NOT_CONNECTED", ex.Message);
    }

    // ─── Gap-11: NpgsqlTopologyRepository — function_parameters resolution boundary ──

    /// <summary>
    /// Overrides LoadFunctionParameterAsync so resolution behavior can be tested
    /// without a real DB connection. All other Npgsql methods remain as-is.
    /// </summary>
    private sealed class FakeNpgsqlTopologyRepository : NpgsqlTopologyRepository
    {
        private readonly Dictionary<string, string?> _params;

        public FakeNpgsqlTopologyRepository(Dictionary<string, string?> @params)
            : base(NullLogger<NpgsqlTopologyRepository>.Instance, "dummy")
        {
            _params = @params;
        }

        public override Task<string?> LoadFunctionParameterAsync(
            string functionName, string parameterKey, CancellationToken ct = default)
            => Task.FromResult(
                _params.TryGetValue($"{functionName}/{parameterKey}", out var v) ? v : (string?)null);
    }

    [Fact]
    public async Task NpgsqlTopologyRepository_LoadDemoEntityList_MissingHubId_ThrowsExplicit()
    {
        var repo = new FakeNpgsqlTopologyRepository(new Dictionary<string, string?>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.LoadDemoEntityListAsync());
        Assert.Equal("DEMO_ENTITY_DEFAULT_HUB_ID_NOT_CONFIGURED", ex.Message);
    }

    [Fact]
    public async Task NpgsqlTopologyRepository_ApplyDemoTransition_MissingHubId_ThrowsExplicit()
    {
        var repo = new FakeNpgsqlTopologyRepository(new Dictionary<string, string?>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.ApplyDemoTransitionAsync(Guid.NewGuid(), "create", "title"));
        Assert.Equal("DEMO_ENTITY_DEFAULT_HUB_ID_NOT_CONFIGURED", ex.Message);
    }

    [Fact]
    public async Task NpgsqlTopologyRepository_ApplyDemoTransition_MissingRelationId_ThrowsExplicit()
    {
        var validHubId = Guid.NewGuid().ToString();
        var repo = new FakeNpgsqlTopologyRepository(new Dictionary<string, string?>
        {
            ["demo_entity_defaults/hub_id"] = validHubId,
            // relation_id intentionally absent
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.ApplyDemoTransitionAsync(Guid.NewGuid(), "create", "title"));
        Assert.Equal("DEMO_ENTITY_DEFAULT_RELATION_ID_NOT_CONFIGURED", ex.Message);
    }

    // ─── Gap-12: Admin Contracts — NotConnected Removed ──────────────────────

    [Fact]
    public async Task ContextRouteRepository_CreateContextToken_ThrowsWhenNotConnected()
    {
        var repo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.CreateContextTokenAsync("label", null, 0.5f));
        Assert.Equal("CONTEXT_TOKEN_REGISTRY_NOT_CONNECTED", ex.Message);
    }

    [Fact]
    public void CreateTokenCode_DoesNotContainNotConnected()
    {
        var values = Enum.GetValues<CreateTokenCode>();
        Assert.DoesNotContain(values, v => v.ToString() == "NotConnected");
        Assert.Contains(CreateTokenCode.Success, values);
        Assert.Contains(CreateTokenCode.Conflict, values);
    }
}
