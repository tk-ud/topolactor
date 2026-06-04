using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ScreenDataShapeQueryRuntimeTests
{
    [Fact]
    public async Task TryExecuteAsync_NonScreenReadAction_SkipsWithoutManifestRepository()
    {
        var runtime = new ScreenDataShapeQueryRuntime(manifestRepository: null);
        var vector = new OperationVector(
            "default", "entity", "Search", "default:entity:search", null, null, null);

        var (data, errors) = await runtime.TryExecuteAsync(Guid.NewGuid(), vector);

        Assert.Empty(errors);
        Assert.Null(data);
    }

    [Fact]
    public async Task TryExecuteAsync_AppliesSearchConditions_OnInitialDataRows()
    {
        var manifestId = Guid.NewGuid();
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new
            {
                type = "dispatcher_mapping",
                role = "admin",
                target = "screen_test",
                layer = "screen_list",
                action = "Read",
            }),
            JsonSerializer.SerializeToElement(new
            {
                type = "runtime_mapping",
                runtime_destination = "topology_transform_runtime",
            }),
            JsonSerializer.SerializeToElement(new
            {
                type = "screen_data_shape",
                initialDataRows = new[]
                {
                    new { values = new Dictionary<string, string> { ["col"] = "keep" } },
                    new { values = new Dictionary<string, string> { ["col"] = "drop" } },
                },
                searchConditions = new[]
                {
                    new { column = "col", @operator = "=", value = "keep" },
                },
                displayColumnMode = "all",
                screenReadQueryWiring = new
                {
                    searchConditions = new
                    {
                        wiringKey = "screen.searchConditions",
                        bindings = new[]
                        {
                            new
                            {
                                wiringKey = "screen.searchConditions[0]",
                                valueSource = new { kind = "literal", sampleValue = "keep" },
                            },
                        },
                    },
                },
            }),
        };

        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(
            manifestId,
            null,
            topology,
            "active",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));

        var runtime = new ScreenDataShapeQueryRuntime(repo);
        var vector = new OperationVector(
            "screen_test", "screen_list", "Read", "screen_test:screen_list:read", "admin", null, null);

        var (data, errors) = await runtime.TryExecuteAsync(manifestId, vector);

        Assert.Empty(errors);
        Assert.NotNull(data);
        Assert.True(data!.Value.TryGetProperty("rows", out var rows));
        Assert.Equal(1, rows.GetArrayLength());
    }
}
