using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AggregateTriggerE2ETests
{
    [Fact]
    public async Task ManifestDispatcher_RoutesAggregateTriggerRuntimeDestination()
    {
        var runtime = new AggregateTriggerRuntime(new InMemoryAggregateTriggerRepository());
        var manifestRepository = new InMemoryManifestAdminRepository();
        manifestRepository.Seed(new ManifestDetailRecord(
            Guid.NewGuid(),
            null,
            [
                JsonSerializer.SerializeToElement(new { type = "dispatcher_mapping", role = "admin", target = "aggregate_trigger", layer = "runtime", action = "execute" }),
                JsonSerializer.SerializeToElement(new { type = "runtime_mapping", runtime_destination = "aggregate_trigger_runtime" })
            ],
            "active",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepository,
            new Dictionary<string, IDispatchableRuntime>{{"aggregate_trigger_runtime", runtime}});

        var response = await dispatcher.DispatchAsync(AggregateTriggerSubstrateTestsAccessor.Request("event-1"));

        Assert.True(response.Success);
        Assert.Contains("threshold_not_satisfied", response.Emission!.Data!.Value.GetRawText());
    }
}

internal static class AggregateTriggerSubstrateTestsAccessor
{
    public static EndpointRequestDto Request(string eventId)
    {
        var method = typeof(AggregateTriggerSubstrateTests).GetMethod("BuildRequest", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        return (EndpointRequestDto)method.Invoke(null, [eventId, null, null, null])!;
    }
}
