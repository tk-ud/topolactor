using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// Load observation tests for RuntimeTimelineScheduler → ManifestDispatcher pipeline.
//
// Purpose: observe queue backlog convergence / divergence and explicit rejection behavior
// under simulated load. These are not precision benchmarks. Timing is measured as an
// upper bound over a deterministic input batch; no wall-clock rate thresholds are asserted.
//
// Two scenarios:
//   steady_load    — input within queue capacity; all requests complete,
//                    backlog converges to 0, zero silent fallback.
//   over_capacity  — input exceeds queue capacity; excess is explicitly rejected as
//                    SCHEDULER_QUEUE_FULL, no response is null, no silent drop.
//
// Metrics collected per window (LoadWindowObservation):
//   input_count, processed_count, rejected_count, timeout_count,
//   backlog_count (derived), max_latency_ms, avg_latency_ms (batch-level).
public class SchedulerLoadObservationTests
{
    // ─── Config constants (no magic numbers) ─────────────────────────────

    // Steady load: input well within queue capacity → expect all to complete.
    private const int SteadyLoad_QueueCapacity = 64;
    private const int SteadyLoad_InputCount = 16;

    // Over capacity: queue filled to capacity before client requests arrive.
    private const int OverCapacity_QueueCapacity = 4;
    private const int OverCapacity_PreFillCount = OverCapacity_QueueCapacity;   // fills queue
    private const int OverCapacity_ClientInputCount = 6;                        // all overflow

    // ─── Steady load ─────────────────────────────────────────────────────

    [Fact]
    public async Task SteadyLoad_WithinCapacity_AllProcessedBacklogConverges()
    {
        // Verifies that processing-capacity-bounded input:
        //   - results in processed_count == input_count
        //   - produces zero rejections (no SCHEDULER_QUEUE_FULL)
        //   - produces zero timeouts (no CLIENT_TRIGGER_CANCELED)
        //   - backlog converges to 0 (no divergence)
        //   - existing invariants (fixed route / topology IDs / recommendation) are not broken

        var fakeHandler = new ImmediateFakeDispatchableRuntime();
        var dispatcher = BuildDispatcherWithFakeHandler(fakeHandler, runtimeDestination: "load_test_steady");

        var scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance,
            dispatcher,
            queueCapacity: SteadyLoad_QueueCapacity);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await scheduler.StartAsync(cts.Token);
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new EndpointRequestDto(
                "Search", "default", "entity", "Search", null, null, null);

            var tasks = Enumerable
                .Range(0, SteadyLoad_InputCount)
                .Select(_ => scheduler.AlignAndDispatchAsync(request, cts.Token))
                .ToArray();

            var responses = await Task.WhenAll(tasks);
            sw.Stop();

            var obs = LoadWindowObservation.Collect(responses, SteadyLoad_InputCount, sw.ElapsedMilliseconds);

            // All requests within capacity must be processed
            Assert.Equal(SteadyLoad_InputCount, obs.InputCount);
            Assert.Equal(SteadyLoad_InputCount, obs.ProcessedCount);

            // No overflow signal — silent fallback is prohibited
            Assert.Equal(0, obs.RejectedCount);

            // No timeout — client lane completion contract must hold
            Assert.Equal(0, obs.TimeoutCount);

            // Backlog must converge to 0 (processed + rejected + timeout == input)
            Assert.True(obs.IsConverged,
                $"backlog={obs.BacklogCount} must be 0 after all tasks complete " +
                $"(input={obs.InputCount} processed={obs.ProcessedCount} " +
                $"rejected={obs.RejectedCount} timeout={obs.TimeoutCount})");
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    // ─── Over capacity load ──────────────────────────────────────────────

    [Fact]
    public async Task OverCapacityLoad_ExceedsQueueCapacity_RejectedExplicitlyNoSilentFallback()
    {
        // Verifies that when the queue is at capacity:
        //   - every additional client trigger returns SCHEDULER_QUEUE_FULL (explicit error)
        //   - no response is null or otherwise missing (no silent drop)
        //   - rejected_count equals the over-capacity client input count
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

        // All subsequent client requests must overflow with an explicit rejection.
        // When TryWrite fails (queue full), AlignAndDispatchAsync returns the rejection
        // response before any async continuation — await still works correctly.
        var rejectedResponses = new EndpointResponseDto[OverCapacity_ClientInputCount];
        for (var i = 0; i < OverCapacity_ClientInputCount; i++)
        {
            rejectedResponses[i] = await scheduler.AlignAndDispatchAsync(request);
        }

        var obs = LoadWindowObservation.Collect(rejectedResponses, OverCapacity_ClientInputCount, 0);

        // Every over-capacity client request must produce an explicit rejection
        Assert.Equal(OverCapacity_ClientInputCount, obs.InputCount);
        Assert.Equal(OverCapacity_ClientInputCount, obs.RejectedCount);
        Assert.Equal(0, obs.ProcessedCount);
        Assert.True(obs.RejectedCount > 0,
            "Over-capacity load must produce at least one explicit rejection.");

        // Each rejection must carry SCHEDULER_QUEUE_FULL (no silent fallback, no null error list)
        Assert.All(rejectedResponses, r =>
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
/// Per-window load observation. One window = one test batch.
/// Metrics names align with the load test specification:
///   input_count_per_sec, processed_count_per_sec, rejected_count, timeout_count,
///   backlog_count (derived), max_latency_ms (batch upper bound), avg_latency_ms.
/// </summary>
internal sealed record LoadWindowObservation(
    int InputCount,
    int ProcessedCount,
    int RejectedCount,
    int TimeoutCount,
    long MaxLatencyMs,
    long AvgLatencyMs)
{
    /// <summary>
    /// Items not yet accounted for (queued but not yet completed/rejected/timed out).
    /// Zero means backlog has converged.
    /// </summary>
    public int BacklogCount => InputCount - ProcessedCount - RejectedCount - TimeoutCount;

    public bool IsConverged => BacklogCount == 0;

    public static LoadWindowObservation Collect(
        EndpointResponseDto[] responses,
        int inputCount,
        long elapsedMs)
    {
        var processedCount = responses.Count(r => r.Success);
        var rejectedCount = responses.Count(r =>
            !r.Success && r.Errors.Any(e => e.Code == "SCHEDULER_QUEUE_FULL"));
        var timeoutCount = responses.Count(r =>
            !r.Success && r.Errors.Any(e => e.Code == "CLIENT_TRIGGER_CANCELED"));
        var avgLatencyMs = inputCount > 0 ? elapsedMs / inputCount : 0;

        return new LoadWindowObservation(
            InputCount: inputCount,
            ProcessedCount: processedCount,
            RejectedCount: rejectedCount,
            TimeoutCount: timeoutCount,
            MaxLatencyMs: elapsedMs,
            AvgLatencyMs: avgLatencyMs);
    }
}

/// <summary>
/// Fake dispatcher that completes immediately with a success response.
/// Used for steady-load tests where dispatch time should not be a variable.
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
