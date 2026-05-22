using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Scheduler;

/// <summary>
/// Unified trigger alignment layer for cron, hook, and client triggers.
///
/// Per SSOT scheduler_contract:
///   - Owns: trigger_alignment, runtime_queue, runtime_phase, causal_order, execution_boundary
///   - Not owns: target_runtime_selection_policy, role_semantics, topology_meaning_judgment
///   - input: cron_trigger | hook_trigger | client_trigger
///   - output: scheduler_aligned_runtime_event → ManifestDispatcher
///
/// Client triggers (AlignAndDispatchAsync) are also aligned through the same queue and
/// await dispatch completion to preserve HTTP response contract while unifying trigger order.
///
/// Cron/hook triggers (EnqueueCronTrigger / EnqueueHookTrigger) are queued in the
/// in-memory Channel and processed by the BackgroundService consumer loop.
///
/// All three trigger kinds share the same ManifestDispatcher path.
/// ManifestDispatcher and RuntimeExecutor must not know about trigger alignment.
/// </summary>
public class RuntimeTimelineScheduler : BackgroundService
{
    private readonly ILogger<RuntimeTimelineScheduler> _logger;
    private readonly ManifestDispatcher _manifestDispatcher;
    private readonly Channel<SchedulerItem> _bgQueue;

    public RuntimeTimelineScheduler(
        ILogger<RuntimeTimelineScheduler> logger,
        ManifestDispatcher manifestDispatcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manifestDispatcher = manifestDispatcher ?? throw new ArgumentNullException(nameof(manifestDispatcher));
        _bgQueue = Channel.CreateBounded<SchedulerItem>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    /// <summary>
    /// Accepts a client trigger, aligns it on the runtime timeline, and forwards
    /// synchronously to the manifest dispatcher. Returns the dispatch result.
    ///
    /// Client triggers execute synchronously to preserve the HTTP response contract.
    /// Ordering, batching, and collision control can be applied here without changing callers.
    /// </summary>
    public async Task<EndpointResponseDto> AlignAndDispatchAsync(
        EndpointRequestDto request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "RuntimeTimelineScheduler: aligning client trigger Target={Target} Layer={Layer} Action={Action}",
            request.Target, request.Layer, request.Action);

        var completion = new TaskCompletionSource<EndpointResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var aligned = request with { TriggerKind = request.TriggerKind ?? "client" };
        var item = SchedulerItem.CreateClient(aligned, completion, ct);

        if (!_bgQueue.Writer.TryWrite(item))
        {
            _logger.LogWarning("RuntimeTimelineScheduler: background queue full; rejecting client trigger.");
            return QueueFullResponse();
        }

        using var _ = ct.Register(() => completion.TrySetResult(ClientCanceledResponse()));
        return await completion.Task.ConfigureAwait(false);
    }


    /// <summary>
    /// Accepts legacy change intake and enqueues as hook trigger.
    /// table_name is preserved as registry resolution key candidate (Target).
    /// </summary>
    public LegacyChangeIntakeResponseDto EnqueueLegacyChangeTrigger(LegacyChangeIntakeRequestDto intake)
    {
        ArgumentNullException.ThrowIfNull(intake);

        var (request, errors) = _manifestDispatcher.BuildLegacyHookRequest(intake);
        if (errors.Count > 0 || request is null)
        {
            return new LegacyChangeIntakeResponseDto(
                Accepted: false,
                QueueStatus: null,
                Errors: errors);
        }

        EnqueueHookTrigger(request);
        return new LegacyChangeIntakeResponseDto(
            Accepted: true,
            QueueStatus: "hook_trigger_enqueued",
            Errors: []);
    }

    /// <summary>
    /// Enqueues a cron trigger for background processing (fire-and-forget).
    /// </summary>
    public void EnqueueCronTrigger(EndpointRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = new SchedulerItem(request with { TriggerKind = "cron" });

        if (!_bgQueue.Writer.TryWrite(item))
        {
            _logger.LogWarning("RuntimeTimelineScheduler: background queue full; dropping cron trigger.");
        }
    }

    /// <summary>
    /// Enqueues a hook trigger for background processing (fire-and-forget).
    /// </summary>
    public void EnqueueHookTrigger(EndpointRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = new SchedulerItem(request with { TriggerKind = "hook" });

        if (!_bgQueue.Writer.TryWrite(item))
        {
            _logger.LogWarning("RuntimeTimelineScheduler: background queue full; dropping hook trigger.");
        }
    }

    /// <summary>
    /// Background consumer: dequeues cron/hook trigger items and dispatches through ManifestDispatcher.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RuntimeTimelineScheduler: background queue consumer started.");

        await foreach (var item in _bgQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (item.IsClient && item.CancellationToken.IsCancellationRequested)
                {
                    item.Response.TrySetResult(ClientCanceledResponse());
                    continue;
                }

                var dispatchToken = item.IsClient ? item.CancellationToken : stoppingToken;
                var response = await _manifestDispatcher.DispatchAsync(item.Request, dispatchToken);
                item.Response.TrySetResult(response);
            }
            catch (OperationCanceledException oce)
            {
                if (item.IsClient)
                {
                    item.Response.TrySetResult(ClientCanceledResponse());
                    continue;
                }

                item.Response.TrySetException(oce);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "RuntimeTimelineScheduler: background dispatch failed for TriggerKind={TriggerKind} Target={Target}",
                    item.Request.TriggerKind, item.Request.Target);
                item.Response.TrySetException(ex);
            }
        }

        _logger.LogInformation("RuntimeTimelineScheduler: background queue consumer stopped.");
    }

    private static EndpointResponseDto QueueFullResponse() =>
        new(
            Success: false,
            Emission: null,
            Errors: [new ValidationError("SCHEDULER_QUEUE_FULL", "runtime queue is full. retry later.")]);

    private static EndpointResponseDto ClientCanceledResponse() =>
        new(
            Success: false,
            Emission: null,
            Errors: [new ValidationError("CLIENT_TRIGGER_CANCELED", "client trigger was canceled before runtime execution completed.")]);
}

/// <summary>
/// An item in the background runtime queue (cron/hook triggers only).
/// </summary>
internal record SchedulerItem(
    EndpointRequestDto Request,
    TaskCompletionSource<EndpointResponseDto> Response,
    bool IsClient,
    CancellationToken CancellationToken)
{
    public SchedulerItem(EndpointRequestDto request)
        : this(
            request,
            new TaskCompletionSource<EndpointResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously),
            IsClient: false,
            CancellationToken: CancellationToken.None)
    {
    }

    public static SchedulerItem CreateClient(
        EndpointRequestDto request,
        TaskCompletionSource<EndpointResponseDto> response,
        CancellationToken cancellationToken) =>
        new(request, response, IsClient: true, CancellationToken: cancellationToken);
}
