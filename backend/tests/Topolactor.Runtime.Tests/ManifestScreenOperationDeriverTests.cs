using Topolactor.Repository;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ManifestScreenOperationDeriverTests
{
    [Theory]
    [InlineData("list", "Read", "entity")]
    [InlineData("search", "Search", "entity")]
    [InlineData("aggregation_view", "Read", "aggregation")]
    public void TryDeriveAxes_KnownKinds_ReturnsExpected(string kind, string action, string layer)
    {
        var ok = ManifestScreenOperationDeriver.TryDeriveAxes(
            kind, out _, out _, out var derivedLayer, out var derivedAction, out var runtime);

        Assert.True(ok);
        Assert.Equal(layer, derivedLayer);
        Assert.Equal(action, derivedAction);
        Assert.Equal("topology_transform_runtime", runtime);
    }

    [Fact]
    public void TryDeriveAxes_Unknown_ReturnsFalse()
    {
        Assert.False(ManifestScreenOperationDeriver.TryDeriveAxes(
            "unknown", out _, out _, out _, out _, out _));
    }
}
