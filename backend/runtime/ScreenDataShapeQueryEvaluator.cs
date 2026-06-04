using System.Globalization;
using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Evaluates screen_data_shape search / aggregation / display intent on in-memory rows.
/// Uses operator vocabulary only — no raw SQL. Mirrors frontend searchConditionEval contract.
/// </summary>
public static class ScreenDataShapeQueryEvaluator
{
    private static readonly HashSet<string> SearchOperators = new(StringComparer.Ordinal)
    {
        "=", "!=", "<>", "like", "ilike", "not like",
        ">", ">=", "<", "<=",
        "between", "in", "not in",
        "is null", "is not null",
    };

    private static readonly HashSet<string> HavingOperators = new(StringComparer.Ordinal)
    {
        "=", "!=", "<>", ">", ">=", "<", "<=",
    };

    public sealed record ResolvedSearchCondition(
        string Column,
        string Operator,
        string? Value,
        string? ValueTo,
        IReadOnlyList<string>? Values,
        string? LogicalConnector);

    public sealed record ResolvedHavingCondition(
        string Column,
        string Function,
        string Operator,
        string Value);

    public sealed record ScreenDataShapeQueryInput(
        IReadOnlyList<Dictionary<string, string>> InitialRows,
        IReadOnlyList<ResolvedSearchCondition> SearchConditions,
        IReadOnlyList<ResolvedHavingCondition> HavingConditions,
        string? AggregationKey,
        IReadOnlyList<(string Column, string Function)> AggregationMeasures,
        IReadOnlyList<string> DisplayColumns,
        string DisplayColumnMode);

    public sealed record ScreenDataShapeQueryResult(
        IReadOnlyList<Dictionary<string, string>> Rows,
        IReadOnlyList<Dictionary<string, object?>> AggregationResults,
        IReadOnlyList<string> ActiveColumns,
        IReadOnlyList<ValidationError> Errors);

    public static ScreenDataShapeQueryResult Execute(ScreenDataShapeQueryInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new List<ValidationError>();

        foreach (var cond in input.SearchConditions)
        {
            if (!SearchOperators.Contains(cond.Operator))
            {
                errors.Add(new ValidationError(
                    "SEARCH_OPERATOR_UNKNOWN",
                    $"searchConditions operator '{cond.Operator}' is not in the SSOT vocabulary."));
            }
        }

        foreach (var hc in input.HavingConditions)
        {
            if (!HavingOperators.Contains(hc.Operator))
            {
                errors.Add(new ValidationError(
                    "HAVING_OPERATOR_UNKNOWN",
                    $"havingConditions operator '{hc.Operator}' is not in the SSOT vocabulary."));
            }
        }

        if (errors.Count > 0)
        {
            return new ScreenDataShapeQueryResult([], [], [], errors);
        }

        var filtered = ApplySearchConditions(input.InitialRows, input.SearchConditions);
        var aggregationResults = new List<Dictionary<string, object?>>();

        if (!string.IsNullOrWhiteSpace(input.AggregationKey) && input.AggregationMeasures.Count > 0)
        {
            var groups = GroupRows(filtered, input.AggregationKey);
            groups = ApplyHavingConditions(groups, input.HavingConditions, input.AggregationMeasures);
            foreach (var (groupKey, rows) in groups)
            {
                var entry = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["groupKey"] = groupKey,
                };
                foreach (var (column, function) in input.AggregationMeasures)
                {
                    var nums = rows
                        .Select(r => TryParseNumber(r.GetValueOrDefault(column)))
                        .Where(n => n.HasValue)
                        .Select(n => n!.Value)
                        .ToList();
                    entry[$"{column}:{function}"] = ComputeMeasure(nums, function);
                }
                aggregationResults.Add(entry);
            }
        }
        else if (input.AggregationMeasures.Count > 0 && filtered.Count > 0)
        {
            var entry = new Dictionary<string, object?>(StringComparer.Ordinal) { ["groupKey"] = "(全体)" };
            foreach (var (column, function) in input.AggregationMeasures)
            {
                var nums = filtered
                    .Select(r => TryParseNumber(r.GetValueOrDefault(column)))
                    .Where(n => n.HasValue)
                    .Select(n => n!.Value)
                    .ToList();
                entry[$"{column}:{function}"] = ComputeMeasure(nums, function);
            }
            aggregationResults.Add(entry);
        }

        var activeColumns = ResolveActiveColumns(
            filtered,
            input.DisplayColumnMode,
            input.DisplayColumns);

        var displayRows = ProjectRows(filtered, activeColumns);

        return new ScreenDataShapeQueryResult(displayRows, aggregationResults, activeColumns, errors);
    }

    private static IReadOnlyList<Dictionary<string, string>> ApplySearchConditions(
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyList<ResolvedSearchCondition> conditions)
    {
        if (conditions.Count == 0) return rows;

        return rows.Where(row =>
        {
            var result = true;
            for (var i = 0; i < conditions.Count; i++)
            {
                var cond = conditions[i];
                row.TryGetValue(cond.Column, out var cellVal);
                cellVal ??= "";
                var match = EvaluateSearchCell(cond, cellVal);

                if (i == 0)
                    result = match;
                else
                {
                    var conn = conditions[i - 1].LogicalConnector ?? "and";
                    result = conn switch
                    {
                        "or" => result || match,
                        "not" => result && !match,
                        _ => result && match,
                    };
                }
            }
            return result;
        }).ToList();
    }

    private static bool EvaluateSearchCell(ResolvedSearchCondition cond, string cellVal)
    {
        return cond.Operator switch
        {
            "=" => cellVal == (cond.Value ?? ""),
            "!=" or "<>" => cellVal != (cond.Value ?? ""),
            "like" or "ilike" => cellVal.Contains(cond.Value ?? "", StringComparison.OrdinalIgnoreCase),
            "not like" => !cellVal.Contains(cond.Value ?? "", StringComparison.OrdinalIgnoreCase),
            ">" => CompareNumber(cellVal, cond.Value) > 0,
            ">=" => CompareNumber(cellVal, cond.Value) >= 0,
            "<" => CompareNumber(cellVal, cond.Value) < 0,
            "<=" => CompareNumber(cellVal, cond.Value) <= 0,
            "between" => CompareNumber(cellVal, cond.Value) >= 0 &&
                         CompareNumber(cellVal, cond.ValueTo) <= 0,
            "in" => (cond.Values ?? []).Contains(cellVal),
            "not in" => !(cond.Values ?? []).Contains(cellVal),
            "is null" => string.IsNullOrEmpty(cellVal),
            "is not null" => !string.IsNullOrEmpty(cellVal),
            _ => true,
        };
    }

    private static Dictionary<string, List<Dictionary<string, string>>> GroupRows(
        IReadOnlyList<Dictionary<string, string>> rows,
        string aggregationKey)
    {
        var groups = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = row.GetValueOrDefault(aggregationKey)?.Trim();
            if (string.IsNullOrEmpty(key)) key = "(空)";
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }
            list.Add(row);
        }
        return groups;
    }

    private static Dictionary<string, List<Dictionary<string, string>>> ApplyHavingConditions(
        Dictionary<string, List<Dictionary<string, string>>> groups,
        IReadOnlyList<ResolvedHavingCondition> havingConditions,
        IReadOnlyList<(string Column, string Function)> measures)
    {
        if (havingConditions.Count == 0) return groups;

        var filtered = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.Ordinal);
        foreach (var (key, rows) in groups)
        {
            var keep = true;
            foreach (var hc in havingConditions)
            {
                var nums = rows
                    .Select(r => TryParseNumber(r.GetValueOrDefault(hc.Column)))
                    .Where(n => n.HasValue)
                    .Select(n => n!.Value)
                    .ToList();
                var measureVal = ComputeMeasure(nums, hc.Function);
                if (measureVal is null)
                {
                    keep = false;
                    break;
                }

                if (!double.TryParse(hc.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var threshold))
                {
                    keep = false;
                    break;
                }

                keep = hc.Operator switch
                {
                    "=" => Math.Abs(measureVal.Value - threshold) < 1e-9,
                    "!=" or "<>" => Math.Abs(measureVal.Value - threshold) >= 1e-9,
                    ">" => measureVal > threshold,
                    ">=" => measureVal >= threshold,
                    "<" => measureVal < threshold,
                    "<=" => measureVal <= threshold,
                    _ => keep,
                };
                if (!keep) break;
            }
            if (keep) filtered[key] = rows;
        }
        return filtered;
    }

    private static IReadOnlyList<string> ResolveActiveColumns(
        IReadOnlyList<Dictionary<string, string>> rows,
        string displayColumnMode,
        IReadOnlyList<string> displayColumns)
    {
        return displayColumnMode switch
        {
            "none" => [],
            "all" => CollectAllColumnKeys(rows),
            _ => displayColumns.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).ToList(),
        };
    }

    private static List<string> CollectAllColumnKeys(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            foreach (var key in row.Keys)
                keys.Add(key);
        }
        return keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    private static List<Dictionary<string, string>> ProjectRows(
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyList<string> activeColumns)
    {
        if (activeColumns.Count == 0) return [];
        return rows.Select(row =>
        {
            var projected = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var col in activeColumns)
                projected[col] = row.GetValueOrDefault(col) ?? "";
            return projected;
        }).ToList();
    }

    private static double? TryParseNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static int CompareNumber(string cellVal, string? bound)
    {
        var left = TryParseNumber(cellVal) ?? 0;
        var right = TryParseNumber(bound) ?? 0;
        return left.CompareTo(right);
    }

    private static double? ComputeMeasure(IReadOnlyList<double> values, string function)
    {
        if (values.Count == 0) return null;
        return function.ToLowerInvariant() switch
        {
            "sum" => values.Sum(),
            "avg" => values.Average(),
            "max" => values.Max(),
            "min" => values.Min(),
            "count" => values.Count,
            _ => null,
        };
    }

    public static List<Dictionary<string, string>> ParseInitialDataRows(JsonElement shapeEntry)
    {
        var rows = new List<Dictionary<string, string>>();
        if (!shapeEntry.TryGetProperty("initialDataRows", out var rowsEl) ||
            rowsEl.ValueKind != JsonValueKind.Array)
        {
            return rows;
        }

        foreach (var rowEl in rowsEl.EnumerateArray())
        {
            if (rowEl.ValueKind == JsonValueKind.Object &&
                rowEl.TryGetProperty("values", out var valuesEl) &&
                valuesEl.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var prop in valuesEl.EnumerateObject())
                    dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.ToString();
                rows.Add(dict);
                continue;
            }

            if (rowEl.ValueKind == JsonValueKind.Object)
            {
                var flat = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var prop in rowEl.EnumerateObject())
                    flat[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.ToString();
                rows.Add(flat);
            }
        }

        return rows;
    }
}
