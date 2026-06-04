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

    /// <summary>
    /// Generates (bucketed→packaging) all items, then promotes all into a single package for routeKey.
    /// 1 route = 1 package; package_schema_json stores the bucketItemIds[] vector.
    /// Already-promoted items skip the generate step.
    /// </summary>
    public async Task<PackageGenerateBatchResult> PromotePackageAsync(
        string routeKey,
        IReadOnlyList<Guid> bucketItemIds,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        if (bucketItemIds.Count == 0)
            return new PackageGenerateBatchResult(
                PackageGenerateCode.NotFound, null, null, null, null, [], [],
                "BUCKET_ITEM_IDS_REQUIRED", "At least one bucketItemId is required.");

        _logger.LogDebug(
            "PackageGeneratorRuntime.PromotePackageAsync: routeKey={Route}, count={Count}.",
            routeKey, bucketItemIds.Count);

        // Generate each item (bucketed → packaging); items already in 'packaging' are a no-op.
        foreach (var bucketItemId in bucketItemIds)
        {
            PackageGenerateResult genResult;
            try
            {
                genResult = await _repository.GenerateFromBucketAsync(bucketItemId, routeKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PackageGeneratorRuntime.PromotePackageAsync: generate failed for bucketItemId={Id}.", bucketItemId);
                return new PackageGenerateBatchResult(
                    PackageGenerateCode.DbUnavailable, null, null, null, null, [], [],
                    "DB_UNAVAILABLE", "Repository unavailable during generate step.");
            }

            if (genResult.Code != PackageGenerateCode.Success && genResult.Code != PackageGenerateCode.NotBucketed)
            {
                return new PackageGenerateBatchResult(
                    genResult.Code, null, null, null, null, [], [],
                    genResult.ErrorCode, genResult.Message);
            }
        }

        PackageGenerateBatchResult result;
        try
        {
            result = await _repository.PromotePackageFromBucketItemsAsync(routeKey, bucketItemIds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PackageGeneratorRuntime.PromotePackageAsync: promote failed for routeKey={Route}.", routeKey);
            return new PackageGenerateBatchResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, [], [],
                "DB_UNAVAILABLE", "Repository unavailable during promote step.");
        }

        if (result.Code == PackageGenerateCode.Success)
        {
            _logger.LogInformation(
                "PackageGeneratorRuntime.PromotePackageAsync: success packageId={PkgId}, routeKey={Route}, components={Count}.",
                result.PackageId, routeKey, result.ComponentIds.Count);
        }

        return result;
    }

}
