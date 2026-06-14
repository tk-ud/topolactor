using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies Gap-12 (admin_contracts finalization) explicit-failure boundaries.
///
/// Gap-12: ContextRouteRepository.CreateContextTokenAsync (base class) throws
///         CONTEXT_TOKEN_REGISTRY_NOT_CONNECTED instead of returning the removed
///         CreateTokenCode.NotConnected placeholder. Production contract is
///         Success | Conflict only.
/// </summary>
public class ProductionHardeningBoundaryTests
{
    [Fact]
    public void NpgsqlDiffLogRepository_AppendEditSql_UsesCanonicalTopologySchema()
    {
        var repositoryPath = Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../../backend/repository/NpgsqlDiffLogRepository.cs");
        var source = File.ReadAllText(repositoryPath);

        Assert.Contains("INSERT INTO topology.topology_edit_log", source);
        Assert.DoesNotContain("INSERT INTO topology_edit_log", source);
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
