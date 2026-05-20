using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Topolactor.Scheduler;

namespace Topolactor.Endpoint;

/// <summary>
/// SSE projection endpoint. Streams topology events to the frontend projection lane.
///
/// Per SSOT sse_projection_lane:
///   backend.sse_emitter -> subscribes to SseEventBroadcaster per connection
///   fed by DbNotifyListener (background service) as hook_triggers.
///
/// Per-connection: each client gets an isolated Channel via SseEventBroadcaster.Subscribe().
/// Multi-instance leakage: no shared state between connections.
/// Keep-alive pings are sent every 30 seconds to prevent proxy timeouts.
///
/// Lane: sse_projection_lane in pipeline-continuity-ssot.yaml (backend.sse_emitter node).
/// </summary>
public class SseEndpoint
{
    private readonly ILogger<SseEndpoint> _logger;
    private readonly SseEventBroadcaster? _broadcaster;

    public SseEndpoint(ILogger<SseEndpoint> logger, SseEventBroadcaster? broadcaster = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _broadcaster = broadcaster;
    }

    /// <summary>
    /// Writes an SSE stream to the response. Sends a keep-alive ping, then streams
    /// projection events from a per-connection subscriber channel until the client disconnects.
    /// </summary>
    public async Task StreamAsync(HttpResponse response, CancellationToken ct = default)
    {
        response.Headers["Content-Type"] = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        _logger.LogDebug("SseEndpoint: client connected to SSE stream.");

        Channel<SseEvent>? subscriberChannel = null;
        if (_broadcaster is not null)
        {
            subscriberChannel = _broadcaster.Subscribe();
        }

        try
        {
            await WriteSseEventAsync(response, "ping", "connected", ct);
            await response.Body.FlushAsync(ct);

            if (subscriberChannel is not null)
            {
                await StreamSubscriberEventsAsync(response, subscriberChannel, ct);
            }
            else
            {
                await KeepAlivePingLoopAsync(response, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SseEndpoint: client disconnected.");
        }
        finally
        {
            if (subscriberChannel is not null && _broadcaster is not null)
            {
                _broadcaster.Unsubscribe(subscriberChannel);
                _logger.LogDebug("SseEndpoint: unsubscribed from broadcaster.");
            }
        }
    }

    private async Task StreamSubscriberEventsAsync(
        HttpResponse response,
        Channel<SseEvent> channel,
        CancellationToken ct)
    {
        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pingTask = KeepAlivePingLoopAsync(response, pingCts.Token);

        try
        {
            await foreach (var sseEvent in channel.Reader.ReadAllAsync(ct))
            {
                await WriteSseEventAsync(response, sseEvent.EventType, sseEvent.Data, ct);
                await response.Body.FlushAsync(ct);
            }
        }
        finally
        {
            await pingCts.CancelAsync();
            try { await pingTask; } catch (OperationCanceledException) { }
        }
    }

    private async Task KeepAlivePingLoopAsync(HttpResponse response, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            await WriteSseEventAsync(response, "ping", "keep-alive", ct);
            await response.Body.FlushAsync(ct);
        }
    }

    private static async Task WriteSseEventAsync(
        HttpResponse response,
        string eventType,
        string data,
        CancellationToken ct)
    {
        await response.WriteAsync($"event: {eventType}\ndata: {data}\n\n", ct);
    }
}
