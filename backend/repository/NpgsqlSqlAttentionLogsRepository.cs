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

        await using var cmd = new NpgsqlCommand(
            "select * from logs.refresh_logs_current_watch(@p_source_set_id, @p_basis_window)", conn);
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
                ChangeReason: reader.IsDBNull(reader.GetOrdinal("change_reason")) ? null : reader.GetString(reader.GetOrdinal("change_reason"))));
        }

        return rows;
    }

    public override async Task<IReadOnlyList<HubCurrentCandidate>> LoadHubCurrentCandidatesAsync(
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
       population_count, population_recordcount
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
                PopulationRecordcount: reader.GetInt64(reader.GetOrdinal("population_recordcount"))));
        }

        _npgsqlLogger.LogInformation("Loaded SQL attention candidates watch/hub for {SourceSetId}/{BasisWindow}: hubs={HubCount}.", sourceSetId, basisWindow, rows.Count);
        return rows;
    }

    public override async Task<int> WriteLogsAttentionAsync(
        HubAttractorExplorationResult explorationResult,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(explorationResult);

        if (explorationResult.Hits.Count == 0)
            return 0;

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

        const string sql = @"
INSERT INTO logs.attention (
    current_id, hub_current_id, source_set_id,
    hub_id, attractor_key, hub_relation_id, relation_registry_id,
    neighbor_score, hit_rank, score_band, permutation_key,
    l2_norm, vector_json, phase_vector_json,
    statistics_json, ema_score, evidence_json,
    archive_policy
) VALUES (
    @current_id, @hub_current_id, @source_set_id,
    @hub_id, @attractor_key, @hub_relation_id, @relation_registry_id,
    @neighbor_score, @hit_rank, @score_band, @permutation_key,
    @l2_norm, @vector_json::jsonb, @phase_vector_json::jsonb,
    @statistics_json::jsonb, @ema_score, @evidence_json::jsonb,
    'required'
)";

        var rowsWritten = 0;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        foreach (var hit in explorationResult.Hits)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("current_id", hit.CurrentId);
            cmd.Parameters.AddWithValue("hub_current_id", hit.HubCurrentId);
            cmd.Parameters.AddWithValue("source_set_id", hit.SourceSetId);
            cmd.Parameters.AddWithValue("hub_id", hit.HubId.HasValue ? hit.HubId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("attractor_key", hit.AttractorKey);
            cmd.Parameters.AddWithValue("hub_relation_id", hit.HubRelationId.HasValue ? hit.HubRelationId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("relation_registry_id", hit.RelationRegistryId.HasValue ? hit.RelationRegistryId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("neighbor_score", hit.NeighborScore);
            cmd.Parameters.AddWithValue("hit_rank", hit.HitRank);
            cmd.Parameters.AddWithValue("score_band", hit.ScoreBand);
            cmd.Parameters.AddWithValue("permutation_key", hit.PermutationKey);
            cmd.Parameters.AddWithValue("l2_norm", 0.0);
            cmd.Parameters.AddWithValue("vector_json", "{}");
            cmd.Parameters.AddWithValue("phase_vector_json", "{}");
            cmd.Parameters.AddWithValue("statistics_json", "{}");
            cmd.Parameters.AddWithValue("ema_score", DBNull.Value);
            cmd.Parameters.AddWithValue("evidence_json", "{}");

            await cmd.ExecuteNonQueryAsync(ct);
            rowsWritten++;
        }

        _npgsqlLogger.LogInformation(
            "WriteLogsAttentionAsync: wrote {RowsWritten} attention row(s) for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
            rowsWritten, explorationResult.SourceSetId, explorationResult.BasisWindow);

        return rowsWritten;
    }
}
