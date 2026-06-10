using Topolactor.Repository;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves catalog layout node componentId from ui_component_registry.component_key
/// when layout_patch_json omitted componentId (legacy / UI-builder gaps).
/// </summary>
public static class LayoutNodeComponentIdEnrichment
{
    public static LayoutNodeRecord Enrich(
        LayoutNodeRecord node,
        IReadOnlyDictionary<string, string> componentIdByKey)
    {
        if (node.NodeKind is "structural_html" || !string.IsNullOrWhiteSpace(node.ComponentId))
            return node;
        if (string.IsNullOrWhiteSpace(node.ComponentKey))
            return node;
        return componentIdByKey.TryGetValue(node.ComponentKey, out var resolvedId)
            ? node with { ComponentId = resolvedId }
            : node;
    }
}
