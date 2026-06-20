namespace Topolactor.Runtime;

/// <summary>
/// Migration shim for legacy external-port compatibility operation keys.
///
/// The canonical external/event path is execute_abstract_function -> AbstractFunctionExecutor.
/// Legacy compatibility keys are allowed only when they explicitly name the abstract function
/// manifest that owns the behavior. Missing delegate authority fails closed.
/// </summary>
public sealed class ExternalPortPolicyStepExecutorCompatibilityShim : IExternalPortPolicyStepExecutor
{
    private static readonly IReadOnlySet<string> CompatibilityOperationKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "build_http_request",
        "send_http",
        "enqueue_scheduler_event",
        "append_runtime_event_log"
    };

    private readonly IExternalPortPolicyStepExecutor _inner;
    private readonly AbstractFunctionExecutor _abstractFunctionExecutor;

    public ExternalPortPolicyStepExecutorCompatibilityShim(
        IExternalPortPolicyStepExecutor inner,
        AbstractFunctionExecutor abstractFunctionExecutor)
    {
        _inner = inner;
        _abstractFunctionExecutor = abstractFunctionExecutor;
    }

    public async Task ExecutePolicyAsync(ExternalPortPolicy policy, ExternalPortExecutionContext context, CancellationToken ct = default)
    {
        context.Policy = policy;
        context.RequiredByBundle ??= policy.RequiredByBundle;
        context.PortKind ??= policy.PortKind;

        foreach (var step in policy.PolicySteps.Where(static s => s.Active).OrderBy(static s => s.StepOrder))
        {
            await ExecuteAsync(step, context, ct);
        }
    }

    public Task ExecuteAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct = default)
    {
        if (CompatibilityOperationKeys.Contains(step.OperationKey))
        {
            return ExecuteCompatibilityAsync(step, context, ct);
        }

        return _inner.ExecuteAsync(step, context, ct);
    }

    public ExternalPortHttpRequest BuildTokenRefreshRequest(ExternalTokenRefreshRequest request) =>
        _inner.BuildTokenRefreshRequest(request);

    public ExternalTokenRefreshResult ParseTokenRefreshResult(ExternalTokenRefreshRequest request, ExternalPortHttpResponse response) =>
        _inner.ParseTokenRefreshResult(request, response);

    private async Task ExecuteCompatibilityAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct)
    {
        if (!step.StepConfig.TryGetValue("abstract_function_key", out var functionKey) || string.IsNullOrWhiteSpace(functionKey))
        {
            throw new InvalidOperationException("EXTERNAL_PORT_COMPAT_OPERATION_REQUIRES_ABSTRACT_FUNCTION_KEY");
        }

        await _abstractFunctionExecutor.ExecuteAsync(
            functionKey,
            new AbstractFunctionExecutionContext(
                context.RequiredByBundle ?? context.Policy?.RequiredByBundle ?? string.Empty,
                context.RequestPayload,
                context),
            ct);

        context.MarkExecuted(step.OperationKey);
    }
}
