using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
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
    public async Task WebhookInboxPortConsumer_SignatureVerificationFailure_FailsClosedAndDoesNotEnqueueScheduler()
    {
        var hookPortId = Guid.Parse("00000000-0000-0000-0000-00000000cafe");
        var record = new ExternalPortRecord(
            hookPortId, "hook_port", "webhook_inbox_bundle", "generic-webhook",
            null, "/hooks/webhook_inbox", "x-webhook-signature", "webhook_inbox", "none", null, Active: true);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "webhook_inbox_hook_policy", "hook_port", "webhook_inbox_bundle",
            [
                NewStep(1, "resolve_port_record"),
                NewStep(2, "verify_signature_by_config", new Dictionary<string, string> { ["expected_signature"] = "expected-sig" }),
                NewStep(3, "enqueue_scheduler_event")
            ],
            Active: true);
        var repo = new FakeRepo(record, policy);
        var eventLog = new CapturingRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor();
        var runtime = new ExternalPortDispatchRuntime(NullLogger<ExternalPortDispatchRuntime>.Instance, repo, executor, null, eventLog);

        var response = await runtime.ExecuteAsync(NewHookRouteRequest("/hooks/webhook_inbox", "webhook_inbox", "wrong-sig"), null);

        Assert.False(response.Success);
        Assert.Equal("EXTERNAL_SIGNATURE_VERIFICATION_FAILED", response.Errors[0].Code);
        Assert.Contains("signature_verification_failure", eventLog.EventTypes);
        Assert.DoesNotContain("scheduler_enqueued", eventLog.EventTypes);
    }

    [Fact]
    public async Task WebhookInboxPortConsumer_FullEventLogChain_LogsFullAuditBoundary()
    {
        // 12-step policy mirrors seed_empty.sql policy e8.
        // SSOT intake_snapshot→validate→preview→explicit_apply→canonical_runtime_route boundary
        // expressed entirely through append_runtime_event_log steps.
        var hookPortId = Guid.Parse("00000000-0000-0000-0000-00000000cafe");
        var record = new ExternalPortRecord(
            hookPortId, "hook_port", "webhook_inbox_bundle", "generic-webhook",
            null, "/hooks/webhook_inbox", "x-webhook-signature", "webhook_inbox", "none", null, Active: true);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "webhook_inbox_hook_policy", "hook_port", "webhook_inbox_bundle",
            WebhookInboxFullPolicy("sig-ok"),
            Active: true);
        var repo = new FakeRepo(record, policy);
        var eventLog = new CapturingRuntimeEventLogRepository();
        var evidence = new FakeConsumerEvidenceRepository();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: eventLog, consumerEvidenceRepository: evidence);
        var runtime = new ExternalPortDispatchRuntime(NullLogger<ExternalPortDispatchRuntime>.Instance, repo, executor);

        var response = await runtime.ExecuteAsync(NewHookRouteRequest("/hooks/webhook_inbox", "webhook_inbox", "sig-ok"), null);

        Assert.True(response.Success);
        Assert.DoesNotContain("signature_verification_failure", eventLog.EventTypes);

        // Full SSOT audit_log_boundary event chain
        Assert.Contains("signature_verification_success", eventLog.EventTypes);
        Assert.Contains("webhook_received", eventLog.EventTypes);
        Assert.Contains("intake_snapshot_created", eventLog.EventTypes);
        Assert.Contains("validation_completed", eventLog.EventTypes);
        Assert.Contains("preview_generated", eventLog.EventTypes);
        Assert.Contains("explicit_apply_initiated", eventLog.EventTypes);
        Assert.Contains("apply_completed", eventLog.EventTypes);
        Assert.Contains("scheduler_enqueued", eventLog.EventTypes);

        // Ordering: signature_verification_success before intake snapshot writes
        var types = eventLog.EventTypes.ToList();
        Assert.True(types.IndexOf("signature_verification_success") < types.IndexOf("webhook_received"),
            "signature_verification_success must precede webhook_received (intake_snapshot_write_after_signature_verification).");
        Assert.True(types.IndexOf("webhook_received") < types.IndexOf("intake_snapshot_created"),
            "webhook_received must precede intake_snapshot_created.");
        Assert.True(types.IndexOf("preview_generated") < types.IndexOf("explicit_apply_initiated"),
            "preview_generated must precede explicit_apply_initiated.");
    }

    [Fact]
    public async Task WebhookInboxPortConsumer_SchedulerAlignAndDispatch_SuccessPath_LogsFullAuditChain()
    {
        // Proves Program.cs path: AlignAndDispatchAsync → scheduler → ManifestDispatcher → ExternalPortDispatchRuntime.
        // AlignAndDispatchAsync is synchronous (waits for result), enabling explicit rejection on failure.
        var hookPortId = Guid.Parse("00000000-0000-0000-0000-00000000cafe");
        var record = new ExternalPortRecord(
            hookPortId, "hook_port", "webhook_inbox_bundle", "generic-webhook",
            null, "/hooks/webhook_inbox", "x-webhook-signature", "webhook_inbox", "none", null, Active: true);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "webhook_inbox_hook_policy", "hook_port", "webhook_inbox_bundle",
            WebhookInboxFullPolicy("sig-ok"),
            Active: true);

        var eventLog = new CapturingRuntimeEventLogRepository();
        var evidence = new FakeConsumerEvidenceRepository();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: eventLog, consumerEvidenceRepository: evidence);
        var externalPortRuntime = new ExternalPortDispatchRuntime(
            NullLogger<ExternalPortDispatchRuntime>.Instance,
            new FakeRepo(record, policy),
            executor);

        var scheduler = BuildSchedulerWithExternalPortRuntime(externalPortRuntime);

        var hookPayload = JsonSerializer.SerializeToElement(new
        {
            hook_path = "/hooks/webhook_inbox",
            route_key = "webhook_inbox",
            signature_input = new { signature = "sig-ok" },
            dispatch_payload = new { event_id = "evt-chain-test" }
        });
        var hookRequest = new EndpointRequestDto(
            "dispatchExternalPort", "external_port", "external_port", "dispatchExternalPort",
            null, hookPayload, null, "hook");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await scheduler.StartAsync(cts.Token);
        try
        {
            var result = await scheduler.AlignAndDispatchAsync(hookRequest, cts.Token);

            Assert.True(result.Success);
            Assert.Contains("signature_verification_success", eventLog.EventTypes);
            Assert.Contains("webhook_received", eventLog.EventTypes);
            Assert.Contains("intake_snapshot_created", eventLog.EventTypes);
            Assert.Contains("validation_completed", eventLog.EventTypes);
            Assert.Contains("preview_generated", eventLog.EventTypes);
            Assert.Contains("explicit_apply_initiated", eventLog.EventTypes);
            Assert.Contains("apply_completed", eventLog.EventTypes);
            Assert.Contains("scheduler_enqueued", eventLog.EventTypes);
            Assert.DoesNotContain("signature_verification_failure", eventLog.EventTypes);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WebhookInboxPortConsumer_SchedulerAlignAndDispatch_SignatureFailure_ReturnsExplicitRejection()
    {
        // Proves SSOT failure_policy.on_signature_verification_failure: reject_webhook_explicitly.
        // AlignAndDispatchAsync returns the runtime result synchronously — caller receives explicit error.
        var hookPortId = Guid.Parse("00000000-0000-0000-0000-00000000cafe");
        var record = new ExternalPortRecord(
            hookPortId, "hook_port", "webhook_inbox_bundle", "generic-webhook",
            null, "/hooks/webhook_inbox", "x-webhook-signature", "webhook_inbox", "none", null, Active: true);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "webhook_inbox_hook_policy", "hook_port", "webhook_inbox_bundle",
            [
                NewStep(1, "resolve_port_record"),
                NewStep(2, "resolve_credential_reference"),
                NewStep(3, "verify_signature_by_config", new Dictionary<string, string> { ["expected_signature"] = "expected-sig" }),
                NewStep(4, "enqueue_scheduler_event"),
            ],
            Active: true);

        var eventLog = new CapturingRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: eventLog);
        var externalPortRuntime = new ExternalPortDispatchRuntime(
            NullLogger<ExternalPortDispatchRuntime>.Instance,
            new FakeRepo(record, policy),
            executor,
            null,
            eventLog);

        var scheduler = BuildSchedulerWithExternalPortRuntime(externalPortRuntime);

        var hookPayload = JsonSerializer.SerializeToElement(new
        {
            hook_path = "/hooks/webhook_inbox",
            route_key = "webhook_inbox",
            signature_input = new { signature = "wrong-sig" },
            dispatch_payload = new { event_id = "evt-reject-test" }
        });
        var hookRequest = new EndpointRequestDto(
            "dispatchExternalPort", "external_port", "external_port", "dispatchExternalPort",
            null, hookPayload, null, "hook");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await scheduler.StartAsync(cts.Token);
        try
        {
            var result = await scheduler.AlignAndDispatchAsync(hookRequest, cts.Token);

            // Explicit rejection returned to caller — not silent 202
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Code == "EXTERNAL_SIGNATURE_VERIFICATION_FAILED");
            // Failure recorded in event log; no scheduler enqueue
            Assert.Contains("signature_verification_failure", eventLog.EventTypes);
            Assert.DoesNotContain("scheduler_enqueued", eventLog.EventTypes);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WebhookInboxPortConsumer_SsePayload_ExcludesRawWebhookPayloadAndCredentials()
    {
        var hookPortId = Guid.Parse("00000000-0000-0000-0000-00000000cafe");
        var record = new ExternalPortRecord(
            hookPortId, "hook_port", "webhook_inbox_bundle", "generic-webhook",
            null, "/hooks/webhook_inbox", "x-webhook-signature", "webhook_inbox", "none", null, Active: true);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "webhook_inbox_hook_policy", "hook_port", "webhook_inbox_bundle",
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

        var sensitivePayload = JsonSerializer.SerializeToElement(new
        {
            hook_path = "/hooks/webhook_inbox",
            route_key = "webhook_inbox",
            signature_input = new { signature = "sig-ok" },
            dispatch_payload = new
            {
                event_id = "evt-sse-test",
                raw_secret = "sk_live_must_not_appear_in_sse",
                webhook_token = "whsec_sensitive_value"
            }
        });
        var sensitiveRequest = new EndpointRequestDto(
            "dispatchExternalPort", "external_port", "external_port", "dispatchExternalPort",
            null, sensitivePayload, null, "webhook");
        var response = await runtime.ExecuteAsync(sensitiveRequest, null);

        Assert.True(response.Success);
        Assert.True(subscription.Reader.TryRead(out var evt));
        Assert.Equal("external_port_dispatch", evt.EventType);
        Assert.DoesNotContain("sk_live_must_not_appear_in_sse", evt.Data, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("whsec_sensitive_value", evt.Data, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw_secret", evt.Data, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WebhookInboxPortConsumer_CredentialVaultPath_VerifiesSignatureFromTokenHash()
    {
        // Proves the credential-vault path: step_config={} for verify_signature_by_config is valid
        // when resolve_credential_reference has populated context.CredentialVaultRecord.TokenHash.
        // This is the real-DB path: seed has verify_signature_by_config step_config={} and the
        // expected value comes from vault:ref:webhook_inbox_signing_key resolved at runtime.
        var hookPortId = Guid.Parse("00000000-0000-0000-0000-00000000dcba");
        var record = new ExternalPortRecord(
            hookPortId, "hook_port", "webhook_inbox_bundle", "generic-webhook",
            null, "/hooks/webhook_inbox", "x-webhook-signature", "webhook_inbox",
            "vault_ref", "webhook_inbox_signing_key", Active: true);
        var vaultRecord = new ExternalCredentialVaultRecord(
            Guid.NewGuid(), "generic-webhook", "webhook_inbox_bundle", "signing_key",
            TokenHash: "vault-signing-key",
            EncryptedPayload: null, EncryptionKeyReference: null, ExpiresAt: null,
            RefreshBeforeSeconds: 0, Version: 1, LockedUntil: null, Active: true);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "webhook_inbox_hook_policy", "hook_port", "webhook_inbox_bundle",
            [
                NewStep(1, "resolve_port_record"),
                NewStep(2, "resolve_credential_reference"),
                NewStep(3, "verify_signature_by_config"),   // step_config={} — expected comes from vault TokenHash
                NewStep(4, "enqueue_scheduler_event"),
            ],
            Active: true);

        var resolver = new FakeCredentialReferenceResolver(vaultRecord);
        var eventLog = new CapturingRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor(
            credentialReferenceResolver: resolver,
            runtimeEventLogRepository: eventLog);
        var runtime = new ExternalPortDispatchRuntime(
            NullLogger<ExternalPortDispatchRuntime>.Instance,
            new FakeRepo(record, policy),
            executor);

        var payload = JsonSerializer.SerializeToElement(new
        {
            hook_path = "/hooks/webhook_inbox",
            route_key = "webhook_inbox",
            signature_input = new { signature = "vault-signing-key" },
            dispatch_payload = new { event_id = "evt-vault-test" }
        });
        var request = new EndpointRequestDto(
            "dispatchExternalPort", "external_port", "external_port", "dispatchExternalPort",
            null, payload, null, "hook");

        var result = await runtime.ExecuteAsync(request, null);

        Assert.True(result.Success, $"Expected success but got: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        Assert.DoesNotContain(result.Errors, e => e.Code == "EXTERNAL_SIGNATURE_CONFIG_MISSING");
        Assert.DoesNotContain(result.Errors, e => e.Code == "EXTERNAL_SIGNATURE_VERIFICATION_FAILED");
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

    private sealed class CapturingRuntimeEventLogRepository : IExternalPortRuntimeEventLogRepository
    {
        private readonly List<string> _eventTypes = new();
        public IReadOnlyList<string> EventTypes => _eventTypes;
        public Task AppendAsync(string eventType, string? entityId, string? requiredByBundle, CancellationToken ct = default)
        {
            _eventTypes.Add(eventType);
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

    private static ExternalPortPolicyStep[] WebhookInboxFullPolicy(string expectedSignature) =>
    [
        NewStep(1, "resolve_port_record"),
        NewStep(2, "resolve_credential_reference"),
        NewStep(3, "verify_signature_by_config", new Dictionary<string, string> { ["expected_signature"] = expectedSignature }),
        NewStep(4, "append_runtime_event_log", new Dictionary<string, string>
        {
            ["event_type"] = "signature_verification_success",
            ["evidence_table_ref"] = "topology.signature_verification_evidence",
            ["projection_table_ref"] = "topology.signature_verification_evidence",
            ["status_value"] = "verified"
        }),
        NewStep(5, "append_runtime_event_log", new Dictionary<string, string>
        {
            ["event_type"] = "webhook_received",
            ["evidence_table_ref"] = "topology.webhook_intake_snapshots",
            ["projection_table_ref"] = "topology.webhook_intake_snapshots",
            ["status_value"] = "received"
        }),
        NewStep(6, "append_runtime_event_log", new Dictionary<string, string>
        {
            ["event_type"] = "intake_snapshot_created",
            ["evidence_table_ref"] = "topology.webhook_intake_snapshots",
            ["status_value"] = "snapshot_created"
        }),
        NewStep(7, "append_runtime_event_log", new Dictionary<string, string>
        {
            ["event_type"] = "validation_completed",
            ["evidence_table_ref"] = "topology.webhook_intake_snapshots",
            ["status_value"] = "validated"
        }),
        NewStep(8, "append_runtime_event_log", new Dictionary<string, string>
        {
            ["event_type"] = "preview_generated",
            ["evidence_table_ref"] = "topology.webhook_intake_snapshots",
            ["status_value"] = "preview_ready"
        }),
        NewStep(9, "enqueue_scheduler_event"),
        NewStep(10, "append_runtime_event_log", new Dictionary<string, string>
        {
            ["event_type"] = "explicit_apply_initiated",
            ["evidence_table_ref"] = "topology.webhook_intake_snapshots",
            ["status_value"] = "apply_initiated"
        }),
        NewStep(11, "append_runtime_event_log", new Dictionary<string, string>
        {
            ["event_type"] = "apply_completed",
            ["evidence_table_ref"] = "topology.webhook_intake_snapshots",
            ["status_value"] = "apply_completed"
        }),
        NewStep(12, "append_runtime_event_log", new Dictionary<string, string>
        {
            ["event_type"] = "scheduler_enqueued",
            ["evidence_table_ref"] = "topology.webhook_intake_snapshots",
            ["projection_table_ref"] = "topology.webhook_intake_snapshots",
            ["status_value"] = "scheduler_enqueued"
        }),
    ];

    private static RuntimeTimelineScheduler BuildSchedulerWithExternalPortRuntime(ExternalPortDispatchRuntime externalPortRuntime)
    {
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = externalPortRuntime,
            ["external_port_runtime"]      = externalPortRuntime,
            ["admin_runtime"]              = new NullRuntime(),
            ["sse_projection_runtime"]     = new NullRuntime(),
        };

        var topologyRepo    = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var contextRouteRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var uiTopoRepo      = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var topoVectorRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, contextRouteRepo);
        var adminRuntime    = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            contextRouteRepo,
            new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topologyRepo, topoVectorRuntime),
            new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiTopoRepo),
            uiTopoRepo);
        var targetOverride  = new TargetDispatchOverride(NullLogger<TargetDispatchOverride>.Instance, adminRuntime);
        var dispatcher      = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            targetOverride,
            manifestRepository: null);

        return new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
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

    private sealed class FakeCredentialReferenceResolver : IExternalPortCredentialReferenceResolver
    {
        private readonly ExternalCredentialVaultRecord _record;
        public FakeCredentialReferenceResolver(ExternalCredentialVaultRecord record) => _record = record;
        public Task<ExternalCredentialVaultRecord?> ResolveCredentialReferenceAsync(ExternalPortRecord portRecord, CancellationToken ct = default)
            => Task.FromResult<ExternalCredentialVaultRecord?>(_record);
    }

    private sealed class NullRuntime : IDispatchableRuntime
    {
        public Task<EndpointResponseDto> ExecuteAsync(EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default) =>
            Task.FromResult(new EndpointResponseDto(Success: true, Emission: null, Errors: []));
    }
}
