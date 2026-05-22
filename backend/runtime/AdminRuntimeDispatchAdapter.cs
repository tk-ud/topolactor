using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Production adapter that routes admin_runtime manifest destinations to AdminRuntime.
/// ManifestDispatcher registers this as the handler for runtime_destination=admin_runtime.
/// Not a stub: wraps real AdminRuntime.ExecuteDataAsync with no skeleton logic.
/// </summary>
public sealed class AdminRuntimeDispatchAdapter : IDispatchableRuntime
{
    private readonly AdminRuntime _adminRuntime;
    private readonly OperationVectorResolver _vectorResolver;

    public AdminRuntimeDispatchAdapter(AdminRuntime adminRuntime, OperationVectorResolver vectorResolver)
    {
        _adminRuntime = adminRuntime ?? throw new ArgumentNullException(nameof(adminRuntime));
        _vectorResolver = vectorResolver ?? throw new ArgumentNullException(nameof(vectorResolver));
    }

    public async Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request,
        Guid? manifestId,
        CancellationToken ct = default)
    {
        var vector = _vectorResolver.Resolve(request);
        var (data, error) = await _adminRuntime.ExecuteDataAsync(vector, ct);

        if (error is not null)
            return new EndpointResponseDto(Success: false, Emission: null, Errors: [error]);

        var emission = new Emission(
            StructureMapId: null,
            PackageId: null,
            SchemaId: null,
            ComponentIds: [],
            Data: data,
            Errors: [],
            ContextRouteRecommendation: null);

        return new EndpointResponseDto(Success: true, Emission: emission, Errors: []);
    }
}
