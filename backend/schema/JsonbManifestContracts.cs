using System.Text.Json.Serialization;

namespace Topolactor.Schema;

// ---------------------------------------------------------------------------
// JSONB Label/Value Manifest Runtime Contracts
// ---------------------------------------------------------------------------
// Resolves field label/value pairs from raw JSONB data using a field manifest.
// The manifest defines how each canvas field maps to a JSONB path in source data.
//
// Out of scope: production PDF layout engine, advanced JSONB query operators,
//               canonical DB mutation, business lifecycle state.
// ---------------------------------------------------------------------------

/// <summary>
/// Defines how a single canvas field maps to a JSONB source path and display policy.
/// </summary>
public record JsonbFieldManifestEntry(
    [property: JsonPropertyName("key")]               string Key,
    [property: JsonPropertyName("jsonPath")]          string? JsonPath,
    [property: JsonPropertyName("label")]             string? Label,
    [property: JsonPropertyName("x")]                 double? X,
    [property: JsonPropertyName("y")]                 double? Y,
    [property: JsonPropertyName("aggregationPolicy")] string? AggregationPolicy,
    [property: JsonPropertyName("displayPolicy")]     string? DisplayPolicy
);

/// <summary>
/// A resolved field ready for DocumentCanvas rendering.
/// Matches DocumentCanvasTemplateEditorField shape: key, label, x, y, value.
/// </summary>
public record JsonbResolvedField(
    [property: JsonPropertyName("key")]   string Key,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("x")]     double? X,
    [property: JsonPropertyName("y")]     double? Y,
    [property: JsonPropertyName("value")] string? Value
);

/// <summary>
/// Result of JsonbManifestRuntime.ResolveFieldsAsync.
/// </summary>
public record JsonbManifestResolveResult(
    bool Success,
    IReadOnlyList<JsonbResolvedField>? Fields,
    string? ErrorCode,
    string? ErrorMessage
);

/// <summary>
/// Request to resolve fields from a JSONB source row using a field manifest.
/// </summary>
public record JsonbManifestResolveRequest(
    [property: JsonPropertyName("sourceJsonb")]    string SourceJsonb,
    [property: JsonPropertyName("fieldManifest")]  IReadOnlyList<JsonbFieldManifestEntry> FieldManifest
);
