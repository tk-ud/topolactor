using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Integration tests for the default:entity:search vertical skeleton.
/// Tests the full dispatch boundary:
/// EndpointRequestDto → DispatchEndpoint → RuntimeTimelineScheduler → ManifestDispatcher → RuntimeExecutor → emission.
/// No DB credentials, no production HTTP host, no real business data required.
/// </summary>
public class DefaultEntitySearchIntegrationTests
{
    private static (DispatchEndpoint Endpoint, RuntimeTimelineScheduler Scheduler) CreateEndpoint()
    {
        var topologyRepository = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");
        var contextRouteRepository = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy");
        var contextRoutePolicyRepository = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy");
        var topologyVectorRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, contextRouteRepository);
        var adminRuntime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            contextRouteRepository,
            new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, contextRoutePolicyRepository, topologyVectorRuntime),
            new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double")),
            new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double"));
        var targetDispatchOverride = new TargetDispatchOverride(
            NullLogger<TargetDispatchOverride>.Instance,
            adminRuntime);
        var executor = new RuntimeExecutor(
            logger: NullLogger<RuntimeExecutor>.Instance,
            operationVectorResolver: new OperationVectorResolver(),
            attractorResolver: new AttractorResolver(topologyRepository),
            structureMapResolver: new StructureMapResolver(topologyRepository),
            packageResolver: new PackageResolver(topologyRepository),
            schemaResolver: new SchemaResolver(topologyRepository),
            emissionBuilder: new EmissionBuilder(),
            semanticMapper: new SemanticMapper(),
            diffLogRepository: new DiffLogRepository(NullLogger<DiffLogRepository>.Instance),
            sqlAttentionLogsRepository: new SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "test-double"),
            runtimeGuard: new RuntimeGuard(),
            contextRouteRecommendationResolver: new ContextRouteRecommendationResolver(
                NullLogger<ContextRouteRecommendationResolver>.Instance,
                contextRouteRepository,
                new ContextVectorBuilder(),
                new ContextNeighborSearch(),
                topologyRepository,
                new SystemOperationCiRuntime(
                    NullLogger<SystemOperationCiRuntime>.Instance, contextRouteRepository)),
            outputLaneRouter: null);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = executor,
        };
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            targetDispatchOverride);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        return (new DispatchEndpoint(NullLogger<DispatchEndpoint>.Instance, scheduler), scheduler);
    }

    [Fact]
    public async Task DefaultEntitySearch_DispatchEndpoint_ReturnsSuccessfulEmission()
    {
        // Verifies the canonical default:entity:search integration path through the dispatch boundary.
        var (endpoint, scheduler) = CreateEndpoint();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);

        try
        {
            var response = await endpoint.HandleAsync(request, cts.Token);

            Assert.True(response.Success);
            Assert.NotNull(response.Emission);
            Assert.Equal("00000000-0000-0000-0000-000000000004", response.Emission!.StructureMapId);
            Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), response.Emission.PackageId);
            Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), response.Emission.SchemaId);
            Assert.Contains("00000000-0000-0000-0000-000000000003", response.Emission.ComponentIds ?? []);
            Assert.Empty(response.Errors);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RouteMissing_DispatchEndpoint_ReturnsFallbackJumpEvent()
    {
        // Route missing is handled as a canonical fallback jump event (not silent fallback).
        // Explicit infrastructure failure code paths (e.g. ATTRACTOR_RESOLVE_FAILED outside route_missing contract) are validated elsewhere.
        var (endpoint, scheduler) = CreateEndpoint();
        var request = new EndpointRequestDto("Search", "missing", "entity", "Search", null, null, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);

        try
        {
            var response = await endpoint.HandleAsync(request, cts.Token);

            Assert.True(response.Success);
            Assert.NotNull(response.Emission);
            Assert.Empty(response.Errors);
            Assert.NotNull(response.Emission!.JumpEvents);
            Assert.Equal(2, response.Emission.JumpEvents!.Count);
            Assert.Collection(
                response.Emission.JumpEvents,
                e =>
                {
                    Assert.Equal("hub", e.Scope);
                    Assert.Equal(0, e.PlannedAddress);
                    Assert.Equal("route_missing", e.Reason);
                },
                e =>
                {
                    Assert.Equal("topology", e.Scope);
                    Assert.Equal(0, e.PlannedAddress);
                    Assert.Equal("route_missing", e.Reason);
                });
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task NullRequest_DispatchEndpoint_ReturnsREQUEST_NULL()
    {
        var (endpoint, scheduler) = CreateEndpoint();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);

        try
        {
            var response = await endpoint.HandleAsync(null!, cts.Token);

            Assert.False(response.Success);
            Assert.Null(response.Emission);
            Assert.Contains(response.Errors, e => e.Code == "REQUEST_NULL");
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PipelineIdentity_DefaultEntitySearch_RequiredIdentityFieldsSurviveFullDispatch()
    {
        // Data-driven pipeline identity continuity check.
        // Verifies all required_identity fields from docs/design/pipeline-continuity-ssot.yaml
        // api_command_lane survive the full dispatch path:
        // EndpointRequestDto{target, layer, action} → DispatchEndpoint → RuntimeExecutor → Emission.
        var (endpoint, scheduler) = CreateEndpoint();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);

        try
        {
            var response = await endpoint.HandleAsync(request, cts.Token);

            Assert.True(response.Success);
            Assert.NotNull(response.Emission);
            Assert.Empty(response.Errors);
            Assert.NotNull(response.Emission!.StructureMapId);   // required_identity: structure_map_id
            Assert.NotNull(response.Emission.PackageId);         // required_identity: package_id
            Assert.NotNull(response.Emission.SchemaId);          // required_identity: schema_id
            Assert.NotNull(response.Emission.ComponentIds);      // required_identity: component_ids
            Assert.NotEmpty(response.Emission.ComponentIds!);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }
}
