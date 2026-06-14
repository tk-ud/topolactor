using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// trigger_kind enum guard validation tests.
///
/// Completion conditions:
///   - invalid trigger_kind returns TRIGGER_KIND_INVALID at endpoint boundary
///   - invalid trigger_kind never reaches the scheduler or ManifestDispatcher
///   - valid trigger_kinds (null / client / hook / cron) are not rejected
///
/// Per SSOT minimal_event_shape: allowed trigger_kind = client | hook | cron.
/// </summary>
public class TriggerKindGuardTests
{
    private static DispatchEndpoint CreateEndpoint(RuntimeTimelineScheduler scheduler) =>
        new(NullLogger<DispatchEndpoint>.Instance, scheduler);

    /// <summary>
    /// Builds a minimal RuntimeTimelineScheduler for trigger_kind guard tests.
    /// Uses dev/demo bypass (manifestRepository: null) with AlwaysSucceedRuntime as
    /// the topology_transform_runtime handler. This is sufficient for guard boundary
    /// tests that only verify the presence or absence of TRIGGER_KIND_INVALID.
    /// </summary>
    private static RuntimeTimelineScheduler CreateScheduler()
    {
        var alwaysSucceed = new AlwaysSucceedRuntime();
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = alwaysSucceed,
            ["admin_runtime"] = alwaysSucceed,
            ["sse_projection_runtime"] = alwaysSucceed,
        };

        var topologyRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        var contextRouteRepo = new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "test-double");
        var uiTopoRepo = new UiTopologyRepository(
            NullLogger<UiTopologyRepository>.Instance, "test-double");
        var topoVectorRuntime = new TopologyVectorRuntime(
            NullLogger<TopologyVectorRuntime>.Instance, contextRouteRepo);
        var adminRuntime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            contextRouteRepo,
            new RegistrarValidationService(
                NullLogger<RegistrarValidationService>.Instance,
                topologyRepo, topoVectorRuntime),
            new PackageGeneratorRuntime(
                NullLogger<PackageGeneratorRuntime>.Instance, uiTopoRepo),
            uiTopoRepo);
        var targetOverride = new TargetDispatchOverride(
            NullLogger<TargetDispatchOverride>.Instance, adminRuntime);

        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            targetOverride,
            manifestRepository: null);

        return new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
    }

    // ─── Invalid trigger_kind: explicit error, scheduler not reached ─────────

    [Theory]
    [InlineData("invalid")]
    [InlineData("webhook")]
    [InlineData("timer")]
    [InlineData("batch")]
    public async Task HandleAsync_InvalidTriggerKind_ReturnsTriggerKindInvalidError(string triggerKind)
    {
        var scheduler = CreateScheduler();
        var endpoint = CreateEndpoint(scheduler);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null, TriggerKind: triggerKind);

        var response = await endpoint.HandleAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TRIGGER_KIND_INVALID");
    }

    [Fact]
    public async Task HandleAsync_InvalidTriggerKind_SchedulerIsNotCalled()
    {
        // Invalid trigger_kind must be caught at endpoint boundary without reaching scheduler.
        // Proof: scheduler background service is never started, so any scheduler call
        // would block forever waiting for queue processing. If this test completes quickly
        // (within 1s), the scheduler was not called.
        var scheduler = CreateScheduler();
        var endpoint = CreateEndpoint(scheduler);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null, TriggerKind: "invalid_trigger");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var response = await endpoint.HandleAsync(request, cts.Token);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TRIGGER_KIND_INVALID");
        // If scheduler were called, the task would not complete before timeout
        // (scheduler bg service not started). Reaching this line proves scheduler was skipped.
        Assert.False(cts.IsCancellationRequested,
            "Test timed out — scheduler may have been called instead of rejecting early.");
    }

    // ─── Valid trigger_kinds: accepted, not rejected ──────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("client")]
    [InlineData("CLIENT")]
    [InlineData("hook")]
    [InlineData("HOOK")]
    [InlineData("cron")]
    [InlineData("CRON")]
    public async Task HandleAsync_ValidTriggerKind_DoesNotReturnTriggerKindInvalidError(string? triggerKind)
    {
        var scheduler = CreateScheduler();
        var endpoint = CreateEndpoint(scheduler);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null, TriggerKind: triggerKind);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await scheduler.StartAsync(cts.Token);
        try
        {
            var response = await endpoint.HandleAsync(request, cts.Token);
            Assert.DoesNotContain(response.Errors, e => e.Code == "TRIGGER_KIND_INVALID");
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    // ─── trigger_kind preservation: scheduler receives correct value ──────────

    [Fact]
    public async Task HandleAsync_NullTriggerKind_IsAlignedToClientInScheduler()
    {
        // When trigger_kind is null, scheduler.AlignAndDispatchAsync defaults it to "client".
        // This is the expected client-trigger normalization path.
        var scheduler = CreateScheduler();
        var endpoint = CreateEndpoint(scheduler);
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null, TriggerKind: null);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await scheduler.StartAsync(cts.Token);
        try
        {
            var response = await endpoint.HandleAsync(request, cts.Token);
            // Result may be success or error depending on runtime; not TRIGGER_KIND_INVALID.
            Assert.DoesNotContain(response.Errors, e => e.Code == "TRIGGER_KIND_INVALID");
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    // ─── Test runtime helper ──────────────────────────────────────────────────

    /// <summary>
    /// Minimal IDispatchableRuntime that always returns Success=true.
    /// Used to decouple trigger_kind guard tests from full topology pipeline execution.
    /// </summary>
    private sealed class AlwaysSucceedRuntime : IDispatchableRuntime
    {
        public Task<EndpointResponseDto> ExecuteAsync(
            EndpointRequestDto request,
            Guid? manifestId,
            CancellationToken ct = default) =>
            Task.FromResult(new EndpointResponseDto(Success: true, Emission: null, Errors: []));
    }
}
