using System.Text.Json;

namespace Topolactor.Runtime;

public static class ScreenReadQueryWiringCandidates
{
    public static List<object> Flatten(JsonElement wiringRoot)
    {
        var candidates = new List<object>();
        if (wiringRoot.ValueKind != JsonValueKind.Object) return candidates;

        foreach (var field in new[] { "searchConditions", "havingConditions", "aggregationMeasures", "displayColumns" })
        {
            if (!wiringRoot.TryGetProperty(field, out var section) || section.ValueKind != JsonValueKind.Object)
                continue;
            if (!section.TryGetProperty("bindings", out var bindings) || bindings.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var binding in bindings.EnumerateArray())
            {
                if (binding.ValueKind != JsonValueKind.Object) continue;
                var wiringKey = binding.TryGetProperty("wiringKey", out var wk) ? wk.GetString() : null;
                if (string.IsNullOrWhiteSpace(wiringKey)) continue;
                var label = field;
                if (binding.TryGetProperty("column", out var col) && col.ValueKind == JsonValueKind.String)
                    label += $" · {col.GetString()}";
                if (binding.TryGetProperty("operator", out var op) && op.ValueKind == JsonValueKind.String)
                    label += $" · {op.GetString()}";
                if (binding.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.String)
                    label += $" · {fn.GetString()}";

                candidates.Add(new
                {
                    wiringKey,
                    label,
                    field,
                });
            }
        }

        if (wiringRoot.TryGetProperty("displayColumnMode", out var mode) && mode.ValueKind == JsonValueKind.Object)
        {
            var wiringKey = mode.TryGetProperty("wiringKey", out var wk) ? wk.GetString() : "screen.displayColumnMode";
            var modeVal = mode.TryGetProperty("mode", out var m) ? m.GetString() : "";
            candidates.Add(new
            {
                wiringKey,
                label = $"displayColumnMode · {modeVal}",
                field = "displayColumnMode",
            });
        }

        return candidates;
    }

    /// <summary>
    /// Returns the set of all wiringKey values present in the screenReadQueryWiring element.
    /// Used for backend validation that a targetRef wiringKey is a known candidate.
    /// </summary>
    public static HashSet<string> GetWiringKeys(JsonElement wiringRoot)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (wiringRoot.ValueKind != JsonValueKind.Object) return keys;

        foreach (var field in new[] { "searchConditions", "havingConditions", "aggregationMeasures", "displayColumns" })
        {
            if (!wiringRoot.TryGetProperty(field, out var section) || section.ValueKind != JsonValueKind.Object)
                continue;
            if (!section.TryGetProperty("bindings", out var bindings) || bindings.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var binding in bindings.EnumerateArray())
            {
                if (binding.ValueKind != JsonValueKind.Object) continue;
                var wk = binding.TryGetProperty("wiringKey", out var wkEl) ? wkEl.GetString() : null;
                if (!string.IsNullOrWhiteSpace(wk)) keys.Add(wk!);
            }
        }

        if (wiringRoot.TryGetProperty("displayColumnMode", out var mode) && mode.ValueKind == JsonValueKind.Object)
        {
            var modeKey = mode.TryGetProperty("wiringKey", out var wk) ? wk.GetString() : "screen.displayColumnMode";
            if (!string.IsNullOrWhiteSpace(modeKey)) keys.Add(modeKey!);
        }

        return keys;
    }
}
