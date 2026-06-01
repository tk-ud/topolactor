using Topolactor.Repository;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ManifestScreenOperationDeriverTests
{
    private static readonly Guid ManifestA = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid ManifestB = Guid.Parse("22222222-2222-2222-2222-222222222202");

    [Theory]
    [InlineData("list", "screen_list", "Read")]
    [InlineData("search", "screen_entity", "Search")]
    [InlineData("detail", "screen_detail", "Read")]
    [InlineData("aggregation_view", "screen_aggregation", "Read")]
    public void TryDeriveAxes_KnownKinds_UseManifestScopedLayers(
        string kind, string expectedLayer, string expectedAction)
    {
        var ok = ManifestScreenOperationDeriver.TryDeriveAxes(
            kind,
            "orders.list",
            ManifestA,
            out _,
            out var target,
            out var layer,
            out var action,
            out var runtime);

        Assert.True(ok);
        Assert.Equal("orders.list", target);
        Assert.Equal(expectedLayer, layer);
        Assert.Equal(expectedAction, action);
        Assert.Equal("topology_transform_runtime", runtime);
    }

    [Fact]
    public void TryDeriveAxes_ListAndDetail_SameManifestKey_UseDistinctDispatcherLayers()
    {
        Assert.True(ManifestScreenOperationDeriver.TryDeriveAxes(
            "list", "orders", ManifestA, out _, out _, out var listLayer, out _, out _));
        Assert.True(ManifestScreenOperationDeriver.TryDeriveAxes(
            "detail", "orders", ManifestA, out _, out _, out var detailLayer, out _, out _));

        Assert.NotEqual(listLayer, detailLayer);
    }

    [Fact]
    public void TryDeriveAxes_TwoListScreens_DifferentManifestKeys_DoNotShareDefaultEntityRead()
    {
        Assert.True(ManifestScreenOperationDeriver.TryDeriveAxes(
            "list", "screen_a", ManifestA, out var r1, out var t1, out var l1, out var a1, out _));
        Assert.True(ManifestScreenOperationDeriver.TryDeriveAxes(
            "list", "screen_b", ManifestB, out var r2, out var t2, out var l2, out var a2, out _));

        Assert.Equal(r1, r2);
        Assert.Equal("Read", a1);
        Assert.Equal("Read", a2);
        Assert.Equal("screen_list", l1);
        Assert.Equal("screen_list", l2);
        Assert.NotEqual(t1, t2);
        Assert.NotEqual("default", t1);
        Assert.NotEqual("entity", l1);
    }

    [Fact]
    public void TryDeriveAxes_WithoutManifestKey_UsesManifestIdTarget()
    {
        Assert.True(ManifestScreenOperationDeriver.TryDeriveAxes(
            "list", null, ManifestA, out _, out var target, out _, out _, out _));

        Assert.Equal($"screen_{ManifestA:N}", target);
    }

    [Fact]
    public void TryDeriveAxes_Unknown_ReturnsFalse()
    {
        Assert.False(ManifestScreenOperationDeriver.TryDeriveAxes(
            "unknown", "x", ManifestA, out _, out _, out _, out _, out _));
    }
}
