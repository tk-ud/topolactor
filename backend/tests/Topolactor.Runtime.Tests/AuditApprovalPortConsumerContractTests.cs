using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Contract tests for the audit_approval_bundle port consumer implementation.
///
/// Covers:
///   - ResolveExternalContext: response_port_ref (opaque port UUID ref) and
///     notification_status (derived from HttpResponse after send_http/capture_response)
///   - af13 (record_request): payload → idempotency_key / approval_subject / requester;
///     external_context → response_port_ref (optional)
///   - af14 (record_grant_evidence): constant → event_type='approval_granted', approval_status='granted'
///   - af15 (record_reject_evidence): constant → event_type='approval_rejected', approval_status='rejected'
///   - af16 (record_notification_evidence): external_context → notification_status after HTTP
///   - projection_deny_keys: credential / approval_secret_token / raw_provider_response blocked
///   - runtime_event_log: approval_requested / approval_granted / approval_rejected / approval_reviewed
///
/// Invariants:
///   - Approval authority is UI/human explicit action only (no autonomous or implicit approval).
///   - No notification provider-specific branching in runtime code.
///   - credential and raw_provider_response are never projected.
/// </summary>
public class AuditApprovalPortConsumerContractTests
{
    // -----------------------------------------------------------------------
    // ResolveExternalContext: response_port_ref
    // -----------------------------------------------------------------------

    [Fact]
    public void ResolveExternalContext_ResponsePortRef_ReturnsPortIdAsString()
    {
        var portId = Guid.NewGuid();
        var portRecord = MakePortRecord(portId);
        var extCtx = new ExternalPortExecutionContext { PortRecord = portRecord };
        var context = new AbstractFunctionExecutionContext("audit_approval_bundle", null, extCtx);
        var binding = new AbstractFunctionInputBinding("response_port_ref", "external_context", "response_port_ref", false, false);

        var result = context.ResolveBinding(binding);

        Assert.Equal(portId.ToString(), result);
    }

    [Fact]
    public void ResolveExternalContext_ResponsePortRef_ReturnsNull_WhenNoPortRecord()
    {
        var extCtx = new ExternalPortExecutionContext { PortRecord = null };
        var context = new AbstractFunctionExecutionContext("audit_approval_bundle", null, extCtx);
        var binding = new AbstractFunctionInputBinding("response_port_ref", "external_context", "response_port_ref", false, false);

        var result = context.ResolveBinding(binding);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveExternalContext_ResponsePortRef_ReturnsNull_WhenNoExternalPortContext()
    {
        var context = new AbstractFunctionExecutionContext("audit_approval_bundle", null, null);
        var binding = new AbstractFunctionInputBinding("response_port_ref", "external_context", "response_port_ref", false, false);

        var result = context.ResolveBinding(binding);

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // ResolveExternalContext: notification_status
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(200, "sent")]
    [InlineData(201, "sent")]
    [InlineData(204, "sent")]
    [InlineData(299, "sent")]
    public void ResolveExternalContext_NotificationStatus_ReturnsSent_For2xxResponse(int statusCode, string expected)
    {
        var extCtx = new ExternalPortExecutionContext
        {
            HttpResponse = new ExternalPortHttpResponse(statusCode, "{}")
        };
        var context = new AbstractFunctionExecutionContext("audit_approval_bundle", null, extCtx);
        var binding = new AbstractFunctionInputBinding("notification_status", "external_context", "notification_status", true, false);

        var result = context.ResolveBinding(binding);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(300)]
    public void ResolveExternalContext_NotificationStatus_ReturnsFailed_ForNon2xxResponse(int statusCode)
    {
        var extCtx = new ExternalPortExecutionContext
        {
            HttpResponse = new ExternalPortHttpResponse(statusCode, "error")
        };
        var context = new AbstractFunctionExecutionContext("audit_approval_bundle", null, extCtx);
        var binding = new AbstractFunctionInputBinding("notification_status", "external_context", "notification_status", true, false);

        var result = context.ResolveBinding(binding);

        Assert.Equal("failed", result);
    }

    [Fact]
    public void ResolveExternalContext_NotificationStatus_ReturnsNull_WhenNoHttpResponse()
    {
        var extCtx = new ExternalPortExecutionContext { HttpResponse = null };
        var context = new AbstractFunctionExecutionContext("audit_approval_bundle", null, extCtx);
        var binding = new AbstractFunctionInputBinding("notification_status", "external_context", "notification_status", false, false);

        var result = context.ResolveBinding(binding);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveExternalContext_NotificationStatus_ReturnsNull_WhenNoExternalPortContext()
    {
        var context = new AbstractFunctionExecutionContext("audit_approval_bundle", null, null);
        var binding = new AbstractFunctionInputBinding("notification_status", "external_context", "notification_status", false, false);

        var result = context.ResolveBinding(binding);

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // af13: record_request — payload bindings + optional response_port_ref
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Af13_RecordRequest_ResolvesPayloadBindings()
    {
        using var doc = JsonDocument.Parse("""
            {"idempotency_key":"key-abc","approval_subject":"export_job_42","requester":"alice"}
            """);
        var captured = new CapturedCallArgs();
        var portId = Guid.NewGuid();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "audit_approval_bundle",
            PortRecord = MakePortRecord(portId),
            Policy = MakePolicy("audit_approval_response_port_generic")
        };
        var manifest = MakeRecordRequestManifest();
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[]
        {
            new CapturingPostgresFunctionAdapter(captured, Guid.NewGuid()),
            new ProjectionPrimitiveAdapter()
        });

        await executor.ExecuteAsync("audit_approval.record_request",
            new AbstractFunctionExecutionContext("audit_approval_bundle", doc.RootElement, extCtx));

        Assert.Equal("key-abc", captured.Args["idempotency_key"]);
        Assert.Equal("export_job_42", captured.Args["approval_subject"]);
        Assert.Equal("alice", captured.Args["requester"]);
        Assert.Equal(portId.ToString(), captured.Args["response_port_ref"]);
    }

    [Fact]
    public async Task Af13_RecordRequest_AcceptsNullResponsePortRef_WhenNoPortRecord()
    {
        using var doc = JsonDocument.Parse("""
            {"idempotency_key":"key-xyz","approval_subject":"item","requester":"bob"}
            """);
        var captured = new CapturedCallArgs();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "audit_approval_bundle",
            PortRecord = null,
            Policy = MakePolicy("audit_approval_response_port_generic")
        };
        var manifest = MakeRecordRequestManifest();
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[]
        {
            new CapturingPostgresFunctionAdapter(captured, Guid.NewGuid()),
            new ProjectionPrimitiveAdapter()
        });

        await executor.ExecuteAsync("audit_approval.record_request",
            new AbstractFunctionExecutionContext("audit_approval_bundle", doc.RootElement, extCtx));

        Assert.Null(captured.Args["response_port_ref"]);
    }

    // -----------------------------------------------------------------------
    // af14: record_grant_evidence — constant bindings for event_type / approval_status
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Af14_RecordGrantEvidence_ConstantBindings_EventTypeGranted_StatusGranted()
    {
        using var doc = JsonDocument.Parse("""{"approval_request_id":"00000000-0000-0000-0000-000000000001"}""");
        var captured = new CapturedCallArgs();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "audit_approval_bundle",
            Policy = MakePolicy("audit_approval_grant_action_generic")
        };
        var manifest = MakeGrantEvidenceManifest();
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[]
        {
            new CapturingPostgresFunctionAdapter(captured, Guid.NewGuid()),
            new ProjectionPrimitiveAdapter()
        });

        await executor.ExecuteAsync("audit_approval.record_grant_evidence",
            new AbstractFunctionExecutionContext("audit_approval_bundle", doc.RootElement, extCtx));

        Assert.Equal("approval_granted", captured.Args["event_type"]);
        Assert.Equal("granted", captured.Args["approval_status"]);
    }

    // -----------------------------------------------------------------------
    // af15: record_reject_evidence — constant bindings for event_type / approval_status
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Af15_RecordRejectEvidence_ConstantBindings_EventTypeRejected_StatusRejected()
    {
        using var doc = JsonDocument.Parse("""{"approval_request_id":"00000000-0000-0000-0000-000000000001"}""");
        var captured = new CapturedCallArgs();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "audit_approval_bundle",
            Policy = MakePolicy("audit_approval_reject_action_generic")
        };
        var manifest = MakeRejectEvidenceManifest();
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[]
        {
            new CapturingPostgresFunctionAdapter(captured, Guid.NewGuid()),
            new ProjectionPrimitiveAdapter()
        });

        await executor.ExecuteAsync("audit_approval.record_reject_evidence",
            new AbstractFunctionExecutionContext("audit_approval_bundle", doc.RootElement, extCtx));

        Assert.Equal("approval_rejected", captured.Args["event_type"]);
        Assert.Equal("rejected", captured.Args["approval_status"]);
    }

    [Fact]
    public void Af14_And_Af15_HaveDifferentConstantValues()
    {
        var grantManifest = MakeGrantEvidenceManifest();
        var rejectManifest = MakeRejectEvidenceManifest();

        var grantEventTypeBinding = grantManifest.Steps[0].InputBindings.First(b => b.InputKey == "event_type");
        var rejectEventTypeBinding = rejectManifest.Steps[0].InputBindings.First(b => b.InputKey == "event_type");

        Assert.Equal("approval_granted", grantEventTypeBinding.BindingPath);
        Assert.Equal("approval_rejected", rejectEventTypeBinding.BindingPath);
        Assert.NotEqual(grantEventTypeBinding.BindingPath, rejectEventTypeBinding.BindingPath);
    }

    // -----------------------------------------------------------------------
    // af16: record_notification_evidence — notification_status from external_context
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Af16_RecordNotificationEvidence_ResolvesNotificationStatusFromHttpResponse()
    {
        using var doc = JsonDocument.Parse("""{"approval_request_id":"00000000-0000-0000-0000-000000000002"}""");
        var captured = new CapturedCallArgs();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "audit_approval_bundle",
            HttpResponse = new ExternalPortHttpResponse(200, "{}"),
            Policy = MakePolicy("audit_approval_response_port_generic")
        };
        var manifest = MakeNotificationEvidenceManifest();
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[]
        {
            new CapturingPostgresFunctionAdapter(captured, Guid.NewGuid())
        });

        await executor.ExecuteAsync("audit_approval.record_notification_evidence",
            new AbstractFunctionExecutionContext("audit_approval_bundle", doc.RootElement, extCtx));

        Assert.Equal("sent", captured.Args["notification_status"]);
    }

    [Fact]
    public async Task Af16_RecordNotificationEvidence_ReturnsFailedStatus_ForNon2xxResponse()
    {
        using var doc = JsonDocument.Parse("""{"approval_request_id":"00000000-0000-0000-0000-000000000002"}""");
        var captured = new CapturedCallArgs();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "audit_approval_bundle",
            HttpResponse = new ExternalPortHttpResponse(503, "error"),
            Policy = MakePolicy("audit_approval_response_port_generic")
        };
        var manifest = MakeNotificationEvidenceManifest();
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[]
        {
            new CapturingPostgresFunctionAdapter(captured, Guid.NewGuid())
        });

        await executor.ExecuteAsync("audit_approval.record_notification_evidence",
            new AbstractFunctionExecutionContext("audit_approval_bundle", doc.RootElement, extCtx));

        Assert.Equal("failed", captured.Args["notification_status"]);
    }

    // -----------------------------------------------------------------------
    // projection_deny_keys: credential / approval_secret_token / raw_provider_response blocked
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("credential")]
    [InlineData("approval_secret_token")]
    [InlineData("raw_provider_response")]
    public async Task ProjectionDenyKeys_AreBlockedFromProjectionOutput(string deniedKey)
    {
        using var doc = JsonDocument.Parse($"{{\"{deniedKey}\":\"should-be-blocked\"}}");
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "audit_approval.test_deny", "external_port_runtime", "audit_approval_bundle",
            new[]
            {
                Step(1, "projection",
                    new[] { new AbstractFunctionInputBinding(deniedKey, "payload", deniedKey, false, false) },
                    "result")
            },
            new[] { "credential", "approval_secret_token", "raw_provider_response" },
            true,
            new AbstractFunctionAuthorityBinding[] { new("policy", "audit_approval_response_port_generic", true), new("table", "topology.audit_approval_requests", true) });

        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[] { new ProjectionPrimitiveAdapter() });
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "audit_approval_bundle",
            Policy = MakePolicy("audit_approval_response_port_generic")
        };

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("audit_approval.test_deny",
                new AbstractFunctionExecutionContext("audit_approval_bundle", doc.RootElement, extCtx)));

        Assert.Equal(AbstractFunctionFailCloseStatus.SecretProjectionDenied, ex.Status);
    }

    // -----------------------------------------------------------------------
    // runtime_event_log: approval event types are valid string constants
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RuntimeEventLog_ApprovalRequested_AppendsCorrectEventType()
    {
        var repo = new CapturingEventLogRepository();
        var adapter = new EventLogPrimitiveAdapter(repo);
        var step = MakeStep(new Dictionary<string, string> { ["event_type"] = "approval_requested" });
        var extCtx = new ExternalPortExecutionContext { RequiredByBundle = "audit_approval_bundle" };
        var context = new AbstractFunctionExecutionContext("audit_approval_bundle", null, extCtx);

        await adapter.ExecuteAsync(step, Empty, context);

        Assert.Single(repo.Calls);
        Assert.Equal("approval_requested", repo.Calls[0].EventType);
        Assert.Equal("audit_approval_bundle", repo.Calls[0].Bundle);
    }

    [Theory]
    [InlineData("approval_granted")]
    [InlineData("approval_rejected")]
    [InlineData("approval_reviewed")]
    public async Task RuntimeEventLog_ApprovalEventTypes_AppendWithCorrectBundle(string eventType)
    {
        var repo = new CapturingEventLogRepository();
        var adapter = new EventLogPrimitiveAdapter(repo);
        var step = MakeStep(new Dictionary<string, string> { ["event_type"] = eventType });
        var extCtx = new ExternalPortExecutionContext { RequiredByBundle = "audit_approval_bundle" };
        var context = new AbstractFunctionExecutionContext("audit_approval_bundle", null, extCtx);

        await adapter.ExecuteAsync(step, Empty, context);

        Assert.Equal(eventType, repo.Calls[0].EventType);
        Assert.Equal("audit_approval_bundle", repo.Calls[0].Bundle);
    }

    // -----------------------------------------------------------------------
    // Prohibited: bundle-specific branching not present in AbstractFunctionRuntime
    // -----------------------------------------------------------------------

    [Fact]
    public void AbstractFunctionRuntime_DoesNotContainBundleSpecificBranching()
    {
        var source = File.ReadAllText(Path.Combine("..", "..", "..", "..", "..", "runtime", "AbstractFunctionRuntime.cs"));
        Assert.DoesNotContain("audit_approval", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("approval_bundle", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("functionKey switch", source, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Manifest factories
    // -----------------------------------------------------------------------

    private static AbstractFunctionManifest MakeRecordRequestManifest() =>
        new(Guid.NewGuid(), "audit_approval.record_request", "external_port_runtime", "audit_approval_bundle",
            new[]
            {
                Step(1, "stub_postgres",
                    new[]
                    {
                        new AbstractFunctionInputBinding("idempotency_key",  "payload",          "idempotency_key",   true,  false),
                        new AbstractFunctionInputBinding("approval_subject",  "payload",          "approval_subject",  true,  false),
                        new AbstractFunctionInputBinding("requester",          "payload",          "requester",         true,  false),
                        new AbstractFunctionInputBinding("response_port_ref",  "external_context", "response_port_ref", false, false)
                    },
                    "ApprovalRequestId",
                    new Dictionary<string, string> { ["function"] = "topology.aa_record_approval_request", ["required_table_authority"] = "topology.audit_approval_requests" }),
                Step(2, "projection",
                    new[] { new AbstractFunctionInputBinding("approval_request_id", "result_context", "ApprovalRequestId", true, false) },
                    "OutputProp")
            },
            new[] { "credential", "approval_secret_token", "raw_provider_response" },
            true,
            new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "audit_approval_response_port_generic", true),
                new("table", "topology.audit_approval_requests", true)
            },
            new Dictionary<string, string> { ["step1_result"] = "ApprovalRequestId", ["step2_result"] = "OutputProp" });

    private static AbstractFunctionManifest MakeGrantEvidenceManifest() =>
        new(Guid.NewGuid(), "audit_approval.record_grant_evidence", "external_port_runtime", "audit_approval_bundle",
            new[]
            {
                Step(1, "stub_postgres",
                    new[]
                    {
                        new AbstractFunctionInputBinding("approval_request_id", "payload",   "approval_request_id", true,  false),
                        new AbstractFunctionInputBinding("event_type",           "constant",  "approval_granted",    true,  false),
                        new AbstractFunctionInputBinding("approval_status",      "constant",  "granted",             true,  false)
                    },
                    "ApprovalEvidenceId",
                    new Dictionary<string, string> { ["function"] = "topology.aa_record_approval_evidence", ["required_table_authority"] = "topology.audit_approval_evidence" }),
                Step(2, "projection",
                    new[] { new AbstractFunctionInputBinding("approval_evidence_id", "result_context", "ApprovalEvidenceId", true, false) },
                    "OutputProp")
            },
            new[] { "credential", "approval_secret_token", "raw_provider_response" },
            true,
            new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "audit_approval_grant_action_generic", true),
                new("table", "topology.audit_approval_evidence", true)
            },
            new Dictionary<string, string> { ["step1_result"] = "ApprovalEvidenceId", ["step2_result"] = "OutputProp" });

    private static AbstractFunctionManifest MakeRejectEvidenceManifest() =>
        new(Guid.NewGuid(), "audit_approval.record_reject_evidence", "external_port_runtime", "audit_approval_bundle",
            new[]
            {
                Step(1, "stub_postgres",
                    new[]
                    {
                        new AbstractFunctionInputBinding("approval_request_id", "payload",   "approval_request_id", true,  false),
                        new AbstractFunctionInputBinding("event_type",           "constant",  "approval_rejected",   true,  false),
                        new AbstractFunctionInputBinding("approval_status",      "constant",  "rejected",            true,  false)
                    },
                    "ApprovalEvidenceId",
                    new Dictionary<string, string> { ["function"] = "topology.aa_record_approval_evidence", ["required_table_authority"] = "topology.audit_approval_evidence" }),
                Step(2, "projection",
                    new[] { new AbstractFunctionInputBinding("approval_evidence_id", "result_context", "ApprovalEvidenceId", true, false) },
                    "OutputProp")
            },
            new[] { "credential", "approval_secret_token", "raw_provider_response" },
            true,
            new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "audit_approval_reject_action_generic", true),
                new("table", "topology.audit_approval_evidence", true)
            },
            new Dictionary<string, string> { ["step1_result"] = "ApprovalEvidenceId", ["step2_result"] = "OutputProp" });

    private static AbstractFunctionManifest MakeNotificationEvidenceManifest() =>
        new(Guid.NewGuid(), "audit_approval.record_notification_evidence", "external_port_runtime", "audit_approval_bundle",
            new[]
            {
                Step(1, "stub_postgres",
                    new[]
                    {
                        new AbstractFunctionInputBinding("approval_request_id", "payload",          "approval_request_id", true,  false),
                        new AbstractFunctionInputBinding("notification_status",  "external_context", "notification_status", true,  false),
                        new AbstractFunctionInputBinding("response_port_ref",    "external_context", "response_port_ref",   false, false)
                    },
                    "NotificationEvidenceId",
                    new Dictionary<string, string> { ["function"] = "topology.aa_record_notification_evidence", ["required_table_authority"] = "topology.audit_notification_evidence" })
            },
            new[] { "credential", "approval_secret_token", "raw_provider_response" },
            true,
            new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "audit_approval_response_port_generic", true),
                new("table", "topology.audit_notification_evidence", true)
            },
            new Dictionary<string, string> { ["result"] = "NotificationEvidenceId" });

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    private static ExternalPortRecord MakePortRecord(Guid portId) =>
        new(portId, "response_port", "audit_approval_bundle", "notification",
            "env:APPROVAL_NOTIFICATION_ENDPOINT_REF", null, null, null,
            "external", "vault:ref:audit_approval_notification_credential", true);

    private static ExternalPortPolicy MakePolicy(string policyKey) =>
        new(Guid.NewGuid(), policyKey, "response_port", "audit_approval_bundle", Array.Empty<ExternalPortPolicyStep>(), true);

    private static AbstractFunctionStep Step(int order, string primitive, IReadOnlyList<AbstractFunctionInputBinding> bindings, string? resultKey, IReadOnlyDictionary<string, string>? stepConfig = null) =>
        new(Guid.NewGuid(), order, primitive, stepConfig ?? new Dictionary<string, string>(), bindings, resultKey, true);

    private static AbstractFunctionStep MakeStep(Dictionary<string, string> stepConfig) =>
        new(Guid.NewGuid(), 1, "test_primitive", stepConfig, Array.Empty<AbstractFunctionInputBinding>(), null, true);

    private sealed class CapturedCallArgs
    {
        public Dictionary<string, object?> Args { get; } = new(StringComparer.Ordinal);
    }

    private sealed class CapturingPostgresFunctionAdapter : IAbstractFunctionPrimitiveAdapter
    {
        private readonly CapturedCallArgs _captured;
        private readonly object? _returnValue;
        public string PrimitiveKey => "stub_postgres";
        public CapturingPostgresFunctionAdapter(CapturedCallArgs captured, object? returnValue)
        {
            _captured = captured;
            _returnValue = returnValue;
        }
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
        {
            foreach (var (k, v) in inputs) _captured.Args[k] = v;
            return Task.FromResult(_returnValue);
        }
    }

    private sealed class CapturingEventLogRepository : IExternalPortRuntimeEventLogRepository
    {
        public List<(string EventType, string? EntityId, string? Bundle)> Calls { get; } = new();
        public Task AppendAsync(string eventType, string? entityId, string? bundle, CancellationToken ct = default)
        {
            Calls.Add((eventType, entityId, bundle));
            return Task.CompletedTask;
        }
    }

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest? _manifest;
        public StaticManifestRepository(AbstractFunctionManifest? manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) => Task.FromResult(_manifest);
    }
}
