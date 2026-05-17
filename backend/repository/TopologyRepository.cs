using Microsoft.Extensions.Logging;

namespace Topolactor.Repository;

/// <summary>
/// Repository for loading stored topology data: structure maps, packages, and schemas.
/// All methods are stub implementations that return null.
/// Real implementations will query the database via the provided connection string.
/// </summary>
public class TopologyRepository
{
    private readonly ILogger<TopologyRepository> _logger;
    private readonly string _connectionString;

    public TopologyRepository(ILogger<TopologyRepository> logger, string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Loads a structure map record by attractor key or structure map ID.
    /// Stub: always returns null.
    /// </summary>
    public Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
    {
        _logger.LogDebug("TopologyRepository.LoadStructureMapAsync called with key='{Key}'. Stub returns null.", key);
        return Task.FromResult<StructureMapRecord?>(null);
    }

    /// <summary>
    /// Loads a package record by its ID.
    /// Stub: always returns null.
    /// </summary>
    public Task<PackageRecord?> LoadPackageAsync(Guid packageId, CancellationToken ct = default)
    {
        _logger.LogDebug("TopologyRepository.LoadPackageAsync called with packageId='{PackageId}'. Stub returns null.", packageId);
        return Task.FromResult<PackageRecord?>(null);
    }

    /// <summary>
    /// Loads a schema record by its ID.
    /// Stub: always returns null.
    /// </summary>
    public Task<SchemaRecord?> LoadSchemaAsync(Guid schemaId, CancellationToken ct = default)
    {
        _logger.LogDebug("TopologyRepository.LoadSchemaAsync called with schemaId='{SchemaId}'. Stub returns null.", schemaId);
        return Task.FromResult<SchemaRecord?>(null);
    }
}

/// <summary>
/// Stored structure map data loaded from topology storage.
/// Maps an attractor key to its associated package, schema, and component definitions.
/// </summary>
public record StructureMapRecord(
    string StructureMapId,
    string AttractorKey,
    Guid PackageId,
    Guid SchemaId,
    IReadOnlyList<string> ComponentIds
);

/// <summary>
/// Stored package definition loaded from topology storage.
/// </summary>
public record PackageRecord(
    Guid PackageId,
    string PackageName,
    string? Version,
    string? RawDefinition
);

/// <summary>
/// Stored schema definition loaded from topology storage.
/// </summary>
public record SchemaRecord(
    Guid SchemaId,
    string SchemaName,
    string? Version,
    string? RawDefinition
);
