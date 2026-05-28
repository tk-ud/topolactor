using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// Protocol invariant tests for RuntimeTimelineScheduler under burst / repeated client-lane input.
//
// Purpose: verify that the local application-layer communication protocol holds when
// a front instance or client lane sends a burst of requests. This is not a performance
// benchmark. The subject under test is the protocol contract:
//
//   - request accepted / rejected is always explicit (no silent drop)
//   - client completion contract is preserved end-to-end
//   - queue full surfaces as SCHEDULER_QUEUE_FULL, never as silent fallback
//   - timeout / cancellation is observable, never swallowed
//   - backlog (unresolved requests) converges to 0 after burst completes
//
// Two scenarios:
//   within_capacity — burst input fits in queue; all client requests complete,
//                     no protocol break, backlog drains to 0.
//   over_capacity   — burst input exceeds queue; every overflow request returns
//                     SCHEDULER_QUEUE_FULL with no silent fallback and no null response.
//
// Protocol state is captured per burst in BurstProtocolObservation:
//   input_count, processed_count, rejected_count, timeout_count, backlog_count.
public class FrontendBurstProtocolTests
{
    // ─── Config constants (no magic numbers) ─────────────────────────────

    // Within-capacity burst: input count well below queue capacity.
    private const int WithinCapacity_QueueCapacity = 64;
    private const int WithinCapacity_BurstInputCount = 16;

    // Over-capacity burst: queue is filled to capacity before client requests arrive.
    private const int OverCapacity_QueueCapacity = 4;
    private const int OverCapacity_PreFillCount = OverCapacity_QueueCapacity;  // fills queue
    private const int OverCapacity_BurstInputCount = 6;                        // all overflow

    // ─── Within-capacity burst ────────────────────────────────────────────

    [Fact]
    public async Task FrontendBurst_WithinQueueCapacity_CompletesAllClientRequests()
    {
        // Protocol invariants verified:
        //   - all accepted requests complete (processed_count == input_count)
        //   - no request is silently dropped (rejected_count == 0)
        //   - client completion contract holds (timeout_count == 0)
        //   - no unresolved communication remains after burst (backlog_count == 0)
        //   - fixed route / existing dispatch invariants are not broken

        var fakeHandler = new ImmediateFakeDispatchableRuntime();
        var dispatcher = BuildDispatcherWithFakeHandler(fakeHandler, runtimeDestination: "burst_protocol_test");

        var scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance,
            dispatcher,
            queueCapacity: WithinCapacity_QueueCapacity);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await scheduler.StartAsync(cts.Token);
        try
        {
            var request = new EndpointRequestDto(
                "Search", "default", "entity", "Search", null, null, null);

            var tasks = Enumerable
                .Range(0, WithinCapacity_BurstInputCount)
                .Select(_ => scheduler.AlignAndDispatchAsync(request, cts.Token))
                .ToArray();

            var responses = await Task.WhenAll(tasks);

            var obs = BurstProtocolObservation.Collect(responses, WithinCapacity_BurstInputCount);

            // All burst requests must be processed — no silent drop
            Assert.Equal(WithinCapacity_BurstInputCount, obs.InputCount);
            Assert.Equal(WithinCapacity_BurstInputCount, obs.ProcessedCount);

            // No queue-full signal — protocol must not reject within-capacity input
            Assert.Equal(0, obs.RejectedCount);

            // No timeout — client lane completion contract must not break
            Assert.Equal(0, obs.TimeoutCount);

            // Backlog must drain to 0 — no unresolved protocol state after burst
            Assert.True(obs.IsConverged,
                $"backlog={obs.BacklogCount} must be 0 after burst completes " +
                $"(input={obs.InputCount} processed={obs.ProcessedCount} " +
                $"rejected={obs.RejectedCount} timeout={obs.TimeoutCount})");
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    // ─── Over-capacity burst ──────────────────────────────────────────────

    [Fact]
    public async Task FrontendBurst_ExceedsQueueCapacity_ReturnsExplicitQueueFullWithoutSilentFallback()
    {
        // Protocol invariants verified:
        //   - every request that exceeds capacity returns SCHEDULER_QUEUE_FULL
        //   - no response is null or missing (silent drop is prohibited)
        //   - Success is false for every rejected request (no ambiguous state)
        //   - rejected_count equals the over-capacity input count
        //
        // Background service is intentionally NOT started so pre-filled cron items
        // remain in the channel, holding it at full capacity for the duration of this test.

        var topologyRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);

        var scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance,
            dispatcher,
            queueCapacity: OverCapacity_QueueCapacity);

        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        // Fill the queue to capacity with cron triggers (no consumer running, so they stay put)
        for (var i = 0; i < OverCapacity_PreFillCount; i++)
        {
            var accepted = scheduler.EnqueueCronTrigger(request);
            Assert.True(accepted,
                $"Pre-fill cron item {i + 1}/{OverCapacity_PreFillCount} must be accepted " +
                "before the queue is full.");
        }

        // Send burst of client requests — all must overflow with an explicit protocol error.
        // When TryWrite fails (queue full), AlignAndDispatchAsync returns the rejection
        // synchronously before any async continuation.
        var responses = new EndpointResponseDto[OverCapacity_BurstInputCount];
        for (var i = 0; i < OverCapacity_BurstInputCount; i++)
        {
            responses[i] = await scheduler.AlignAndDispatchAsync(request);
        }

        var obs = BurstProtocolObservation.Collect(responses, OverCapacity_BurstInputCount);

        // Every over-capacity request must be explicitly rejected
        Assert.Equal(OverCapacity_BurstInputCount, obs.InputCount);
        Assert.Equal(OverCapacity_BurstInputCount, obs.RejectedCount);
        Assert.Equal(0, obs.ProcessedCount);
        Assert.True(obs.RejectedCount > 0,
            "Over-capacity burst must surface at least one explicit rejection.");

        // Each rejection must carry the protocol error code — null errors list is silent fallback
        Assert.All(responses, r =>
        {
            Assert.False(r.Success);
            Assert.NotNull(r.Errors);
            Assert.Contains(r.Errors, e => e.Code == "SCHEDULER_QUEUE_FULL");
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static ManifestDispatcher BuildDispatcherWithFakeHandler(
        IDispatchableRuntime handler,
        string runtimeDestination)
    {
        var topologyEntry = JsonSerializer.SerializeToElement(new
        {
            type = "runtime_mapping",
            runtime_destination = runtimeDestination
        });
        var manifest = new ManifestRecord(
            ManifestId: Guid.NewGuid(),
            RelationRegistryId: null,
            Topology: [topologyEntry],
            Status: "active");

        return RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            new StubManifestRepository(manifest),
            extraHandlers: new Dictionary<string, IDispatchableRuntime>
            {
                [runtimeDestination] = handler
            });
    }
}

/// <summary>
/// Protocol-state snapshot for one burst. Counts each request outcome by
/// communication result: processed (Success), rejected (SCHEDULER_QUEUE_FULL),
/// or timed out (CLIENT_TRIGGER_CANCELED). Backlog is derived as the residual
/// unresolved count; zero means the burst completed without protocol leakage.
/// </summary>
internal sealed record BurstProtocolObservation(
    int InputCount,
    int ProcessedCount,
    int RejectedCount,
    int TimeoutCount)
{
    /// <summary>
    /// Requests not accounted for by any terminal protocol state.
    /// Zero means no unresolved communication remains after the burst.
    /// </summary>
    public int BacklogCount => InputCount - ProcessedCount - RejectedCount - TimeoutCount;

    public bool IsConverged => BacklogCount == 0;

    public static BurstProtocolObservation Collect(
        EndpointResponseDto[] responses,
        int inputCount)
    {
        var processedCount = responses.Count(r => r.Success);
        var rejectedCount = responses.Count(r =>
            !r.Success && r.Errors.Any(e => e.Code == "SCHEDULER_QUEUE_FULL"));
        var timeoutCount = responses.Count(r =>
            !r.Success && r.Errors.Any(e => e.Code == "CLIENT_TRIGGER_CANCELED"));

        return new BurstProtocolObservation(
            InputCount: inputCount,
            ProcessedCount: processedCount,
            RejectedCount: rejectedCount,
            TimeoutCount: timeoutCount);
    }
}

/// <summary>
/// Fake dispatcher that completes immediately with a success response.
/// Isolates protocol state transitions from dispatch execution time.
/// </summary>
internal sealed class ImmediateFakeDispatchableRuntime : IDispatchableRuntime
{
    private int _callCount;
    public int CallCount => _callCount;

    public Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _callCount);
        var emission = new Emission(
            StructureMapId: null,
            PackageId: null,
            SchemaId: null,
            ComponentIds: [],
            Data: null,
            Errors: [],
            ContextRouteRecommendation: null);
        return Task.FromResult(new EndpointResponseDto(Success: true, Emission: emission, Errors: []));
    }
}
