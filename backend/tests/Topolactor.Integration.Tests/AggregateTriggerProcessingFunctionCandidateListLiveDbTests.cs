using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof that admin/contents Step3's processing_function_scope.function_id selector
/// candidates come from a real query against topology.abstract_function_manifests filtered to
/// runtime_lane=aggregate_trigger_runtime and active=true, not a fabricated or hardcoded list.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires db/topology_tables.sql
/// (abstract_function_manifests.runtime_lane CHECK extended with 'aggregate_trigger_runtime')
/// applied to the target database.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class AggregateTriggerProcessingFunctionCandidateListLiveDbTests
{
    [Fact]
    public async Task ListActiveByRuntimeLaneAsync_ReturnsOnlyActiveAggregateTriggerRuntimeRows()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N");
        var activeKey = $"candidate-active-{suffix}";
        var inactiveKey = $"candidate-inactive-{suffix}";
        var otherLaneKey = $"candidate-otherlane-{suffix}";
        var repo = new NpgsqlAbstractFunctionManifestRepository(cs);

        try
        {
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(cs, activeKey, "aggregate_trigger_runtime", "candidate-scope", active: true);
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(cs, inactiveKey, "aggregate_trigger_runtime", "candidate-scope", active: false);
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(cs, otherLaneKey, "admin_runtime", "candidate-scope", active: true);

            var candidates = await repo.ListActiveByRuntimeLaneAsync("aggregate_trigger_runtime");

            Assert.Contains(candidates, c => c.FunctionKey == activeKey);
            Assert.DoesNotContain(candidates, c => c.FunctionKey == inactiveKey);
            Assert.DoesNotContain(candidates, c => c.FunctionKey == otherLaneKey);
            var found = Assert.Single(candidates.Where(c => c.FunctionKey == activeKey));
            Assert.Equal("candidate-scope", found.AuthorityScope);
            Assert.True(found.Active);
        }
        finally
        {
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, activeKey);
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, inactiveKey);
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, otherLaneKey);
        }
    }

    [Fact]
    public async Task AdminRuntime_ListAggregateTriggerProcessingFunctions_ReturnsRegistryBackedCandidates()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N");
        var activeKey = $"admin-candidate-active-{suffix}";
        var inactiveKey = $"admin-candidate-inactive-{suffix}";
        var abstractFunctionRepo = new NpgsqlAbstractFunctionManifestRepository(cs);
        var runtime = CreateAdminRuntime(abstractFunctionRepo);

        try
        {
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(cs, activeKey, "aggregate_trigger_runtime", "candidate-scope", active: true);
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(cs, inactiveKey, "aggregate_trigger_runtime", "candidate-scope", active: false);

            var (data, error) = await runtime.ExecuteDataAsync(
                new OperationVector("admin", "manifest", "list_aggregate_trigger_processing_functions", null, "admin", null, null),
                default);

            Assert.Null(error);
            Assert.True(data.HasValue);
            var raw = data!.Value.GetRawText();
            Assert.Contains(activeKey, raw);
            Assert.DoesNotContain(inactiveKey, raw);
        }
        finally
        {
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, activeKey);
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, inactiveKey);
        }
    }

    private static AdminRuntime CreateAdminRuntime(IAbstractFunctionManifestRepository abstractFunctionRepo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            abstractFunctionManifestRepository: abstractFunctionRepo);
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
