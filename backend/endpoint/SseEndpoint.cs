using Microsoft.Extensions.Logging;

namespace Topolactor.Endpoint;

/// <summary>
/// SSE projection endpoint. Streams topology events to the frontend projection lane.
///
/// Skeleton: currently streams keep-alive ping events only. Actual topology events
/// require the notify_listen contract (db_notify → backend_scheduler → sse_emitter)
/// to be implemented. The endpoint shape and Content-Type boundary are established here.
///
/// Lane: sse_projection_lane in pipeline-continuity-ssot.yaml (backend.sse_emitter node).
/// </summary>
public class SseEndpoint
{
    private readonly ILogger<SseEndpoint> _logger;

    public SseEndpoint(ILogger<SseEndpoint> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Writes an SSE stream to the response. Sends a keep-alive ping and holds
    /// the connection open until cancelled.
    /// </summary>
    public async Task StreamAsync(HttpResponse response, CancellationToken ct = default)
    {
        response.Headers["Content-Type"] = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        _logger.LogDebug("SseEndpoint: client connected to SSE stream.");

        try
        {
            await WriteSseEventAsync(response, "ping", "connected", ct);
            await response.Body.FlushAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                await WriteSseEventAsync(response, "ping", "keep-alive", ct);
                await response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SseEndpoint: client disconnected.");
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
