using System.Text.Json;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AbstractFunctionExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_UsesManifestOrderAndBindsResultContext()
    {
        using var doc = JsonDocument.Parse("{\"value\":\"alpha\"}");
        var manifest = new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope", new[]
        {
            Step(1, "echo", new[] { new AbstractFunctionInputBinding("source", "payload", "value", true, false) }, "echoed"),
            Step(2, "projection", new[] { new AbstractFunctionInputBinding("public_value", "result_context", "echoed", true, false) }, "projection")
        }, Array.Empty<string>(), true);
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter(), new ProjectionPrimitiveAdapter() });

        var result = await executor.ExecuteAsync("test.function", new AbstractFunctionExecutionContext("test_scope", doc.RootElement));

        Assert.Equal(new[] { "echo", "projection" }, result.ExecutedPrimitiveKeys);
        Assert.Equal("alpha", result.ResultContext["echoed"]);
        var projection = Assert.IsType<Dictionary<string, object?>>(result.ResultContext["projection"]);
        Assert.Equal("alpha", projection["public_value"]);
    }

    [Theory]
    [InlineData("missing_authority")]
    [InlineData("invalid_authority")]
    [InlineData("missing_input")]
    [InlineData("unsupported_primitive")]
    [InlineData("secret_projection_denied")]
    public async Task ExecuteAsync_FailsClosedWithExplicitStatus(string status)
    {
        using var doc = JsonDocument.Parse("{\"value\":\"alpha\"}");
        AbstractFunctionManifest? manifest = status switch
        {
            "missing_authority" => null,
            "invalid_authority" => new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "new_dedicated_route", "test_scope", Array.Empty<AbstractFunctionStep>(), Array.Empty<string>(), true),
            "missing_input" => new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope", new[] { Step(1, "echo", new[] { new AbstractFunctionInputBinding("required", "payload", "missing", true, false) }, null) }, Array.Empty<string>(), true),
            "unsupported_primitive" => new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope", new[] { Step(1, "bundle_specific_handler", Array.Empty<AbstractFunctionInputBinding>(), null) }, Array.Empty<string>(), true),
            _ => new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope", new[] { Step(1, "projection", new[] { new AbstractFunctionInputBinding("credential", "payload", "value", true, false) }, null) }, Array.Empty<string>(), true)
        };
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter(), new ProjectionPrimitiveAdapter() });

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => executor.ExecuteAsync("test.function", new AbstractFunctionExecutionContext("test_scope", doc.RootElement)));

        Assert.Equal(status, ex.Status);
    }



    [Fact]
    public async Task ExternalPortPolicyStepExecutor_ExecuteAbstractFunction_UsesExecutorInsteadOfDbFallback()
    {
        using var doc = JsonDocument.Parse("{\"value\":\"alpha\"}");
        var manifest = new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope", new[]
        {
            Step(1, "echo", new[] { new AbstractFunctionInputBinding("source", "payload", "value", true, false) }, "echoed")
        }, Array.Empty<string>(), true);
        var abstractExecutor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new[] { new EchoPrimitiveAdapter() });
        var executor = new ExternalPortPolicyStepExecutor(dbFunctionRepository: new ThrowingDbFunctionRepository(), abstractFunctionExecutor: abstractExecutor);
        var context = new ExternalPortExecutionContext { RequiredByBundle = "test_scope", RequestPayload = doc.RootElement };
        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 1, "execute_abstract_function", new Dictionary<string, string> { ["abstract_function_key"] = "test.function" }, true);

        await executor.ExecuteAsync(step, context);

        Assert.Equal(new[] { "execute_abstract_function" }, context.ExecutedOperationKeys);
    }

    [Fact]
    public async Task ExternalPortPolicyStepExecutor_ExecuteAbstractFunction_FailsWhenExecutorMissing()
    {
        var executor = new ExternalPortPolicyStepExecutor(dbFunctionRepository: new ThrowingDbFunctionRepository());
        var context = new ExternalPortExecutionContext { RequiredByBundle = "test_scope" };
        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 1, "execute_abstract_function", new Dictionary<string, string> { ["abstract_function_key"] = "test.function", ["compatibility_function"] = "topology.fs_record_export_job" }, true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(step, context));

        Assert.Equal("EXTERNAL_PORT_ABSTRACT_FUNCTION_EXECUTOR_MISSING", ex.Message);
    }

    [Fact]
    public void RuntimeExecutor_DoesNotReferenceProviderOrBundleBranches()
    {
        var source = File.ReadAllText(Path.Combine("..", "..", "..", "..", "..", "runtime", "AbstractFunctionRuntime.cs"));
        Assert.DoesNotContain("provider_kind", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file_storage", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stripe", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("functionName switch", source, StringComparison.OrdinalIgnoreCase);
    }

    private static AbstractFunctionStep Step(int order, string primitive, IReadOnlyList<AbstractFunctionInputBinding> bindings, string? resultKey) => new(Guid.NewGuid(), order, primitive, new Dictionary<string, string>(), bindings, resultKey, true);

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest? _manifest;
        public StaticManifestRepository(AbstractFunctionManifest? manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) => Task.FromResult(_manifest);
    }

    private sealed class EchoPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public string PrimitiveKey => "echo";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default) => Task.FromResult(inputs.TryGetValue("source", out var value) ? value : null);
    }

    private sealed class ThrowingDbFunctionRepository : IExternalPortDbFunctionRepository
    {
        public Task ExecuteAsync(string functionName, IReadOnlyDictionary<string, string> stepConfig, ExternalPortExecutionContext context, CancellationToken ct = default) =>
            throw new InvalidOperationException("DB_FUNCTION_FALLBACK_SHOULD_NOT_BE_USED");
    }
}
