namespace Topolactor.Schema;

/// <summary>
/// SSOT schema record for context route recommendation engine tuning parameters.
/// Loaded at runtime from context_route_config (DB registry) via ContextRouteConfigRepository.
/// No hardcoded values exist in runtime code — all parameters originate from the DB registry.
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
);

/// <summary>
/// Result of loading ContextRouteConfig from the context_route_config registry.
/// Policy-missing is an explicit named state — never silently defaulted.
/// </summary>
public abstract record ConfigLoadResult
{
    /// <summary>Config was found and all required keys are present.</summary>
    public sealed record Loaded(ContextRouteConfig Config) : ConfigLoadResult;

    /// <summary>No config rows found in the registry — policy must be seeded first.</summary>
    public sealed record MissingPolicy : ConfigLoadResult;

    /// <summary>Config rows exist but one or more required keys are missing or unparseable.</summary>
    public sealed record InvalidPolicy(string Reason) : ConfigLoadResult;
}
