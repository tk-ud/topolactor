using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Repository for ui_topology_tables: ui_component_bucket, ui_component_registry,
/// ui_component_package, ui_package_component_map, ui_layout_registry,
/// ui_wiring_registry, ui_topology_tensor.
///
/// In-memory skeleton: all reads return empty results and all writes are no-ops.
/// Production implementation overrides these stubs with real SQL via Npgsql.
/// </summary>
public class UiTopologyRepository
{
    protected readonly ILogger<UiTopologyRepository> _logger;
    protected readonly string _connectionString;

    public UiTopologyRepository(ILogger<UiTopologyRepository> logger, string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Lists bucket items with the given status (default 'bucketed').
    /// In-memory skeleton: returns empty. Override in production.
    /// </summary>
    public virtual Task<IReadOnlyList<UiComponentBucketRecord>> ListBucketItemsAsync(
        string status = "bucketed",
        CancellationToken ct = default)
    {
        _logger.LogDebug("UiTopologyRepository.ListBucketItemsAsync: in-memory skeleton — returning empty.");
        return Task.FromResult<IReadOnlyList<UiComponentBucketRecord>>([]);
    }

    /// <summary>
    /// Loads a single bucket item by PK.
    /// Returns null when not found.
    /// In-memory skeleton: returns null. Override in production.
    /// </summary>
    public virtual Task<UiComponentBucketRecord?> LoadBucketItemAsync(
        Guid bucketItemId,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "UiTopologyRepository.LoadBucketItemAsync: in-memory skeleton — returning null for id={Id}.",
            bucketItemId);
        return Task.FromResult<UiComponentBucketRecord?>(null);
    }

    /// <summary>
    /// Atomically transitions a bucket item status from expectedStatus to newStatus.
    /// Returns true when exactly one row was updated.
    /// Returns false when the item does not exist or is not in expectedStatus.
    /// In-memory skeleton: returns false. Override in production.
    /// </summary>
    public virtual Task<bool> TransitionBucketStatusAsync(
        Guid bucketItemId,
        string expectedStatus,
        string newStatus,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "UiTopologyRepository.TransitionBucketStatusAsync: in-memory skeleton — returning false for id={Id}.",
            bucketItemId);
        return Task.FromResult(false);
    }

    /// <summary>
    /// Inserts a row into ui_component_registry.
    /// Returns the issued component_id on success.
    /// Throws on constraint violation or DB unavailability.
    /// In-memory skeleton: returns a new Guid. Override in production.
    /// </summary>
    public virtual Task<Guid> InsertComponentRegistryAsync(
        string componentKey,
        string componentKind,
        string sourcePath,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "UiTopologyRepository.InsertComponentRegistryAsync: in-memory skeleton for key={Key}.",
            componentKey);
        return Task.FromResult(Guid.NewGuid());
    }

    /// <summary>
    /// Inserts a row into ui_component_package.
    /// Returns the issued package_id on success.
    /// Throws on constraint violation or DB unavailability.
    /// In-memory skeleton: returns a new Guid. Override in production.
    /// </summary>
    public virtual Task<Guid> InsertComponentPackageAsync(
        string packageKey,
        string packageKind,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "UiTopologyRepository.InsertComponentPackageAsync: in-memory skeleton for key={Key}.",
            packageKey);
        return Task.FromResult(Guid.NewGuid());
    }

    /// <summary>
    /// Inserts a row into ui_package_component_map.
    /// In-memory skeleton: no-op. Override in production.
    /// </summary>
    public virtual Task InsertPackageComponentMapAsync(
        Guid packageId,
        Guid componentId,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "UiTopologyRepository.InsertPackageComponentMapAsync: in-memory skeleton.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Inserts a row into ui_layout_registry.
    /// Returns the issued layout_id on success.
    /// Throws on constraint violation or DB unavailability.
    /// In-memory skeleton: returns a new Guid. Override in production.
    /// </summary>
    public virtual Task<Guid> InsertLayoutRegistryAsync(
        string layoutKey,
        string layoutKind,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "UiTopologyRepository.InsertLayoutRegistryAsync: in-memory skeleton for key={Key}.",
            layoutKey);
        return Task.FromResult(Guid.NewGuid());
    }

    /// <summary>
    /// Inserts a row into ui_wiring_registry.
    /// Returns the issued wiring_id on success.
    /// Throws on constraint violation or DB unavailability.
    /// In-memory skeleton: returns a new Guid. Override in production.
    /// </summary>
    public virtual Task<Guid> InsertWiringRegistryAsync(
        string wiringKey,
        string wiringKind,
        string targetSurface,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "UiTopologyRepository.InsertWiringRegistryAsync: in-memory skeleton for key={Key}.",
            wiringKey);
        return Task.FromResult(Guid.NewGuid());
    }

    /// <summary>
    /// Inserts a row into ui_topology_tensor.
    /// Returns the issued tensor_id on success.
    /// Throws on constraint violation or DB unavailability.
    /// In-memory skeleton: returns a new Guid. Override in production.
    /// </summary>
    public virtual Task<Guid> InsertTopologyTensorAsync(
        string routeKey,
        Guid packageId,
        Guid layoutId,
        Guid wiringId,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "UiTopologyRepository.InsertTopologyTensorAsync: in-memory skeleton for route={Route}.",
            routeKey);
        return Task.FromResult(Guid.NewGuid());
    }
}
