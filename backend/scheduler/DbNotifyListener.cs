using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Topolactor.Scheduler;

/// <summary>
/// BackgroundService that holds a pg LISTEN connection and feeds incoming
/// db_notify payloads into RuntimeTimelineScheduler as hook triggers.
///
/// Per SSOT notify_listen_contract.db_listen:
///   trigger_kind: hook
///   backend.holder: background_service
///   backend.feeds: backend_scheduler_queue_as_hook_trigger
///
/// Per SSOT notify_listen_contract.invariants:
///   listen_event_enters_scheduler_before_projection_runtime
///
/// Payload shape: { "table_id": "...", "table_registry_id": "...", "manifest_id": "<guid>" }
/// manifest_id is required; malformed or missing payload yields explicit LogError (no silent fallback).
///
/// Connection is re-established on disconnect; errors are logged and retried.
/// </summary>
public class DbNotifyListener : BackgroundService
{
    private const string NotifyChannel = "topolactor_topology_changed";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly ILogger<DbNotifyListener> _logger;
    private readonly string _connectionString;
    private readonly RuntimeTimelineScheduler _scheduler;

    public DbNotifyListener(
        ILogger<DbNotifyListener> logger,
        string connectionString,
        RuntimeTimelineScheduler scheduler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
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
            "DbNotifyListener: received notification channel={Channel}.",
            e.Channel);

        HandleNotificationPayload(e.Payload ?? "{}");
    }

    /// <summary>
    /// Handles a db_notify payload by enqueuing it into RuntimeTimelineScheduler as a hook trigger.
    /// Per SSOT: listen_event_enters_scheduler_before_projection_runtime.
    ///
    /// Explicit failures (no silent fallback):
    ///   - Malformed JSON payload → LogError DB_NOTIFY_PAYLOAD_INVALID
    ///   - Missing or invalid manifest_id → LogError DB_NOTIFY_PAYLOAD_MANIFEST_ID_MISSING
    ///   - Scheduler queue full → LogError DB_NOTIFY_HOOK_TRIGGER_QUEUE_FULL
    ///
    /// Extracted for testability — callers can invoke this directly without a live DB connection.
    /// </summary>
    internal protected virtual void HandleNotificationPayload(string payload)
    {
        JsonElement parsedPayload;
        try
        {
            parsedPayload = JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "DbNotifyListener: DB_NOTIFY_PAYLOAD_INVALID — failed to parse payload as JSON. payload={Payload}",
                payload);
            return;
        }

        if (!parsedPayload.TryGetProperty("manifest_id", out var manifestIdEl) ||
            manifestIdEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(manifestIdEl.GetString(), out _))
        {
            _logger.LogError(
                "DbNotifyListener: DB_NOTIFY_PAYLOAD_MANIFEST_ID_MISSING — payload missing or invalid manifest_id. payload={Payload}",
                payload);
            return;
        }

        var request = new Schema.EndpointRequestDto(
            OperationType: "DbNotifyProjection",
            Target: "db_notify",
            Layer: "projection",
            Action: "broadcast",
            IdOrHubId: null,
            Payload: parsedPayload,
            Context: null,
            TriggerKind: "hook",
            Role: null);

        if (!_scheduler.EnqueueHookTrigger(request))
        {
            _logger.LogError(
                "DbNotifyListener: DB_NOTIFY_HOOK_TRIGGER_QUEUE_FULL — scheduler queue full; db_notify hook trigger dropped.");
        }
    }
}

/// <summary>
/// An event to be streamed over the SSE projection lane.
/// </summary>
public record SseEvent(string EventType, string Data);
