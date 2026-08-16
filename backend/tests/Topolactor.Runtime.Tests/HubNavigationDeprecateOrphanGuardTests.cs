using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// production_projection_connectivity_invariant (docs/design/db-schema.yaml manifest_hub_chain,
/// docs/design/runtime-orchestration-ssot.yaml production_projection_connectivity_invariant):
/// every hubs.topology_manifests row must retain at least one active hub_relations row. The only
/// existing mutation that can reduce a topology_manifest's active hub_relations count is
/// hub_navigation:deprecate (hub_navigation:update replaces relatedHubId in place;
/// hub_navigation:reorder never changes status) — so this is the canonical fail-close boundary
/// for this invariant, not manifest promotion/authoring (which the existing
/// navigation_binding_authoring_and_verification sequencing note in
/// docs/design/admin-normal-surface-projection-seed-ssot.yaml explicitly keeps decoupled from
/// hub relation authoring).
/// </summary>
public class HubNavigationDeprecateOrphanGuardTests
{
    [Fact]
    public async Task Deprecate_LastActiveRelationForManifest_FailsClosed_ManifestKeepsItsRelation()
    {
        // The default InMemoryContentBundleRepository fixture seeds exactly ONE active
        // hub_relations row (FixtureHubRelationId) for FixtureTopologyManifestId.
        var repo = new InMemoryContentBundleRepository();
        var runtime = CreateRuntime(repo);

        var payload = JsonSerializer.SerializeToElement(new
        {
            hubRelationId = InMemoryContentBundleRepository.FixtureHubRelationId.ToString(),
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "deprecate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST", data.Value.GetProperty("errorCode").GetString());

        // The relation must still be active — the guard did not mutate state on rejection.
        var relations = await repo.ListHubRelationsByManifestAsync(InMemoryContentBundleRepository.FixtureTopologyManifestId);
        var relation = Assert.Single(relations, r => r.HubRelationId == InMemoryContentBundleRepository.FixtureHubRelationId.ToString());
        Assert.Equal("active", relation.Status);
    }

    [Fact]
    public async Task Deprecate_OneOfTwoActiveRelationsForManifest_Succeeds_ManifestStillHasAnActiveRelation()
    {
        var repo = new InMemoryContentBundleRepository();
        var secondRelationId = Guid.NewGuid();
        repo.AddHubRelation(
            secondRelationId,
            InMemoryContentBundleRepository.FixtureTopologyManifestId,
            InMemoryContentBundleRepository.FixtureRelatedHubId,
            2);
        var runtime = CreateRuntime(repo);

        var payload = JsonSerializer.SerializeToElement(new
        {
            hubRelationId = InMemoryContentBundleRepository.FixtureHubRelationId.ToString(),
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "deprecate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());

        var relations = await repo.ListHubRelationsByManifestAsync(InMemoryContentBundleRepository.FixtureTopologyManifestId);
        var deprecated = Assert.Single(relations, r => r.HubRelationId == InMemoryContentBundleRepository.FixtureHubRelationId.ToString());
        Assert.Equal("deprecated", deprecated.Status);
        var stillActive = Assert.Single(relations, r => r.HubRelationId == secondRelationId.ToString());
        Assert.Equal("active", stillActive.Status);
    }

    [Fact]
    public async Task Deprecate_LastActiveRelationForOneManifest_DoesNotBlockDeprecatingAnotherManifestsLastRelation_ScopedPerManifest()
    {
        // Guard must be scoped per topology_manifest_id, not a global "at least one relation
        // anywhere" check — a second, independent manifest's own last relation is a separate
        // orphan concern from the fixture manifest's.
        var repo = new InMemoryContentBundleRepository();
        var otherManifestId = Guid.NewGuid();
        var otherRelationId = Guid.NewGuid();
        var otherHubId = Guid.NewGuid();
        repo.AddHubRelation(otherRelationId, otherManifestId, otherHubId, 1);
        var runtime = CreateRuntime(repo);

        var payload = JsonSerializer.SerializeToElement(new { hubRelationId = otherRelationId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "deprecate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.False(
            data!.Value.GetProperty("ok").GetBoolean(),
            "otherManifestId's own last active relation must independently fail closed");
        Assert.Equal("HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST", data.Value.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Deprecate_UnknownOrAlreadyDeprecatedRelation_ReturnsNotFound_NotTheOrphanGuardCode()
    {
        var repo = new InMemoryContentBundleRepository();
        var runtime = CreateRuntime(repo);

        var payload = JsonSerializer.SerializeToElement(new { hubRelationId = Guid.NewGuid().ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "deprecate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("HUB_RELATION_NOT_FOUND", data.Value.GetProperty("errorCode").GetString());
    }

    private static AdminRuntime CreateRuntime(InMemoryContentBundleRepository? contentRepo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(
            NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            null,
            null,
            null,
            null,
            null,
            null,
            contentRepo);
    }
}
