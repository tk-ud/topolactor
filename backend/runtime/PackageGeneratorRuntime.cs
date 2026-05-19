using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Package Generator Runtime: validates the promotion request and delegates
/// the entire atomic promotion to UiTopologyRepository.PromoteBucketItemAsync.
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
    /// Validates the request and delegates promotion to the repository.
    /// Returns a PackageGenerateResult with all issued IDs on success,
    /// or an explicit error code on any failure.
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

        PackageGenerateResult result;
        try
        {
            result = await _repository.PromoteBucketItemAsync(bucketItemId, routeKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PackageGeneratorRuntime.GenerateAsync: unexpected exception for bucketItemId={Id}.", bucketItemId);
            return new PackageGenerateResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, null,
                "DB_UNAVAILABLE", "Repository unavailable.");
        }

        if (result.Code == PackageGenerateCode.Success)
        {
            _logger.LogInformation(
                "PackageGeneratorRuntime.GenerateAsync: success tensorId={TensorId}, bucketItemId={Id}.",
                result.TensorId, bucketItemId);
        }

        return result;
    }
}
