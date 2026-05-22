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

    /// <summary>
    /// Creates a ManifestDispatcher with a standard handler dict:
    ///   topology_transform_runtime → executor built from topologyRepository
    /// Extra handlers are merged in (and can override the default).
    /// </summary>
    internal static ManifestDispatcher CreateDispatcher(
        TopologyRepository topologyRepository,
        ManifestRepository? manifestRepository = null,
        IReadOnlyDictionary<string, IDispatchableRuntime>? extraHandlers = null)
    {
        var executor = CreateExecutor(topologyRepository);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = executor,
        };
        if (extraHandlers is not null)
            foreach (var (k, v) in extraHandlers) handlers[k] = v;

        return new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            CreateTargetDispatchOverride(topologyRepository),
            manifestRepository);
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
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
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
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
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

    // --- Queue overflow boundary tests (Gap-14) ---

    [Fact]
    public void EnqueueCronTrigger_QueueNotFull_ReturnsTrue()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 4);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var accepted = scheduler.EnqueueCronTrigger(request);

        Assert.True(accepted);
    }

    [Fact]
    public void EnqueueCronTrigger_QueueFull_ReturnsFalse()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        // capacity=1: fill then attempt to overflow (no consumer running)
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 1);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var first = scheduler.EnqueueCronTrigger(request);
        var second = scheduler.EnqueueCronTrigger(request);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void EnqueueHookTrigger_QueueNotFull_ReturnsTrue()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 4);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var accepted = scheduler.EnqueueHookTrigger(request);

        Assert.True(accepted);
    }

    [Fact]
    public void EnqueueHookTrigger_QueueFull_ReturnsFalse()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 1);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var first = scheduler.EnqueueHookTrigger(request);
        var second = scheduler.EnqueueHookTrigger(request);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task AlignAndDispatchAsync_ClientTrigger_QueueFull_ReturnsSchedulerQueueFullError()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 1);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        // Do not start the consumer — queue fills immediately with the first item.
        var cronRequest = request with { TriggerKind = "cron" };
        scheduler.EnqueueCronTrigger(cronRequest);

        var response = await scheduler.AlignAndDispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "SCHEDULER_QUEUE_FULL");
    }

    [Fact]
    public async Task AlignAndDispatchAsync_ClientTriggerCanceled_ReturnsClientCanceledError()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        using var serviceCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(serviceCts.Token);

        try
        {
            using var clientCts = new CancellationTokenSource();
            clientCts.Cancel();

            var response = await scheduler.AlignAndDispatchAsync(
                new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null),
                clientCts.Token);

            Assert.False(response.Success);
            Assert.Contains(response.Errors, e => e.Code == "CLIENT_TRIGGER_CANCELED");
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
        => RuntimeExecutorTests.CreateDispatcher(topologyRepository);
}

/// <summary>
/// Tests that verify Gap-1 completion conditions:
/// - Gap-1a: target_layer_action_destination_selection_is_moved_to_manifest_dispatcher.
/// - Gap-1b: runtime_destination drives actual handler selection (not validation-only).
///
/// When ManifestRepository is configured, ManifestDispatcher resolves destination
/// from the manifest and dispatches to the registered IDispatchableRuntime handler.
/// TargetDispatchOverride is not consulted.
/// </summary>
public class ManifestDispatcherManifestDrivenTests
{
    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_ManifestFound_RoutesToRuntimeExecutor()
    {
        // Gap-1a: TargetDispatchOverride must NOT be called when manifest repo is configured.
        var manifestId = Guid.NewGuid();
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var topologyRepo = new DemoEntityValidRouteTopologyRepository();
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo, manifestRepo);

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
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
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
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
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
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
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
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "RUNTIME_DESTINATION_UNKNOWN");
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_AxesMismatch_ReturnsManifestNotFound()
    {
        // AxesFilteredStubManifestRepository returns manifest only for default/entity/Search.
        // A request with non-matching axes (admin/seed_runtime/save) must return MANIFEST_NOT_FOUND.
        var manifestRepo = new AxesFilteredStubManifestRepository(
            matchTarget: "default",
            matchLayer: "entity",
            matchAction: "Search",
            manifest: new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "admin", "seed_runtime", "save", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    /// <summary>
    /// Gap-1b verification: runtime_destination selects the registered handler.
    /// FakeDispatchableRuntime is test-only (not in production DI).
    /// Sentinel in Emission.Data proves the correct handler was invoked, not just that
    /// the request succeeded via the default topology pipeline.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_AdminRuntime_SelectsRegisteredHandler_WithSentinel()
    {
        var sentinel = JsonSerializer.SerializeToElement(new { handledBy = "admin_runtime", action = "save" });
        var fakeAdminHandler = new FakeDispatchableRuntime(sentinel);
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildTopology("admin_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime> { ["admin_runtime"] = fakeAdminHandler });

        var request = new EndpointRequestDto("X", "admin", "seed_runtime", "save", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.True(fakeAdminHandler.WasCalled,
            "admin_runtime handler must be called when manifest runtime_destination=admin_runtime.");
        Assert.Equal("admin_runtime",
            response.Emission!.Data!.Value.GetProperty("handledBy").GetString());
        Assert.False(response.Errors.Any());
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_TopologyTransformRuntime_DoesNotCallAdminHandler()
    {
        var fakeAdminHandler = new FakeDispatchableRuntime(null);
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime> { ["admin_runtime"] = fakeAdminHandler });

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.False(fakeAdminHandler.WasCalled,
            "admin_runtime handler must NOT be called when manifest runtime_destination=topology_transform_runtime.");
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

/// <summary>
/// Test-only stub handler. Records whether it was called and returns sentinel data.
/// Must NOT appear in production DI (Program.cs).
/// Per test_runtime_fixture_policy in docs/design/runtime-orchestration-ssot.yaml.
/// </summary>
internal sealed class FakeDispatchableRuntime : IDispatchableRuntime
{
    private readonly JsonElement? _sentinelData;
    public bool WasCalled { get; private set; }
    public EndpointRequestDto? LastRequest { get; private set; }

    public FakeDispatchableRuntime(JsonElement? sentinelData) => _sentinelData = sentinelData;

    public Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default)
    {
        WasCalled = true;
        LastRequest = request;
        var emission = new Emission(
            StructureMapId: null,
            PackageId: null,
            SchemaId: null,
            ComponentIds: [],
            Data: _sentinelData,
            Errors: [],
            ContextRouteRecommendation: null);
        return Task.FromResult(new EndpointResponseDto(Success: true, Emission: emission, Errors: []));
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

internal sealed class AxesFilteredStubManifestRepository : ManifestRepository
{
    private readonly string? _matchTarget;
    private readonly string? _matchLayer;
    private readonly string? _matchAction;
    private readonly ManifestRecord _manifest;

    public AxesFilteredStubManifestRepository(string? matchTarget, string? matchLayer, string? matchAction, ManifestRecord manifest)
        : base(NullLogger<ManifestRepository>.Instance)
    {
        _matchTarget = matchTarget;
        _matchLayer = matchLayer;
        _matchAction = matchAction;
        _manifest = manifest;
    }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action,
        CancellationToken ct = default)
    {
        if (target == _matchTarget && layer == _matchLayer && action == _matchAction)
            return Task.FromResult<ManifestRecord?>(_manifest);
        return Task.FromResult<ManifestRecord?>(null);
    }

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
