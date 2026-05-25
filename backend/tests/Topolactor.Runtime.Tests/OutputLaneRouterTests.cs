using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class OutputLaneRouterTests
{
    [Fact]
    public async Task RouteAsync_FailedResponse_SkipsDbNotifyAndAttractorSignal()
    {
        var repo = new SpyDbNotifyRepository();
        var sut = new OutputLaneRouter(NullLogger<OutputLaneRouter>.Instance, repo);
        var vector = new OperationVector("registry_table", "runtime", "upsert", "attractor-alpha", "admin", null, null);
        var response = new EndpointResponseDto(false, null, []);

        await sut.RouteAsync(vector, response, Guid.NewGuid());

        Assert.Equal(0, repo.CallCount);
        Assert.False(sut.RebuildSignalChannel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task RouteAsync_SuccessWithoutDbNotifyRepository_EnqueuesAttractorSignalOnly()
    {
        var sut = new OutputLaneRouter(NullLogger<OutputLaneRouter>.Instance, dbNotifyRepository: null);
        var manifestId = Guid.NewGuid();
        var vector = new OperationVector("registry_table", "runtime", "upsert", "attractor-alpha", "admin", null, null);
        var response = new EndpointResponseDto(true, null, []);

        await sut.RouteAsync(vector, response, manifestId);

        Assert.True(sut.RebuildSignalChannel.Reader.TryRead(out var signal));
        Assert.NotNull(signal);
        Assert.Equal("attractor-alpha", signal!.AttractorKey);
        Assert.Equal(manifestId, signal.ManifestId);
    }

    [Fact]
    public async Task RouteAsync_SuccessWithDbNotifyRepository_CallsDbNotifyAndEnqueuesAttractorSignal()
    {
        var repo = new SpyDbNotifyRepository();
        var sut = new OutputLaneRouter(NullLogger<OutputLaneRouter>.Instance, repo);
        var manifestId = Guid.NewGuid();
        var vector = new OperationVector("table-registry-id", "runtime", "upsert", "attractor-alpha", "admin", null, null);
        var response = new EndpointResponseDto(true, null, []);

        await sut.RouteAsync(vector, response, manifestId);

        Assert.Equal(1, repo.CallCount);
        Assert.Equal("attractor-alpha", repo.LastTableId);
        Assert.Equal("table-registry-id", repo.LastTableRegistryId);
        Assert.Equal(manifestId, repo.LastManifestId);

        Assert.True(sut.RebuildSignalChannel.Reader.TryRead(out var signal));
        Assert.NotNull(signal);
        Assert.Equal("attractor-alpha", signal!.AttractorKey);
        Assert.Equal(manifestId, signal.ManifestId);
    }

    [Fact]
    public async Task RouteAsync_DbNotifyFailure_IsExplicitAndStopsRouting()
    {
        var sut = new OutputLaneRouter(
            NullLogger<OutputLaneRouter>.Instance,
            new ThrowingDbNotifyRepository(new InvalidOperationException("db_notify failed")));
        var vector = new OperationVector("table-registry-id", "runtime", "upsert", "attractor-alpha", "admin", null, null);
        var response = new EndpointResponseDto(true, null, []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RouteAsync(vector, response, Guid.NewGuid()));
        Assert.False(sut.RebuildSignalChannel.Reader.TryRead(out _));
    }

    private sealed class SpyDbNotifyRepository() : DbNotifyRepository(NullLogger<DbNotifyRepository>.Instance)
    {
        public int CallCount { get; private set; }
        public string? LastTableId { get; private set; }
        public string? LastTableRegistryId { get; private set; }
        public Guid? LastManifestId { get; private set; }

        public override Task NotifyAsync(string? tableId, string? tableRegistryId, Guid? manifestId, CancellationToken ct = default)
        {
            _ = ct;
            CallCount++;
            LastTableId = tableId;
            LastTableRegistryId = tableRegistryId;
            LastManifestId = manifestId;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDbNotifyRepository(Exception ex) : DbNotifyRepository(NullLogger<DbNotifyRepository>.Instance)
    {
        public override Task NotifyAsync(string? tableId, string? tableRegistryId, Guid? manifestId, CancellationToken ct = default)
            => Task.FromException(ex);
    }
}
