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
        var runtime = BuildRuntime(new FakeInstanceRepo { CredentialMissing = true });
        var response = await runtime.ExecuteAsync(Request($"instance-port:db_instance_port:{PortId}:approved"), null);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "INSTANCE_CREDENTIAL_REFERENCE_MISSING");
    }

    [Theory]
    [InlineData("record", "INSTANCE_PORT_RECORD_MISSING")]
    [InlineData("policy", "INSTANCE_CONNECTION_POLICY_MISSING")]
    [InlineData("binding", "INSTANCE_OPERATION_AUTHORITY_MISSING")]
    public async Task ExecuteAsync_MissingAuthority_FailsClose(string missing, string code)
    {
        var repo = new FakeInstanceRepo { RecordMissing = missing == "record", PolicyMissing = missing == "policy", BindingMissing = missing == "binding" };
        var response = await BuildRuntime(repo).ExecuteAsync(Request($"instance-port:db_instance_port:{PortId}:approved"), null);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == code);
    }

    [Theory]
    [InlineData("bad-ref", "INSTANCE_PORT_TARGET_REF_INVALID")]
    [InlineData("instance-port:access_port:00000000-0000-0000-0000-000000000ab1:approved", "INSTANCE_PORT_TARGET_REF_INVALID")]
    public async Task ExecuteAsync_MalformedInstanceTargetRef_FailsClose(string targetRef, string code)
    {
        var response = await BuildRuntime(new FakeInstanceRepo()).ExecuteAsync(Request(targetRef), null);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == code);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderSelectorAttempt_FailsClose()
    {
        var payload = JsonSerializer.SerializeToElement(new { instanceTargetRef = $"instance-port:db_instance_port:{PortId}:approved", provider_kind_selector = "external_postgres" });
        var response = await BuildRuntime(new FakeInstanceRepo()).ExecuteAsync(new EndpointRequestDto("x", null, null, null, null, payload, null), null);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "INSTANCE_PROVIDER_SELECTOR_FORBIDDEN");
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
        ctx.SetAuthorityBindings([new("instance", "registered_instance_key", true), new("instance_function", "registered_instance.approved_operation", true)]);
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

    private static InstancePortDispatchRuntime BuildRuntime(FakeInstanceRepo repo) => new(NullLogger<InstancePortDispatchRuntime>.Instance, repo, new FakeMaterializer(), new AbstractFunctionExecutor(new FakeManifestRepo(), new IAbstractFunctionPrimitiveAdapter[] { new CallBoundInstanceFunctionPrimitiveAdapter() }));
    private static EndpointRequestDto Request(string targetRef) => new EndpointRequestDto("x", null, null, null, null, JsonSerializer.SerializeToElement(new { instanceTargetRef = targetRef, dispatch_payload = new { x = "ok" } }), null);
}

internal sealed class FakeMaterializer : IInstanceCredentialMaterializer { public string MaterializeConnectionString(ExternalCredentialVaultRecord credentialVaultRecord) => "fake-runtime-only"; }

internal sealed class FakeInstanceRepo : IInstancePortPolicyRepository
{
    public bool RecordMissing { get; init; }
    public bool CredentialMissing { get; init; }
    public bool PolicyMissing { get; init; }
    public bool BindingMissing { get; init; }
    public Task<InstancePortRecord?> ResolveInstancePortRecordAsync(string portKind, Guid instancePortId, CancellationToken ct = default) => Task.FromResult(RecordMissing ? null : new InstancePortRecord(instancePortId, portKind, "registered_instance_key", "external_postgres", "generic_instance_integration", "vault:ref", true));
    public Task<ExternalCredentialVaultRecord?> ResolveInstanceCredentialReferenceAsync(string referenceKey, CancellationToken ct = default) => Task.FromResult(CredentialMissing ? null : new ExternalCredentialVaultRecord(Guid.NewGuid(), "external_postgres", "generic_instance_integration", "runtime", null, [0], "kms:ref", null, 300, 1, null, true));
    public Task<InstanceConnectionPolicy?> LoadConnectionPolicyAsync(string instanceAuthorityKey, string credentialReferenceKey, CancellationToken ct = default) => Task.FromResult(PolicyMissing ? null : Policy());
    public Task<InstanceOperationAuthorityBinding?> LoadOperationAuthorityBindingAsync(string instanceAuthorityKey, string operationBindingKey, CancellationToken ct = default) => Task.FromResult(BindingMissing ? null : Binding());
    public static InstanceConnectionPolicy Policy() => new(Guid.NewGuid(), "registered_instance_key", "vault:ref", 1000, 1000, 1024, new HashSet<string>(["approved_schema"], StringComparer.Ordinal), new HashSet<string>(["approved_function"], StringComparer.Ordinal), "secret_deny_default", true);
    public static InstanceOperationAuthorityBinding Binding() => new(Guid.NewGuid(), "approved", "registered_instance_key", "registered_instance.approved_operation", "approved_schema", "approved_function", "registered_instance.bound_operation", new Dictionary<string, string> { ["result"] = "InstanceResult" }, true, true);
    public static AbstractFunctionExecutionContext Context(InstanceOperationAuthorityBinding? binding = null) => new("registered_instance.approved_operation", requiredRuntimeLane: "instance_port_runtime", instancePortContext: new InstancePortExecutionContext { PortRecord = new InstancePortRecord(Guid.NewGuid(), "db_instance_port", "registered_instance_key", "external_postgres", "generic_instance_integration", "vault:ref", true), CredentialVaultRecord = new ExternalCredentialVaultRecord(Guid.NewGuid(), "external_postgres", "generic_instance_integration", "runtime", null, [0], "kms:ref", null, 300, 1, null, true), ConnectionPolicy = Policy(), OperationBinding = binding ?? Binding(), RuntimeOnlyConnectionString = "fake" });
}

internal sealed class FakeManifestRepo : IAbstractFunctionManifestRepository
{
    private readonly string _runtimeLane;
    public FakeManifestRepo(string runtimeLane = "instance_port_runtime") => _runtimeLane = runtimeLane;
    public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) => Task.FromResult<AbstractFunctionManifest?>(new AbstractFunctionManifest(Guid.NewGuid(), functionKey, _runtimeLane, "registered_instance.approved_operation", [new AbstractFunctionStep(Guid.NewGuid(), 1, "call_bound_instance_function", new Dictionary<string, string>(), [], "result", true)], [], true, [new("instance", "registered_instance_key", true), new("instance_function", "registered_instance.approved_operation", true)], new Dictionary<string, string> { ["result"] = "result" }));
}
