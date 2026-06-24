using System.Runtime.CompilerServices;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Contract tests for the email_bundle port consumer implementation.
///
/// Covers (per docs/design/runtime-bundle-email-ssot.yaml +
/// docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane):
///   - af40 (record_draft): payload → dispatch_idempotency_key / recipient_ref /
///     subject_ref; external_context → response_port_ref (optional)
///   - af41 (record_approval): constant → approval_status='approved'
///   - af42 (record_dispatch_initiated): payload → email_draft_id (approval gate guard)
///   - af43 (record_send_evidence): external_context → send_result (notification_status)
///   - projection_deny_keys: credential / smtp_* / raw_provider_response blocked
///   - runtime_event_log: draft_recorded / approval_recorded / dispatch_initiated /
///     send_dispatched
///   - seed lane: email send policy steps route through the generic
///     response_port → credential resolution → send_http/capture_response →
///     evidence/runtime_event_log lane; the approval-gate guard runs before send_http
///   - no SMTP provider-specific client/runtime; provider_kind is data only
///
/// Invariants:
///   - Email send authority is ui_approval_then_backend_dispatch (no autonomous /
///     implicit approval, no CLI/MCP send).
///   - No provider-specific branching in runtime code.
///   - credential / SMTP endpoint / raw provider response are never projected.
/// </summary>
public class EmailPortConsumerContractTests
{
    private const string SmtpResponsePortRef = "external-port:response_port:00000000-0000-0000-0000-000000000f03";
    private const string DraftActionPortRef = "external-port:response_port:00000000-0000-0000-0000-000000000f11";
    private const string ApprovalActionPortRef = "external-port:response_port:00000000-0000-0000-0000-000000000f12";

    // -----------------------------------------------------------------------
    // ResolveExternalContext: response_port_ref / send_result (notification_status)
    // -----------------------------------------------------------------------

    [Fact]
    public void ResolveExternalContext_ResponsePortRef_ReturnsPortIdAsString()
    {
        var portId = Guid.NewGuid();
        var extCtx = new ExternalPortExecutionContext { PortRecord = MakePortRecord(portId) };
        var context = new AbstractFunctionExecutionContext("email_bundle", null, extCtx);
        var binding = new AbstractFunctionInputBinding("response_port_ref", "external_context", "response_port_ref", false, false);

        Assert.Equal(portId.ToString(), context.ResolveBinding(binding));
    }

    [Theory]
    [InlineData(200, "sent")]
    [InlineData(202, "sent")]
    [InlineData(204, "sent")]
    public void ResolveExternalContext_SendResult_ReturnsSent_For2xx(int statusCode, string expected)
    {
        var extCtx = new ExternalPortExecutionContext { HttpResponse = new ExternalPortHttpResponse(statusCode, "{}") };
        var context = new AbstractFunctionExecutionContext("email_bundle", null, extCtx);
        var binding = new AbstractFunctionInputBinding("send_result", "external_context", "notification_status", true, false);

        Assert.Equal(expected, context.ResolveBinding(binding));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(503)]
    public void ResolveExternalContext_SendResult_ReturnsFailed_ForNon2xx(int statusCode)
    {
        var extCtx = new ExternalPortExecutionContext { HttpResponse = new ExternalPortHttpResponse(statusCode, "error") };
        var context = new AbstractFunctionExecutionContext("email_bundle", null, extCtx);
        var binding = new AbstractFunctionInputBinding("send_result", "external_context", "notification_status", true, false);

        Assert.Equal("failed", context.ResolveBinding(binding));
    }

    // -----------------------------------------------------------------------
    // af40: record_draft — payload bindings + optional response_port_ref
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Af40_RecordDraft_ResolvesPayloadBindings()
    {
        using var doc = JsonDocument.Parse("""
            {"dispatch_idempotency_key":"disp-1","recipient_ref":"rcpt-ref","subject_ref":"subj-ref"}
            """);
        var captured = new CapturedCallArgs();
        var portId = Guid.NewGuid();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "email_bundle",
            PortRecord = MakePortRecord(portId),
            Policy = MakePolicy("email_draft_action_generic")
        };
        var executor = NewExecutor(MakeRecordDraftManifest(), captured);

        await executor.ExecuteAsync("email.record_draft",
            new AbstractFunctionExecutionContext("email_bundle", doc.RootElement, extCtx));

        Assert.Equal("disp-1", captured.Args["dispatch_idempotency_key"]);
        Assert.Equal("rcpt-ref", captured.Args["recipient_ref"]);
        Assert.Equal("subj-ref", captured.Args["subject_ref"]);
        Assert.Equal(portId.ToString(), captured.Args["response_port_ref"]);
    }

    // -----------------------------------------------------------------------
    // af41: record_approval — constant binding approval_status='approved'
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Af41_RecordApproval_ConstantApprovalStatus_IsApproved()
    {
        using var doc = JsonDocument.Parse("""{"email_draft_id":"00000000-0000-0000-0000-000000000001"}""");
        var captured = new CapturedCallArgs();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "email_bundle",
            Policy = MakePolicy("email_approval_action_generic")
        };
        var executor = NewExecutor(MakeRecordApprovalManifest(), captured);

        await executor.ExecuteAsync("email.record_approval",
            new AbstractFunctionExecutionContext("email_bundle", doc.RootElement, extCtx));

        Assert.Equal("00000000-0000-0000-0000-000000000001", captured.Args["email_draft_id"]);
        Assert.Equal("approved", captured.Args["approval_status"]);
    }

    // -----------------------------------------------------------------------
    // af42: record_dispatch_initiated — approval-gate guard, payload email_draft_id
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Af42_RecordDispatchInitiated_ResolvesEmailDraftIdFromPayload()
    {
        using var doc = JsonDocument.Parse("""{"email_draft_id":"00000000-0000-0000-0000-000000000002"}""");
        var captured = new CapturedCallArgs();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "email_bundle",
            Policy = MakePolicy("email_response_port_generic")
        };
        var executor = NewExecutor(MakeDispatchInitiatedManifest(), captured);

        await executor.ExecuteAsync("email.record_dispatch_initiated",
            new AbstractFunctionExecutionContext("email_bundle", doc.RootElement, extCtx));

        Assert.Equal("00000000-0000-0000-0000-000000000002", captured.Args["email_draft_id"]);
    }

    // -----------------------------------------------------------------------
    // af43: record_send_evidence — send_result from external_context (HTTP response)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(200, "sent")]
    [InlineData(503, "failed")]
    public async Task Af43_RecordSendEvidence_ResolvesSendResultFromHttpResponse(int statusCode, string expected)
    {
        using var doc = JsonDocument.Parse("""{"email_draft_id":"00000000-0000-0000-0000-000000000003"}""");
        var captured = new CapturedCallArgs();
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "email_bundle",
            HttpResponse = new ExternalPortHttpResponse(statusCode, "{}"),
            Policy = MakePolicy("email_response_port_generic")
        };
        var executor = NewExecutor(MakeSendEvidenceManifest(), captured);

        await executor.ExecuteAsync("email.record_send_evidence",
            new AbstractFunctionExecutionContext("email_bundle", doc.RootElement, extCtx));

        Assert.Equal(expected, captured.Args["send_result"]);
    }

    // -----------------------------------------------------------------------
    // projection_deny_keys: credential / smtp_* / raw_provider_response blocked
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("credential")]
    [InlineData("smtp_host")]
    [InlineData("smtp_port")]
    [InlineData("smtp_api_key")]
    [InlineData("raw_provider_response")]
    public async Task ProjectionDenyKeys_AreBlockedFromProjectionOutput(string deniedKey)
    {
        using var doc = JsonDocument.Parse($"{{\"{deniedKey}\":\"should-be-blocked\"}}");
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "email.test_deny", "external_port_runtime", "email_bundle",
            new[]
            {
                Step(1, "projection",
                    new[] { new AbstractFunctionInputBinding(deniedKey, "payload", deniedKey, false, false) },
                    "result")
            },
            new[] { "credential", "smtp_host", "smtp_port", "smtp_api_key", "raw_provider_response", "plaintext_payload", "token" },
            true,
            new AbstractFunctionAuthorityBinding[] { new("policy", "email_response_port_generic", true), new("table", "topology.email_drafts", true) });

        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[] { new ProjectionPrimitiveAdapter() });
        var extCtx = new ExternalPortExecutionContext
        {
            RequiredByBundle = "email_bundle",
            Policy = MakePolicy("email_response_port_generic")
        };

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("email.test_deny",
                new AbstractFunctionExecutionContext("email_bundle", doc.RootElement, extCtx)));

        Assert.Equal(AbstractFunctionFailCloseStatus.SecretProjectionDenied, ex.Status);
    }

    // -----------------------------------------------------------------------
    // runtime_event_log: email event types append with the email bundle
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("draft_recorded")]
    [InlineData("approval_recorded")]
    [InlineData("dispatch_initiated")]
    [InlineData("send_dispatched")]
    public async Task RuntimeEventLog_EmailEventTypes_AppendWithEmailBundle(string eventType)
    {
        var repo = new CapturingEventLogRepository();
        var adapter = new EventLogPrimitiveAdapter(repo);
        var step = MakeStep(new Dictionary<string, string> { ["event_type"] = eventType });
        var extCtx = new ExternalPortExecutionContext { RequiredByBundle = "email_bundle" };
        var context = new AbstractFunctionExecutionContext("email_bundle", null, extCtx);

        await adapter.ExecuteAsync(step, Empty, context);

        Assert.Equal(eventType, repo.Calls[0].EventType);
        Assert.Equal("email_bundle", repo.Calls[0].Bundle);
    }

    // -----------------------------------------------------------------------
    // Prohibited: bundle-/provider-specific branching not present in runtime code
    // -----------------------------------------------------------------------

    [Fact]
    public void AbstractFunctionRuntime_DoesNotContainEmailOrSmtpBranching()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "backend", "runtime", "AbstractFunctionRuntime.cs"));
        Assert.DoesNotContain("email", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("smtp", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("functionKey switch", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DispatchRuntime_ContainsNoSmtpProviderClient()
    {
        var dispatch = File.ReadAllText(Path.Combine(RepoRoot(), "backend", "runtime", "ExternalPortDispatchRuntime.cs"));
        var executor = File.ReadAllText(Path.Combine(RepoRoot(), "backend", "runtime", "ExternalPortCredentialRefresher.cs"));
        foreach (var source in new[] { dispatch, executor })
        {
            Assert.DoesNotContain("SmtpClient", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("System.Net.Mail", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    // -----------------------------------------------------------------------
    // Seed lane contract: email send routes through the generic external-port
    // lane, with the approval-gate guard ahead of send_http.
    // -----------------------------------------------------------------------

    [Fact]
    public void SeedLane_EmailSend_RoutesThroughGenericLane_WithApprovalGateBeforeSend()
    {
        var seed = File.ReadAllText(Path.Combine(RepoRoot(), "db", "email_approval_form_preset_seed.sql"));
        // Scope ordering checks to the e6 send-lane re-seed block (after the DELETE),
        // so they reflect policy-step order rather than the manifest definitions above.
        var lane = seed[seed.IndexOf("DELETE FROM topology.external_port_policy_steps", StringComparison.Ordinal)..];

        // execute_abstract_function approval guard precedes send_http in policy e6.
        var guardIdx = lane.IndexOf("'email.record_dispatch_initiated'", StringComparison.Ordinal);
        var sendIdx = lane.IndexOf("'send_http'", StringComparison.Ordinal);
        var captureIdx = lane.IndexOf("'capture_response'", StringComparison.Ordinal);
        var sendEvidenceIdx = lane.IndexOf("'email.record_send_evidence'", StringComparison.Ordinal);
        Assert.True(guardIdx > 0, "dispatch-initiated guard must be present in send lane");
        Assert.True(sendIdx > guardIdx, "send_http must follow the approval-gate guard");
        Assert.True(sendEvidenceIdx > captureIdx && captureIdx > 0, "send evidence must follow capture_response");

        // Generic operation keys are present; the send lane uses credential resolution.
        foreach (var key in new[]
        {
            "resolve_port_record", "resolve_credential_reference", "load_encrypted_credential_payload",
            "decrypt_for_runtime_use", "build_http_request", "inject_authorization_header",
            "send_http", "capture_response", "append_runtime_event_log"
        })
        {
            Assert.Contains($"'{key}'", seed);
        }

        // portTargetRef wiring for the three UI actions.
        Assert.Contains(SmtpResponsePortRef, seed);
        Assert.Contains(DraftActionPortRef, seed);
        Assert.Contains(ApprovalActionPortRef, seed);
        Assert.Contains("physical_search_crud_aggregate.v1", seed);
        Assert.Contains("dispatchExternalPort", seed);

        // Evidence/runtime_event_log event types required by SSOT audit_log_boundary.
        foreach (var evt in new[] { "approval_recorded", "dispatch_initiated", "send_success", "send_failure" })
            Assert.Contains(evt, seed);

        // No provider-specific SMTP client and no plaintext SMTP endpoint literal.
        Assert.DoesNotContain("SmtpClient", seed);
        Assert.DoesNotContain("smtp://", seed);
    }

    [Fact]
    public void SeedLane_ProjectionForbidsCredentialAndSmtpAndRawResponse()
    {
        var seed = File.ReadAllText(Path.Combine(RepoRoot(), "db", "email_approval_form_preset_seed.sql"));
        foreach (var key in new[] { "credential", "smtp_host", "smtp_port", "smtp_api_key", "raw_provider_response" })
            Assert.Contains(key, seed);
        Assert.Contains("forbiddenProjectionFields", seed);
    }

    [Fact]
    public void SeedLane_AbstractFunctionAuthority_IsManifestDefined_NotPayloadDerived()
    {
        var seed = File.ReadAllText(Path.Combine(RepoRoot(), "db", "email_approval_form_preset_seed.sql"));
        // call_postgres_function steps declare required_table_authority (manifest authority),
        // and authority bindings reference topology.email_* tables — never payload-derived.
        Assert.Contains("\"required_table_authority\":\"topology.email_drafts\"", seed);
        Assert.Contains("\"required_table_authority\":\"topology.email_delivery_evidence\"", seed);
        Assert.Contains("abstract_function_authority_bindings", seed);
        Assert.Contains("'table',  'topology.email_drafts'", seed);
    }

    // -----------------------------------------------------------------------
    // Manifest factories (stub_postgres primitive captures call arguments)
    // -----------------------------------------------------------------------

    private static AbstractFunctionManifest MakeRecordDraftManifest() =>
        new(Guid.NewGuid(), "email.record_draft", "external_port_runtime", "email_bundle",
            new[]
            {
                Step(1, "stub_postgres",
                    new[]
                    {
                        new AbstractFunctionInputBinding("dispatch_idempotency_key", "payload",          "dispatch_idempotency_key", true,  false),
                        new AbstractFunctionInputBinding("recipient_ref",            "payload",          "recipient_ref",            true,  false),
                        new AbstractFunctionInputBinding("subject_ref",              "payload",          "subject_ref",              true,  false),
                        new AbstractFunctionInputBinding("response_port_ref",        "external_context", "response_port_ref",        false, false)
                    },
                    "EmailDraftId",
                    new Dictionary<string, string> { ["function"] = "topology.em_record_draft", ["required_table_authority"] = "topology.email_drafts" }),
                Step(2, "projection",
                    new[] { new AbstractFunctionInputBinding("email_draft_id", "result_context", "EmailDraftId", true, false) },
                    "OutputProp")
            },
            EmailDenyKeys, true,
            EmailAuthority("email_draft_action_generic", "topology.email_drafts"),
            new Dictionary<string, string> { ["step1_result"] = "EmailDraftId", ["step2_result"] = "OutputProp" });

    private static AbstractFunctionManifest MakeRecordApprovalManifest() =>
        new(Guid.NewGuid(), "email.record_approval", "external_port_runtime", "email_bundle",
            new[]
            {
                Step(1, "stub_postgres",
                    new[]
                    {
                        new AbstractFunctionInputBinding("email_draft_id",  "payload",  "email_draft_id",  true,  false),
                        new AbstractFunctionInputBinding("approval_status", "constant", "approved",        true,  false),
                        new AbstractFunctionInputBinding("reviewed_by",     "payload",  "reviewed_by",     false, false)
                    },
                    "ApprovalRecordId",
                    new Dictionary<string, string> { ["function"] = "topology.em_record_approval", ["required_table_authority"] = "topology.email_approval_records" }),
                Step(2, "projection",
                    new[] { new AbstractFunctionInputBinding("approval_record_id", "result_context", "ApprovalRecordId", true, false) },
                    "OutputProp")
            },
            EmailDenyKeys, true,
            EmailAuthority("email_approval_action_generic", "topology.email_approval_records"),
            new Dictionary<string, string> { ["step1_result"] = "ApprovalRecordId", ["step2_result"] = "OutputProp" });

    private static AbstractFunctionManifest MakeDispatchInitiatedManifest() =>
        new(Guid.NewGuid(), "email.record_dispatch_initiated", "external_port_runtime", "email_bundle",
            new[]
            {
                Step(1, "stub_postgres",
                    new[] { new AbstractFunctionInputBinding("email_draft_id", "payload", "email_draft_id", true, false) },
                    "DispatchInitiatedId",
                    new Dictionary<string, string> { ["function"] = "topology.em_record_dispatch_initiated", ["required_table_authority"] = "topology.email_drafts" })
            },
            EmailDenyKeys, true,
            EmailAuthority("email_response_port_generic", "topology.email_drafts"),
            new Dictionary<string, string> { ["result"] = "DispatchInitiatedId" });

    private static AbstractFunctionManifest MakeSendEvidenceManifest() =>
        new(Guid.NewGuid(), "email.record_send_evidence", "external_port_runtime", "email_bundle",
            new[]
            {
                Step(1, "stub_postgres",
                    new[]
                    {
                        new AbstractFunctionInputBinding("email_draft_id", "payload",          "email_draft_id",      true, false),
                        new AbstractFunctionInputBinding("send_result",    "external_context", "notification_status", true, false)
                    },
                    "SendEvidenceId",
                    new Dictionary<string, string> { ["function"] = "topology.em_record_send_evidence", ["required_table_authority"] = "topology.email_delivery_evidence" })
            },
            EmailDenyKeys, true,
            EmailAuthority("email_response_port_generic", "topology.email_delivery_evidence"),
            new Dictionary<string, string> { ["result"] = "SendEvidenceId" });

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly string[] EmailDenyKeys =
        { "credential", "smtp_host", "smtp_port", "smtp_api_key", "sender_credential", "raw_provider_response", "plaintext_payload", "token" };

    private static AbstractFunctionAuthorityBinding[] EmailAuthority(string policyKey, string tableRef) =>
        new AbstractFunctionAuthorityBinding[] { new("policy", policyKey, true), new("table", tableRef, true) };

    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    private static AbstractFunctionExecutor NewExecutor(AbstractFunctionManifest manifest, CapturedCallArgs captured) =>
        new(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[]
        {
            new CapturingPostgresFunctionAdapter(captured, Guid.NewGuid()),
            new ProjectionPrimitiveAdapter()
        });

    private static ExternalPortRecord MakePortRecord(Guid portId) =>
        new(portId, "response_port", "email_bundle", "smtp",
            "env:SMTP_HOST_REF", null, null, null,
            "external", "vault:ref:email_smtp_credential", true);

    private static ExternalPortPolicy MakePolicy(string policyKey) =>
        new(Guid.NewGuid(), policyKey, "response_port", "email_bundle", Array.Empty<ExternalPortPolicyStep>(), true);

    private static AbstractFunctionStep Step(int order, string primitive, IReadOnlyList<AbstractFunctionInputBinding> bindings, string? resultKey, IReadOnlyDictionary<string, string>? stepConfig = null) =>
        new(Guid.NewGuid(), order, primitive, stepConfig ?? new Dictionary<string, string>(), bindings, resultKey, true);

    private static AbstractFunctionStep MakeStep(Dictionary<string, string> stepConfig) =>
        new(Guid.NewGuid(), 1, "test_primitive", stepConfig, Array.Empty<AbstractFunctionInputBinding>(), null, true);

    private static string RepoRoot([CallerFilePath] string sourceFile = "")
    {
        var fromSource = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", ".."));
        if (File.Exists(Path.Combine(fromSource, "db", "seed_empty.sql"))) return fromSource;
        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "db", "seed_empty.sql"))) return cwd;
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "db", "seed_empty.sql")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

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
