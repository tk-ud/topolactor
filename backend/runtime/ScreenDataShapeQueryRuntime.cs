using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// topology_transform_runtime screen_data_shape query execution.
/// Loads manifest topology, applies operator vocabulary over initialDataRows, returns emission data.
/// </summary>
public sealed class ScreenDataShapeQueryRuntime
{
    private readonly ManifestRepository? _manifestRepository;

    public ScreenDataShapeQueryRuntime(ManifestRepository? manifestRepository = null)
    {
        _manifestRepository = manifestRepository;
    }

    public async Task<(JsonElement? data, IReadOnlyList<ValidationError> errors)> TryExecuteAsync(
        Guid manifestId,
        OperationVector vector,
        CancellationToken ct = default)
    {
        if (!ScreenDataShapeTopologyReader.IsScreenReadAction(vector.Layer, vector.Action))
            return (null, []);

        if (_manifestRepository is null)
        {
            return (null, [
                new ValidationError(
                    "MANIFEST_REPOSITORY_UNAVAILABLE",
                    "Screen data shape query requires manifest repository."),
            ]);
        }

        var manifest = await _manifestRepository.LoadByIdAsync(manifestId, ct);
        if (manifest is null)
        {
            return (null, [
                new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."),
            ]);
        }

        var shapeEntry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntry(manifest.Topology);
        if (shapeEntry is null)
        {
            return (null, [
                new ValidationError(
                    "SCREEN_DATA_SHAPE_MISSING",
                    "Manifest topology has no screen_data_shape entry."),
            ]);
        }

        var entry = shapeEntry.Value;
        var initialRows = ScreenDataShapeQueryEvaluator.ParseInitialDataRows(entry);
        var searchConditions = ParseSearchConditions(entry, vector);
        var havingConditions = ParseHavingConditions(entry, vector);
        var aggregationKey = entry.TryGetProperty("aggregationKey", out var ak) && ak.ValueKind == JsonValueKind.String
            ? ak.GetString()
            : null;
        var measures = ParseAggregationMeasures(entry);
        var displayColumns = ParseStringArray(entry, "displayColumns");
        var displayColumnMode = entry.TryGetProperty("displayColumnMode", out var dcm) && dcm.ValueKind == JsonValueKind.String
            ? dcm.GetString() ?? "selected"
            : displayColumns.Count > 0 ? "selected" : "all";

        var result = ScreenDataShapeQueryEvaluator.Execute(new ScreenDataShapeQueryEvaluator.ScreenDataShapeQueryInput(
            InitialRows: initialRows,
            SearchConditions: searchConditions,
            HavingConditions: havingConditions,
            AggregationKey: aggregationKey,
            AggregationMeasures: measures,
            DisplayColumns: displayColumns,
            DisplayColumnMode: displayColumnMode));

        if (result.Errors.Count > 0)
            return (null, result.Errors);

        var payload = new
        {
            kind = "screen_data_shape_query_result",
            manifestId = manifestId.ToString(),
            rows = result.Rows,
            aggregationResults = result.AggregationResults,
            activeColumns = result.ActiveColumns,
            displayColumnMode,
        };

        return (JsonSerializer.SerializeToElement(payload), []);
    }

    private static List<ScreenDataShapeQueryEvaluator.ResolvedSearchCondition> ParseSearchConditions(
        JsonElement shapeEntry,
        OperationVector vector)
    {
        var wiring = FindWiringEntry(shapeEntry, "searchConditions");
        var list = new List<ScreenDataShapeQueryEvaluator.ResolvedSearchCondition>();
        if (!shapeEntry.TryGetProperty("searchConditions", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        var idx = 0;
        foreach (var cond in arr.EnumerateArray())
        {
            if (cond.ValueKind != JsonValueKind.Object) continue;
            var column = cond.TryGetProperty("column", out var col) ? col.GetString() ?? "" : "";
            var op = cond.TryGetProperty("operator", out var opEl) ? opEl.GetString() ?? "" : "";
            ValueSourceBinding? binding = null;
            if (wiring is not null &&
                wiring.Value.TryGetProperty("bindings", out var bindings) &&
                bindings.ValueKind == JsonValueKind.Array &&
                idx < bindings.GetArrayLength())
            {
                binding = ParseBinding(bindings[idx]);
            }

            var literalValue = cond.TryGetProperty("value", out var valEl) ? valEl.GetString() : null;
            var resolvedValue = binding?.Value is not null
                ? ScreenDataShapeValueResolver.Resolve(binding.Value, literalValue, vector)
                : literalValue;

            string? valueTo = cond.TryGetProperty("valueTo", out var vt) ? vt.GetString() : null;
            if (binding?.ValueTo is not null)
                valueTo = ScreenDataShapeValueResolver.Resolve(binding.ValueTo, valueTo, vector);

            IReadOnlyList<string>? values = null;
            if (cond.TryGetProperty("values", out var valuesEl) && valuesEl.ValueKind == JsonValueKind.Array)
            {
                values = valuesEl.EnumerateArray()
                    .Where(v => v.ValueKind == JsonValueKind.String)
                    .Select(v => v.GetString() ?? "")
                    .ToList();
            }

            var connector = cond.TryGetProperty("logicalConnector", out var lc) ? lc.GetString() : null;
            list.Add(new ScreenDataShapeQueryEvaluator.ResolvedSearchCondition(
                column, op, resolvedValue, valueTo, values, connector));
            idx++;
        }

        return list;
    }

    private static List<ScreenDataShapeQueryEvaluator.ResolvedHavingCondition> ParseHavingConditions(
        JsonElement shapeEntry,
        OperationVector vector)
    {
        var wiring = FindWiringEntry(shapeEntry, "havingConditions");
        var list = new List<ScreenDataShapeQueryEvaluator.ResolvedHavingCondition>();
        if (!shapeEntry.TryGetProperty("havingConditions", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        var idx = 0;
        foreach (var cond in arr.EnumerateArray())
        {
            if (cond.ValueKind != JsonValueKind.Object) continue;
            var column = cond.TryGetProperty("column", out var col) ? col.GetString() ?? "" : "";
            var fn = cond.TryGetProperty("function", out var fnEl) ? fnEl.GetString() ?? "" : "";
            var op = cond.TryGetProperty("operator", out var opEl) ? opEl.GetString() ?? "" : "";
            var literal = cond.TryGetProperty("value", out var valEl) ? valEl.GetString() ?? "" : "";
            ValueSourceBinding? binding = null;
            if (wiring is not null &&
                wiring.Value.TryGetProperty("bindings", out var bindings) &&
                bindings.ValueKind == JsonValueKind.Array &&
                idx < bindings.GetArrayLength())
            {
                binding = ParseBinding(bindings[idx]);
            }

            var resolved = binding?.Value is not null
                ? ScreenDataShapeValueResolver.Resolve(binding.Value, literal, vector) ?? literal
                : literal;
            list.Add(new ScreenDataShapeQueryEvaluator.ResolvedHavingCondition(column, fn, op, resolved));
            idx++;
        }

        return list;
    }

    private sealed record ValueSourceBinding(
        ScreenDataShapeValueResolver.ValueSource? Value,
        ScreenDataShapeValueResolver.ValueSource? ValueTo);

    private static ValueSourceBinding? ParseBinding(JsonElement bindingEl)
    {
        if (bindingEl.ValueKind != JsonValueKind.Object) return null;
        ScreenDataShapeValueResolver.ValueSource? value = null;
        ScreenDataShapeValueResolver.ValueSource? valueTo = null;
        if (bindingEl.TryGetProperty("valueSource", out var vs))
            value = ScreenDataShapeValueResolver.ParseValueSource(vs);
        if (bindingEl.TryGetProperty("valueToSource", out var vts))
            valueTo = ScreenDataShapeValueResolver.ParseValueSource(vts);
        return new ValueSourceBinding(value, valueTo);
    }

    private static JsonElement? FindWiringEntry(JsonElement shapeEntry, string field)
    {
        if (!shapeEntry.TryGetProperty("screenReadQueryWiring", out var wiring) ||
            wiring.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (!wiring.TryGetProperty(field, out var entry))
            return null;
        return entry.ValueKind == JsonValueKind.Object ? entry : null;
    }

    private static List<(string Column, string Function)> ParseAggregationMeasures(JsonElement shapeEntry)
    {
        var list = new List<(string, string)>();
        if (!shapeEntry.TryGetProperty("aggregationMeasures", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var m in arr.EnumerateArray())
        {
            if (m.ValueKind != JsonValueKind.Object) continue;
            var col = m.TryGetProperty("column", out var c) ? c.GetString()?.Trim() : null;
            var fn = m.TryGetProperty("function", out var f) ? f.GetString()?.Trim() : null;
            if (!string.IsNullOrEmpty(col) && !string.IsNullOrEmpty(fn))
                list.Add((col, fn));
        }
        return list;
    }

    private static List<string> ParseStringArray(JsonElement shapeEntry, string property)
    {
        var list = new List<string>();
        if (!shapeEntry.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString()?.Trim();
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
        }
        return list;
    }
}
