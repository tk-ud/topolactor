using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Scheduler;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Runtime handler for db_notify projection events.
/// Registered as "sse_projection_runtime" in ManifestDispatcher handler dict.
///
/// Per SSOT notify_listen_contract.db_listen: db_notify events enter the scheduler as
/// hook triggers before reaching the projection runtime. This class is that projection runtime.
///
/// ExecuteAsync broadcasts the db_notify payload to all connected SSE clients.
/// Returns Success=true (fire-and-forget; non-client hook, response not observed by caller).
/// </summary>
public sealed class SseProjectionRuntime : IDispatchableRuntime
{
    private readonly ILogger<SseProjectionRuntime> _logger;
    private readonly SseEventBroadcaster _broadcaster;

    public SseProjectionRuntime(
        ILogger<SseProjectionRuntime> logger,
        SseEventBroadcaster broadcaster)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
    }

    public Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request,
        Guid? manifestId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string payloadJson;
        if (request.Payload.HasValue && request.Payload.Value.ValueKind != JsonValueKind.Undefined)
        {
            payloadJson = request.Payload.Value.GetRawText();
        }
        else
        {
            payloadJson = "{}";
        }

        _logger.LogDebug(
            "SseProjectionRuntime: broadcasting SSE projection event. subscribers={Count}",
            _broadcaster.SubscriberCount);

        _broadcaster.Broadcast(new SseEvent(EventType: "projection", Data: payloadJson));

        return Task.FromResult(new EndpointResponseDto(
            Success: true,
            Emission: null,
            Errors: []));
    }
}
