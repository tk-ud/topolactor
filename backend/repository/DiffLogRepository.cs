using Microsoft.Extensions.Logging;

namespace Topolactor.Repository;

/// <summary>
/// Append-only log of topology mutations. Records before/after state for each operation.
/// Writes are never destructive — entries are only ever appended, never updated or deleted.
/// Stub implementation logs to ILogger; real implementation writes to persistent storage.
/// </summary>
public class DiffLogRepository
{
    private readonly ILogger<DiffLogRepository> _logger;

    public DiffLogRepository(ILogger<DiffLogRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Appends a diff log entry for an operation.
    /// Stub: writes to ILogger at Information level.
    /// </summary>
    /// <param name="hubId">The hub or entity ID affected, if applicable.</param>
    /// <param name="action">The action that was performed (e.g., "create", "update").</param>
    /// <param name="before">State before the operation, or null for creates.</param>
    /// <param name="after">State after the operation, or null for deletes.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task AppendAsync(
        Guid? hubId,
        string action,
        object? before,
        object? after,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "DiffLog: hubId={HubId} action={Action} before={Before} after={After}",
            hubId?.ToString() ?? "(none)",
            action,
            before is null ? "(null)" : "[present]",
            after is null ? "(null)" : "[present]");

        return Task.CompletedTask;
    }
}
