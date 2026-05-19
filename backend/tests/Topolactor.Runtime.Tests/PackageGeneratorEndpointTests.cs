using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// ---------------------------------------------------------------------------
// Stub repository for PackageGenerator tests.
//
// Overrides ListBucketItemsAsync and PromoteBucketItemAsync.
// All failure modes (constraint violation, DB unavailable, promotion failed)
// are set via properties; the stub does NOT throw from the base NotImplemented
// methods because it overrides them.
// ---------------------------------------------------------------------------

internal sealed class StubUiTopologyRepository()
    : UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "dummy")
{
    private readonly List<UiComponentBucketRecord> _bucket = [];

    public void AddBucketItem(UiComponentBucketRecord record) => _bucket.Add(record);

    public bool SimulateConstraintViolation { get; set; }
    public bool SimulateDbUnavailable { get; set; }
    public bool SimulateFinalTransitionFailure { get; set; }

    public string GetBucketStatus(Guid id) =>
        _bucket.First(b => b.BucketItemId == id).Status;

    public override Task<IReadOnlyList<UiComponentBucketRecord>> ListBucketItemsAsync(
        string status = "bucketed", CancellationToken ct = default)
    {
        if (SimulateDbUnavailable)
            throw new InvalidOperationException("DB unavailable (stub)");
        return Task.FromResult<IReadOnlyList<UiComponentBucketRecord>>(
            _bucket.Where(b => b.Status == status).ToList());
    }

    public override Task<PackageGenerateResult> PromoteBucketItemAsync(
        Guid bucketItemId, string routeKey, CancellationToken ct = default)
    {
        if (SimulateDbUnavailable)
            return Task.FromResult(new PackageGenerateResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, null,
                "DB_UNAVAILABLE", "DB unavailable (stub)"));

        var item = _bucket.FirstOrDefault(b => b.BucketItemId == bucketItemId);
        if (item is null)
            return Task.FromResult(new PackageGenerateResult(
                PackageGenerateCode.NotFound, null, null, null, null, null,
                "NOT_FOUND", $"Bucket item {bucketItemId} not found."));

        if (item.Status != "bucketed")
            return Task.FromResult(new PackageGenerateResult(
                PackageGenerateCode.NotBucketed, null, null, null, null, null,
                "NOT_BUCKETED",
                $"Bucket item {bucketItemId} is in status '{item.Status}', expected 'bucketed'."));

        // Simulate constraint violation — no status change (transaction rolled back in production)
        if (SimulateConstraintViolation)
            return Task.FromResult(new PackageGenerateResult(
                PackageGenerateCode.ConstraintViolation, null, null, null, null, null,
                "CONSTRAINT_VIOLATION",
                "A registry key derived from this bucket item already exists."));

        // Simulate final transition failure — no status change (transaction rolled back)
        if (SimulateFinalTransitionFailure)
            return Task.FromResult(new PackageGenerateResult(
                PackageGenerateCode.PromotionFailed, null, null, null, null, null,
                "PROMOTION_FAILED",
                "Final status update to 'promoted' did not affect exactly one row."));

        // Happy path: update status to 'promoted' and return issued IDs
        var idx = _bucket.FindIndex(b => b.BucketItemId == bucketItemId);
        _bucket[idx] = _bucket[idx] with { Status = "promoted" };

        return Task.FromResult(new PackageGenerateResult(
            PackageGenerateCode.Success,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }
}

internal static class PackageGeneratorFactory
{
    internal static (PackageGeneratorEndpoint Endpoint, StubUiTopologyRepository Repository) Create()
    {
        var repo = new StubUiTopologyRepository();
        var runtime = new PackageGeneratorRuntime(
            NullLogger<PackageGeneratorRuntime>.Instance,
            repo);
        var endpoint = new PackageGeneratorEndpoint(
            NullLogger<PackageGeneratorEndpoint>.Instance,
            runtime,
            repo);
        return (endpoint, repo);
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class PackageGeneratorEndpointTests
{
    // ── List bucket items ──────────────────────────────────────────────────

    [Fact]
    public async Task HandleListBucketItemsAsync_EmptyBucket_Returns200EmptyList()
    {
        var (endpoint, _) = PackageGeneratorFactory.Create();
        var (items, statusCode) = await endpoint.HandleListBucketItemsAsync();
        Assert.Equal(200, statusCode);
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task HandleListBucketItemsAsync_WithBucketedItem_ReturnsSingleItem()
    {
        var (endpoint, repo) = PackageGeneratorFactory.Create();
        var id = Guid.NewGuid();
        repo.AddBucketItem(new UiComponentBucketRecord(id, "comp_key", "/path", "island", "bucketed"));

        var (items, statusCode) = await endpoint.HandleListBucketItemsAsync("bucketed");

        Assert.Equal(200, statusCode);
        Assert.NotNull(items);
        Assert.Single(items);
        Assert.Equal(id.ToString(), items[0].BucketItemId);
        Assert.Equal("comp_key", items[0].ComponentKey);
    }

    [Fact]
    public async Task HandleListBucketItemsAsync_DbUnavailable_Returns503()
    {
        var (endpoint, repo) = PackageGeneratorFactory.Create();
        repo.SimulateDbUnavailable = true;
        var (items, statusCode) = await endpoint.HandleListBucketItemsAsync();
        Assert.Equal(503, statusCode);
        Assert.Null(items);
    }

    // ── Generate: validation errors ───────────────────────────────────────

    [Fact]
    public async Task HandleGenerateAsync_NullBucketItemId_Returns400()
    {
        var (endpoint, _) = PackageGeneratorFactory.Create();
        var request = new PackageGenerateRequestDto(null!, "test:route");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);
        Assert.Equal(400, statusCode);
        Assert.False(response.Ok);
        Assert.Equal("BUCKET_ITEM_ID_REQUIRED", response.ErrorCode);
    }

    [Fact]
    public async Task HandleGenerateAsync_MalformedBucketItemId_Returns400()
    {
        var (endpoint, _) = PackageGeneratorFactory.Create();
        var request = new PackageGenerateRequestDto("not-a-uuid", "test:route");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);
        Assert.Equal(400, statusCode);
        Assert.False(response.Ok);
        Assert.Equal("MALFORMED_BUCKET_ITEM_ID", response.ErrorCode);
    }

    [Fact]
    public async Task HandleGenerateAsync_EmptyRouteKey_Returns400()
    {
        var (endpoint, _) = PackageGeneratorFactory.Create();
        var request = new PackageGenerateRequestDto(Guid.NewGuid().ToString(), "");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);
        Assert.Equal(400, statusCode);
        Assert.False(response.Ok);
        Assert.Equal("ROUTE_KEY_REQUIRED", response.ErrorCode);
    }

    // ── Generate: not found ───────────────────────────────────────────────

    [Fact]
    public async Task HandleGenerateAsync_BucketItemNotFound_Returns404()
    {
        var (endpoint, _) = PackageGeneratorFactory.Create();
        var request = new PackageGenerateRequestDto(Guid.NewGuid().ToString(), "test:route");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);
        Assert.Equal(404, statusCode);
        Assert.False(response.Ok);
        Assert.Equal("NOT_FOUND", response.ErrorCode);
    }

    // ── Generate: not bucketed ────────────────────────────────────────────

    [Fact]
    public async Task HandleGenerateAsync_AlreadyPromoted_Returns409()
    {
        var (endpoint, repo) = PackageGeneratorFactory.Create();
        var id = Guid.NewGuid();
        repo.AddBucketItem(new UiComponentBucketRecord(id, "comp_key", "/path", "island", "promoted"));

        var request = new PackageGenerateRequestDto(id.ToString(), "test:route");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);
        Assert.Equal(409, statusCode);
        Assert.False(response.Ok);
        Assert.Equal("NOT_BUCKETED", response.ErrorCode);
    }

    // ── Generate: success ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleGenerateAsync_ValidBucketItem_Returns200WithAllIds()
    {
        var (endpoint, repo) = PackageGeneratorFactory.Create();
        var id = Guid.NewGuid();
        repo.AddBucketItem(new UiComponentBucketRecord(id, "comp_key", "/path/comp.tsx", "island", "bucketed"));

        var request = new PackageGenerateRequestDto(id.ToString(), "default:entity:search");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);

        Assert.Equal(200, statusCode);
        Assert.True(response.Ok);
        Assert.NotNull(response.TensorId);
        Assert.NotNull(response.ComponentId);
        Assert.NotNull(response.PackageId);
        Assert.NotNull(response.LayoutId);
        Assert.NotNull(response.WiringId);
        Assert.Equal("promoted", repo.GetBucketStatus(id));
    }

    // ── Generate: constraint violation — no partial INSERT ────────────────

    [Fact]
    public async Task HandleGenerateAsync_ConstraintViolation_Returns422_AndBucketStatusUnchanged()
    {
        var (endpoint, repo) = PackageGeneratorFactory.Create();
        var id = Guid.NewGuid();
        repo.AddBucketItem(new UiComponentBucketRecord(id, "comp_key", "/path", "island", "bucketed"));
        repo.SimulateConstraintViolation = true;

        var request = new PackageGenerateRequestDto(id.ToString(), "test:route");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);

        Assert.Equal(422, statusCode);
        Assert.False(response.Ok);
        Assert.Equal("CONSTRAINT_VIOLATION", response.ErrorCode);
        // Transaction rolled back: bucket remains 'bucketed' (no partial INSERT)
        Assert.Equal("bucketed", repo.GetBucketStatus(id));
    }

    // ── Generate: DB unavailable ──────────────────────────────────────────

    [Fact]
    public async Task HandleGenerateAsync_DbUnavailable_Returns503()
    {
        var (endpoint, repo) = PackageGeneratorFactory.Create();
        var id = Guid.NewGuid();
        repo.AddBucketItem(new UiComponentBucketRecord(id, "comp_key", "/path", "island", "bucketed"));
        repo.SimulateDbUnavailable = true;

        var request = new PackageGenerateRequestDto(id.ToString(), "test:route");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);

        Assert.Equal(503, statusCode);
        Assert.False(response.Ok);
        Assert.Equal("DB_UNAVAILABLE", response.ErrorCode);
    }

    // ── Generate: final transition failure → success must NOT be returned ─

    [Fact]
    public async Task HandleGenerateAsync_FinalTransitionFailure_Returns503_NotSuccess()
    {
        var (endpoint, repo) = PackageGeneratorFactory.Create();
        var id = Guid.NewGuid();
        repo.AddBucketItem(new UiComponentBucketRecord(id, "comp_key", "/path", "island", "bucketed"));
        repo.SimulateFinalTransitionFailure = true;

        var request = new PackageGenerateRequestDto(id.ToString(), "test:route");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);

        Assert.Equal(503, statusCode);
        Assert.False(response.Ok);
        Assert.Equal("PROMOTION_FAILED", response.ErrorCode);
        // Transaction rolled back: bucket remains 'bucketed'
        Assert.Equal("bucketed", repo.GetBucketStatus(id));
    }
}
