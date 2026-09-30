using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimeContentBundleTests
{
    [Fact]
    public async Task ListHubs_ReturnsSeededHubs()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "list_hubs", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.GetArrayLength() >= 1);
        Assert.Equal("hub", data.Value[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task ListEntities_ReturnsSeededEntities()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "list_entities", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.GetArrayLength() >= 1);
        Assert.Equal("entity", data.Value[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task ListRelations_ReturnsSeededRelations()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "list_relations", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.GetArrayLength() >= 1);
        Assert.Equal("relation", data.Value[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task GetEntity_ReturnsDetail()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            entityId = InMemoryContentBundleRepository.FixtureEntityAlphaId.ToString(),
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "get_entity", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal("Alpha Entity", data.Value.GetProperty("label").GetString());
    }

    [Fact]
    public async Task GetEntity_Missing_ReturnsNotFound()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new { entityId = Guid.NewGuid().ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "get_entity", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENTITY_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task CreateDraft_MissingHub_ReturnsBlockingError()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            hubId = Guid.NewGuid().ToString(),
            entityJsonb = new { label = "Test Entity", state = "active" },
            relationIds = new[] { InMemoryContentBundleRepository.FixtureRelationId.ToString() },
            stateName = "active",
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "create_entity_draft", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("HUB_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task CreateDraft_MissingRelation_ReturnsBlockingError()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            hubId = InMemoryContentBundleRepository.FixtureHubId.ToString(),
            entityJsonb = new { label = "Test Entity", state = "active" },
            relationIds = new[] { Guid.NewGuid().ToString() },
            stateName = "active",
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "create_entity_draft", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("RELATION_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task ValidateDraft_ValidDraft_Passes()
    {
        var repo = new InMemoryContentBundleRepository();
        var runtime = CreateRuntime(repo);
        var draftId = await CreateValidDraftAsync(runtime);

        var payload = JsonSerializer.SerializeToElement(new { draftId });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "validate_draft", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task PromoteDraft_ValidDraft_CreatesActiveEntity()
    {
        var repo = new InMemoryContentBundleRepository();
        var runtime = CreateRuntime(repo);
        var draftId = await CreateValidDraftAsync(runtime);

        var payload = JsonSerializer.SerializeToElement(new { draftId });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "promote_draft", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.NotNull(data.Value.GetProperty("entityId").GetString());
        Assert.NotNull(data.Value.GetProperty("readback").GetProperty("label").GetString());
    }

    [Fact]
    public async Task PromoteDraft_InvalidDraft_Fails()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new { draftId = Guid.NewGuid().ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "promote_draft", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("DRAFT_NOT_FOUND", data.Value.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task RepositoryUnavailable_ReturnsExplicitError()
    {
        var runtime = CreateRuntime(null);
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "list_hubs", null, "admin", null, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("CONTENT_BUNDLE_REPOSITORY_NOT_AVAILABLE", error!.Code);
    }

    [Fact]
    public async Task GetHub_ReturnsDetail()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            hubId = InMemoryContentBundleRepository.FixtureHubId.ToString(),
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "get_hub", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("entityCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task GetHub_Missing_ReturnsNotFound()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new { hubId = Guid.NewGuid().ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "get_hub", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.Equal("HUB_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task GetRelation_ReturnsDetail()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            relationRegistryId = InMemoryContentBundleRepository.FixtureRelationId.ToString(),
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "get_relation", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.Equal("fixture_relation", data!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListHubRelations_ReturnsItems()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "list_hub_relations", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetArrayLength() >= 1);
        Assert.Equal("hub_relation", data.Value[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task UpdateDraft_ValidDraft_Updates()
    {
        var repo = new InMemoryContentBundleRepository();
        var runtime = CreateRuntime(repo);
        var draftId = await CreateValidDraftAsync(runtime);

        var payload = JsonSerializer.SerializeToElement(new
        {
            draftId,
            hubId = InMemoryContentBundleRepository.FixtureHubId.ToString(),
            entityJsonb = new { label = "Updated Entity", state = "active" },
            relationIds = new[] { InMemoryContentBundleRepository.FixtureRelationId.ToString() },
            stateName = "active",
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "update_entity_draft", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.Contains("Updated Entity", data!.Value.GetProperty("entityJsonb").GetString());
    }

    [Fact]
    public async Task UpdateDraft_MissingHub_ReturnsBlockingError()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var draftId = await CreateValidDraftAsync(runtime);
        var payload = JsonSerializer.SerializeToElement(new
        {
            draftId,
            hubId = Guid.NewGuid().ToString(),
            entityJsonb = new { label = "Updated", state = "active" },
            relationIds = new[] { InMemoryContentBundleRepository.FixtureRelationId.ToString() },
            stateName = "active",
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "update_entity_draft", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.Equal("HUB_NOT_FOUND", error!.Code);
    }

    // Hub Navigation tests

    [Fact]
    public async Task HubNavigation_ListManifests_ReturnsSeededManifest()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "list_manifests", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.GetArrayLength() >= 1);
        var first = data.Value[0];
        Assert.Equal(InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
            first.GetProperty("topologyManifestId").GetString());
        Assert.True(first.GetProperty("hasHubRelations").GetBoolean());
    }

    [Fact]
    public async Task HubNavigation_GetHubRelations_ReturnsSeededEntry()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "get_hub_relations", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.GetArrayLength() >= 1);
        Assert.Equal(1, data.Value[0].GetProperty("sequencePosition").GetInt32());
    }

    [Fact]
    public async Task HubNavigation_Create_AddsNewEntry()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
            targetTopologyManifestId = InMemoryContentBundleRepository.FixtureRelatedHubManifestId.ToString(),
            sequencePosition = 99,
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "create", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("active", data.Value.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HubNavigation_Create_SequenceConflict_ReturnsError()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
            targetTopologyManifestId = InMemoryContentBundleRepository.FixtureRelatedHubManifestId.ToString(),
            sequencePosition = 1, // already exists in seed
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "create", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.False(data.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("SEQUENCE_CONFLICT", data.Value.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task HubNavigation_Update_RepointsTargetManifest_AndDerivesRelatedHubMirror()
    {
        var repo = new InMemoryContentBundleRepository();
        var newTargetManifestId = Guid.NewGuid();
        var newTargetHubId = Guid.NewGuid();
        repo.AddTopologyManifestHub(newTargetManifestId, newTargetHubId);
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            hubRelationId = InMemoryContentBundleRepository.FixtureHubRelationId.ToString(),
            targetTopologyManifestId = newTargetManifestId.ToString(),
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "update", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("active", data.Value.GetProperty("status").GetString());

        // write_path_legacy_mirror_contract: the canonical target is what the caller selected, and
        // related_hub_id is that target manifest's own hub_id -- derived, never a caller input.
        var relation = Assert.Single(
            await repo.ListHubRelationsByManifestAsync(InMemoryContentBundleRepository.FixtureTopologyManifestId),
            r => r.HubRelationId == InMemoryContentBundleRepository.FixtureHubRelationId.ToString());
        Assert.Equal(newTargetManifestId.ToString(), relation.TargetTopologyManifestId);
        Assert.Equal(newTargetHubId.ToString(), relation.RelatedHubId);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    public async Task HubNavigation_CreateOrUpdate_CallerSuppliedRelatedHubId_IsRejected_NoDualTargetAuthority(string action)
    {
        // related_hub_id is derived from targetTopologyManifestId; a caller-supplied relatedHubId would
        // be a second, independently-selected target authority, so it fails closed explicitly (never
        // silently ignored), even alongside a valid targetTopologyManifestId.
        var repo = new InMemoryContentBundleRepository();
        var runtime = CreateRuntime(repo);
        var payload = action == "create"
            ? JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
                targetTopologyManifestId = InMemoryContentBundleRepository.FixtureRelatedHubManifestId.ToString(),
                relatedHubId = InMemoryContentBundleRepository.FixtureRelatedHubId.ToString(),
                sequencePosition = 50,
            })
            : JsonSerializer.SerializeToElement(new
            {
                hubRelationId = InMemoryContentBundleRepository.FixtureHubRelationId.ToString(),
                targetTopologyManifestId = InMemoryContentBundleRepository.FixtureRelatedHubManifestId.ToString(),
                relatedHubId = InMemoryContentBundleRepository.FixtureRelatedHubId.ToString(),
            });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", action, null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("MALFORMED_PAYLOAD", error!.Code);
        Assert.Single(await repo.ListHubRelationsByManifestAsync(InMemoryContentBundleRepository.FixtureTopologyManifestId));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    public async Task HubNavigation_CreateOrUpdate_MissingTargetTopologyManifestId_IsMalformed(string action)
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = action == "create"
            ? JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
                sequencePosition = 50,
            })
            : JsonSerializer.SerializeToElement(new
            {
                hubRelationId = InMemoryContentBundleRepository.FixtureHubRelationId.ToString(),
            });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", action, null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.Equal("MALFORMED_PAYLOAD", error!.Code);
    }

    [Fact]
    public async Task HubNavigation_Create_SelfLoop_ReturnsSelfLoopError()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
            // the source manifest itself: its hub_id equals the source hub
            targetTopologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
            sequencePosition = 2,
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "create", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.False(data.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("SELF_LOOP", data.Value.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task HubNavigation_Update_SelfLoop_ReturnsSelfLoopError()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            hubRelationId = InMemoryContentBundleRepository.FixtureHubRelationId.ToString(),
            // the source manifest itself: its hub_id equals the source hub
            targetTopologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "update", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.False(data.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("SELF_LOOP", data.Value.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task HubNavigation_Create_DifferentManifestSharingSourceHub_IsStillSelfLoop_HubIdentityGuardUnchanged()
    {
        // self_loop_authoring_guard_relationship: the guard stays a Hub-identity comparison. A target
        // manifest that is NOT the source manifest but shares its hub_id is rejected too -- the guard
        // is not narrowed to Manifest identity by the target_topology_manifest_id migration.
        var repo = new InMemoryContentBundleRepository();
        var siblingManifestId = Guid.NewGuid();
        repo.AddTopologyManifestHub(siblingManifestId, InMemoryContentBundleRepository.FixtureHubId);
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
            targetTopologyManifestId = siblingManifestId.ToString(),
            sequencePosition = 2,
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "create", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("SELF_LOOP", data.Value.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task HubNavigation_Reorder_ChangesSequencePositions()
    {
        var repo = new InMemoryContentBundleRepository();
        var runtime = CreateRuntime(repo);

        // Add a second hub_relation to have something to swap
        var createPayload = JsonSerializer.SerializeToElement(new
        {
            topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
            targetTopologyManifestId = InMemoryContentBundleRepository.FixtureRelatedHubManifestId.ToString(),
            sequencePosition = 2,
        });
        await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "create", null, "admin", createPayload, null), default);

        var listPayload = JsonSerializer.SerializeToElement(new
        {
            topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
        });
        var (listData, _) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "get_hub_relations", null, "admin", listPayload, null), default);
        var relations = listData!.Value.EnumerateArray()
            .Select(e => new { id = e.GetProperty("hubRelationId").GetString()!, seq = e.GetProperty("sequencePosition").GetInt32() })
            .OrderBy(r => r.seq).ToList();

        // Swap positions
        var reorderPayload = JsonSerializer.SerializeToElement(new
        {
            topologyManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId.ToString(),
            items = new[]
            {
                new { hubRelationId = relations[0].id, newSequencePosition = relations[1].seq },
                new { hubRelationId = relations[1].id, newSequencePosition = relations[0].seq },
            },
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "reorder", null, "admin", reorderPayload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task HubNavigation_Update_UnknownTargetManifest_ReturnsTargetManifestNotFound()
    {
        var runtime = CreateRuntime(new InMemoryContentBundleRepository());
        var payload = JsonSerializer.SerializeToElement(new
        {
            hubRelationId = InMemoryContentBundleRepository.FixtureHubRelationId.ToString(),
            targetTopologyManifestId = Guid.NewGuid().ToString(),
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "hub_navigation", "update", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.False(data.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("TARGET_MANIFEST_NOT_FOUND", data.Value.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task HubNavigation_Deprecate_SetsDeprecatedStatus()
    {
        // production_projection_connectivity_invariant (docs/design/db-schema.yaml
        // hub_relations.minimum_cardinality_completion_invariant): deprecating a topology_manifest's
        // ONLY active hub_relations row now fails closed (see
        // HubNavigationDeprecateOrphanGuardTests for that behavior) — a second active relation is
        // seeded here first so this test can still exercise the ordinary success path.
        var repo = new InMemoryContentBundleRepository();
        repo.AddHubRelation(
            Guid.NewGuid(),
            InMemoryContentBundleRepository.FixtureTopologyManifestId,
            InMemoryContentBundleRepository.FixtureRelatedHubManifestId,
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
        Assert.True(data.HasValue);
        Assert.True(data.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("deprecated", data.Value.GetProperty("status").GetString());
    }

    private static async Task<string> CreateValidDraftAsync(AdminRuntime runtime)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            hubId = InMemoryContentBundleRepository.FixtureHubId.ToString(),
            entityJsonb = new { label = "New Test Entity", state = "active" },
            relationIds = new[] { InMemoryContentBundleRepository.FixtureRelationId.ToString() },
            stateName = "active",
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "content_bundle", "create_entity_draft", null, "admin", payload, null), default);
        Assert.Null(error);
        return data!.Value.GetProperty("draftId").GetString()!;
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
