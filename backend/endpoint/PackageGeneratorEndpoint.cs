using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

/// <summary>
/// Endpoint for the package generator pipeline.
/// Handles listing ui_component_bucket items and promoting bucket items
/// through the package generator to ui_topology_tensor persistence.
///
/// All methods require a valid JWT (enforced by the HTTP layer in Program.cs).
/// Contains no business logic — delegates to runtime and repository.
/// </summary>
public class PackageGeneratorEndpoint
{
    private readonly ILogger<PackageGeneratorEndpoint> _logger;
    private readonly PackageGeneratorRuntime _packageGeneratorRuntime;
    private readonly UiTopologyRepository _uiTopologyRepository;

    public PackageGeneratorEndpoint(
        ILogger<PackageGeneratorEndpoint> logger,
        PackageGeneratorRuntime packageGeneratorRuntime,
        UiTopologyRepository uiTopologyRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _packageGeneratorRuntime = packageGeneratorRuntime ?? throw new ArgumentNullException(nameof(packageGeneratorRuntime));
        _uiTopologyRepository = uiTopologyRepository ?? throw new ArgumentNullException(nameof(uiTopologyRepository));
    }

    /// <summary>
    /// Lists bucket items with the given status (default 'bucketed').
    /// Returns (items, 200) on success.
    /// Returns (null, 503) when the repository is unavailable.
    /// </summary>
    public async Task<(IReadOnlyList<UiComponentBucketItemDto>? Items, int StatusCode)> HandleListBucketItemsAsync(
        string status = "bucketed",
        CancellationToken ct = default)
    {
        IReadOnlyList<UiComponentBucketRecord> records;
        try
        {
            records = await _uiTopologyRepository.ListBucketItemsAsync(status, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PackageGeneratorEndpoint.HandleListBucketItemsAsync: repository unavailable.");
            return (null, 503);
        }

        var dtos = records
            .Select(r => new UiComponentBucketItemDto(
                r.BucketItemId.ToString(),
                r.ComponentKey,
                r.SourcePath,
                r.ComponentKind,
                r.Status))
            .ToList();
        return (dtos, 200);
    }

    /// <summary>
    /// Promotes a bucket item through the package generator pipeline.
    /// Returns (response, statusCode) where statusCode follows HTTP semantics:
    ///   200 — success
    ///   400 — malformed bucketItemId or missing routeKey
    ///   404 — bucket item not found
    ///   409 — bucket item not in 'bucketed' status
    ///   422 — constraint violation (duplicate registry key)
    ///   503 — DB unavailable or final promotion update failed
    /// </summary>
    public async Task<(PackageGenerateResponseDto Response, int StatusCode)> HandleGenerateAsync(
        PackageGenerateRequestDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request?.BucketItemId))
        {
            return (
                new PackageGenerateResponseDto(false, null, null, null, null, null,
                    "bucketItemId is required.", "BUCKET_ITEM_ID_REQUIRED"),
                400);
        }

        if (!Guid.TryParse(request.BucketItemId, out var bucketItemGuid))
        {
            return (
                new PackageGenerateResponseDto(false, null, null, null, null, null,
                    "bucketItemId must be a valid UUID.", "MALFORMED_BUCKET_ITEM_ID"),
                400);
        }

        if (string.IsNullOrWhiteSpace(request.RouteKey))
        {
            return (
                new PackageGenerateResponseDto(false, null, null, null, null, null,
                    "routeKey is required.", "ROUTE_KEY_REQUIRED"),
                400);
        }

        _logger.LogDebug(
            "PackageGeneratorEndpoint.HandleGenerateAsync: bucketItemId={Id}, routeKey={Route}.",
            bucketItemGuid, request.RouteKey);

        var result = await _packageGeneratorRuntime.GenerateAsync(
            bucketItemGuid, request.RouteKey, ct);

        return result.Code switch
        {
            PackageGenerateCode.Success =>
                (new PackageGenerateResponseDto(
                    true,
                    result.TensorId!.Value.ToString(),
                    result.ComponentId!.Value.ToString(),
                    result.PackageId!.Value.ToString(),
                    result.LayoutId!.Value.ToString(),
                    result.WiringId!.Value.ToString(),
                    "Package generated successfully."),
                200),

            PackageGenerateCode.NotFound =>
                (new PackageGenerateResponseDto(false, null, null, null, null, null,
                    result.Message ?? "Bucket item not found.", result.ErrorCode),
                404),

            PackageGenerateCode.NotBucketed =>
                (new PackageGenerateResponseDto(false, null, null, null, null, null,
                    result.Message ?? "Bucket item is not in 'bucketed' status.", result.ErrorCode),
                409),

            PackageGenerateCode.ConstraintViolation =>
                (new PackageGenerateResponseDto(false, null, null, null, null, null,
                    result.Message ?? "Registry key conflict.", result.ErrorCode),
                422),

            PackageGenerateCode.DbUnavailable or PackageGenerateCode.PromotionFailed =>
                (new PackageGenerateResponseDto(false, null, null, null, null, null,
                    result.Message ?? "Repository unavailable.", result.ErrorCode),
                503),

            _ =>
                (new PackageGenerateResponseDto(false, null, null, null, null, null,
                    "Unexpected error.", "UNEXPECTED_ERROR"),
                500),
        };
    }
}
