using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

public class FrontendComponentEventLogLaneTests
{
    [Fact]
    public async Task Continuity_RequiredIdentity_IsPreserved_ToRepositoryAppend()
    {
        var spy = new SpyRepo();
        var sut = new ComponentEventAppendEndpoint(spy);

        var req = new ComponentEventAppendRequestDto([
            new ComponentOperationEventDto(
                ComponentId: "cmp-001",
                PackageId: "pkg-001",
                LayoutId: "layout-001",
                WiringId: "wiring-001",
                EventType: "click",
                Payload: new Dictionary<string, object?> { ["label"] = "save", ["checked"] = true },
                ActorOrSource: "ProjectionView",
                OccurredAt: "2026-05-24T00:00:00.000Z",
                IdempotencyKey: "idem-001")
        ]);

        var res = await sut.HandleAsync(req, CancellationToken.None);

        Assert.True(res.Success);
        Assert.Equal(1, res.Accepted);
        Assert.Single(spy.Appended);
        var appended = spy.Appended[0];
        Assert.Equal("cmp-001", appended.ComponentId);
        Assert.Equal("pkg-001", appended.PackageId);
        Assert.Equal("layout-001", appended.LayoutId);
        Assert.Equal("wiring-001", appended.WiringId);
        Assert.Equal("click", appended.EventType);
        Assert.Contains("\"label\":\"save\"", appended.PayloadJson);
        Assert.Equal("ProjectionView", appended.ActorOrSource);
        Assert.Equal(DateTimeOffset.Parse("2026-05-24T00:00:00.000Z"), appended.OccurredAt);
        Assert.Equal("idem-001", appended.IdempotencyKey);
    }

    [Fact]
    public async Task Continuity_DuplicateIdempotency_IsAccepted()
    {
        var spy = new SpyRepo { DuplicateMode = true };
        var sut = new ComponentEventAppendEndpoint(spy);

        var req = new ComponentEventAppendRequestDto([
            new ComponentOperationEventDto("cmp-001", "pkg-001", "layout-001", "wiring-dup", "submit", new Dictionary<string, object?>(), "OperationPanel", "2026-05-24T00:00:00.000Z", "idem-dup")
        ]);

        var res = await sut.HandleAsync(req, CancellationToken.None);

        Assert.True(res.Success);
        Assert.Equal(1, res.Accepted);
    }

    [Fact]
    public async Task Continuity_AckOnlyOrPayloadDrop_IsRejected_WhenRequiredIdentityMissing()
    {
        var sut = new ComponentEventAppendEndpoint(new SpyRepo());

        var req = new ComponentEventAppendRequestDto([
            new ComponentOperationEventDto(
                ComponentId: "",
                PackageId: "pkg-001",
                LayoutId: "layout-001",
                WiringId: "wiring-001",
                EventType: "click",
                Payload: new Dictionary<string, object?>(),
                ActorOrSource: "ProjectionView",
                OccurredAt: "2026-05-24T00:00:00.000Z",
                IdempotencyKey: "idem-missing")
        ]);

        var res = await sut.HandleAsync(req, CancellationToken.None);
        Assert.False(res.Success);
        Assert.NotNull(res.Errors);
        Assert.Contains(res.Errors!, e => e.Code == "COMPONENT_EVENT_INVALID");
    }

    private sealed class SpyRepo : ContextRouteRepository
    {
        public List<ComponentOperationEventLogRecord> Appended { get; } = [];
        public bool DuplicateMode { get; set; }

        public SpyRepo() : base(NullLogger<ContextRouteRepository>.Instance, "dummy") { }

        public override Task<bool> AppendComponentOperationEventLogAsync(ComponentOperationEventLogRecord ev, CancellationToken ct = default)
        {
            if (DuplicateMode) return Task.FromResult(false);
            Appended.Add(ev);
            return Task.FromResult(true);
        }
    }
}
