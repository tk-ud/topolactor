using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies the canonical default entry fallback in ManifestDispatcher.DispatchAsync: a bare
/// projection entry (frontend/runtime/projectionEntry.ts resolveProjectionEntryAxes({}) — no
/// route, no explicit manifest, no pre-injected payload.target_ref — sends exactly
/// target="default" layer="screen_list" action="Search") resolves an initial manifest via the
/// hubs.hub_relations row explicitly marked relation_config.transition="canonical_default_entry"
/// (ContentBundleRepository.ResolveCanonicalDefaultEntryManifestIdAsync), distinct from
/// NavigationSequence's outbound "current hub relation" links from an already-resolved manifest.
/// </summary>
public class ManifestDispatcherCanonicalDefaultEntryTests
{
    private static readonly Guid CanonicalManifestId = new("00000000-0000-0000-0000-000000000092");

    private static JsonElement[] BuildTopology() =>
    [
        JsonSerializer.SerializeToElement(new { type = "runtime_mapping", runtime_destination = "admin_runtime" }),
    ];

    /// <summary>Axes resolution always misses (mirrors "no dispatcher_mapping seeded for the bare
    /// default entry"); LoadByIdAsync resolves only the canonical manifest id, with a
    /// caller-supplied status (default "active").</summary>
    private sealed class NoAxesMatchManifestRepository(JsonElement[] topology, Guid canonicalManifestId, string status = "active")
        : ManifestRepository(NullLogger<ManifestRepository>.Instance)
    {
        public override Task<ManifestRecord?> LoadByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ManifestRecord?>(id == canonicalManifestId
                ? new ManifestRecord(canonicalManifestId, null, topology, status)
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

    private static EndpointRequestDto DefaultEntryRequest() =>
        new("Search", "default", "screen_list", "Search", null, null, null, "client", "admin");

    private ManifestDispatcher BuildDispatcher(
        ManifestRepository manifestRepo,
        HubNavigationResolver? hubNavResolver,
        IDispatchableRuntime? adminHandler = null)
    {
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = adminHandler ?? new FakeDispatchableRuntime(JsonSerializer.SerializeToElement(new { ok = true })),
        };
        return new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            RuntimeExecutorTests.CreateTargetDispatchOverride(),
            manifestRepo,
            errorAppender: null,
            topologyRepository: null,
            hubNavigationResolver: hubNavResolver);
    }

    [Fact]
    public async Task DispatchAsync_BareDefaultEntry_NoAxesMatch_ResolvesCanonicalDefaultEntryManifest()
    {
        var manifestRepo = new NoAxesMatchManifestRepository(BuildTopology(), CanonicalManifestId);
        var contentBundleRepo = new InMemoryContentBundleRepository
        {
            CanonicalDefaultEntryManifestId = CanonicalManifestId,
        };
        var hubNavResolver = new HubNavigationResolver(contentBundleRepo, manifestRepo);
        var dispatcher = BuildDispatcher(manifestRepo, hubNavResolver);

        var response = await dispatcher.DispatchAsync(DefaultEntryRequest());

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        Assert.Equal(CanonicalManifestId.ToString(), response.Emission!.ManifestId);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("deprecated")]
    public async Task DispatchAsync_BareDefaultEntry_MarkedRelationNamesInactiveManifest_ResolvesToNoManifest_NoStaleProjection(string inactiveStatus)
    {
        // The relation row itself is active (marked canonical_default_entry), but the manifest it
        // names is draft/deprecated — active_status_requirement must fail this closed, never
        // resolve a stale/inactive projection.
        var manifestRepo = new NoAxesMatchManifestRepository(BuildTopology(), CanonicalManifestId, status: inactiveStatus);
        var contentBundleRepo = new InMemoryContentBundleRepository
        {
            CanonicalDefaultEntryManifestId = CanonicalManifestId,
        };
        var hubNavResolver = new HubNavigationResolver(contentBundleRepo, manifestRepo);
        var dispatcher = BuildDispatcher(manifestRepo, hubNavResolver);

        var response = await dispatcher.DispatchAsync(DefaultEntryRequest());

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    [Fact]
    public async Task DispatchAsync_BareDefaultEntry_NoCanonicalDefaultEntryConfigured_StaysManifestNotFound_NoGuessing()
    {
        var manifestRepo = new NoAxesMatchManifestRepository(BuildTopology(), CanonicalManifestId);
        // CanonicalDefaultEntryManifestId left null — no marked row exists.
        var hubNavResolver = new HubNavigationResolver(new InMemoryContentBundleRepository(), manifestRepo);
        var dispatcher = BuildDispatcher(manifestRepo, hubNavResolver);

        var response = await dispatcher.DispatchAsync(DefaultEntryRequest());

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    [Fact]
    public async Task DispatchAsync_NonDefaultEntryAxes_DoesNotTriggerCanonicalDefaultEntryFallback()
    {
        // Same manifest/hub_relations fixture, but axes are NOT the exact bare-default-entry
        // combination (layer differs) — the fallback must stay narrowly scoped, never a general
        // MANIFEST_NOT_FOUND rescue for arbitrary unmatched axes.
        var manifestRepo = new NoAxesMatchManifestRepository(BuildTopology(), CanonicalManifestId);
        var contentBundleRepo = new InMemoryContentBundleRepository
        {
            CanonicalDefaultEntryManifestId = CanonicalManifestId,
        };
        var hubNavResolver = new HubNavigationResolver(contentBundleRepo, manifestRepo);
        var dispatcher = BuildDispatcher(manifestRepo, hubNavResolver);

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null, "client", "admin");
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    [Fact]
    public async Task DispatchAsync_ExplicitTargetRef_IsUnaffectedByCanonicalDefaultEntryFallback()
    {
        // An explicit ?manifest= selection (payload.target_ref present) must resolve via the
        // existing target_ref path — the canonical default entry fallback must never be
        // consulted, and must never override an explicit selection.
        var otherManifestId = Guid.NewGuid();
        var manifestRepo = new NoAxesMatchManifestRepository(
            [JsonSerializer.SerializeToElement(new { type = "runtime_mapping", runtime_destination = "admin_runtime" })],
            otherManifestId);
        var contentBundleRepo = new InMemoryContentBundleRepository
        {
            CanonicalDefaultEntryManifestId = CanonicalManifestId,
        };
        var hubNavResolver = new HubNavigationResolver(contentBundleRepo, manifestRepo);
        var dispatcher = BuildDispatcher(manifestRepo, hubNavResolver);

        var payload = JsonSerializer.SerializeToElement(new { target_ref = $"manifest:{otherManifestId}:projection_entry" });
        var request = new EndpointRequestDto("Search", "default", "screen_list", "Search", null, payload, null, "client", "admin");
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.Equal(otherManifestId.ToString(), response.Emission!.ManifestId);
    }

    /// <summary>Content bundle repository double whose canonical-default-entry lookup always
    /// reports ambiguity — exercises ManifestDispatcher's CANONICAL_DEFAULT_ENTRY_AMBIGUOUS catch.</summary>
    private sealed class AmbiguousCanonicalDefaultEntryContentBundleRepository()
        : InMemoryContentBundleRepository
    {
        public override Task<Guid?> ResolveCanonicalDefaultEntryManifestIdAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException(
                "CANONICAL_DEFAULT_ENTRY_AMBIGUOUS: multiple hubs.hub_relations rows are marked " +
                "relation_config.transition='canonical_default_entry'.");
    }

    [Fact]
    public async Task DispatchAsync_BareDefaultEntry_AmbiguousCanonicalDefaultEntry_ReturnsExplicitError_NoSilentPick()
    {
        var manifestRepo = new NoAxesMatchManifestRepository(BuildTopology(), CanonicalManifestId);
        var hubNavResolver = new HubNavigationResolver(new AmbiguousCanonicalDefaultEntryContentBundleRepository(), manifestRepo);
        var dispatcher = BuildDispatcher(manifestRepo, hubNavResolver);

        var response = await dispatcher.DispatchAsync(DefaultEntryRequest());

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CANONICAL_DEFAULT_ENTRY_AMBIGUOUS");
    }
}
