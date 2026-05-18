namespace Topolactor.Schema;

/// <summary>
/// Resolved policy record for the context route recommendation engine.
/// This is NOT an independent config record — it is a resolved view of
/// function_parameters stored in the topology (function_name =
/// 'context_route_recommendation_resolve', parameter_key = 'default_policy').
///
/// Values originate from the topology data store; no production defaults
/// exist in runtime code.  Policy-missing → ExplicitError.
/// </summary>
public record ContextRoutePolicy(
    /// <summary>Minimum cosine similarity for a prefix candidate to be included as a neighbor.</summary>
    float MinSimilarity,

    /// <summary>Maximum number of prefix candidates to retrieve for cosine search.</summary>
    int TopK,

    /// <summary>Minimum neighbor count required before producing recommendations.</summary>
    int MinNeighbors,

    /// <summary>History window in days for prefix vector candidate search. When null, no date constraint applies.</summary>
    int? RecentDays,

    /// <summary>Maximum number of recommendation candidates returned in output.</summary>
    int MaxCandidatesShown,

    /// <summary>Weight of transition stat baseline in operation scoring (sum with NeighborWeight = 1.0).</summary>
    float BaselineWeight,

    /// <summary>Weight of neighbor votes in operation scoring (sum with BaselineWeight = 1.0).</summary>
    float NeighborWeight,

    /// <summary>Optional windowed transition aggregation policy. When null, uses the pre-aggregated context_transition_stats table.</summary>
    TransitionAggregationPolicy? TransitionAggregation = null,

    /// <summary>Optional Topology Vector Runtime policy. When null, topology vector runtime is not active.</summary>
    TopologyVectorRuntimePolicy? TopologyVectorRuntime = null
);

/// <summary>
/// Policy for computing windowed transition stats directly from context_event raw rows.
/// </summary>
/// <param name="AggregationLimit">Count-based window for transition stats (e.g. 10000 most recent events).</param>
/// <param name="PreferRecent">When true, sort by created_at DESC before limiting.</param>
/// <param name="RecentDays">Optional date filter; when null, no date constraint applies.</param>
public record TransitionAggregationPolicy(
    int AggregationLimit,
    bool PreferRecent,
    int? RecentDays
);

/// <summary>
/// Policy for the Topology Vector Runtime extension.
/// Loaded from function_parameters (function_name='context_route_recommendation_resolve',
/// parameter_key='default_policy') as the "topology_vector_runtime" sub-object.
///
/// All numeric values originate from topology data store.
/// No production defaults exist in runtime code.
/// Missing sub-key → ExplicitError("TOPOLOGY_VECTOR_RUNTIME_POLICY_NOT_FOUND").
/// enabled=false → explicit disabled result; not silent fallback.
/// </summary>
public record TopologyVectorRuntimePolicy(
    bool Enabled,
    RegistryValidationPolicy? RegistryValidation,
    HubAttentionPolicy? HubAttention,
    TransitionKeyEvidencePolicy? TransitionKeyEvidence,
    TopologyMlpPolicy? TopologyMlp,
    FeedbackWeightUpdatePolicy? FeedbackWeightUpdate
);

/// <summary>
/// Policy for registry vector validation (duplicate_vector / near_duplicate_vector /
/// related_existing_registry / zero_vector detection).
/// Thresholds are read from policy — never hardcoded in runtime.
/// </summary>
public record RegistryValidationPolicy(
    bool Enabled,
    float DuplicateThreshold,
    float NearDuplicateThreshold,
    float RelatedThreshold,
    int TopK
);

/// <summary>
/// Policy for hub attention recommendation current (EMA, scope limits, update cap).
/// All numeric values from topology data store — never hardcoded.
/// scope_limits must be a subset of {1000, 3000, 10000}.
/// </summary>
public record HubAttentionPolicy(
    bool Enabled,
    IReadOnlyList<int> ScopeLimits,
    float EmaFastAlpha,
    float EmaSlowAlpha,
    int MaxUpdateCandidatesPerEvent
);

/// <summary>
/// Policy for transition key evidence extraction.
/// Contribution scores for each key kind and the neighbor top-k count are read from policy.
/// All values are scoring behavior — never hardcoded in runtime code.
/// </summary>
public record TransitionKeyEvidencePolicy(
    bool Enabled,
    float OperationContribution,
    float RelationContribution,
    float StateContribution,
    float TableContribution,
    int NeighborTopK
);

/// <summary>
/// Policy for Topology MLP feature crossing.
/// max_feature_cross_order: maximum number of Key dimensions combined in a single feature cross.
/// Read from policy — never hardcoded.
/// </summary>
public record TopologyMlpPolicy(
    bool Enabled,
    int MaxFeatureCrossOrder
);

/// <summary>
/// Policy for feedback weight update (selected / ignored / missing_candidate deltas).
/// All delta values read from policy — never hardcoded.
/// </summary>
public record FeedbackWeightUpdatePolicy(
    bool Enabled,
    float PositiveDelta,
    float NegativeDelta,
    float MissingCandidateDelta
);
