using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// CI lane: RuntimeTimelineScheduler → ManifestDispatcher → RuntimeExecutor canonical route audit.
//
// SSOT contract (docs/design/runtime-orchestration-ssot.yaml, system-roadmap.yaml):
//   - RuntimeTimelineScheduler always routes through ManifestDispatcher (canonical route boundary).
//   - ManifestDispatcher resolves runtime_destination from manifest handler registry.
//   - Unknown runtime_destination → RUNTIME_DESTINATION_UNKNOWN (no implicit fallthrough).
//   - Missing manifest → MANIFEST_NOT_FOUND (no silent fallback to any default runtime).
//   - Queue overflow → SCHEDULER_QUEUE_FULL (explicit signal, not silent drop).
//   - Route missing propagates through full chain as canonical fallback jump events.
//   - Canonical route preserves topology identity (StructureMapId/PackageId/SchemaId).
public class SsotWiringAuditSchedulerRuntimeTests
{
    // ─── Canonical route: full stack ID continuity ────────────────────────────

    [Fact]
    public async Task SchedulerRuntime_CanonicalRoute_DefaultEntitySearch_PreservesTopologyIds()
    {
        // Canonical route: scheduler → ManifestDispatcher → RuntimeExecutor.
        // All three topology identity fields must survive end-to-end.
        var topologyRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);
        try
        {
            var response = await scheduler.AlignAndDispatchAsync(request, cts.Token);

            Assert.True(response.Success);
            Assert.Equal(TopologyRepository.DefaultStructureMapId, response.Emission?.StructureMapId);
            Assert.Equal(TopologyRepository.DefaultPackageId, response.Emission?.PackageId);
            Assert.Equal(TopologyRepository.DefaultSchemaId, response.Emission?.SchemaId);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SchedulerRuntime_CanonicalRoute_RouteMissing_PropagatesFallbackJumpEvents()
    {
        // Route-missing must propagate through the full canonical chain as jump events.
        // The scheduler must not swallow route-missing or convert it to an error.
        var topologyRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        var request = new EndpointRequestDto(
            "Search", "missing_route", "entity", "Search", null, null, null);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);
        try
        {
            var response = await scheduler.AlignAndDispatchAsync(request, cts.Token);

            Assert.True(response.Success);
            Assert.Empty(response.Errors);
            Assert.NotEmpty(response.Emission?.JumpEvents ?? []);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    // ─── Queue overflow: explicit boundary ───────────────────────────────────

    [Fact]
    public async Task SchedulerRuntime_ClientQueueFull_ReturnsSchedulerQueueFullError()
    {
        // Queue overflow must return SCHEDULER_QUEUE_FULL for client triggers.
        // Silent drop is prohibited per SSOT overflow_boundary contract.
        var topologyRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 0);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await scheduler.AlignAndDispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "SCHEDULER_QUEUE_FULL");
    }

    [Fact]
    public void SchedulerRuntime_CronQueueFull_ReturnsFalseExplicitly()
    {
        // Cron overflow must return false (explicit signal).
        // Silent success on overflow is prohibited per SSOT.
        var topologyRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 1);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var first = scheduler.EnqueueCronTrigger(request);
        var second = scheduler.EnqueueCronTrigger(request);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void SchedulerRuntime_HookQueueFull_ReturnsFalseExplicitly()
    {
        var topologyRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 1);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var first = scheduler.EnqueueHookTrigger(request);
        var second = scheduler.EnqueueHookTrigger(request);

        Assert.True(first);
        Assert.False(second);
    }

    // ─── ManifestDispatcher: runtime_destination routing ─────────────────────

    [Fact]
    public async Task SchedulerRuntime_UnknownRuntimeDestination_ReturnsRuntimeDestinationUnknown()
    {
        // ManifestDispatcher must return RUNTIME_DESTINATION_UNKNOWN when no handler
        // is registered for the manifest's runtime_destination.
        // No implicit fallthrough to topology_transform_runtime is permitted.
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildRuntimeMappingTopology("nonexistent_runtime_destination_xyz"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "RUNTIME_DESTINATION_UNKNOWN");
    }

    [Fact]
    public async Task SchedulerRuntime_ManifestNotFound_ReturnsManifestNotFoundError()
    {
        // When no active manifest matches the request axes, MANIFEST_NOT_FOUND is returned.
        // No fallback to any default runtime is permitted when manifest repository is configured.
        var manifestRepo = new StubManifestRepository(manifest: null);
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    [Fact]
    public async Task SchedulerRuntime_KnownRuntimeDestination_DispatchesToRegisteredHandler()
    {
        // ManifestDispatcher must select the handler registered for the manifest's
        // runtime_destination. FakeDispatchableRuntime sentinel proves the correct handler ran.
        var sentinel = JsonSerializer.SerializeToElement(
            new { dispatchedBy = "ssot_wiring_audit_handler" });
        var fakeHandler = new FakeDispatchableRuntime(sentinel);
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildRuntimeMappingTopology("audit_test_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime>
            {
                ["audit_test_runtime"] = fakeHandler
            });
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.True(fakeHandler.WasCalled,
            "Registered handler must be called when manifest runtime_destination matches.");
    }

    // ─── db_notify boundary: client trigger rejected ──────────────────────────

    [Fact]
    public async Task SchedulerRuntime_DbNotifyFromClientTrigger_ReturnsDbNotifyTriggerKindInvalid()
    {
        // db_notify target is only allowed for hook triggers, not client triggers.
        // This enforces the SSOT listen_event_enters_scheduler_before_projection_runtime boundary.
        var manifestId = Guid.NewGuid();
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildRuntimeMappingTopology("topology_transform_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);
        var payload = JsonSerializer.SerializeToElement(
            new { manifest_id = manifestId.ToString() });
        var request = new EndpointRequestDto(
            "X", "db_notify", "projection", "broadcast", null, payload, null, "client");

        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "DB_NOTIFY_TRIGGER_KIND_INVALID");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static IReadOnlyList<JsonElement> BuildRuntimeMappingTopology(string runtimeDestination)
    {
        var entry = JsonSerializer.SerializeToElement(new
        {
            type = "runtime_mapping",
            runtime_destination = runtimeDestination
        });
        return [entry];
    }
}
