using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimePromotionManifestTests
{
    private static readonly Guid AdminPackageId = new("00000000-0000-0000-0000-000000000020");
    private static readonly Guid AdminSchemaId = new("00000000-0000-0000-0000-000000000021");
    private const string AdminComponentId = "00000000-0000-0000-0000-000000000022";
    private const string AdminPlacementKey = "admin:context_token_registry:list";

    [Fact]
    public async Task PromotionList_ReturnsRowsWithMetadata()
    {
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(BuildPromotionDraft(Guid.NewGuid(), ValidPromotionMetadata()));

        var runtime = CreateRuntime(repo);
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "promotion_manifest", "list", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal(1, data.Value.GetArrayLength());
        Assert.Equal("campaign-alpha", data.Value[0].GetProperty("manifestKey").GetString());
    }

    [Fact]
    public async Task Validate_MissingDisclosureText_ReturnsBlockingError()
    {
        var manifestId = Guid.NewGuid();
        var metadata = ValidPromotionMetadata() with { DisclosureText = "" };
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(BuildPromotionDraft(manifestId, metadata));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "promotion_manifest", "validate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.False(data!.Value.GetProperty("valid").GetBoolean());
        Assert.Contains(
            data.Value.GetProperty("issues").EnumerateArray(),
            i => i.GetProperty("code").GetString() == "PROMOTION_DISCLOSURE_TEXT_REQUIRED");
    }

    [Fact]
    public async Task Validate_BrokenPackageRef_ReturnsBlockingError()
    {
        var manifestId = Guid.NewGuid();
        var metadata = ValidPromotionMetadata() with
        {
            TargetTopologyRefs =
            [
                new AdminPromotionManifestTargetRefDto(
                    "00000000-0000-0000-0000-000000000099",
                    AdminSchemaId.ToString(),
                    AdminComponentId),
            ],
        };
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(BuildPromotionDraft(manifestId, metadata));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "promotion_manifest", "validate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.False(data!.Value.GetProperty("valid").GetBoolean());
        Assert.Contains(
            data.Value.GetProperty("issues").EnumerateArray(),
            i => i.GetProperty("code").GetString() == "PROMOTION_PACKAGE_REF_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateDraft_PersistsPromotionMetadata()
    {
        var manifestId = Guid.NewGuid();
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(
            manifestId,
            null,
            ValidWiringTopology("admin", "admin", "promotion_manifest", "update_draft"),
            "draft",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new AdminPromotionManifestUpdateDraftRequestDto(
            manifestId.ToString(),
            "campaign-beta",
            "v2",
            "Updated disclosure text.",
            "notice",
            AdminPlacementKey,
            "banner_surface",
            "manual",
            null,
            [new AdminPromotionManifestTargetRefDto(
                AdminPackageId.ToString(),
                AdminSchemaId.ToString(),
                AdminComponentId)]));

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "promotion_manifest", "update_draft", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.Equal("campaign-beta", data!.Value.GetProperty("metadata").GetProperty("manifestKey").GetString());
    }

    [Fact]
    public async Task Promote_WithValidPromotionMetadataAndWiring_Succeeds()
    {
        var manifestId = Guid.NewGuid();
        var topology = ValidWiringTopology("admin", "admin", "promotion_manifest", "promote");
        topology = topology.Concat([PromotionManifestValidator.BuildEntry(ValidPromotionMetadata())]).ToList();
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, topology, "draft", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "promote", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("active", data.Value.GetProperty("status").GetString());
    }

    private static AdminPromotionManifestMetadataDto ValidPromotionMetadata() =>
        new(
            "campaign-alpha",
            "v1",
            "Sample disclosure text for promotion intent.",
            "info",
            AdminPlacementKey,
            "banner_surface",
            "manual",
            null,
            [new AdminPromotionManifestTargetRefDto(
                AdminPackageId.ToString(),
                AdminSchemaId.ToString(),
                AdminComponentId)]);

    private static ManifestDetailRecord BuildPromotionDraft(
        Guid manifestId,
        AdminPromotionManifestMetadataDto metadata) =>
        new(
            manifestId,
            null,
            [PromotionManifestValidator.BuildEntry(metadata)],
            "draft",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static IReadOnlyList<JsonElement> ValidWiringTopology(
        string role, string target, string layer, string action) =>
        ManifestTopologyValidator.BuildTopology(role, target, layer, action, "admin_runtime", null);

    private static AdminRuntime CreateRuntime(InMemoryManifestAdminRepository manifestRepo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new PromotionTestTopologyRepository();
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
            manifestRepo,
            null,
            topoRepo);
    }

    private sealed class PromotionTestTopologyRepository : TopologyRepository
    {
        public PromotionTestTopologyRepository()
            : base(NullLogger<TopologyRepository>.Instance, "test-double") { }

        public override Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
        {
            if (key == AdminPlacementKey)
            {
                return Task.FromResult<StructureMapRecord?>(new StructureMapRecord(
                    "00000000-0000-0000-0000-000000000030",
                    AdminPlacementKey,
                    AdminPackageId,
                    AdminSchemaId,
                    [AdminComponentId],
                    null));
            }

            return base.LoadStructureMapAsync(key, ct);
        }

        public override Task<PackageRecord?> LoadPackageAsync(Guid packageId, CancellationToken ct = default)
        {
            if (packageId == AdminPackageId)
            {
                return Task.FromResult<PackageRecord?>(new PackageRecord(
                    AdminPackageId, "admin_package", null, "{}"));
            }

            return base.LoadPackageAsync(packageId, ct);
        }

        public override Task<SchemaRecord?> LoadSchemaAsync(Guid schemaId, CancellationToken ct = default)
        {
            if (schemaId == AdminSchemaId)
            {
                return Task.FromResult<SchemaRecord?>(new SchemaRecord(
                    AdminSchemaId, "admin_schema", null, "{}"));
            }

            return base.LoadSchemaAsync(schemaId, ct);
        }

        public override Task<ComponentRecord?> LoadComponentAsync(Guid componentId, CancellationToken ct = default)
        {
            if (componentId.ToString() == AdminComponentId)
            {
                return Task.FromResult<ComponentRecord?>(new ComponentRecord(
                    componentId, "admin_component", "admin", "{}"));
            }

            return base.LoadComponentAsync(componentId, ct);
        }
    }
}
