using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Routes runtime output to the appropriate output lanes after topology execution.
///
/// Per SSOT backend_contract.output_lanes:
///   response_emission    — return response (implemented, pre-existing)
///   db_notify_emission   — send pg_notify with {table_id, table_registry_id, manifest_id}
///   registry_attractor_update — enqueue attractor rebuild signal to RebuildSignalChannel
///
/// Owned by RuntimeExecutor pipeline; called after emission is built.
/// db_notify failure is non-blocking (logged, not propagated to caller).
/// registry_attractor_update signals are written to RebuildSignalChannel (bounded, drop-oldest).
/// </summary>
public class OutputLaneRouter
{
    private readonly ILogger<OutputLaneRouter> _logger;
    private readonly DbNotifyRepository? _dbNotifyRepository;

    /// <summary>
    /// In-process attractor rebuild signal queue.
    /// Consumers read from this channel to trigger registry attractor rebuilds.
    /// Bounded (capacity 64); overflow drops oldest signals without blocking.
    /// </summary>
    public Channel<AttractorRebuildSignal> RebuildSignalChannel { get; } =
        Channel.CreateBounded<AttractorRebuildSignal>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
        });

    public OutputLaneRouter(
        ILogger<OutputLaneRouter> logger,
        DbNotifyRepository? dbNotifyRepository = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbNotifyRepository = dbNotifyRepository;
    }

    /// <summary>
    /// Routes the output lanes for the given emission result.
    /// response_emission is handled by the HTTP layer (not this class).
    /// db_notify_emission and registry_attractor_update are handled here.
    /// </summary>
    public async Task RouteAsync(
        OperationVector vector,
        EndpointResponseDto response,
        Guid? manifestId,
        CancellationToken ct = default)
    {
        if (!response.Success)
        {
            _logger.LogDebug("OutputLaneRouter: skipping output lanes for failed response.");
            return;
        }

        await RouteDbNotifyLaneAsync(vector, manifestId, ct);
        RouteRegistryAttractorLane(vector, manifestId);
    }

    private async Task RouteDbNotifyLaneAsync(
        OperationVector vector,
        Guid? manifestId,
        CancellationToken ct)
    {
        if (_dbNotifyRepository is null)
        {
            _logger.LogDebug("OutputLaneRouter: db_notify lane skipped (no repository configured).");
            return;
        }

        var tableId = vector.AttractorKey;
        var tableRegistryId = vector.Target;

        await _dbNotifyRepository.NotifyAsync(
            tableId: tableId,
            tableRegistryId: tableRegistryId,
            manifestId: manifestId,
            ct: ct);

        _logger.LogDebug(
            "OutputLaneRouter: db_notify lane routed for tableId={TableId} manifestId={ManifestId}",
            tableId, manifestId);
    }

    private void RouteRegistryAttractorLane(OperationVector vector, Guid? manifestId)
    {
        var signal = new AttractorRebuildSignal(
            AttractorKey: vector.AttractorKey,
            ManifestId: manifestId);

        if (RebuildSignalChannel.Writer.TryWrite(signal))
        {
            _logger.LogDebug(
                "OutputLaneRouter: registry_attractor_update signal enqueued. " +
                "AttractorKey={AttractorKey} ManifestId={ManifestId}",
                signal.AttractorKey, signal.ManifestId);
        }
        else
        {
            _logger.LogWarning(
                "OutputLaneRouter: registry_attractor_update signal dropped (channel full). " +
                "AttractorKey={AttractorKey}",
                signal.AttractorKey);
        }
    }
}

/// <summary>
/// Attractor rebuild signal emitted by the registry_attractor_update output lane.
/// Consumers (e.g. SystemOperationCiScheduler) read from OutputLaneRouter.RebuildSignalChannel
/// to trigger registry attractor recalculation after topology changes.
/// </summary>
public record AttractorRebuildSignal(string? AttractorKey, Guid? ManifestId);
