using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ExternalPortDispatchRuntimeTests
{
    private static readonly Guid ResponsePortId = Guid.Parse("00000000-0000-0000-0000-00000000abcd");

    [Fact]
    public async Task ExecuteAsync_CanonicalPhysicalBinding_ReachesGenericPolicyBoundary()
    {
        var repo = new FakeRepo(NewRecord(ResponsePortId, credentialKind: "none"), NewPolicy());
        var runtime = new ExternalPortDispatchRuntime(NullLogger<ExternalPortDispatchRuntime>.Instance, repo, new ExternalPortPolicyStepExecutor());
        var payload = JsonSerializer.SerializeToElement(new
        {
            canonical_binding_manifest_key = "auth.external.credential_management.projection",
            canonical_binding_table_ref = "topology.external_response_ports",
            canonical_binding_port_kind = "response_port",
            canonical_binding_port_id = ResponsePortId.ToString()
        });
        var request = new EndpointRequestDto("dispatchExternalPort", "external_port", "external_port", "dispatchExternalPort", null, payload, null, "client");

        var response = await runtime.ExecuteAsync(request, null);

        Assert.True(response.Success);
        Assert.True(repo.CanonicalBindingUsed);
    }

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

    private static ExternalPortRecord NewRecord(Guid id, string credentialKind, string? referenceKey = null) =>
        new(id, "response_port", "test_bundle", "smtp", "https://example.invalid", null, null, null, credentialKind, referenceKey, true);

    private static ExternalPortPolicy NewPolicy() =>
        new(Guid.NewGuid(), "test_policy", "response_port", "test_bundle", [NewStep(1, "append_runtime_event_log")], true);

    private static ExternalPortPolicyStep NewStep(int order, string operationKey) =>
        new(Guid.NewGuid(), Guid.NewGuid(), order, operationKey, new Dictionary<string, string>(), true);

    private sealed class FakeRepo : IExternalPortPolicyRepository
    {
        private readonly ExternalPortRecord? _record;
        private readonly ExternalPortPolicy? _policy;
        public Guid? LastPortId { get; private set; }
        public FakeRepo(ExternalPortRecord? record, ExternalPortPolicy? policy) { _record = record; _policy = policy; }
        public Task<ExternalPortRecord?> LoadPortRecordAsync(string requiredByBundle, string portKind, string? routeKey, CancellationToken ct = default) => Task.FromResult(_record);
        public bool CanonicalBindingUsed { get; private set; }
        public Task<ExternalPortRecord?> LoadPortRecordByIdAsync(string portKind, Guid portId, string? routeKey, CancellationToken ct = default) { LastPortId = portId; return Task.FromResult(_record); }
        public Task<ExternalPortRecord?> LoadPortRecordByCanonicalBindingAsync(string manifestKey, string tableRef, string portKind, Guid portId, string? routeKey, CancellationToken ct = default) { CanonicalBindingUsed = true; LastPortId = portId; return Task.FromResult(_record); }
        public Task<ExternalPortPolicy?> LoadPolicyAsync(ExternalPortRecord portRecord, CancellationToken ct = default) => Task.FromResult(_policy);
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
