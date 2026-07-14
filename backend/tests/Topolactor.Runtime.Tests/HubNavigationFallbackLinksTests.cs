using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// HubNavigationResolver.ResolveFallbackNavigationLinksAsync — the read-only navigation link-list
/// fallback for authenticated surfaces with no business projection of their own (e.g. Normal
/// Dashboard Home). Reuses the exact same two existing hub_relation authority calls
/// (ResolveCanonicalDefaultEntryManifestIdAsync + LoadHubNavigationSequenceAsync) RuntimeExecutor's
/// manifest-scoped NavigationSequence enrichment already relies on — no new parallel authority.
///
/// Subject/role isolation: there is no per-individual-user relation ownership table anywhere in
/// this schema (hubs.hub_relations is a manifest-scoped navigation sequence, not user-owned), so
/// the "subject membership" axis this fallback reuses is the existing manifest
/// capability_requirement/required_role authority (ManifestDispatcher.ValidateCapabilityRequirement)
/// — the same authority that already gates every admin_runtime manifest. A relation whose resolved
/// target manifest requires a role the caller does not have is excluded, never returned to every
/// authenticated subject uniformly.
/// </summary>
public class HubNavigationFallbackLinksTests
{
    [Fact]
    public async Task NoCanonicalDefaultEntryConfigured_ReturnsExplicitEmptyList()
    {
        var repo = new InMemoryContentBundleRepository { CanonicalDefaultEntryManifestId = null };
        var resolver = new HubNavigationResolver(repo, new RoleGatedFakeManifestRepository());

        var links = await resolver.ResolveFallbackNavigationLinksAsync("user");

        Assert.Empty(links);
    }

    [Fact]
    public async Task CanonicalDefaultEntryConfigured_ReturnsItsHubRelationSequence()
    {
        var repo = new InMemoryContentBundleRepository
        {
            CanonicalDefaultEntryManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId,
        };
        // Fixture's single relation's target manifest carries no capability_requirement -> visible
        // to any authenticated role, admin included.
        var resolver = new HubNavigationResolver(repo, new RoleGatedFakeManifestRepository());

        var links = await resolver.ResolveFallbackNavigationLinksAsync("user");

        var link = Assert.Single(links);
        Assert.Equal(InMemoryContentBundleRepository.FixtureRelatedHubId.ToString(), link.RelatedHubId);
        Assert.Equal(1, link.SequencePosition);
        Assert.Equal(InMemoryContentBundleRepository.FixtureRelatedHubManifestId.ToString(), link.TargetManifestId);
    }

    [Fact]
    public async Task CanonicalDefaultEntryConfigured_ButManifestHasNoActiveHubRelations_ReturnsExplicitEmptyList()
    {
        var repo = new InMemoryContentBundleRepository
        {
            // A manifest id that is not the fixture's -> no hub_relations rows match it.
            CanonicalDefaultEntryManifestId = InMemoryContentBundleRepository.FixtureRelatedHubManifestId,
        };
        var resolver = new HubNavigationResolver(repo, new RoleGatedFakeManifestRepository());

        var links = await resolver.ResolveFallbackNavigationLinksAsync("user");

        Assert.Empty(links);
    }

    [Fact]
    public async Task AdminOnlyTargetRelation_ExcludedForUserRoleCaller_IncludedForAdminRoleCaller()
    {
        var repo = new InMemoryContentBundleRepository();
        var manifestId = Guid.NewGuid();
        var publicHubId = Guid.NewGuid();
        var publicTargetManifestId = Guid.NewGuid();
        var adminHubId = Guid.NewGuid();
        var adminTargetManifestId = Guid.NewGuid();

        repo.CanonicalDefaultEntryManifestId = manifestId;
        repo.AddHubRelation(Guid.NewGuid(), manifestId, publicHubId, 1);
        repo.AddHubRelation(Guid.NewGuid(), manifestId, adminHubId, 2);
        repo.AddTopologyManifestHub(publicTargetManifestId, publicHubId);
        repo.AddTopologyManifestHub(adminTargetManifestId, adminHubId);

        var manifestRepo = new RoleGatedFakeManifestRepository();
        manifestRepo.SetRequiredRole(publicTargetManifestId, null);
        manifestRepo.SetRequiredRole(adminTargetManifestId, "admin");
        var resolver = new HubNavigationResolver(repo, manifestRepo);

        var userLinks = await resolver.ResolveFallbackNavigationLinksAsync("user");
        var adminLinks = await resolver.ResolveFallbackNavigationLinksAsync("admin");

        Assert.Single(userLinks);
        Assert.Equal(publicHubId.ToString(), userLinks[0].RelatedHubId);
        Assert.DoesNotContain(userLinks, l => l.RelatedHubId == adminHubId.ToString());

        Assert.Equal(2, adminLinks.Count);
        Assert.Contains(adminLinks, l => l.RelatedHubId == publicHubId.ToString());
        Assert.Contains(adminLinks, l => l.RelatedHubId == adminHubId.ToString());
    }

    [Fact]
    public async Task RelationWithNoResolvableTargetManifest_IsExcluded_NotNavigable()
    {
        // related_hub_id has zero or multiple active topology_manifests -> TargetManifestId is
        // null (per LoadHubNavigationSequenceAsync's no_implicit_fallback contract). A link with no
        // navigable target is not useful in a navigation link list and must not be included.
        var repo = new InMemoryContentBundleRepository();
        var manifestId = Guid.NewGuid();
        var orphanHubId = Guid.NewGuid();
        repo.CanonicalDefaultEntryManifestId = manifestId;
        repo.AddHubRelation(Guid.NewGuid(), manifestId, orphanHubId, 1);
        // Deliberately do not register any topology_manifest for orphanHubId.

        var resolver = new HubNavigationResolver(repo, new RoleGatedFakeManifestRepository());

        var links = await resolver.ResolveFallbackNavigationLinksAsync("admin");

        Assert.Empty(links);
    }

    [Fact]
    public async Task RelationWithNonNullTargetManifestIdButNoSuchManifestRow_IsExcluded_FailClosed()
    {
        // Distinct from the above: here TargetManifestId itself resolves to a non-null GUID (the
        // hub_navigation sequence layer found exactly one topology_manifest registered under the
        // related hub), but that manifest id names a row the ManifestRepository does not actually
        // have (ManifestRepository.LoadByIdAsync returns null — e.g. deleted/never-created row).
        // This must fail closed exactly like a null TargetManifestId, never be treated as "no
        // capability_requirement resolvable therefore visible to every caller".
        var repo = new InMemoryContentBundleRepository();
        var manifestId = Guid.NewGuid();
        var relatedHubId = Guid.NewGuid();
        var danglingTargetManifestId = Guid.NewGuid();
        repo.CanonicalDefaultEntryManifestId = manifestId;
        repo.AddHubRelation(Guid.NewGuid(), manifestId, relatedHubId, 1);
        repo.AddTopologyManifestHub(danglingTargetManifestId, relatedHubId);

        var manifestRepo = new RoleGatedFakeManifestRepository();
        manifestRepo.SetMissing(danglingTargetManifestId);
        var resolver = new HubNavigationResolver(repo, manifestRepo);

        var links = await resolver.ResolveFallbackNavigationLinksAsync("admin");

        Assert.Empty(links);
    }

    /// <summary>Test-only ManifestRepository double resolving required_role from a per-manifest
    /// capability_requirement map, mirroring the real capability_requirement topology entry shape
    /// ManifestDispatcher.ValidateCapabilityRequirement reads.</summary>
    private sealed class RoleGatedFakeManifestRepository : ManifestRepository
    {
        private readonly Dictionary<Guid, string?> _requiredRoleByManifestId = new();
        private readonly HashSet<Guid> _missingManifestIds = new();

        public RoleGatedFakeManifestRepository() : base(NullLogger<ManifestRepository>.Instance) { }

        public void SetRequiredRole(Guid manifestId, string? requiredRole) =>
            _requiredRoleByManifestId[manifestId] = requiredRole;

        /// <summary>Marks manifestId as a dangling reference: LoadByIdAsync returns null for it,
        /// simulating a TargetManifestId that names a row which does not actually exist.</summary>
        public void SetMissing(Guid manifestId) => _missingManifestIds.Add(manifestId);

        public override Task<ManifestRecord?> LoadByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (_missingManifestIds.Contains(id)) return Task.FromResult<ManifestRecord?>(null);
            var requiredRole = _requiredRoleByManifestId.TryGetValue(id, out var r) ? r : null;
            IReadOnlyList<JsonElement> topology = requiredRole is null
                ? []
                : [JsonSerializer.SerializeToElement(new { type = "capability_requirement", required_role = requiredRole })];
            return Task.FromResult<ManifestRecord?>(new ManifestRecord(id, null, topology, "active"));
        }

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
}
