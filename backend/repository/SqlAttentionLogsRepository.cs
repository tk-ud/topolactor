using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Repository for SQL Attention logs operations.
///
/// Provides access to:
///   - logs.refresh_logs_current_watch: returns watch change candidates
///   - explicit physical table -> topology manifest resolution
///   - hubs.hub_relations: canonical SQL Attention exploration candidates
///   - logs.hub_current: deprecated diagnostics-only support cache
///   - logs.attention: append-only SQLAT / phaseAT / lifecycle evidence
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
    /// Resolves explicit physical-table bindings to active hubs.topology_manifests.
    /// Empty results are explicit no-hit evidence; implementations must not fall back.
    /// </summary>
    public virtual Task<RelatedTopologyManifestResolution> ResolveRelatedTopologyManifestsAsync(
        WatchChangeCandidate candidate,
        CancellationToken ct = default) =>
        Task.FromResult(new RelatedTopologyManifestResolution(candidate.CurrentId, [], "{\"resolver\":\"test_double_empty\",\"no_implicit_fallback\":true}"));

    /// <summary>
    /// Loads active canonical hubs.hub_relations candidates for resolved manifest scopes.
    /// </summary>
    public virtual Task<IReadOnlyList<HubRelationExplorationCandidate>> LoadHubRelationExplorationCandidatesAsync(
        IReadOnlyList<Guid> topologyManifestIds,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HubRelationExplorationCandidate>>([]);

    /// <summary>
    /// Appends one SQLAT hit row and its phaseAT q row in one generation line.
    /// </summary>
    public virtual Task<AttentionGenerationAppendResult> AppendAttentionGenerationAsync(
        AttentionGenerationAppendRequest request,
        CancellationToken ct = default)
    {
        ValidateGenerationRequest(request);
        return Task.FromResult(new AttentionGenerationAppendResult(request.GenerationLineId, Guid.NewGuid(), Guid.NewGuid()));
    }

    public virtual Task<AttentionLifecycleSource?> LoadAttentionLifecycleSourceAsync(
        Guid attentionId,
        CancellationToken ct = default) =>
        Task.FromResult<AttentionLifecycleSource?>(null);

    public virtual Task<Guid> AppendAttentionLifecycleEvidenceAsync(
        AttentionLifecycleAppendRequest request,
        CancellationToken ct = default)
    {
        ValidateLifecycleAppendRequest(request);
        return Task.FromResult(Guid.NewGuid());
    }

    /// <summary>
    /// Loads deprecated logs.hub_current support-cache records for explicit diagnostics only.
    /// These records are not canonical SQL Attention exploration candidates; the canonical
    /// route resolves related topology_manifest_id[] and explores hubs.hub_relations separately.
    ///
    /// In-memory test double: returns empty list.
    /// Production: override to query logs.hub_current via Npgsql.
    /// </summary>
    public virtual Task<IReadOnlyList<HubCurrentCandidate>> LoadLegacyHubCurrentSupportCacheCandidatesAsync(
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "SqlAttentionLogsRepository.LoadLegacyHubCurrentSupportCacheCandidatesAsync: no DB connection (test double) — returning empty list for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
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
    ///   - phase_vector_json is stored as provided append-only phaseAT evidence JSON; q is not Draft.
    ///   - write boundary does not generate phase vectors; it only appends provided evidence.
    ///   - statistics_json / ema_score are stored as provided; EMA integration is a separate TODO.
    ///   - No registry mutation / migration / column promotion.
    ///
    /// In-memory test double: validates invariants and returns hit count without writing.
    /// Production: override in NpgsqlSqlAttentionLogsRepository.
    /// </summary>
    public virtual Task<int> WriteLogsAttentionAsync(
        IReadOnlyList<LogsAttentionWriteRequest> requests,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        foreach (var request in requests)
        {
            if (request.CurrentId == Guid.Empty)
                throw new ArgumentException(
                    $"write_logs_attention: CurrentId must not be empty (request AttractorKey={request.AttractorKey}).",
                    nameof(requests));

            if (request.HubCurrentId == Guid.Empty)
                throw new ArgumentException(
                    $"write_logs_attention: HubCurrentId must not be empty (request AttractorKey={request.AttractorKey}).",
                    nameof(requests));

            if (!string.Equals(request.ArchivePolicy, "required", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"write_logs_attention: ArchivePolicy must be 'required' (request AttractorKey={request.AttractorKey}).",
                    nameof(requests));
        }

        var count = requests.Count;
        _logger.LogDebug(
            "SqlAttentionLogsRepository.WriteLogsAttentionAsync: no DB connection (test double) — {Count} hit(s) validated, no write performed.",
            count);
        return Task.FromResult(count);
    }

    /// <summary>
    /// Loads recent logs.attention evidence rows for child projection (read-only).
    /// Returns evidence ordered by neighbor_score DESC, created_at DESC, limited to topK.
    /// Evidence layer separation (statistics/attention/phase_attention) is preserved in returned records.
    /// This boundary never mutates the evidence layer.
    ///
    /// Prohibited: INSERT / UPDATE / DELETE on logs.attention.
    /// In-memory test double: returns empty list.
    /// Production: override in NpgsqlSqlAttentionLogsRepository.
    /// </summary>
    public virtual Task<IReadOnlyList<AttentionEvidenceRecord>> LoadAttentionEvidenceForProjectionAsync(
        string sourceSetId,
        int topK,
        double minNeighborScore,
        int recentWindowDays,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "SqlAttentionLogsRepository.LoadAttentionEvidenceForProjectionAsync: no DB connection (test double) — returning empty list for sourceSetId={SourceSetId}.",
            sourceSetId);
        return Task.FromResult<IReadOnlyList<AttentionEvidenceRecord>>([]);
    }

    public virtual Task AppendLogsDiffAsync(
        LogsDiffAppendRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateLogsDiffRequest(request);
        _logger.LogDebug("SqlAttentionLogsRepository.AppendLogsDiffAsync: no DB connection (test double) — request validated only.");
        return Task.CompletedTask;
    }

    protected static void ValidateGenerationRequest(AttentionGenerationAppendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.GenerationLineId == Guid.Empty) throw new ArgumentException("generation_line_id must not be empty.", nameof(request));
        if (request.CurrentId == Guid.Empty) throw new ArgumentException("current_id must not be empty.", nameof(request));
        if (request.TopologyManifestId == Guid.Empty) throw new ArgumentException("topology_manifest_id must not be empty.", nameof(request));
        if (request.HubRelationId == Guid.Empty) throw new ArgumentException("hub_relation_id must not be empty.", nameof(request));
        if (request.HubId == Guid.Empty) throw new ArgumentException("hub_id must not be empty.", nameof(request));
        if (!string.Equals(request.ArchivePolicy, "required", StringComparison.Ordinal)) throw new ArgumentException("archive_policy must be required.", nameof(request));
    }

    protected static void ValidateLifecycleAppendRequest(AttentionLifecycleAppendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Source.AttentionId == Guid.Empty) throw new ArgumentException("source_attention_id must not be empty.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorOrSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CommandId);
    }

    protected static void ValidateLogsDiffRequest(LogsDiffAppendRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceSetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BasisWindow);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PhysicalTableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PhysicalTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RecordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKind);

        if (!string.Equals(request.ArchivePolicy, "required", StringComparison.Ordinal))
            throw new ArgumentException("append_logs_diff: ArchivePolicy must be 'required'.", nameof(request));
    }
}
