namespace Topolactor.Repository;

/// <summary>
/// Derives dispatcher axes from user-facing screen operation kinds (contents authoring).
/// </summary>
public static class ManifestScreenOperationDeriver
{
    public static bool TryDeriveAxes(
        string? screenOperationKind,
        out string role,
        out string target,
        out string layer,
        out string action,
        out string runtimeDestination)
    {
        role = target = layer = action = runtimeDestination = string.Empty;
        if (string.IsNullOrWhiteSpace(screenOperationKind)) return false;

        switch (screenOperationKind.Trim().ToLowerInvariant())
        {
            case "list":
                role = "admin"; target = "default"; layer = "entity"; action = "Read";
                runtimeDestination = "topology_transform_runtime";
                return true;
            case "search":
                role = "admin"; target = "default"; layer = "entity"; action = "Search";
                runtimeDestination = "topology_transform_runtime";
                return true;
            case "detail":
                role = "admin"; target = "default"; layer = "entity"; action = "Read";
                runtimeDestination = "topology_transform_runtime";
                return true;
            case "create":
                role = "admin"; target = "default"; layer = "entity"; action = "Create";
                runtimeDestination = "topology_transform_runtime";
                return true;
            case "update":
                role = "admin"; target = "default"; layer = "entity"; action = "Update";
                runtimeDestination = "topology_transform_runtime";
                return true;
            case "aggregation_view":
                role = "admin"; target = "default"; layer = "aggregation"; action = "Read";
                runtimeDestination = "topology_transform_runtime";
                return true;
            default:
                return false;
        }
    }
}
