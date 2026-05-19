using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// ---------------------------------------------------------------------------
// Stub repository for PackageGenerator tests.
// ---------------------------------------------------------------------------

internal sealed class StubUiTopologyRepository()
    : UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "dummy")
{
    private readonly List<UiComponentBucketRecord> _bucket = [];
    private readonly List<(Guid PackageId, Guid ComponentId)> _maps = [];

    public void AddBucketItem(UiComponentBucketRecord record) => _bucket.Add(record);

    public bool ConstraintViolation { get; set; }
    public bool DbUnavailable { get; set; }

    public override Task<IReadOnlyList<UiComponentBucketRecord>> ListBucketItemsAsync(
        string status = "bucketed", CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UiComponentBucketRecord>>(
            _bucket.Where(b => b.Status == status).ToList());

    public override Task<UiComponentBucketRecord?> LoadBucketItemAsync(
        Guid bucketItemId, CancellationToken ct = default)
        => Task.FromResult(_bucket.FirstOrDefault(b => b.BucketItemId == bucketItemId));

    public override Task<bool> TransitionBucketStatusAsync(
        Guid bucketItemId, string expectedStatus, string newStatus, CancellationToken ct = default)
    {
        var idx = _bucket.FindIndex(b => b.BucketItemId == bucketItemId && b.Status == expectedStatus);
        if (idx < 0) return Task.FromResult(false);
        _bucket[idx] = _bucket[idx] with { Status = newStatus };
        return Task.FromResult(true);
    }

    public override Task<Guid> InsertComponentRegistryAsync(
        string componentKey, string componentKind, string sourcePath, CancellationToken ct = default)
    {
        if (DbUnavailable) throw new InvalidOperationException("DB unavailable (stub)");
        if (ConstraintViolation) throw new Npgsql.PostgresException("duplicate", "ERROR", "ERROR", "23505");
        return Task.FromResult(Guid.NewGuid());
    }

    public override Task<Guid> InsertComponentPackageAsync(
        string packageKey, string packageKind, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());

    public override Task InsertPackageComponentMapAsync(
        Guid packageId, Guid componentId, CancellationToken ct = default)
    {
        _maps.Add((packageId, componentId));
        return Task.CompletedTask;
    }

    public override Task<Guid> InsertLayoutRegistryAsync(
        string layoutKey, string layoutKind, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());

    public override Task<Guid> InsertWiringRegistryAsync(
        string wiringKey, string wiringKind, string targetSurface, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());

    public override Task<Guid> InsertTopologyTensorAsync(
        string routeKey, Guid packageId, Guid layoutId, Guid wiringId, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());

    public string GetBucketStatus(Guid id) =>
        _bucket.First(b => b.BucketItemId == id).Status;
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
    public async Task HandleListBucketItemsAsync_EmptyBucket_ReturnsEmptyList()
    {
        var (endpoint, _) = PackageGeneratorFactory.Create();
        var result = await endpoint.HandleListBucketItemsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleListBucketItemsAsync_WithBucketedItem_ReturnsSingleItem()
    {
        var (endpoint, repo) = PackageGeneratorFactory.Create();
        var id = Guid.NewGuid();
        repo.AddBucketItem(new UiComponentBucketRecord(id, "comp_key", "/path", "island", "bucketed"));

        var result = await endpoint.HandleListBucketItemsAsync("bucketed");

        Assert.Single(result);
        Assert.Equal(id.ToString(), result[0].BucketItemId);
        Assert.Equal("comp_key", result[0].ComponentKey);
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

    // ── Generate: constraint violation ───────────────────────────────────

    [Fact]
    public async Task HandleGenerateAsync_ConstraintViolation_Returns422AndRevertsStatus()
    {
        var (endpoint, repo) = PackageGeneratorFactory.Create();
        var id = Guid.NewGuid();
        repo.AddBucketItem(new UiComponentBucketRecord(id, "comp_key", "/path", "island", "bucketed"));
        repo.ConstraintViolation = true;

        var request = new PackageGenerateRequestDto(id.ToString(), "test:route");
        var (response, statusCode) = await endpoint.HandleGenerateAsync(request);

        Assert.Equal(422, statusCode);
        Assert.False(response.Ok);
        Assert.Equal("CONSTRAINT_VIOLATION", response.ErrorCode);
        // Status reverted to 'bucketed' after failure
        Assert.Equal("bucketed", repo.GetBucketStatus(id));
    }
}
