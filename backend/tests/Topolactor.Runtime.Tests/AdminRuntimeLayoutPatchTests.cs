using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimeLayoutPatchTests
{
    [Fact]
    public async Task PromotedPalette_ReturnsEntriesWithIdentity()
    {
        var runtime = CreateRuntime(new StubUiRepo(true, [
            new PromotedPaletteEntryDto("Button", "primitive", Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "/admin/ui-builder")
        ]));
        var (data, error) = await runtime.ExecuteDataAsync(new OperationVector("admin", "ui_topology", "promoted_palette", null, "admin", null, null), default);
        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal(1, data.Value.GetArrayLength());
        Assert.True(data.Value[0].TryGetProperty("componentId", out _));
        Assert.True(data.Value[0].TryGetProperty("packageId", out _));
        Assert.True(data.Value[0].TryGetProperty("layoutId", out _));
        Assert.True(data.Value[0].TryGetProperty("wiringId", out _));
        Assert.True(data.Value[0].TryGetProperty("tensorId", out _));
    }

    [Fact]
    public async Task LayoutCandidates_ReturnsDistinctEntries()
    {
        var layoutId = Guid.NewGuid().ToString();
        var runtime = CreateRuntime(new StubUiRepo(true, palette: null, candidates: [
            new LayoutCandidateDto(layoutId, "/admin/ui-builder:button.primitive:layout", "/admin/ui-builder", "action/button", ["main", "header"])
        ]));
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "ui_topology", "layout_candidates", null, "admin", null, null), default);
        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal(1, data.Value.GetArrayLength());
        Assert.True(data.Value[0].TryGetProperty("layoutKey", out _));
        Assert.True(data.Value[0].TryGetProperty("slotKeys", out var slots));
        Assert.Equal(2, slots.GetArrayLength());
    }

    [Fact]
    public async Task LayoutCandidates_DbUnavailable_ReturnsExplicitError()
    {
        var runtime = CreateRuntime(new ThrowingUiRepo());
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "ui_topology", "layout_candidates", null, "admin", null, null), default);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("DB_UNAVAILABLE", error!.Code);
    }

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

    private sealed class StubUiRepo(
        bool valid,
        IReadOnlyList<PromotedPaletteEntryDto>? palette = null,
        IReadOnlyList<LayoutCandidateDto>? candidates = null) : UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "Host=localhost")
    {
        private readonly IReadOnlyList<PromotedPaletteEntryDto> _palette = palette ?? [];
        private readonly IReadOnlyList<LayoutCandidateDto> _candidates = candidates ?? [];
        public override Task<LayoutPatchResult> PreviewLayoutPatchAsync(Guid layoutId, string routeKey, string? tensorPatchJson, IReadOnlyList<string>? cssTokenRefs, IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs, CancellationToken ct = default)
            => Task.FromResult(new LayoutPatchResult(true, true, layoutId.ToString(), routeKey, "{}", [], new Dictionary<string, IReadOnlyList<string>>(), "ok"));
        public override Task<LayoutPatchResult> ValidateLayoutPatchAsync(Guid layoutId, string routeKey, string? tensorPatchJson, IReadOnlyList<string>? cssTokenRefs, IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs, CancellationToken ct = default)
            => Task.FromResult(new LayoutPatchResult(valid, valid, layoutId.ToString(), routeKey, "{}", [], new Dictionary<string, IReadOnlyList<string>>(), valid ? "ok" : "bad"));
        public override Task<LayoutPatchResult> ApplyConfirmedLayoutPatchAsync(Guid layoutId, string routeKey, string? tensorPatchJson, IReadOnlyList<string>? cssTokenRefs, IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs, CancellationToken ct = default)
            => Task.FromResult(new LayoutPatchResult(valid, valid, layoutId.ToString(), routeKey, "{}", [], new Dictionary<string, IReadOnlyList<string>>(), valid ? "ok" : "bad"));
        public override Task<IReadOnlyList<PromotedPaletteEntryDto>> ListPromotedPaletteEntriesAsync(CancellationToken ct = default)
            => Task.FromResult(_palette);
        public override Task<IReadOnlyList<LayoutCandidateDto>> ListLayoutCandidatesAsync(CancellationToken ct = default)
            => Task.FromResult(_candidates);
    }

    private sealed class ThrowingUiRepo : UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "Host=localhost")
    {
        public override Task<IReadOnlyList<LayoutCandidateDto>> ListLayoutCandidatesAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("connection refused");
        public override Task<IReadOnlyList<PromotedPaletteEntryDto>> ListPromotedPaletteEntriesAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("connection refused");
    }
}
