using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves label/value field manifests from raw JSONB data sources.
///
/// Purpose:
///   Given a raw JSONB blob (e.g., from admin_import_records.records) and a
///   field manifest definition, extract and resolve key→label→value pairs
///   in the format expected by DocumentCanvasTemplateEditor fields.
///
/// Invariants:
///   - Resolution is read-only; no DB mutation.
///   - Missing JSONB paths resolve to null value (not error) unless required by policy.
///   - Unknown aggregation or display policies return explicit error (no silent fallback).
///   - Output fields are in DocumentCanvas field shape: key, label?, x?, y?, value?.
/// </summary>
public class JsonbManifestRuntime
{
    private readonly ILogger<JsonbManifestRuntime> _logger;

    // Supported aggregation policies
    private static readonly IReadOnlySet<string> SupportedAggregationPolicies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "first", "last", "concat", "sum", "count"
    };

    // Supported display policies
    private static readonly IReadOnlySet<string> SupportedDisplayPolicies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "as_is", "format_currency", "format_date", "format_number", "uppercase", "lowercase"
    };

    public JsonbManifestRuntime(ILogger<JsonbManifestRuntime> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves DocumentCanvas fields from raw JSONB data and a field manifest.
    /// </summary>
    public JsonbManifestResolveResult ResolveFields(
        string sourceJsonb,
        IReadOnlyList<JsonbFieldManifestEntry> fieldManifest)
    {
        if (string.IsNullOrWhiteSpace(sourceJsonb))
            return Error("JSONB_MANIFEST_SOURCE_EMPTY", "sourceJsonb is required and must not be empty.");

        if (fieldManifest is null || fieldManifest.Count == 0)
            return Error("JSONB_MANIFEST_FIELD_MANIFEST_EMPTY", "fieldManifest is required and must contain at least one entry.");

        // Validate manifest entries before parsing source
        for (var i = 0; i < fieldManifest.Count; i++)
        {
            var entry = fieldManifest[i];
            if (string.IsNullOrWhiteSpace(entry.Key))
                return Error("JSONB_MANIFEST_FIELD_KEY_REQUIRED", $"fieldManifest[{i}].key is required and must be non-empty.");

            if (entry.AggregationPolicy is not null &&
                !SupportedAggregationPolicies.Contains(entry.AggregationPolicy))
                return Error("JSONB_MANIFEST_UNSUPPORTED_AGGREGATION_POLICY",
                    $"fieldManifest[{i}].aggregationPolicy '{entry.AggregationPolicy}' is not supported. " +
                    $"Supported: {string.Join(", ", SupportedAggregationPolicies)}");

            if (entry.DisplayPolicy is not null &&
                !SupportedDisplayPolicies.Contains(entry.DisplayPolicy))
                return Error("JSONB_MANIFEST_UNSUPPORTED_DISPLAY_POLICY",
                    $"fieldManifest[{i}].displayPolicy '{entry.DisplayPolicy}' is not supported. " +
                    $"Supported: {string.Join(", ", SupportedDisplayPolicies)}");
        }

        JsonDocument sourceDoc;
        try
        {
            sourceDoc = JsonDocument.Parse(sourceJsonb);
        }
        catch (JsonException ex)
        {
            return Error("JSONB_MANIFEST_INVALID_SOURCE_JSON",
                $"sourceJsonb is not valid JSON: {ex.Message}");
        }

        var resolvedFields = new List<JsonbResolvedField>();

        foreach (var entry in fieldManifest)
        {
            var rawValue = ExtractJsonValue(sourceDoc.RootElement, entry.JsonPath);
            var displayedValue = ApplyDisplayPolicy(rawValue, entry.DisplayPolicy ?? "as_is");

            resolvedFields.Add(new JsonbResolvedField(
                Key: entry.Key,
                Label: entry.Label,
                X: entry.X,
                Y: entry.Y,
                Value: displayedValue));
        }

        _logger.LogDebug(
            "JsonbManifestRuntime.ResolveFields: resolved {Count} fields from manifest",
            resolvedFields.Count);

        return new JsonbManifestResolveResult(
            Success: true,
            Fields: resolvedFields,
            ErrorCode: null,
            ErrorMessage: null);
    }

    // ---------------------------------------------------------------------------
    // JSON path extraction — supports simple dot-notation and root-level keys
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Extracts a value from a JsonElement by jsonPath.
    /// Supports: null/empty (returns string of root), "$.key", "key", "key.subkey".
    /// Returns null if path not found.
    /// </summary>
    private static string? ExtractJsonValue(JsonElement root, string? jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
            return ElementToString(root);

        // Normalize: strip leading "$." prefix
        var path = jsonPath.StartsWith("$.", StringComparison.Ordinal)
            ? jsonPath[2..]
            : jsonPath.TrimStart('$');

        var segments = path.Split('.');
        var current = root;

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment)) continue;

            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(segment, out var next))
                return null;

            current = next;
        }

        return ElementToString(current);
    }

    private static string? ElementToString(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String  => el.GetString(),
            JsonValueKind.Number  => el.GetRawText(),
            JsonValueKind.True    => "true",
            JsonValueKind.False   => "false",
            JsonValueKind.Null    => null,
            JsonValueKind.Undefined => null,
            _ => el.GetRawText()
        };
    }

    // ---------------------------------------------------------------------------
    // Display policy application
    // ---------------------------------------------------------------------------

    private static string? ApplyDisplayPolicy(string? value, string policy)
    {
        if (value is null) return null;

        return policy.ToLowerInvariant() switch
        {
            "as_is"            => value,
            "uppercase"        => value.ToUpperInvariant(),
            "lowercase"        => value.ToLowerInvariant(),
            "format_currency"  => FormatCurrency(value),
            "format_date"      => FormatDate(value),
            "format_number"    => FormatNumber(value),
            _                  => value
        };
    }

    private static string FormatCurrency(string value)
    {
        if (double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        return value;
    }

    private static string FormatDate(string value)
    {
        if (DateTimeOffset.TryParse(value, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return value;
    }

    private static string FormatNumber(string value)
    {
        if (double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        return value;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static JsonbManifestResolveResult Error(string code, string message)
        => new(Success: false, Fields: null, ErrorCode: code, ErrorMessage: message);
}
