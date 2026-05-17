using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Repository for context_route_config — the SSOT registry table for
/// recommendation engine tuning parameters.
///
/// In-memory skeleton: LoadConfigAsync returns MissingPolicy (not a hardcoded Default).
/// Policy-missing is surfaced explicitly to the caller; no silent fallback.
/// Production implementation replaces the stub with real DB reads/writes.
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
    /// Loads all config rows from context_route_config and maps them to ContextRouteConfig.
    /// Returns MissingPolicy when the table is empty; InvalidPolicy when required keys are absent.
    /// Never returns a hardcoded fallback.
    ///
    /// In-memory skeleton: always returns MissingPolicy.
    /// </summary>
    public virtual Task<ConfigLoadResult> LoadConfigAsync(CancellationToken ct = default)
    {
        _logger.LogDebug(
            "ContextRouteConfigRepository.LoadConfigAsync: in-memory skeleton — MissingPolicy.");
        return Task.FromResult<ConfigLoadResult>(new ConfigLoadResult.MissingPolicy());
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
