using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Package Generator Runtime: promotes a ui_component_bucket item into
/// ui_topology_tensor by issuing componentId, packageId, layoutId, wiringId
/// and persisting to the corresponding registry tables.
///
/// Flow (mirrors db/ui_topology_tables.sql SSOT comment):
///   ui_component_bucket (status='bucketed')
///   -> status='packaging' (atomic transition)
///   -> INSERT ui_component_registry  (componentId issued)
///   -> INSERT ui_component_package   (packageId issued)
///   -> INSERT ui_package_component_map
///   -> INSERT ui_layout_registry     (layoutId issued)
///   -> INSERT ui_wiring_registry     (wiringId issued)
///   -> INSERT ui_topology_tensor     (tensorId issued)
///   -> status='promoted'
///
/// Key derivation:
///   component_key = bucket.component_key (verbatim)
///   package_key   = "{routeKey}:{bucket.component_key}:pkg"
///   layout_key    = "{routeKey}:{bucket.component_key}:layout"
///   wiring_key    = "{routeKey}:{bucket.component_key}:wiring"
///
/// Error codes:
///   NOT_FOUND              — bucket item does not exist
///   NOT_BUCKETED           — bucket item is not in 'bucketed' status
///   CONSTRAINT_VIOLATION   — duplicate key in a registry table
///   DB_UNAVAILABLE         — repository or DB connection failed
/// </summary>
public class PackageGeneratorRuntime
{
    private readonly ILogger<PackageGeneratorRuntime> _logger;
    private readonly UiTopologyRepository _repository;

    public PackageGeneratorRuntime(
        ILogger<PackageGeneratorRuntime> logger,
        UiTopologyRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Generates a ui_topology_tensor entry from the given bucket item.
    ///
    /// Returns PackageGenerateCode.NotFound when the item does not exist.
    /// Returns PackageGenerateCode.NotBucketed when the item is not in 'bucketed' status.
    /// Returns PackageGenerateCode.ConstraintViolation when a registry unique key conflicts.
    /// Returns PackageGenerateCode.DbUnavailable when the DB is unreachable.
    /// Returns PackageGenerateCode.Success with all issued IDs on success.
    /// </summary>
    public async Task<PackageGenerateResult> GenerateAsync(
        Guid bucketItemId,
        string routeKey,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);

        _logger.LogDebug(
            "PackageGeneratorRuntime.GenerateAsync: bucketItemId={Id}, routeKey={Route}.",
            bucketItemId, routeKey);

        UiComponentBucketRecord? item;
        try
        {
            item = await _repository.LoadBucketItemAsync(bucketItemId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PackageGeneratorRuntime: DB unavailable loading bucket item {Id}.", bucketItemId);
            return new PackageGenerateResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, null,
                "DB_UNAVAILABLE", "Repository unavailable.");
        }

        if (item is null)
        {
            return new PackageGenerateResult(
                PackageGenerateCode.NotFound, null, null, null, null, null,
                "NOT_FOUND", $"Bucket item {bucketItemId} not found.");
        }

        if (item.Status != "bucketed")
        {
            return new PackageGenerateResult(
                PackageGenerateCode.NotBucketed, null, null, null, null, null,
                "NOT_BUCKETED",
                $"Bucket item {bucketItemId} is in status '{item.Status}', expected 'bucketed'.");
        }

        // Atomically transition to 'packaging' — concurrent requests get NOT_BUCKETED
        bool transitioned;
        try
        {
            transitioned = await _repository.TransitionBucketStatusAsync(
                bucketItemId, "bucketed", "packaging", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PackageGeneratorRuntime: DB unavailable transitioning bucket item {Id}.", bucketItemId);
            return new PackageGenerateResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, null,
                "DB_UNAVAILABLE", "Repository unavailable during status transition.");
        }

        if (!transitioned)
        {
            return new PackageGenerateResult(
                PackageGenerateCode.NotBucketed, null, null, null, null, null,
                "NOT_BUCKETED",
                $"Bucket item {bucketItemId} could not be transitioned to 'packaging'.");
        }

        // Issue IDs and persist
        try
        {
            var componentId = await _repository.InsertComponentRegistryAsync(
                item.ComponentKey,
                item.ComponentKind,
                item.SourcePath,
                ct);

            var packageKey = $"{routeKey}:{item.ComponentKey}:pkg";
            var packageId = await _repository.InsertComponentPackageAsync(
                packageKey,
                item.ComponentKind,
                ct);

            await _repository.InsertPackageComponentMapAsync(packageId, componentId, ct);

            var layoutKey = $"{routeKey}:{item.ComponentKey}:layout";
            var layoutId = await _repository.InsertLayoutRegistryAsync(
                layoutKey,
                item.ComponentKind,
                ct);

            var wiringKey = $"{routeKey}:{item.ComponentKey}:wiring";
            var wiringId = await _repository.InsertWiringRegistryAsync(
                wiringKey,
                item.ComponentKind,
                "route",
                ct);

            var tensorId = await _repository.InsertTopologyTensorAsync(
                routeKey, packageId, layoutId, wiringId, ct);

            await _repository.TransitionBucketStatusAsync(
                bucketItemId, "packaging", "promoted", ct);

            _logger.LogInformation(
                "PackageGeneratorRuntime.GenerateAsync: success tensorId={TensorId}, bucketItemId={Id}.",
                tensorId, bucketItemId);

            return new PackageGenerateResult(
                PackageGenerateCode.Success,
                tensorId, componentId, packageId, layoutId, wiringId);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            _logger.LogWarning(ex,
                "PackageGeneratorRuntime: constraint violation for bucket item {Id}.", bucketItemId);
            // Revert packaging status on constraint violation
            try
            {
                await _repository.TransitionBucketStatusAsync(
                    bucketItemId, "packaging", "bucketed", ct);
            }
            catch (Exception revertEx)
            {
                _logger.LogError(revertEx,
                    "PackageGeneratorRuntime: failed to revert bucket status for item {Id}.", bucketItemId);
            }

            return new PackageGenerateResult(
                PackageGenerateCode.ConstraintViolation, null, null, null, null, null,
                "CONSTRAINT_VIOLATION", "A registry key derived from this bucket item already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PackageGeneratorRuntime: DB error for bucket item {Id}.", bucketItemId);
            // Revert packaging status on unexpected error
            try
            {
                await _repository.TransitionBucketStatusAsync(
                    bucketItemId, "packaging", "bucketed", ct);
            }
            catch (Exception revertEx)
            {
                _logger.LogError(revertEx,
                    "PackageGeneratorRuntime: failed to revert bucket status for item {Id}.", bucketItemId);
            }

            return new PackageGenerateResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, null,
                "DB_UNAVAILABLE", "Repository unavailable during package generation.");
        }
    }
}
