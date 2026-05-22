using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Handler abstraction for manifest-driven runtime dispatch.
/// ManifestDispatcher selects the implementing handler based on
/// runtime_mapping.runtime_destination resolved from the active manifest.
/// </summary>
public interface IDispatchableRuntime
{
    Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request,
        Guid? manifestId,
        CancellationToken ct = default);
}
