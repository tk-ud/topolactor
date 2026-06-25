using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ExternalAndEventPrimitiveAdapterTests
{
    // -----------------------------------------------------------------------
    // EventLogPrimitiveAdapter
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EventLog_AppendsWithEventTypeAndNullEntityId_WhenNoEntityRefKey()
    {
        var repo = new CapturingEventLogRepository();
        var adapter = new EventLogPrimitiveAdapter(repo);
        var step = MakeStep(new Dictionary<string, string> { ["event_type"] = "test_event" });
        var context = MakeContext();

        await adapter.ExecuteAsync(step, Empty, context);

        Assert.Single(repo.Calls);
        Assert.Equal("test_event", repo.Calls[0].EventType);
        Assert.Null(repo.Calls[0].EntityId);
    }

    [Fact]
    public async Task EventLog_ResolvesEntityId_FromExportJobId()
    {
        var repo = new CapturingEventLogRepository();
        var adapter = new EventLogPrimitiveAdapter(repo);
        var step = MakeStep(new Dictionary<string, string> { ["event_type"] = "export_done", ["entity_ref_key"] = "export_job_id" });
        var jobId = Guid.NewGuid();
        var context = MakeContext(extCtx: new ExternalPortExecutionContext { RequiredByBundle = "file_storage", ExportJobId = jobId });

        await adapter.ExecuteAsync(step, Empty, context);

        Assert.Equal(jobId.ToString(), repo.Calls[0].EntityId);
        Assert.Equal("file_storage", repo.Calls[0].Bundle);
    }

    [Fact]
    public async Task EventLog_FailsClose_WhenEventTypeMissing()
    {
        var repo = new CapturingEventLogRepository();
        var adapter = new EventLogPrimitiveAdapter(repo);
        var step = MakeStep(new Dictionary<string, string>());
        var context = MakeContext();

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => adapter.ExecuteAsync(step, Empty, context));
        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
        Assert.Contains("EVENT_LOG_EVENT_TYPE_MISSING", ex.Message);
    }

    [Fact]
    public async Task EventLog_FailsClose_WhenEntityRefKeyUnresolvable()
    {
        var repo = new CapturingEventLogRepository();
        var adapter = new EventLogPrimitiveAdapter(repo);
        var step = MakeStep(new Dictionary<string, string> { ["event_type"] = "e", ["entity_ref_key"] = "UnknownKey" });
        var context = MakeContext(extCtx: new ExternalPortExecutionContext());

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => adapter.ExecuteAsync(step, Empty, context));
        Assert.Equal(AbstractFunctionFailCloseStatus.MissingInput, ex.Status);
        Assert.Contains("EVENT_LOG_ENTITY_REF_KEY_UNRESOLVABLE", ex.Message);
    }

    // -----------------------------------------------------------------------
    // HttpRequestPrimitiveAdapter
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HttpRequest_FailsClose_WhenUrlMissing()
    {
        var adapter = new HttpRequestPrimitiveAdapter(new StubHttpClient(200, "{}"));
        var step = MakeStep(new Dictionary<string, string> { ["method"] = "POST" });
        var context = MakeContext();

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => adapter.ExecuteAsync(step, Empty, context));
        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
        Assert.Contains("HTTP_REQUEST_URL_MISSING", ex.Message);
    }

    [Fact]
    public async Task HttpRequest_FailsClose_WhenMethodMissing()
    {
        var adapter = new HttpRequestPrimitiveAdapter(new StubHttpClient(200, "{}"));
        var step = MakeStep(new Dictionary<string, string> { ["url"] = "https://example.invalid" });
        var context = MakeContext();

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => adapter.ExecuteAsync(step, Empty, context));
        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
        Assert.Contains("HTTP_REQUEST_METHOD_MISSING", ex.Message);
    }

    [Fact]
    public async Task HttpRequest_FailsClose_OnNon2xxResponse()
    {
        var adapter = new HttpRequestPrimitiveAdapter(new StubHttpClient(500, "error"));
        var step = MakeStep(new Dictionary<string, string> { ["url"] = "https://example.invalid", ["method"] = "GET" });
        var context = MakeContext();

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => adapter.ExecuteAsync(step, Empty, context));
        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidInputBinding, ex.Status);
        Assert.Contains("HTTP_REQUEST_NON_2XX", ex.Message);
    }

    [Fact]
    public async Task HttpRequest_ReturnsHttpResponse_On2xx()
    {
        var adapter = new HttpRequestPrimitiveAdapter(new StubHttpClient(200, """{"ok":true}"""));
        var step = MakeStep(new Dictionary<string, string> { ["url"] = "https://example.invalid/api", ["method"] = "POST" });
        var context = MakeContext();

        var result = await adapter.ExecuteAsync(step, Empty, context);

        var response = Assert.IsType<ExternalPortHttpResponse>(result);
        Assert.Equal(200, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // SchedulerEnqueuePrimitiveAdapter
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SchedulerEnqueue_SetsSchedulerEventEnqueued_WhenExternalPortContextPresent()
    {
        var adapter = new SchedulerEnqueuePrimitiveAdapter();
        var extCtx = new ExternalPortExecutionContext { RequiredByBundle = "test_bundle" };
        var step = MakeStep(new Dictionary<string, string>());
        var context = MakeContext(extCtx: extCtx);

        await adapter.ExecuteAsync(step, Empty, context);

        Assert.True(extCtx.SchedulerEventEnqueued);
    }

    [Fact]
    public async Task SchedulerEnqueue_FailsClose_WhenExternalPortContextMissing()
    {
        var adapter = new SchedulerEnqueuePrimitiveAdapter();
        var step = MakeStep(new Dictionary<string, string>());
        var context = new AbstractFunctionExecutionContext("scope", null, null);

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(() => adapter.ExecuteAsync(step, Empty, context));
        Assert.Equal(AbstractFunctionFailCloseStatus.MissingInput, ex.Status);
        Assert.Contains("SCHEDULER_ENQUEUE_EXTERNAL_PORT_CONTEXT_MISSING", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    private static AbstractFunctionStep MakeStep(Dictionary<string, string> stepConfig) =>
        new(Guid.NewGuid(), 1, "test_primitive", stepConfig, Array.Empty<AbstractFunctionInputBinding>(), null, true);

    private static AbstractFunctionExecutionContext MakeContext(ExternalPortExecutionContext? extCtx = null) =>
        new("test_scope", null, extCtx ?? new ExternalPortExecutionContext { RequiredByBundle = "test_bundle" });

    private sealed class CapturingEventLogRepository : IExternalPortRuntimeEventLogRepository
    {
        public List<(string EventType, string? EntityId, string? Bundle)> Calls { get; } = new();
        public Task AppendAsync(string eventType, string? entityId, string? bundle, CancellationToken ct = default)
        {
            Calls.Add((eventType, entityId, bundle));
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpClient : IExternalPortHttpClient
    {
        private readonly int _statusCode;
        private readonly string _body;
        public StubHttpClient(int statusCode, string body) { _statusCode = statusCode; _body = body; }
        public Task<ExternalPortHttpResponse> SendAsync(ExternalPortHttpRequest request, CancellationToken ct = default) =>
            Task.FromResult(new ExternalPortHttpResponse(_statusCode, _body));
    }
}
