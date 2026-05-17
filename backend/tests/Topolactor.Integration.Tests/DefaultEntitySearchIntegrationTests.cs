using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Integration tests for the default:entity:search vertical skeleton.
/// Tests the full dispatch boundary: EndpointRequestDto → DispatchEndpoint → RuntimeExecutor → emission.
/// No DB credentials, no production HTTP host, no real business data required.
/// </summary>
public class DefaultEntitySearchIntegrationTests
{
    private static DispatchEndpoint CreateEndpoint()
    {
        var topologyRepository = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");
        var contextRouteRepository = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy");
        var executor = new RuntimeExecutor(
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
            runtimeGuard: new RuntimeGuard(),
            contextRouteRecommendationResolver: new ContextRouteRecommendationResolver(
                NullLogger<ContextRouteRecommendationResolver>.Instance,
                contextRouteRepository,
                new ContextVectorBuilder(),
                new ContextNeighborSearch(),
                topologyRepository));
        return new DispatchEndpoint(NullLogger<DispatchEndpoint>.Instance, executor);
    }

    [Fact]
    public async Task DefaultEntitySearch_DispatchEndpoint_ReturnsSuccessfulEmission()
    {
        // Verifies the canonical default:entity:search integration path through the dispatch boundary.
        var endpoint = CreateEndpoint();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await endpoint.HandleAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Equal("00000000-0000-0000-0000-000000000004", response.Emission!.StructureMapId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), response.Emission.PackageId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), response.Emission.SchemaId);
        Assert.Contains("00000000-0000-0000-0000-000000000003", response.Emission.ComponentIds ?? []);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task BrokenAttractor_DispatchEndpoint_ReturnsATTRACTOR_RESOLVE_FAILED()
    {
        // Broken refs are explicit errors — no silent fallback to default:entity:search.
        var endpoint = CreateEndpoint();
        var request = new EndpointRequestDto("Search", "missing", "entity", "Search", null, null, null);

        var response = await endpoint.HandleAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "ATTRACTOR_RESOLVE_FAILED");
    }

    [Fact]
    public async Task NullRequest_DispatchEndpoint_ReturnsREQUEST_NULL()
    {
        var endpoint = CreateEndpoint();

        var response = await endpoint.HandleAsync(null!);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "REQUEST_NULL");
    }
}
