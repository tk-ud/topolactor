using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Contract tests for the stripe_bundle port consumer (hook consumer).
///
/// Covers (per docs/design/runtime-bundle-stripe-ssot.yaml +
/// docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane):
///   - verify_signature_by_config failure records an explicit verification_failure to
///     runtime_event_log + signature_verification_evidence and fails closed; payment_state
///     is never mutated. The data-defined failure config makes this generic for any hook
///     consumer (no provider_kind branching).
///   - ExternalPortDispatchRuntime does not double-log a legacy signature_verification_failure
///     when the policy step already recorded the explicit verification_failure.
///   - hook_path/route_key resolution reaches the generic policy boundary.
///   - No Stripe provider-specific client/runtime/branching in runtime code.
///   - Seed lane: stripe hook policy routes through generic operation keys
///     (verify_signature_by_config + execute_abstract_function -> call_postgres_function);
///     physical writes are DB-driven and idempotent; projection response is DB-sourced.
/// </summary>
public class StripePortConsumerContractTests
{
    private static readonly Guid StripeHookPortId = Guid.Parse("00000000-0000-0000-0000-000000000f04");

    // -----------------------------------------------------------------------
    // verify_signature_by_config failure: explicit verification_failure to
    // runtime_event_log + evidence; payment_state never touched.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SignatureFailure_RecordsVerificationFailure_ToEventLogAndEvidence_NoPaymentMutation()
    {
        var record = StripeHookRecord();
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "stripe_hook_port_scheduler_boundary", "hook_port", "stripe_bundle",
            [
                Step(1, "resolve_port_record"),
                Step(2, "verify_signature_by_config", new Dictionary<string, string>
                {
                    ["expected_signature"] = "good-sig",
                    ["failure_event_type"] = "verification_failure",
                    ["failure_evidence_table_ref"] = "topology.signature_verification_evidence",
                    ["failure_status_value"] = "verification_failed"
                })
            ],
            Active: true);

        var eventLog = new CapturingEventLog();
        var evidence = new CapturingEvidence();
        var runtime = new ExternalPortDispatchRuntime(
            NullLogger<ExternalPortDispatchRuntime>.Instance,
            new FakeRepo(record, policy),
            new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: eventLog, consumerEvidenceRepository: evidence),
            null,
            eventLog);

        var response = await runtime.ExecuteAsync(StripeHookRequest("evt_fail", "payment_intent.succeeded", "wrong-sig"), null);

        Assert.False(response.Success);
        Assert.Equal("EXTERNAL_SIGNATURE_VERIFICATION_FAILED", response.Errors[0].Code);

        // verification_failure recorded exactly once (no legacy double-log).
        Assert.Equal(1, eventLog.EventTypes.Count(t => t == "verification_failure"));
        Assert.DoesNotContain("signature_verification_failure", eventLog.EventTypes);

        // evidence written to the verification evidence table, with the rejected status.
        Assert.Equal("topology.signature_verification_evidence", evidence.LastAppendTableRef);
        Assert.Equal("verification_failed", evidence.LastStatusValue);

        // payment_state projection table is never touched on the failure path.
        Assert.DoesNotContain("topology.payment_state_projections", evidence.AppendTableRefs);
    }

    // -----------------------------------------------------------------------
    // hook_path/route_key resolution reaches the generic boundary on success.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task HookPathRouteKey_ResolvesActiveRecord_AndReachesGenericBoundary()
    {
        var record = StripeHookRecord();
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "stripe_hook_port_scheduler_boundary", "hook_port", "stripe_bundle",
            [
                Step(1, "resolve_port_record"),
                Step(2, "verify_signature_by_config", new Dictionary<string, string> { ["expected_signature"] = "ok-sig" })
            ],
            Active: true);

        var repo = new FakeRepo(record, policy);
        var runtime = new ExternalPortDispatchRuntime(
            NullLogger<ExternalPortDispatchRuntime>.Instance, repo,
            new ExternalPortPolicyStepExecutor());

        var response = await runtime.ExecuteAsync(StripeHookRequest("evt_ok", "payment_intent.succeeded", "ok-sig"), null);

        Assert.True(response.Success, string.Join(",", response.Errors.Select(e => e.Code)));
        Assert.Equal("/hooks/stripe", repo.LastHookPath);
        Assert.Equal("stripe", repo.LastHookRouteKey);
        Assert.Contains("boundary_reached", response.Emission!.Data!.Value.GetRawText());
    }

    // -----------------------------------------------------------------------
    // No Stripe provider-specific client / runtime / branching.
    // -----------------------------------------------------------------------
    [Fact]
    public void RuntimeCode_ContainsNoStripeProviderClientOrBranching()
    {
        foreach (var rel in new[] { "backend/runtime/ExternalPortDispatchRuntime.cs", "backend/runtime/ExternalPortCredentialRefresher.cs" })
        {
            var src = File.ReadAllText(RepoFile(rel));
            Assert.DoesNotContain("StripeClient", src, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Stripe.net", src, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ProviderKind ==", src, StringComparison.Ordinal);
            Assert.DoesNotContain("case \"stripe\"", src, StringComparison.OrdinalIgnoreCase);
        }
    }

    // -----------------------------------------------------------------------
    // Seed lane contract: stripe hook policy + DB-driven physical writes.
    // -----------------------------------------------------------------------
    [Fact]
    public void SeedLane_StripeHookPolicy_RoutesThroughGenericAbstractFunctionLane()
    {
        var seed = File.ReadAllText(RepoFile("db/stripe_webhook_intake_preset_seed.sql"));

        // Generic operation keys: verify before any write, then DB-driven abstract functions.
        Assert.Contains("verify_signature_by_config", seed);
        foreach (var fn in new[]
        {
            "stripe.record_webhook_intake", "stripe.record_verification_success",
            "stripe.record_payment_state", "stripe.record_ledger_binding", "stripe.project_payment_state"
        })
            Assert.Contains($"'{fn}'", seed);

        // Physical writes are call_postgres_function (manifest-defined table authority, not payload).
        Assert.Contains("call_postgres_function", seed);
        Assert.Contains("\"required_table_authority\":\"topology.payment_state_projections\"", seed);

        // verify_signature_by_config carries the data-defined failure evidence config.
        Assert.Contains("\"failure_event_type\":\"verification_failure\"", seed);
        Assert.Contains("\"failure_evidence_table_ref\":\"topology.signature_verification_evidence\"", seed);

        // screen_data_shape projection with forbidden secret/credential fields.
        Assert.Contains("screen_data_shape", seed);
        Assert.Contains("forbiddenProjectionFields", seed);
        Assert.Contains("webhook_secret", seed);

        // No plaintext stripe secret / endpoint literal.
        Assert.DoesNotContain("sk_live_", seed);
        Assert.DoesNotContain("whsec_", seed);
    }

    [Fact]
    public void DbFunctions_EnforceVerifiedAuthorityAndIdempotency()
    {
        var ddl = File.ReadAllText(RepoFile("db/topology_tables.sql"));

        // payment_state authority = verified_webhook_event_only.
        var fnStart = ddl.IndexOf("FUNCTION topology.stripe_record_payment_state", StringComparison.Ordinal);
        Assert.True(fnStart > 0, "stripe_record_payment_state must exist");
        var fnBody = ddl.Substring(fnStart, Math.Min(2200, ddl.Length - fnStart));
        Assert.Contains("STRIPE_PAYMENT_STATE_NOT_VERIFIED", fnBody);
        Assert.Contains("ON CONFLICT (stripe_event_id)", fnBody);
        Assert.Contains("payment_state_projected", fnBody);

        // Idempotent intake keyed by stripe_event_id; partial unique index present.
        Assert.Contains("FUNCTION topology.stripe_record_webhook_intake", ddl);
        Assert.Contains("uq_payment_state_projections_stripe_event_id", ddl);

        // Projection response is read-only DB-sourced, not inline.
        Assert.Contains("FUNCTION topology.stripe_project_payment_state", ddl);
    }

    [Fact]
    public void Program_HasStripeHookEndpoint_WithSignatureHeader()
    {
        var program = File.ReadAllText(RepoFile("backend/Program.cs"));
        Assert.Contains("/hooks/stripe", program);
        Assert.Contains("stripe-signature", program);
        Assert.Contains("STRIPE_INTAKE_BODY_INVALID", program);
        Assert.Contains("AlignAndDispatchAsync", program);
        // The stripe intake forwards only id + type + a content hash (never the raw body).
        Assert.Contains("STRIPE_INTAKE_FIELDS_MISSING", program);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static ExternalPortRecord StripeHookRecord() =>
        new(StripeHookPortId, "hook_port", "stripe_bundle", "stripe",
            null, "/hooks/stripe", "stripe-signature", "stripe", "none", null, true);

    private static EndpointRequestDto StripeHookRequest(string stripeEventId, string paymentState, string signature)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            hook_path = "/hooks/stripe",
            route_key = "stripe",
            signature_input = new { signature },
            dispatch_payload = new { stripe_event_id = stripeEventId, payment_state = paymentState, payload_hash = "sha256:x" }
        });
        return new EndpointRequestDto(
            "dispatchExternalPort", "external_port", "external_port", "dispatchExternalPort",
            null, payload, null, "hook");
    }

    private static ExternalPortPolicyStep Step(int order, string operationKey) =>
        Step(order, operationKey, new Dictionary<string, string>());

    private static ExternalPortPolicyStep Step(int order, string operationKey, IReadOnlyDictionary<string, string> config) =>
        new(Guid.NewGuid(), Guid.NewGuid(), order, operationKey, config, true);

    private static string RepoFile(string rel, [CallerFilePath] string sourceFile = "")
    {
        var dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", ".."));
        while (dir is not null && !File.Exists(Path.Combine(dir, "db", "seed_empty.sql")))
            dir = Directory.GetParent(dir)?.FullName;
        return Path.Combine(dir ?? throw new InvalidOperationException("repo root not found"), rel);
    }

    private sealed class FakeRepo : IExternalPortPolicyRepository
    {
        private readonly ExternalPortRecord? _record;
        private readonly ExternalPortPolicy? _policy;
        public string? LastHookPath { get; private set; }
        public string? LastHookRouteKey { get; private set; }
        public FakeRepo(ExternalPortRecord? record, ExternalPortPolicy? policy) { _record = record; _policy = policy; }
        public Task<ExternalPortRecord?> LoadPortRecordAsync(string requiredByBundle, string portKind, string? routeKey, CancellationToken ct = default) => Task.FromResult(_record);
        public Task<ExternalPortRecord?> LoadPortRecordByIdAsync(string portKind, Guid portId, string? routeKey, CancellationToken ct = default) => Task.FromResult(_record);
        public Task<ExternalPortRecord?> LoadPortRecordByCanonicalBindingAsync(string manifestKey, string tableRef, string portKind, Guid portId, string? routeKey, CancellationToken ct = default) => Task.FromResult(_record);
        public Task<ExternalPortRecord?> LoadHookPortRecordAsync(string hookPath, string routeKey, CancellationToken ct = default) { LastHookPath = hookPath; LastHookRouteKey = routeKey; return Task.FromResult(_record); }
        public Task<ExternalPortPolicy?> LoadPolicyAsync(ExternalPortRecord portRecord, CancellationToken ct = default) => Task.FromResult(_policy);
    }

    private sealed class CapturingEventLog : IExternalPortRuntimeEventLogRepository
    {
        private readonly List<string> _eventTypes = new();
        public IReadOnlyList<string> EventTypes => _eventTypes;
        public Task AppendAsync(string eventType, string? entityId, string? requiredByBundle, CancellationToken ct = default)
        {
            _eventTypes.Add(eventType);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingEvidence : IExternalPortConsumerEvidenceRepository
    {
        private readonly List<string> _appendTableRefs = new();
        public IReadOnlyList<string> AppendTableRefs => _appendTableRefs;
        public string? LastAppendTableRef { get; private set; }
        public string? LastStatusValue { get; private set; }

        public Task AppendEvidenceAsync(string tableRef, string eventType, string? entityId, string? requiredByBundle, ExternalPortExecutionContext context, IReadOnlyDictionary<string, string> stepConfig, CancellationToken ct = default)
        {
            _appendTableRefs.Add(tableRef);
            LastAppendTableRef = tableRef;
            LastStatusValue = stepConfig.TryGetValue("status_value", out var s) ? s : null;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExternalPortConsumerEvidenceProjection>> LoadProjectionAsync(string tableRef, string? requiredByBundle, string? entityId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalPortConsumerEvidenceProjection>>([]);
    }
}
