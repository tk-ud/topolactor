using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies ManifestDispatcher target_ref routing contract:
/// - Payload with target_ref="manifest:{uuid}:{key}" routes to LoadByIdAsync (exact manifest).
/// - Absent target_ref falls through to ResolveActiveManifestAsync (axes-based).
/// - Invalid target_ref format (non-manifest prefix) falls through to axes resolution.
/// - Valid target_ref with unknown UUID returns MANIFEST_NOT_FOUND.
/// </summary>
public class ManifestDispatcherTargetRefTests
{
    private static readonly Guid KnownManifestId = new("aaaaaaaa-bbbb-cccc-dddd-000000000001");

    private static ManifestDispatcher BuildDispatcher(TrackingManifestRepository repo)
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var targetOverride = RuntimeExecutorTests.CreateTargetDispatchOverride(topologyRepo);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = new StubSuccessRuntime(),
        };
        return new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            targetOverride,
            repo);
    }

    private static EndpointRequestDto MakeRequest(JsonElement? payload = null) =>
        new("Search", "screen", "screen_list", "Search",
            IdOrHubId: null,
            Payload: payload,
            Context: null,
            TriggerKind: "client",
            Role: null);

    private static JsonElement BuildPayload(string? targetRef = null, string? wiringKey = null, string? wiringId = null)
    {
        var dict = new Dictionary<string, object?>();
        if (targetRef is not null) dict["target_ref"] = targetRef;
        if (wiringKey is not null) dict["wiring_key"] = wiringKey;
        if (wiringId is not null) dict["wiring_id"] = wiringId;
        return JsonSerializer.SerializeToElement(dict);
    }

    // ─── target_ref present: routes to LoadByIdAsync ─────────────────────────

    [Fact]
    public async Task DispatchAsync_TargetRef_ManifestFormat_CallsLoadByIdAsync()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{KnownManifestId}:search_wiring";
        var payload = BuildPayload(targetRef: targetRef, wiringKey: "search_wiring", wiringId: "wiring-001");

        await dispatcher.DispatchAsync(MakeRequest(payload));

        Assert.True(repo.LoadByIdCalled, "LoadByIdAsync must be called when target_ref is present");
        Assert.False(repo.ResolveAxesCalled, "ResolveActiveManifestAsync must NOT be called when target_ref routes to exact manifest");
        Assert.Equal(KnownManifestId, repo.LoadByIdCalledWith);
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_ManifestFormat_ManifestFound_ReturnsSuccess()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{KnownManifestId}:search_wiring";

        var response = await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: targetRef)));

        Assert.True(response.Success);
        Assert.Empty(response.Errors);
    }

    // ─── target_ref absent: falls through to axes resolution ─────────────────

    [Fact]
    public async Task DispatchAsync_NoTargetRef_CallsResolveActiveManifestAsync()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);

        await dispatcher.DispatchAsync(MakeRequest(BuildPayload(wiringKey: "search_wiring")));

        Assert.False(repo.LoadByIdCalled, "LoadByIdAsync must NOT be called when target_ref is absent");
        Assert.True(repo.ResolveAxesCalled, "ResolveActiveManifestAsync must be called when target_ref absent");
    }

    [Fact]
    public async Task DispatchAsync_EmptyPayload_CallsResolveActiveManifestAsync()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);

        await dispatcher.DispatchAsync(MakeRequest());

        Assert.False(repo.LoadByIdCalled);
        Assert.True(repo.ResolveAxesCalled);
    }

    // ─── invalid target_ref format: falls through to axes resolution ──────────

    [Fact]
    public async Task DispatchAsync_TargetRef_NonManifestPrefix_FallsThroughToAxes()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);

        // "screen:..." is not manifest:<uuid> format — must not trigger LoadByIdAsync
        await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: "screen:some-ref")));

        Assert.False(repo.LoadByIdCalled);
        Assert.True(repo.ResolveAxesCalled);
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_InvalidUuid_FallsThroughToAxes()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);

        // "manifest:" prefix but invalid UUID portion — must not trigger LoadByIdAsync
        await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: "manifest:not-a-uuid:key")));

        Assert.False(repo.LoadByIdCalled);
        Assert.True(repo.ResolveAxesCalled);
    }

    // ─── valid target_ref format but manifest not found ───────────────────────

    [Fact]
    public async Task DispatchAsync_TargetRef_ManifestNotFound_ReturnsManifestNotFoundError()
    {
        var unknownId = Guid.NewGuid();
        var repo = new TrackingManifestRepository(knownManifestId: null); // no known manifest
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{unknownId}:search_wiring";

        var response = await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: targetRef)));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
        Assert.True(repo.LoadByIdCalled);
    }
}

// ─── Test doubles ─────────────────────────────────────────────────────────────

internal sealed class TrackingManifestRepository(Guid? knownManifestId)
    : ManifestRepository(NullLogger<ManifestRepository>.Instance)
{
    public bool LoadByIdCalled { get; private set; }
    public Guid LoadByIdCalledWith { get; private set; }
    public bool ResolveAxesCalled { get; private set; }

    private static ManifestRecord MakeManifest(Guid id) => new(
        ManifestId: id,
        RelationRegistryId: null,
        Topology: JsonSerializer.SerializeToElement(new[]
        {
            new { type = "runtime_mapping", runtime_destination = "topology_transform_runtime" },
        }).EnumerateArray().ToArray(),
        Status: "active");

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default)
    {
        LoadByIdCalled = true;
        LoadByIdCalledWith = manifestId;
        var result = knownManifestId.HasValue && manifestId == knownManifestId.Value
            ? MakeManifest(manifestId)
            : null;
        return Task.FromResult<ManifestRecord?>(result);
    }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action, CancellationToken ct = default)
    {
        ResolveAxesCalled = true;
        var result = knownManifestId.HasValue ? MakeManifest(knownManifestId.Value) : null;
        return Task.FromResult<ManifestRecord?>(result);
    }

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestDetailRecord?>(null);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId, Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
        Guid manifestId, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedLifecycle();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
        Guid manifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedLifecycle();

    public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.EmptyPromotionList(statusFilter, ct);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
        Guid manifestId, JsonElement promotionEntry, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();
}

internal sealed class StubSuccessRuntime : IDispatchableRuntime
{
    public Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default) =>
        Task.FromResult(new EndpointResponseDto(
            Success: true,
            Emission: new Emission(null, null, null, [], null, [], null, null),
            Errors: []));
}
