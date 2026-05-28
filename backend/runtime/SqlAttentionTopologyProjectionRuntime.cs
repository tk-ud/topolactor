using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Child projection runtime that consumes SQL Attention evidence (logs.attention)
/// and produces topology recommendation candidates for user-visible surfaces.
///
/// Parent / child boundary:
///   Parent  — SQL Attention DB-native hub-attractor observation (logs.current / logs.hub_current / logs.attention).
///   Child   — this service; reads evidence, produces projection candidates, never mutates the evidence layer.
///
/// Evidence layer separation per SSOT is preserved throughout:
///   statistics layer  : statistics_json  (convergence confidence / stability / continuity)
///   attention layer   : vector_json      (current excitation / neighbor hit)
///   phase_attention   : phase_vector_json (exploratory variance / shifted candidate direction)
///
/// Policy source: topologys.function_parameters
///   function_name = 'sql_attention_topology_projection'
///   parameter_key = 'default_policy'
///
/// Prohibited:
///   - Writing to logs.attention or any parent evidence layer.
///   - Auto-mutating registry / topology / fixed route from evidence.
///   - Magic number policy defaults in runtime code.
///   - Silent fallback on policy missing / invalid / DB unavailable.
/// </summary>
public class SqlAttentionTopologyProjectionRuntime
{
    internal const string ProjectionFunctionName = "sql_attention_topology_projection";
    internal const string ProjectionPolicyKey = "default_policy";

    private readonly ILogger<SqlAttentionTopologyProjectionRuntime> _logger;
    private readonly SqlAttentionLogsRepository _sqlAttentionLogsRepository;
    private readonly TopologyRepository _topologyRepository;

    public SqlAttentionTopologyProjectionRuntime(
        ILogger<SqlAttentionTopologyProjectionRuntime> logger,
        SqlAttentionLogsRepository sqlAttentionLogsRepository,
        TopologyRepository topologyRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sqlAttentionLogsRepository = sqlAttentionLogsRepository ?? throw new ArgumentNullException(nameof(sqlAttentionLogsRepository));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    /// <summary>
    /// Projects SQL Attention evidence into topology recommendation candidates.
    ///
    /// Returns:
    ///   MissingPolicy   — policy not found in function_parameters; explicit failure.
    ///   MalformedPolicy — policy JSON is invalid or required keys are missing/non-positive; explicit failure.
    ///   DbUnavailable   — logs.attention query failed; explicit failure.
    ///   NoEvidence      — no recent evidence rows found; expected for cold start; not an error.
    ///   Ok              — candidates projected; Candidates may still be empty if evidence was filtered out.
    ///
    /// This method is read-only relative to the evidence layer.
    /// It never writes to logs.attention, logs.current, logs.hub_current, registry, or topology.
    /// </summary>
    public async Task<SqlAttentionTopologyProjectionResult> ProjectAsync(
        string sourceSetId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSetId);

        var evaluatedAt = DateTimeOffset.UtcNow;

        var policyJson = await _topologyRepository.LoadFunctionParameterAsync(
            ProjectionFunctionName, ProjectionPolicyKey, ct);

        if (policyJson is null)
        {
            _logger.LogError(
                "SqlAttentionTopologyProjectionRuntime: MissingPolicy — no active function_parameters row for '{Fn}/{Key}'.",
                ProjectionFunctionName, ProjectionPolicyKey);
            return Failure(
                TopologyProjectionStatus.MissingPolicy,
                $"No active function_parameters row for '{ProjectionFunctionName}/{ProjectionPolicyKey}'.",
                evaluatedAt);
        }

        SqlAttentionTopologyProjectionPolicy policy;
        try
        {
            policy = ParsePolicy(policyJson);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            _logger.LogError(ex,
                "SqlAttentionTopologyProjectionRuntime: MalformedPolicy — '{Fn}/{Key}' could not be parsed.",
                ProjectionFunctionName, ProjectionPolicyKey);
            return Failure(
                TopologyProjectionStatus.MalformedPolicy,
                $"Policy JSON for '{ProjectionFunctionName}/{ProjectionPolicyKey}' is malformed: {ex.Message}",
                evaluatedAt);
        }

        IReadOnlyList<AttentionEvidenceRecord> evidence;
        try
        {
            evidence = await _sqlAttentionLogsRepository.LoadAttentionEvidenceForProjectionAsync(
                sourceSetId, policy.TopK, policy.MinNeighborScore, policy.RecentWindowDays, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SqlAttentionTopologyProjectionRuntime: DbUnavailable — logs.attention query failed for sourceSetId={SourceSetId}.",
                sourceSetId);
            return Failure(
                TopologyProjectionStatus.DbUnavailable,
                $"logs.attention query failed for sourceSetId='{sourceSetId}': {ex.Message}",
                evaluatedAt);
        }

        if (evidence.Count == 0)
        {
            _logger.LogDebug(
                "SqlAttentionTopologyProjectionRuntime: NoEvidence — no evidence rows for sourceSetId={SourceSetId}.",
                sourceSetId);
            return new SqlAttentionTopologyProjectionResult(
                Status: TopologyProjectionStatus.NoEvidence,
                StatusDetail: "No recent attention evidence rows found.",
                Candidates: [],
                EvaluatedAt: evaluatedAt);
        }

        var candidates = ProjectCandidates(evidence);

        _logger.LogInformation(
            "SqlAttentionTopologyProjectionRuntime: projected {Count} candidate(s) from {EvidenceCount} evidence row(s) for sourceSetId={SourceSetId}.",
            candidates.Count, evidence.Count, sourceSetId);

        return new SqlAttentionTopologyProjectionResult(
            Status: TopologyProjectionStatus.Ok,
            StatusDetail: null,
            Candidates: candidates,
            EvaluatedAt: evaluatedAt);
    }

    // ---------------------------------------------------------------------------
    // Evidence → candidate projection
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Projects evidence rows into recommendation candidates.
    /// Evidence layer separation is preserved — all three layers (statistics/attention/phase_attention)
    /// are carried through as-is on each candidate.
    /// AttentionScore is derived for display/ranking and must not be written back to evidence.
    /// </summary>
    internal static IReadOnlyList<TopologyProjectionCandidate> ProjectCandidates(
        IReadOnlyList<AttentionEvidenceRecord> evidence)
    {
        var candidates = new List<TopologyProjectionCandidate>(evidence.Count);
        foreach (var row in evidence)
        {
            // AttentionScore: display/ranking score derived from neighbor_score × l2_norm.
            // Not collapsed into a single evidence score — evidence layers are preserved separately.
            var attentionScore = row.NeighborScore * row.L2Norm;

            candidates.Add(new TopologyProjectionCandidate(
                HubId:             row.HubId,
                AttractorKey:      row.AttractorKey,
                RelationRegistryId:row.RelationRegistryId,
                AttentionScore:    attentionScore,
                ScoreBand:         row.ScoreBand,
                StatisticsJson:    row.StatisticsJson,
                VectorJson:        row.VectorJson,
                PhaseVectorJson:   row.PhaseVectorJson,
                EvidenceJson:      row.EvidenceJson,
                HitRank:           row.HitRank
            ));
        }
        return candidates;
    }

    // ---------------------------------------------------------------------------
    // Policy parsing
    // ---------------------------------------------------------------------------

    private static SqlAttentionTopologyProjectionPolicy ParsePolicy(string json)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        var topK = GetPositiveInt(element, "top_k");
        var minNeighborScore = GetPositiveDouble(element, "min_neighbor_score");
        var recentWindowDays = GetPositiveInt(element, "recent_window_days");

        return new SqlAttentionTopologyProjectionPolicy(
            TopK: topK,
            MinNeighborScore: minNeighborScore,
            RecentWindowDays: recentWindowDays);
    }

    private static int GetPositiveInt(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var el))
            throw new KeyNotFoundException($"Policy key '{key}' is missing.");
        var value = el.GetInt32();
        if (value <= 0)
            throw new InvalidOperationException($"Policy key '{key}' must be a positive integer, got {value}.");
        return value;
    }

    private static double GetPositiveDouble(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var el))
            throw new KeyNotFoundException($"Policy key '{key}' is missing.");
        var value = el.GetDouble();
        if (value <= 0.0)
            throw new InvalidOperationException($"Policy key '{key}' must be positive, got {value}.");
        return value;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static SqlAttentionTopologyProjectionResult Failure(
        TopologyProjectionStatus status,
        string detail,
        DateTimeOffset evaluatedAt) =>
        new(
            Status: status,
            StatusDetail: detail,
            Candidates: [],
            EvaluatedAt: evaluatedAt);
}
