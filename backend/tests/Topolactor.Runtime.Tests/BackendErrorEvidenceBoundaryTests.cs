using System.Text.Json;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// AbstractFunctionExecutor primary-boundary error evidence tests.
/// Proves: system errors append once; user input miss / success never append; compensation +
/// rethrow + fail-close semantics are preserved; appender failure never masks the original failure.
/// </summary>
public class BackendErrorEvidenceBoundaryTests
{
    private static readonly AbstractFunctionAuthorityBinding[] ValidAuthority =
    {
        new("policy", "test_policy", true),
        new("table", "topology.test", true),
    };

    [Fact]
    public async Task ManifestMissing_AppendsSystemError_AndRethrows()
    {
        var appender = new FakeBackendErrorEvidenceAppender();
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(null),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() },
            appender);

        await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("missing.fn", new AbstractFunctionExecutionContext("test_scope")));

        Assert.Equal(1, appender.AppendCount);
        Assert.True(appender.Appended.TryDequeue(out var evidence));
        Assert.Equal(BackendErrorOriginLayer.AbstractFunctionExecutor, evidence!.OriginLayer);
        Assert.Equal(BackendErrorKind.SystemError, evidence.ErrorKind);
        Assert.Equal(AbstractFunctionFailCloseStatus.MissingAuthority, evidence.ErrorCode);
    }

    [Fact]
    public async Task UserInputMiss_DoesNotAppend_ButStillRethrows()
    {
        using var doc = JsonDocument.Parse("{}");
        var appender = new FakeBackendErrorEvidenceAppender();
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.fn", "external_port_runtime", "test_scope",
            new[] { Step(1, "echo", new[] { new AbstractFunctionInputBinding("required", "payload", "absent", true, false) }, null) },
            Array.Empty<string>(), true, ValidAuthority);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() },
            appender);

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("test.fn", new AbstractFunctionExecutionContext("test_scope", doc.RootElement)));

        Assert.Equal(AbstractFunctionFailCloseStatus.MissingInput, ex.Status);
        Assert.Equal(0, appender.AppendCount); // user input miss is never appended to logs.error
    }

    [Fact]
    public async Task PrimitiveSubstrateException_AppendsSystemError_RunsCompensation_AndRethrows()
    {
        var appender = new FakeBackendErrorEvidenceAppender();
        var compensation = new RecordingCompensationPrimitiveAdapter();
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.fn", "external_port_runtime", "test_scope",
            new[]
            {
                Step(1, "throwing", Array.Empty<AbstractFunctionInputBinding>(), null),
                CompensationStep(2, "compensate"),
            },
            Array.Empty<string>(), true, ValidAuthority);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new ThrowingPrimitiveAdapter(), compensation },
            appender);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync("test.fn", new AbstractFunctionExecutionContext("test_scope")));

        Assert.Equal("PRIMITIVE_DB_UNAVAILABLE", ex.Message);
        Assert.True(compensation.Executed, "compensation step must still run on failure");
        Assert.Equal(1, appender.AppendCount);
        Assert.True(appender.Appended.TryDequeue(out var evidence));
        Assert.Equal("throwing", evidence!.PrimitiveKey); // failing step recorded
        Assert.Equal(1, evidence.StepOrder);
        Assert.Equal("external_port_runtime", evidence.RuntimeLane);
    }

    [Fact]
    public async Task SuccessfulExecution_AppendsNothing()
    {
        var appender = new FakeBackendErrorEvidenceAppender();
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "test.fn", "external_port_runtime", "test_scope",
            new[] { Step(1, "echo", new[] { new AbstractFunctionInputBinding("source", "constant", "ok", false, false) }, "echoed") },
            Array.Empty<string>(), true, ValidAuthority);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() },
            appender);

        await executor.ExecuteAsync("test.fn", new AbstractFunctionExecutionContext("test_scope"));

        Assert.Equal(0, appender.AppendCount);
    }

    [Fact]
    public async Task AppenderFailure_DoesNotMaskOriginalFailure()
    {
        // Appender throws — the executor must still surface the ORIGINAL fail-close, not the appender error.
        var appender = new FakeBackendErrorEvidenceAppender(throwOnAppend: true);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(null),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() },
            appender);

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("missing.fn", new AbstractFunctionExecutionContext("test_scope")));
        Assert.Equal(AbstractFunctionFailCloseStatus.MissingAuthority, ex.Status);
    }

    [Fact]
    public async Task NoAppenderWired_PreservesExistingSemantics()
    {
        // Without an appender the executor behaves exactly as before (fail-close still thrown).
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(null),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });

        await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("missing.fn", new AbstractFunctionExecutionContext("test_scope")));
    }

    private static AbstractFunctionStep Step(int order, string primitive, IReadOnlyList<AbstractFunctionInputBinding> bindings, string? resultKey, IReadOnlyDictionary<string, string>? stepConfig = null) =>
        new(Guid.NewGuid(), order, primitive, stepConfig ?? new Dictionary<string, string>(), bindings, resultKey, true);

    private static AbstractFunctionStep CompensationStep(int order, string primitive) =>
        new(Guid.NewGuid(), order, primitive, new Dictionary<string, string>(), Array.Empty<AbstractFunctionInputBinding>(), null, true, IsCompensationStep: true);

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest? _manifest;
        public StaticManifestRepository(AbstractFunctionManifest? manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) => Task.FromResult(_manifest);
    }

    private sealed class EchoPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public string PrimitiveKey => "echo";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
            => Task.FromResult(inputs.TryGetValue("source", out var v) ? v : null);
    }

    private sealed class ThrowingPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public string PrimitiveKey => "throwing";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
            => throw new InvalidOperationException("PRIMITIVE_DB_UNAVAILABLE");
    }

    private sealed class RecordingCompensationPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public bool Executed { get; private set; }
        public string PrimitiveKey => "compensate";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
        {
            Executed = true;
            return Task.FromResult<object?>(null);
        }
    }
}
