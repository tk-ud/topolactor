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
    TransitionAggregationPolicy? TransitionAggregation = null
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
