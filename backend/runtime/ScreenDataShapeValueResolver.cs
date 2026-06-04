using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves condition values from runtime value sources (not preview literals).
/// </summary>
public static class ScreenDataShapeValueResolver
{
    public sealed record ValueSource(
        string Kind,
        string? Key,
        string? SampleValue);

    public static string? Resolve(ValueSource? source, string? literalFallback, OperationVector vector)
    {
        if (source is null || string.Equals(source.Kind, "literal", StringComparison.OrdinalIgnoreCase))
            return literalFallback ?? source?.SampleValue;

        var key = source.Key?.Trim();
        if (string.IsNullOrEmpty(key))
            return literalFallback;

        return source.Kind.ToLowerInvariant() switch
        {
            "runtimeparam" => LookupContext(vector, $"runtimeParam.{key}") ??
                              LookupPayloadString(vector, "runtimeParams", key),
            "operationinput" => LookupPayloadString(vector, null, key) ??
                                LookupPayloadNested(vector, "operationInput", key),
            "routequery" => LookupContext(vector, $"routeQuery.{key}") ??
                            LookupPayloadString(vector, "routeQuery", key),
            "authclaim" => LookupContext(vector, $"authClaim.{key}") ??
                           LookupPayloadString(vector, "authClaims", key),
            "formvalue" => LookupPayloadString(vector, "formValues", key) ??
                           LookupContext(vector, $"formValue.{key}"),
            _ => literalFallback,
        };
    }

    public static ValueSource? ParseValueSource(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty("kind", out var kindEl) || kindEl.ValueKind != JsonValueKind.String)
            return null;
        var kind = kindEl.GetString()?.Trim();
        if (string.IsNullOrEmpty(kind)) return null;
        string? key = null;
        if (el.TryGetProperty("key", out var keyEl) && keyEl.ValueKind == JsonValueKind.String)
            key = keyEl.GetString();
        string? sample = null;
        if (el.TryGetProperty("sampleValue", out var sampleEl) && sampleEl.ValueKind == JsonValueKind.String)
            sample = sampleEl.GetString();
        return new ValueSource(kind, key, sample);
    }

    private static string? LookupContext(OperationVector vector, string fullKey)
    {
        // Context is not on OperationVector directly — callers pass via payload "__context" when needed.
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return null;
        if (!vector.Payload.Value.TryGetProperty("__context", out var ctxEl) ||
            ctxEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (ctxEl.TryGetProperty(fullKey, out var direct) && direct.ValueKind == JsonValueKind.String)
            return direct.GetString();
        return null;
    }

    private static string? LookupPayloadString(OperationVector vector, string? objectKey, string fieldKey)
    {
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return null;

        var root = vector.Payload.Value;
        if (objectKey is null)
        {
            if (root.TryGetProperty(fieldKey, out var direct))
                return ElementToString(direct);
            return null;
        }

        if (!root.TryGetProperty(objectKey, out var obj) || obj.ValueKind != JsonValueKind.Object)
            return null;
        if (!obj.TryGetProperty(fieldKey, out var val))
            return null;
        return ElementToString(val);
    }

    private static string? LookupPayloadNested(OperationVector vector, string objectKey, string fieldKey) =>
        LookupPayloadString(vector, objectKey, fieldKey);

    private static string? ElementToString(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
}
