namespace Topolactor.Repository;

/// <summary>
/// Derives dispatcher axes from screen operation kind and manifest identity.
/// Target/layer are manifest-scoped so multiple active screens do not share admin/default/entity/Read.
/// </summary>
public static class ManifestScreenOperationDeriver
{
    public static bool TryDeriveAxes(
        string? screenOperationKind,
        string? manifestKey,
        Guid? manifestId,
        out string role,
        out string target,
        out string layer,
        out string action,
        out string runtimeDestination)
    {
        role = target = layer = action = runtimeDestination = string.Empty;
        if (string.IsNullOrWhiteSpace(screenOperationKind)) return false;

        target = ResolveManifestTarget(manifestKey, manifestId);
        role = "admin";
        runtimeDestination = "topology_transform_runtime";

        switch (screenOperationKind.Trim().ToLowerInvariant())
        {
            case "list":
                layer = "screen_list";
                action = "Read";
                return true;
            case "search":
                layer = "screen_entity";
                action = "Search";
                return true;
            case "detail":
                layer = "screen_detail";
                action = "Read";
                return true;
            case "create":
                layer = "screen_entity";
                action = "Create";
                return true;
            case "update":
                layer = "screen_entity";
                action = "Update";
                return true;
            case "aggregation_view":
                layer = "screen_aggregation";
                action = "Read";
                return true;
            default:
                return false;
        }
    }

    /// <summary>Backward-compatible overload (no manifest identity — prefer manifest-scoped overload).</summary>
    public static bool TryDeriveAxes(
        string? screenOperationKind,
        out string role,
        out string target,
        out string layer,
        out string action,
        out string runtimeDestination) =>
        TryDeriveAxes(
            screenOperationKind,
            manifestKey: null,
            manifestId: null,
            out role,
            out target,
            out layer,
            out action,
            out runtimeDestination);

    public static string ResolveManifestTarget(string? manifestKey, Guid? manifestId)
    {
        if (!string.IsNullOrWhiteSpace(manifestKey))
        {
            var trimmed = manifestKey.Trim();
            return SanitizeTarget(trimmed);
        }

        if (manifestId.HasValue && manifestId.Value != Guid.Empty)
            return $"screen_{manifestId.Value:N}";

        return "screen_unassigned";
    }

    internal static string SanitizeTarget(string raw)
    {
        var chars = raw
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_')
            .ToArray();
        var sanitized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "screen_unassigned" : sanitized;
    }
}
