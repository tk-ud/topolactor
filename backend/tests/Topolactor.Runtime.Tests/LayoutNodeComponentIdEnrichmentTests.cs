using Topolactor.Repository;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class LayoutNodeComponentIdEnrichmentTests
{
    [Fact]
    public void CatalogNodeWithoutComponentId_IsResolvableFromRegistryKey()
    {
        var node = new LayoutNodeRecord(
            NodeId: "node-input",
            NodeKind: null,
            HtmlTag: null,
            ComponentKey: "input.primitive",
            ComponentId: null,
            ParentNodeId: null,
            SlotKey: null,
            OrderIndex: 0,
            X: 0,
            Y: 0,
            Width: null,
            Height: null,
            LayoutClassRefs: null);

        var idByKey = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["input.primitive"] = "15efada6-6962-4038-b3ee-d015c4e63c63",
        };

        var enriched = LayoutNodeComponentIdEnrichment.Enrich(node, idByKey);

        Assert.Equal("15efada6-6962-4038-b3ee-d015c4e63c63", enriched.ComponentId);
    }

    [Fact]
    public void StructuralHtmlNode_IsNotEnriched()
    {
        var node = new LayoutNodeRecord(
            NodeId: "node-div",
            NodeKind: "structural_html",
            HtmlTag: "div",
            ComponentKey: "layout/structural_html",
            ComponentId: null,
            ParentNodeId: null,
            SlotKey: null,
            OrderIndex: 0,
            X: 0,
            Y: 0,
            Width: null,
            Height: null,
            LayoutClassRefs: null);

        var idByKey = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["layout/structural_html"] = "00000000-0000-0000-0000-000000000099",
        };

        var enriched = LayoutNodeComponentIdEnrichment.Enrich(node, idByKey);

        Assert.Null(enriched.ComponentId);
    }
}
