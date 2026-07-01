using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Production adapter that routes admin_runtime manifest destinations to AdminRuntime.
/// ManifestDispatcher registers this as the handler for runtime_destination=admin_runtime.
/// Resolves OperationVector from request, delegates to AdminRuntime.ExecuteDataAsync, maps result to EndpointResponseDto.
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

        JsonElement? projectionDefinition = null;
        IReadOnlyList<string> componentIds = [];
        if (data.HasValue && data.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (data.Value.TryGetProperty("projectionDefinition", out var projectionDefinitionElement) &&
                projectionDefinitionElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                projectionDefinition = projectionDefinitionElement.Clone();
            }
            if (data.Value.TryGetProperty("componentIds", out var componentIdsElement) &&
                componentIdsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                componentIds = componentIdsElement.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToList();
            }
        }

        var emission = new Emission(
            StructureMapId: null,
            PackageId: null,
            SchemaId: null,
            ComponentIds: componentIds,
            Data: data,
            Errors: [],
            ContextRouteRecommendation: null,
            ProjectionDefinition: projectionDefinition);

        return new EndpointResponseDto(Success: true, Emission: emission, Errors: []);
    }
}
