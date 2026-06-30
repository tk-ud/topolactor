using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Scheduler;

/// <summary>
/// Configuration for the post-notify bridge. hook_path / route_key identify the seeded error-notify
/// hook_port (its admin/seed configuration is a follow-up scope); they are data-defined, never a
/// provider/bundle-specific C# branch.
/// </summary>
public sealed record BackendErrorNotifyBridgeOptions(
    string HookPath = "/hooks/error_notify",
    string RouteKey = "error_notify",
    int BatchSize = 20,
    int MaxAttempts = 5,
    int RetryBackoffSeconds = 60,
    int PollIntervalSeconds = 30)
{
    public static BackendErrorNotifyBridgeOptions FromEnvironment()
    {
        static string Str(string key, string fallback)
        {
            var v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(v) ? fallback : v;
        }
        static int Int(string key, int fallback)
        {
            var v = Environment.GetEnvironmentVariable(key);
            return int.TryParse(v, out var parsed) && parsed > 0 ? parsed : fallback;
        }
        return new BackendErrorNotifyBridgeOptions(
            HookPath: Str("BACKEND_ERROR_NOTIFY_HOOK_PATH", "/hooks/error_notify"),
            RouteKey: Str("BACKEND_ERROR_NOTIFY_ROUTE_KEY", "error_notify"),
            BatchSize: Int("BACKEND_ERROR_NOTIFY_BATCH", 20),
            MaxAttempts: Int("BACKEND_ERROR_NOTIFY_MAX_ATTEMPTS", 5),
            RetryBackoffSeconds: Int("BACKEND_ERROR_NOTIFY_RETRY_BACKOFF_SECONDS", 60),
            PollIntervalSeconds: Int("BACKEND_ERROR_NOTIFY_POLL_INTERVAL_SECONDS", 30));
    }
}

/// <summary>
/// Post-notify bridge for the backend system error evidence substrate.
///
/// SSOT: docs/design/backend-error-evidence-report-notify-ssot.yaml (report_and_notify.notify_queue).
///
/// Flow:
///   1. LISTEN on the 'logs_error_inserted' channel — a WAKE-UP HINT ONLY.
///   2. The durable logs.error_notify_queue is the SOURCE OF TRUTH: on wake (and on a periodic
///      poll, because a pg_notify can be missed) the bridge atomically CLAIMS pending /
///      failed_retryable rows (FOR UPDATE SKIP LOCKED) and JOINS logs.error.
///   3. Builds a dispatchExternalPort hook trigger payload (hook_path / route_key /
///      dispatch_payload.{error_id, queue_id, origin_layer, boundary_key, error_code, severity,
///      stack_hash}) and dispatches it THROUGH the RuntimeTimelineScheduler hook trigger path —
///      never calling ExternalPortDispatchRuntime or an external provider directly.
///   4. On an accepted dispatch boundary result -> ACKNOWLEDGED. On failure the row is NOT deleted;
///      it transitions to FAILED_RETRYABLE (retry later) or FAILED_TERMINAL (attempts exhausted).
///
/// The pg_notify payload alone never acknowledges a row.
/// </summary>
public sealed class BackendErrorNotifyBridge : BackgroundService
{
    internal const string NotifyChannel = "logs_error_inserted";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly ILogger<BackendErrorNotifyBridge> _logger;
    private readonly string _connectionString;
    private readonly IBackendErrorNotifyQueueRepository _queueRepository;
    private readonly IBackendErrorNotifyHookDispatcher _dispatcher;
    private readonly BackendErrorNotifyBridgeOptions _options;
    private readonly SemaphoreSlim _wake = new(0, 1);

    public BackendErrorNotifyBridge(
        ILogger<BackendErrorNotifyBridge> logger,
        string connectionString,
        IBackendErrorNotifyQueueRepository queueRepository,
        IBackendErrorNotifyHookDispatcher dispatcher,
        BackendErrorNotifyBridgeOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _options = options ?? new BackendErrorNotifyBridgeOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackendErrorNotifyBridge: starting LISTEN on '{Channel}'.", NotifyChannel);
        var listenTask = ListenLoopAsync(stoppingToken);
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Drain the durable queue on startup and on every wake/poll — pg_notify can be missed,
                // so the durable queue (not the signal) drives processing.
                await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BackendErrorNotifyBridge: drain cycle failed; continuing.");
            }

            try
            {
                // Wake on either a pg_notify signal or the poll interval, whichever comes first.
                await _wake.WaitAsync(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        await listenTask;
        _logger.LogInformation("BackendErrorNotifyBridge: stopped.");
    }

    private async Task ListenLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(stoppingToken);
                conn.Notification += OnNotification;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"LISTEN {NotifyChannel}";
                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }
                while (!stoppingToken.IsCancellationRequested)
                    await conn.WaitAsync(stoppingToken);
                conn.Notification -= OnNotification;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BackendErrorNotifyBridge: LISTEN connection lost; reconnecting in {Delay}s.", ReconnectDelay.TotalSeconds);
                try { await Task.Delay(ReconnectDelay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    // Wake-up hint only. The payload is intentionally not used to acknowledge anything.
    private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        if (_wake.CurrentCount == 0)
        {
            try { _wake.Release(); }
            catch (SemaphoreFullException) { /* already signalled */ }
        }
    }

    /// <summary>Claims and processes batches until the queue is drained (or cancellation).</summary>
    private async Task DrainAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var processed = await ProcessOnceAsync(ct);
            if (processed == 0) break;
        }
    }

    /// <summary>
    /// Claims one batch and dispatches each row through the scheduler hook trigger path, transitioning
    /// the durable queue row by the dispatch boundary result. Returns the number of rows processed.
    /// Extracted for testability — callers can invoke this without a live LISTEN connection.
    /// </summary>
    internal async Task<int> ProcessOnceAsync(CancellationToken ct)
    {
        var claims = await _queueRepository.ClaimPendingAsync(
            _options.BatchSize, TimeSpan.FromSeconds(_options.RetryBackoffSeconds), ct);

        foreach (var claim in claims)
        {
            var request = BuildHookTriggerRequest(claim);
            EndpointResponseDto result;
            try
            {
                result = await _dispatcher.DispatchAsync(request, ct);
            }
            catch (Exception ex)
            {
                // A thrown dispatch failure is a substrate failure for this attempt — keep the row
                // (failed_retryable / failed_terminal), never drop it.
                _logger.LogError(ex, "BackendErrorNotifyBridge: dispatch threw for queue_id={QueueId}.", claim.QueueId);
                await FailAsync(claim, ex.GetType().Name, ct);
                continue;
            }

            if (result.Success)
            {
                await _queueRepository.AcknowledgeAsync(claim.QueueId, ct);
            }
            else
            {
                var reason = result.Errors.Count > 0
                    ? string.Join("; ", result.Errors.Select(e => e.Code))
                    : "DISPATCH_NOT_ACCEPTED";
                await FailAsync(claim, reason, ct);
            }
        }

        return claims.Count;
    }

    private async Task FailAsync(BackendErrorNotifyClaim claim, string reason, CancellationToken ct)
    {
        var terminal = claim.AttemptCount >= _options.MaxAttempts;
        await _queueRepository.FailAsync(claim.QueueId, reason, terminal, ct);
        _logger.LogWarning(
            "BackendErrorNotifyBridge: queue_id={QueueId} dispatch not accepted (attempt={Attempt}/{Max}) -> {Status}. reason={Reason}",
            claim.QueueId, claim.AttemptCount, _options.MaxAttempts,
            terminal ? BackendErrorNotifyQueueStatus.FailedTerminal : BackendErrorNotifyQueueStatus.FailedRetryable,
            reason);
    }

    internal EndpointRequestDto BuildHookTriggerRequest(BackendErrorNotifyClaim claim)
    {
        var hookPayload = JsonSerializer.SerializeToElement(new
        {
            hook_path = _options.HookPath,
            route_key = _options.RouteKey,
            dispatch_payload = new
            {
                error_id = claim.ErrorId.ToString(),
                queue_id = claim.QueueId.ToString(),
                origin_layer = claim.OriginLayer,
                boundary_key = claim.BoundaryKey,
                error_code = claim.ErrorCode,
                severity = claim.Severity,
                stack_hash = claim.StackHash,
            },
        });

        // hook trigger -> RuntimeTimelineScheduler -> ManifestDispatcher -> external_port_runtime.
        return new EndpointRequestDto(
            OperationType: "dispatchExternalPort",
            Target: "external_port",
            Layer: "external_port",
            Action: "dispatchExternalPort",
            IdOrHubId: null,
            Payload: hookPayload,
            Context: null,
            TriggerKind: "hook",
            Role: null);
    }
}

/// <summary>
/// Adapter that routes the post-notify bridge dispatch through the RuntimeTimelineScheduler hook
/// trigger path (AlignAndDispatchAsync awaits the scheduler -> ManifestDispatcher boundary result).
/// The bridge never calls ExternalPortDispatchRuntime directly.
/// </summary>
public sealed class SchedulerBackendErrorNotifyHookDispatcher : IBackendErrorNotifyHookDispatcher
{
    private readonly RuntimeTimelineScheduler _scheduler;

    public SchedulerBackendErrorNotifyHookDispatcher(RuntimeTimelineScheduler scheduler) =>
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

    public Task<EndpointResponseDto> DispatchAsync(EndpointRequestDto request, CancellationToken ct = default) =>
        _scheduler.AlignAndDispatchAsync(request, ct);
}
