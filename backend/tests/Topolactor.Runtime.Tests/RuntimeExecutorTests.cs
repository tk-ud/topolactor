using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class RuntimeExecutorTests
{
    internal static RuntimeExecutor CreateExecutor()
    {
        var topologyRepository = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");
        var contextRoutePolicyRepository = new StubValidPolicyTopologyRepository();
        var contextRouteRepository = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy");

        return new RuntimeExecutor(
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
                contextRoutePolicyRepository));
    }

    [Fact]
    public async Task ExecuteAsync_DefaultDummyRoute_ReturnsSuccessfulEmission()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        var vector = new OperationVectorResolver().Resolve(request);
        Assert.Equal("default:entity:search", vector.AttractorKey);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Equal("00000000-0000-0000-0000-000000000004", response.Emission!.StructureMapId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), response.Emission.PackageId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), response.Emission.SchemaId);
        Assert.Contains("00000000-0000-0000-0000-000000000003", response.Emission.ComponentIds ?? []);
        Assert.Empty(response.Errors);
        Assert.NotNull(response.Emission.ContextRouteRecommendation);
        Assert.Equal(RecommendationStatus.InsufficientHistory, response.Emission.ContextRouteRecommendation!.Status);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredFields_ReturnsValidationErrorsAndNoEmission()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto("Search", null, "entity", null, null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "MISSING_TARGET");
        Assert.Contains(response.Errors, e => e.Code == "MISSING_ACTION");
    }

    [Fact]
    public async Task ExecuteAsync_BrokenAttractor_ReturnsExplicitErrorWithoutFallback()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto("Search", "missing", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "ATTRACTOR_RESOLVE_FAILED");
    }

    [Fact]
    public async Task TopologyRepository_NonDefaultLookups_ReturnNull()
    {
        var repository = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");

        var structureMap = await repository.LoadStructureMapAsync("missing:entity:search");
        var package = await repository.LoadPackageAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var schema = await repository.LoadSchemaAsync(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var functionParameter = await repository.LoadFunctionParameterAsync("context_route_recommendation_resolve", "default_policy");

        Assert.Null(structureMap);
        Assert.Null(package);
        Assert.Null(schema);
        Assert.Null(functionParameter);
    }
}