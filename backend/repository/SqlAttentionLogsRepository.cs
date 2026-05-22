using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Repository for SQL Attention logs operations.
///
/// Provides access to:
///   - logs.refresh_logs_current_watch: returns watch change candidates
///   - logs.hub_current: returns hub attractor candidates for exploration
///
/// In-memory test double: returns empty collections by default.
/// Production: override in NpgsqlSqlAttentionLogsRepository.
/// </summary>
public class SqlAttentionLogsRepository
{
    protected readonly ILogger<SqlAttentionLogsRepository> _logger;
    protected readonly string _connectionString;

    public SqlAttentionLogsRepository(
        ILogger<SqlAttentionLogsRepository> logger,
        string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Calls logs.refresh_logs_current_watch(sourceSetId, basisWindow) and returns
    /// the change candidates. Only candidates with ChangeDetected=true are eligible
    /// for hub-attractor exploration.
    ///
    /// In-memory test double: returns empty list.
    /// Production: override to call the SQL function via Npgsql.
    /// </summary>
    public virtual Task<IReadOnlyList<WatchChangeCandidate>> LoadWatchCandidatesAsync(
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "SqlAttentionLogsRepository.LoadWatchCandidatesAsync: no DB connection (test double) — returning empty list for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
            sourceSetId, basisWindow);
        return Task.FromResult<IReadOnlyList<WatchChangeCandidate>>([]);
    }

    /// <summary>
    /// Loads logs.hub_current records for (sourceSetId, basisWindow).
    /// These are used as hub attractor candidates during exploration.
    ///
    /// In-memory test double: returns empty list.
    /// Production: override to query logs.hub_current via Npgsql.
    /// </summary>
    public virtual Task<IReadOnlyList<HubCurrentCandidate>> LoadHubCurrentCandidatesAsync(
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "SqlAttentionLogsRepository.LoadHubCurrentCandidatesAsync: no DB connection (test double) — returning empty list for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
            sourceSetId, basisWindow);
        return Task.FromResult<IReadOnlyList<HubCurrentCandidate>>([]);
    }
}
