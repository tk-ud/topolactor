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

        await WaitUntilBodyContains(body, "event: ping\n", TimeSpan.FromMilliseconds(150));
        broadcaster.Broadcast(new SseEvent("projection", """{"table_id":"t1"}"""));
        await WaitUntilBodyContains(body, "event: projection\n", TimeSpan.FromMilliseconds(150));
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

    private static async Task WaitUntilBodyContains(
        MemoryStream body,
        string expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var text = Encoding.UTF8.GetString(body.ToArray());
            if (text.Contains(expected, StringComparison.Ordinal))
                return;

            await Task.Delay(5, CancellationToken.None);
        }

        var snapshot = Encoding.UTF8.GetString(body.ToArray());
        throw new Xunit.Sdk.XunitException(
            $"Timed out waiting for SSE body to contain '{expected}'. Current body: '{snapshot}'");
    }
}

/// <summary>
/// DbNotifyListener payload handling tests.
/// Verifies that HandleNotificationPayload broadcasts the correct SseEvent
/// without requiring a live PostgreSQL connection.
///
/// Known gap (Gap-7): full DbNotifyListener → pg_notify → broadcaster → SSE E2E
/// path requires a live DB and is not covered here.
/// Known gap (Gap-7): per SSOT, db_notify events should enter the scheduler queue
/// as hook_triggers before SSE emission (scheduler routing not yet implemented).
/// </summary>
public class DbNotifyListenerPayloadTests
{
    private sealed class TestableDbNotifyListener : DbNotifyListener
    {
        public TestableDbNotifyListener(SseEventBroadcaster broadcaster)
            : base(NullLogger<DbNotifyListener>.Instance, "test-double", broadcaster) { }

        public void SimulateNotification(string payload) => HandleNotificationPayload(payload);
    }

    [Fact]
    public void HandleNotificationPayload_BroadcastsProjectionEvent()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch = broadcaster.Subscribe();
        var listener = new TestableDbNotifyListener(broadcaster);

        listener.SimulateNotification("""{"table_id":"t1","manifest_id":"m1"}""");

        Assert.True(ch.Reader.TryRead(out var evt));
        Assert.Equal("projection", evt.EventType);
        Assert.Equal("""{"table_id":"t1","manifest_id":"m1"}""", evt.Data);
    }

    [Fact]
    public void HandleNotificationPayload_EmptyPayload_BroadcastsEmptyJson()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch = broadcaster.Subscribe();
        var listener = new TestableDbNotifyListener(broadcaster);

        listener.SimulateNotification("{}");

        Assert.True(ch.Reader.TryRead(out var evt));
        Assert.Equal("projection", evt.EventType);
        Assert.Equal("{}", evt.Data);
    }

    [Fact]
    public void HandleNotificationPayload_MultipleSubscribers_AllReceiveEvent()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch1 = broadcaster.Subscribe();
        var ch2 = broadcaster.Subscribe();
        var listener = new TestableDbNotifyListener(broadcaster);

        listener.SimulateNotification("""{"table_id":"t2"}""");

        Assert.True(ch1.Reader.TryRead(out var e1));
        Assert.True(ch2.Reader.TryRead(out var e2));
        Assert.Equal("projection", e1.EventType);
        Assert.Equal("projection", e2.EventType);
        Assert.Equal("""{"table_id":"t2"}""", e1.Data);
        Assert.Equal("""{"table_id":"t2"}""", e2.Data);
    }
}
