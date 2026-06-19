using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// COMPATIBILITY FALLBACK — ResolveAsync delegates to the 4 decomposed sub-phase methods.
/// Canonical path (af09 manifest):
///   recommendation_candidate_source → recommendation_eligibility →
///   recommendation_score_rank → recommendation_projection
///
/// Each phase is a separate abstract function primitive step with explicit input bindings.
/// Phase boundaries: policy/vector/candidates (source) → neighbor/event-append (eligibility) →
///   blend/rank (score_rank) → lane-enforcement/assembly (projection).
///
/// Policy source: function_parameters (function_name from step_config, parameter_key from step_config).
/// Not read from an independent config table — resolved from stored topology data.
///
/// Status is always explicit:
///   Ok                  — candidates available
///   InsufficientHistory — not enough history (not an error; expected cold start)
///   ExplicitError       — pipeline failure, including policy-missing and policy-invalid
///
/// No silent fallback. No business-specific naming in this layer.
/// </summary>
public class ContextRouteRecommendationResolver
{
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
    /// COMPATIBILITY FALLBACK — calls sub-phase methods in order.
    /// Canonical path: af09 manifest → 4 primitive steps
    ///   (recommendation_candidate_source → recommendation_eligibility →
    ///    recommendation_score_rank → recommendation_projection).
    /// </summary>
    public async Task<ContextRouteRecommendationResult> ResolveAsync(
        RuntimeWorkingShape shape,
        string functionName,
        string defaultParameterKey,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var source = await BuildCandidateSourceAsync(shape, functionName, defaultParameterKey, ct);
        if (source.Status != RecommendationStatus.Ok)
            return BuildProjectionResult(source, null, null);

        var eligibility = await BuildEligibilityAsync(shape, source, ct);
        if (eligibility.Status != RecommendationStatus.Ok)
            return BuildProjectionResult(source, eligibility, null);

        var scoreRank = await BuildScoreRankAsync(shape, source, eligibility, ct);
        return BuildProjectionResult(source, eligibility, scoreRank);
    }

    // ---------------------------------------------------------------------------
    // Primitive-decomposed sub-phase methods (canonical dispatch target)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Phase 1 — candidate source: load policy, check vector/session, load prefix candidates.
    /// Called by recommendation_candidate_source primitive adapter.
    /// Returns InsufficientHistory when no session ID; ExplicitError on policy or vector failure.
    /// Prefix candidates are loaded BEFORE event append (ordering invariant for the LATERAL JOIN).
    /// </summary>
    public async Task<RecommendationCandidateSourceResult> BuildCandidateSourceAsync(
        RuntimeWorkingShape shape,
        string functionName,
        string defaultParameterKey,
        CancellationToken ct = default)
    {
        var (policyKey, policyKeyError) = ResolvePolicyKey(shape.StructureMapStatePolicyJson, defaultParameterKey);
        if (policyKeyError is not null)
        {
            _logger.LogWarning("ContextRouteRecommendationResolver: state_policy resolution failed — {ErrorCode}.", policyKeyError);
            return CandidateSourceExplicitError(policyKeyError);
        }

        var policyJson = await _topologyRepository.LoadFunctionParameterAsync(functionName, policyKey!, ct);
        if (policyJson is null)
        {
            _logger.LogWarning("ContextRouteRecommendationResolver: function_parameter '{Fn}/{Key}' not found.", functionName, policyKey);
            return CandidateSourceExplicitError("CONTEXT_ROUTE_POLICY_NOT_FOUND");
        }

        ContextRoutePolicy policy;
        try { policy = ParsePolicy(policyJson); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ContextRouteRecommendationResolver: policy JSON invalid.");
            return CandidateSourceExplicitError($"CONTEXT_ROUTE_POLICY_INVALID:{ex.Message}");
        }

        var vector = shape.Vector;
        if (vector is null)
            return CandidateSourceExplicitError("MISSING_VECTOR");

        if (string.IsNullOrWhiteSpace(vector.ContextSessionId) ||
            !Guid.TryParse(vector.ContextSessionId, out var sessionId))
        {
            _logger.LogDebug("ContextRouteRecommendationResolver: no ContextSessionId — InsufficientHistory.");
            return new RecommendationCandidateSourceResult(
                Policy: policy,
                EventVector: new Dictionary<Guid, float>(),
                EventNorm: 0f,
                PrefixCandidates: [],
                SessionId: Guid.Empty,
                CurrentOperation: "",
                Role: vector.UserRole,
                TableName: null,
                TokenIds: [],
                Status: RecommendationStatus.InsufficientHistory,
                StatusDetail: "NO_SESSION_ID");
        }

        var tokenIds = ParseTokenIds(vector.ContextTokenIds);
        var currentOperation = vector.AttractorKey ?? "";
        var role = vector.UserRole;
        var tableName = (string?)null;
        var eventVector = _vectorBuilder.BuildMultiHotVector(tokenIds);
        var eventNorm = _vectorBuilder.ComputeL2Norm(eventVector);

        // Read 1: prefix candidates — MUST run before AppendContextEventAsync.
        var prefixCandidates = await _contextRouteRepository.LoadRecentPrefixVectorsAsync(tableName, role, policy.RecentDays, ct);

        return new RecommendationCandidateSourceResult(
            Policy: policy,
            EventVector: eventVector,
            EventNorm: eventNorm,
            PrefixCandidates: prefixCandidates,
            SessionId: sessionId,
            CurrentOperation: currentOperation,
            Role: role,
            TableName: tableName,
            TokenIds: tokenIds,
            Status: RecommendationStatus.Ok,
            StatusDetail: null);
    }

    private static RecommendationCandidateSourceResult CandidateSourceExplicitError(string detail) =>
        new(Policy: null, EventVector: new Dictionary<Guid, float>(), EventNorm: 0f,
            PrefixCandidates: [], SessionId: Guid.Empty, CurrentOperation: "",
            Role: null, TableName: null, TokenIds: [],
            Status: RecommendationStatus.ExplicitError, StatusDetail: detail);

    /// <summary>
    /// Phase 2 — eligibility: neighbor search, transition stats, event append, TVR extension.
    /// Called by recommendation_eligibility primitive adapter.
    /// Event append always runs when source status is Ok (sessionId is available).
    /// Returns InsufficientHistory when candidates/neighbors are insufficient after event append.
    /// Transition stats failure returns ExplicitError BEFORE event append (matching original ordering).
    /// </summary>
    public async Task<RecommendationEligibilityResult> BuildEligibilityAsync(
        RuntimeWorkingShape shape,
        RecommendationCandidateSourceResult source,
        CancellationToken ct = default)
    {
        if (source.Status != RecommendationStatus.Ok)
            return new RecommendationEligibilityResult([], [], source.Status, source.StatusDetail);

        var policy = source.Policy!;
        var vector = shape.Vector!;

        // Read 2: nearest prefixes (pure in-memory).
        var neighbors = source.PrefixCandidates.Count > 0
            ? _neighborSearch.FindNearestPrefixes(
                source.EventVector, source.EventNorm, source.PrefixCandidates,
                policy.MinSimilarity, policy.TopK)
            : (IReadOnlyList<ContextNeighborResult>)[];

        // Read 3: transition stats — only loaded when neighbor count satisfies MinNeighbors.
        var transitionStats = new List<ContextTransitionStat>();
        if (neighbors.Count >= policy.MinNeighbors && !string.IsNullOrWhiteSpace(source.CurrentOperation))
        {
            try
            {
                IReadOnlyList<ContextTransitionStat> stats;
                if (policy.TransitionAggregation is not null)
                    stats = await _contextRouteRepository.GetWindowedTransitionStatsAsync(
                        source.CurrentOperation, source.Role, policy.TransitionAggregation, policy.MaxCandidatesShown, ct);
                else
                    stats = await _contextRouteRepository.GetTransitionStatsAsync(
                        source.CurrentOperation, source.Role, policy.MaxCandidatesShown, ct);
                transitionStats.AddRange(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContextRouteRepository transition stats query failed.");
                return new RecommendationEligibilityResult([], [], RecommendationStatus.ExplicitError, "TRANSITION_STATS_QUERY_FAILED");
            }
        }

        // Event append — MUST run after prefix candidates load (LATERAL JOIN ordering invariant).
        // Runs on every path that has a valid sessionId so cold-start history can grow.
        var contextEvent = new ContextEventRecord(
            EventId: Guid.NewGuid(),
            SessionId: source.SessionId,
            UserId: vector.ContextUserId,
            Role: source.Role,
            TableName: source.TableName,
            RecordId: vector.ContextRecordId,
            Operation: source.CurrentOperation,
            TokenIds: source.TokenIds,
            CreatedAt: DateTimeOffset.UtcNow,
            NextOperationHint: null,
            NextTokenIdsHint: null);

        try
        {
            await _contextRouteRepository.AppendContextEventAsync(contextEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContextRouteRepository.AppendContextEventAsync failed.");
            return new RecommendationEligibilityResult([], [], RecommendationStatus.ExplicitError, "CONTEXT_EVENT_APPEND_FAILED");
        }

        if (TryBuildEnumTransitionEvent(vector, source.SessionId, out var enumTransition))
        {
            try
            {
                await _contextRouteRepository.AppendContextEnumTransitionAsync(enumTransition, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContextRouteRepository.AppendContextEnumTransitionAsync failed.");
                return new RecommendationEligibilityResult([], [], RecommendationStatus.ExplicitError, "ENUM_TRANSITION_APPEND_FAILED");
            }
        }

        // TVR extension — after event append.
        if (policy.TopologyVectorRuntime is { Enabled: true } tvPolicy)
        {
            try
            {
                await RunTopologyVectorRuntimeExtensionAsync(
                    tvPolicy, source.CurrentOperation, source.TokenIds, source.TableName,
                    source.SessionId, vector.IdOrHubId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContextRouteRecommendationResolver: TopologyVectorRuntime extension failed.");
                return new RecommendationEligibilityResult([], [], RecommendationStatus.ExplicitError, "TVR_EXTENSION_FAILED");
            }
        }

        // Eligibility post-check (event is already appended — cold-start history can now grow).
        if (source.PrefixCandidates.Count == 0)
        {
            _logger.LogDebug("ContextRouteRecommendationResolver: no prefix candidates — InsufficientHistory.");
            return new RecommendationEligibilityResult(neighbors, [], RecommendationStatus.InsufficientHistory, "NO_CONTEXT_HISTORY");
        }

        if (neighbors.Count < policy.MinNeighbors)
        {
            _logger.LogDebug("ContextRouteRecommendationResolver: {Count} neighbors < min {Min} — InsufficientHistory.", neighbors.Count, policy.MinNeighbors);
            return new RecommendationEligibilityResult(neighbors, transitionStats, RecommendationStatus.InsufficientHistory, "INSUFFICIENT_CONTEXT_HISTORY");
        }

        return new RecommendationEligibilityResult(neighbors, transitionStats, RecommendationStatus.Ok, null);
    }

    /// <summary>
    /// Phase 3 — score and rank: load blend map, rank operations/tokens/enum items.
    /// Called by recommendation_score_rank primitive adapter.
    /// Skips computation when eligibility status is not Ok.
    /// </summary>
    public async Task<RecommendationScoreRankResult> BuildScoreRankAsync(
        RuntimeWorkingShape shape,
        RecommendationCandidateSourceResult source,
        RecommendationEligibilityResult eligibility,
        CancellationToken ct = default)
    {
        if (eligibility.Status != RecommendationStatus.Ok)
            return new RecommendationScoreRankResult([], [], [], [], [], eligibility.Status, eligibility.StatusDetail);

        var policy = source.Policy!;
        var vector = shape.Vector!;

        IReadOnlyDictionary<string, HubAttentionCurrentRecord> tokenAttentionByCandidate;
        try
        {
            tokenAttentionByCandidate = await LoadTokenAttentionBlendMapAsync(shape, policy, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContextRouteRecommendationResolver: recommendation blend current query failed.");
            return new RecommendationScoreRankResult([], [], [], [], [], RecommendationStatus.ExplicitError, "RECOMMENDATION_BLEND_QUERY_FAILED");
        }

        var nextOperations = ResolveNextOperations(eligibility.Neighbors, eligibility.TransitionStats, policy);
        var nextTokens = ResolveNextTokens(eligibility.Neighbors, policy, tokenAttentionByCandidate);
        var nextEnumItems = await ResolveNextEnumItemsAsync(vector, source.Role, policy, ct);
        var nearestIds = eligibility.Neighbors.Take(policy.MaxCandidatesShown).Select(n => n.SessionId).Distinct().ToList();
        var contributing = GetContributingTokenIds(source.EventVector);

        return new RecommendationScoreRankResult(
            NextOperations: nextOperations,
            NextTokens: nextTokens,
            NextEnumItems: nextEnumItems,
            NearestPrefixSessionIds: nearestIds,
            ContributingTokens: contributing,
            Status: RecommendationStatus.Ok,
            StatusDetail: null);
    }

    /// <summary>
    /// Phase 4 — projection: assemble final result from intermediate phases.
    /// Called by recommendation_projection primitive adapter (which also enforces lane separation).
    /// Propagates error/insufficient status from whichever phase failed first.
    /// </summary>
    public static ContextRouteRecommendationResult BuildProjectionResult(
        RecommendationCandidateSourceResult source,
        RecommendationEligibilityResult? eligibility,
        RecommendationScoreRankResult? scoreRank)
    {
        if (source.Status != RecommendationStatus.Ok)
        {
            return source.Status == RecommendationStatus.InsufficientHistory
                ? InsufficientHistory(source.StatusDetail ?? "UNKNOWN")
                : ExplicitError(source.StatusDetail ?? "UNKNOWN");
        }

        if (eligibility is null || eligibility.Status != RecommendationStatus.Ok)
        {
            return eligibility?.Status == RecommendationStatus.InsufficientHistory
                ? InsufficientHistory(eligibility.StatusDetail ?? "UNKNOWN")
                : ExplicitError(eligibility?.StatusDetail ?? "ELIGIBILITY_FAILED");
        }

        if (scoreRank is null || scoreRank.Status != RecommendationStatus.Ok)
        {
            return scoreRank?.Status == RecommendationStatus.InsufficientHistory
                ? InsufficientHistory(scoreRank.StatusDetail ?? "UNKNOWN")
                : ExplicitError(scoreRank?.StatusDetail ?? "SCORE_RANK_FAILED");
        }

        return new ContextRouteRecommendationResult(
            NextOperations: scoreRank.NextOperations,
            NextTokens: scoreRank.NextTokens,
            NextEnumItems: scoreRank.NextEnumItems,
            NearestPrefixSessionIds: scoreRank.NearestPrefixSessionIds,
            ContributingTokens: scoreRank.ContributingTokens,
            Status: RecommendationStatus.Ok,
            StatusDetail: null);
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
    private static (string? Key, string? ErrorCode) ResolvePolicyKey(string? statePolicyJson, string defaultParameterKey)
    {
        if (string.IsNullOrWhiteSpace(statePolicyJson))
            return (defaultParameterKey, null);

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
                return (defaultParameterKey, null);

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
