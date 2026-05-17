using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

public class DefaultEntitySearchIntegrationTests
{
    private static DispatchEndpoint CreateDispatchEndpoint()
    {
        var topologyRepository = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");

        var runtimeExecutor = new RuntimeExecutor(
            logger: NullLogger<RuntimeExecutor>.Instance,
            operationVectorResolver: new OperationVectorResolver(),
            attractorResolver: new AttractorResolver(topologyRepository),
            structureMapResolver: new StructureMapResolver(topologyRepository),
            packageResolver: new PackageResolver(topologyRepository),
            schemaResolver: new SchemaResolver(topologyRepository),
            emissionBuilder: new EmissionBuilder(),
            semanticMapper: new SemanticMapper(),
            topologyRepository: topologyRepository,
            diffLogRepository: new DiffLogRepository(NullLogger<DiffLogRepository>.Instance),
            runtimeGuard: new RuntimeGuard());

        return new DispatchEndpoint(NullLogger<DispatchEndpoint>.Instance, runtimeExecutor);
    }

    [Fact]
    public async Task HandleAsync_DefaultEntitySearch_DispatchesCanonicalFlowAndReturnsEmission()
    {
        var endpoint = CreateDispatchEndpoint();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await endpoint.HandleAsync(request);

        var vector = new OperationVectorResolver().Resolve(request);
        Assert.Equal("default:entity:search", vector.AttractorKey);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Equal("00000000-0000-0000-0000-000000000004", response.Emission!.StructureMapId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), response.Emission.PackageId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), response.Emission.SchemaId);
        Assert.Contains("00000000-0000-0000-0000-000000000003", response.Emission.ComponentIds ?? []);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task HandleAsync_MissingAttractor_ReturnsExplicitAttractorResolveFailedError()
    {
        var endpoint = CreateDispatchEndpoint();

        var response = await endpoint.HandleAsync(new EndpointRequestDto("Search", "missing", "entity", "Search", null, null, null));

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "ATTRACTOR_RESOLVE_FAILED");
    }

    [Fact]
    public async Task HandleAsync_NullRequest_ReturnsRequestNullError()
    {
        var endpoint = CreateDispatchEndpoint();

        var response = await endpoint.HandleAsync(request: null!);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "REQUEST_NULL");
    }
}
