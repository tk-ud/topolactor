using System.Text.Json;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ScreenDataShapeQueryEvaluatorTests
{
    [Fact]
    public void Execute_SearchConditions_FiltersRows()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.Ordinal) { ["col"] = "a" },
            new(StringComparer.Ordinal) { ["col"] = "b" },
        };
        var conditions = new List<ScreenDataShapeQueryEvaluator.ResolvedSearchCondition>
        {
            new("col", "=", "b", null, null, null),
        };

        var result = ScreenDataShapeQueryEvaluator.Execute(new ScreenDataShapeQueryEvaluator.ScreenDataShapeQueryInput(
            rows, conditions, [], null, [], [], "all"));

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
        Assert.Equal("b", result.Rows[0]["col"]);
    }

    [Fact]
    public void Execute_DisplayColumnMode_None_ReturnsNoRowColumns()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.Ordinal) { ["col"] = "a" },
        };
        var result = ScreenDataShapeQueryEvaluator.Execute(new ScreenDataShapeQueryEvaluator.ScreenDataShapeQueryInput(
            rows, [], [], null, [], [], "none"));

        Assert.Empty(result.Rows);
        Assert.Empty(result.ActiveColumns);
    }

    [Fact]
    public void Execute_HavingConditions_FiltersAggregationGroups()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.Ordinal) { ["grp"] = "x", ["salary"] = "100" },
            new(StringComparer.Ordinal) { ["grp"] = "x", ["salary"] = "200" },
            new(StringComparer.Ordinal) { ["grp"] = "y", ["salary"] = "50" },
        };
        var measures = new List<(string, string)> { ("salary", "sum") };
        var having = new List<ScreenDataShapeQueryEvaluator.ResolvedHavingCondition>
        {
            new("salary", "sum", ">", "150"),
        };

        var result = ScreenDataShapeQueryEvaluator.Execute(new ScreenDataShapeQueryEvaluator.ScreenDataShapeQueryInput(
            rows, [], having, "grp", measures, ["grp", "salary"], "all"));

        Assert.Empty(result.Errors);
        Assert.Single(result.AggregationResults);
        Assert.Equal("x", result.AggregationResults[0]["groupKey"]);
    }

    [Fact]
    public void Execute_UnknownOperator_ReturnsExplicitError()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.Ordinal) { ["col"] = "a" },
        };
        var conditions = new List<ScreenDataShapeQueryEvaluator.ResolvedSearchCondition>
        {
            new("col", "INVALID_OP", "a", null, null, null),
        };

        var result = ScreenDataShapeQueryEvaluator.Execute(new ScreenDataShapeQueryEvaluator.ScreenDataShapeQueryInput(
            rows, conditions, [], null, [], [], "selected"));

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Code == "SEARCH_OPERATOR_UNKNOWN");
    }

    [Fact]
    public void ValueResolver_RuntimeParam_ReadsFromPayload()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            runtimeParams = new { status = "active" },
        });
        var vector = new OperationVector(
            "t", "l", "Read", "t:l:read", "admin", payload, null);
        var source = new ScreenDataShapeValueResolver.ValueSource("runtimeParam", "status", null);
        var resolved = ScreenDataShapeValueResolver.Resolve(source, "fallback", vector);
        Assert.Equal("active", resolved);
    }
}
