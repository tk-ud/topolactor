using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves context route recommendations: next operations and next token candidates
/// derived from nearest historical context prefix search + transition statistics.
///
/// Insertion point in the canonical runtime route:
///   ... → component_expand → context_route_recommendation_resolve → emission_or_projection
///
/// Policy source: function_parameters (function_name = 'context_route_recommendation_resolve',
/// parameter_key = 'default_policy') stored in the topology data store.
/// Policy is NOT read from an independent config table — it is resolved from stored topology data.
///
/// Status is always explicit:
///   Ok                  — candidates available
///   InsufficientHistory — not enough history to recommend (not an error; expected cold start)
///   ExplicitError       — pipeline failure, including policy-missing and policy-invalid
///
/// No silent fallback. No business-specific naming in this layer.
/// </summary>
public class ContextRouteRecommendationResolver
{
    private const string PolicyFunctionName = "context_route_recommendation_resolve";
    private const string PolicyParameterKey = "default_policy";

    private readonly ILogger<ContextRouteRecommendationResolver> _logger;
    private readonly ContextRouteRepository _contextRouteRepository;
    private readonly TopologyRepository _topologyRepository;
    private readonly ContextVectorBuilder _vectorBuilder;
    private readonly ContextNeighborSearch _neighborSearch;

    public ContextRouteRecommendationResolver(
        ILogger<ContextRouteRecommendationResolver> logger,
        ContextRouteRepository contextRouteRepository,
        ContextVectorBuilder vectorBuilder,
        ContextNeighborSearch neighborSearch,
        TopologyRepository topologyRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextRouteRepository = contextRouteRepository ?? throw new ArgumentNullException(nameof(contextRouteRepository));
        _vectorBuilder = vectorBuilder ?? throw new ArgumentNullException(nameof(vectorBuilder));
        _neighborSearch = neighborSearch ?? throw new ArgumentNullException(nameof(neighborSearch));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    /// <summary>
    /// Resolves context route recommendations for the given working shape.
    /// Loads policy from function_parameters; returns ExplicitError if policy is absent or invalid.
    /// </summary>
    public async Task<ContextRouteRecommendationResult> ResolveAsync(
        RuntimeWorkingShape shape,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shape);

        // Resolve policy key: use context_route_policy_ref from structure_maps.state_policy
        // when present, otherwise fall back to the global default_policy key.
        var (policyKey, policyKeyError) = ResolvePolicyKey(shape.StructureMapStatePolicyJson);
        if (policyKeyError is not null)
        {
            _logger.LogWarning(
                "ContextRouteRecommendationResolver: state_policy resolution failed — {ErrorCode}.",
                policyKeyError);
            return ExplicitError(policyKeyError);
        }

        var policyJson = await _topologyRepository.LoadFunctionParameterAsync(
            PolicyFunctionName, policyKey!, ct);

        if (policyJson is null)
        {
            _logger.LogWarning(
                "ContextRouteRecommendationResolver: function_parameter '{FunctionName}/{Key}' not found — CONTEXT_ROUTE_POLICY_NOT_FOUND.",
                PolicyFunctionName, policyKey);
            return ExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND");
        }

        ContextRoutePolicy policy;
        try
        {
            policy = ParsePolicy(policyJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ContextRouteRecommendationResolver: policy JSON is invalid — CONTEXT_ROUTE_POLICY_INVALID.");
            return ExplicitError($"CONTEXT_ROUTE_POLICY_INVALID:{ex.Message}");
        }

        var vector = shape.Vector;
        if (vector is null)
            return ExplicitError("MISSING_VECTOR");

        if (string.IsNullOrWhiteSpace(vector.ContextSessionId) ||
            !Guid.TryParse(vector.ContextSessionId, out var sessionId))
        {
            _logger.LogDebug("ContextRouteRecommendationResolver: no ContextSessionId — returning InsufficientHistory.");
            return InsufficientHistory("NO_SESSION_ID");
        }

        var tokenIds = ParseTokenIds(vector.ContextTokenIds);
        var currentOperation = vector.AttractorKey ?? "";
        var role = vector.UserRole;
        var tableName = (string?)null;

        // Load token registry values for the current event
        IReadOnlyDictionary<Guid, float> tokenValueMap;
        if (tokenIds.Count > 0)
        {
            var tokens = await _contextRouteRepository.LoadActiveTokensAsync(tokenIds, ct);
            tokenValueMap = tokens.ToDictionary(t => t.TokenId, t => t.Value);
        }
        else
        {
            tokenValueMap = new Dictionary<Guid, float>();
        }

        var eventVector = _vectorBuilder.BuildEventVector(tokenIds, tokenValueMap);
        var eventNorm = _vectorBuilder.ComputeL2Norm(eventVector);

        // Append context event — non-fatal
        var contextEvent = new ContextEventRecord(
            EventId: Guid.NewGuid(),
            SessionId: sessionId,
            UserId: vector.ContextUserId,
            Role: role,
            TableName: tableName,
            RecordId: vector.ContextRecordId,
            Operation: currentOperation,
            TokenIds: tokenIds,
            CreatedAt: DateTimeOffset.UtcNow,
            NextOperationHint: null,
            NextTokenIdsHint: null
        );

        try
        {
            await _contextRouteRepository.AppendContextEventAsync(contextEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContextRouteRepository.AppendContextEventAsync failed — continuing.");
        }

        // Load prefix vector candidates.
        var prefixCandidates = await _contextRouteRepository.LoadRecentPrefixVectorsAsync(
            tableName, role, policy.RecentDays, ct);

        if (prefixCandidates.Count == 0)
        {
            _logger.LogDebug("ContextRouteRecommendationResolver: no prefix candidates — InsufficientHistory.");
            return InsufficientHistory("NO_CONTEXT_HISTORY");
        }

        // Find nearest prefixes
        var neighbors = _neighborSearch.FindNearestPrefixes(
            eventVector, eventNorm, prefixCandidates, policy.MinSimilarity, policy.TopK);

        if (neighbors.Count < policy.MinNeighbors)
        {
            _logger.LogDebug(
                "ContextRouteRecommendationResolver: {Count} neighbors < min {Min} — InsufficientHistory.",
                neighbors.Count, policy.MinNeighbors);
            return InsufficientHistory("INSUFFICIENT_CONTEXT_HISTORY");
        }

        // Load transition stats for baseline.
        // When TransitionAggregation policy is set, compute windowed stats from raw context_event rows
        // instead of reading from the pre-aggregated context_transition_stats table.
        var transitionStats = new List<ContextTransitionStat>();
        if (!string.IsNullOrWhiteSpace(currentOperation))
        {
            try
            {
                IReadOnlyList<ContextTransitionStat> stats;
                if (policy.TransitionAggregation is not null)
                {
                    stats = await _contextRouteRepository.GetWindowedTransitionStatsAsync(
                        currentOperation, role, policy.TransitionAggregation, policy.MaxCandidatesShown, ct);
                }
                else
                {
                    stats = await _contextRouteRepository.GetTransitionStatsAsync(currentOperation, role, policy.MaxCandidatesShown, ct);
                }
                transitionStats.AddRange(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContextRouteRepository transition stats query failed — continuing without baseline.");
            }
        }

        var nextOperations = ResolveNextOperations(neighbors, transitionStats, policy);
        var nextTokens = ResolveNextTokens(neighbors, policy);
        var nearestIds = neighbors.Take(policy.MaxCandidatesShown).Select(n => n.SessionId).Distinct().ToList();
        var contributing = GetContributingTokenIds(eventVector);

        return new ContextRouteRecommendationResult(
            NextOperations: nextOperations,
            NextTokens: nextTokens,
            NearestPrefixSessionIds: nearestIds,
            ContributingTokens: contributing,
            Status: RecommendationStatus.Ok,
            StatusDetail: null
        );
    }

    /// <summary>
    /// Derives next operation candidates by blending neighbor voting with transition stat baseline.
    /// </summary>
    public IReadOnlyList<RecommendationCandidate> ResolveNextOperations(
        IReadOnlyList<ContextNeighborResult> neighbors,
        IReadOnlyList<ContextTransitionStat> transitionStats,
        ContextRoutePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(neighbors);
        ArgumentNullException.ThrowIfNull(transitionStats);
        ArgumentNullException.ThrowIfNull(policy);

        var neighborVotes = new Dictionary<string, (float Score, int Count)>();
        foreach (var n in neighbors)
        {
            if (string.IsNullOrWhiteSpace(n.NextOperation))
                continue;
            var (s, c) = neighborVotes.GetValueOrDefault(n.NextOperation);
            neighborVotes[n.NextOperation] = (s + n.Similarity * policy.NeighborWeight, c + 1);
        }

        var baselineVotes = new Dictionary<string, float>();
        foreach (var stat in transitionStats)
            baselineVotes[stat.NextOperation] = stat.Prob01 * policy.BaselineWeight;

        var merged = new Dictionary<string, float>();
        foreach (var (op, score) in neighborVotes)
            merged[op] = merged.GetValueOrDefault(op) + score.Score;
        foreach (var (op, score) in baselineVotes)
            merged[op] = merged.GetValueOrDefault(op) + score;

        return merged
            .OrderByDescending(kv => kv.Value)
            .Take(policy.MaxCandidatesShown)
            .Select(kv =>
            {
                var evidence = new List<string>();
                if (neighborVotes.TryGetValue(kv.Key, out var nv))
                    evidence.Add($"neighbor_count={nv.Count} neighbor_score={nv.Score:F3}");
                if (baselineVotes.TryGetValue(kv.Key, out var bv))
                    evidence.Add($"baseline_score={bv:F3}");
                return new RecommendationCandidate(
                    Value: kv.Key,
                    Score: kv.Value,
                    Probability: null,
                    Evidence: evidence
                );
            })
            .ToList();
    }

    /// <summary>
    /// Derives next token candidates from neighbor next_token_ids_hint voting.
    /// </summary>
    public IReadOnlyList<RecommendationCandidate> ResolveNextTokens(
        IReadOnlyList<ContextNeighborResult> neighbors,
        ContextRoutePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(neighbors);
        ArgumentNullException.ThrowIfNull(policy);

        var votes = new Dictionary<Guid, (float Score, int Count)>();
        foreach (var n in neighbors)
        {
            if (n.NextTokenIdsHint is null)
                continue;
            foreach (var tokenId in n.NextTokenIdsHint)
            {
                var (s, c) = votes.GetValueOrDefault(tokenId);
                votes[tokenId] = (s + n.Similarity, c + 1);
            }
        }

        return votes
            .OrderByDescending(kv => kv.Value.Score)
            .Take(policy.MaxCandidatesShown)
            .Select(kv => new RecommendationCandidate(
                Value: kv.Key.ToString(),
                Score: kv.Value.Score,
                Probability: null,
                Evidence: [$"neighbor_count={kv.Value.Count}", $"total_sim={kv.Value.Score:F3}"]
            ))
            .ToList();
    }

    // ---------------------------------------------------------------------------
    // Policy key resolution
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Resolves the function_parameters key for this recommendation resolver.
    /// When structure_maps.state_policy contains "context_route_policy_ref", that key
    /// is used instead of the global "default_policy" — enabling per-structure-map,
    /// per-relation, and per-hub scoped policy without code changes.
    ///
    /// Return semantics:
    ///   (key, null)                              — success; use key to load policy
    ///   (null, "CONTEXT_ROUTE_STATE_POLICY_INVALID") — malformed JSON
    ///   (null, "CONTEXT_ROUTE_POLICY_REF_INVALID")   — context_route_policy_ref exists but is empty/whitespace
    ///
    /// Null/empty/`{}` state_policy with no context_route_policy_ref → (default_policy, null).
    /// No silent fallback on broken refs.
    /// </summary>
    private static (string? Key, string? ErrorCode) ResolvePolicyKey(string? statePolicyJson)
    {
        if (string.IsNullOrWhiteSpace(statePolicyJson))
            return (PolicyParameterKey, null);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(statePolicyJson);
        }
        catch (JsonException)
        {
            return (null, "CONTEXT_ROUTE_STATE_POLICY_INVALID");
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("context_route_policy_ref", out var refEl))
                return (PolicyParameterKey, null);

            var policyRef = refEl.GetString();
            if (string.IsNullOrWhiteSpace(policyRef))
                return (null, "CONTEXT_ROUTE_POLICY_REF_INVALID");

            return (policyRef, null);
        }
    }

    // ---------------------------------------------------------------------------
    // Policy parsing
    // ---------------------------------------------------------------------------

    private static ContextRoutePolicy ParsePolicy(string json)
    {
        var dto = JsonSerializer.Deserialize<PolicyDto>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Policy JSON deserialized to null.");

        TransitionAggregationPolicy? transitionAggregation = dto.TransitionAggregation is not null
            ? new TransitionAggregationPolicy(
                AggregationLimit: dto.TransitionAggregation.AggregationLimit,
                PreferRecent:     dto.TransitionAggregation.PreferRecent,
                RecentDays:       dto.TransitionAggregation.RecentDays)
            : null;

        return new ContextRoutePolicy(
            MinSimilarity:        dto.MinSimilarity,
            TopK:                 dto.TopK,
            MinNeighbors:         dto.MinNeighbors,
            RecentDays:           dto.RecentDays,
            MaxCandidatesShown:   dto.MaxCandidatesShown,
            BaselineWeight:       dto.BaselineWeight,
            NeighborWeight:       dto.NeighborWeight,
            TransitionAggregation: transitionAggregation
        );
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private record TransitionAggregationDto(
        [property: JsonPropertyName("aggregation_limit")] int  AggregationLimit,
        [property: JsonPropertyName("prefer_recent")]     bool PreferRecent,
        [property: JsonPropertyName("recent_days")]       int? RecentDays
    );

    private record PolicyDto(
        [property: JsonPropertyName("min_similarity")]         float MinSimilarity,
        [property: JsonPropertyName("top_k")]                  int   TopK,
        [property: JsonPropertyName("min_neighbors")]          int   MinNeighbors,
        [property: JsonPropertyName("recent_days")]            int?  RecentDays,
        [property: JsonPropertyName("max_candidates_shown")]   int   MaxCandidatesShown,
        [property: JsonPropertyName("baseline_weight")]        float BaselineWeight,
        [property: JsonPropertyName("neighbor_weight")]        float NeighborWeight,
        [property: JsonPropertyName("transition_aggregation")] TransitionAggregationDto? TransitionAggregation = null
    );

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IReadOnlyList<Guid> ParseTokenIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? (Guid?)g : null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();
    }

    private static IReadOnlyList<string> GetContributingTokenIds(
        IReadOnlyDictionary<Guid, float> vector) =>
        vector.Keys.Select(id => id.ToString()).ToList();

    private static ContextRouteRecommendationResult InsufficientHistory(string detail) =>
        new(
            NextOperations: [],
            NextTokens: [],
            NearestPrefixSessionIds: [],
            ContributingTokens: [],
            Status: RecommendationStatus.InsufficientHistory,
            StatusDetail: detail
        );

    private static ContextRouteRecommendationResult ExplicitError(string detail) =>
        new(
            NextOperations: [],
            NextTokens: [],
            NearestPrefixSessionIds: [],
            ContributingTokens: [],
            Status: RecommendationStatus.ExplicitError,
            StatusDetail: detail
        );
}
