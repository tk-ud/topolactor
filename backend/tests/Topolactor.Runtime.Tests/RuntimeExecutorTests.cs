using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class RuntimeExecutorTests
{
    internal static RuntimeExecutor CreateExecutor(TopologyRepository? topologyRepositoryOverride = null)
    {
        var topologyRepository = topologyRepositoryOverride ?? new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var contextRoutePolicyRepository = new StubValidPolicyTopologyRepository();
        var contextRouteRepository = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");

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
                contextRoutePolicyRepository,
                new SystemOperationCiRuntime(
                    NullLogger<SystemOperationCiRuntime>.Instance, contextRouteRepository)));
    }

    [Fact]
    public async Task ExecuteAsync_InMemoryDefaultRoute_ReturnsSuccessfulEmission()
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
        var repository = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");

        var structureMap = await repository.LoadStructureMapAsync("missing:entity:search");
        var package = await repository.LoadPackageAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var schema = await repository.LoadSchemaAsync(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var functionParameter = await repository.LoadFunctionParameterAsync("context_route_recommendation_resolve", "default_policy");

        Assert.Null(structureMap);
        Assert.Null(package);
        Assert.Null(schema);
        Assert.Null(functionParameter);
    }

    [Fact]
    public async Task ExecuteAsync_DemoEntityUnknownAction_ReturnsInvalidOperation()
    {
        var executor = CreateExecutor();
        var req = new EndpointRequestDto("Search", "demo", "entity", "noop", null, null, null);

        var res = await executor.ExecuteAsync(req);

        Assert.False(res.Success);
        Assert.Contains(res.Errors, e => e.Code == "INVALID_OPERATION");
    }

    [Fact]
    public async Task ExecuteAsync_DemoEntityDetailWithoutEntityId_ReturnsInvalidPayload()
    {
        var executor = CreateExecutor();
        var payload = JsonSerializer.SerializeToElement(new { title = "x" });
        var req = new EndpointRequestDto("Search", "demo", "entity", "detail", null, payload, null);

        var res = await executor.ExecuteAsync(req);

        Assert.False(res.Success);
        Assert.Contains(res.Errors, e => e.Code == "INVALID_PAYLOAD");
    }

    [Fact]
    public async Task ExecuteAsync_DemoEntityDetailMalformedEntityId_ReturnsInvalidPayload()
    {
        var executor = CreateExecutor();
        var payload = JsonSerializer.SerializeToElement(new { entityId = "not-a-uuid" });
        var req = new EndpointRequestDto("Search", "demo", "entity", "detail", null, payload, null);

        var res = await executor.ExecuteAsync(req);

        Assert.False(res.Success);
        Assert.Contains(res.Errors, e => e.Code == "INVALID_PAYLOAD");
    }

    [Fact]
    public async Task ExecuteAsync_DemoEntityList_ReachesPostAttractorFlow()
    {
        var repo = new DemoEntityValidRouteTopologyRepository();
        var executor = CreateExecutor(repo);
        var req = new EndpointRequestDto("Search", "demo", "entity", "list", null, null, null);

        var res = await executor.ExecuteAsync(req);

        Assert.DoesNotContain(res.Errors, e => e.Code == "INVALID_OPERATION");
        Assert.DoesNotContain(res.Errors, e => e.Code == "INVALID_PAYLOAD");
        Assert.DoesNotContain(res.Errors, e => e.Code == "ATTRACTOR_RESOLVE_FAILED");
        Assert.DoesNotContain(res.Errors, e => e.Code == "STRUCTURE_MAP_RESOLVE_FAILED");
        Assert.True(repo.DemoEntityListCalled);
    }
}

internal sealed class DemoEntityValidRouteTopologyRepository : TopologyRepository
{
    public bool DemoEntityListCalled { get; private set; }

    public DemoEntityValidRouteTopologyRepository() : base(NullLogger<TopologyRepository>.Instance, "test-double") { }

    public override Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
    {
        if (key is "demo:entity:list" or "demo:entity:detail" or "demo:entity:create" or "demo:entity:advance" or "11111111-1111-1111-1111-111111111111")
        {
            return Task.FromResult<StructureMapRecord?>(new StructureMapRecord(
                StructureMapId: "11111111-1111-1111-1111-111111111111",
                AttractorKey: "demo:entity:list",
                PackageId: TopologyRepository.DefaultPackageId,
                SchemaId: TopologyRepository.DefaultSchemaId,
                ComponentIds: [TopologyRepository.DefaultComponentId],
                StatePolicyJson: null));
        }
        return base.LoadStructureMapAsync(key, ct);
    }

    public override Task<IReadOnlyList<DemoEntityProjection>> LoadDemoEntityListAsync(CancellationToken ct = default)
    {
        DemoEntityListCalled = true;
        return Task.FromResult<IReadOnlyList<DemoEntityProjection>>([]);
    }
}
