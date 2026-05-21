using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Topolactor.Scheduler;

/// <summary>
/// BackgroundService that holds a pg LISTEN connection and broadcasts incoming
/// db_notify payloads as hook_triggers to the SseEventBroadcaster.
///
/// Per SSOT notify_listen_contract.db_listen:
///   trigger_kind: hook
///   backend.holder: background_service
///   backend.feeds: backend_scheduler_queue_as_hook_trigger
///
/// Events are broadcast to all connected SSE clients via SseEventBroadcaster.
/// Connection is re-established on disconnect; errors are logged and retried.
///
/// Known gap (Gap-7): direct broadcast bypasses the scheduler queue.
/// Per SSOT, db_notify events should enter the scheduler as hook_triggers before
/// SSE emission. Full scheduler routing requires architectural wiring (live DB needed).
/// </summary>
public class DbNotifyListener : BackgroundService
{
    private const string NotifyChannel = "topolactor_topology_changed";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly ILogger<DbNotifyListener> _logger;
    private readonly string _connectionString;
    private readonly SseEventBroadcaster _broadcaster;

    public DbNotifyListener(
        ILogger<DbNotifyListener> logger,
        string connectionString,
        SseEventBroadcaster broadcaster)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DbNotifyListener: starting LISTEN on channel '{Channel}'.", NotifyChannel);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(stoppingToken);

                conn.Notification += OnNotification;

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"LISTEN {NotifyChannel}";
                await cmd.ExecuteNonQueryAsync(stoppingToken);

                _logger.LogDebug("DbNotifyListener: LISTEN established.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    await conn.WaitAsync(stoppingToken);
                }

                conn.Notification -= OnNotification;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "DbNotifyListener: connection lost; reconnecting in {Delay}s.", ReconnectDelay.TotalSeconds);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }

        _logger.LogInformation("DbNotifyListener: stopped.");
    }

    private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        _logger.LogDebug(
            "DbNotifyListener: received notification channel={Channel} subscribers={Count}.",
            e.Channel, _broadcaster.SubscriberCount);

        HandleNotificationPayload(e.Payload ?? "{}");
    }

    /// <summary>
    /// Handles a db_notify payload by broadcasting it to SSE subscribers.
    /// Extracted for testability — callers can invoke this directly without a live DB connection.
    ///
    /// Known gap (Gap-7): this broadcasts directly, bypassing the scheduler queue.
    /// Per SSOT the hook event should enter the scheduler before SSE emission.
    /// </summary>
    internal protected virtual void HandleNotificationPayload(string payload)
    {
        _broadcaster.Broadcast(new SseEvent(EventType: "projection", Data: payload));
    }
}

/// <summary>
/// An event to be streamed over the SSE projection lane.
/// </summary>
public record SseEvent(string EventType, string Data);
