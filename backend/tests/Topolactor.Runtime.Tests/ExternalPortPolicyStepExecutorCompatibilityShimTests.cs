using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ExternalPortPolicyStepExecutorCompatibilityShimTests
{
    [Fact]
    public async Task ExecuteAsync_CompatibilityOperationWithoutAbstractFunctionKey_FailsClosed()
    {
        var inner = new RecordingPolicyStepExecutor();
        var shim = new ExternalPortPolicyStepExecutorCompatibilityShim(inner, BuildExecutor("test.function", "test_scope"));
        var context = new ExternalPortExecutionContext { RequiredByBundle = "test_scope" };
        var step = Step("send_http", new Dictionary<string, string>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => shim.ExecuteAsync(step, context));

        Assert.Equal("EXTERNAL_PORT_COMPAT_OPERATION_REQUIRES_ABSTRACT_FUNCTION_KEY", ex.Message);
        Assert.False(inner.ExecuteCalled);
        Assert.Empty(context.ExecutedOperationKeys);
    }

    [Fact]
    public async Task ExecuteAsync_CompatibilityOperationWithAbstractFunctionKey_DelegatesToAbstractFunction()
    {
        var inner = new RecordingPolicyStepExecutor();
        var shim = new ExternalPortPolicyStepExecutorCompatibilityShim(inner, BuildExecutor("test.function", "test_scope"));
        var context = new ExternalPortExecutionContext { RequiredByBundle = "test_scope" };
        var step = Step("append_runtime_event_log", new Dictionary<string, string> { ["abstract_function_key"] = "test.function" });

        await shim.ExecuteAsync(step, context);

        Assert.False(inner.ExecuteCalled);
        Assert.Equal(new[] { "append_runtime_event_log" }, context.ExecutedOperationKeys);
    }

    [Fact]
    public async Task ExecuteAsync_CompatibilityHttpOperation_BridgesHttpResponseForCaptureResponse()
    {
        var inner = new RecordingPolicyStepExecutor();
        var shim = new ExternalPortPolicyStepExecutorCompatibilityShim(inner, BuildHttpExecutor("test.http", "test_scope"));
        var context = new ExternalPortExecutionContext { RequiredByBundle = "test_scope" };
        var step = Step("send_http", new Dictionary<string, string> { ["abstract_function_key"] = "test.http" });

        await shim.ExecuteAsync(step, context);

        Assert.False(inner.ExecuteCalled);
        Assert.Equal(new[] { "send_http" }, context.ExecutedOperationKeys);
        Assert.NotNull(context.HttpResponse);
        Assert.Equal("{\"ok\":true}", context.HttpResponse!.Body);
    }

    [Fact]
    public async Task ExecuteAsync_NonCompatibilityOperation_DelegatesToInnerExecutor()
    {
        var inner = new RecordingPolicyStepExecutor();
        var shim = new ExternalPortPolicyStepExecutorCompatibilityShim(inner, BuildExecutor("test.function", "test_scope"));
        var context = new ExternalPortExecutionContext { RequiredByBundle = "test_scope" };
        var step = Step("execute_abstract_function", new Dictionary<string, string> { ["abstract_function_key"] = "test.function" });

        await shim.ExecuteAsync(step, context);

        Assert.True(inner.ExecuteCalled);
        Assert.Equal(new[] { "execute_abstract_function" }, inner.ExecutedOperationKeys);
    }

    [Fact]
    public async Task ExecutePolicyAsync_SetsPolicyContextBeforeCompatibilityDelegation()
    {
        var policyId = Guid.NewGuid();
        var step = new ExternalPortPolicyStep(
            Guid.NewGuid(),
            policyId,
            1,
            "enqueue_scheduler_event",
            new Dictionary<string, string> { ["abstract_function_key"] = "test.function" },
            true);
        var policy = new ExternalPortPolicy(
            policyId,
            "test_policy",
            "access_port",
            "test_scope",
            new[] { step },
            true);
        var shim = new ExternalPortPolicyStepExecutorCompatibilityShim(
            new RecordingPolicyStepExecutor(),
            BuildExecutor("test.function", "test_scope", "test_policy"));
        var context = new ExternalPortExecutionContext();

        await shim.ExecutePolicyAsync(policy, context);

        Assert.Same(policy, context.Policy);
        Assert.Equal("test_scope", context.RequiredByBundle);
        Assert.Equal("access_port", context.PortKind);
        Assert.Equal(new[] { "enqueue_scheduler_event" }, context.ExecutedOperationKeys);
    }

    private static ExternalPortPolicyStep Step(string operationKey, IReadOnlyDictionary<string, string> config) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1, operationKey, config, true);

    private static AbstractFunctionExecutor BuildExecutor(string functionKey, string authorityScope, string policyKey = "test_policy")
    {
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(),
            functionKey,
            "external_port_runtime",
            authorityScope,
            new[]
            {
                new AbstractFunctionStep(
                    Guid.NewGuid(),
                    1,
                    "echo",
                    new Dictionary<string, string>(),
                    new[] { new AbstractFunctionInputBinding("source", "constant", "ok", false, false) },
                    "result",
                    true)
            },
            Array.Empty<string>(),
            true,
            new[] { new AbstractFunctionAuthorityBinding("policy", policyKey, true) });

        return new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
    }

    private static AbstractFunctionExecutor BuildHttpExecutor(string functionKey, string authorityScope, string policyKey = "test_policy")
    {
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(),
            functionKey,
            "external_port_runtime",
            authorityScope,
            new[]
            {
                new AbstractFunctionStep(
                    Guid.NewGuid(),
                    1,
                    "stub_http_request",
                    new Dictionary<string, string>(),
                    Array.Empty<AbstractFunctionInputBinding>(),
                    "http_response",
                    true)
            },
            Array.Empty<string>(),
            true,
            new[] { new AbstractFunctionAuthorityBinding("policy", policyKey, true) });

        return new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new StubHttpRequestPrimitiveAdapter() });
    }

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest _manifest;
        public StaticManifestRepository(AbstractFunctionManifest manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) => Task.FromResult<AbstractFunctionManifest?>(_manifest);
    }

    private sealed class EchoPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public string PrimitiveKey => "echo";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default) =>
            Task.FromResult(inputs.TryGetValue("source", out var value) ? value : null);
    }

    private sealed class StubHttpRequestPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public string PrimitiveKey => "stub_http_request";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default) =>
            Task.FromResult<object?>(new ExternalPortHttpResponse(200, "{\"ok\":true}"));
    }

    private sealed class RecordingPolicyStepExecutor : IExternalPortPolicyStepExecutor
    {
        private readonly List<string> _executedOperationKeys = new();

        public bool ExecuteCalled { get; private set; }
        public IReadOnlyList<string> ExecutedOperationKeys => _executedOperationKeys;

        public Task ExecuteAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct = default)
        {
            ExecuteCalled = true;
            _executedOperationKeys.Add(step.OperationKey);
            return Task.CompletedTask;
        }

        public Task ExecutePolicyAsync(ExternalPortPolicy policy, ExternalPortExecutionContext context, CancellationToken ct = default) =>
            throw new NotSupportedException("POLICY_DELEGATION_NOT_USED_IN_SHIM_TEST");

        public ExternalPortHttpRequest BuildTokenRefreshRequest(ExternalTokenRefreshRequest request) =>
            throw new NotSupportedException("TOKEN_REFRESH_NOT_USED_IN_SHIM_TEST");

        public ExternalTokenRefreshResult ParseTokenRefreshResult(ExternalTokenRefreshRequest request, ExternalPortHttpResponse response) =>
            throw new NotSupportedException("TOKEN_REFRESH_NOT_USED_IN_SHIM_TEST");
    }
}
