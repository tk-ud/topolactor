using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ExternalPortDispatchRuntimeTests
{
    private static readonly Guid ResponsePortId = Guid.Parse("00000000-0000-0000-0000-00000000abcd");

    [Fact]
    public async Task ExecuteAsync_ValidTargetRef_ReachesGenericPolicyBoundary()
    {
        var repo = new FakeRepo(NewRecord(ResponsePortId, credentialKind: "none"), NewPolicy());
        var runtime = new ExternalPortDispatchRuntime(NullLogger<ExternalPortDispatchRuntime>.Instance, repo, new ExternalPortPolicyStepExecutor());
        var response = await runtime.ExecuteAsync(NewRequest($"external-port:response_port:{ResponsePortId}"), null);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission?.Data);
        Assert.Contains("boundary_reached", response.Emission!.Data!.Value.GetRawText());
        Assert.Equal(ResponsePortId, repo.LastPortId);
    }

    [Theory]
    [InlineData(null, "EXTERNAL_PORT_TARGET_REF_MISSING")]
    [InlineData("external-port:response_port:not-a-guid", "EXTERNAL_PORT_TARGET_REF_INVALID")]
    [InlineData("external-port:hook_port:00000000-0000-0000-0000-00000000abcd", "EXTERNAL_PORT_TARGET_REF_INVALID")]
    public async Task ExecuteAsync_MissingOrMalformedTargetRef_FailsClosed(string? targetRef, string expectedCode)
    {
        var runtime = new ExternalPortDispatchRuntime(NullLogger<ExternalPortDispatchRuntime>.Instance, new FakeRepo(null, null), new ExternalPortPolicyStepExecutor());
        var response = await runtime.ExecuteAsync(NewRequest(targetRef), null);
        Assert.False(response.Success);
        Assert.Equal(expectedCode, response.Errors[0].Code);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPortRecord_FailsClosed()
    {
        var runtime = new ExternalPortDispatchRuntime(NullLogger<ExternalPortDispatchRuntime>.Instance, new FakeRepo(null, null), new ExternalPortPolicyStepExecutor());
        var response = await runtime.ExecuteAsync(NewRequest($"external-port:response_port:{ResponsePortId}"), null);
        Assert.False(response.Success);
        Assert.Equal("EXTERNAL_PORT_RECORD_MISSING", response.Errors[0].Code);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPolicy_FailsClosed()
    {
        var runtime = new ExternalPortDispatchRuntime(NullLogger<ExternalPortDispatchRuntime>.Instance, new FakeRepo(NewRecord(ResponsePortId, "none"), null), new ExternalPortPolicyStepExecutor());
        var response = await runtime.ExecuteAsync(NewRequest($"external-port:response_port:{ResponsePortId}"), null);
        Assert.False(response.Success);
        Assert.Equal("EXTERNAL_PORT_POLICY_MISSING", response.Errors[0].Code);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCredentialRequirement_FailsClosed()
    {
        var runtime = new ExternalPortDispatchRuntime(NullLogger<ExternalPortDispatchRuntime>.Instance, new FakeRepo(NewRecord(ResponsePortId, "external", referenceKey: null), NewPolicy()), new ExternalPortPolicyStepExecutor());
        var response = await runtime.ExecuteAsync(NewRequest($"external-port:response_port:{ResponsePortId}"), null);
        Assert.False(response.Success);
        Assert.Equal("EXTERNAL_CREDENTIAL_REQUIREMENT_INVALID", response.Errors[0].Code);
    }


    [Fact]
    public async Task ExternalPortParentCompletion_HookPathRouteKey_ExecutesPolicyEnqueuesLogsEvidenceAndBroadcastsSseProjectionResponse()
    {
        var hookPortId = Guid.Parse("00000000-0000-0000-0000-00000000beef");
        var record = new ExternalPortRecord(
            hookPortId,
            "hook_port",
            "webhook_inbox_bundle",
            "generic-webhook",
            null,
            "/hooks/external/inbox",
            "x-signature",
            "incoming",
            "none",
            null,
            Active: true);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(),
            "webhook_inbox_hook_policy",
            "hook_port",
            "webhook_inbox_bundle",
            [
                NewStep(1, "resolve_port_record"),
                NewStep(2, "verify_signature_by_config", new Dictionary<string, string> { ["expected_signature"] = "sig-ok" }),
                NewStep(3, "enqueue_scheduler_event"),
                NewStep(4, "append_runtime_event_log", new Dictionary<string, string>
                {
                    ["event_type"] = "scheduler_enqueued",
                    ["evidence_table_ref"] = "topology.webhook_intake_snapshots",
                    ["projection_table_ref"] = "topology.webhook_intake_snapshots",
                    ["status_value"] = "scheduler_enqueued"
                })
            ],
            Active: true);
        var repo = new FakeRepo(record, policy);
        var eventLog = new FakeRuntimeEventLogRepository();
        var evidence = new FakeConsumerEvidenceRepository();
        var broadcaster = new SseEventBroadcaster();
        var subscription = broadcaster.Subscribe();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: eventLog, consumerEvidenceRepository: evidence);
        var runtime = new ExternalPortDispatchRuntime(NullLogger<ExternalPortDispatchRuntime>.Instance, repo, executor, broadcaster);

        var request = NewHookRouteRequest("/hooks/external/inbox", "incoming", "sig-ok");
        var response = await runtime.ExecuteAsync(request, null);

        Assert.True(response.Success);
        Assert.Equal("/hooks/external/inbox", repo.LastHookPath);
        Assert.Equal("incoming", repo.LastHookRouteKey);
        Assert.Equal("scheduler_enqueued", eventLog.LastEventType);
        Assert.Equal("topology.webhook_intake_snapshots", evidence.LastAppendTableRef);
        Assert.Equal("topology.webhook_intake_snapshots", evidence.LastLoadTableRef);
        Assert.Contains("enqueue_scheduler_event", response.Emission!.Data!.Value.GetRawText());
        Assert.Contains("projection_response", response.Emission!.Data!.Value.GetRawText());
        Assert.True(subscription.Reader.TryRead(out var evt));
        Assert.Equal("external_port_dispatch", evt.EventType);
        Assert.Contains("projection_response", evt.Data);
    }

    [Fact]
    public void Source_DoesNotContainProviderKindBranching()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/runtime/ExternalPortDispatchRuntime.cs"));
        Assert.DoesNotContain("ProviderKind ==", source, StringComparison.Ordinal);
        Assert.DoesNotContain("switch (record.ProviderKind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"smtp\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("case \"stripe\"", source, StringComparison.OrdinalIgnoreCase);
    }

    private static EndpointRequestDto NewRequest(string? targetRef)
    {
        var payload = targetRef is null
            ? JsonSerializer.SerializeToElement(new { dispatch_payload = new { subject = "hello" } })
            : JsonSerializer.SerializeToElement(new { port_target_ref = targetRef, dispatch_payload = new { subject = "hello" } });
        return new EndpointRequestDto("dispatchExternalPort", "external_port", "external_port", "dispatchExternalPort", null, payload, null, "client");
    }

    private static EndpointRequestDto NewHookRouteRequest(string hookPath, string routeKey, string signature)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            hook_path = hookPath,
            route_key = routeKey,
            signature_input = new { signature },
            dispatch_payload = new { event_id = "evt-test" }
        });
        return new EndpointRequestDto("dispatchExternalPort", "external_port", "external_port", "dispatchExternalPort", null, payload, null, "client");
    }

    private static ExternalPortRecord NewRecord(Guid id, string credentialKind, string? referenceKey = null) =>
        new(id, "response_port", "test_bundle", "smtp", "https://example.invalid", null, null, null, credentialKind, referenceKey, true);

    private static ExternalPortPolicy NewPolicy() =>
        new(Guid.NewGuid(), "test_policy", "response_port", "test_bundle", [NewStep(1, "capture_response")], true);

    private static ExternalPortPolicyStep NewStep(int order, string operationKey) =>
        NewStep(order, operationKey, new Dictionary<string, string>());

    private static ExternalPortPolicyStep NewStep(int order, string operationKey, IReadOnlyDictionary<string, string> config) =>
        new(Guid.NewGuid(), Guid.NewGuid(), order, operationKey, config, true);

    private sealed class FakeRepo : IExternalPortPolicyRepository
    {
        private readonly ExternalPortRecord? _record;
        private readonly ExternalPortPolicy? _policy;
        public Guid? LastPortId { get; private set; }
        public string? LastHookPath { get; private set; }
        public string? LastHookRouteKey { get; private set; }
        public FakeRepo(ExternalPortRecord? record, ExternalPortPolicy? policy) { _record = record; _policy = policy; }
        public Task<ExternalPortRecord?> LoadPortRecordAsync(string requiredByBundle, string portKind, string? routeKey, CancellationToken ct = default) => Task.FromResult(_record);
        public Task<ExternalPortRecord?> LoadPortRecordByIdAsync(string portKind, Guid portId, string? routeKey, CancellationToken ct = default) { LastPortId = portId; return Task.FromResult(_record); }
        public Task<ExternalPortRecord?> LoadPortRecordByCanonicalBindingAsync(string manifestKey, string tableRef, string portKind, Guid portId, string? routeKey, CancellationToken ct = default) { LastPortId = portId; return Task.FromResult(_record); }
        public Task<ExternalPortRecord?> LoadHookPortRecordAsync(string hookPath, string routeKey, CancellationToken ct = default) { LastHookPath = hookPath; LastHookRouteKey = routeKey; return Task.FromResult(_record); }
        public Task<ExternalPortPolicy?> LoadPolicyAsync(ExternalPortRecord portRecord, CancellationToken ct = default) => Task.FromResult(_policy);
    }

    private sealed class FakeRuntimeEventLogRepository : IExternalPortRuntimeEventLogRepository
    {
        public string? LastEventType { get; private set; }
        public Task AppendAsync(string eventType, string? entityId, string? requiredByBundle, CancellationToken ct = default)
        {
            LastEventType = eventType;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConsumerEvidenceRepository : IExternalPortConsumerEvidenceRepository
    {
        public string? LastAppendTableRef { get; private set; }
        public string? LastLoadTableRef { get; private set; }

        public Task AppendEvidenceAsync(string tableRef, string eventType, string? entityId, string? requiredByBundle, ExternalPortExecutionContext context, IReadOnlyDictionary<string, string> stepConfig, CancellationToken ct = default)
        {
            LastAppendTableRef = tableRef;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExternalPortConsumerEvidenceProjection>> LoadProjectionAsync(string tableRef, string? requiredByBundle, string? entityId, int limit = 20, CancellationToken ct = default)
        {
            LastLoadTableRef = tableRef;
            IReadOnlyList<ExternalPortConsumerEvidenceProjection> rows =
            [
                new(tableRef, "scheduler_enqueued", entityId, "scheduler_enqueued", new Dictionary<string, string> { ["required_by_bundle"] = requiredByBundle ?? string.Empty })
            ];
            return Task.FromResult(rows);
        }
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relativePath);
    }
}
