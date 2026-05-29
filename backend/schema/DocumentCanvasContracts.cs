using System.Text.Json.Serialization;

namespace Topolactor.Schema;

// ---------------------------------------------------------------------------
// DocumentCanvas Binding Policy Contracts
// ---------------------------------------------------------------------------
// User-defined aggregation, display, and binding policy for DocumentCanvas fields.
//
// Scope:
//   - AggregationPolicy: how multiple source values are reduced to one field value
//   - DisplayPolicy: how a resolved value is formatted for display
//   - BindingPolicy: how fields are mapped to canvas coordinates and source paths
//
// Out of scope: production PDF layout engine, canonical DB mutation.
// ---------------------------------------------------------------------------

/// <summary>
/// Aggregation policy for reducing multiple source values to a single field value.
/// </summary>
public enum CanvasAggregationPolicy
{
    First,
    Last,
    Concat,
    Sum,
    Count
}

/// <summary>
/// Display policy for formatting a resolved value for canvas presentation.
/// </summary>
public enum CanvasDisplayPolicy
{
    AsIs,
    FormatCurrency,
    FormatDate,
    FormatNumber,
    Uppercase,
    Lowercase
}

/// <summary>
/// User-defined binding policy for a single DocumentCanvas field.
/// Combines source path, layout position, and aggregation/display policies.
/// </summary>
public record CanvasFieldBindingPolicy(
    [property: JsonPropertyName("key")]               string Key,
    [property: JsonPropertyName("jsonPath")]          string? JsonPath,
    [property: JsonPropertyName("label")]             string? Label,
    [property: JsonPropertyName("x")]                 double? X,
    [property: JsonPropertyName("y")]                 double? Y,
    [property: JsonPropertyName("aggregationPolicy")] CanvasAggregationPolicy AggregationPolicy,
    [property: JsonPropertyName("displayPolicy")]     CanvasDisplayPolicy DisplayPolicy
);

/// <summary>
/// User-defined binding policy document for a DocumentCanvas template.
/// Contains the ordered set of field policies and optional canvas metadata.
/// </summary>
public record DocumentCanvasBindingPolicyDocument(
    [property: JsonPropertyName("templateKey")]       string TemplateKey,
    [property: JsonPropertyName("backgroundImageUrl")]string? BackgroundImageUrl,
    [property: JsonPropertyName("fields")]            IReadOnlyList<CanvasFieldBindingPolicy> Fields
);

/// <summary>
/// Validation result for a DocumentCanvasBindingPolicyDocument.
/// </summary>
public record CanvasBindingPolicyValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors
);

/// <summary>
/// Result of applying a binding policy to produce canvas-ready field values.
/// </summary>
public record CanvasBindingApplyResult(
    bool Success,
    string? TemplateKey,
    string? BackgroundImageUrl,
    IReadOnlyList<JsonbResolvedField>? Fields,
    string? ErrorCode,
    string? ErrorMessage
);
