using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Builds screen_read_query_wiring nested under screen_data_shape for UI Builder event binding.
/// Separates preview sample literals from runtime value sources.
/// </summary>
public static class ScreenReadQueryWiringBuilder
{
    public static JsonElement Build(
        IReadOnlyList<AdminManifestSearchConditionDto>? searchConditions,
        IReadOnlyList<AdminManifestHavingConditionDto>? havingConditions,
        IReadOnlyList<AdminManifestAggregationMeasureDto>? aggregationMeasures,
        IReadOnlyList<string>? displayColumns,
        string? displayColumnMode)
    {
        var wiring = new Dictionary<string, object?>
        {
            ["searchConditions"] = BuildSearchWiring(searchConditions),
            ["havingConditions"] = BuildHavingWiring(havingConditions),
            ["aggregationMeasures"] = BuildMeasureWiring(aggregationMeasures),
            ["displayColumns"] = BuildDisplayColumnWiring(displayColumns),
            ["displayColumnMode"] = new Dictionary<string, object?>
            {
                ["wiringKey"] = "screen.displayColumnMode",
                ["mode"] = displayColumnMode ?? "selected",
            },
        };
        return JsonSerializer.SerializeToElement(wiring);
    }

    private static object BuildSearchWiring(IReadOnlyList<AdminManifestSearchConditionDto>? conditions)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return new { wiringKey = "screen.searchConditions", bindings = Array.Empty<object>() };
        }

        var bindings = conditions.Select((c, i) => new Dictionary<string, object?>
        {
            ["wiringKey"] = $"screen.searchConditions[{i}]",
            ["column"] = c.Column,
            ["operator"] = c.Operator,
            ["valueSource"] = c.ValueSource is not null
                ? MapValueSource(c.ValueSource)
                : new Dictionary<string, object?> { ["kind"] = "literal", ["sampleValue"] = c.Value ?? "" },
            ["valueToSource"] = c.ValueToSource is not null
                ? MapValueSource(c.ValueToSource)
                : c.ValueTo is not null
                    ? new Dictionary<string, object?> { ["kind"] = "literal", ["sampleValue"] = c.ValueTo }
                    : null,
        }).ToList();

        return new { wiringKey = "screen.searchConditions", bindings };
    }

    private static object BuildHavingWiring(IReadOnlyList<AdminManifestHavingConditionDto>? conditions)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return new { wiringKey = "screen.havingConditions", bindings = Array.Empty<object>() };
        }

        var bindings = conditions.Select((c, i) => new Dictionary<string, object?>
        {
            ["wiringKey"] = $"screen.havingConditions[{i}]",
            ["column"] = c.Column,
            ["function"] = c.Function,
            ["operator"] = c.Operator,
            ["valueSource"] = c.ValueSource is not null
                ? MapValueSource(c.ValueSource)
                : new Dictionary<string, object?> { ["kind"] = "literal", ["sampleValue"] = c.Value },
        }).ToList();

        return new { wiringKey = "screen.havingConditions", bindings };
    }

    private static object BuildMeasureWiring(IReadOnlyList<AdminManifestAggregationMeasureDto>? measures)
    {
        if (measures is null || measures.Count == 0)
        {
            return new { wiringKey = "screen.aggregationMeasures", bindings = Array.Empty<object>() };
        }

        var bindings = measures.Select((m, i) => new Dictionary<string, object?>
        {
            ["wiringKey"] = $"screen.aggregationMeasures[{i}]",
            ["column"] = m.Column,
            ["function"] = m.Function,
        }).ToList();

        return new { wiringKey = "screen.aggregationMeasures", bindings };
    }

    private static object BuildDisplayColumnWiring(IReadOnlyList<string>? displayColumns)
    {
        var cols = displayColumns ?? [];
        var bindings = cols.Select((c, i) => new Dictionary<string, object?>
        {
            ["wiringKey"] = $"screen.displayColumns[{i}]",
            ["column"] = c,
        }).ToList();
        return new { wiringKey = "screen.displayColumns", bindings };
    }

    private static Dictionary<string, object?> MapValueSource(AdminManifestConditionValueSourceDto source) =>
        new()
        {
            ["kind"] = source.Kind,
            ["key"] = source.Key,
            ["sampleValue"] = source.SampleValue,
        };
}
