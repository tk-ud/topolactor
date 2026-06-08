using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for AdminRuntime layer=mock_preset actions.
/// SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml
///
/// Verifies:
///   - create/list/get/compile/bind happy paths and explicit error paths
///   - bind does NOT write to topology.ui_topology_tensor (data payload only)
///   - all failures return explicit error codes (no silent fallback)
///   - unconfigured repository returns MOCK_PRESET_NOT_CONFIGURED
/// </summary>
public class AdminRuntimeMockPresetTests
{
    // ─── create ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MockPresetCreate_ValidPayload_ReturnsPresetId()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = MakeCreatePayload("my_preset", "My Preset", "svg_xml");

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "create", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.TryGetProperty("ok", out var ok));
        Assert.True(ok.GetBoolean());
        Assert.True(data.Value.TryGetProperty("presetId", out _));
    }

    [Fact]
    public async Task MockPresetCreate_MissingPresetKey_ReturnsPresetKeyRequired()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetLabel = "My Preset",
            sourceKind = "svg_xml",
            sourceHash = "abc",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "create", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PRESET_KEY_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task MockPresetCreate_EmptyPresetKey_ReturnsPresetKeyRequired()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = MakeCreatePayload("   ", "My Preset", "svg_xml");

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "create", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PRESET_KEY_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task MockPresetCreate_MissingPresetLabel_ReturnsPresetLabelRequired()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetKey = "my_preset",
            sourceKind = "svg_xml",
            sourceHash = "abc",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "create", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PRESET_LABEL_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task MockPresetCreate_InvalidSourceKind_ReturnsSourceKindInvalid()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = MakeCreatePayload("my_preset", "My Preset", "unknown_format");

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "create", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SOURCE_KIND_INVALID", error!.Code);
    }

    [Fact]
    public async Task MockPresetCreate_ValidSourceKinds_AllAccepted()
    {
        var validKinds = new[] { "svg_xml", "generic_xml", "figma_json", "ui_builder_canvas" };
        foreach (var kind in validKinds)
        {
            var repo = new StubMockPresetRepo();
            var runtime = CreateRuntime(repo);
            var payload = MakeCreatePayload($"preset_{kind}", $"Preset {kind}", kind);

            var (data, error) = await runtime.ExecuteDataAsync(
                new OperationVector("admin", "mock_preset", "create", null, "admin", payload, null), default);

            Assert.Null(error);
            Assert.True(data.HasValue, $"Expected ok for sourceKind={kind}");
        }
    }

    [Fact]
    public async Task MockPresetCreate_NotConfigured_ReturnsMockPresetNotConfigured()
    {
        var runtime = CreateRuntimeNoPresetRepo();
        var payload = MakeCreatePayload("my_preset", "My Preset", "svg_xml");

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "create", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("MOCK_PRESET_NOT_CONFIGURED", error!.Code);
    }

    // ─── list ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MockPresetList_ReturnsListFromRepository()
    {
        var presetId = Guid.NewGuid().ToString();
        var repo = new StubMockPresetRepo(presets: [
            new MockPresetListItem(presetId, "preset_a", "Preset A", "svg_xml", "active", "2025-01-01T00:00:00Z"),
        ]);
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "list", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal(1, data.Value.GetArrayLength());
        Assert.True(data.Value[0].TryGetProperty("presetId", out _));
        Assert.True(data.Value[0].TryGetProperty("presetKey", out _));
        Assert.True(data.Value[0].TryGetProperty("sourceKind", out _));
    }

    [Fact]
    public async Task MockPresetList_NotConfigured_ReturnsMockPresetNotConfigured()
    {
        var runtime = CreateRuntimeNoPresetRepo();

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "list", null, "admin", null, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("MOCK_PRESET_NOT_CONFIGURED", error!.Code);
    }

    // ─── get ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MockPresetGet_MissingIdOrHubId_ReturnsPresetIdRequired()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "get", null, "admin", null, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PRESET_ID_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task MockPresetGet_PresetNotFound_ReturnsPresetNotFound()
    {
        var repo = new StubMockPresetRepo(detail: null);
        var runtime = CreateRuntime(repo);
        var unknownId = Guid.NewGuid();

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "get", null, "admin", null, null, IdOrHubId: unknownId), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PRESET_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task MockPresetGet_ValidId_ReturnsDetail()
    {
        var presetId = Guid.NewGuid();
        var detail = MakeDetail(presetId.ToString(), "preset_a", "Preset A");
        var repo = new StubMockPresetRepo(detail: detail);
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "get", null, "admin", null, null, IdOrHubId: presetId), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
    }

    // ─── compile ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MockPresetCompile_MissingId_ReturnsPresetIdRequired()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "compile", null, "admin", null, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PRESET_ID_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task MockPresetCompile_PresetNotFound_ReturnsPresetNotFound()
    {
        var repo = new StubMockPresetRepo(detail: null);
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "compile", null, "admin", null, null, IdOrHubId: Guid.NewGuid()), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PRESET_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task MockPresetCompile_MappedNodes_ProducesLayoutPatchJson()
    {
        var presetId = Guid.NewGuid();
        var bbox = JsonSerializer.SerializeToElement(new { x = 10, y = 20, width = 140, height = 60 });
        var detail = MakeDetail(presetId.ToString(), "preset_a", "Preset A",
            mappings: [
                new MockPresetObjectMappingRecord(
                    Guid.NewGuid().ToString(), "rect_1", "preset_node_rect_1",
                    "catalog_component", "button.primitive", "action/button",
                    null, null, null, 0,
                    bbox, JsonSerializer.SerializeToElement(new { }),
                    JsonSerializer.SerializeToElement(new { }), "mapped")
            ]);
        var repo = new StubMockPresetRepo(detail: detail);
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "compile", null, "admin", null, null, IdOrHubId: presetId), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.TryGetProperty("ok", out var ok));
        Assert.True(ok.GetBoolean());
        Assert.True(data.Value.TryGetProperty("layoutPatchJson", out var lpj));
        Assert.True(lpj.TryGetProperty("nodes", out var nodes));
        Assert.Equal(1, nodes.GetArrayLength());
        // Must have VisualNodePayload-compatible fields
        var node = nodes[0];
        Assert.True(node.TryGetProperty("nodeId", out _));
        Assert.True(node.TryGetProperty("nodeKind", out _));
        Assert.True(node.TryGetProperty("componentKey", out _));
        Assert.True(node.TryGetProperty("x", out _));
        Assert.True(node.TryGetProperty("y", out _));
        Assert.True(node.TryGetProperty("width", out _));
        Assert.True(node.TryGetProperty("height", out _));
    }

    [Fact]
    public async Task MockPresetCompile_UnresolvedNodes_GoToUnresolvedJson()
    {
        var presetId = Guid.NewGuid();
        var bbox = JsonSerializer.SerializeToElement(new { x = 0, y = 0, width = 50, height = 50 });
        var detail = MakeDetail(presetId.ToString(), "preset_b", "Preset B",
            mappings: [
                new MockPresetObjectMappingRecord(
                    Guid.NewGuid().ToString(), "unknown_shape", "preset_node_unknown_shape",
                    "catalog_component", null, null,
                    null, null, null, 0,
                    bbox, JsonSerializer.SerializeToElement(new { }),
                    JsonSerializer.SerializeToElement(new { }), "unassigned")
            ]);
        var repo = new StubMockPresetRepo(detail: detail);
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "compile", null, "admin", null, null, IdOrHubId: presetId), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.TryGetProperty("layoutPatchJson", out var lpj));
        Assert.True(lpj.TryGetProperty("nodes", out var nodes));
        Assert.Equal(0, nodes.GetArrayLength());
        Assert.True(data.Value.TryGetProperty("unresolvedJson", out var unresolved));
        Assert.Equal(1, unresolved.GetArrayLength());
    }

    // ─── bind ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MockPresetBind_MissingRouteKey_ReturnsRouteKeyNotSelected()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { presetId = Guid.NewGuid().ToString() });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "bind", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ROUTE_KEY_NOT_SELECTED", error!.Code);
    }

    [Fact]
    public async Task MockPresetBind_EmptyRouteKey_ReturnsRouteKeyNotSelected()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetId = Guid.NewGuid().ToString(),
            routeKey = "",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "bind", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ROUTE_KEY_NOT_SELECTED", error!.Code);
    }

    [Fact]
    public async Task MockPresetBind_MissingPresetId_ReturnsPresetIdRequired()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { routeKey = "/admin/ui-builder" });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "bind", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PRESET_ID_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task MockPresetBind_WithExistingSnapshot_ReturnsLayoutPatchJson()
    {
        var presetId = Guid.NewGuid();
        var lpj = JsonSerializer.SerializeToElement(new { nodes = new[] { new { nodeId = "n1" } }, layoutClassRefs = Array.Empty<string>() });
        var bindResult = new MockPresetBindResult(
            Ok: true,
            LayoutPatchJson: lpj,
            PackageMembershipCandidateJson: JsonSerializer.SerializeToElement(new { }),
            WiringCandidateJson: JsonSerializer.SerializeToElement(Array.Empty<object>()),
            StyleCandidateJson: JsonSerializer.SerializeToElement(Array.Empty<object>()),
            UnresolvedJson: JsonSerializer.SerializeToElement(Array.Empty<object>())
        );
        var repo = new StubMockPresetRepo(latestSnapshot: bindResult);
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetId = presetId.ToString(),
            routeKey = "/admin/ui-builder",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "bind", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.TryGetProperty("layoutPatchJson", out _));
    }

    [Fact]
    public async Task MockPresetBind_DoesNotContainTopologyTensorFields()
    {
        // Bind result must NOT contain canonical topology entity fields.
        // Per SSOT §bind_to_canvas_contract.not_to: bind does not write to topology.ui_topology_tensor.
        var presetId = Guid.NewGuid();
        var lpj = JsonSerializer.SerializeToElement(new { nodes = Array.Empty<object>(), layoutClassRefs = Array.Empty<string>() });
        var bindResult = new MockPresetBindResult(
            Ok: true,
            LayoutPatchJson: lpj,
            PackageMembershipCandidateJson: JsonSerializer.SerializeToElement(new { }),
            WiringCandidateJson: JsonSerializer.SerializeToElement(Array.Empty<object>()),
            StyleCandidateJson: JsonSerializer.SerializeToElement(Array.Empty<object>()),
            UnresolvedJson: JsonSerializer.SerializeToElement(Array.Empty<object>())
        );
        var repo = new StubMockPresetRepo(latestSnapshot: bindResult);
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetId = presetId.ToString(),
            routeKey = "/admin/ui-builder",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "bind", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        // Must NOT contain topology tensor / canonical entity fields
        Assert.False(data.Value.TryGetProperty("tensor_id", out _), "bind result must not have tensor_id");
        Assert.False(data.Value.TryGetProperty("package_id", out _), "bind result must not have package_id");
        Assert.False(data.Value.TryGetProperty("layout_id", out _), "bind result must not have layout_id");
        Assert.False(data.Value.TryGetProperty("wiring_id", out _), "bind result must not have wiring_id");
        Assert.False(data.Value.TryGetProperty("topology_manifest_id", out _), "bind result must not have topology_manifest_id");
        // Must contain layout_patch_json (tmp canvas state)
        Assert.True(data.Value.TryGetProperty("layoutPatchJson", out _), "bind result must contain layoutPatchJson");
    }

    [Fact]
    public async Task MockPresetBind_NotConfigured_ReturnsMockPresetNotConfigured()
    {
        var runtime = CreateRuntimeNoPresetRepo();
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetId = Guid.NewGuid().ToString(),
            routeKey = "/admin/ui-builder",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "bind", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("MOCK_PRESET_NOT_CONFIGURED", error!.Code);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private static JsonElement MakeCreatePayload(string presetKey, string presetLabel, string sourceKind)
        => JsonSerializer.SerializeToElement(new
        {
            presetKey,
            presetLabel,
            sourceKind,
            sourceHash = "testhash",
            sourceSnapshotJson = new { raw = "<svg/>" },
            visualTreeJson = new { nodes = Array.Empty<object>() },
        });

    private static MockPresetDetail MakeDetail(
        string presetId,
        string presetKey,
        string presetLabel,
        IReadOnlyList<MockPresetObjectMappingRecord>? mappings = null)
        => new MockPresetDetail(
            PresetId: presetId,
            PresetKey: presetKey,
            PresetLabel: presetLabel,
            SourceKind: "svg_xml",
            SourceHash: "testhash",
            VisualTreeJson: JsonSerializer.SerializeToElement(new { nodes = Array.Empty<object>() }),
            Status: "active",
            ObjectMappings: mappings ?? [],
            WiringCandidates: []);

    private static AdminRuntime CreateRuntime(MockPresetRepository presetRepo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new StubUiRepoForPreset();
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            mockPresetRepository: presetRepo);
    }

    private static AdminRuntime CreateRuntimeNoPresetRepo()
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new StubUiRepoForPreset();
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            mockPresetRepository: null);
    }

    // ─── save_mappings ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MockPresetSaveMappings_ValidPayload_ReturnsOkWithCounts()
    {
        var presetId = Guid.NewGuid();
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetId = presetId.ToString(),
            mappings = new[]
            {
                new
                {
                    sourceObjectId = "rect_1",
                    nodeId = "preset_node_rect_1",
                    nodeKind = "catalog_component",
                    componentKey = "button.primitive",
                    componentKind = "action/button",
                    htmlTag = (string?)null,
                    parentSourceObjectId = (string?)null,
                    slotKey = (string?)null,
                    orderIndex = 0,
                    bboxJson = new { x = 0, y = 0, width = 100, height = 40 },
                    textJson = new { },
                    styleCandidateJson = Array.Empty<object>(),
                    mappingStatus = "mapped",
                },
            },
            wiringCandidates = Array.Empty<object>(),
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "save_mappings", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.TryGetProperty("ok", out var ok));
        Assert.True(ok.GetBoolean());
        Assert.True(data.Value.TryGetProperty("savedCount", out var saved));
        Assert.Equal(1, saved.GetInt32());
    }

    [Fact]
    public async Task MockPresetSaveMappings_WithWiringCandidates_SavesBoth()
    {
        var presetId = Guid.NewGuid();
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetId = presetId.ToString(),
            mappings = new[]
            {
                new
                {
                    sourceObjectId = "btn_1",
                    nodeId = "preset_node_btn_1",
                    nodeKind = "catalog_component",
                    componentKey = "button.primitive",
                    componentKind = "action/button",
                    orderIndex = 0,
                    bboxJson = new { },
                    textJson = new { },
                    styleCandidateJson = Array.Empty<object>(),
                    mappingStatus = "mapped",
                },
            },
            wiringCandidates = new[]
            {
                new
                {
                    sourceObjectId = "btn_1",
                    nodeId = "preset_node_btn_1",
                    capabilityTag = "emits_event",
                    wiringKind = "route_navigation",
                    targetSurface = (string?)null,
                    targetRef = "route:my_route_key",
                    bindingJson = new { },
                    status = "confirmed",
                },
            },
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "save_mappings", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.TryGetProperty("ok", out var ok));
        Assert.True(ok.GetBoolean());
        Assert.True(data.Value.TryGetProperty("savedCount", out var saved));
        Assert.Equal(1, saved.GetInt32());
        Assert.True(data.Value.TryGetProperty("wiringCandidatesSavedCount", out var wcs));
        Assert.Equal(1, wcs.GetInt32());
    }

    [Fact]
    public async Task MockPresetSaveMappings_MissingPresetId_ReturnsPresetIdRequired()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            mappings = Array.Empty<object>(),
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "save_mappings", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PRESET_ID_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task MockPresetSaveMappings_MissingMappingsArray_ReturnsMappingsRequired()
    {
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetId = Guid.NewGuid().ToString(),
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "save_mappings", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("MAPPINGS_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task MockPresetSaveMappings_NotConfigured_ReturnsMockPresetNotConfigured()
    {
        var runtime = CreateRuntimeNoPresetRepo();
        var payload = JsonSerializer.SerializeToElement(new
        {
            presetId = Guid.NewGuid().ToString(),
            mappings = Array.Empty<object>(),
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "save_mappings", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("MOCK_PRESET_NOT_CONFIGURED", error!.Code);
    }

    [Fact]
    public async Task MockPresetSaveMappings_UiBuilderCanvasSourceKind_AcceptedByCreate()
    {
        // source_kind=ui_builder_canvas must be valid (used by save-canvas-as-preset flow).
        var repo = new StubMockPresetRepo();
        var runtime = CreateRuntime(repo);
        var payload = MakeCreatePayload("canvas_preset", "Canvas Preset", "ui_builder_canvas");

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "mock_preset", "create", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(data.Value.TryGetProperty("ok", out var ok));
        Assert.True(ok.GetBoolean());
    }

    // ─── stub repositories ────────────────────────────────────────────────────────

    private class StubMockPresetRepo(
        IReadOnlyList<MockPresetListItem>? presets = null,
        MockPresetDetail? detail = null,
        MockPresetBindResult? latestSnapshot = null)
        : MockPresetRepository(NullLogger<MockPresetRepository>.Instance)
    {
        private static readonly string _stubPresetId = Guid.NewGuid().ToString();

        public override Task<(string? PresetId, string? ErrorCode, string? Message)>
            CreatePresetAsync(MockPresetCreateRequest request, CancellationToken ct = default)
            => Task.FromResult<(string?, string?, string?)>((_stubPresetId, null, null));

        public override Task<IReadOnlyList<MockPresetListItem>>
            ListPresetsAsync(string status = "active", CancellationToken ct = default)
            => Task.FromResult(presets ?? (IReadOnlyList<MockPresetListItem>)[]);

        public override Task<MockPresetDetail?>
            GetPresetAsync(Guid presetId, CancellationToken ct = default)
            => Task.FromResult(detail);

        public override Task<(string? MappingId, string? ErrorCode)>
            UpsertObjectMappingAsync(Guid presetId, MockPresetObjectMappingRecord mapping, CancellationToken ct = default)
            => Task.FromResult<(string?, string?)>((_stubPresetId, null));

        public override Task<(string? WiringCandidateId, string? ErrorCode)>
            UpsertWiringCandidateAsync(Guid presetId, MockPresetWiringCandidateRecord candidate, CancellationToken ct = default)
            => Task.FromResult<(string?, string?)>((_stubPresetId, null));

        public override Task<(string? CompileSnapshotId, string? ErrorCode)>
            SaveCompileSnapshotAsync(Guid presetId, MockPresetCompileResult result, CancellationToken ct = default)
            => Task.FromResult<(string?, string?)>((_stubPresetId, null));

        public override Task<MockPresetBindResult?>
            GetLatestCompileSnapshotAsync(Guid presetId, CancellationToken ct = default)
            => Task.FromResult(latestSnapshot);
    }

    private class StubUiRepoForPreset()
        : UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "Host=localhost")
    {
        // Minimal stub; mock preset tests don't exercise UiTopologyRepository methods.
    }
}
