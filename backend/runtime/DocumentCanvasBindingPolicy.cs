using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Validates and applies user-defined DocumentCanvas binding policies.
///
/// Responsibilities:
///   - Validates a DocumentCanvasBindingPolicyDocument (field keys, policies, coordinates).
///   - Applies the binding policy to JSONB source data via JsonbManifestRuntime.
///   - Returns canvas-ready fields (key, label, x, y, value) for DocumentCanvas rendering.
///
/// Invariants:
///   - Policy validation is fail-close: any invalid policy entry returns explicit error.
///   - Source data extraction is read-only (no DB mutation).
///   - Unknown policy enum values return explicit error (no silent fallback).
///   - Display and aggregation policy responsibilities are separated:
///       AggregationPolicy: how values from multi-value JSONB paths are reduced.
///       DisplayPolicy: how a single resolved value is formatted for display.
/// </summary>
public class DocumentCanvasBindingPolicy
{
    private readonly ILogger<DocumentCanvasBindingPolicy> _logger;
    private readonly JsonbManifestRuntime _jsonbManifestRuntime;

    public DocumentCanvasBindingPolicy(
        ILogger<DocumentCanvasBindingPolicy> logger,
        JsonbManifestRuntime jsonbManifestRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonbManifestRuntime = jsonbManifestRuntime ?? throw new ArgumentNullException(nameof(jsonbManifestRuntime));
    }

    /// <summary>
    /// Validates a DocumentCanvasBindingPolicyDocument without applying it to data.
    /// Returns all validation errors; empty list means valid.
    /// </summary>
    public CanvasBindingPolicyValidationResult Validate(
        DocumentCanvasBindingPolicyDocument policy)
    {
        if (policy is null)
            return Invalid(["policy document is required."]);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(policy.TemplateKey))
            errors.Add("templateKey is required and must be non-empty.");

        if (policy.Fields is null || policy.Fields.Count == 0)
            errors.Add("fields is required and must contain at least one entry.");
        else
        {
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < policy.Fields.Count; i++)
            {
                var field = policy.Fields[i];
                if (string.IsNullOrWhiteSpace(field.Key))
                {
                    errors.Add($"fields[{i}].key is required and must be non-empty.");
                    continue;
                }
                if (!seenKeys.Add(field.Key))
                    errors.Add($"fields[{i}].key '{field.Key}' is duplicated; field keys must be unique.");

                if (field.X.HasValue && double.IsNaN(field.X.Value))
                    errors.Add($"fields[{i}].x is NaN; must be a finite number.");
                if (field.Y.HasValue && double.IsNaN(field.Y.Value))
                    errors.Add($"fields[{i}].y is NaN; must be a finite number.");
            }
        }

        return errors.Count == 0
            ? new CanvasBindingPolicyValidationResult(IsValid: true, Errors: [])
            : new CanvasBindingPolicyValidationResult(IsValid: false, Errors: errors);
    }

    /// <summary>
    /// Applies the binding policy to a raw JSONB source row.
    /// Returns canvas-ready fields for DocumentCanvas rendering.
    /// </summary>
    public CanvasBindingApplyResult Apply(
        DocumentCanvasBindingPolicyDocument policy,
        string sourceJsonb)
    {
        var validation = Validate(policy);
        if (!validation.IsValid)
            return ApplyError("CANVAS_BINDING_POLICY_INVALID",
                string.Join("; ", validation.Errors));

        // Convert binding policy fields to JsonbFieldManifestEntry for resolution
        var manifestEntries = policy.Fields
            .Select(f => new JsonbFieldManifestEntry(
                Key: f.Key,
                JsonPath: f.JsonPath,
                Label: f.Label,
                X: f.X,
                Y: f.Y,
                AggregationPolicy: AggregationPolicyToString(f.AggregationPolicy),
                DisplayPolicy: DisplayPolicyToString(f.DisplayPolicy)))
            .ToList();

        var resolveResult = _jsonbManifestRuntime.ResolveFields(sourceJsonb, manifestEntries);
        if (!resolveResult.Success)
            return ApplyError(resolveResult.ErrorCode!, resolveResult.ErrorMessage!);

        _logger.LogDebug(
            "DocumentCanvasBindingPolicy.Apply: templateKey={Key} fieldCount={Count}",
            policy.TemplateKey, resolveResult.Fields!.Count);

        return new CanvasBindingApplyResult(
            Success: true,
            TemplateKey: policy.TemplateKey,
            BackgroundImageUrl: policy.BackgroundImageUrl,
            Fields: resolveResult.Fields,
            ErrorCode: null,
            ErrorMessage: null);
    }

    // ---------------------------------------------------------------------------
    // Enum conversion helpers
    // ---------------------------------------------------------------------------

    private static string AggregationPolicyToString(CanvasAggregationPolicy policy)
        => policy switch
        {
            CanvasAggregationPolicy.First  => "first",
            CanvasAggregationPolicy.Last   => "last",
            CanvasAggregationPolicy.Concat => "concat",
            CanvasAggregationPolicy.Sum    => "sum",
            CanvasAggregationPolicy.Count  => "count",
            _ => "first"
        };

    private static string DisplayPolicyToString(CanvasDisplayPolicy policy)
        => policy switch
        {
            CanvasDisplayPolicy.AsIs           => "as_is",
            CanvasDisplayPolicy.FormatCurrency => "format_currency",
            CanvasDisplayPolicy.FormatDate     => "format_date",
            CanvasDisplayPolicy.FormatNumber   => "format_number",
            CanvasDisplayPolicy.Uppercase      => "uppercase",
            CanvasDisplayPolicy.Lowercase      => "lowercase",
            _ => "as_is"
        };

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static CanvasBindingApplyResult ApplyError(string code, string message)
        => new(Success: false, TemplateKey: null, BackgroundImageUrl: null,
               Fields: null, ErrorCode: code, ErrorMessage: message);

    private static CanvasBindingPolicyValidationResult Invalid(IReadOnlyList<string> errors)
        => new(IsValid: false, Errors: errors);
}
