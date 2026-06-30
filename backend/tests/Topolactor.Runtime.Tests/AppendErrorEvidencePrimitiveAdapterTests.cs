using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Proves the append_error_evidence primitive is an EXPLICIT seeded-step adapter, not the global
/// error-capture path. The global catch is the AbstractFunctionExecutor boundary classification.
/// </summary>
public class AppendErrorEvidencePrimitiveAdapterTests
{
    private static readonly AbstractFunctionAuthorityBinding[] ValidAuthority =
    {
        new("policy", "test_policy", true),
        new("table", "topology.test", true),
    };

    [Fact]
    public async Task ExplicitStep_AppendsConfiguredEvidence_Once()
    {
        var stepAppender = new FakeBackendErrorEvidenceAppender();
        var boundaryAppender = new FakeBackendErrorEvidenceAppender();

        var stepConfig = new Dictionary<string, string>
        {
            ["origin_layer"] = BackendErrorOriginLayer.PrimitiveAdapter,
            ["boundary_key"] = "explicit_seeded_step",
            ["error_code"] = "EXPLICIT_STEP_ERROR",
        };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.fn", "external_port_runtime", "test_scope",
            new[]
            {
                Step(1, "append_error_evidence",
                    new[] { new AbstractFunctionInputBinding("message_public", "constant", "explicit step message", true, false) },
                    null, stepConfig),
            },
            Array.Empty<string>(), true, ValidAuthority);

        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new AppendErrorEvidencePrimitiveAdapter(stepAppender) },
            boundaryAppender);

        await executor.ExecuteAsync("test.fn", new AbstractFunctionExecutionContext("test_scope"));

        // The explicit step appended exactly one row; the boundary (global catch) did not run.
        Assert.Equal(1, stepAppender.AppendCount);
        Assert.Equal(0, boundaryAppender.AppendCount);
        Assert.True(stepAppender.Appended.TryDequeue(out var evidence));
        Assert.Equal("EXPLICIT_STEP_ERROR", evidence!.ErrorCode);
        Assert.Equal(BackendErrorOriginLayer.PrimitiveAdapter, evidence.OriginLayer);
    }

    [Fact]
    public async Task PrimitiveIsNotGlobalCatch_BoundaryCapturesFailureWithoutTheStep()
    {
        // A manifest that FAILS but has NO append_error_evidence step. The system error is captured
        // by the executor boundary appender, NOT by the primitive. The primitive's appender is untouched.
        var stepAppender = new FakeBackendErrorEvidenceAppender();
        var boundaryAppender = new FakeBackendErrorEvidenceAppender();

        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.fn", "external_port_runtime", "test_scope",
            new[] { Step(1, "throwing", Array.Empty<AbstractFunctionInputBinding>(), null) },
            Array.Empty<string>(), true, ValidAuthority);

        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[]
            {
                new ThrowingPrimitiveAdapter(),
                new AppendErrorEvidencePrimitiveAdapter(stepAppender),
            },
            boundaryAppender);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync("test.fn", new AbstractFunctionExecutionContext("test_scope")));

        Assert.Equal(0, stepAppender.AppendCount);     // primitive is NOT the global catch
        Assert.Equal(1, boundaryAppender.AppendCount);  // executor boundary IS the global catch
    }

    [Fact]
    public async Task ExplicitStep_MissingStepConfig_FailsClose()
    {
        var stepAppender = new FakeBackendErrorEvidenceAppender();
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.fn", "external_port_runtime", "test_scope",
            new[]
            {
                Step(1, "append_error_evidence",
                    new[] { new AbstractFunctionInputBinding("message_public", "constant", "msg", true, false) },
                    null, new Dictionary<string, string>()),
            },
            Array.Empty<string>(), true, ValidAuthority);

        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new AppendErrorEvidencePrimitiveAdapter(stepAppender) });

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("test.fn", new AbstractFunctionExecutionContext("test_scope")));
        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
    }

    private static AbstractFunctionStep Step(int order, string primitive, IReadOnlyList<AbstractFunctionInputBinding> bindings, string? resultKey, IReadOnlyDictionary<string, string>? stepConfig = null) =>
        new(Guid.NewGuid(), order, primitive, stepConfig ?? new Dictionary<string, string>(), bindings, resultKey, true);

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest? _manifest;
        public StaticManifestRepository(AbstractFunctionManifest? manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) => Task.FromResult(_manifest);
    }

    private sealed class ThrowingPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public string PrimitiveKey => "throwing";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
            => throw new InvalidOperationException("PRIMITIVE_DB_UNAVAILABLE");
    }
}
