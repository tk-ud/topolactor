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

    public HubNavigationResolver(ContentBundleRepository contentBundleRepository) =>
        _contentBundleRepository = contentBundleRepository;

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
    /// </summary>
    public async Task<IReadOnlyList<HubNavigationSequenceItemDto>> ResolveFallbackNavigationLinksAsync(
        CancellationToken ct = default)
    {
        var manifestId = await ResolveCanonicalDefaultEntryManifestIdAsync(ct);
        if (manifestId is null) return [];
        return await ResolveAsync(manifestId.Value, ct);
    }
}
