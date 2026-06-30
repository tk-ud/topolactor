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

    /// <summary>
    /// Dedicated structured-signal channel for the manifest_topology_key_expansion draft lane.
    /// Emitted by SQL trigger logs.notify_sql_attention_draft_candidate_created on insert.
    /// </summary>
    internal const string SqlAttentionDraftCandidateChannel = "sql_attention_draft_candidate";

    /// <summary>SSE event type used when relaying the draft-candidate-created signal to clients.</summary>
    internal const string SqlAttentionDraftCandidateSseEventType = "sql_attention_draft_candidate";

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly ILogger<DbNotifyListener> _logger;
    private readonly string _connectionString;
    private readonly RuntimeTimelineScheduler _scheduler;
    private readonly SseEventBroadcaster? _broadcaster;
    private readonly Topolactor.Schema.IBackendErrorEvidenceAppender? _errorAppender;

    public DbNotifyListener(
        ILogger<DbNotifyListener> logger,
        string connectionString,
        RuntimeTimelineScheduler scheduler,
        SseEventBroadcaster? broadcaster = null,
        Topolactor.Schema.IBackendErrorEvidenceAppender? errorAppender = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _broadcaster = broadcaster;
        _errorAppender = errorAppender;
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

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"LISTEN {NotifyChannel}; LISTEN {SqlAttentionDraftCandidateChannel}";
                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }

                _logger.LogDebug(
                    "DbNotifyListener: LISTEN established on '{TopologyChannel}' and '{DraftChannel}'.",
                    NotifyChannel, SqlAttentionDraftCandidateChannel);

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

        if (string.Equals(e.Channel, SqlAttentionDraftCandidateChannel, StringComparison.Ordinal))
        {
            HandleSqlAttentionDraftCandidatePayload(e.Payload ?? "{}");
            return;
        }

        HandleNotificationPayload(e.Payload ?? "{}");
    }

    /// <summary>
    /// Bridges a structured sql_attention_draft_candidate DB NOTIFY payload onto the SSE projection
    /// lane as a render-only signal. This is a notification bridge only: it carries no candidate
    /// inference, no topology promotion, and no UI placement authority — the UI decides display surface.
    ///
    /// Required structured payload fields (explicit failure on any missing/invalid, no silent fallback):
    ///   event_type = "sql_attention_draft_candidate_created"
    ///   source     = "sql_attention"
    ///   candidate_id (guid), candidate_lane (non-empty), markdown_projection_id (guid)
    ///
    /// Extracted for testability — callers can invoke this directly without a live DB connection.
    /// </summary>
    internal protected virtual void HandleSqlAttentionDraftCandidatePayload(string payload)
    {
        JsonElement parsed;
        try
        {
            parsed = JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "DbNotifyListener: SQL_ATTENTION_DRAFT_CANDIDATE_PAYLOAD_INVALID — failed to parse payload as JSON. payload={Payload}",
                payload);
            return;
        }

        if (!TryGetStringEquals(parsed, "event_type", "sql_attention_draft_candidate_created")
            || !TryGetStringEquals(parsed, "source", "sql_attention")
            || !TryGetGuid(parsed, "candidate_id")
            || !TryGetNonEmptyString(parsed, "candidate_lane")
            || !TryGetGuid(parsed, "markdown_projection_id"))
        {
            _logger.LogError(
                "DbNotifyListener: SQL_ATTENTION_DRAFT_CANDIDATE_PAYLOAD_INVALID_FIELDS — payload missing/invalid required structured fields. payload={Payload}",
                payload);
            return;
        }

        if (_broadcaster is null)
        {
            _logger.LogWarning(
                "DbNotifyListener: SQL_ATTENTION_DRAFT_CANDIDATE_NO_BROADCASTER — structured draft signal received but no SSE broadcaster is wired. payload={Payload}",
                payload);
            return;
        }

        _broadcaster.Broadcast(new SseEvent(SqlAttentionDraftCandidateSseEventType, payload));
        _logger.LogDebug(
            "DbNotifyListener: relayed sql_attention_draft_candidate signal to {Count} SSE subscriber(s).",
            _broadcaster.SubscriberCount);
    }

    private static bool TryGetStringEquals(JsonElement obj, string key, string expected) =>
        obj.TryGetProperty(key, out var el)
        && el.ValueKind == JsonValueKind.String
        && string.Equals(el.GetString(), expected, StringComparison.Ordinal);

    private static bool TryGetNonEmptyString(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var el)
        && el.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(el.GetString());

    private static bool TryGetGuid(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var el)
        && el.ValueKind == JsonValueKind.String
        && Guid.TryParse(el.GetString(), out _);

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
            // Queue saturation is a backend substrate capacity failure (system error), distinct from
            // the malformed-payload data rejects above. Best-effort fire-and-forget: the helper never
            // throws, so the unobserved task carries no failure path.
            _ = Topolactor.Runtime.BackendErrorBoundary.RecordSystemErrorAsync(
                _errorAppender, _logger,
                new Topolactor.Schema.BackendErrorEvidence(
                    OriginLayer: Topolactor.Schema.BackendErrorOriginLayer.DbNotifyListener,
                    BoundaryKey: "db_notify_listener:hook_trigger_enqueue",
                    ErrorKind: Topolactor.Schema.BackendErrorKind.SystemError,
                    ErrorCode: "DB_NOTIFY_HOOK_TRIGGER_QUEUE_FULL",
                    MessagePublic: "scheduler queue full; db_notify hook trigger dropped.",
                    StackHash: Topolactor.Runtime.BackendErrorBoundary.ComputeHash("db_notify_listener|DB_NOTIFY_HOOK_TRIGGER_QUEUE_FULL"),
                    Severity: "error",
                    Retryable: true));
        }
    }
}

/// <summary>
/// An event to be streamed over the SSE projection lane.
/// </summary>
public record SseEvent(string EventType, string Data);
