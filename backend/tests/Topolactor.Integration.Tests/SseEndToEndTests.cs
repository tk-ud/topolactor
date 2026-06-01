using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Topolactor.Scheduler;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// SSE E2E tests covering:
///   SseEventBroadcaster fan-out (subscribe, broadcast, unsubscribe)
///   SseEndpoint SSE format output (event: ...\ndata: ...\n\n)
///
/// Pipeline under test:
///   Broadcast(event) → subscriber channel → SseEndpoint.StreamAsync → response body
///
/// Out of scope: DbNotifyListener → PostgreSQL (requires live DB).
/// </summary>
public class SseEventBroadcasterTests
{
    [Fact]
    public void Subscribe_ReturnsIsolatedChannel()
    {
        var broadcaster = new SseEventBroadcaster();

        var ch1 = broadcaster.Subscribe();
        var ch2 = broadcaster.Subscribe();

        Assert.NotSame(ch1, ch2);
        Assert.Equal(2, broadcaster.SubscriberCount);
    }

    [Fact]
    public void Broadcast_DeliversEventToAllSubscribers()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch1 = broadcaster.Subscribe();
        var ch2 = broadcaster.Subscribe();

        broadcaster.Broadcast(new SseEvent("projection", """{"manifest_id":"test"}"""));

        Assert.True(ch1.Reader.TryRead(out var e1));
        Assert.True(ch2.Reader.TryRead(out var e2));
        Assert.Equal("projection", e1.EventType);
        Assert.Equal("projection", e2.EventType);
        Assert.Equal("""{"manifest_id":"test"}""", e1.Data);
    }

    [Fact]
    public void Broadcast_NoSubscribers_DoesNotThrow()
    {
        var broadcaster = new SseEventBroadcaster();
        var ex = Record.Exception(() => broadcaster.Broadcast(new SseEvent("projection", "{}")));
        Assert.Null(ex);
    }

    [Fact]
    public void Unsubscribe_RemovesChannelFromFanOut()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch = broadcaster.Subscribe();
        broadcaster.Unsubscribe(ch);

        broadcaster.Broadcast(new SseEvent("projection", "{}"));

        Assert.Equal(0, broadcaster.SubscriberCount);
        Assert.False(ch.Reader.TryRead(out _));
    }

    [Fact]
    public void Broadcast_FullChannel_DropsOldestWithoutBlocking()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch = broadcaster.Subscribe();

        // Channel capacity is 64; overflow should drop oldest without blocking.
        for (var i = 0; i < 70; i++)
        {
            broadcaster.Broadcast(new SseEvent("projection", $"{{\"i\":{i}}}"));
        }

        // Should still be able to read without hanging.
        Assert.True(ch.Reader.TryRead(out _));
    }
}

/// <summary>
/// SSE streaming format tests using a fake HttpResponse backed by MemoryStream.
/// Verifies that SseEndpoint writes the correct SSE wire format.
/// </summary>
public class SseEndpointStreamFormatTests
{
    [Fact]
    public async Task StreamAsync_WithoutBroadcaster_WritesPingAndNoMoreEvents()
    {
        // Arrange
        var (response, body) = MakeFakeResponse();
        var endpoint = new Topolactor.Endpoint.SseEndpoint(
            NullLogger<Topolactor.Endpoint.SseEndpoint>.Instance,
            broadcaster: null);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        // Act — let the keep-alive loop run until cancelled
        try { await endpoint.StreamAsync(response, cts.Token); }
        catch (OperationCanceledException) { }

        // Assert
        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("event: ping\n", text);
        Assert.Contains("data: connected\n\n", text);
    }

    [Fact]
    public async Task StreamAsync_WithBroadcaster_ReceivesEventAndWritesSseFormat()
    {
        // Arrange
        var broadcaster = new SseEventBroadcaster();
        var (response, body) = MakeFakeResponse();
        var endpoint = new Topolactor.Endpoint.SseEndpoint(
            NullLogger<Topolactor.Endpoint.SseEndpoint>.Instance,
            broadcaster);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));

        // Act: start streaming in background, broadcast one event, then cancel.
        var streamTask = Task.Run(async () =>
        {
            try { await endpoint.StreamAsync(response, cts.Token); }
            catch (OperationCanceledException) { }
        });

        await WaitUntilBodyContains(body, "event: ping\n", TimeSpan.FromMilliseconds(600));
        broadcaster.Broadcast(new SseEvent("projection", """{"table_id":"t1"}"""));
        await WaitUntilBodyContains(body, "event: projection\n", TimeSpan.FromMilliseconds(600));
        await cts.CancelAsync();

        try { await streamTask; } catch (OperationCanceledException) { }

        // Assert SSE wire format
        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("event: projection\n", text);
        Assert.Contains("""data: {"table_id":"t1"}""" + "\n\n", text);
    }

    private static (HttpResponse response, MemoryStream body) MakeFakeResponse()
    {
        var ctx = new DefaultHttpContext();
        var body = new MemoryStream();
        ctx.Response.Body = body;
        return (ctx.Response, body);
    }

    private static async Task WaitUntilBodyContains(
        MemoryStream body,
        string expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var text = Encoding.UTF8.GetString(body.ToArray());
            if (text.Contains(expected, StringComparison.Ordinal))
                return;

            await Task.Delay(5, CancellationToken.None);
        }

        var snapshot = Encoding.UTF8.GetString(body.ToArray());
        throw new Xunit.Sdk.XunitException(
            $"Timed out waiting for SSE body to contain '{expected}'. Current body: '{snapshot}'");
    }
}

/// <summary>
/// DbNotifyListener payload validation tests.
/// Verifies that HandleNotificationPayload validates the payload and enqueues valid
/// db_notify payloads to the RuntimeTimelineScheduler as hook triggers.
///
/// Per SSOT notify_listen_contract.db_listen:
///   trigger_kind: hook
///   backend.feeds: backend_scheduler_queue_as_hook_trigger
///   invariants: listen_event_enters_scheduler_before_projection_runtime
///
/// Invalid payloads (malformed JSON, missing/invalid manifest_id) must NOT be enqueued;
/// they yield explicit LogError (no silent fallback).
///
/// Gap-7: full pg_notify → scheduler → SSE E2E path requires live DB and is not covered here.
/// </summary>
public class DbNotifyListenerPayloadTests
{
    private sealed class TestableDbNotifyListener : DbNotifyListener
    {
        public TestableDbNotifyListener(RuntimeTimelineScheduler scheduler)
            : base(NullLogger<DbNotifyListener>.Instance, "test-double", scheduler) { }

        public void SimulateNotification(string payload) => HandleNotificationPayload(payload);
    }

    private static (TestableDbNotifyListener listener, SseEventBroadcaster broadcaster)
        BuildListenerWithFullChain(out RuntimeTimelineScheduler scheduler)
    {
        var broadcaster = new SseEventBroadcaster();
        var sseRuntime = new SseProjectionRuntime(
            NullLogger<SseProjectionRuntime>.Instance, broadcaster);

        // Minimal ManifestDispatcher: only sse_projection_runtime handler is needed.
        // db_notify path is manifest-id driven; provide a manifest repository stub keyed by payload manifest_id.
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["sse_projection_runtime"] = sseRuntime,
        };
        var topologyRepo = new Topolactor.Repository.TopologyRepository(
            NullLogger<Topolactor.Repository.TopologyRepository>.Instance, "test-double");
        var contextRouteRepo = new Topolactor.Repository.ContextRouteRepository(
            NullLogger<Topolactor.Repository.ContextRouteRepository>.Instance, "test-double");
        var uiTopoRepo = new Topolactor.Repository.UiTopologyRepository(
            NullLogger<Topolactor.Repository.UiTopologyRepository>.Instance, "test-double");
        var topoVectorRuntime = new Topolactor.Runtime.TopologyVectorRuntime(
            NullLogger<Topolactor.Runtime.TopologyVectorRuntime>.Instance, contextRouteRepo);
        var adminRuntime = new Topolactor.Runtime.AdminRuntime(
            NullLogger<Topolactor.Runtime.AdminRuntime>.Instance,
            contextRouteRepo,
            new Topolactor.Runtime.RegistrarValidationService(
                NullLogger<Topolactor.Runtime.RegistrarValidationService>.Instance,
                topologyRepo, topoVectorRuntime),
            new Topolactor.Runtime.PackageGeneratorRuntime(
                NullLogger<Topolactor.Runtime.PackageGeneratorRuntime>.Instance, uiTopoRepo),
            uiTopoRepo);
        var targetOverride = new Topolactor.Runtime.TargetDispatchOverride(
            NullLogger<Topolactor.Runtime.TargetDispatchOverride>.Instance, topologyRepo, adminRuntime);

        var manifestId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology:
                [
                    JsonSerializer.SerializeToElement(new { type = "runtime_mapping", runtime_destination = "topology_transform_runtime" }),
                    JsonSerializer.SerializeToElement(new { type = "db_notify_projection_mapping", runtime_destination = "sse_projection_runtime" })
                ],
                Status: "active"));
        var dispatcher = new Topolactor.Runtime.ManifestDispatcher(
            NullLogger<Topolactor.Runtime.ManifestDispatcher>.Instance,
            handlers,
            new Topolactor.Runtime.OperationVectorResolver(),
            targetOverride,
            manifestRepository: manifestRepo);

        scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);

        return (new TestableDbNotifyListener(scheduler), broadcaster);
    }

    [Fact]
    public async Task HandleNotificationPayload_ValidPayload_EnqueuesAndBroadcastsSseEvent()
    {
        var (listener, broadcaster) = BuildListenerWithFullChain(out var scheduler);
        var ch = broadcaster.Subscribe();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await scheduler.StartAsync(cts.Token);
        try
        {
            listener.SimulateNotification(
                """{"table_id":"t1","table_registry_id":"reg1","manifest_id":"00000000-0000-0000-0000-000000000001"}""");

            await WaitUntilChannelHasItem(ch, TimeSpan.FromSeconds(2));

            Assert.True(ch.Reader.TryRead(out var evt));
            Assert.Equal("projection", evt.EventType);
            Assert.Contains("manifest_id", evt.Data);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleNotificationPayload_MultipleSubscribers_AllReceiveEvent()
    {
        var (listener, broadcaster) = BuildListenerWithFullChain(out var scheduler);
        var ch1 = broadcaster.Subscribe();
        var ch2 = broadcaster.Subscribe();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await scheduler.StartAsync(cts.Token);
        try
        {
            listener.SimulateNotification(
                """{"table_id":"t2","table_registry_id":"reg2","manifest_id":"00000000-0000-0000-0000-000000000001"}""");

            await WaitUntilChannelHasItem(ch1, TimeSpan.FromSeconds(2));

            Assert.True(ch1.Reader.TryRead(out var e1));
            Assert.True(ch2.Reader.TryRead(out var e2));
            Assert.Equal("projection", e1.EventType);
            Assert.Equal("projection", e2.EventType);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleNotificationPayload_MalformedJson_NoBroadcast()
    {
        var (listener, broadcaster) = BuildListenerWithFullChain(out var scheduler);
        var ch = broadcaster.Subscribe();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await scheduler.StartAsync(cts.Token);
        try
        {
            listener.SimulateNotification("not-valid-json{{");

            // Wait briefly; no event should arrive
            await Task.Delay(150, CancellationToken.None);
            Assert.False(ch.Reader.TryRead(out _));
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleNotificationPayload_MissingManifestId_NoBroadcast()
    {
        var (listener, broadcaster) = BuildListenerWithFullChain(out var scheduler);
        var ch = broadcaster.Subscribe();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await scheduler.StartAsync(cts.Token);
        try
        {
            listener.SimulateNotification("""{"table_id":"t3","table_registry_id":"reg3"}""");

            await Task.Delay(150, CancellationToken.None);
            Assert.False(ch.Reader.TryRead(out _));
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleNotificationPayload_InvalidManifestIdFormat_NoBroadcast()
    {
        var (listener, broadcaster) = BuildListenerWithFullChain(out var scheduler);
        var ch = broadcaster.Subscribe();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await scheduler.StartAsync(cts.Token);
        try
        {
            listener.SimulateNotification("""{"table_id":"t4","manifest_id":"not-a-guid"}""");

            await Task.Delay(150, CancellationToken.None);
            Assert.False(ch.Reader.TryRead(out _));
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleNotificationPayload_EmptyPayload_NoBroadcast()
    {
        var (listener, broadcaster) = BuildListenerWithFullChain(out var scheduler);
        var ch = broadcaster.Subscribe();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await scheduler.StartAsync(cts.Token);
        try
        {
            // Empty JSON object has no manifest_id → explicit failure, no broadcast
            listener.SimulateNotification("{}");

            await Task.Delay(150, CancellationToken.None);
            Assert.False(ch.Reader.TryRead(out _));
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitUntilChannelHasItem<T>(
        System.Threading.Channels.Channel<T> channel,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (channel.Reader.TryPeek(out _))
                return;
            await Task.Delay(10, CancellationToken.None);
        }
    }
}

/// <summary>
/// SseProjectionRuntime unit tests.
/// Verifies that ExecuteAsync broadcasts the correct SseEvent for valid db_notify payloads.
/// </summary>
public class SseProjectionRuntimeTests
{
    [Fact]
    public async Task ExecuteAsync_ValidPayload_BroadcastsProjectionEvent()
    {
        var broadcaster = new SseEventBroadcaster();
        var ch = broadcaster.Subscribe();
        var runtime = new SseProjectionRuntime(
            NullLogger<SseProjectionRuntime>.Instance, broadcaster);

        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            table_id = "t1",
            table_registry_id = "reg1",
            manifest_id = "00000000-0000-0000-0000-000000000001"
        });
        var request = new Topolactor.Schema.EndpointRequestDto(
            OperationType: "DbNotifyProjection",
            Target: "db_notify",
            Layer: "projection",
            Action: "broadcast",
            IdOrHubId: null,
            Payload: payload,
            Context: null,
            TriggerKind: "hook",
            Role: null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.True(response.Success);
        Assert.True(ch.Reader.TryRead(out var evt));
        Assert.Equal("projection", evt.EventType);
        Assert.Contains("manifest_id", evt.Data);
    }

    [Fact]
    public async Task ExecuteAsync_NoSubscribers_ReturnsSuccess()
    {
        var broadcaster = new SseEventBroadcaster();
        var runtime = new SseProjectionRuntime(
            NullLogger<SseProjectionRuntime>.Instance, broadcaster);

        var request = new Topolactor.Schema.EndpointRequestDto(
            OperationType: "DbNotifyProjection",
            Target: "db_notify",
            Layer: "projection",
            Action: "broadcast",
            IdOrHubId: null,
            Payload: null,
            Context: null,
            TriggerKind: "hook",
            Role: null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.True(response.Success);
        Assert.Empty(response.Errors);
    }
}

internal sealed class StubManifestRepository : Topolactor.Repository.ManifestRepository
{
    private readonly ManifestRecord? _manifest;

    public StubManifestRepository(ManifestRecord? manifest)
        : base(NullLogger<Topolactor.Repository.ManifestRepository>.Instance) => _manifest = manifest;

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action, CancellationToken ct = default) =>
        Task.FromResult(_manifest);

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult(_manifest?.ManifestId == manifestId ? _manifest : null);

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestDetailRecord?>(null);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId, IReadOnlyList<System.Text.Json.JsonElement> topology, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId, Guid? relationRegistryId, IReadOnlyList<System.Text.Json.JsonElement> topology, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
        Guid manifestId, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
        Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PromotionManifestListItem>>([]);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
        Guid manifestId, JsonElement promotionEntry, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));
}
