namespace Topolactor.Schema;

/// <summary>
/// SSOT for all tuning parameters of the context route recommendation engine.
/// Every constant that governs inference, neighbor search, and scoring lives here.
/// No numeric literals are allowed in ContextRouteRecommendationResolver — use this record.
///
/// Loaded at runtime from context_route_config (DB registry) via ContextRouteConfigRepository.
/// ContextRouteConfig.Default is the canonical fallback when the DB returns no rows.
/// </summary>
public record ContextRouteConfig(
    /// <summary>Minimum cosine similarity for a prefix candidate to be included as a neighbor.</summary>
    float MinSimilarity,

    /// <summary>Maximum number of prefix candidates to retrieve from the DB for cosine search.</summary>
    int TopK,

    /// <summary>Minimum neighbor count required before producing recommendations.</summary>
    int MinNeighbors,

    /// <summary>History window in days for prefix vector candidate search.</summary>
    int RecentDays,

    /// <summary>Maximum number of recommendation candidates returned in output.</summary>
    int MaxCandidatesShown,

    /// <summary>Weight of transition stat baseline in operation scoring (sum with NeighborWeight = 1.0).</summary>
    float BaselineWeight,

    /// <summary>Weight of neighbor votes in operation scoring (sum with BaselineWeight = 1.0).</summary>
    float NeighborWeight
)
{
    /// <summary>
    /// Canonical default values sourced from enum-session-cos-route-theory spec.
    /// Used by ContextRouteConfigRepository skeleton and as test baseline.
    /// </summary>
    public static ContextRouteConfig Default => new(
        MinSimilarity: 0.05f,
        TopK: 50,
        MinNeighbors: 10,
        RecentDays: 90,
        MaxCandidatesShown: 5,
        BaselineWeight: 0.5f,
        NeighborWeight: 0.5f
    );
}
