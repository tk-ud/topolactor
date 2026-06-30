using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// ManifestDispatcher secondary-boundary connection: an unregistered runtime destination is a
/// substrate configuration failure (system error) and is recorded into the same logs.error
/// evidence envelope. Ordinary not-found / capability rejects are not exercised here.
/// </summary>
public class ManifestDispatcherErrorEvidenceTests
{
    [Fact]
    public async Task UnregisteredRuntimeDestination_RecordsSystemError()
    {
        var appender = new FakeBackendErrorEvidenceAppender();
        // Dev-bypass path (null manifest repository): an empty handler registry makes the fallthrough
        // destination 'topology_transform_runtime' unregistered -> RUNTIME_DESTINATION_UNKNOWN.
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            new Dictionary<string, IDispatchableRuntime>(),
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepository: null,
            errorAppender: appender);

        var request = new EndpointRequestDto(
            "Search", "screen", "screen_list", "Search",
            IdOrHubId: null, Payload: null, Context: null, TriggerKind: "client", Role: null);

        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "RUNTIME_DESTINATION_UNKNOWN");
        Assert.Equal(1, appender.AppendCount);
        Assert.True(appender.Appended.TryDequeue(out var evidence));
        Assert.Equal(BackendErrorOriginLayer.ManifestDispatcher, evidence!.OriginLayer);
        Assert.Equal("RUNTIME_DESTINATION_UNKNOWN", evidence.ErrorCode);
    }
}
