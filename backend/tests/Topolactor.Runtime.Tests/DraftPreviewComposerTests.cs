using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class DraftPreviewComposerTests
{
    [Fact]
    public async Task ComposeAsync_LayoutOnly_SucceedsWithoutManifestRows()
    {
        var layoutId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var topo = new StubDraftPreviewTopologyRepo(layoutId, [
            new LayoutNodeRecord(
                NodeId: "node-main",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: "card.primitive",
                ComponentId: Guid.NewGuid().ToString(),
                ParentNodeId: null,
                SlotKey: "main",
                OrderIndex: 0,
                X: 0, Y: 0, Width: 200, Height: 100,
                LayoutClassRefs: ["layout.section"]),
        ]);
        var ui = new StubDraftPreviewUiRepo(layoutId, packageId, "employees", ["layout.root"]);
        var manifest = new StubDraftPreviewManifestRepo();

        var (success, error, statusCode) = await DraftPreviewComposer.ComposeAsync(
            layoutId.ToString(),
            topo,
            manifest,
            ui);

        Assert.Null(error);
        Assert.Equal(200, statusCode);
        Assert.NotNull(success);
        Assert.Equal("layout_only", success!.PreviewMode);
        Assert.Equal("employees", success.RouteKey);
        Assert.Single(success.LayoutNodes);
        Assert.Equal(["layout.root"], success.RootLayoutClassRefs);
        Assert.Empty(success.InitialDataRows);
    }

    [Fact]
    public async Task ComposeAsync_ResolvesInitialDataRowsFromDraftManifest()
    {
        var layoutId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var topo = new StubDraftPreviewTopologyRepo(layoutId, [
            new LayoutNodeRecord(
                NodeId: "node-main",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: "input.primitive",
                ComponentId: Guid.NewGuid().ToString(),
                ParentNodeId: null,
                SlotKey: "name",
                OrderIndex: 0,
                X: 0, Y: 0, Width: 200, Height: 40,
                LayoutClassRefs: null),
        ]);
        var ui = new StubDraftPreviewUiRepo(layoutId, Guid.NewGuid(), "employees", []);
        var shapeJson = JsonSerializer.Serialize(new
        {
            type = ManifestCanonicalProjection.ScreenDataShapeEntryType,
            topologySystemName = "employees",
            initialDataRows = new[]
            {
                new { values = new Dictionary<string, string> { ["name"] = "Alice" } },
            },
        });
        var manifest = new StubDraftPreviewManifestRepo([
            new ManifestDetailRecord(
                manifestId,
                null,
                [JsonSerializer.Deserialize<JsonElement>(shapeJson)],
                "draft",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
        ]);

        var (success, error, _) = await DraftPreviewComposer.ComposeAsync(
            layoutId.ToString(),
            topo,
            manifest,
            ui);

        Assert.Null(error);
        Assert.NotNull(success);
        Assert.Equal("layout_and_topology_intent", success!.PreviewMode);
        Assert.Equal(manifestId.ToString(), success.ManifestId);
        Assert.Single(success.InitialDataRows);
        Assert.Equal("Alice", success.InitialDataRows[0]["name"]);
    }

    [Fact]
    public async Task ComposeAsync_DesignProjection_MergesInlineTextAndCssTokenRefs()
    {
        var layoutId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        const string nodeId = "node-card";
        var topo = new StubDraftPreviewTopologyRepo(layoutId, [
            new LayoutNodeRecord(
                NodeId: nodeId,
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: "card.primitive",
                ComponentId: Guid.NewGuid().ToString(),
                ParentNodeId: null,
                SlotKey: "main",
                OrderIndex: 0,
                X: 0, Y: 0, Width: 200, Height: 100,
                LayoutClassRefs: ["layout.card"]),
        ]);
        var ui = new StubDraftPreviewUiRepo(layoutId, packageId, "employees", [], [
            new ComponentStyleDesignListItemDto(
                DesignId: Guid.NewGuid().ToString(),
                Name: "card-design",
                ComponentId: null,
                LayoutNodeId: nodeId,
                CssTokenRefs: ["color.primary"],
                InlineText: "Hello demo",
                LinkHref: null,
                LinkTarget: null),
        ]);
        var manifest = new StubDraftPreviewManifestRepo();

        var (success, error, _) = await DraftPreviewComposer.ComposeAsync(
            layoutId.ToString(),
            topo,
            manifest,
            ui);

        Assert.Null(error);
        Assert.NotNull(success);
        Assert.True(success!.DesignByNodeId.ContainsKey(nodeId));
        var designJson = JsonSerializer.Serialize(success.DesignByNodeId[nodeId]);
        Assert.Contains("Hello demo", designJson);
        Assert.Contains("color.primary", designJson);
    }

    [Fact]
    public void BuildDesignByNodeId_SkipsDesignsWithoutLayoutNodeId()
    {
        var map = DraftPreviewComposer.BuildDesignByNodeId([
            new ComponentStyleDesignListItemDto(
                Guid.NewGuid().ToString(), "orphan", null, null, ["color.primary"]),
            new ComponentStyleDesignListItemDto(
                Guid.NewGuid().ToString(), "bound", null, "node-1", ["color.secondary"], InlineText: "x"),
        ]);
        Assert.Single(map);
        Assert.True(map.ContainsKey("node-1"));
    }

    private sealed class StubDraftPreviewTopologyRepo(
        Guid layoutId,
        IReadOnlyList<LayoutNodeRecord> nodes) : TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double")
    {
        public override Task<IReadOnlyList<LayoutNodeRecord>> LoadLayoutNodesAsync(
            Guid requestedLayoutId,
            CancellationToken ct = default)
        {
            if (requestedLayoutId != layoutId)
                return Task.FromResult<IReadOnlyList<LayoutNodeRecord>>([]);
            return Task.FromResult(nodes);
        }
    }

    private sealed class StubDraftPreviewUiRepo(
        Guid layoutId,
        Guid packageId,
        string routeKey,
        IReadOnlyList<string> rootLayoutClassRefs,
        IReadOnlyList<ComponentStyleDesignListItemDto>? designs = null)
        : UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double")
    {
        public override Task<LayoutTensorContextDto?> ResolveLayoutTensorContextAsync(
            Guid requestedLayoutId,
            CancellationToken ct = default)
        {
            if (requestedLayoutId != layoutId) return Task.FromResult<LayoutTensorContextDto?>(null);
            return Task.FromResult<LayoutTensorContextDto?>(
                new LayoutTensorContextDto(packageId, routeKey, rootLayoutClassRefs));
        }

        public override Task<IReadOnlyList<ComponentStyleDesignListItemDto>> ListComponentStyleDesignsAsync(
            Guid? requestedPackageId,
            CancellationToken ct = default)
        {
            if (requestedPackageId != packageId)
                return Task.FromResult<IReadOnlyList<ComponentStyleDesignListItemDto>>([]);
            return Task.FromResult(designs ?? []);
        }
    }

    private sealed class StubDraftPreviewManifestRepo(
        IReadOnlyList<ManifestDetailRecord>? drafts = null) : ManifestRepository(
            NullLogger<ManifestRepository>.Instance)
    {
        private readonly IReadOnlyList<ManifestDetailRecord> _drafts = drafts ?? [];

        public override Task<ManifestRecord?> ResolveActiveManifestAsync(
            string? role, string? target, string? layer, string? action, CancellationToken ct = default)
            => Task.FromResult<ManifestRecord?>(null);

        public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default)
        {
            var detail = _drafts.FirstOrDefault(d => d.ManifestId == manifestId);
            return Task.FromResult(detail is null
                ? null
                : new ManifestRecord(detail.ManifestId, detail.RelationRegistryId, detail.Topology, detail.Status));
        }

        public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(
            string? statusFilter, CancellationToken ct = default)
        {
            if (!string.Equals(statusFilter, "draft", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

            IReadOnlyList<ManifestListItem> items = _drafts
                .Select(d => new ManifestListItem(
                    d.ManifestId,
                    d.RelationRegistryId,
                    d.Status,
                    null, null, null, null, null,
                    d.CreatedAt,
                    d.UpdatedAt))
                .ToList();
            return Task.FromResult(items);
        }

        public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default)
            => Task.FromResult(_drafts.FirstOrDefault(d => d.ManifestId == manifestId));

        public override Task<int> CountActiveAxisConflictsAsync(
            string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default)
            => Task.FromResult(0);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
            Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default)
            => Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, null));

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
            Guid manifestId, Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default)
            => Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, null));

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
            Guid manifestId, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default)
            => Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, null));

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
            Guid manifestId, CancellationToken ct = default)
            => Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, null));

        public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
            string? statusFilter, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PromotionManifestListItem>>([]);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
            Guid manifestId, JsonElement promotionEntry, CancellationToken ct = default)
            => Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, null));

        public override Task<int> CountActivePromotionKeyConflictsAsync(
            string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default)
            => Task.FromResult(0);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
            Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default)
            => Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, null));
    }
}
