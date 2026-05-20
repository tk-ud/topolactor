using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Scheduler;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// SSE E2E tests covering:
///   SseEventBroadcaster fan-out (subscribe, broadcast, unsubscribe)
///   SseEndpoint SSE format output (event: ...\ndata: ...\n\n)
///
/// Pipeline under test:
///   Broadcast(event) → subscriber channel → SseEndpoint.StreamAsync → response body
///
/// Out of scope: DbNotifyListener → PostgreSQL (requires live DB).
/// </summary>
public class SseEventBroadcasterTests
{
    [Fact]
    public void Subscribe_ReturnsIsolatedChannel()
    {
        var broadcaster = new SseEventBroadcaster();

        var ch1 = broadcaster.Subscribe();
        var ch2 = broadcaster.Subscribe();

        Assert.NotSame(ch1, ch2);
        Assert.Equal(2, broadcaster.SubscriberCount);
    }

    [Fact]
    public void Broadcast_DeliversEventToAllSubscribers()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch1 = broadcaster.Subscribe();
        var ch2 = broadcaster.Subscribe();

        broadcaster.Broadcast(new SseEvent("projection", """{"manifest_id":"test"}"""));

        Assert.True(ch1.Reader.TryRead(out var e1));
        Assert.True(ch2.Reader.TryRead(out var e2));
        Assert.Equal("projection", e1.EventType);
        Assert.Equal("projection", e2.EventType);
        Assert.Equal("""{"manifest_id":"test"}""", e1.Data);
    }

    [Fact]
    public void Broadcast_NoSubscribers_DoesNotThrow()
    {
        var broadcaster = new SseEventBroadcaster();
        var ex = Record.Exception(() => broadcaster.Broadcast(new SseEvent("projection", "{}")));
        Assert.Null(ex);
    }

    [Fact]
    public void Unsubscribe_RemovesChannelFromFanOut()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch = broadcaster.Subscribe();
        broadcaster.Unsubscribe(ch);

        broadcaster.Broadcast(new SseEvent("projection", "{}"));

        Assert.Equal(0, broadcaster.SubscriberCount);
        Assert.False(ch.Reader.TryRead(out _));
    }

    [Fact]
    public void Broadcast_FullChannel_DropsOldestWithoutBlocking()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch = broadcaster.Subscribe();

        // Channel capacity is 64; overflow should drop oldest without blocking.
        for (var i = 0; i < 70; i++)
        {
            broadcaster.Broadcast(new SseEvent("projection", $"{{\"i\":{i}}}"));
        }

        // Should still be able to read without hanging.
        Assert.True(ch.Reader.TryRead(out _));
    }
}

/// <summary>
/// SSE streaming format tests using a fake HttpResponse backed by MemoryStream.
/// Verifies that SseEndpoint writes the correct SSE wire format.
/// </summary>
public class SseEndpointStreamFormatTests
{
    [Fact]
    public async Task StreamAsync_WithoutBroadcaster_WritesPingAndNoMoreEvents()
    {
        // Arrange
        var (response, body) = MakeFakeResponse();
        var endpoint = new Topolactor.Endpoint.SseEndpoint(
            NullLogger<Topolactor.Endpoint.SseEndpoint>.Instance,
            broadcaster: null);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act — let the keep-alive loop run until cancelled
        try { await endpoint.StreamAsync(response, cts.Token); }
        catch (OperationCanceledException) { }

        // Assert
        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("event: ping\n", text);
        Assert.Contains("data: connected\n\n", text);
    }

    [Fact]
    public async Task StreamAsync_WithBroadcaster_ReceivesEventAndWritesSseFormat()
    {
        // Arrange
        var broadcaster = new SseEventBroadcaster();
        var (response, body) = MakeFakeResponse();
        var endpoint = new Topolactor.Endpoint.SseEndpoint(
            NullLogger<Topolactor.Endpoint.SseEndpoint>.Instance,
            broadcaster);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act: start streaming in background, broadcast one event, then cancel.
        var streamTask = Task.Run(async () =>
        {
            try { await endpoint.StreamAsync(response, cts.Token); }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(20, CancellationToken.None);
        broadcaster.Broadcast(new SseEvent("projection", """{"table_id":"t1"}"""));
        await Task.Delay(20, CancellationToken.None);
        await cts.CancelAsync();

        try { await streamTask; } catch (OperationCanceledException) { }

        // Assert SSE wire format
        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("event: projection\n", text);
        Assert.Contains("""data: {"table_id":"t1"}""" + "\n\n", text);
    }

    private static (HttpResponse response, MemoryStream body) MakeFakeResponse()
    {
        var ctx = new DefaultHttpContext();
        var body = new MemoryStream();
        ctx.Response.Body = body;
        return (ctx.Response, body);
    }
}
