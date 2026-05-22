using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class RuntimeExecutorTests
{
    internal static TargetDispatchOverride CreateTargetDispatchOverride(TopologyRepository topologyRepository)
    {
        var contextRoutePolicyRepository = new StubValidPolicyTopologyRepository();
        var contextRouteRepository = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topologyVectorRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, contextRouteRepository);

        var adminRuntime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            contextRouteRepository,
            new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, contextRoutePolicyRepository, topologyVectorRuntime),
            new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double")),
            new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double"));

        return new TargetDispatchOverride(NullLogger<TargetDispatchOverride>.Instance, topologyRepository, adminRuntime);
    }

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
    public async Task ExecuteAsync_DefaultEntitySearch_RequestIdentityProducesAttractorKeyAndEmissionIdentity()
    {
        // Verifies pipeline identity chain from docs/design/pipeline-continuity-ssot.yaml
        // api_command_lane.required_identity:
        // request{target, layer, action} → vector.AttractorKey → emission{structureMapId, packageId, schemaId, componentIds}.
        var executor = CreateExecutor();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var vector = new OperationVectorResolver().Resolve(request);
        Assert.Equal("default:entity:search", vector.AttractorKey); // required_identity: attractor_key

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.NotNull(response.Emission!.StructureMapId);  // required_identity: structure_map_id
        Assert.NotNull(response.Emission.PackageId);        // required_identity: package_id
        Assert.NotNull(response.Emission.SchemaId);         // required_identity: schema_id
        Assert.NotNull(response.Emission.ComponentIds);     // required_identity: component_ids
        Assert.NotEmpty(response.Emission.ComponentIds!);
    }

}

public class SchedulerDispatcherChainTests
{
    [Fact]
    public async Task SchedulerDispatcherChain_DefaultEntitySearch_PassesThroughToExecutor()
    {
        // Verifies wiring: RuntimeTimelineScheduler → ManifestDispatcher → RuntimeExecutor.
        // Scheduler and dispatcher are pass-through skeletons; emission must be identical.
        var executor = RuntimeExecutorTests.CreateExecutor();
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            executor,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")));
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);

        try
        {
            var response = await scheduler.AlignAndDispatchAsync(request, cts.Token);

            Assert.True(response.Success);
            Assert.NotNull(response.Emission);
            Assert.Equal(TopologyRepository.DefaultStructureMapId, response.Emission!.StructureMapId);
            Assert.Equal(TopologyRepository.DefaultPackageId, response.Emission.PackageId);
            Assert.Equal(TopologyRepository.DefaultSchemaId, response.Emission.SchemaId);
            Assert.Contains(TopologyRepository.DefaultComponentId, response.Emission.ComponentIds ?? []);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SchedulerDispatcherChain_BrokenAttractor_PropagatesExplicitError()
    {
        // Broken refs must propagate through the full chain — no silent fallback.
        var executor = RuntimeExecutorTests.CreateExecutor();
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            executor,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")));
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        var request = new EndpointRequestDto("Search", "missing", "entity", "Search", null, null, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);

        try
        {
            var response = await scheduler.AlignAndDispatchAsync(request, cts.Token);

            Assert.False(response.Success);
            Assert.Null(response.Emission);
            Assert.Contains(response.Errors, e => e.Code == "ATTRACTOR_RESOLVE_FAILED");
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }
}

public class ManifestDispatcherOverrideTests
{
    [Fact]
    public async Task DispatchAsync_DemoEntityUnknownAction_ReturnsInvalidOperation()
    {
        var repo = new DemoEntityValidRouteTopologyRepository();
        var dispatcher = CreateDispatcher(repo);
        var req = new EndpointRequestDto("Search", "demo", "entity", "noop", null, null, null);

        var res = await dispatcher.DispatchAsync(req);

        Assert.False(res.Success);
        Assert.Contains(res.Errors, e => e.Code == "INVALID_OPERATION");
    }

    [Fact]
    public async Task DispatchAsync_DemoEntityDetailWithoutEntityId_ReturnsInvalidPayload()
    {
        var repo = new DemoEntityValidRouteTopologyRepository();
        var dispatcher = CreateDispatcher(repo);
        var payload = JsonSerializer.SerializeToElement(new { title = "x" });
        var req = new EndpointRequestDto("Search", "demo", "entity", "detail", null, payload, null);

        var res = await dispatcher.DispatchAsync(req);

        Assert.False(res.Success);
        Assert.Contains(res.Errors, e => e.Code == "INVALID_PAYLOAD");
    }

    [Fact]
    public async Task DispatchAsync_DemoEntityDetailMalformedEntityId_ReturnsInvalidPayload()
    {
        var repo = new DemoEntityValidRouteTopologyRepository();
        var dispatcher = CreateDispatcher(repo);
        var payload = JsonSerializer.SerializeToElement(new { entityId = "not-a-uuid" });
        var req = new EndpointRequestDto("Search", "demo", "entity", "detail", null, payload, null);

        var res = await dispatcher.DispatchAsync(req);

        Assert.False(res.Success);
        Assert.Contains(res.Errors, e => e.Code == "INVALID_PAYLOAD");
    }

    [Fact]
    public async Task DispatchAsync_DemoEntityList_ReachesOverrideRepositoryFlow()
    {
        var repo = new DemoEntityValidRouteTopologyRepository();
        var dispatcher = CreateDispatcher(repo);
        var req = new EndpointRequestDto("Search", "demo", "entity", "list", null, null, null);

        var res = await dispatcher.DispatchAsync(req);

        Assert.True(res.Success);
        Assert.NotNull(res.Emission);
        Assert.DoesNotContain(res.Errors, e => e.Code == "ATTRACTOR_RESOLVE_FAILED");
        Assert.True(repo.DemoEntityListCalled);
    }

    private static ManifestDispatcher CreateDispatcher(TopologyRepository topologyRepository)
    {
        var executor = RuntimeExecutorTests.CreateExecutor(topologyRepository);
        return new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            executor,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(topologyRepository));
    }
}

/// <summary>
/// Tests that verify Gap-1 completion condition:
/// target_layer_action_destination_selection_is_moved_to_manifest_dispatcher.
///
/// When ManifestRepository is configured, ManifestDispatcher resolves destination
/// from the manifest only. TargetDispatchOverride is not consulted.
/// </summary>
public class ManifestDispatcherManifestDrivenTests
{
    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_ManifestFound_RoutesToRuntimeExecutor()
    {
        var manifestId = Guid.NewGuid();
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var topologyRepo = new DemoEntityValidRouteTopologyRepository();
        var executor = RuntimeExecutorTests.CreateExecutor(topologyRepo);
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            executor,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(topologyRepo),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "demo", "entity", "list", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Empty(response.Errors);
        Assert.False(topologyRepo.DemoEntityListCalled,
            "TargetDispatchOverride must not be called when manifest repository is configured.");
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_ManifestNotFound_ReturnsManifestNotFound()
    {
        var manifestRepo = new StubManifestRepository(manifest: null);
        var executor = RuntimeExecutorTests.CreateExecutor();
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            executor,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "demo", "entity", "list", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_AmbiguousManifest_ReturnsManifestAmbiguous()
    {
        var manifestRepo = new AmbiguousStubManifestRepository();
        var executor = RuntimeExecutorTests.CreateExecutor();
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            executor,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_AMBIGUOUS");
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_DefaultEntitySearch_ManifestFound_RoutesToRuntimeExecutor()
    {
        var manifestId = Guid.NewGuid();
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var executor = RuntimeExecutorTests.CreateExecutor();
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            executor,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_UnknownRuntimeDestination_ReturnsRuntimeDestinationUnknown()
    {
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildTopology("unknown_runtime_xyz"),
                Status: "active"));
        var executor = RuntimeExecutorTests.CreateExecutor();
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            executor,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "RUNTIME_DESTINATION_UNKNOWN");
    }

    private static IReadOnlyList<System.Text.Json.JsonElement> BuildTopology(string runtimeDestination)
    {
        var entry = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            type = "runtime_mapping",
            runtime_destination = runtimeDestination
        });
        return [entry];
    }
}

internal sealed class StubManifestRepository : ManifestRepository
{
    private readonly ManifestRecord? _manifest;

    public StubManifestRepository(ManifestRecord? manifest)
        : base(NullLogger<ManifestRepository>.Instance) =>
        _manifest = manifest;

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action,
        CancellationToken ct = default) =>
        Task.FromResult(_manifest);

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult(_manifest?.ManifestId == manifestId ? _manifest : null);
}

internal sealed class AmbiguousStubManifestRepository : ManifestRepository
{
    public AmbiguousStubManifestRepository() : base(NullLogger<ManifestRepository>.Instance) { }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action,
        CancellationToken ct = default) =>
        throw new InvalidOperationException("MANIFEST_AMBIGUOUS: multiple active manifests match axes.");

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestRecord?>(null);
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
