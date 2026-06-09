using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies the component_wiring_execution_lane contract:
/// - LayoutNode carries ComponentKind, RuntimeDispatchAction, and full wiring spec fields
/// - LayoutNodeRecord carries ComponentKind, RuntimeDispatchAction, and full wiring spec fields
/// - StructureMapResolver forwards all enriched fields to LayoutNode
/// - Base TopologyRepository.LoadComponentKindsByIdsAsync returns empty dict
/// </summary>
public class AdminWiringExecutionLaneTests
{
    // ─── LayoutNode contract shape ───────────────────────────────────────────

    [Fact]
    public void LayoutNode_HasComponentKind_Field()
    {
        var node = new LayoutNode(
            NodeId: "n1", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-001", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 100, Height: 50,
            LayoutClassRefs: null,
            ComponentKind: "action/button",
            RuntimeDispatchAction: null);

        Assert.Equal("action/button", node.ComponentKind);
    }

    [Fact]
    public void LayoutNode_HasRuntimeDispatchAction_Field()
    {
        var node = new LayoutNode(
            NodeId: "n2", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-002", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 100, Height: 50,
            LayoutClassRefs: null,
            ComponentKind: "action/button",
            RuntimeDispatchAction: "Search");

        Assert.Equal("Search", node.RuntimeDispatchAction);
    }

    [Fact]
    public void LayoutNode_CarriesFullWiringSpec()
    {
        var node = new LayoutNode(
            NodeId: "n2w", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-002w", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 100, Height: 50,
            LayoutClassRefs: null,
            ComponentKind: "action/button",
            RuntimeDispatchAction: "Search",
            WiringId: "wiring-uuid-001",
            WiringKey: "search_key",
            WiringKind: "search",
            TargetSurface: "screen",
            TargetRef: "manifest-uuid-001");

        Assert.Equal("wiring-uuid-001", node.WiringId);
        Assert.Equal("search_key", node.WiringKey);
        Assert.Equal("search", node.WiringKind);
        Assert.Equal("screen", node.TargetSurface);
        Assert.Equal("manifest-uuid-001", node.TargetRef);
    }

    [Fact]
    public void LayoutNode_ComponentKind_DefaultsToNull()
    {
        var node = new LayoutNode(
            NodeId: "n3", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-003", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 0, Height: 0,
            LayoutClassRefs: null);

        Assert.Null(node.ComponentKind);
        Assert.Null(node.RuntimeDispatchAction);
        Assert.Null(node.WiringId);
        Assert.Null(node.WiringKind);
        Assert.Null(node.TargetSurface);
    }

    // ─── LayoutNodeRecord contract shape ────────────────────────────────────

    [Fact]
    public void LayoutNodeRecord_HasComponentKind_Field()
    {
        var record = new LayoutNodeRecord(
            NodeId: "r1", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-001", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 0, Height: 0,
            LayoutClassRefs: null,
            ComponentKind: "display/card",
            RuntimeDispatchAction: "Search");

        Assert.Equal("display/card", record.ComponentKind);
        Assert.Equal("Search", record.RuntimeDispatchAction);
    }

    [Fact]
    public void LayoutNodeRecord_CarriesFullWiringSpec()
    {
        var record = new LayoutNodeRecord(
            NodeId: "r1w", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-001w", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 0, Height: 0,
            LayoutClassRefs: null,
            ComponentKind: "display/card",
            RuntimeDispatchAction: "Search",
            WiringId: "wiring-uuid-r1",
            WiringKey: "search_screen",
            WiringKind: "search",
            TargetSurface: "screen",
            TargetRef: "manifest-uuid-r1");

        Assert.Equal("wiring-uuid-r1", record.WiringId);
        Assert.Equal("search_screen", record.WiringKey);
        Assert.Equal("search", record.WiringKind);
        Assert.Equal("screen", record.TargetSurface);
        Assert.Equal("manifest-uuid-r1", record.TargetRef);
    }

    [Fact]
    public void LayoutNodeRecord_ComponentKind_DefaultsToNull()
    {
        var record = new LayoutNodeRecord(
            NodeId: "r2", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-002", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 0, Height: 0,
            LayoutClassRefs: null);

        Assert.Null(record.ComponentKind);
        Assert.Null(record.RuntimeDispatchAction);
        Assert.Null(record.WiringId);
        Assert.Null(record.WiringKind);
        Assert.Null(record.TargetSurface);
    }

    // ─── StructureMapResolver: ComponentKind + RuntimeDispatchAction forwarded ─

    [Fact]
    public async Task StructureMapResolver_Resolve_ForwardsComponentKindAndDispatchAction()
    {
        var repo = new EnrichedLayoutNodeStubRepository();
        var resolver = new StructureMapResolver(repo);

        var attractor = new AttractorResult(
            AttractorKey: "layout-test:entity:search",
            StructureMapId: EnrichedLayoutNodeStubRepository.LayoutStructureMapId,
            PackageId: Guid.NewGuid(),
            SchemaId: Guid.NewGuid());

        var shape = await resolver.Resolve(attractor);

        Assert.NotNull(shape.LayoutNodes);
        Assert.NotEmpty(shape.LayoutNodes!);

        var catalogNode = shape.LayoutNodes!.First(n => n.NodeKind == "catalog_component");
        Assert.Equal("action/button", catalogNode.ComponentKind);
        Assert.Equal("Search", catalogNode.RuntimeDispatchAction);
        Assert.Equal("wiring-stub-001", catalogNode.WiringId);
        Assert.Equal("search_wiring", catalogNode.WiringKey);
        Assert.Equal("search", catalogNode.WiringKind);
        Assert.Equal("screen", catalogNode.TargetSurface);
        Assert.Equal("manifest-stub-001", catalogNode.TargetRef);
    }

    [Fact]
    public async Task StructureMapResolver_Resolve_StructuralHtmlNode_HasNullComponentKind()
    {
        var repo = new EnrichedLayoutNodeStubRepository();
        var resolver = new StructureMapResolver(repo);

        var attractor = new AttractorResult(
            AttractorKey: "layout-test:entity:search",
            StructureMapId: EnrichedLayoutNodeStubRepository.LayoutStructureMapId,
            PackageId: Guid.NewGuid(),
            SchemaId: Guid.NewGuid());

        var shape = await resolver.Resolve(attractor);

        Assert.NotNull(shape.LayoutNodes);
        var htmlNode = shape.LayoutNodes!.FirstOrDefault(n => n.NodeKind == "structural_html");
        if (htmlNode is not null)
        {
            Assert.Null(htmlNode.ComponentKind);
            Assert.Null(htmlNode.RuntimeDispatchAction);
        }
    }

    // ─── LayoutNode: PropsJson / StateJson contract shape ───────────────────

    [Fact]
    public void LayoutNode_HasPropsJson_AndStateJson_Fields()
    {
        var node = new LayoutNode(
            NodeId: "n-pj", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-pj-001", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 100, Height: 50,
            LayoutClassRefs: null,
            ComponentKind: "action/button",
            PropsJson: "{\"data\":{\"label\":\"カスタム\"}}",
            StateJson: "{\"open\":true}");

        Assert.Equal("{\"data\":{\"label\":\"カスタム\"}}", node.PropsJson);
        Assert.Equal("{\"open\":true}", node.StateJson);
    }

    [Fact]
    public void LayoutNode_PropsJson_DefaultsToNull()
    {
        var node = new LayoutNode(
            NodeId: "n-pj-null", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-pj-null", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 0, Height: 0,
            LayoutClassRefs: null);

        Assert.Null(node.PropsJson);
        Assert.Null(node.StateJson);
    }

    [Fact]
    public void LayoutNodeRecord_HasPropsJson_AndStateJson_Fields()
    {
        var record = new LayoutNodeRecord(
            NodeId: "r-pj", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-pj-r1", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 0, Height: 0,
            LayoutClassRefs: null,
            ComponentKind: "disclosure/modal",
            PropsJson: "{\"data\":{\"title\":\"モーダル\"}}",
            StateJson: "{\"open\":false}");

        Assert.Equal("{\"data\":{\"title\":\"モーダル\"}}", record.PropsJson);
        Assert.Equal("{\"open\":false}", record.StateJson);
    }

    [Fact]
    public void LayoutNodeRecord_PropsJson_DefaultsToNull()
    {
        var record = new LayoutNodeRecord(
            NodeId: "r-pj-null", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-pj-r-null", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 0, Height: 0,
            LayoutClassRefs: null);

        Assert.Null(record.PropsJson);
        Assert.Null(record.StateJson);
    }

    [Fact]
    public async Task StructureMapResolver_Resolve_ForwardsPropsJson_AndStateJson()
    {
        var repo = new EnrichedLayoutNodeWithPropsStubRepository();
        var resolver = new StructureMapResolver(repo);

        var attractor = new AttractorResult(
            AttractorKey: "layout-props-test:entity:search",
            StructureMapId: EnrichedLayoutNodeWithPropsStubRepository.LayoutStructureMapId,
            PackageId: Guid.NewGuid(),
            SchemaId: Guid.NewGuid());

        var shape = await resolver.Resolve(attractor);

        Assert.NotNull(shape.LayoutNodes);
        var node = shape.LayoutNodes!.First(n => n.NodeKind == "catalog_component");
        Assert.Equal("{\"data\":{\"label\":\"カスタムボタン\"}}", node.PropsJson);
        Assert.Equal("{\"open\":true}", node.StateJson);
    }

    // ─── Base TopologyRepository.LoadComponentKindsByIdsAsync ────────────────

    [Fact]
    public async Task TopologyRepository_LoadComponentKindsByIdsAsync_ReturnsEmptyDict()
    {
        var logger = NullLogger<TopologyRepository>.Instance;
        var repo = new TopologyRepository(logger, "dummy");

        var result = await repo.LoadComponentKindsByIdsAsync(["comp-001", "comp-002"]);

        Assert.Empty(result);
    }
}

// ─── Test doubles ────────────────────────────────────────────────────────────

file class EnrichedLayoutNodeStubRepository : TopologyRepository
{
    public const string LayoutStructureMapId = "00000000-0000-0000-0001-000000000001";
    private static readonly Guid LayoutId = new("00000000-0000-0000-0001-000000000002");

    public EnrichedLayoutNodeStubRepository()
        : base(NullLogger<TopologyRepository>.Instance, "dummy") { }

    public override Task<StructureMapRecord?> LoadStructureMapAsync(
        string key, CancellationToken ct = default)
    {
        if (key == LayoutStructureMapId)
        {
            return Task.FromResult<StructureMapRecord?>(new StructureMapRecord(
                StructureMapId: LayoutStructureMapId,
                AttractorKey: "layout-test:entity:search",
                PackageId: Guid.NewGuid(),
                SchemaId: Guid.NewGuid(),
                ComponentIds: [],
                StatePolicyJson: null,
                LayoutId: LayoutId));
        }
        return Task.FromResult<StructureMapRecord?>(null);
    }

    public override Task<IReadOnlyList<LayoutNodeRecord>> LoadLayoutNodesAsync(
        Guid layoutId, CancellationToken ct = default)
    {
        IReadOnlyList<LayoutNodeRecord> rows =
        [
            new LayoutNodeRecord(
                NodeId: "node-btn",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: "Search Btn",
                ComponentId: "comp-btn-001",
                ParentNodeId: null,
                SlotKey: "slot_btn",
                OrderIndex: 0,
                X: 0, Y: 0, Width: 100, Height: 40,
                LayoutClassRefs: null,
                ComponentKind: "action/button",
                RuntimeDispatchAction: "Search",
                WiringId: "wiring-stub-001",
                WiringKey: "search_wiring",
                WiringKind: "search",
                TargetSurface: "screen",
                TargetRef: "manifest-stub-001"),
        ];
        return Task.FromResult(rows);
    }
}

file class EnrichedLayoutNodeWithPropsStubRepository : TopologyRepository
{
    public const string LayoutStructureMapId = "00000000-0000-0000-0002-000000000001";
    private static readonly Guid LayoutId = new("00000000-0000-0000-0002-000000000002");

    public EnrichedLayoutNodeWithPropsStubRepository()
        : base(NullLogger<TopologyRepository>.Instance, "dummy") { }

    public override Task<StructureMapRecord?> LoadStructureMapAsync(
        string key, CancellationToken ct = default)
    {
        if (key == LayoutStructureMapId)
        {
            return Task.FromResult<StructureMapRecord?>(new StructureMapRecord(
                StructureMapId: LayoutStructureMapId,
                AttractorKey: "layout-props-test:entity:search",
                PackageId: Guid.NewGuid(),
                SchemaId: Guid.NewGuid(),
                ComponentIds: [],
                StatePolicyJson: null,
                LayoutId: LayoutId));
        }
        return Task.FromResult<StructureMapRecord?>(null);
    }

    public override Task<IReadOnlyList<LayoutNodeRecord>> LoadLayoutNodesAsync(
        Guid layoutId, CancellationToken ct = default)
    {
        IReadOnlyList<LayoutNodeRecord> rows =
        [
            new LayoutNodeRecord(
                NodeId: "node-modal",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: "modal.template",
                ComponentId: "comp-modal-001",
                ParentNodeId: null,
                SlotKey: null,
                OrderIndex: 0,
                X: 0, Y: 0, Width: 320, Height: 200,
                LayoutClassRefs: null,
                ComponentKind: "disclosure/modal",
                RuntimeDispatchAction: null,
                PropsJson: "{\"data\":{\"label\":\"カスタムボタン\"}}",
                StateJson: "{\"open\":true}"),
        ];
        return Task.FromResult(rows);
    }
}
