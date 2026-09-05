using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// hubs.topology_manifests.topology_jsonb is read by 3 real, live writer shapes -- the
/// authoring/promote entries-wrapper shape, the preset-seed direct-key merge shape (see
/// db/email_approval_form_preset_seed.sql), and a bare entries array (some seed rows via
/// to_jsonb(m.topology)) -- plus an unrelated ad-hoc object that must yield null, never an
/// exception or a false-positive match. This backs the /admin/manifests + hub_navigation
/// production visibleName projection (docs/design/admin-console-workflow-ssot.yaml
/// topology_naming_ssot.user_facing_topology_label.display_rule).
/// </summary>
public class ScreenDataShapeTopologyReaderHubNavigationJsonbTests
{
    [Fact]
    public void FindScreenDataShapeEntryFromTopologyManifestJsonb_EntriesWrapperShape_Resolves()
    {
        var json = """
        {"manifest_id": "11111111-1111-1111-1111-111111111111", "entries": [
            {"type": "hub_grouping", "manifestKey": "orders.list"},
            {"type": "screen_data_shape", "topologySystemName": "orders-list", "userFacingTopologyLabel": "受注一覧"}
        ]}
        """;

        var entry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntryFromTopologyManifestJsonb(json);

        Assert.NotNull(entry);
        Assert.Equal("orders-list", ScreenDataShapeTopologyReader.ExtractStringProperty(entry!.Value, "topologySystemName"));
        Assert.Equal("受注一覧", ScreenDataShapeTopologyReader.ExtractStringProperty(entry!.Value, "userFacingTopologyLabel"));
    }

    [Fact]
    public void FindScreenDataShapeEntryFromTopologyManifestJsonb_DirectKeyMergeShape_Resolves()
    {
        // Mirrors db/email_approval_form_preset_seed.sql's
        // `topology_jsonb || jsonb_build_object('screen_data_shape', '{...}'::jsonb)` merge.
        var json = """
        {"screen_data_shape": {"type": "screen_data_shape", "topologySystemName": "email-approval-form-seed", "userFacingTopologyLabel": "Email Approval & Delivery"}}
        """;

        var entry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntryFromTopologyManifestJsonb(json);

        Assert.NotNull(entry);
        Assert.Equal("email-approval-form-seed", ScreenDataShapeTopologyReader.ExtractStringProperty(entry!.Value, "topologySystemName"));
        Assert.Equal("Email Approval & Delivery", ScreenDataShapeTopologyReader.ExtractStringProperty(entry!.Value, "userFacingTopologyLabel"));
    }

    [Fact]
    public void FindScreenDataShapeEntryFromTopologyManifestJsonb_BareEntriesArrayShape_Resolves()
    {
        var json = """
        [{"type": "screen_data_shape", "topologySystemName": "scheduler-settings-projection"}]
        """;

        var entry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntryFromTopologyManifestJsonb(json);

        Assert.NotNull(entry);
        Assert.Equal("scheduler-settings-projection", ScreenDataShapeTopologyReader.ExtractStringProperty(entry!.Value, "topologySystemName"));
        Assert.Null(ScreenDataShapeTopologyReader.ExtractStringProperty(entry!.Value, "userFacingTopologyLabel"));
    }

    [Theory]
    [InlineData("""{"demo": true}""")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not valid json")]
    public void FindScreenDataShapeEntryFromTopologyManifestJsonb_UnrelatedOrMalformedShape_ReturnsNull(string? json)
    {
        var entry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntryFromTopologyManifestJsonb(json);

        Assert.Null(entry);
    }
}
