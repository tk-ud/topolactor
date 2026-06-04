using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimePackageGenerateTests
{
    [Fact]
    public async Task ExecuteDataAsync_PackageGenerate_TransitionsToPackagingWithoutIssuedIds()
    {
        var runtime = CreateRuntime(new StubUiTopologyRepository(new PackageGenerateResult(
            PackageGenerateCode.Success,
            null, null, null, null, null)));

        var vector = new OperationVector("admin", "package_generator", "generate", null, "admin", JsonSerializer.SerializeToElement(new
        {
            bucketItemId = Guid.NewGuid().ToString(),
            routeKey = "admin:ui-builder"
        }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("packaging", data.Value.GetProperty("status").GetString());
        Assert.False(data.Value.TryGetProperty("tensorId", out _));
        Assert.False(data.Value.TryGetProperty("componentId", out _));
        Assert.False(data.Value.TryGetProperty("packageId", out _));
        Assert.False(data.Value.TryGetProperty("layoutId", out _));
        Assert.False(data.Value.TryGetProperty("wiringId", out _));
    }

    [Fact]
    public async Task ExecuteDataAsync_PackageGenerate_MapsNotBucketedToExplicitError()
    {
        var runtime = CreateRuntime(new StubUiTopologyRepository(new PackageGenerateResult(
            PackageGenerateCode.NotBucketed,
            null, null, null, null, null,
            "NOT_BUCKETED",
            "bucket item is not in bucketed status")));

        var vector = new OperationVector("admin", "package_generator", "generate", null, "admin", JsonSerializer.SerializeToElement(new
        {
            bucketItemId = Guid.NewGuid().ToString(),
            routeKey = "admin:ui-builder"
        }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PACKAGE_NOT_BUCKETED", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_PackagePromote_FromPackaging_ReturnsIssuedIds()
    {
        var tensorId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();

        var runtime = CreateRuntime(new StubUiTopologyRepository(new PackageGenerateResult(
            PackageGenerateCode.Success,
            tensorId,
            componentId,
            packageId,
            layoutId,
            wiringId)));

        var vector = new OperationVector("admin", "package_generator", "promote", null, "admin", JsonSerializer.SerializeToElement(new
        {
            bucketItemId = Guid.NewGuid().ToString(),
            routeKey = "admin:ui-builder"
        }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal(componentId.ToString(), data.Value.GetProperty("componentId").GetString());
        Assert.Equal(packageId.ToString(), data.Value.GetProperty("packageId").GetString());
        Assert.Equal(layoutId.ToString(), data.Value.GetProperty("layoutId").GetString());
        Assert.Equal(wiringId.ToString(), data.Value.GetProperty("wiringId").GetString());
    }

    [Fact]
    public async Task ExecuteDataAsync_PackagePromote_FromBucketed_ReturnsExplicitError()
    {
        var runtime = CreateRuntime(new StubUiTopologyRepository(
            new PackageGenerateResult(PackageGenerateCode.Success, null, null, null, null, null),
            new PackageGenerateResult(
                PackageGenerateCode.NotBucketed,
                null, null, null, null, null,
                "NOT_PACKAGING",
                "bucket item is not in packaging status")));

        var vector = new OperationVector("admin", "package_generator", "promote", null, "admin", JsonSerializer.SerializeToElement(new
        {
            bucketItemId = Guid.NewGuid().ToString(),
            routeKey = "admin:ui-builder"
        }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PACKAGE_NOT_BUCKETED", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_PromotePackage_TwoBucketItems_ReturnsSinglePackageWithVectors()
    {
        var tensorId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var compId1 = Guid.NewGuid();
        var compId2 = Guid.NewGuid();
        var bucketId1 = Guid.NewGuid();
        var bucketId2 = Guid.NewGuid();

        var batchResult = new PackageGenerateBatchResult(
            PackageGenerateCode.Success,
            tensorId, packageId, layoutId, wiringId,
            [compId1, compId2],
            [bucketId1, bucketId2]);

        var runtime = CreateRuntime(new StubUiTopologyRepository(
            new PackageGenerateResult(PackageGenerateCode.Success, null, null, null, null, null),
            batchResult: batchResult));

        var vector = new OperationVector("admin", "package_generator", "promote_package", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                routeKey = "admin:ui-builder",
                bucketItemIds = new[] { bucketId1.ToString(), bucketId2.ToString() }
            }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal(packageId.ToString(), data.Value.GetProperty("packageId").GetString());
        Assert.Equal(layoutId.ToString(), data.Value.GetProperty("layoutId").GetString());
        Assert.Equal(wiringId.ToString(), data.Value.GetProperty("wiringId").GetString());
        var componentIds = data.Value.GetProperty("componentIds").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(2, componentIds.Count);
        Assert.Contains(compId1.ToString(), componentIds);
        Assert.Contains(compId2.ToString(), componentIds);
        var returnedBucketIds = data.Value.GetProperty("bucketItemIds").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(2, returnedBucketIds.Count);
        Assert.Contains(bucketId1.ToString(), returnedBucketIds);
        Assert.Contains(bucketId2.ToString(), returnedBucketIds);
    }

    [Fact]
    public async Task ExecuteDataAsync_PromotePackage_MissingRouteKey_ReturnsError()
    {
        var runtime = CreateRuntime(new StubUiTopologyRepository(
            new PackageGenerateResult(PackageGenerateCode.Success, null, null, null, null, null)));

        var vector = new OperationVector("admin", "package_generator", "promote_package", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                bucketItemIds = new[] { Guid.NewGuid().ToString() }
            }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ROUTE_KEY_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_PromotePackage_EmptyBucketItemIds_ReturnsError()
    {
        var runtime = CreateRuntime(new StubUiTopologyRepository(
            new PackageGenerateResult(PackageGenerateCode.Success, null, null, null, null, null)));

        var vector = new OperationVector("admin", "package_generator", "promote_package", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                routeKey = "admin:ui-builder",
                bucketItemIds = Array.Empty<string>()
            }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("BUCKET_ITEM_IDS_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_PromotePackage_InvalidUuidInBucketItemIds_ReturnsError()
    {
        var runtime = CreateRuntime(new StubUiTopologyRepository(
            new PackageGenerateResult(PackageGenerateCode.Success, null, null, null, null, null)));

        var vector = new OperationVector("admin", "package_generator", "promote_package", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                routeKey = "admin:ui-builder",
                bucketItemIds = new[] { "not-a-uuid" }
            }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("MALFORMED_BUCKET_ITEM_ID", error!.Code);
    }

    private static AdminRuntime CreateRuntime(UiTopologyRepository uiRepo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo, null);
    }

    private sealed class StubUiTopologyRepository : UiTopologyRepository
    {
        private readonly PackageGenerateResult _generateResult;
        private readonly PackageGenerateResult _promoteResult;
        private readonly PackageGenerateBatchResult? _batchResult;

        public StubUiTopologyRepository(
            PackageGenerateResult generateResult,
            PackageGenerateResult? promoteResult = null,
            PackageGenerateBatchResult? batchResult = null)
            : base(NullLogger<UiTopologyRepository>.Instance, "test-double")
        {
            _generateResult = generateResult;
            _promoteResult = promoteResult ?? generateResult;
            _batchResult = batchResult;
        }

        public override Task<PackageGenerateResult> GenerateFromBucketAsync(Guid bucketItemId, string routeKey, CancellationToken ct = default)
            => Task.FromResult(_generateResult);

        public override Task<PackageGenerateResult> PromoteBucketItemAsync(Guid bucketItemId, string routeKey, CancellationToken ct = default)
            => Task.FromResult(_promoteResult);

        public override Task<PackageGenerateBatchResult> PromotePackageFromBucketItemsAsync(
            string routeKey, IReadOnlyList<Guid> bucketItemIds, CancellationToken ct = default)
        {
            if (_batchResult is null)
                throw new InvalidOperationException("StubUiTopologyRepository: _batchResult not configured for batch promote.");
            return Task.FromResult(_batchResult);
        }
    }
}
