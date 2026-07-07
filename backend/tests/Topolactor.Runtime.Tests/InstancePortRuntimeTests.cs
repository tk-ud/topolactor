using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

public sealed class InstancePortRuntimeTests
{
    private static readonly Guid PortId = Guid.Parse("00000000-0000-0000-0000-000000000ab1");

    [Fact]
    public async Task ExecuteAsync_MissingCredentialReference_FailsClose()
    {
        var runtime = BuildRuntime(new FakeInstanceRepo { CredentialMissing = true }, out var log);
        var response = await runtime.ExecuteAsync(Request($"instance-port:db_instance_port:{PortId}:approved"), null);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "INSTANCE_CREDENTIAL_REFERENCE_MISSING");
        Assert.Contains(log.Events, e => e.EventType == "instance_port_execution_fail_close" && e.EntityId!.Contains("failure_code=INSTANCE_CREDENTIAL_REFERENCE_MISSING", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("record", "INSTANCE_PORT_RECORD_MISSING")]
    [InlineData("policy", "INSTANCE_CONNECTION_POLICY_MISSING")]
    [InlineData("binding", "INSTANCE_OPERATION_AUTHORITY_MISSING")]
    public async Task ExecuteAsync_MissingAuthority_FailsClose(string missing, string code)
    {
        var repo = new FakeInstanceRepo { RecordMissing = missing == "record", PolicyMissing = missing == "policy", BindingMissing = missing == "binding" };
        var response = await BuildRuntime(repo, out _).ExecuteAsync(Request($"instance-port:db_instance_port:{PortId}:approved"), null);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == code);
    }

    [Theory]
    [InlineData("bad-ref", "INSTANCE_PORT_TARGET_REF_INVALID")]
    [InlineData("instance-port:access_port:00000000-0000-0000-0000-000000000ab1:approved", "INSTANCE_PORT_TARGET_REF_INVALID")]
    public async Task ExecuteAsync_MalformedInstanceTargetRef_FailsClose(string targetRef, string code)
    {
        var response = await BuildRuntime(new FakeInstanceRepo(), out _).ExecuteAsync(Request(targetRef), null);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == code);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderSelectorAttempt_FailsClose()
    {
        var payload = JsonSerializer.SerializeToElement(new { instanceTargetRef = $"instance-port:db_instance_port:{PortId}:approved", provider_kind_selector = "external_postgres" });
        var response = await BuildRuntime(new FakeInstanceRepo(), out _).ExecuteAsync(new EndpointRequestDto("x", null, null, null, null, payload, null), null);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "INSTANCE_PROVIDER_SELECTOR_FORBIDDEN");
    }


    [Fact]
    public async Task ExecuteAsync_MaterializationFailure_UsesRealVaultMaterializerAndFailsClose()
    {
        var response = await BuildRuntime(new FakeInstanceRepo { MaterializationFails = true }, out var log)
            .ExecuteAsync(Request($"instance-port:db_instance_port:{PortId}:approved"), null);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "INSTANCE_CREDENTIAL_MATERIALIZATION_FAILED");
        Assert.Contains(log.Events, e => e.EventType == "instance_port_execution_fail_close" && e.EntityId!.Contains("failure_code=INSTANCE_CREDENTIAL_MATERIALIZATION_FAILED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_Success_AppendsReferenceOnlyRuntimeEvidence()
    {
        var response = await BuildRuntime(new FakeInstanceRepo(), out var log)
            .ExecuteAsync(Request($"instance-port:db_instance_port:{PortId}:approved"), null);
        Assert.True(response.Success);
        var evidence = Assert.Single(log.Events, e => e.EventType == "instance_port_execution_success");
        Assert.Contains("instance_authority_key=registered_instance_key", evidence.EntityId);
        Assert.Contains("operation_binding_key=approved", evidence.EntityId);
        Assert.DoesNotContain("Host=", evidence.EntityId);
        Assert.DoesNotContain("secret", evidence.EntityId, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task ExecuteAsync_PrimitiveFailure_AppendsReferenceOnlyFailureEvidence()
    {
        var repo = new FakeInstanceRepo { BindingOverride = FakeInstanceRepo.Binding() with { SecretDeny = false } };
        var response = await BuildRuntime(repo, out var log)
            .ExecuteAsync(Request($"instance-port:db_instance_port:{PortId}:approved"), null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "INSTANCE_RESULT_SECRET_DENIED");
        var evidence = Assert.Single(log.Events, e => e.EventType == "instance_port_execution_fail_close" && e.EntityId!.Contains("failure_code=INSTANCE_RESULT_SECRET_DENIED", StringComparison.Ordinal));
        Assert.Contains("instance_authority_key=registered_instance_key", evidence.EntityId);
        Assert.Contains("port_kind=db_instance_port", evidence.EntityId);
        Assert.Contains("operation_binding_key=approved", evidence.EntityId);
        Assert.Contains("function_key=registered_instance.approved_operation", evidence.EntityId);
        Assert.DoesNotContain("Host=", evidence.EntityId);
    }

    [Fact]
    public async Task ExecuteAsync_AuthorityMismatch_AppendsReferenceOnlyFailureEvidence()
    {
        var manifestRepo = new FakeManifestRepo(authorities: FakeManifestRepo.Authorities.Where(a => a.AuthorityKind != "instance_operation").ToArray());
        var response = await BuildRuntime(new FakeInstanceRepo(), out var log, manifestRepo)
            .ExecuteAsync(Request($"instance-port:db_instance_port:{PortId}:approved"), null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "INSTANCE_OPERATION_NOT_APPROVED");
        var evidence = Assert.Single(log.Events, e => e.EventType == "instance_port_execution_fail_close" && e.EntityId!.Contains("failure_code=INSTANCE_OPERATION_NOT_APPROVED", StringComparison.Ordinal));
        Assert.Contains("instance_authority_key=registered_instance_key", evidence.EntityId);
        Assert.Contains("port_kind=db_instance_port", evidence.EntityId);
        Assert.Contains("operation_binding_key=approved", evidence.EntityId);
        Assert.Contains("function_key=registered_instance.approved_operation", evidence.EntityId);
    }

    [Theory]
    [InlineData("instance_schema", "INSTANCE_FUNCTION_SCHEMA_NOT_ALLOWLISTED")]
    [InlineData("instance_operation", "INSTANCE_OPERATION_NOT_APPROVED")]
    [InlineData("output", "INSTANCE_OUTPUT_AUTHORITY_MISSING")]
    public async Task CallBoundInstanceFunction_MissingAuthorityKinds_FailClose(string removedKind, string expectedCode)
    {
        var ctx = FakeInstanceRepo.Context();
        ctx.SetAuthorityBindings(FakeManifestRepo.Authorities.Where(a => a.AuthorityKind != removedKind).ToArray());
        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => new CallBoundInstanceFunctionPrimitiveAdapter().ExecuteAsync(new AbstractFunctionStep(Guid.NewGuid(), 1, "call_bound_instance_function", new Dictionary<string, string>(), [], "Result", true), new Dictionary<string, object?> { ["x"] = "ok" }, ctx, CancellationToken.None));
        Assert.Equal(expectedCode, ex.Message);
    }

    [Fact]
    public async Task AbstractFunctionRuntimeLaneMismatch_FailsClose()
    {
        var executor = new AbstractFunctionExecutor(new FakeManifestRepo(runtimeLane: "external_port_runtime"), new IAbstractFunctionPrimitiveAdapter[] { new CallBoundInstanceFunctionPrimitiveAdapter() });
        await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => executor.ExecuteAsync("registered_instance.bound_operation", new AbstractFunctionExecutionContext("registered_instance.bound_operation", requiredRuntimeLane: "instance_port_runtime", instancePortContext: FakeInstanceRepo.Context().InstancePortContext), CancellationToken.None));
    }

    [Fact]
    public async Task CallBoundInstanceFunction_SecretResultDenial_FailsClose()
    {
        var binding = FakeInstanceRepo.Binding() with { SecretDeny = false };
        var ctx = FakeInstanceRepo.Context(binding);
        ctx.SetAuthorityBindings(FakeManifestRepo.Authorities);
        await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => new CallBoundInstanceFunctionPrimitiveAdapter().ExecuteAsync(new AbstractFunctionStep(Guid.NewGuid(), 1, "call_bound_instance_function", new Dictionary<string, string>(), [], "Result", true), new Dictionary<string, object?> { ["x"] = "ok" }, ctx, CancellationToken.None));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("function")]
    [InlineData("allowlist_missing")]
    [InlineData("sanitize")]
    [InlineData("timeout")]
    public void CallInstancePostgresFunction_AuthorityHelpers_FailClose(string mode)
    {
        var policy = FakeInstanceRepo.Policy();
        var binding = FakeInstanceRepo.Binding();
        if (mode == "schema") binding = binding with { FunctionSchema = "denied_schema" };
        if (mode == "function") binding = binding with { FunctionName = "denied_function" };
        if (mode == "allowlist_missing") policy = policy with { AllowedFunctionNames = new HashSet<string>(StringComparer.Ordinal) };
        if (mode == "timeout") policy = policy with { StatementTimeoutMs = 0 };
        if (mode == "sanitize") Assert.Throws<AbstractFunctionFailCloseException>(() => CallInstancePostgresFunctionPrimitiveAdapter.SanitizeInstanceFunctionResultAsync("secret token", policy, binding));
        else Assert.Throws<AbstractFunctionFailCloseException>(() => CallInstancePostgresFunctionPrimitiveAdapter.VerifyInstanceConnectionPolicyAsync(policy, binding));
    }

    [Fact]
    public async Task CallPostgresFunction_TopologyOnlyBoundary_Regression()
    {
        var adapter = new CallPostgresFunctionPrimitiveAdapter("Host=topolactor-db-boundary-test");
        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => adapter.ExecuteAsync(new AbstractFunctionStep(Guid.NewGuid(), 1, "call_postgres_function", new Dictionary<string, string> { ["function"] = "public.anything", ["required_table_authority"] = "public.x" }, [], null, true), new Dictionary<string, object?>(), new AbstractFunctionExecutionContext("x"), CancellationToken.None));
        Assert.Equal("ABSTRACT_FUNCTION_POSTGRES_FUNCTION_INVALID", ex.Message);
    }

    // ─── retry-safe dispatch idempotency (response-lost / retry duplicate prevention) ─────────

    [Fact]
    public async Task ExecuteAsync_RetryWithSameIdempotencyKeyAfterCompletion_DoesNotReExecuteAndReturnsSameResult()
    {
        var adapter = new CountingBoundInstanceFunctionAdapter();
        var ledger = new FakeIdempotencyLedger();
        var runtime = BuildRuntimeWithLedger(new FakeInstanceRepo(), adapter, ledger, out _);

        // request 1: dispatchInstanceOperation with idempotency_key = K — backend executes the
        // instance operation, then marks K completed.
        var response1 = await runtime.ExecuteAsync(
            RequestWithIdempotencyKey($"instance-port:db_instance_port:{PortId}:approved", "idem-key-1"), null);
        Assert.True(response1.Success);
        Assert.Equal(1, adapter.CallCount);

        // request 2: response to request 1 was lost — client retries with the SAME idempotency_key.
        var response2 = await runtime.ExecuteAsync(
            RequestWithIdempotencyKey($"instance-port:db_instance_port:{PortId}:approved", "idem-key-1"), null);

        Assert.True(response2.Success);
        Assert.Equal(1, adapter.CallCount);
        Assert.Equal(
            response1.Emission!.Data!.Value.GetRawText(),
            response2.Emission!.Data!.Value.GetRawText());
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentDuplicateWhileInFlight_DoesNotExecuteAndFailsClosed()
    {
        var adapter = new CountingBoundInstanceFunctionAdapter();
        var ledger = new FakeIdempotencyLedger();
        var runtime = BuildRuntimeWithLedger(new FakeInstanceRepo(), adapter, ledger, out var log);

        await ledger.ClaimAsync("idem-key-2", "instance_port_runtime");

        var response = await runtime.ExecuteAsync(
            RequestWithIdempotencyKey($"instance-port:db_instance_port:{PortId}:approved", "idem-key-2"), null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "INSTANCE_PORT_DISPATCH_IN_FLIGHT");
        Assert.Equal(0, adapter.CallCount);
        Assert.Contains(log.Events, e => e.EventType == "instance_port_execution_fail_close" && e.EntityId!.Contains("failure_code=INSTANCE_PORT_DISPATCH_IN_FLIGHT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_NoIdempotencyKey_SkipsLedgerAndExecutesNormally()
    {
        var adapter = new CountingBoundInstanceFunctionAdapter();
        var ledger = new FakeIdempotencyLedger();
        var runtime = BuildRuntimeWithLedger(new FakeInstanceRepo(), adapter, ledger, out _);

        var response = await runtime.ExecuteAsync(Request($"instance-port:db_instance_port:{PortId}:approved"), null);

        Assert.True(response.Success);
        Assert.Equal(1, adapter.CallCount);
        Assert.Equal(0, ledger.ClaimCallCount);
    }

    private static InstancePortDispatchRuntime BuildRuntimeWithLedger(FakeInstanceRepo repo, IAbstractFunctionPrimitiveAdapter adapter, IRuntimeDispatchIdempotencyLedger ledger, out FakeRuntimeEventLog log)
    {
        log = new FakeRuntimeEventLog();
        return new InstancePortDispatchRuntime(NullLogger<InstancePortDispatchRuntime>.Instance, repo, new VaultReferenceInstanceCredentialMaterializer(new FakeCredentialCrypto()), new AbstractFunctionExecutor(new FakeManifestRepo(), new[] { adapter }), log, ledger);
    }

    private static EndpointRequestDto RequestWithIdempotencyKey(string targetRef, string idempotencyKey) =>
        new EndpointRequestDto("x", null, null, null, null, JsonSerializer.SerializeToElement(new { instanceTargetRef = targetRef, dispatch_payload = new { x = "ok" }, idempotency_key = idempotencyKey }), null);

    private static InstancePortDispatchRuntime BuildRuntime(FakeInstanceRepo repo, out FakeRuntimeEventLog log, IAbstractFunctionManifestRepository? manifestRepo = null)
    {
        log = new FakeRuntimeEventLog();
        return new InstancePortDispatchRuntime(NullLogger<InstancePortDispatchRuntime>.Instance, repo, new VaultReferenceInstanceCredentialMaterializer(new FakeCredentialCrypto()), new AbstractFunctionExecutor(manifestRepo ?? new FakeManifestRepo(), new IAbstractFunctionPrimitiveAdapter[] { new CallBoundInstanceFunctionPrimitiveAdapter() }), log);
    }
    private static EndpointRequestDto Request(string targetRef) => new EndpointRequestDto("x", null, null, null, null, JsonSerializer.SerializeToElement(new { instanceTargetRef = targetRef, dispatch_payload = new { x = "ok" } }), null);
}

/// <summary>
/// In-memory stand-in mirroring topology.rt_claim_dispatch_idempotency_key /
/// rt_complete_dispatch_idempotency_key / rt_fail_dispatch_idempotency_key semantics
/// (see db/topology_tables.sql), validated against a real Postgres instance separately.
/// </summary>
internal sealed class FakeIdempotencyLedger : IRuntimeDispatchIdempotencyLedger
{
    private readonly Dictionary<string, (string Status, JsonElement? Result)> _entries = new(StringComparer.Ordinal);
    public int ClaimCallCount { get; private set; }

    public Task<RuntimeDispatchIdempotencyClaimResult> ClaimAsync(string idempotencyKey, string dispatchLane, CancellationToken ct = default)
    {
        ClaimCallCount++;
        if (!_entries.TryGetValue(idempotencyKey, out var entry))
        {
            _entries[idempotencyKey] = ("processing", null);
            return Task.FromResult(new RuntimeDispatchIdempotencyClaimResult(RuntimeDispatchIdempotencyClaimStatus.Claimed, null));
        }
        if (entry.Status == "completed")
            return Task.FromResult(new RuntimeDispatchIdempotencyClaimResult(RuntimeDispatchIdempotencyClaimStatus.AlreadyCompleted, entry.Result));
        if (entry.Status == "failed")
        {
            _entries[idempotencyKey] = ("processing", null);
            return Task.FromResult(new RuntimeDispatchIdempotencyClaimResult(RuntimeDispatchIdempotencyClaimStatus.Claimed, null));
        }
        return Task.FromResult(new RuntimeDispatchIdempotencyClaimResult(RuntimeDispatchIdempotencyClaimStatus.InFlight, null));
    }

    public Task CompleteAsync(string idempotencyKey, JsonElement resultJson, CancellationToken ct = default)
    {
        _entries[idempotencyKey] = ("completed", resultJson);
        return Task.CompletedTask;
    }

    public Task FailAsync(string idempotencyKey, string failureCode, CancellationToken ct = default)
    {
        _entries[idempotencyKey] = ("failed", null);
        return Task.CompletedTask;
    }
}

/// <summary>Wraps CallBoundInstanceFunctionPrimitiveAdapter to count invocations for duplicate-execution proof.</summary>
internal sealed class CountingBoundInstanceFunctionAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly CallBoundInstanceFunctionPrimitiveAdapter _inner = new();
    public int CallCount { get; private set; }
    public string PrimitiveKey => _inner.PrimitiveKey;
    public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        CallCount++;
        return _inner.ExecuteAsync(step, inputs, context, ct);
    }
}

internal sealed class FakeCredentialCrypto : IExternalCredentialCrypto
{
    public string DecryptForRuntimeUse(byte[] encryptedPayload, string encryptionKeyReference)
    {
        if (encryptionKeyReference == "kms:fail") throw new InvalidOperationException("decrypt failed");
        return "Host=runtime-only-decrypted";
    }
    public byte[] EncryptForVaultStorage(string plaintextPayload, string encryptionKeyReference) => [1, 2, 3];
    public string ComputeTokenHash(string plaintextPayload) => "sha256:fake";
}

internal sealed class FakeRuntimeEventLog : IExternalPortRuntimeEventLogRepository
{
    public List<(string EventType, string? EntityId, string? RequiredByBundle)> Events { get; } = [];
    public Task AppendAsync(string eventType, string? entityId, string? requiredByBundle, CancellationToken ct = default)
    {
        Events.Add((eventType, entityId, requiredByBundle));
        return Task.CompletedTask;
    }
}

internal sealed class FakeInstanceRepo : IInstancePortPolicyRepository
{
    public bool RecordMissing { get; init; }
    public bool CredentialMissing { get; init; }
    public bool PolicyMissing { get; init; }
    public bool BindingMissing { get; init; }
    public bool MaterializationFails { get; init; }
    public InstanceOperationAuthorityBinding? BindingOverride { get; init; }
    public Task<InstancePortRecord?> ResolveInstancePortRecordAsync(string portKind, Guid instancePortId, CancellationToken ct = default) => Task.FromResult(RecordMissing ? null : new InstancePortRecord(instancePortId, portKind, "registered_instance_key", "external_postgres", "generic_instance_integration", "vault:ref", true));
    public Task<ExternalCredentialVaultRecord?> ResolveInstanceCredentialReferenceAsync(string referenceKey, CancellationToken ct = default) => Task.FromResult(CredentialMissing ? null : new ExternalCredentialVaultRecord(Guid.NewGuid(), "external_postgres", "generic_instance_integration", "runtime", null, [0], MaterializationFails ? "kms:fail" : "kms:ref", null, 300, 1, null, true));
    public Task<InstanceConnectionPolicy?> LoadConnectionPolicyAsync(string instanceAuthorityKey, string credentialReferenceKey, CancellationToken ct = default) => Task.FromResult(PolicyMissing ? null : Policy());
    public Task<InstanceOperationAuthorityBinding?> LoadOperationAuthorityBindingAsync(string instanceAuthorityKey, string operationBindingKey, CancellationToken ct = default) => Task.FromResult(BindingMissing ? null : BindingOverride ?? Binding());
    public static InstanceConnectionPolicy Policy() => new(Guid.NewGuid(), "registered_instance_key", "vault:ref", 1000, 1000, 1024, new HashSet<string>(["approved_schema"], StringComparer.Ordinal), new HashSet<string>(["approved_function"], StringComparer.Ordinal), "secret_deny_default", true);
    public static InstanceOperationAuthorityBinding Binding() => new(Guid.NewGuid(), "approved", "registered_instance_key", "registered_instance.approved_operation", "approved_schema", "approved_function", "registered_instance.bound_operation", new Dictionary<string, string> { ["result"] = "InstanceResult" }, true, true);
    public static AbstractFunctionExecutionContext Context(InstanceOperationAuthorityBinding? binding = null) => new("registered_instance.approved_operation", requiredRuntimeLane: "instance_port_runtime", instancePortContext: new InstancePortExecutionContext { PortRecord = new InstancePortRecord(Guid.NewGuid(), "db_instance_port", "registered_instance_key", "external_postgres", "generic_instance_integration", "vault:ref", true), CredentialVaultRecord = new ExternalCredentialVaultRecord(Guid.NewGuid(), "external_postgres", "generic_instance_integration", "runtime", null, [0], "kms:ref", null, 300, 1, null, true), ConnectionPolicy = Policy(), OperationBinding = binding ?? Binding(), RuntimeOnlyConnectionString = "fake" });
}

internal sealed class FakeManifestRepo : IAbstractFunctionManifestRepository
{
    private readonly string _runtimeLane;
    private readonly AbstractFunctionAuthorityBinding[] _authorities;
    public FakeManifestRepo(string runtimeLane = "instance_port_runtime", AbstractFunctionAuthorityBinding[]? authorities = null)
    {
        _runtimeLane = runtimeLane;
        _authorities = authorities ?? Authorities;
    }
    public static readonly AbstractFunctionAuthorityBinding[] Authorities =
    [
        new("instance", "registered_instance_key", true),
        new("instance_function", "registered_instance.approved_operation", true),
        new("instance_schema", "approved_schema", true),
        new("instance_operation", "approved", true),
        new("output", "InstanceResult", true)
    ];
    public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) => Task.FromResult<AbstractFunctionManifest?>(new AbstractFunctionManifest(Guid.NewGuid(), functionKey, _runtimeLane, "registered_instance.approved_operation", [new AbstractFunctionStep(Guid.NewGuid(), 1, "call_bound_instance_function", new Dictionary<string, string>(), [], "InstanceResult", true)], [], true, _authorities, new Dictionary<string, string> { ["result"] = "InstanceResult" }));
}
