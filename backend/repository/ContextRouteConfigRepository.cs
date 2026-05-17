using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Repository for context_route_config — the SSOT registry table for
/// recommendation engine tuning parameters.
///
/// In-memory skeleton: LoadConfigAsync returns the seeded config values that
/// match the INSERT rows in db/context_route_config.sql.  This mirrors the
/// pattern used by TopologyRepository, which returns in-memory seed records
/// matching db/seed_empty.sql — no real DB connection is required to run
/// the canonical flow against the skeleton.
///
/// Real DB implementation (TODO): replace the skeleton body with actual
/// SELECT / UPSERT against context_route_config using the _connectionString.
/// When the table is empty → return MissingPolicy; when a required key is
/// absent → return InvalidPolicy; never return a hardcoded fallback.
/// </summary>
public class ContextRouteConfigRepository
{
    // Seed values matching db/context_route_config.sql INSERT rows.
    // Used only by the in-memory skeleton; real DB implementation reads these from the table.
    private static readonly ContextRouteConfig _seedConfig = new(
        MinSimilarity: 0.05f,
        TopK: 50,
        MinNeighbors: 10,
        RecentDays: 90,
        MaxCandidatesShown: 5,
        BaselineWeight: 0.5f,
        NeighborWeight: 0.5f
    );

    private readonly ILogger<ContextRouteConfigRepository> _logger;
    private readonly string _connectionString;

    public ContextRouteConfigRepository(
        ILogger<ContextRouteConfigRepository> logger,
        string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Loads all config rows from context_route_config and maps them to ContextRouteConfig.
    /// Returns MissingPolicy when the table is empty; InvalidPolicy when required keys are absent.
    /// Never returns a hardcoded fallback — the skeleton returns the DB seed values.
    ///
    /// In-memory skeleton: returns Loaded(_seedConfig) matching context_route_config.sql INSERT rows.
    /// Real DB implementation: SELECT all rows, map to ContextRouteConfig, return MissingPolicy if empty.
    /// </summary>
    public virtual Task<ConfigLoadResult> LoadConfigAsync(CancellationToken ct = default)
    {
        _logger.LogDebug(
            "ContextRouteConfigRepository.LoadConfigAsync: in-memory skeleton — returning seed config.");
        return Task.FromResult<ConfigLoadResult>(new ConfigLoadResult.Loaded(_seedConfig));
    }

    /// <summary>
    /// Upserts all fields of the given config into context_route_config.
    ///
    /// In-memory skeleton: no-op.
    /// Real DB implementation: INSERT ... ON CONFLICT (config_key) DO UPDATE for each field.
    /// </summary>
    public virtual Task SaveConfigAsync(ContextRouteConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        _logger.LogDebug(
            "ContextRouteConfigRepository.SaveConfigAsync: in-memory skeleton — no-op.");
        return Task.CompletedTask;
    }
}
