using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Hub-attractor exploration runtime for SQL Attention.
///
/// Consumes watch change candidates from logs.refresh_logs_current_watch and performs
/// bounded hub-attractor topK neighbor exploration. Returns an exploration result that
/// downstream write_logs_attention can consume — does NOT write to logs.attention here.
///
/// Policy source: topologys.function_parameters
///   function_name = "sql_attention_hub_attractor_exploration"
///   parameter_key = "default_policy"
///   Required keys: topK_per_hub_kind, max_hub_kinds_per_current, max_hub_tables_per_kind,
///                  phase_expansion_limit, max_attention_rows_saved
///
/// Fail-close invariants:
///   - Policy missing (no active function_parameters row) → MissingPolicy
///   - Policy JSON malformed or required keys missing/non-positive → MalformedPolicy
///   - No change candidates → NoChange (exploration skipped, not an error)
///
/// Prohibited:
///   - Writing to logs.attention (write_logs_attention boundary is a separate TODO)
///   - phase_vector generation
///   - registry mutation / migration / column promotion
///   - Magic number policy defaults in runtime code
/// </summary>
public class HubAttractorExplorationRuntime
{
    internal const string ExplorationFunctionName = "sql_attention_hub_attractor_exploration";
    internal const string ExplorationPolicyKey = "default_policy";

    private readonly ILogger<HubAttractorExplorationRuntime> _logger;
    private readonly TopologyRepository _topologyRepository;
    private readonly SqlAttentionLogsRepository _sqlAttentionLogsRepository;

    public HubAttractorExplorationRuntime(
        ILogger<HubAttractorExplorationRuntime> logger,
        TopologyRepository topologyRepository,
        SqlAttentionLogsRepository sqlAttentionLogsRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _sqlAttentionLogsRepository = sqlAttentionLogsRepository ?? throw new ArgumentNullException(nameof(sqlAttentionLogsRepository));
    }

    /// <summary>
    /// Executes one hub-attractor exploration run.
    ///
    /// Returns:
    ///   NoChange   — no change candidates detected; exploration skipped.
    ///   MissingPolicy  — no active policy row; fail-close.
    ///   MalformedPolicy — policy JSON invalid or required key invalid; fail-close.
    ///   Ok         — exploration ran; Result contains hits (may be empty if no hub current records).
    ///
    /// Never writes to logs.attention. Phase vector generation is not performed here.
    /// </summary>
    public async Task<HubAttractorExplorationRunResult> ExploreAsync(
        IReadOnlyList<WatchChangeCandidate> candidates,
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(basisWindow);

        var executedAt = DateTimeOffset.UtcNow;

        var changeCandidates = candidates.Where(c => c.ChangeDetected).ToList();
        if (changeCandidates.Count == 0)
        {
            _logger.LogDebug(
                "HubAttractorExplorationRuntime: no change candidates — skipping exploration for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
                sourceSetId, basisWindow);
            return new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.NoChange,
                "No change candidates detected — exploration skipped.",
                null,
                executedAt);
        }

        var policyJson = await _topologyRepository.LoadFunctionParameterAsync(
            ExplorationFunctionName, ExplorationPolicyKey, ct);

        if (policyJson is null)
        {
            _logger.LogError(
                "HubAttractorExplorationRuntime: MissingPolicy — no active function_parameters row for '{Fn}/{Key}'.",
                ExplorationFunctionName, ExplorationPolicyKey);
            return new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.MissingPolicy,
                $"No active function_parameters row for '{ExplorationFunctionName}/{ExplorationPolicyKey}'.",
                null,
                executedAt);
        }

        HubAttractorExplorationPolicy policy;
        try
        {
            var raw = JsonSerializer.Deserialize<JsonElement>(policyJson);
            policy = ParsePolicy(raw);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            _logger.LogError(ex,
                "HubAttractorExplorationRuntime: MalformedPolicy — '{Fn}/{Key}' could not be parsed.",
                ExplorationFunctionName, ExplorationPolicyKey);
            return new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.MalformedPolicy,
                $"Policy JSON for '{ExplorationFunctionName}/{ExplorationPolicyKey}' is malformed: {ex.Message}",
                null,
                executedAt);
        }

        var validationError = ValidatePolicy(policy);
        if (validationError is not null)
        {
            _logger.LogError(
                "HubAttractorExplorationRuntime: MalformedPolicy — invalid policy value: {Detail}",
                validationError);
            return new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.MalformedPolicy,
                $"Policy for '{ExplorationFunctionName}/{ExplorationPolicyKey}' has invalid value: {validationError}",
                null,
                executedAt);
        }

        var hubCurrentCandidates = await _sqlAttentionLogsRepository.LoadHubCurrentCandidatesAsync(
            sourceSetId, basisWindow, ct);

        var hits = RunExploration(changeCandidates, hubCurrentCandidates, policy, sourceSetId);

        _logger.LogInformation(
            "HubAttractorExplorationRuntime: exploration complete — sourceSetId={SourceSetId} basisWindow={BasisWindow} changeCandidates={ChangeCandidateCount} hubCurrentCandidates={HubCurrentCount} hits={HitCount}.",
            sourceSetId, basisWindow, changeCandidates.Count, hubCurrentCandidates.Count, hits.Count);

        return new HubAttractorExplorationRunResult(
            HubAttractorExplorationStatus.Ok,
            $"Exploration complete: {hits.Count} hit(s) from {changeCandidates.Count} change candidate(s).",
            new HubAttractorExplorationResult(sourceSetId, basisWindow, hits),
            executedAt);
    }

    /// <summary>
    /// Runs bounded hub-attractor topK exploration.
    /// Applies max_hub_kinds_per_current, topK_per_hub_kind, max_hub_tables_per_kind,
    /// phase_expansion_limit, and max_attention_rows_saved caps.
    /// Does NOT write to logs.attention.
    /// </summary>
    private static IReadOnlyList<HubAttractorExplorationHit> RunExploration(
        IReadOnlyList<WatchChangeCandidate> changeCandidates,
        IReadOnlyList<HubCurrentCandidate> hubCurrentCandidates,
        HubAttractorExplorationPolicy policy,
        string sourceSetId)
    {
        var hits = new List<HubAttractorExplorationHit>();

        var hubsByKind = hubCurrentCandidates
            .GroupBy(h => h.AttractorKey)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var candidate in changeCandidates)
        {
            if (hits.Count >= policy.MaxAttentionRowsSaved)
                break;

            var kindsProcessed = 0;

            foreach (var (attractorKey, hubsInKind) in hubsByKind)
            {
                if (kindsProcessed >= policy.MaxHubKindsPerCurrent)
                    break;
                if (hits.Count >= policy.MaxAttentionRowsSaved)
                    break;

                var permutations = Math.Min(policy.PhaseExpansionLimit, 1);
                for (var permIdx = 0; permIdx < permutations; permIdx++)
                {
                    if (hits.Count >= policy.MaxAttentionRowsSaved)
                        break;

                    var permutationKey = permIdx == 0 ? "default" : $"permutation_{permIdx}";

                    var tablesForKind = hubsInKind
                        .Take(policy.MaxHubTablesPerKind)
                        .ToList();

                    var scoredHubs = tablesForKind
                        .Select(h => (Hub: h, Score: ComputeNeighborScore(candidate, h)))
                        .OrderByDescending(x => x.Score)
                        .Take(policy.TopKPerHubKind)
                        .ToList();

                    for (var rank = 0; rank < scoredHubs.Count; rank++)
                    {
                        if (hits.Count >= policy.MaxAttentionRowsSaved)
                            break;

                        var (hub, score) = scoredHubs[rank];
                        hits.Add(new HubAttractorExplorationHit(
                            CurrentId: candidate.CurrentId,
                            HubCurrentId: hub.HubCurrentId,
                            SourceSetId: sourceSetId,
                            HubId: hub.HubId,
                            AttractorKey: hub.AttractorKey,
                            HubRelationId: hub.HubRelationId,
                            RelationRegistryId: hub.RelationRegistryId,
                            NeighborScore: score,
                            HitRank: rank + 1,
                            ScoreBand: ClassifyScoreBand(score),
                            PermutationKey: permutationKey
                        ));
                    }
                }

                kindsProcessed++;
            }
        }

        return hits;
    }

    /// <summary>
    /// Computes a neighbor score between a watch candidate and a hub current candidate.
    /// Score is derived from population-normalized L2 pressure ratio.
    /// Range: [0.0, 1.0]. Higher = stronger attractor hit.
    ///
    /// Note: This is a structural approximation. Full vector cosine similarity requires
    /// logs.hub_current refresh implementation (separate TODO).
    /// </summary>
    private static double ComputeNeighborScore(
        WatchChangeCandidate candidate,
        HubCurrentCandidate hub)
    {
        if (hub.PopulationCount <= 0)
            return 0.0;

        var normBase = Math.Sqrt(hub.PopulationCount * hub.PopulationCount +
                                  hub.PopulationRecordcount * hub.PopulationRecordcount);
        if (normBase <= 0.0)
            return 0.0;

        // Score based on attractor key string match with candidate physical table id.
        // This is a structural stub — production scoring requires attractor_vector_json × basis_vector_json.
        var keyMatchBonus = hub.AttractorKey.Contains(candidate.PhysicalTableId,
            StringComparison.OrdinalIgnoreCase) ? 0.1 : 0.0;

        var normScore = Math.Min(normBase / (normBase + 1.0), 1.0);
        return Math.Min(normScore + keyMatchBonus, 1.0);
    }

    /// <summary>
    /// Classifies a neighbor score into a score band per SSOT neighbor_score_policy_range.
    /// strong: 0.95-1.00, normal: 0.90-0.95, exploratory: 0.85-0.90, evidence_only: below 0.85.
    /// </summary>
    internal static string ClassifyScoreBand(double score) =>
        score >= 0.95 ? "strong"
        : score >= 0.90 ? "normal"
        : score >= 0.85 ? "exploratory"
        : "evidence_only";

    /// <summary>
    /// Parses the policy JSON element into a HubAttractorExplorationPolicy.
    /// Throws InvalidOperationException if required keys are missing.
    /// </summary>
    private static HubAttractorExplorationPolicy ParsePolicy(JsonElement root)
    {
        static int RequireInt(JsonElement root, string key)
        {
            if (!root.TryGetProperty(key, out var el))
                throw new InvalidOperationException(
                    $"Required policy key '{key}' is missing.");

            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
                return v;

            if (el.ValueKind == JsonValueKind.String &&
                int.TryParse(el.GetString(), out var sv))
                return sv;

            throw new InvalidOperationException(
                $"Required policy key '{key}' is not a valid integer.");
        }

        return new HubAttractorExplorationPolicy(
            TopKPerHubKind: RequireInt(root, "topK_per_hub_kind"),
            MaxHubKindsPerCurrent: RequireInt(root, "max_hub_kinds_per_current"),
            MaxHubTablesPerKind: RequireInt(root, "max_hub_tables_per_kind"),
            PhaseExpansionLimit: RequireInt(root, "phase_expansion_limit"),
            MaxAttentionRowsSaved: RequireInt(root, "max_attention_rows_saved")
        );
    }

    /// <summary>
    /// Validates policy values. All must be positive integers.
    /// Returns a human-readable error message, or null when valid.
    /// </summary>
    private static string? ValidatePolicy(HubAttractorExplorationPolicy policy)
    {
        if (policy.TopKPerHubKind <= 0)
            return $"topK_per_hub_kind={policy.TopKPerHubKind} must be > 0.";
        if (policy.MaxHubKindsPerCurrent <= 0)
            return $"max_hub_kinds_per_current={policy.MaxHubKindsPerCurrent} must be > 0.";
        if (policy.MaxHubTablesPerKind <= 0)
            return $"max_hub_tables_per_kind={policy.MaxHubTablesPerKind} must be > 0.";
        if (policy.PhaseExpansionLimit <= 0)
            return $"phase_expansion_limit={policy.PhaseExpansionLimit} must be > 0.";
        if (policy.MaxAttentionRowsSaved <= 0)
            return $"max_attention_rows_saved={policy.MaxAttentionRowsSaved} must be > 0.";
        return null;
    }
}
