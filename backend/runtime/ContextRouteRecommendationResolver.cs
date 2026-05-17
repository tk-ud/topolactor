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
/// Status is always explicit:
///   Ok                  — candidates available
///   InsufficientHistory — not enough history to recommend (not an error; expected cold start)
///   ExplicitError       — resolver pipeline failure, including policy-missing
///
/// Tuning parameters are resolved from context_route_config (DB registry) via
/// ContextRouteConfigRepository on every call.  Policy-missing returns ExplicitError —
/// no silent fallback to hardcoded values.
///
/// No silent fallback. No business-specific naming in this layer.
/// </summary>
public class ContextRouteRecommendationResolver
{
    private readonly ILogger<ContextRouteRecommendationResolver> _logger;
    private readonly ContextRouteRepository _contextRouteRepository;
    private readonly ContextRouteConfigRepository _configRepository;
    private readonly ContextVectorBuilder _vectorBuilder;
    private readonly ContextNeighborSearch _neighborSearch;

    public ContextRouteRecommendationResolver(
        ILogger<ContextRouteRecommendationResolver> logger,
        ContextRouteRepository contextRouteRepository,
        ContextVectorBuilder vectorBuilder,
        ContextNeighborSearch neighborSearch,
        ContextRouteConfigRepository configRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextRouteRepository = contextRouteRepository ?? throw new ArgumentNullException(nameof(contextRouteRepository));
        _vectorBuilder = vectorBuilder ?? throw new ArgumentNullException(nameof(vectorBuilder));
        _neighborSearch = neighborSearch ?? throw new ArgumentNullException(nameof(neighborSearch));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    /// <summary>
    /// Resolves context route recommendations for the given working shape.
    /// Returns ExplicitError with CONTEXT_ROUTE_POLICY_NOT_FOUND if config registry is empty.
    /// Returns InsufficientHistory if no session context or no prefix history exists.
    /// </summary>
    public async Task<ContextRouteRecommendationResult> ResolveAsync(
        RuntimeWorkingShape shape,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var configResult = await _configRepository.LoadConfigAsync(ct);
        switch (configResult)
        {
            case ConfigLoadResult.MissingPolicy:
                _logger.LogWarning("ContextRouteRecommendationResolver: config registry is empty — CONTEXT_ROUTE_POLICY_NOT_FOUND.");
                return ExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND");
            case ConfigLoadResult.InvalidPolicy inv:
                _logger.LogWarning("ContextRouteRecommendationResolver: config invalid ({Reason}) — CONTEXT_ROUTE_POLICY_INCOMPLETE.", inv.Reason);
                return ExplicitError($"CONTEXT_ROUTE_POLICY_INCOMPLETE:{inv.Reason}");
        }

        var config = ((ConfigLoadResult.Loaded)configResult).Config;

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
        var currentOperation = vector.Action ?? "";
        var role = vector.UserRole;
        var tableName = vector.Target;

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

        // Load prefix vector candidates
        var prefixCandidates = await _contextRouteRepository.LoadRecentPrefixVectorsAsync(
            tableName, role, config.RecentDays, ct);

        if (prefixCandidates.Count == 0)
        {
            _logger.LogDebug("ContextRouteRecommendationResolver: no prefix candidates — InsufficientHistory.");
            return InsufficientHistory("NO_CONTEXT_HISTORY");
        }

        // Find nearest prefixes
        var neighbors = _neighborSearch.FindNearestPrefixes(
            eventVector, eventNorm, prefixCandidates, config.MinSimilarity, config.TopK);

        if (neighbors.Count < config.MinNeighbors)
        {
            _logger.LogDebug(
                "ContextRouteRecommendationResolver: {Count} neighbors < min {Min} — InsufficientHistory.",
                neighbors.Count, config.MinNeighbors);
            return InsufficientHistory("INSUFFICIENT_CONTEXT_HISTORY");
        }

        // Load transition stats for baseline
        var transitionStats = new List<ContextTransitionStat>();
        if (!string.IsNullOrWhiteSpace(currentOperation))
        {
            try
            {
                var stats = await _contextRouteRepository.GetTransitionStatsAsync(currentOperation, role, ct);
                transitionStats.AddRange(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContextRouteRepository.GetTransitionStatsAsync failed — continuing without baseline.");
            }
        }

        var nextOperations = ResolveNextOperations(neighbors, transitionStats, config);
        var nextTokens = ResolveNextTokens(neighbors, config);
        var nearestIds = neighbors.Take(config.MaxCandidatesShown).Select(n => n.SessionId).Distinct().ToList();
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
        ContextRouteConfig config)
    {
        ArgumentNullException.ThrowIfNull(neighbors);
        ArgumentNullException.ThrowIfNull(transitionStats);
        ArgumentNullException.ThrowIfNull(config);

        var neighborVotes = new Dictionary<string, (float Score, int Count)>();
        foreach (var n in neighbors)
        {
            if (string.IsNullOrWhiteSpace(n.NextOperation))
                continue;
            var (s, c) = neighborVotes.GetValueOrDefault(n.NextOperation);
            neighborVotes[n.NextOperation] = (s + n.Similarity * config.NeighborWeight, c + 1);
        }

        var baselineVotes = new Dictionary<string, float>();
        foreach (var stat in transitionStats)
            baselineVotes[stat.NextOperation] = stat.Prob01 * config.BaselineWeight;

        var merged = new Dictionary<string, float>();
        foreach (var (op, score) in neighborVotes)
            merged[op] = merged.GetValueOrDefault(op) + score.Score;
        foreach (var (op, score) in baselineVotes)
            merged[op] = merged.GetValueOrDefault(op) + score;

        return merged
            .OrderByDescending(kv => kv.Value)
            .Take(config.MaxCandidatesShown)
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
        ContextRouteConfig config)
    {
        ArgumentNullException.ThrowIfNull(neighbors);
        ArgumentNullException.ThrowIfNull(config);

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
            .Take(config.MaxCandidatesShown)
            .Select(kv => new RecommendationCandidate(
                Value: kv.Key.ToString(),
                Score: kv.Value.Score,
                Probability: null,
                Evidence: [$"neighbor_count={kv.Value.Count}", $"total_sim={kv.Value.Score:F3}"]
            ))
            .ToList();
    }

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
