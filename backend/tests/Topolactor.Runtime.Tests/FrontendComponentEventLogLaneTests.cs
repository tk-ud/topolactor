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
        Assert.Equal("cmp-001", appended.RecordId);
        Assert.Equal("click", appended.Operation);
        Assert.Equal("frontend_component_event", appended.Role);
        Assert.Equal("ui_component", appended.TableName);
    }

    [Fact]
    public async Task Continuity_DuplicateIdempotency_IsAccepted()
    {
        var spy = new SpyRepo { ThrowDuplicateOnFirst = true };
        var sut = new ComponentEventAppendEndpoint(spy);

        var req = new ComponentEventAppendRequestDto([
            new ComponentOperationEventDto("cmp-001", "pkg-001", "layout-001", "submit", new Dictionary<string, object?>(), "OperationPanel", "2026-05-24T00:00:00.000Z", "idem-dup")
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
        public List<ContextEventRecord> Appended { get; } = [];
        public bool ThrowDuplicateOnFirst { get; set; }

        public SpyRepo() : base(NullLogger<ContextRouteRepository>.Instance, "dummy") { }

        public override Task AppendContextEventAsync(ContextEventRecord ev, CancellationToken ct = default)
        {
            if (ThrowDuplicateOnFirst)
            {
                ThrowDuplicateOnFirst = false;
                throw new Exception("23505 duplicate key");
            }
            Appended.Add(ev);
            return Task.CompletedTask;
        }
    }
}
