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
    public async Task ExecuteDataAsync_PackageGenerate_ReturnsAllIssuedIds()
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

        var vector = new OperationVector("admin", "package_generator", "generate", null, "admin", JsonSerializer.SerializeToElement(new
        {
            bucketItemId = Guid.NewGuid().ToString(),
            routeKey = "admin:ui-builder"
        }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal(tensorId.ToString(), data.Value.GetProperty("tensorId").GetString());
        Assert.Equal(componentId.ToString(), data.Value.GetProperty("componentId").GetString());
        Assert.Equal(packageId.ToString(), data.Value.GetProperty("packageId").GetString());
        Assert.Equal(layoutId.ToString(), data.Value.GetProperty("layoutId").GetString());
        Assert.Equal(wiringId.ToString(), data.Value.GetProperty("wiringId").GetString());
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
    public async Task ExecuteDataAsync_PackagePromote_UsesGenerateContinuityAndReturnsIssuedIds()
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

        public StubUiTopologyRepository(PackageGenerateResult generateResult)
            : base(NullLogger<UiTopologyRepository>.Instance, "test-double")
        {
            _generateResult = generateResult;
        }

        public override Task<PackageGenerateResult> PromoteBucketItemAsync(Guid bucketItemId, string routeKey, CancellationToken ct = default)
            => Task.FromResult(_generateResult);
    }
}
