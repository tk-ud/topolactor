using Microsoft.Extensions.Logging;

namespace Topolactor.Repository;

/// <summary>
/// db_notify output lane repository.
///
/// Per SSOT notify_listen_contract.db_notify:
///   output_lane: backend
///   payload: { table_id, table_registry_id, manifest_id }
///
/// pg_notify sends the payload to the LISTEN channel so the backend DbNotifyListener
/// background service can feed the hook_trigger into the backend scheduler queue.
/// Frontend receives the notification via SSE (sse_projection_lane).
///
/// Failure policy: pg_notify failure is non-blocking to the emission (logged, not rethrown).
/// </summary>
public abstract class DbNotifyRepository
{
    protected readonly ILogger<DbNotifyRepository> _logger;

    protected DbNotifyRepository(ILogger<DbNotifyRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sends a pg_notify event for the given topology change.
    /// Non-blocking: failure is logged, not propagated.
    /// </summary>
    public abstract Task NotifyAsync(
        string? tableId,
        string? tableRegistryId,
        Guid? manifestId,
        CancellationToken ct = default);
}
