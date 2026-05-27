using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimeLayoutPatchTests
{
    [Fact]
    public async Task LayoutPatchValidate_InvalidToken_ReturnsExplicitError()
    {
        var runtime = CreateRuntime(new StubUiRepo(false));
        var (data, error) = await runtime.ExecuteDataAsync(new OperationVector("admin","layout_patch","validate",null,"admin",
            System.Text.Json.JsonSerializer.SerializeToElement(new { layoutId = Guid.NewGuid().ToString(), routeKey = "/admin/ui-builder" }), null), default);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("LAYOUT_PATCH_VALIDATION_FAILED", error!.Code);
    }

    [Fact]
    public async Task LayoutPatchApply_Valid_ReturnsData()
    {
        var runtime = CreateRuntime(new StubUiRepo(true));
        var (data, error) = await runtime.ExecuteDataAsync(new OperationVector("admin","layout_patch","apply",null,"admin",
            System.Text.Json.JsonSerializer.SerializeToElement(new { layoutId = Guid.NewGuid().ToString(), routeKey = "/admin/ui-builder" }), null), default);
        Assert.Null(error);
        Assert.True(data.HasValue);
    }

    private static AdminRuntime CreateRuntime(UiTopologyRepository uiRepo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo, null);
    }

    private sealed class StubUiRepo(bool valid) : UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "Host=localhost")
    {
        public override Task<LayoutPatchResult> PreviewLayoutPatchAsync(Guid layoutId, string routeKey, string? tensorPatchJson, IReadOnlyList<string>? cssTokenRefs, IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs, CancellationToken ct = default)
            => Task.FromResult(new LayoutPatchResult(true, true, layoutId.ToString(), routeKey, "{}", [], new Dictionary<string, IReadOnlyList<string>>(), "ok"));
        public override Task<LayoutPatchResult> ValidateLayoutPatchAsync(Guid layoutId, string routeKey, string? tensorPatchJson, IReadOnlyList<string>? cssTokenRefs, IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs, CancellationToken ct = default)
            => Task.FromResult(new LayoutPatchResult(valid, valid, layoutId.ToString(), routeKey, "{}", [], new Dictionary<string, IReadOnlyList<string>>(), valid ? "ok" : "bad"));
        public override Task<LayoutPatchResult> ApplyConfirmedLayoutPatchAsync(Guid layoutId, string routeKey, string? tensorPatchJson, IReadOnlyList<string>? cssTokenRefs, IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs, CancellationToken ct = default)
            => Task.FromResult(new LayoutPatchResult(valid, valid, layoutId.ToString(), routeKey, "{}", [], new Dictionary<string, IReadOnlyList<string>>(), valid ? "ok" : "bad"));
    }
}
