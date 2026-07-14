using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies ManifestDispatcher's destination-agnostic enrichment (credential-management hub
/// relation / navigation / ui_projection bundle):
/// - manifest.topology[ui_projection].layoutId resolves LayoutId/LayoutNodes/PackageId on the
///   emission for ANY runtime_destination (not only topology_transform_runtime), reusing
///   TopologyRepository.LoadLayoutNodesAsync + StructureMapResolver's node validation.
/// - hub_relations NavigationSequence and the resolved ManifestId ("current topology phase")
///   are attached for ANY runtime_destination.
/// - an admin_runtime manifest with a ui_projection entry and no matching admin action
///   (ADMIN_OPERATION_NOT_FOUND) on a screen-read axes combination is treated as a structural
///   render-only entry rather than a hard failure — but only for screen-read axes; other
///   ADMIN_OPERATION_NOT_FOUND failures are preserved unchanged.
/// - a broken ui_projection.layoutId (zero tensor nodes) fails close (LAYOUT_NODES_NOT_FOUND),
///   not a silent empty render.
/// - the structure-map-driven pipeline (LayoutId already resolved) is left untouched.
/// </summary>
public class ManifestDispatcherUiProjectionEnrichmentTests
{
    private static readonly Guid ManifestId = new("bbbbbbbb-cccc-dddd-eeee-000000000092");
    private static readonly Guid LayoutId = new("bbbbbbbb-cccc-dddd-eeee-0000000cd002");
    private static readonly Guid PackageId = new("bbbbbbbb-cccc-dddd-eeee-0000000cd005");

    private static JsonElement[] BuildTopology(string runtimeDestination, bool includeUiProjection)
    {
        var entries = new List<object>
        {
            new { type = "runtime_mapping", runtime_destination = runtimeDestination },
        };
        if (includeUiProjection)
        {
            entries.Add(new
            {
                type = "ui_projection",
                packageIds = new[] { PackageId.ToString() },
                layoutId = LayoutId.ToString(),
                wiringId = "bbbbbbbb-cccc-dddd-eeee-0000000cd003",
                tensorId = "bbbbbbbb-cccc-dddd-eeee-0000000cd004",
            });
        }
        return JsonSerializer.SerializeToElement(entries).EnumerateArray().ToArray();
    }

    private sealed class SingleManifestRepository(JsonElement[] topology, Guid manifestId)
        : ManifestRepository(NullLogger<ManifestRepository>.Instance)
    {
        public override Task<ManifestRecord?> LoadByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ManifestRecord?>(id == manifestId
                ? new ManifestRecord(manifestId, null, topology, "active")
                : null);

        public override Task<ManifestRecord?> ResolveActiveManifestAsync(
            string? role, string? target, string? layer, string? action, CancellationToken ct = default) =>
            Task.FromResult<ManifestRecord?>(new ManifestRecord(manifestId, null, topology, "active"));

        public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(
            string? statusFilter, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.EmptyList(statusFilter, ct);

        public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid id, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NullDetail(id, ct);

        public override Task<int> CountActiveAxisConflictsAsync(
            string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.ZeroConflicts(role, target, layer, action, excludeManifestId, ct);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
            Guid? relationRegistryId, IReadOnlyList<JsonElement> newTopology, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NotImplementedDraft();

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
            Guid id, Guid? relationRegistryId, IReadOnlyList<JsonElement> newTopology, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NotImplementedDraft();

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
            Guid id, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NotImplementedLifecycle();

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
            Guid id, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NotImplementedLifecycle();

        public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
            string? statusFilter, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.EmptyPromotionList(statusFilter, ct);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
            Guid id, JsonElement promotionEntry, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NotImplementedMerge();

        public override Task<int> CountActivePromotionKeyConflictsAsync(
            string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.ZeroPromotionConflicts(manifestKey, versionLabel, excludeManifestId, ct);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
            Guid id, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NotImplementedMerge();
    }

    private sealed class AdminOperationNotFoundRuntime : IDispatchableRuntime
    {
        public Task<EndpointResponseDto> ExecuteAsync(
            EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default) =>
            Task.FromResult(new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError("ADMIN_OPERATION_NOT_FOUND", "Unknown admin operation: screen_list:search")]));
    }

    private sealed class OtherAdminErrorRuntime : IDispatchableRuntime
    {
        public Task<EndpointResponseDto> ExecuteAsync(
            EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default) =>
            Task.FromResult(new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError("ADMIN_OPERATION_NOT_FOUND", "Unknown admin operation: mutate:write")]));
    }

    private sealed class CompositeErrorAdminRuntime : IDispatchableRuntime
    {
        public Task<EndpointResponseDto> ExecuteAsync(
            EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default) =>
            Task.FromResult(new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors:
                [
                    new ValidationError("ADMIN_OPERATION_NOT_FOUND", "Unknown admin operation: screen_list:search"),
                    new ValidationError("SOME_OTHER_REAL_FAILURE", "a genuine second failure alongside the routing gap"),
                ]));
    }

    private sealed class SuccessRuntimeWithLayoutAlreadySet : IDispatchableRuntime
    {
        public Task<EndpointResponseDto> ExecuteAsync(
            EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default) =>
            Task.FromResult(new EndpointResponseDto(
                Success: true,
                Emission: new Emission(
                    StructureMapId: null, PackageId: null, SchemaId: null, ComponentIds: [],
                    Data: null, Errors: [], LayoutId: "already-resolved-by-structure-map"),
                Errors: []));
    }

    private sealed class FakeTopologyRepository : TopologyRepository
    {
        private readonly IReadOnlyList<LayoutNodeRecord> _nodes;
        public bool LoadLayoutNodesCalled { get; private set; }

        public FakeTopologyRepository(IReadOnlyList<LayoutNodeRecord> nodes)
            : base(NullLogger<TopologyRepository>.Instance, "unused")
        {
            _nodes = nodes;
        }

        public override Task<IReadOnlyList<LayoutNodeRecord>> LoadLayoutNodesAsync(
            Guid layoutId, CancellationToken ct = default)
        {
            LoadLayoutNodesCalled = true;
            return Task.FromResult(layoutId == LayoutId ? _nodes : Array.Empty<LayoutNodeRecord>());
        }

        public override Task<string?> LoadLayoutCalcBindingsJsonAsync(Guid layoutId, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
    }

    private static LayoutNodeRecord MakeNode(string nodeId) => new(
        NodeId: nodeId,
        NodeKind: "structural_html",
        HtmlTag: "div",
        ComponentKey: null,
        ComponentId: null,
        ParentNodeId: null,
        SlotKey: null,
        OrderIndex: 0,
        X: 0,
        Y: 0,
        Width: null,
        Height: null,
        LayoutClassRefs: null);

    private static EndpointRequestDto ScreenReadRequest() =>
        new("Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: null, Context: null, TriggerKind: "client", Role: "admin");

    [Fact]
    public async Task DispatchAsync_AdminRuntimeUiProjection_ScreenReadNotFound_SynthesizesStructuralSuccessAndResolvesLayoutNodes()
    {
        var topology = BuildTopology("admin_runtime", includeUiProjection: true);
        var manifestRepo = new SingleManifestRepository(topology, ManifestId);
        var fakeTopologyRepo = new FakeTopologyRepository([MakeNode("root")]);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new AdminOperationNotFoundRuntime(),
        };
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepo,
            errorAppender: null,
            topologyRepository: fakeTopologyRepo,
            hubNavigationResolver: null);

        var response = await dispatcher.DispatchAsync(ScreenReadRequest());

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Equal(LayoutId.ToString(), response.Emission!.LayoutId);
        Assert.NotNull(response.Emission.LayoutNodes);
        Assert.Single(response.Emission.LayoutNodes!);
        Assert.Equal(PackageId, response.Emission.PackageId);
        Assert.Equal(ManifestId.ToString(), response.Emission.ManifestId);
        Assert.True(fakeTopologyRepo.LoadLayoutNodesCalled);
    }

    [Fact]
    public async Task DispatchAsync_AdminRuntime_NonScreenReadAdminOperationNotFound_StaysAFailure()
    {
        var topology = BuildTopology("admin_runtime", includeUiProjection: true);
        var manifestRepo = new SingleManifestRepository(topology, ManifestId);
        var fakeTopologyRepo = new FakeTopologyRepository([MakeNode("root")]);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new OtherAdminErrorRuntime(),
        };
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepo,
            errorAppender: null,
            topologyRepository: fakeTopologyRepo,
            hubNavigationResolver: null);

        var request = new EndpointRequestDto("Mutate", "default", "other_layer", "write",
            IdOrHubId: null, Payload: null, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.False(fakeTopologyRepo.LoadLayoutNodesCalled);
    }

    [Fact]
    public async Task DispatchAsync_NonAdminRuntimeDestination_AdminOperationNotFoundOnScreenRead_StaysAFailure()
    {
        // Guard must require destination == "admin_runtime" explicitly, not merely infer it
        // from the error code — a differently-routed manifest returning the same error code
        // (however unlikely) must never take the structural-success fallback path.
        var topology = BuildTopology("topology_transform_runtime", includeUiProjection: true);
        var manifestRepo = new SingleManifestRepository(topology, ManifestId);
        var fakeTopologyRepo = new FakeTopologyRepository([MakeNode("root")]);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = new AdminOperationNotFoundRuntime(),
        };
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepo,
            errorAppender: null,
            topologyRepository: fakeTopologyRepo,
            hubNavigationResolver: null);

        var response = await dispatcher.DispatchAsync(ScreenReadRequest());

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.False(fakeTopologyRepo.LoadLayoutNodesCalled);
    }

    [Fact]
    public async Task DispatchAsync_AdminRuntimeUiProjection_CompositeErrorAlongsideAdminOperationNotFound_StaysAFailure()
    {
        // A composite/multi-error failure is a real failure, not a routing gap — must not be
        // silently downgraded to structural success just because one of the errors happens to
        // be ADMIN_OPERATION_NOT_FOUND.
        var topology = BuildTopology("admin_runtime", includeUiProjection: true);
        var manifestRepo = new SingleManifestRepository(topology, ManifestId);
        var fakeTopologyRepo = new FakeTopologyRepository([MakeNode("root")]);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new CompositeErrorAdminRuntime(),
        };
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepo,
            errorAppender: null,
            topologyRepository: fakeTopologyRepo,
            hubNavigationResolver: null);

        var response = await dispatcher.DispatchAsync(ScreenReadRequest());

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.False(fakeTopologyRepo.LoadLayoutNodesCalled);
    }

    [Fact]
    public async Task DispatchAsync_AdminRuntimeUiProjection_EmptyTensorNodes_FailsCloseWithLayoutNodesNotFound()
    {
        var topology = BuildTopology("admin_runtime", includeUiProjection: true);
        var manifestRepo = new SingleManifestRepository(topology, ManifestId);
        var fakeTopologyRepo = new FakeTopologyRepository([]); // no nodes for LayoutId — broken config
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new AdminOperationNotFoundRuntime(),
        };
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepo,
            errorAppender: null,
            topologyRepository: fakeTopologyRepo,
            hubNavigationResolver: null);

        var response = await dispatcher.DispatchAsync(ScreenReadRequest());

        Assert.False(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Contains(response.Emission!.Errors, e => e.Code == "LAYOUT_NODES_NOT_FOUND");
    }

    [Fact]
    public async Task DispatchAsync_AnyDestination_AttachesNavigationSequenceAndManifestId()
    {
        // Reuses InMemoryContentBundleRepository's fixture: FixtureTopologyManifestId has one
        // active hub_relation to FixtureRelatedHubId, and FixtureRelatedHubManifestId is the
        // sole topology_manifest registered under that hub — so TargetManifestId resolves.
        var fixtureManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId;
        var topology = BuildTopology("admin_runtime", includeUiProjection: false);
        var manifestRepo = new SingleManifestRepository(topology, fixtureManifestId);
        var hubNavResolver = new HubNavigationResolver(new InMemoryContentBundleRepository(), manifestRepo);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new SuccessRuntimeWithLayoutAlreadySet(),
        };
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepo,
            errorAppender: null,
            topologyRepository: null,
            hubNavigationResolver: hubNavResolver);

        var response = await dispatcher.DispatchAsync(ScreenReadRequest());

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Equal(fixtureManifestId.ToString(), response.Emission!.ManifestId);
        Assert.NotNull(response.Emission.NavigationSequence);
        var item = Assert.Single(response.Emission.NavigationSequence!);
        Assert.Equal(InMemoryContentBundleRepository.FixtureRelatedHubId.ToString(), item.RelatedHubId);
        Assert.Equal(InMemoryContentBundleRepository.FixtureRelatedHubManifestId.ToString(), item.TargetManifestId);
        // structure-map-set LayoutId is untouched by ui_projection enrichment (no topologyRepository injected here anyway).
        Assert.Equal("already-resolved-by-structure-map", response.Emission.LayoutId);
    }

    [Fact]
    public async Task DispatchAsync_StructureMapAlreadyResolvedLayoutId_UiProjectionEnrichmentDoesNotOverwrite()
    {
        var topology = BuildTopology("topology_transform_runtime", includeUiProjection: true);
        var manifestRepo = new SingleManifestRepository(topology, ManifestId);
        var fakeTopologyRepo = new FakeTopologyRepository([MakeNode("root")]);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = new SuccessRuntimeWithLayoutAlreadySet(),
        };
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepo,
            errorAppender: null,
            topologyRepository: fakeTopologyRepo,
            hubNavigationResolver: null);

        var response = await dispatcher.DispatchAsync(ScreenReadRequest());

        Assert.True(response.Success);
        Assert.Equal("already-resolved-by-structure-map", response.Emission!.LayoutId);
        Assert.False(fakeTopologyRepo.LoadLayoutNodesCalled);
    }
}
