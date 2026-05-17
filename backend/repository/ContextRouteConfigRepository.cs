using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Repository for context_route_config — the SSOT registry table for
/// recommendation engine tuning parameters.
///
/// In-memory skeleton: LoadConfigAsync returns ContextRouteConfig.Default
/// and SaveConfigAsync is a no-op.  Production implementation replaces these
/// stubs with real DB reads/writes against context_route_config.sql.
/// </summary>
public class ContextRouteConfigRepository
{
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
    /// Loads all config rows from context_route_config and maps them to
    /// ContextRouteConfig.  Returns ContextRouteConfig.Default when the table
    /// is empty or the DB is unavailable.
    ///
    /// In-memory skeleton: always returns Default.
    /// </summary>
    public virtual Task<ContextRouteConfig> LoadConfigAsync(CancellationToken ct = default)
    {
        _logger.LogDebug(
            "ContextRouteConfigRepository.LoadConfigAsync: in-memory skeleton — returning Default.");
        return Task.FromResult(ContextRouteConfig.Default);
    }

    /// <summary>
    /// Upserts all fields of the given config into context_route_config.
    ///
    /// In-memory skeleton: no-op.
    /// </summary>
    public virtual Task SaveConfigAsync(ContextRouteConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        _logger.LogDebug(
            "ContextRouteConfigRepository.SaveConfigAsync: in-memory skeleton — no-op.");
        return Task.CompletedTask;
    }
}
