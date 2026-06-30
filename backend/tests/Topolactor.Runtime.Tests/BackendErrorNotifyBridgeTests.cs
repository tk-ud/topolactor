using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Post-notify bridge tests: durable queue is the source of truth, dispatch routes through the
/// scheduler hook trigger seam (never ExternalPortDispatchRuntime directly), accepted boundary
/// result -> acknowledged, failure -> failed_retryable / failed_terminal (row never dropped).
/// </summary>
public class BackendErrorNotifyBridgeTests
{
    private static BackendErrorNotifyClaim Claim(int attempt = 1) => new(
        QueueId: Guid.NewGuid(),
        ErrorId: Guid.NewGuid(),
        AttemptCount: attempt,
        OriginLayer: BackendErrorOriginLayer.AbstractFunctionExecutor,
        BoundaryKey: "execute_abstract_function:test.fn",
        ErrorCode: "ABSTRACT_FUNCTION_RUNTIME_LANE_INVALID",
        Severity: "error",
        StackHash: "sha256:abc");

    private static BackendErrorNotifyBridge BuildBridge(
        FakeNotifyQueueRepository repo, FakeHookDispatcher dispatcher,
        BackendErrorNotifyBridgeOptions? options = null) =>
        new(NullLogger<BackendErrorNotifyBridge>.Instance,
            "Host=localhost;Database=unused", repo, dispatcher,
            options ?? new BackendErrorNotifyBridgeOptions(MaxAttempts: 5));

    [Fact]
    public async Task AcceptedDispatch_AcknowledgesRow_AndRoutesAsHookTrigger()
    {
        var claim = Claim();
        var repo = new FakeNotifyQueueRepository(claim);
        var dispatcher = new FakeHookDispatcher(new EndpointResponseDto(true, null, []));
        var bridge = BuildBridge(repo, dispatcher);

        var processed = await bridge.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(new[] { claim.QueueId }, repo.Acknowledged);
        Assert.Empty(repo.Failed);

        // Dispatch routed through the scheduler hook trigger seam with the SSOT payload shape.
        var request = Assert.Single(dispatcher.Requests);
        Assert.Equal("hook", request.TriggerKind);
        Assert.Equal("external_port", request.Target);
        Assert.Equal("dispatchExternalPort", request.Action);
        var payload = request.Payload!.Value;
        Assert.Equal("/hooks/error_notify", payload.GetProperty("hook_path").GetString());
        Assert.Equal("error_notify", payload.GetProperty("route_key").GetString());
        var dispatchPayload = payload.GetProperty("dispatch_payload");
        Assert.Equal(claim.ErrorId.ToString(), dispatchPayload.GetProperty("error_id").GetString());
        Assert.Equal(claim.QueueId.ToString(), dispatchPayload.GetProperty("queue_id").GetString());
        Assert.Equal(claim.OriginLayer, dispatchPayload.GetProperty("origin_layer").GetString());
        Assert.Equal(claim.BoundaryKey, dispatchPayload.GetProperty("boundary_key").GetString());
        Assert.Equal(claim.ErrorCode, dispatchPayload.GetProperty("error_code").GetString());
        Assert.Equal(claim.Severity, dispatchPayload.GetProperty("severity").GetString());
        Assert.Equal(claim.StackHash, dispatchPayload.GetProperty("stack_hash").GetString());
    }

    [Fact]
    public async Task RejectedDispatch_UnderMaxAttempts_FailsRetryable_NotDeleted()
    {
        var claim = Claim(attempt: 2);
        var repo = new FakeNotifyQueueRepository(claim);
        var dispatcher = new FakeHookDispatcher(
            new EndpointResponseDto(false, null, [new ValidationError("EXTERNAL_PORT_RECORD_MISSING", "no hook port")]));
        var bridge = BuildBridge(repo, dispatcher, new BackendErrorNotifyBridgeOptions(MaxAttempts: 5));

        await bridge.ProcessOnceAsync(CancellationToken.None);

        Assert.Empty(repo.Acknowledged);
        var fail = Assert.Single(repo.Failed);
        Assert.Equal(claim.QueueId, fail.QueueId);
        Assert.False(fail.Terminal); // attempt 2 < max 5 -> retryable, row preserved
        Assert.Contains("EXTERNAL_PORT_RECORD_MISSING", fail.Reason);
    }

    [Fact]
    public async Task RejectedDispatch_AtMaxAttempts_FailsTerminal()
    {
        var claim = Claim(attempt: 5);
        var repo = new FakeNotifyQueueRepository(claim);
        var dispatcher = new FakeHookDispatcher(
            new EndpointResponseDto(false, null, [new ValidationError("EXTERNAL_PORT_POLICY_MISSING", "x")]));
        var bridge = BuildBridge(repo, dispatcher, new BackendErrorNotifyBridgeOptions(MaxAttempts: 5));

        await bridge.ProcessOnceAsync(CancellationToken.None);

        var fail = Assert.Single(repo.Failed);
        Assert.True(fail.Terminal); // attempt 5 >= max 5 -> terminal
    }

    [Fact]
    public async Task DispatchThrows_RowIsFailedNotDropped()
    {
        var claim = Claim(attempt: 1);
        var repo = new FakeNotifyQueueRepository(claim);
        var dispatcher = new FakeHookDispatcher(throwOnDispatch: true);
        var bridge = BuildBridge(repo, dispatcher);

        await bridge.ProcessOnceAsync(CancellationToken.None);

        Assert.Empty(repo.Acknowledged);
        Assert.Single(repo.Failed); // preserved as failed_retryable, never deleted
    }

    [Fact]
    public async Task EmptyQueue_ProcessesNothing()
    {
        var repo = new FakeNotifyQueueRepository();
        var dispatcher = new FakeHookDispatcher(new EndpointResponseDto(true, null, []));
        var bridge = BuildBridge(repo, dispatcher);

        Assert.Equal(0, await bridge.ProcessOnceAsync(CancellationToken.None));
        Assert.Empty(dispatcher.Requests);
    }

    private sealed class FakeNotifyQueueRepository : IBackendErrorNotifyQueueRepository
    {
        private readonly Queue<IReadOnlyList<BackendErrorNotifyClaim>> _batches = new();
        public ConcurrentQueue<Guid> Acknowledged { get; } = new();
        public ConcurrentQueue<(Guid QueueId, string Reason, bool Terminal)> Failed { get; } = new();

        public FakeNotifyQueueRepository(params BackendErrorNotifyClaim[] firstBatch)
        {
            _batches.Enqueue(firstBatch);
            _batches.Enqueue(Array.Empty<BackendErrorNotifyClaim>()); // subsequent claims drain empty
        }

        public Task<IReadOnlyList<BackendErrorNotifyClaim>> ClaimPendingAsync(int batchSize, TimeSpan retryBackoff, CancellationToken ct = default)
            => Task.FromResult(_batches.Count > 0 ? _batches.Dequeue() : Array.Empty<BackendErrorNotifyClaim>());

        public Task AcknowledgeAsync(Guid queueId, CancellationToken ct = default)
        {
            Acknowledged.Enqueue(queueId);
            return Task.CompletedTask;
        }

        public Task FailAsync(Guid queueId, string reason, bool terminal, CancellationToken ct = default)
        {
            Failed.Enqueue((queueId, reason, terminal));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHookDispatcher : IBackendErrorNotifyHookDispatcher
    {
        private readonly EndpointResponseDto? _result;
        private readonly bool _throw;
        public ConcurrentQueue<EndpointRequestDto> Requests { get; } = new();

        public FakeHookDispatcher(EndpointResponseDto result) => _result = result;
        public FakeHookDispatcher(bool throwOnDispatch) => _throw = throwOnDispatch;

        public Task<EndpointResponseDto> DispatchAsync(EndpointRequestDto request, CancellationToken ct = default)
        {
            Requests.Enqueue(request);
            if (_throw) throw new InvalidOperationException("SCHEDULER_DISPATCH_THREW");
            return Task.FromResult(_result!);
        }
    }
}
