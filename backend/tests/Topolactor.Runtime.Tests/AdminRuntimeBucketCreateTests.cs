using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimeBucketCreateTests
{
    [Fact]
    public async Task ExecuteDataAsync_BucketCreate_MapsConstraintViolationToExplicitError()
    {
        var runtime = CreateRuntime(new StubUiTopologyRepository(UiComponentBucketCreateCode.ConstraintViolation));
        var vector = new OperationVector("admin", "ui_component_bucket", "create", null, "admin", JsonSerializer.SerializeToElement(new
        {
            componentKey = "button.primitive",
            sourcePath = "frontend/components/Button.tsx",
            componentKind = "primitive",
            metadataJson = "{}"
        }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("CONSTRAINT_VIOLATION", error!.Code);
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
        private readonly UiComponentBucketCreateCode _code;

        public StubUiTopologyRepository(UiComponentBucketCreateCode code)
            : base(NullLogger<UiTopologyRepository>.Instance, "test-double")
        {
            _code = code;
        }

        public override Task<UiComponentBucketCreateResult> CreateBucketItemAsync(string componentKey, string sourcePath, string componentKind, string? metadataJson = null, CancellationToken ct = default)
        {
            UiComponentBucketRecord? record = _code == UiComponentBucketCreateCode.Success
                ? new UiComponentBucketRecord(Guid.NewGuid(), componentKey, sourcePath, componentKind, "bucketed")
                : null;
            return Task.FromResult(new UiComponentBucketCreateResult(_code, record, _code.ToString(), _code.ToString()));
        }
    }
}
