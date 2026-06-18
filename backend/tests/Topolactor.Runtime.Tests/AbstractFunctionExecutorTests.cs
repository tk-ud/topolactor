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
        }, Array.Empty<string>(), true, new AbstractFunctionAuthorityBinding[] { new("policy", "test_policy", true), new("table", "topology.test", true) });
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
        var validAuthority = new AbstractFunctionAuthorityBinding[] {
            new("policy", "test_policy", true),
            new("table", "topology.test_table", true)
        };
        AbstractFunctionManifest? manifest = status switch
        {
            "missing_authority" => null,
            "invalid_authority" => new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "new_dedicated_route", "test_scope", Array.Empty<AbstractFunctionStep>(), Array.Empty<string>(), true, validAuthority),
            "missing_input" => new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope", new[] { Step(1, "echo", new[] { new AbstractFunctionInputBinding("required", "payload", "missing", true, false) }, null) }, Array.Empty<string>(), true, validAuthority),
            "unsupported_primitive" => new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope", new[] { Step(1, "bundle_specific_handler", Array.Empty<AbstractFunctionInputBinding>(), null) }, Array.Empty<string>(), true, validAuthority),
            _ => new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope", new[] { Step(1, "projection", new[] { new AbstractFunctionInputBinding("credential", "payload", "value", true, false) }, null) }, Array.Empty<string>(), true, validAuthority)
        };
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter(), new ProjectionPrimitiveAdapter() });

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => executor.ExecuteAsync("test.function", new AbstractFunctionExecutionContext("test_scope", doc.RootElement)));

        Assert.Equal(status, ex.Status);
    }



    [Theory]
    [InlineData("no_bindings")]
    [InlineData("no_policy_binding")]
    [InlineData("inactive_only")]
    [InlineData("valid_binding")]
    public async Task ExecuteAsync_AuthorityBinding_EnforcedFailClose(string scenario)
    {
        var policyBinding = new AbstractFunctionAuthorityBinding("policy", "file_storage_access_port_generic", true);
        var tableBinding = new AbstractFunctionAuthorityBinding("table", "topology.export_jobs", true);
        var inactivePolicy = new AbstractFunctionAuthorityBinding("policy", "file_storage_access_port_generic", false);

        IReadOnlyList<AbstractFunctionAuthorityBinding>? authorityBindings = scenario switch
        {
            "no_bindings" => Array.Empty<AbstractFunctionAuthorityBinding>(),
            "no_policy_binding" => new[] { tableBinding },
            "inactive_only" => new[] { inactivePolicy },
            "valid_binding" => new[] { policyBinding, tableBinding },
            _ => null
        };

        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope",
            new[] { Step(1, "echo", new[] { new AbstractFunctionInputBinding("source", "constant", "ok", false, false) }, null) },
            Array.Empty<string>(), true, authorityBindings);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });

        if (scenario == "valid_binding")
        {
            var result = await executor.ExecuteAsync("test.function", new AbstractFunctionExecutionContext("test_scope"));
            Assert.Equal(new[] { "echo" }, result.ExecutedPrimitiveKeys);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
                () => executor.ExecuteAsync("test.function", new AbstractFunctionExecutionContext("test_scope")));
            Assert.Equal(AbstractFunctionFailCloseStatus.MissingAuthority, ex.Status);
        }
    }

    [Theory]
    [InlineData("wrong_table")]
    [InlineData("wrong_policy_key")]
    [InlineData("wrong_output_key")]
    public async Task ExecuteAsync_AuthorityEnforcement_RejectsInvalidAuthority(string scenario)
    {
        AbstractFunctionManifest manifest;
        AbstractFunctionExecutionContext context;

        switch (scenario)
        {
            case "wrong_table":
            {
                // fs_record_export_job requires topology.export_jobs — providing topology.file_artifacts is invalid
                var bindings = new AbstractFunctionAuthorityBinding[]
                {
                    new("policy", "some_policy", true),
                    new("table", "topology.file_artifacts", true) // wrong table for this function
                };
                manifest = new AbstractFunctionManifest(
                    Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope",
                    new[] { Step(1, "call_postgres_function",
                        new[] { new AbstractFunctionInputBinding("x", "constant", "1", false, false) }, null,
                        new Dictionary<string, string> { ["function"] = "topology.fs_record_export_job", ["required_table_authority"] = "topology.export_jobs" }) },
                    Array.Empty<string>(), true, bindings);
                context = new AbstractFunctionExecutionContext("test_scope");
                var executor = new AbstractFunctionExecutor(
                    new StaticManifestRepository(manifest),
                    new IAbstractFunctionPrimitiveAdapter[] { new CallPostgresFunctionPrimitiveAdapter("Host=localhost;Database=test;Username=test;Password=test") });
                var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
                    () => executor.ExecuteAsync("test.function", context));
                Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
                return;
            }
            case "wrong_policy_key":
            {
                // ExternalPortContext.Policy.PolicyKey = "correct_policy" but manifest has "other_policy"
                var bindings = new AbstractFunctionAuthorityBinding[]
                {
                    new("policy", "other_policy", true),
                    new("table", "topology.export_jobs", true)
                };
                manifest = new AbstractFunctionManifest(
                    Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope",
                    new[] { Step(1, "echo", new[] { new AbstractFunctionInputBinding("x", "constant", "ok", false, false) }, null) },
                    Array.Empty<string>(), true, bindings);
                var policy = new ExternalPortPolicy(Guid.NewGuid(), "correct_policy", "access_port", "test_scope", Array.Empty<ExternalPortPolicyStep>(), true);
                var externalCtx = new ExternalPortExecutionContext { RequiredByBundle = "test_scope", Policy = policy };
                context = new AbstractFunctionExecutionContext("test_scope", null, externalCtx);
                var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
                var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
                    () => executor.ExecuteAsync("test.function", context));
                Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
                return;
            }
            case "wrong_output_key":
            {
                // output_shape declares "ExportJobId" but step has ResultContextKey="WrongKey"
                var bindings = new AbstractFunctionAuthorityBinding[]
                {
                    new("policy", "some_policy", true),
                    new("table", "topology.export_jobs", true)
                };
                var outputShape = new Dictionary<string, string> { ["result"] = "ExportJobId" };
                manifest = new AbstractFunctionManifest(
                    Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope",
                    new[] { Step(1, "echo", new[] { new AbstractFunctionInputBinding("x", "constant", "ok", false, false) }, "WrongKey") },
                    Array.Empty<string>(), true, bindings, outputShape);
                context = new AbstractFunctionExecutionContext("test_scope");
                var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
                var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
                    () => executor.ExecuteAsync("test.function", context));
                Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
                return;
            }
        }
    }

    [Fact]
    public async Task CallPostgresFunctionAdapter_CorrectTableAuthority_Executes()
    {
        // fs_record_export_job → requires topology.export_jobs — providing it passes
        var policyBinding = new AbstractFunctionAuthorityBinding("policy", "some_policy", true);
        var tableBinding = new AbstractFunctionAuthorityBinding("table", "topology.export_jobs", true);
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope",
            new[] { Step(1, "stub_postgres_function",
                new[] { new AbstractFunctionInputBinding("x", "constant", "1", false, false) }, null,
                new Dictionary<string, string> { ["function"] = "topology.fs_record_export_job", ["required_table_authority"] = "topology.export_jobs" }) },
            Array.Empty<string>(), true, new[] { policyBinding, tableBinding });
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new StubPostgresFunctionPrimitiveAdapter() });

        var result = await executor.ExecuteAsync("test.function", new AbstractFunctionExecutionContext("test_scope"));
        Assert.Equal(new[] { "stub_postgres_function" }, result.ExecutedPrimitiveKeys);
        Assert.Contains(result.AuthorityBindings, b => b.AuthorityKind == "table" && b.AuthorityRef == "topology.export_jobs");
    }

    [Fact]
    public async Task CallPostgresFunctionAdapter_MissingRequiredTableAuthority_FailsInvalidAuthority()
    {
        // Step has no required_table_authority in step_config — must fail regardless of what table bindings are present
        var policyBinding = new AbstractFunctionAuthorityBinding("policy", "some_policy", true);
        var tableBinding = new AbstractFunctionAuthorityBinding("table", "topology.some_arbitrary_table", true);
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope",
            new[] { Step(1, "call_postgres_function",
                new[] { new AbstractFunctionInputBinding("x", "constant", "1", false, false) }, null,
                new Dictionary<string, string> { ["function"] = "topology.some_unknown_function" }) },
            Array.Empty<string>(), true, new[] { policyBinding, tableBinding });
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new CallPostgresFunctionPrimitiveAdapter("Host=localhost;Database=test;Username=test;Password=test") });

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("test.function", new AbstractFunctionExecutionContext("test_scope")));
        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ValidOutputShape_PassesWhenKeyMatches()
    {
        var bindings = new AbstractFunctionAuthorityBinding[]
        {
            new("policy", "some_policy", true),
            new("table", "topology.export_jobs", true)
        };
        var outputShape = new Dictionary<string, string> { ["result"] = "ExportJobId" };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope",
            new[] { Step(1, "echo", new[] { new AbstractFunctionInputBinding("x", "constant", "ok", false, false) }, "ExportJobId") },
            Array.Empty<string>(), true, bindings, outputShape);
        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });

        var result = await executor.ExecuteAsync("test.function", new AbstractFunctionExecutionContext("test_scope"));
        Assert.Equal(new[] { "echo" }, result.ExecutedPrimitiveKeys);
    }

    [Fact]
    public async Task ExternalPortPolicyStepExecutor_ExecuteAbstractFunction_UsesExecutorInsteadOfDbFallback()
    {
        using var doc = JsonDocument.Parse("{\"value\":\"alpha\"}");
        var manifest = new AbstractFunctionManifest(Guid.NewGuid(), "test.function", "external_port_runtime", "test_scope", new[]
        {
            Step(1, "echo", new[] { new AbstractFunctionInputBinding("source", "payload", "value", true, false) }, "echoed")
        }, Array.Empty<string>(), true, new AbstractFunctionAuthorityBinding[] { new("policy", "test_policy", true), new("table", "topology.test", true) });
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

    private static AbstractFunctionStep Step(int order, string primitive, IReadOnlyList<AbstractFunctionInputBinding> bindings, string? resultKey, IReadOnlyDictionary<string, string>? stepConfig = null) => new(Guid.NewGuid(), order, primitive, stepConfig ?? new Dictionary<string, string>(), bindings, resultKey, true);

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

    private sealed class StubPostgresFunctionPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public string PrimitiveKey => "stub_postgres_function";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default) => Task.FromResult<object?>(null);
    }

    private sealed class ThrowingDbFunctionRepository : IExternalPortDbFunctionRepository
    {
        public Task ExecuteAsync(string functionName, IReadOnlyDictionary<string, string> stepConfig, ExternalPortExecutionContext context, CancellationToken ct = default) =>
            throw new InvalidOperationException("DB_FUNCTION_FALLBACK_SHOULD_NOT_BE_USED");
    }
}
