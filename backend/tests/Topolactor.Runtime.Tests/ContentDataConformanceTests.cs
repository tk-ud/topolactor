using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ContentDataConformanceTests
{
    [Fact]
    public void ValidateRow_numericMismatch_returnsTypeError()
    {
        var row = new Dictionary<string, object?> { ["age"] = "abc" };
        var fields = new List<ContentDataConformance.SchemaField>
        {
            new("age", "integer", false),
        };
        var errors = ContentDataConformance.ValidateRow(row, fields);
        Assert.Contains(errors, e => e.Contains("not numeric", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRow_requiredEmpty_returnsRequiredError()
    {
        var row = new Dictionary<string, object?> { ["name"] = "" };
        var fields = new List<ContentDataConformance.SchemaField>
        {
            new("name", "text", true),
        };
        var errors = ContentDataConformance.ValidateRow(row, fields);
        Assert.Contains(errors, e => e.Contains("required", StringComparison.OrdinalIgnoreCase));
    }
}
