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
}
