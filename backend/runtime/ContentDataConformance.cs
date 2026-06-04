namespace Topolactor.Runtime;

/// <summary>
/// Shared manifest/schema type-shape diagnostics for import preview and Step3 initial data.
/// Non-blocking warnings — callers may persist values despite mismatches.
/// </summary>
public static class ContentDataConformance
{
    public sealed record SchemaField(string Key, string Type, bool Required);

    public static List<string> ValidateRow(
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyList<SchemaField> fields)
    {
        var errors = new List<string>();

        foreach (var field in fields)
        {
            row.TryGetValue(field.Key, out var value);

            if (field.Required)
            {
                if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
                {
                    errors.Add($"field '{field.Key}' is required but missing or empty");
                    continue;
                }
            }

            if (value is null) continue;

            var typeError = ValidateFieldType(field.Key, value, field.Type);
            if (typeError is not null)
                errors.Add(typeError);
        }

        return errors;
    }

    public static string? ValidateFieldType(string key, object value, string type)
    {
        var raw = value?.ToString() ?? "";

        return type switch
        {
            "number" or "integer" or "float" or "decimal"
                when !double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _)
                => $"field '{key}' expects type '{type}' but value is not numeric: '{raw}'",

            "boolean"
                when !bool.TryParse(raw, out _) && raw is not ("1" or "0" or "true" or "false")
                => $"field '{key}' expects type 'boolean' but value is not boolean: '{raw}'",

            "uuid"
                when !Guid.TryParse(raw, out _)
                => $"field '{key}' expects type 'uuid' but value is not a valid UUID: '{raw}'",

            _ => null
        };
    }
}
