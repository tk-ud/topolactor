using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Package Generator Runtime:
/// - GenerateFromBucketAsync stages bucketed -> packaging (no ID issuance)
/// - PromoteBucketItemAsync performs packaging -> promoted + ID issuance/registration
///
/// The repository is the single persistence boundary — it owns the transaction
/// and ensures no partial state can remain on failure.
///
/// Error codes returned to the endpoint:
///   NOT_FOUND              — bucket item does not exist
///   NOT_BUCKETED           — bucket item is not in 'bucketed' status
///   CONSTRAINT_VIOLATION   — duplicate key in a registry table
///   PROMOTION_FAILED       — final promoted status update returned 0 rows
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
    /// Validates the request and delegates staging transition (bucketed -> packaging)
    /// to the repository. No topology IDs are issued in this stage.
    /// </summary>
    public async Task<PackageGenerateResult> GenerateFromBucketAsync(
        Guid bucketItemId,
        string routeKey,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);

        _logger.LogDebug(
            "PackageGeneratorRuntime.GenerateFromBucketAsync: bucketItemId={Id}, routeKey={Route}.",
            bucketItemId, routeKey);

        PackageGenerateResult result;
        try
        {
            result = await _repository.GenerateFromBucketAsync(bucketItemId, routeKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PackageGeneratorRuntime.GenerateFromBucketAsync: unexpected exception for bucketItemId={Id}.", bucketItemId);
            return new PackageGenerateResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, null,
                "DB_UNAVAILABLE", "Repository unavailable.");
        }

        if (result.Code == PackageGenerateCode.Success)
        {
            _logger.LogInformation(
                "PackageGeneratorRuntime.GenerateFromBucketAsync: staged bucket item {Id} to packaging.",
                bucketItemId);
        }

        return result;
    }

    /// <summary>
    /// Executes full DB registration promotion (packaging -> promoted) and issues
    /// componentId/packageId/layoutId/wiringId/tensorId in one transaction.
    /// </summary>
    public async Task<PackageGenerateResult> PromoteBucketItemAsync(
        Guid bucketItemId,
        string routeKey,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);

        _logger.LogDebug(
            "PackageGeneratorRuntime.PromoteBucketItemAsync: bucketItemId={Id}, routeKey={Route}.",
            bucketItemId, routeKey);

        PackageGenerateResult result;
        try
        {
            result = await _repository.PromoteBucketItemAsync(bucketItemId, routeKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PackageGeneratorRuntime.PromoteBucketItemAsync: unexpected exception for bucketItemId={Id}.", bucketItemId);
            return new PackageGenerateResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, null,
                "DB_UNAVAILABLE", "Repository unavailable.");
        }

        if (result.Code == PackageGenerateCode.Success)
        {
            _logger.LogInformation(
                "PackageGeneratorRuntime.PromoteBucketItemAsync: success tensorId={TensorId}, bucketItemId={Id}.",
                result.TensorId, bucketItemId);
        }

        return result;
    }

}
