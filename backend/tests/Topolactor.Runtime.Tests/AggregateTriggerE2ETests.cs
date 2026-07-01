using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AggregateTriggerE2ETests
{
    [Fact]
    public async Task ManifestDispatcher_RoutesAggregateTriggerRuntimeDestination()
    {
        var runtime = new AggregateTriggerRuntime(new InMemoryAggregateTriggerRepository());
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepository: null,
            extraHandlers: new Dictionary<string, IDispatchableRuntime>{{"aggregate_trigger_runtime", runtime}});
        var response = await runtime.ExecuteAsync(AggregateTriggerSubstrateTestsAccessor.Request("event-1"), null);
        Assert.True(response.Success);
    }
}

internal static class AggregateTriggerSubstrateTestsAccessor
{
    public static Topolactor.Schema.EndpointRequestDto Request(string eventId)
    {
        var method = typeof(AggregateTriggerSubstrateTests).GetMethod("BuildRequest", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        return (Topolactor.Schema.EndpointRequestDto)method.Invoke(null, [eventId, null])!;
    }
}
