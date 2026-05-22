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

    /// <summary>
    /// Appends evidence rows to logs.attention for each hit in the exploration result.
    /// This is the write_logs_attention boundary — completion of one SQL Attention run.
    ///
    /// Invariants:
    ///   - current_id must be non-empty (hard error if absent).
    ///   - hub_current_id must be non-empty (hard error if absent).
    ///   - Empty hits → returns 0 without INSERT (no-change early return).
    ///   - append-only: INSERT only, no UPDATE or DELETE.
    ///   - archive_policy is always 'required'.
    ///   - phase_vector_json is stored as provided; generation is a separate TODO.
    ///   - statistics_json / ema_score are stored as provided; EMA integration is a separate TODO.
    ///   - No registry mutation / migration / column promotion.
    ///
    /// In-memory test double: validates invariants and returns hit count without writing.
    /// Production: override in NpgsqlSqlAttentionLogsRepository.
    /// </summary>
    public virtual Task<int> WriteLogsAttentionAsync(
        HubAttractorExplorationResult explorationResult,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(explorationResult);

        foreach (var hit in explorationResult.Hits)
        {
            if (hit.CurrentId == Guid.Empty)
                throw new ArgumentException(
                    $"write_logs_attention: CurrentId must not be empty (hit AttractorKey={hit.AttractorKey}).",
                    nameof(explorationResult));

            if (hit.HubCurrentId == Guid.Empty)
                throw new ArgumentException(
                    $"write_logs_attention: HubCurrentId must not be empty (hit AttractorKey={hit.AttractorKey}).",
                    nameof(explorationResult));
        }

        var count = explorationResult.Hits.Count;
        _logger.LogDebug(
            "SqlAttentionLogsRepository.WriteLogsAttentionAsync: no DB connection (test double) — {Count} hit(s) validated, no write performed.",
            count);
        return Task.FromResult(count);
    }
}
