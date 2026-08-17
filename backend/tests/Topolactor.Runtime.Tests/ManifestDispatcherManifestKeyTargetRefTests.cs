using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies ManifestDispatcher's generic manifest_key_target_ref_resolution_contract
/// (docs/design/runtime-orchestration-ssot.yaml dispatcher_contract): payload.target_ref may name
/// a manifest by hubs.topology_manifests.manifest_key ("manifest_key:{key}:{wiring_key}") instead
/// of a raw UUID ("manifest:{uuid}:{wiring_key}") — a route/entry point identifies its target
/// manifest by the same stable, already-existing, human-readable identity string every manifest in
/// the system already carries. Resolution uses the same "exactly one active row" fail-close
/// discipline as ContentBundleRepository.ResolveCanonicalDefaultEntryManifestIdAsync (see
/// ManifestDispatcherCanonicalDefaultEntryTests.cs for that sibling contract's own tests), then
/// flows into the IDENTICAL LoadByIdAsync/active-check pipeline the UUID-based target_ref path
/// already uses — these tests isolate the NEW key-resolution step only, not that shared downstream
/// pipeline (already covered elsewhere).
/// </summary>
public class ManifestDispatcherManifestKeyTargetRefTests
{
    private static readonly Guid ResolvedManifestId = new("00000000-0000-0000-0000-0000000fe001");

    // ui_projection entry required so a bare "projection_entry" target_ref (Layer=screen_list,
    // Action=Search) qualifies as IsGenericStructuralReadTargetRef -- matching the shape a real
    // team-dashboard-style projection manifest actually declares (see db/seed_empty.sql dd010/dd020).
    private static JsonElement[] BuildTopology() =>
    [
        JsonSerializer.SerializeToElement(new { type = "runtime_mapping", runtime_destination = "admin_runtime" }),
        JsonSerializer.SerializeToElement(new
        {
            type = "ui_projection",
            packageIds = new[] { Guid.NewGuid().ToString() },
            layoutId = Guid.NewGuid().ToString(),
            wiringId = Guid.NewGuid().ToString(),
            tensorId = Guid.NewGuid().ToString(),
        }),
    ];

    /// <summary>Axes resolution always misses; LoadByIdAsync resolves only the one fixture
    /// manifest id, with a caller-supplied status (default "active").</summary>
    private sealed class SingleManifestRepository(JsonElement[] topology, Guid manifestId, string status = "active")
        : ManifestRepository(NullLogger<ManifestRepository>.Instance)
    {
        public override Task<ManifestRecord?> LoadByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ManifestRecord?>(id == manifestId
                ? new ManifestRecord(manifestId, null, topology, status)
                : null);

        public override Task<ManifestRecord?> ResolveActiveManifestAsync(
            string? role, string? target, string? layer, string? action, CancellationToken ct = default) =>
            Task.FromResult<ManifestRecord?>(null);

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

    private static ManifestDispatcher BuildDispatcher(ManifestRepository manifestRepo, HubNavigationResolver? hubNavResolver) =>
        new(
            NullLogger<ManifestDispatcher>.Instance,
            new Dictionary<string, IDispatchableRuntime>
            {
                ["admin_runtime"] = new FakeDispatchableRuntime(JsonSerializer.SerializeToElement(new { ok = true })),
            },
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepo,
            errorAppender: null,
            topologyRepository: null,
            hubNavigationResolver: hubNavResolver);

    private static EndpointRequestDto BuildRequest(string targetRef)
    {
        var payload = JsonSerializer.SerializeToElement(new { target_ref = targetRef });
        return new EndpointRequestDto("Search", "default", "screen_list", "Search", null, payload, null, "client", "admin");
    }

    [Fact]
    public async Task DispatchAsync_ManifestKeyTargetRef_ExactlyOneActiveRow_ResolvesToThatManifest()
    {
        var manifestRepo = new SingleManifestRepository(BuildTopology(), ResolvedManifestId);
        var contentBundleRepo = new InMemoryContentBundleRepository();
        contentBundleRepo.ActiveManifestIdsByKey["team_dashboard.normal.projection"] = ResolvedManifestId;
        var dispatcher = BuildDispatcher(manifestRepo, new HubNavigationResolver(contentBundleRepo, manifestRepo));

        var response = await dispatcher.DispatchAsync(
            BuildRequest("manifest_key:team_dashboard.normal.projection:projection_entry"));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.Equal(ResolvedManifestId.ToString(), response.Emission!.ManifestId);
    }

    [Fact]
    public async Task DispatchAsync_ManifestKeyTargetRef_NoActiveRowForKey_ReturnsManifestNotFound_NoGuessing()
    {
        var manifestRepo = new SingleManifestRepository(BuildTopology(), ResolvedManifestId);
        var contentBundleRepo = new InMemoryContentBundleRepository();
        // Key deliberately left unregistered — mirrors zero active hubs.topology_manifests rows.
        var dispatcher = BuildDispatcher(manifestRepo, new HubNavigationResolver(contentBundleRepo, manifestRepo));

        var response = await dispatcher.DispatchAsync(
            BuildRequest("manifest_key:no_such_surface.projection:projection_entry"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    [Fact]
    public async Task DispatchAsync_ManifestKeyTargetRef_MultipleActiveRowsShareKey_ReturnsExplicitAmbiguousError_NoFirstMatch()
    {
        var manifestRepo = new SingleManifestRepository(BuildTopology(), ResolvedManifestId);
        var contentBundleRepo = new InMemoryContentBundleRepository();
        contentBundleRepo.AmbiguousManifestKeys.Add("team_dashboard.normal.projection");
        var dispatcher = BuildDispatcher(manifestRepo, new HubNavigationResolver(contentBundleRepo, manifestRepo));

        var response = await dispatcher.DispatchAsync(
            BuildRequest("manifest_key:team_dashboard.normal.projection:projection_entry"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_KEY_AMBIGUOUS");
    }

    [Fact]
    public async Task DispatchAsync_ManifestKeyTargetRef_EmptyKeySegment_ReturnsTargetRefInvalid()
    {
        var manifestRepo = new SingleManifestRepository(BuildTopology(), ResolvedManifestId);
        var dispatcher = BuildDispatcher(manifestRepo, new HubNavigationResolver(new InMemoryContentBundleRepository(), manifestRepo));

        var response = await dispatcher.DispatchAsync(BuildRequest("manifest_key::projection_entry"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_INVALID");
    }

    [Fact]
    public async Task DispatchAsync_ManifestKeyTargetRef_NoHubNavigationResolverConfigured_ReturnsManifestNotFound_NoThrow()
    {
        var manifestRepo = new SingleManifestRepository(BuildTopology(), ResolvedManifestId);
        // hubNavResolver deliberately null — mirrors an environment where it was never wired.
        var dispatcher = BuildDispatcher(manifestRepo, hubNavResolver: null);

        var response = await dispatcher.DispatchAsync(
            BuildRequest("manifest_key:team_dashboard.normal.projection:projection_entry"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    [Fact]
    public async Task DispatchAsync_UuidTargetRef_StillResolvesUnaffectedByManifestKeyAddition()
    {
        // Regression guard: the pre-existing "manifest:{uuid}:{wiring_key}" shape must remain
        // fully unaffected by the new manifest_key: branch.
        var manifestRepo = new SingleManifestRepository(BuildTopology(), ResolvedManifestId);
        var dispatcher = BuildDispatcher(manifestRepo, new HubNavigationResolver(new InMemoryContentBundleRepository(), manifestRepo));

        var response = await dispatcher.DispatchAsync(BuildRequest($"manifest:{ResolvedManifestId}:projection_entry"));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.Equal(ResolvedManifestId.ToString(), response.Emission!.ManifestId);
    }
}
