using Microsoft.Extensions.Logging;

namespace Topolactor.Repository;

/// <summary>
/// Append-only log of topology mutations. Records before/after state for each operation.
/// Writes are never destructive — entries are only ever appended, never updated or deleted.
///
/// Separation from demo_state_transitions:
///   demo_state_transitions = state machine transition log (state A → state B events).
///   topology_edit_log      = edit diff audit log (what changed, who, when).
///   These are semantically distinct; callers must not mix them.
///
/// Base implementation logs to ILogger. Production: override in NpgsqlDiffLogRepository
/// to persist to topology_edit_log.
/// </summary>
public class DiffLogRepository
{
    protected readonly ILogger<DiffLogRepository> _logger;

    public DiffLogRepository(ILogger<DiffLogRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Appends a structured edit diff log entry.
    /// target_table: attractor_key or domain scope identifier (not a literal DB table name).
    /// target_id: primary key or ID of the record being edited; null for creates/lists.
    /// operation: action name (e.g. "create", "update", "advance").
    /// before_json: state before the operation as JSON; null for creates.
    /// after_json: state after the operation as JSON; null for deletes.
    /// diff_json: diff between before and after; null when not computed.
    /// actor: user_id or service identifier; null when unknown.
    ///
    /// Base implementation: logs to ILogger. Production: persists to topology_edit_log.
    /// </summary>
    public virtual Task AppendEditAsync(
        string targetTable,
        string? targetId,
        string operation,
        string? beforeJson,
        string? afterJson,
        string? diffJson,
        string? actor,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "DiffLog: targetTable={TargetTable} targetId={TargetId} operation={Operation} actor={Actor} before={Before} after={After} diff={Diff}",
            targetTable,
            targetId ?? "(none)",
            operation,
            actor ?? "(none)",
            beforeJson is null ? "(null)" : "[present]",
            afterJson is null ? "(null)" : "[present]",
            diffJson is null ? "(null)" : "[present]");

        return Task.CompletedTask;
    }
}
