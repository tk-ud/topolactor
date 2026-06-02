using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public class NpgsqlSqlAttentionLogsRepository : SqlAttentionLogsRepository
{
    private readonly ILogger<NpgsqlSqlAttentionLogsRepository> _npgsqlLogger;

    public NpgsqlSqlAttentionLogsRepository(
        ILogger<NpgsqlSqlAttentionLogsRepository> logger,
        string connectionString)
        : base(logger, connectionString)
    {
        _npgsqlLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<IReadOnlyList<WatchChangeCandidate>> LoadWatchCandidatesAsync(
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(basisWindow);

        var rows = new List<WatchChangeCandidate>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string watchSql = @"
SELECT current_id, physical_table_id, physical_table_name, norm_rank,
       previous_norm_level, norm_level,
       change_detected, change_reason,
       l2_norm, basis_vector_json::text AS basis_vector_json
  FROM logs.refresh_logs_current_watch(@p_source_set_id, @p_basis_window)";
        await using var cmd = new NpgsqlCommand(watchSql, conn);
        cmd.Parameters.AddWithValue("p_source_set_id", sourceSetId);
        cmd.Parameters.AddWithValue("p_basis_window", basisWindow);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new WatchChangeCandidate(
                CurrentId: reader.GetGuid(reader.GetOrdinal("current_id")),
                PhysicalTableId: reader.GetString(reader.GetOrdinal("physical_table_id")),
                NormRank: reader.GetInt32(reader.GetOrdinal("norm_rank")),
                PreviousNormLevel: reader.IsDBNull(reader.GetOrdinal("previous_norm_level")) ? null : reader.GetString(reader.GetOrdinal("previous_norm_level")),
                NormLevel: reader.IsDBNull(reader.GetOrdinal("norm_level")) ? null : reader.GetString(reader.GetOrdinal("norm_level")),
                ChangeDetected: reader.GetBoolean(reader.GetOrdinal("change_detected")),
                ChangeReason: reader.IsDBNull(reader.GetOrdinal("change_reason")) ? null : reader.GetString(reader.GetOrdinal("change_reason")),
                L2Norm: reader.GetDouble(reader.GetOrdinal("l2_norm")),
                BasisVectorJson: reader.GetString(reader.GetOrdinal("basis_vector_json")),
                PhysicalTableName: reader.GetString(reader.GetOrdinal("physical_table_name"))));
        }

        return rows;
    }

    public override async Task<RelatedTopologyManifestResolution> ResolveRelatedTopologyManifestsAsync(
        WatchChangeCandidate candidate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT topology_manifest_id, resolver_evidence_json::text
  FROM logs.resolve_related_topology_manifests(@current_id, @physical_table_id, @physical_table_name)";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("current_id", candidate.CurrentId);
        cmd.Parameters.AddWithValue("physical_table_id", candidate.PhysicalTableId);
        cmd.Parameters.AddWithValue("physical_table_name", candidate.PhysicalTableName);
        var ids = new List<Guid>();
        var evidence = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetGuid(0));
            evidence.Add(reader.GetString(1));
        }
        return new RelatedTopologyManifestResolution(candidate.CurrentId, ids, System.Text.Json.JsonSerializer.Serialize(evidence));
    }

    public override async Task<IReadOnlyList<HubRelationExplorationCandidate>> LoadHubRelationExplorationCandidatesAsync(
        IReadOnlyList<Guid> topologyManifestIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(topologyManifestIds);
        if (topologyManifestIds.Count == 0) return [];
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT hr.hub_relation_id, hr.topology_manifest_id, tm.hub_id, hr.related_hub_id,
       hr.sequence_position, (hr.relation_config->>'sql_attention_score')::double precision,
       hr.relation_config::text
  FROM hubs.hub_relations hr
  JOIN hubs.topology_manifests tm ON tm.topology_manifest_id = hr.topology_manifest_id
 WHERE hr.topology_manifest_id = ANY(@manifest_ids)
   AND hr.status = 'active'
   AND tm.status = 'active'
   AND hr.relation_config ? 'sql_attention_score'
 ORDER BY hr.topology_manifest_id, hr.sequence_position";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("manifest_ids", topologyManifestIds.ToArray());
        var rows = new List<HubRelationExplorationCandidate>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new HubRelationExplorationCandidate(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
                reader.GetInt32(4), reader.GetDouble(5), reader.GetString(6)));
        }
        return rows;
    }

    public override async Task<AttentionGenerationAppendResult> AppendAttentionGenerationAsync(
        AttentionGenerationAppendRequest request,
        CancellationToken ct = default)
    {
        ValidateGenerationRequest(request);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var hitId = Guid.NewGuid();
        var phaseId = Guid.NewGuid();
        const string sql = @"
INSERT INTO logs.attention (
  attention_id, current_id, source_current_id, source_set_id, evidence_kind,
  generation_line_id, source_attention_id, source_topology_manifest_ids,
  hit_hub_relation_ids, expanded_hub_relation_ids, expanded_topology_manifest_ids,
  expanded_hub_ids, phase_status, promotion_status, hub_id, attractor_key,
  hub_relation_id, neighbor_score, hit_rank, score_band, l2_norm,
  phase_vector_json, evidence_json, archive_policy
) VALUES (
  @attention_id, @current_id, @source_current_id, @source_set_id, @evidence_kind,
  @generation_line_id, @source_attention_id, @source_topology_manifest_ids,
  @hit_hub_relation_ids, @expanded_hub_relation_ids, @expanded_topology_manifest_ids,
  @expanded_hub_ids, @phase_status, 'not_requested', @hub_id, 'hubs.hub_relations',
  @hub_relation_id, @neighbor_score, @hit_rank, @score_band, @l2_norm,
  @phase_vector_json::jsonb, @evidence_json::jsonb, 'required'
)";
        async Task InsertAsync(Guid attentionId, string evidenceKind, Guid? sourceAttentionId, string phaseStatus)
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("attention_id", attentionId);
            cmd.Parameters.AddWithValue("current_id", request.CurrentId);
            cmd.Parameters.AddWithValue("source_current_id", request.CurrentId);
            cmd.Parameters.AddWithValue("source_set_id", request.SourceSetId);
            cmd.Parameters.AddWithValue("evidence_kind", evidenceKind);
            cmd.Parameters.AddWithValue("generation_line_id", request.GenerationLineId);
            cmd.Parameters.AddWithValue("source_attention_id", sourceAttentionId.HasValue ? sourceAttentionId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("source_topology_manifest_ids", request.SourceTopologyManifestIds.ToArray());
            cmd.Parameters.AddWithValue("hit_hub_relation_ids", new[] { request.HubRelationId });
            cmd.Parameters.AddWithValue("expanded_hub_relation_ids", request.ExpandedHubRelationIds.ToArray());
            cmd.Parameters.AddWithValue("expanded_topology_manifest_ids", request.ExpandedTopologyManifestIds.ToArray());
            cmd.Parameters.AddWithValue("expanded_hub_ids", request.ExpandedHubIds.ToArray());
            cmd.Parameters.AddWithValue("phase_status", phaseStatus);
            cmd.Parameters.AddWithValue("hub_id", request.HubId);
            cmd.Parameters.AddWithValue("hub_relation_id", request.HubRelationId);
            cmd.Parameters.AddWithValue("neighbor_score", request.NeighborScore);
            cmd.Parameters.AddWithValue("hit_rank", request.HitRank);
            cmd.Parameters.AddWithValue("score_band", request.ScoreBand);
            cmd.Parameters.AddWithValue("l2_norm", request.L2Norm);
            cmd.Parameters.AddWithValue("phase_vector_json", request.PhaseVectorJson);
            cmd.Parameters.AddWithValue("evidence_json", request.EvidenceJson);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await InsertAsync(hitId, "sql_attention_hit", null, "not_applicable");
        await InsertAsync(phaseId, "phaseAT", hitId, "evidence");
        await tx.CommitAsync(ct);
        return new AttentionGenerationAppendResult(request.GenerationLineId, hitId, phaseId);
    }

    public override async Task<AttentionLifecycleSource?> LoadAttentionLifecycleSourceAsync(Guid attentionId, CancellationToken ct = default)
    {
        if (attentionId == Guid.Empty) throw new ArgumentException("attentionId must not be empty.", nameof(attentionId));
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT attention_id, current_id, source_set_id, generation_line_id, evidence_kind,
       phase_vector_json::text, source_topology_manifest_ids, hit_hub_relation_ids,
       expanded_hub_relation_ids, expanded_topology_manifest_ids, expanded_hub_ids
  FROM logs.attention WHERE attention_id = @attention_id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("attention_id", attentionId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new AttentionLifecycleSource(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.GetFieldValue<Guid[]>(6), reader.GetFieldValue<Guid[]>(7), reader.GetFieldValue<Guid[]>(8), reader.GetFieldValue<Guid[]>(9), reader.GetFieldValue<Guid[]>(10));
    }

    public override async Task<Guid> AppendAttentionLifecycleEvidenceAsync(AttentionLifecycleAppendRequest request, CancellationToken ct = default)
    {
        ValidateLifecycleAppendRequest(request);
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"
INSERT INTO logs.attention (
  attention_id, current_id, source_current_id, source_set_id, evidence_kind,
  generation_line_id, source_attention_id, source_topology_manifest_ids,
  hit_hub_relation_ids, expanded_hub_relation_ids, expanded_topology_manifest_ids,
  expanded_hub_ids, phase_status, promotion_status, attractor_key,
  phase_vector_json, evidence_json, actor_or_source, command_id, archive_policy
) VALUES (
  @attention_id, @current_id, @current_id, @source_set_id, @evidence_kind,
  @generation_line_id, @source_attention_id, @source_topology_manifest_ids,
  @hit_hub_relation_ids, @expanded_hub_relation_ids, @expanded_topology_manifest_ids,
  @expanded_hub_ids, @phase_status, @promotion_status, 'explicit_evidence_lifecycle',
  @phase_vector_json::jsonb, @evidence_json::jsonb, @actor_or_source, @command_id, 'required'
)";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("attention_id", id);
        cmd.Parameters.AddWithValue("current_id", request.Source.CurrentId);
        cmd.Parameters.AddWithValue("source_set_id", request.Source.SourceSetId);
        cmd.Parameters.AddWithValue("evidence_kind", request.EvidenceKind);
        cmd.Parameters.AddWithValue("generation_line_id", request.Source.GenerationLineId);
        cmd.Parameters.AddWithValue("source_attention_id", request.Source.AttentionId);
        cmd.Parameters.AddWithValue("source_topology_manifest_ids", request.Source.SourceTopologyManifestIds.ToArray());
        cmd.Parameters.AddWithValue("hit_hub_relation_ids", request.Source.HitHubRelationIds.ToArray());
        cmd.Parameters.AddWithValue("expanded_hub_relation_ids", request.Source.ExpandedHubRelationIds.ToArray());
        cmd.Parameters.AddWithValue("expanded_topology_manifest_ids", request.Source.ExpandedTopologyManifestIds.ToArray());
        cmd.Parameters.AddWithValue("expanded_hub_ids", request.Source.ExpandedHubIds.ToArray());
        cmd.Parameters.AddWithValue("phase_status", request.PhaseStatus);
        cmd.Parameters.AddWithValue("promotion_status", request.PromotionStatus);
        cmd.Parameters.AddWithValue("phase_vector_json", request.Source.PhaseVectorJson);
        cmd.Parameters.AddWithValue("evidence_json", request.EvidenceJson);
        cmd.Parameters.AddWithValue("actor_or_source", request.ActorOrSource);
        cmd.Parameters.AddWithValue("command_id", request.CommandId);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    public override async Task<IReadOnlyList<HubCurrentCandidate>> LoadLegacyHubCurrentSupportCacheCandidatesAsync(
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(basisWindow);

        var rows = new List<HubCurrentCandidate>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
select hub_current_id, source_set_id, hub_id, attractor_key, hub_relation_id,
       relation_registry_id, basis_window, attractor_vector_json::text,
       population_count, population_recordcount,
       axis_population_json::text, axis_z_score_json::text, phase_basis_json::text
  from logs.hub_current
 where source_set_id = @p_source_set_id
   and basis_window = @p_basis_window";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("p_source_set_id", sourceSetId);
        cmd.Parameters.AddWithValue("p_basis_window", basisWindow);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new HubCurrentCandidate(
                HubCurrentId: reader.GetGuid(reader.GetOrdinal("hub_current_id")),
                SourceSetId: reader.GetString(reader.GetOrdinal("source_set_id")),
                HubId: reader.IsDBNull(reader.GetOrdinal("hub_id")) ? null : reader.GetGuid(reader.GetOrdinal("hub_id")),
                AttractorKey: reader.GetString(reader.GetOrdinal("attractor_key")),
                HubRelationId: reader.IsDBNull(reader.GetOrdinal("hub_relation_id")) ? null : reader.GetGuid(reader.GetOrdinal("hub_relation_id")),
                RelationRegistryId: reader.IsDBNull(reader.GetOrdinal("relation_registry_id")) ? null : reader.GetGuid(reader.GetOrdinal("relation_registry_id")),
                BasisWindow: reader.GetString(reader.GetOrdinal("basis_window")),
                AttractorVectorJson: reader.GetString(reader.GetOrdinal("attractor_vector_json")),
                PopulationCount: reader.GetInt64(reader.GetOrdinal("population_count")),
                PopulationRecordcount: reader.GetInt64(reader.GetOrdinal("population_recordcount")),
                AxisPopulationJson: reader.GetString(reader.GetOrdinal("axis_population_json")),
                AxisZScoreJson: reader.GetString(reader.GetOrdinal("axis_z_score_json")),
                PhaseBasisJson: reader.GetString(reader.GetOrdinal("phase_basis_json"))));
        }

        _npgsqlLogger.LogInformation("Loaded deprecated SQL Attention logs.hub_current support-cache diagnostics for {SourceSetId}/{BasisWindow}: hubs={HubCount}.", sourceSetId, basisWindow, rows.Count);
        return rows;
    }

    public override async Task<int> WriteLogsAttentionAsync(
        IReadOnlyList<LogsAttentionWriteRequest> requests,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
            return 0;

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

        const string sql = @"
INSERT INTO logs.attention (
    current_id, source_current_id, hub_current_id, source_set_id,
    hub_id, attractor_key, hub_relation_id, relation_registry_id,
    neighbor_score, hit_rank, score_band, permutation_key,
    l2_norm, vector_json, phase_vector_json,
    statistics_json, ema_score, evidence_json,
    archive_policy
) VALUES (
    @current_id, @current_id, @hub_current_id, @source_set_id,
    @hub_id, @attractor_key, @hub_relation_id, @relation_registry_id,
    @neighbor_score, @hit_rank, @score_band, @permutation_key,
    @l2_norm, @vector_json::jsonb, @phase_vector_json::jsonb,
    @statistics_json::jsonb, @ema_score, @evidence_json::jsonb,
    @archive_policy
)";

        var rowsWritten = 0;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        foreach (var request in requests)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("current_id", request.CurrentId);
            cmd.Parameters.AddWithValue("hub_current_id", request.HubCurrentId);
            cmd.Parameters.AddWithValue("source_set_id", request.SourceSetId);
            cmd.Parameters.AddWithValue("hub_id", request.HubId.HasValue ? request.HubId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("attractor_key", request.AttractorKey);
            cmd.Parameters.AddWithValue("hub_relation_id", request.HubRelationId.HasValue ? request.HubRelationId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("relation_registry_id", request.RelationRegistryId.HasValue ? request.RelationRegistryId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("neighbor_score", request.NeighborScore);
            cmd.Parameters.AddWithValue("hit_rank", request.HitRank);
            cmd.Parameters.AddWithValue("score_band", request.ScoreBand);
            cmd.Parameters.AddWithValue("permutation_key", request.PermutationKey);
            cmd.Parameters.AddWithValue("l2_norm", request.L2Norm);
            cmd.Parameters.AddWithValue("vector_json", request.VectorJson);
            cmd.Parameters.AddWithValue("phase_vector_json", request.PhaseVectorJson);
            cmd.Parameters.AddWithValue("statistics_json", request.StatisticsJson);
            cmd.Parameters.AddWithValue("ema_score", request.EmaScore.HasValue ? request.EmaScore.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("evidence_json", request.EvidenceJson);
            cmd.Parameters.AddWithValue("archive_policy", request.ArchivePolicy);

            await cmd.ExecuteNonQueryAsync(ct);
            rowsWritten++;
        }

        _npgsqlLogger.LogInformation(
            "WriteLogsAttentionAsync: wrote {RowsWritten} attention row(s) for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
            rowsWritten, requests[0].SourceSetId, "n/a");

        return rowsWritten;
    }

    public override async Task<IReadOnlyList<AttentionEvidenceRecord>> LoadAttentionEvidenceForProjectionAsync(
        string sourceSetId,
        int topK,
        double minNeighborScore,
        int recentWindowDays,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSetId);
        if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK), "topK must be positive.");
        if (recentWindowDays <= 0) throw new ArgumentOutOfRangeException(nameof(recentWindowDays), "recentWindowDays must be positive.");

        var rows = new List<AttentionEvidenceRecord>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT attention_id, current_id, source_set_id,
       hub_id, attractor_key, hub_relation_id, relation_registry_id,
       neighbor_score, hit_rank, score_band, permutation_key,
       l2_norm,
       vector_json::text       AS vector_json,
       phase_vector_json::text AS phase_vector_json,
       statistics_json::text   AS statistics_json,
       ema_score,
       evidence_json::text     AS evidence_json,
       created_at
  FROM logs.attention
 WHERE source_set_id = @p_source_set_id
   AND evidence_kind IN ('sql_attention_hit', 'phaseAT')
   AND neighbor_score >= @p_min_neighbor_score
   AND created_at >= now() - (@p_recent_window_days || ' days')::interval
 ORDER BY neighbor_score DESC, created_at DESC
 LIMIT @p_top_k";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("p_source_set_id", sourceSetId);
        cmd.Parameters.AddWithValue("p_min_neighbor_score", minNeighborScore);
        cmd.Parameters.AddWithValue("p_recent_window_days", recentWindowDays);
        cmd.Parameters.AddWithValue("p_top_k", topK);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new AttentionEvidenceRecord(
                AttentionId:       reader.GetGuid(reader.GetOrdinal("attention_id")),
                CurrentId:         reader.GetGuid(reader.GetOrdinal("current_id")),
                SourceSetId:       reader.GetString(reader.GetOrdinal("source_set_id")),
                HubId:             reader.IsDBNull(reader.GetOrdinal("hub_id")) ? null : reader.GetGuid(reader.GetOrdinal("hub_id")),
                AttractorKey:      reader.GetString(reader.GetOrdinal("attractor_key")),
                HubRelationId:     reader.IsDBNull(reader.GetOrdinal("hub_relation_id")) ? null : reader.GetGuid(reader.GetOrdinal("hub_relation_id")),
                RelationRegistryId:reader.IsDBNull(reader.GetOrdinal("relation_registry_id")) ? null : reader.GetGuid(reader.GetOrdinal("relation_registry_id")),
                NeighborScore:     reader.GetDouble(reader.GetOrdinal("neighbor_score")),
                HitRank:           reader.IsDBNull(reader.GetOrdinal("hit_rank")) ? 0 : reader.GetInt32(reader.GetOrdinal("hit_rank")),
                ScoreBand:         reader.GetString(reader.GetOrdinal("score_band")),
                PermutationKey:    reader.IsDBNull(reader.GetOrdinal("permutation_key")) ? "" : reader.GetString(reader.GetOrdinal("permutation_key")),
                L2Norm:            reader.GetDouble(reader.GetOrdinal("l2_norm")),
                VectorJson:        reader.GetString(reader.GetOrdinal("vector_json")),
                PhaseVectorJson:   reader.GetString(reader.GetOrdinal("phase_vector_json")),
                StatisticsJson:    reader.GetString(reader.GetOrdinal("statistics_json")),
                EmaScore:          reader.IsDBNull(reader.GetOrdinal("ema_score")) ? null : reader.GetDouble(reader.GetOrdinal("ema_score")),
                EvidenceJson:      reader.GetString(reader.GetOrdinal("evidence_json")),
                CreatedAt:         reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"))
            ));
        }

        _npgsqlLogger.LogInformation(
            "LoadAttentionEvidenceForProjectionAsync: loaded {Count} evidence row(s) for sourceSetId={SourceSetId}.",
            rows.Count, sourceSetId);
        return rows;
    }

    public override async Task AppendLogsDiffAsync(
        LogsDiffAppendRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateLogsDiffRequest(request);

        const string sql = @"
INSERT INTO logs.diff (
    source_set_id, basis_window, physical_table_id, physical_table_name,
    record_id, operation_kind, before_state_or_diff_json, after_state_or_diff_json,
    observed_at, actor_or_source, archive_policy
) VALUES (
    @source_set_id, @basis_window, @physical_table_id, @physical_table_name,
    @record_id, @operation_kind, @before_state_or_diff_json::jsonb, @after_state_or_diff_json::jsonb,
    @observed_at, @actor_or_source, @archive_policy
)";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("source_set_id", request.SourceSetId);
        cmd.Parameters.AddWithValue("basis_window", request.BasisWindow);
        cmd.Parameters.AddWithValue("physical_table_id", request.PhysicalTableId);
        cmd.Parameters.AddWithValue("physical_table_name", request.PhysicalTableName);
        cmd.Parameters.AddWithValue("record_id", request.RecordId);
        cmd.Parameters.AddWithValue("operation_kind", request.OperationKind);
        cmd.Parameters.AddWithValue("before_state_or_diff_json", request.BeforeStateOrDiffJson);
        cmd.Parameters.AddWithValue("after_state_or_diff_json", request.AfterStateOrDiffJson);
        cmd.Parameters.AddWithValue("observed_at", request.ObservedAt);
        cmd.Parameters.AddWithValue("actor_or_source", (object?)request.ActorOrSource ?? DBNull.Value);
        cmd.Parameters.AddWithValue("archive_policy", request.ArchivePolicy);

        await cmd.ExecuteNonQueryAsync(ct);
    }

}
