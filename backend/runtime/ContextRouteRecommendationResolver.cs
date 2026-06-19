using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// COMPATIBILITY FALLBACK — not the canonical dispatch target for context route recommendation.
/// Canonical path: seed/manifest af09 → AbstractFunctionExecutor → recommendation_attention primitive
///   → RecommendationAttentionPrimitiveAdapter → this class (adapter call only).
///
/// Resolves context route recommendations: next operations and next token candidates
/// derived from nearest historical context prefix search + transition statistics.
///
/// Insertion point in the canonical runtime route:
///   ... → component_expand → AbstractFunctionExecutor("context_route.recommendation_resolve")
///           → RecommendationAttentionPrimitiveAdapter → ResolveAsync (this class)
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
    private readonly SystemOperationCiRuntime _systemCiRuntime;

    public ContextRouteRecommendationResolver(
        ILogger<ContextRouteRecommendationResolver> logger,
        ContextRouteRepository contextRouteRepository,
        ContextVectorBuilder vectorBuilder,
        ContextNeighborSearch neighborSearch,
        TopologyRepository topologyRepository,
        SystemOperationCiRuntime systemCiRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextRouteRepository = contextRouteRepository ?? throw new ArgumentNullException(nameof(contextRouteRepository));
        _vectorBuilder = vectorBuilder ?? throw new ArgumentNullException(nameof(vectorBuilder));
        _neighborSearch = neighborSearch ?? throw new ArgumentNullException(nameof(neighborSearch));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _systemCiRuntime = systemCiRuntime ?? throw new ArgumentNullException(nameof(systemCiRuntime));
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

        // Multi-hot event vector: token presence = 1.0, absence = 0.0.
        // token.value from context_token_registry is NOT the vector weight;
        // the topology observation signal is token ID presence (multi-hot).
        var eventVector = _vectorBuilder.BuildMultiHotVector(tokenIds);
        var eventNorm = _vectorBuilder.ComputeL2Norm(eventVector);

        // Read 1: prefix candidates — MUST run before AppendContextEventAsync.
        // The LATERAL JOIN resolves next_operation by finding the event immediately
        // following last_event_id in the same session. Appending first would make the
        // current dispatch appear as that successor, corrupting next_operation.
        var prefixCandidates = await _contextRouteRepository.LoadRecentPrefixVectorsAsync(
            tableName, role, policy.RecentDays, ct);

        // Read 2: nearest prefixes (pure in-memory; skipped when no candidates).
        var neighbors = prefixCandidates.Count > 0
            ? _neighborSearch.FindNearestPrefixes(
                eventVector, eventNorm, prefixCandidates, policy.MinSimilarity, policy.TopK)
            : (IReadOnlyList<ContextNeighborResult>)[];

        // Read 3: transition stats — only loaded when neighbor count already satisfies
        // MinNeighbors, since transition stats are only used in the Ok path.
        var transitionStats = new List<ContextTransitionStat>();
        if (neighbors.Count >= policy.MinNeighbors && !string.IsNullOrWhiteSpace(currentOperation))
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
                _logger.LogError(ex, "ContextRouteRepository transition stats query failed.");
                return ExplicitError("TRANSITION_STATS_QUERY_FAILED");
            }
        }

        // Append current dispatch AFTER all recommendation reads, before any status return.
        // Ordering constraint: LoadRecentPrefixVectorsAsync already ran — LATERAL JOIN is not corrupted.
        // Appends on every path (including InsufficientHistory) so cold-start history can grow
        // and subsequent dispatches can eventually produce Ok recommendations.
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
            _logger.LogError(ex, "ContextRouteRepository.AppendContextEventAsync failed.");
            return ExplicitError("CONTEXT_EVENT_APPEND_FAILED");
        }

        if (TryBuildEnumTransitionEvent(vector, sessionId, out var enumTransition))
        {
            try
            {
                await _contextRouteRepository.AppendContextEnumTransitionAsync(enumTransition, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContextRouteRepository.AppendContextEnumTransitionAsync failed.");
                return ExplicitError("ENUM_TRANSITION_APPEND_FAILED");
            }
        }

        // TopologyVectorRuntime extension: evidence extraction, MLP feature crossing, hub attention update.
        // Runs AFTER AppendContextEventAsync so the event is in context before hub attention is updated.
        // enabled=false → no-op (explicit, not silent). Failure → ExplicitError("TVR_EXTENSION_FAILED").
        if (policy.TopologyVectorRuntime is { Enabled: true } tvPolicy)
        {
            try
            {
                await RunTopologyVectorRuntimeExtensionAsync(
                    tvPolicy, currentOperation, tokenIds, tableName, sessionId, vector.IdOrHubId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ContextRouteRecommendationResolver: TopologyVectorRuntime extension failed.");
                return ExplicitError("TVR_EXTENSION_FAILED");
            }
        }

        if (prefixCandidates.Count == 0)
        {
            _logger.LogDebug("ContextRouteRecommendationResolver: no prefix candidates — InsufficientHistory.");
            return InsufficientHistory("NO_CONTEXT_HISTORY");
        }

        if (neighbors.Count < policy.MinNeighbors)
        {
            _logger.LogDebug(
                "ContextRouteRecommendationResolver: {Count} neighbors < min {Min} — InsufficientHistory.",
                neighbors.Count, policy.MinNeighbors);
            return InsufficientHistory("INSUFFICIENT_CONTEXT_HISTORY");
        }

        IReadOnlyDictionary<string, HubAttentionCurrentRecord> tokenAttentionByCandidate;
        try
        {
            tokenAttentionByCandidate = await LoadTokenAttentionBlendMapAsync(shape, policy, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContextRouteRecommendationResolver: recommendation blend current query failed.");
            return ExplicitError("RECOMMENDATION_BLEND_QUERY_FAILED");
        }

        var nextOperations = ResolveNextOperations(neighbors, transitionStats, policy);
        var nextTokens = ResolveNextTokens(neighbors, policy, tokenAttentionByCandidate);
        var nextEnumItems = await ResolveNextEnumItemsAsync(vector, role, policy, ct);
        var nearestIds = neighbors.Take(policy.MaxCandidatesShown).Select(n => n.SessionId).Distinct().ToList();
        var contributing = GetContributingTokenIds(eventVector);

        return new ContextRouteRecommendationResult(
            NextOperations: nextOperations,
            NextTokens: nextTokens,
            NextEnumItems: nextEnumItems,
            NearestPrefixSessionIds: nearestIds,
            ContributingTokens: contributing,
            Status: RecommendationStatus.Ok,
            StatusDetail: null
        );
    }

    // ---------------------------------------------------------------------------
    // TopologyVectorRuntime extension (side effects only — does not affect result)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Executes the TopologyVectorRuntime extension after the context event is appended.
    /// Extracts transition key evidence, builds MLP features, and updates hub attention current.
    ///
    /// Timing: called after AppendContextEventAsync so the event is in context.
    /// Failure: caller catches and returns ExplicitError("TVR_EXTENSION_FAILED").
    ///
    /// Hub identity: hubId is the caller-supplied IdOrHubId from the request (hub-entity-scoped).
    /// Hub attention is skipped when hubId is null — no hub entity means no hub attention record.
    ///
    /// enabled=false is handled by the caller (this method is not called when disabled).
    /// </summary>
    private async Task RunTopologyVectorRuntimeExtensionAsync(
        TopologyVectorRuntimePolicy tvPolicy,
        string? currentOperation,
        IReadOnlyList<Guid> tokenIds,
        string? tableName,
        Guid sessionId,
        Guid? hubId,
        CancellationToken ct)
    {
        // 1. Extract transition key evidence (static — no DB).
        // relationIds: [] — token IDs are not relation IDs; actual relation IDs are unavailable
        // at this point in the recommendation route. topNeighbors = [] (no registry validation here).
        var evidence = TopologyVectorRuntime.ExtractTransitionKeyEvidence(
            hubId: hubId ?? sessionId,
            currentOperation: currentOperation,
            relationIds: [],
            stateIds: [],
            tableNames: tableName is not null ? [tableName] : [],
            topNeighbors: [],
            policy: tvPolicy.TransitionKeyEvidence);

        // CI gate: evidence integrity (event-driven, in-memory — no DB round-trip).
        // Blocking → throw → caller catches → ExplicitError("TVR_EXTENSION_FAILED").
        // Gap → log warning, recommendation continues.
        var evidenceCiResult = _systemCiRuntime.InspectEvidenceIntegrity(currentOperation, evidence);
        if (evidenceCiResult.OverallStatus == SystemCiStatus.Blocking)
            throw new InvalidOperationException(
                $"SystemCI evidence_integrity blocking: [{string.Join(", ", evidenceCiResult.Findings.Select(f => f.CheckName))}]");
        if (evidenceCiResult.OverallStatus == SystemCiStatus.Gap)
            _logger.LogWarning(
                "SystemCI evidence_integrity gap findings: {Findings}",
                string.Join(", ", evidenceCiResult.Findings.Select(f => f.CheckName)));

        // 2. Build MLP features (static — no DB).
        var (mlpFeatures, mlpScore) = tvPolicy.TopologyMlp is { Enabled: true } mlpPolicy
            ? TopologyVectorRuntime.BuildTopologyMlpFeatures(evidence, mlpPolicy.MaxFeatureCrossOrder)
            : ((IReadOnlyList<TopologyMlpFeature>)[], 0.0f);

        // 3. Hub attention current: load → compute EMA → upsert → recalculate ranks.
        // Runs only when HubAttention policy is present, enabled, hubId is known, and tokenIds is non-empty.
        // hubId is the caller-supplied hub entity; null means no hub entity is associated with this
        // dispatch, so hub attention is skipped — hub attention is hub-entity-scoped, not session-scoped.
        if (tvPolicy.HubAttention is { Enabled: true } hubPolicy && tokenIds.Count > 0 && hubId.HasValue)
        {
            var evidenceJson = TopologyVectorRuntime.SerializeEvidenceJson(evidence);
            var mlpFeatureJson = TopologyVectorRuntime.SerializeMlpFeatureJson(mlpFeatures);

            foreach (var scopeLimit in hubPolicy.ScopeLimits)
            {
                var existingRecords = await _contextRouteRepository.LoadHubAttentionCurrentAsync(
                    hubId.Value, scopeLimit, ct);
                var existingByCandidate = existingRecords.ToDictionary(r => r.CandidateId);

                var candidatesToProcess = tokenIds
                    .Take(hubPolicy.MaxUpdateCandidatesPerEvent)
                    .ToList();

                foreach (var tokenId in candidatesToProcess)
                {
                    existingByCandidate.TryGetValue(tokenId, out var existing);

                    // Multi-hot observation: token presence = 1.0 (not weighted by token.value).
                    // EMA observes token presence frequency, not a manually-authored value.
                    const float currentValue = 1.0f;

                    var newEmaFast = TopologyVectorRuntime.ComputeEma(
                        currentValue, existing?.EmaFast, hubPolicy.EmaFastAlpha);
                    var newEmaSlow = TopologyVectorRuntime.ComputeEma(
                        currentValue, existing?.EmaSlow, hubPolicy.EmaSlowAlpha);
                    var trend = newEmaFast - newEmaSlow;
                    var crossState = TopologyVectorRuntime.ComputeCrossState(
                        newEmaFast, newEmaSlow, existing?.EmaFast, existing?.EmaSlow);

                    var attentionScore = newEmaFast + mlpScore + (existing?.FeedbackAdjustment ?? 0.0f);

                    var record = new HubAttentionCurrentRecord(
                        HubId:                hubId.Value,
                        TargetTable:          "context_token_registry",
                        CandidateKind:        "token",
                        CandidateId:          tokenId,
                        ScopeLimit:           scopeLimit,
                        BaseProbability:      null,
                        CosineSimilarity:     null,
                        StaticRelationWeight: null,
                        StatisticalWeight:    currentValue,
                        MlpFeatureScore:      mlpScore > 0.0f ? mlpScore : null,
                        FeedbackAdjustment:   existing?.FeedbackAdjustment ?? 0.0f,
                        EmaFast:              newEmaFast,
                        EmaSlow:              newEmaSlow,
                        Trend:                trend,
                        CrossState:           crossState,
                        AttentionScore:       attentionScore,
                        Rank:                 null,
                        EvidenceJson:         evidenceJson,
                        MlpFeatureJson:       mlpFeatureJson,
                        UpdatedAt:            DateTimeOffset.UtcNow
                    );

                    // CI gate: hub attention invariant check (event-driven, in-memory — no DB round-trip).
                    // Blocking → throw → caller catches → ExplicitError("TVR_EXTENSION_FAILED").
                    // Gap → log warning, upsert proceeds.
                    var hubCiResult = _systemCiRuntime.InspectHubAttentionAfterUpdate(
                        record, evidenceJson, mlpFeatureJson);
                    if (hubCiResult.OverallStatus == SystemCiStatus.Blocking)
                        throw new InvalidOperationException(
                            $"SystemCI hub_attention blocking: [{string.Join(", ", hubCiResult.Findings.Select(f => f.CheckName))}]");
                    if (hubCiResult.OverallStatus == SystemCiStatus.Gap)
                        _logger.LogWarning(
                            "SystemCI hub_attention gap findings: {Findings}",
                            string.Join(", ", hubCiResult.Findings.Select(f => f.CheckName)));

                    await _contextRouteRepository.UpsertHubAttentionCurrentAsync(record, ct);
                }

                if (candidatesToProcess.Count > 0)
                {
                    await _contextRouteRepository.RecalculateHubAttentionRanksAsync(
                        hubId.Value, scopeLimit, ct);
                }
            }
        }
    }


    private async Task<IReadOnlyDictionary<string, HubAttentionCurrentRecord>> LoadTokenAttentionBlendMapAsync(
        RuntimeWorkingShape shape,
        ContextRoutePolicy policy,
        CancellationToken ct)
    {
        var blend = policy.TopologyVectorRuntime?.RecommendationBlend;
        if (blend is not { Enabled: true })
            return new Dictionary<string, HubAttentionCurrentRecord>();

        var hubId = shape.Vector?.IdOrHubId;
        if (!hubId.HasValue)
            return new Dictionary<string, HubAttentionCurrentRecord>();

        var rows = await _contextRouteRepository.LoadHubAttentionCurrentAsync(hubId.Value, blend.ScopeLimit, ct);
        return rows
            .Where(r => r.CandidateKind == "token")
            .ToDictionary(r => r.CandidateId.ToString(), r => r);
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
                    Evidence: evidence,
                    Lane: RecommendationPressureLanes.UiPressure
                );
            })
            .ToList();
    }

    /// <summary>
    /// Derives state_pressure lane candidates from context_enum_transition_stats.
    /// </summary>
    public async Task<IReadOnlyList<RecommendationCandidate>> ResolveNextEnumItemsAsync(
        OperationVector vector,
        string? role,
        ContextRoutePolicy policy,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentNullException.ThrowIfNull(policy);

        if (!Guid.TryParse(vector.ContextEnumGroupId, out var enumGroupId) ||
            !vector.ContextPrevEnumIndex.HasValue)
            return [];

        try
        {
            var stats = await _contextRouteRepository.GetEnumTransitionStatsAsync(
                enumGroupId,
                vector.ContextPrevEnumIndex.Value,
                role,
                policy.MaxCandidatesShown,
                ct);

            return ResolveNextEnumItemsFromStats(stats, policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContextRouteRepository.GetEnumTransitionStatsAsync failed.");
            throw;
        }
    }

    internal static IReadOnlyList<RecommendationCandidate> ResolveNextEnumItemsFromStats(
        IReadOnlyList<ContextEnumTransitionStat> stats,
        ContextRoutePolicy policy)
    {
        if (stats.Count == 0)
            return [];

        var ranked = stats
            .OrderByDescending(s => s.Prob01)
            .Take(policy.MaxCandidatesShown)
            .ToList();

        var candidates = new List<RecommendationCandidate>();
        if (ranked.Count > 0)
        {
            var top = ranked[0];
            candidates.Add(new RecommendationCandidate(
                Value: top.NextEnumIndex.ToString(),
                Score: top.Prob01,
                Probability: top.Prob01,
                Evidence: [$"output_kind=next_enum_item", $"prob01={top.Prob01:F3}"],
                Lane: RecommendationPressureLanes.StatePressure));
            candidates.Add(new RecommendationCandidate(
                Value: top.NextEnumIndex.ToString(),
                Score: top.Prob01,
                Probability: top.Prob01,
                Evidence: [$"output_kind=likely_status", $"prob01={top.Prob01:F3}"],
                Lane: RecommendationPressureLanes.StatePressure));
        }

        if (ranked.Count > 1)
        {
            var second = ranked[1];
            candidates.Add(new RecommendationCandidate(
                Value: second.NextEnumIndex.ToString(),
                Score: second.Prob01,
                Probability: second.Prob01,
                Evidence: [$"output_kind=state_shift_candidate", $"prob01={second.Prob01:F3}"],
                Lane: RecommendationPressureLanes.StatePressure));
        }

        return candidates;
    }

    /// <summary>
    /// Derives next token candidates from neighbor next_token_ids_hint voting.
    /// </summary>
    public IReadOnlyList<RecommendationCandidate> ResolveNextTokens(
        IReadOnlyList<ContextNeighborResult> neighbors,
        ContextRoutePolicy policy,
        IReadOnlyDictionary<string, HubAttentionCurrentRecord>? hubAttentionByCandidate = null)
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

        var blendPolicy = policy.TopologyVectorRuntime?.RecommendationBlend;

        return votes
            .Select(kv =>
            {
                var key = kv.Key.ToString();
                var blendScore = 0.0f;
                var blendEvidence = new List<string>();
                if (blendPolicy is { Enabled: true } && hubAttentionByCandidate is not null &&
                    hubAttentionByCandidate.TryGetValue(key, out var attention))
                {
                    var attentionTerm = (attention.AttentionScore ?? 0.0f) * blendPolicy.AttentionScoreWeight;
                    var trendTerm = (attention.Trend ?? 0.0f) * blendPolicy.TrendWeight;
                    var statTerm = (attention.StatisticalWeight ?? 0.0f) * blendPolicy.StatisticsWeight;
                    blendScore = attentionTerm + trendTerm + statTerm;
                    blendEvidence.Add($"attention_score={attentionTerm:F3}");
                    blendEvidence.Add($"ema_trend={trendTerm:F3}");
                    blendEvidence.Add($"statistical_weight={statTerm:F3}");
                }

                return new { kv.Key, kv.Value, FinalScore = kv.Value.Score + blendScore, BlendEvidence = blendEvidence };
            })
            .OrderByDescending(x => x.FinalScore)
            .Take(policy.MaxCandidatesShown)
            .Select(x => new RecommendationCandidate(
                Value: x.Key.ToString(),
                Score: x.FinalScore,
                Probability: null,
                Evidence: [$"neighbor_count={x.Value.Count}", $"total_sim={x.Value.Score:F3}", ..x.BlendEvidence],
                Lane: RecommendationPressureLanes.UiPressure
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

        TopologyVectorRuntimePolicy? topologyVectorRuntime = dto.TopologyVectorRuntime is null ? null
            : new TopologyVectorRuntimePolicy(
                Enabled: dto.TopologyVectorRuntime.Enabled,
                RegistryValidation: dto.TopologyVectorRuntime.RegistryValidation is null ? null
                    : new RegistryValidationPolicy(
                        Enabled:                dto.TopologyVectorRuntime.RegistryValidation.Enabled,
                        DuplicateThreshold:     dto.TopologyVectorRuntime.RegistryValidation.DuplicateThreshold,
                        NearDuplicateThreshold: dto.TopologyVectorRuntime.RegistryValidation.NearDuplicateThreshold,
                        RelatedThreshold:       dto.TopologyVectorRuntime.RegistryValidation.RelatedThreshold,
                        TopK:                   dto.TopologyVectorRuntime.RegistryValidation.TopK),
                HubAttention: dto.TopologyVectorRuntime.HubAttention is null ? null
                    : new HubAttentionPolicy(
                        Enabled:                      dto.TopologyVectorRuntime.HubAttention.Enabled,
                        ScopeLimits:                  dto.TopologyVectorRuntime.HubAttention.ScopeLimits,
                        EmaFastAlpha:                 dto.TopologyVectorRuntime.HubAttention.EmaFastAlpha,
                        EmaSlowAlpha:                 dto.TopologyVectorRuntime.HubAttention.EmaSlowAlpha,
                        MaxUpdateCandidatesPerEvent:  dto.TopologyVectorRuntime.HubAttention.MaxUpdateCandidatesPerEvent),
                TransitionKeyEvidence: dto.TopologyVectorRuntime.TransitionKeyEvidence is null ? null
                    : new TransitionKeyEvidencePolicy(
                        Enabled:               dto.TopologyVectorRuntime.TransitionKeyEvidence.Enabled,
                        OperationContribution: dto.TopologyVectorRuntime.TransitionKeyEvidence.OperationContribution,
                        RelationContribution:  dto.TopologyVectorRuntime.TransitionKeyEvidence.RelationContribution,
                        StateContribution:     dto.TopologyVectorRuntime.TransitionKeyEvidence.StateContribution,
                        TableContribution:     dto.TopologyVectorRuntime.TransitionKeyEvidence.TableContribution,
                        NeighborTopK:          dto.TopologyVectorRuntime.TransitionKeyEvidence.NeighborTopK),
                TopologyMlp: dto.TopologyVectorRuntime.TopologyMlp is null ? null
                    : new TopologyMlpPolicy(
                        Enabled:             dto.TopologyVectorRuntime.TopologyMlp.Enabled,
                        MaxFeatureCrossOrder: dto.TopologyVectorRuntime.TopologyMlp.MaxFeatureCrossOrder),
                FeedbackWeightUpdate: dto.TopologyVectorRuntime.FeedbackWeightUpdate is null ? null
                    : new FeedbackWeightUpdatePolicy(
                        Enabled:               dto.TopologyVectorRuntime.FeedbackWeightUpdate.Enabled,
                        PositiveDelta:         dto.TopologyVectorRuntime.FeedbackWeightUpdate.PositiveDelta,
                        NegativeDelta:         dto.TopologyVectorRuntime.FeedbackWeightUpdate.NegativeDelta,
                        MissingCandidateDelta: dto.TopologyVectorRuntime.FeedbackWeightUpdate.MissingCandidateDelta),
                RecommendationBlend: dto.TopologyVectorRuntime.RecommendationBlend is null ? null
                    : new RecommendationBlendPolicy(
                        Enabled: dto.TopologyVectorRuntime.RecommendationBlend.Enabled,
                        ScopeLimit: dto.TopologyVectorRuntime.RecommendationBlend.ScopeLimit,
                        AttentionScoreWeight: dto.TopologyVectorRuntime.RecommendationBlend.AttentionScoreWeight,
                        TrendWeight: dto.TopologyVectorRuntime.RecommendationBlend.TrendWeight,
                        StatisticsWeight: dto.TopologyVectorRuntime.RecommendationBlend.StatisticsWeight));

        return new ContextRoutePolicy(
            MinSimilarity:          dto.MinSimilarity,
            TopK:                   dto.TopK,
            MinNeighbors:           dto.MinNeighbors,
            RecentDays:             dto.RecentDays,
            MaxCandidatesShown:     dto.MaxCandidatesShown,
            BaselineWeight:         dto.BaselineWeight,
            NeighborWeight:         dto.NeighborWeight,
            TransitionAggregation:  transitionAggregation,
            TopologyVectorRuntime:  topologyVectorRuntime
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
        [property: JsonPropertyName("transition_aggregation")] TransitionAggregationDto? TransitionAggregation = null,
        [property: JsonPropertyName("topology_vector_runtime")] TopologyVectorRuntimeDto? TopologyVectorRuntime = null
    );

    private record TopologyVectorRuntimeDto(
        [property: JsonPropertyName("enabled")]                  bool Enabled,
        [property: JsonPropertyName("registry_validation")]      RegistryValidationDto? RegistryValidation = null,
        [property: JsonPropertyName("hub_attention")]            HubAttentionDto? HubAttention = null,
        [property: JsonPropertyName("transition_key_evidence")]  TransitionKeyEvidenceDto? TransitionKeyEvidence = null,
        [property: JsonPropertyName("topology_mlp")]             TopologyMlpDto? TopologyMlp = null,
        [property: JsonPropertyName("feedback_weight_update")]   FeedbackWeightUpdateDto? FeedbackWeightUpdate = null,
        [property: JsonPropertyName("recommendation_blend")]   RecommendationBlendDto? RecommendationBlend = null
    );

    private record TransitionKeyEvidenceDto(
        [property: JsonPropertyName("enabled")]                bool  Enabled,
        [property: JsonPropertyName("operation_contribution")] float OperationContribution,
        [property: JsonPropertyName("relation_contribution")]  float RelationContribution,
        [property: JsonPropertyName("state_contribution")]     float StateContribution,
        [property: JsonPropertyName("table_contribution")]     float TableContribution,
        [property: JsonPropertyName("neighbor_top_k")]         int   NeighborTopK
    );

    private record RegistryValidationDto(
        [property: JsonPropertyName("enabled")]                    bool  Enabled,
        [property: JsonPropertyName("duplicate_threshold")]        float DuplicateThreshold,
        [property: JsonPropertyName("near_duplicate_threshold")]   float NearDuplicateThreshold,
        [property: JsonPropertyName("related_threshold")]          float RelatedThreshold,
        [property: JsonPropertyName("top_k")]                      int   TopK
    );

    private record HubAttentionDto(
        [property: JsonPropertyName("enabled")]                          bool          Enabled,
        [property: JsonPropertyName("scope_limits")]                     int[]         ScopeLimits,
        [property: JsonPropertyName("ema_fast_alpha")]                   float         EmaFastAlpha,
        [property: JsonPropertyName("ema_slow_alpha")]                   float         EmaSlowAlpha,
        [property: JsonPropertyName("max_update_candidates_per_event")]  int           MaxUpdateCandidatesPerEvent
    );

    private record TopologyMlpDto(
        [property: JsonPropertyName("enabled")]                bool Enabled,
        [property: JsonPropertyName("max_feature_cross_order")] int MaxFeatureCrossOrder
    );

    private record RecommendationBlendDto(
        [property: JsonPropertyName("enabled")]                 bool Enabled,
        [property: JsonPropertyName("scope_limit")]             int ScopeLimit,
        [property: JsonPropertyName("attention_score_weight")]  float AttentionScoreWeight,
        [property: JsonPropertyName("trend_weight")]            float TrendWeight,
        [property: JsonPropertyName("statistics_weight")]       float StatisticsWeight
    );

    private record FeedbackWeightUpdateDto(
        [property: JsonPropertyName("enabled")]                  bool  Enabled,
        [property: JsonPropertyName("positive_delta")]           float PositiveDelta,
        [property: JsonPropertyName("negative_delta")]           float NegativeDelta,
        [property: JsonPropertyName("missing_candidate_delta")]  float MissingCandidateDelta
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

    private static bool TryBuildEnumTransitionEvent(
        OperationVector vector,
        Guid sessionId,
        out ContextEnumTransitionEventRecord record)
    {
        record = null!;
        if (!Guid.TryParse(vector.ContextEnumGroupId, out var enumGroupId) ||
            !vector.ContextPrevEnumIndex.HasValue ||
            !vector.ContextNextEnumIndex.HasValue)
            return false;

        record = new ContextEnumTransitionEventRecord(
            EventId: Guid.NewGuid(),
            EnumGroupId: enumGroupId,
            PrevEnumIndex: vector.ContextPrevEnumIndex.Value,
            NextEnumIndex: vector.ContextNextEnumIndex.Value,
            Role: vector.UserRole,
            UserId: vector.ContextUserId,
            SessionId: sessionId,
            CreatedAt: DateTimeOffset.UtcNow);
        return true;
    }

    private static ContextRouteRecommendationResult InsufficientHistory(string detail) =>
        new(
            NextOperations: [],
            NextTokens: [],
            NextEnumItems: [],
            NearestPrefixSessionIds: [],
            ContributingTokens: [],
            Status: RecommendationStatus.InsufficientHistory,
            StatusDetail: detail
        );

    private static ContextRouteRecommendationResult ExplicitError(string detail) =>
        new(
            NextOperations: [],
            NextTokens: [],
            NextEnumItems: [],
            NearestPrefixSessionIds: [],
            ContributingTokens: [],
            Status: RecommendationStatus.ExplicitError,
            StatusDetail: detail
        );
}
