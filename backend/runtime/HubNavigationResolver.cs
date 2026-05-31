using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Loads the hub_relation navigation sequence for a given hub from ContentBundleRepository.
/// Injected optionally into RuntimeExecutor so topology_transform_runtime can include
/// navigation hints in the emission without modifying the core attractor/schema pipeline.
/// Non-fatal: if resolution fails the emission proceeds without NavigationSequence.
/// </summary>
public class HubNavigationResolver
{
    private readonly ContentBundleRepository _contentBundleRepository;

    public HubNavigationResolver(ContentBundleRepository contentBundleRepository) =>
        _contentBundleRepository = contentBundleRepository;

    public Task<IReadOnlyList<HubNavigationSequenceItemDto>> ResolveAsync(Guid hubId, CancellationToken ct = default) =>
        _contentBundleRepository.LoadHubNavigationSequenceAsync(hubId, ct);
}
