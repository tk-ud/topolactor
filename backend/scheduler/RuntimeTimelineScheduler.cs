using Microsoft.Extensions.Logging;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Scheduler;

/// <summary>
/// Client-flow trigger alignment layer. Sits between the dispatch endpoint and
/// ManifestDispatcher, owning trigger_alignment, runtime_queue, and execution_boundary
/// per runtime-orchestration-ssot.
///
/// Skeleton: currently executes synchronously (no queue). When scheduler semantics
/// (ordering, batching, collision control) are implemented, they are confined to this class.
/// ManifestDispatcher and RuntimeExecutor must not know about trigger alignment.
/// </summary>
public class RuntimeTimelineScheduler
{
    private readonly ILogger<RuntimeTimelineScheduler> _logger;
    private readonly ManifestDispatcher _manifestDispatcher;

    public RuntimeTimelineScheduler(
        ILogger<RuntimeTimelineScheduler> logger,
        ManifestDispatcher manifestDispatcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manifestDispatcher = manifestDispatcher ?? throw new ArgumentNullException(nameof(manifestDispatcher));
    }

    /// <summary>
    /// Accepts a client trigger, aligns it on the runtime timeline, and forwards
    /// to the manifest dispatcher. Currently a synchronous pass-through skeleton.
    /// </summary>
    public Task<EndpointResponseDto> AlignAndDispatchAsync(
        EndpointRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "RuntimeTimelineScheduler: aligning client trigger Target={Target} Layer={Layer} Action={Action}",
            request?.Target, request?.Layer, request?.Action);

        return _manifestDispatcher.DispatchAsync(request!, ct);
    }
}
