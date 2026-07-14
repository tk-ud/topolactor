using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// HubNavigationResolver.ResolveFallbackNavigationLinksAsync — the read-only navigation link-list
/// fallback for authenticated surfaces with no business projection of their own (e.g. Normal
/// Dashboard Home). Reuses the exact same two existing hub_relation authority calls
/// (ResolveCanonicalDefaultEntryManifestIdAsync + LoadHubNavigationSequenceAsync) RuntimeExecutor's
/// manifest-scoped NavigationSequence enrichment already relies on — no new parallel authority.
/// </summary>
public class HubNavigationFallbackLinksTests
{
    [Fact]
    public async Task NoCanonicalDefaultEntryConfigured_ReturnsExplicitEmptyList()
    {
        var repo = new InMemoryContentBundleRepository { CanonicalDefaultEntryManifestId = null };
        var resolver = new HubNavigationResolver(repo);

        var links = await resolver.ResolveFallbackNavigationLinksAsync();

        Assert.Empty(links);
    }

    [Fact]
    public async Task CanonicalDefaultEntryConfigured_ReturnsItsHubRelationSequence()
    {
        var repo = new InMemoryContentBundleRepository
        {
            CanonicalDefaultEntryManifestId = InMemoryContentBundleRepository.FixtureTopologyManifestId,
        };
        var resolver = new HubNavigationResolver(repo);

        var links = await resolver.ResolveFallbackNavigationLinksAsync();

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
        var resolver = new HubNavigationResolver(repo);

        var links = await resolver.ResolveFallbackNavigationLinksAsync();

        Assert.Empty(links);
    }
}
