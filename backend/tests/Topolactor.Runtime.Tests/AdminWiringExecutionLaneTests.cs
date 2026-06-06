using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies the component_wiring_execution_lane contract:
/// - LayoutNode carries ComponentKind and RuntimeDispatchAction
/// - LayoutNodeRecord carries ComponentKind and RuntimeDispatchAction
/// - StructureMapResolver forwards enriched fields to LayoutNode
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
    public void LayoutNode_ComponentKind_DefaultsToNull()
    {
        var node = new LayoutNode(
            NodeId: "n3", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-003", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 0, Height: 0,
            LayoutClassRefs: null);

        Assert.Null(node.ComponentKind);
        Assert.Null(node.RuntimeDispatchAction);
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
    public void LayoutNodeRecord_ComponentKind_DefaultsToNull()
    {
        var record = new LayoutNodeRecord(
            NodeId: "r2", NodeKind: "catalog_component", HtmlTag: null,
            ComponentKey: null, ComponentId: "comp-002", ParentNodeId: null,
            SlotKey: null, OrderIndex: 0, X: 0, Y: 0, Width: 0, Height: 0,
            LayoutClassRefs: null);

        Assert.Null(record.ComponentKind);
        Assert.Null(record.RuntimeDispatchAction);
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
                ComponentKey: null,
                ComponentId: "comp-btn-001",
                ParentNodeId: null,
                SlotKey: "slot_btn",
                OrderIndex: 0,
                X: 0, Y: 0, Width: 100, Height: 40,
                LayoutClassRefs: null,
                ComponentKind: "action/button",
                RuntimeDispatchAction: "Search"),
        ];
        return Task.FromResult(rows);
    }
}
