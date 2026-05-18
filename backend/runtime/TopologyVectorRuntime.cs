using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Topology Vector Runtime: registry sparse vector validation, hub attention recommendation,
/// topology MLP feature crossing, and feedback weight update.
///
/// Treats registry ID arrays (UUID[]) as sparse vectors and applies multi-hot cosine
/// similarity for validation and recommendation.
///
/// No external embedding or pgvector. Uses existing PostgreSQL UUID[], JSONB, GIN indexes.
///
/// Policy source: function_parameters topology_vector_runtime sub-object.
/// All thresholds, alpha values, and deltas are read from policy — never hardcoded.
/// Policy-missing → ExplicitError. enabled=false → explicit disabled result.
/// </summary>
public class TopologyVectorRuntime
{
    private readonly ILogger<TopologyVectorRuntime> _logger;
    private readonly ContextRouteRepository _repository;

    public TopologyVectorRuntime(
        ILogger<TopologyVectorRuntime> logger,
        ContextRouteRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    // ---------------------------------------------------------------------------
    // Registry Vector Validation
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates a candidate registry ID array against existing registry rows using
    /// multi-hot cosine similarity.
    ///
    /// Returns RegistryVectorValidationResult with class:
    ///   ZeroVector             — queryIds is empty
    ///   DuplicateVector        — cosine >= duplicateThreshold (blocking)
    ///   NearDuplicateVector    — cosine >= nearDuplicateThreshold (blocking or confirm)
    ///   RelatedExistingRegistry — cosine >= relatedThreshold (warning)
    ///   Pass                   — no neighbor above relatedThreshold
    ///
    /// All thresholds come from policy — never hardcoded.
    /// DB unavailability → StatusDetail "REGISTRY_VECTOR_VALIDATION_DB_UNAVAILABLE".
    /// </summary>
    public async Task<RegistryVectorValidationResult> ValidateRegistryVectorAsync(
        string registryTable,
        IReadOnlyList<Guid> queryIds,
        RegistryValidationPolicy policy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(registryTable);
        ArgumentNullException.ThrowIfNull(queryIds);
        ArgumentNullException.ThrowIfNull(policy);

        if (!policy.Enabled)
        {
            _logger.LogDebug("TopologyVectorRuntime.ValidateRegistryVectorAsync: disabled by policy.");
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.Pass,
                IsBlocking: false,
                Neighbors: [],
                StatusDetail: "REGISTRY_VECTOR_VALIDATION_DISABLED"
            );
        }

        if (queryIds.Count == 0)
        {
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.ZeroVector,
                IsBlocking: false,
                Neighbors: [],
                StatusDetail: "ZERO_VECTOR"
            );
        }

        IReadOnlyList<RegistryVectorNeighbor> neighbors;
        try
        {
            neighbors = await _repository.FindRegistryVectorNeighborsAsync(
                registryTable, queryIds, policy.RelatedThreshold, policy.TopK, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "TopologyVectorRuntime.ValidateRegistryVectorAsync: DB error for table={Table}.",
                registryTable);
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.Pass,
                IsBlocking: false,
                Neighbors: [],
                StatusDetail: "REGISTRY_VECTOR_VALIDATION_DB_UNAVAILABLE"
            );
        }

        if (neighbors.Count == 0)
        {
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.Pass,
                IsBlocking: false,
                Neighbors: []
            );
        }

        var top = neighbors[0];
        if (top.CosineScore >= policy.DuplicateThreshold)
        {
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.DuplicateVector,
                IsBlocking: true,
                Neighbors: neighbors
            );
        }

        if (top.CosineScore >= policy.NearDuplicateThreshold)
        {
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.NearDuplicateVector,
                IsBlocking: true,
                Neighbors: neighbors
            );
        }

        if (top.CosineScore >= policy.RelatedThreshold)
        {
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.RelatedExistingRegistry,
                IsBlocking: false,
                Neighbors: neighbors
            );
        }

        return new RegistryVectorValidationResult(
            ValidationClass: RegistryVectorValidationClass.Pass,
            IsBlocking: false,
            Neighbors: neighbors
        );
    }

    // ---------------------------------------------------------------------------
    // Cosine similarity (in-memory, multi-hot)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Computes multi-hot cosine similarity between two ID sets.
    /// cosine = intersection_count / sqrt(cardinality(a) * cardinality(b))
    /// Returns 0.0 when either set is empty (zero norm).
    /// </summary>
    public static float ComputeSparseCosineSimilarity(
        IReadOnlySet<Guid> a,
        IReadOnlySet<Guid> b)
    {
        if (a.Count == 0 || b.Count == 0)
            return 0.0f;

        var intersection = 0;
        foreach (var id in a)
        {
            if (b.Contains(id))
                intersection++;
        }

        return (float)(intersection / Math.Sqrt((double)a.Count * b.Count));
    }

    // ---------------------------------------------------------------------------
    // Transition Key Evidence Extraction
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Extracts transition key evidence from the current hub/relation/operation context.
    /// Identifies which table / relation / state / entity is the transition Key.
    ///
    /// Returns evidence entries to be stored in evidence_json.
    /// Evidence explains "why this candidate?" for hub attention.
    /// </summary>
    public static IReadOnlyList<TransitionKeyEvidence> ExtractTransitionKeyEvidence(
        Guid hubId,
        string? currentOperation,
        IReadOnlyList<Guid> relationIds,
        IReadOnlyList<Guid> stateIds,
        IReadOnlyList<string> tableNames,
        IReadOnlyList<RegistryVectorNeighbor> topNeighbors)
    {
        var evidence = new List<TransitionKeyEvidence>();

        if (!string.IsNullOrWhiteSpace(currentOperation))
        {
            evidence.Add(new TransitionKeyEvidence(
                KeyKind: "operation",
                KeyId: currentOperation,
                KeyLabel: currentOperation,
                ContributionScore: 1.0f,
                Reason: "current_operation"
            ));
        }

        foreach (var rel in relationIds)
        {
            evidence.Add(new TransitionKeyEvidence(
                KeyKind: "relation",
                KeyId: rel.ToString(),
                KeyLabel: rel.ToString(),
                ContributionScore: 0.8f,
                Reason: "active_relation"
            ));
        }

        foreach (var state in stateIds)
        {
            evidence.Add(new TransitionKeyEvidence(
                KeyKind: "state",
                KeyId: state.ToString(),
                KeyLabel: state.ToString(),
                ContributionScore: 0.7f,
                Reason: "active_state"
            ));
        }

        foreach (var table in tableNames)
        {
            evidence.Add(new TransitionKeyEvidence(
                KeyKind: "table",
                KeyId: table,
                KeyLabel: table,
                ContributionScore: 0.6f,
                Reason: "active_table"
            ));
        }

        foreach (var neighbor in topNeighbors.Take(3))
        {
            evidence.Add(new TransitionKeyEvidence(
                KeyKind: "cosine_neighbor",
                KeyId: neighbor.RegistryId.ToString(),
                KeyLabel: neighbor.Name,
                ContributionScore: neighbor.CosineScore,
                Reason: $"cosine_neighbor score={neighbor.CosineScore:F3}"
            ));
        }

        return evidence;
    }

    // ---------------------------------------------------------------------------
    // Topology MLP Feature Crossing
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds Topology MLP feature crossing entries from key evidence.
    ///
    /// Combines up to maxFeatureCrossOrder dimensions of evidence keys.
    /// Example: relation_id × state_id, table × operation, etc.
    ///
    /// max_feature_cross_order comes from TopologyMlpPolicy — never hardcoded.
    ///
    /// Returns feature crossing entries to be stored in mlp_feature_json.
    /// mlp_feature_score = sum of cross scores (normalized to [0,1]).
    /// </summary>
    public static (IReadOnlyList<TopologyMlpFeature> Features, float MlpFeatureScore) BuildTopologyMlpFeatures(
        IReadOnlyList<TransitionKeyEvidence> evidence,
        int maxFeatureCrossOrder)
    {
        if (evidence.Count == 0 || maxFeatureCrossOrder <= 0)
            return ([], 0.0f);

        var features = new List<TopologyMlpFeature>();
        var order = Math.Min(maxFeatureCrossOrder, evidence.Count);

        for (var crossOrder = 2; crossOrder <= order; crossOrder++)
        {
            var combinations = GetCombinations(evidence, crossOrder);
            foreach (var combo in combinations)
            {
                var crossScore = combo.Aggregate(1.0f, (acc, e) => acc * e.ContributionScore);
                features.Add(new TopologyMlpFeature(
                    FeatureKeys: combo.Select(e => $"{e.KeyKind}:{e.KeyId}").ToList(),
                    CrossScore: crossScore,
                    Reason: string.Join(" × ", combo.Select(e => e.KeyKind))
                ));
            }
        }

        var mlpScore = features.Count > 0
            ? Math.Min(1.0f, features.Sum(f => f.CrossScore) / features.Count)
            : 0.0f;

        return (features, mlpScore);
    }

    private static IEnumerable<IReadOnlyList<TransitionKeyEvidence>> GetCombinations(
        IReadOnlyList<TransitionKeyEvidence> source,
        int size)
    {
        var indices = Enumerable.Range(0, size).ToArray();
        var n = source.Count;

        while (true)
        {
            yield return indices.Select(i => source[i]).ToList();

            var i = size - 1;
            while (i >= 0 && indices[i] == i + n - size)
                i--;

            if (i < 0)
                yield break;

            indices[i]++;
            for (var j = i + 1; j < size; j++)
                indices[j] = indices[j - 1] + 1;
        }
    }

    // ---------------------------------------------------------------------------
    // EMA and Cross State
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Computes EMA: ema = alpha * currentValue + (1 - alpha) * previousEma
    /// When previousEma is null, returns currentValue as the initial EMA.
    /// alpha comes from HubAttentionPolicy (ema_fast_alpha or ema_slow_alpha) — never hardcoded.
    /// </summary>
    public static float ComputeEma(float currentValue, float? previousEma, float alpha) =>
        previousEma.HasValue
            ? alpha * currentValue + (1.0f - alpha) * previousEma.Value
            : currentValue;

    /// <summary>
    /// Detects EMA cross state by comparing current trend (fast - slow) with previous trend.
    /// cross_up:   trend crossed from <= 0 to > 0
    /// cross_down: trend crossed from >= 0 to &lt; 0
    /// none:       no cross
    /// </summary>
    public static string ComputeCrossState(float currentFast, float currentSlow, float? previousFast, float? previousSlow)
    {
        if (!previousFast.HasValue || !previousSlow.HasValue)
            return "none";

        var currentTrend = currentFast - currentSlow;
        var previousTrend = previousFast.Value - previousSlow.Value;

        if (previousTrend <= 0 && currentTrend > 0)
            return "up";
        if (previousTrend >= 0 && currentTrend < 0)
            return "down";
        return "none";
    }

    // ---------------------------------------------------------------------------
    // Feedback Weight Update
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds feedback events from selected / ignored / missing candidate ID lists.
    /// Delta values come from FeedbackWeightUpdatePolicy — never hardcoded.
    /// </summary>
    public static IReadOnlyList<HubFeedbackEvent> BuildFeedbackEvents(
        Guid hubId,
        string candidateKind,
        int scopeLimit,
        IReadOnlyList<Guid> selectedIds,
        IReadOnlyList<Guid> ignoredIds,
        IReadOnlyList<Guid> missingCandidateIds,
        FeedbackWeightUpdatePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var events = new List<HubFeedbackEvent>();

        foreach (var id in selectedIds)
        {
            events.Add(new HubFeedbackEvent(
                HubId: hubId,
                CandidateId: id,
                CandidateKind: candidateKind,
                ScopeLimit: scopeLimit,
                FeedbackKind: "selected",
                DeltaApplied: policy.PositiveDelta
            ));
        }

        foreach (var id in ignoredIds)
        {
            events.Add(new HubFeedbackEvent(
                HubId: hubId,
                CandidateId: id,
                CandidateKind: candidateKind,
                ScopeLimit: scopeLimit,
                FeedbackKind: "ignored",
                DeltaApplied: policy.NegativeDelta
            ));
        }

        foreach (var id in missingCandidateIds)
        {
            events.Add(new HubFeedbackEvent(
                HubId: hubId,
                CandidateId: id,
                CandidateKind: candidateKind,
                ScopeLimit: scopeLimit,
                FeedbackKind: "missing_candidate",
                DeltaApplied: policy.MissingCandidateDelta
            ));
        }

        return events;
    }

    // ---------------------------------------------------------------------------
    // JSON serialization helpers
    // ---------------------------------------------------------------------------

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    /// <summary>Serializes transition key evidence list to JSON for evidence_json column.</summary>
    public static string SerializeEvidenceJson(IReadOnlyList<TransitionKeyEvidence> evidence) =>
        JsonSerializer.Serialize(evidence, _jsonOptions);

    /// <summary>Serializes MLP feature list to JSON for mlp_feature_json column.</summary>
    public static string SerializeMlpFeatureJson(IReadOnlyList<TopologyMlpFeature> features) =>
        JsonSerializer.Serialize(features, _jsonOptions);
}
