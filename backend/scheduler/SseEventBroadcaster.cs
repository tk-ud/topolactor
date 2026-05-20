using System.Threading.Channels;

namespace Topolactor.Scheduler;

/// <summary>
/// Broadcast hub for SSE projection events.
///
/// Each connected SSE client registers a per-connection Channel&lt;SseEvent&gt; via Subscribe().
/// DbNotifyListener writes events via Broadcast(); the broadcaster fans out to all
/// registered subscriber channels.
///
/// Per SSOT notify_listen_contract symmetry:
///   external_event_listener feeds hook_trigger into scheduler queue
///   SseEndpoint reads from per-connection channel and streams to client
///
/// Multi-instance leakage: each Subscribe() call creates a new isolated channel.
/// No shared state leaks between connections.
/// </summary>
public class SseEventBroadcaster
{
    private readonly List<Channel<SseEvent>> _subscribers = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Registers a new subscriber channel. The returned channel is per-connection.
    /// Call Unsubscribe() when the client disconnects.
    /// </summary>
    public Channel<SseEvent> Subscribe()
    {
        var ch = Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

        lock (_lock)
        {
            _subscribers.Add(ch);
        }

        return ch;
    }

    /// <summary>
    /// Removes a subscriber channel (called on client disconnect).
    /// </summary>
    public void Unsubscribe(Channel<SseEvent> channel)
    {
        lock (_lock)
        {
            _subscribers.Remove(channel);
        }
        channel.Writer.TryComplete();
    }

    /// <summary>
    /// Broadcasts an event to all registered subscriber channels.
    /// Full channels drop the event (non-blocking).
    /// </summary>
    public void Broadcast(SseEvent sseEvent)
    {
        List<Channel<SseEvent>> snapshot;
        lock (_lock)
        {
            snapshot = [.._subscribers];
        }

        foreach (var ch in snapshot)
        {
            ch.Writer.TryWrite(sseEvent);
        }
    }

    /// <summary>Current number of connected SSE subscribers.</summary>
    public int SubscriberCount
    {
        get { lock (_lock) return _subscribers.Count; }
    }
}
