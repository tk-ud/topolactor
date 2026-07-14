using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Loads the manifest-scoped hub_relation navigation sequence from ContentBundleRepository.
/// Injected optionally into RuntimeExecutor so topology_transform_runtime can include
/// navigation hints in the emission without modifying the core attractor/schema pipeline.
/// Non-fatal: if resolution fails the emission proceeds without NavigationSequence.
/// </summary>
public class HubNavigationResolver
{
    private readonly ContentBundleRepository _contentBundleRepository;
    private readonly ManifestRepository _manifestRepository;

    public HubNavigationResolver(ContentBundleRepository contentBundleRepository, ManifestRepository manifestRepository)
    {
        _contentBundleRepository = contentBundleRepository;
        _manifestRepository = manifestRepository;
    }

    public Task<IReadOnlyList<HubNavigationSequenceItemDto>> ResolveAsync(Guid topologyManifestId, CancellationToken ct = default) =>
        _contentBundleRepository.LoadHubNavigationSequenceAsync(topologyManifestId, ct);

    /// <summary>
    /// Resolves the canonical default entry manifest — the means by which a bare/no-selection
    /// projection entry (no route, no manifest, no target_ref) resolves an initial manifest. See
    /// ContentBundleRepository.ResolveCanonicalDefaultEntryManifestIdAsync for the marker
    /// contract. Distinct from ResolveAsync, which resolves outbound navigation links from an
    /// ALREADY-resolved manifest.
    /// </summary>
    public Task<Guid?> ResolveCanonicalDefaultEntryManifestIdAsync(CancellationToken ct = default) =>
        _contentBundleRepository.ResolveCanonicalDefaultEntryManifestIdAsync(ct);

    /// <summary>
    /// Read-only navigation link-list fallback for authenticated surfaces with no business
    /// projection of their own: resolves the canonical default entry manifest, then its outbound
    /// hub_relations sequence — the exact same two existing hub_relation authority calls
    /// RuntimeExecutor's manifest-scoped NavigationSequence enrichment already makes, just without
    /// requiring an already-resolved manifestId from a prior dispatch. Returns an explicit empty
    /// list (never an error, never a fabricated placeholder) when no canonical default entry is
    /// configured or it has no active outbound relations.
    ///
    /// Subject/role isolation: every candidate link's TargetManifestId is resolved against the
    /// existing manifest capability_requirement/required_role authority
    /// (ManifestDispatcher.ResolveRequiredRole) — the same authority that already gates every
    /// admin_runtime manifest dispatch. A link is excluded (not returned to callerRole) when its
    /// target manifest requires a role callerRole does not hold, and excluded entirely when it has
    /// no resolvable TargetManifestId (not navigable) OR when TargetManifestId names a manifest row
    /// that does not actually exist (LoadByIdAsync returns null) — a dangling relation is not
    /// treated as "no capability requirement = visible to everyone"; it must fail closed exactly
    /// like a relation with no TargetManifestId at all. This is deliberately NOT a uniform
    /// return-everything-to-every-authenticated-subject projection: the canonical default manifest's
    /// outbound relations are per-target-role filtered, reusing existing capability authority rather
    /// than any new per-user membership table (none exists in this schema) or a JWT-role-only rule.
    /// </summary>
    public async Task<IReadOnlyList<HubNavigationSequenceItemDto>> ResolveFallbackNavigationLinksAsync(
        string? callerRole, CancellationToken ct = default)
    {
        var manifestId = await ResolveCanonicalDefaultEntryManifestIdAsync(ct);
        if (manifestId is null) return [];

        var candidates = await ResolveAsync(manifestId.Value, ct);
        if (candidates.Count == 0) return [];

        var visible = new List<HubNavigationSequenceItemDto>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (candidate.TargetManifestId is null || !Guid.TryParse(candidate.TargetManifestId, out var targetManifestId))
                continue; // not navigable — no resolvable target manifest

            var targetManifest = await _manifestRepository.LoadByIdAsync(targetManifestId, ct);
            if (targetManifest is null)
                continue; // TargetManifestId names a row that does not exist — fail closed, not "no requirement"

            var requiredRole = ManifestDispatcher.ResolveRequiredRole(targetManifest.Topology);
            if (requiredRole is not null && !string.Equals(requiredRole, callerRole, StringComparison.Ordinal))
                continue; // caller's role does not satisfy the target manifest's capability_requirement

            visible.Add(candidate);
        }

        return visible;
    }
}
