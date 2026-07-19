using Npgsql;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for the admin-dashboard subBundle seed (admin-surface-topology-seed-conversion,
/// .agent/tasks/todo.md), db/seed_empty.sql manifest 00000000-0000-0000-0000-0000000ad200
/// ("admin.dashboard.navigation.projection"). Reuses
/// HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync (manifest-agnostic) the
/// same way CredentialManagementHubRelationUiProjectionLiveDbTests does for manifest 092.
///
/// Scope: this manifest owns zero hubs.hub_relations rows of its own (see the seed's header
/// comment in db/seed_empty.sql) -- authoring a relation is an ordinary /admin/manifests action,
/// not seed content. This proof only asserts: (1) the manifest is renderable via the real
/// admin_runtime structural-render fallback with its SSOT component_tree (card_list.primitive /
/// panel.alias) fully resolved, and (2) once a relation IS authored
/// (inserted here as ordinary test setup, mirroring how an admin would author it), the
/// card_list's items propBinding (emission.navigationSequence -> navigationLinksToCardItems)
/// reflects it -- proving the frontend/backend propBindingResolver.ts /
/// StructureMapResolver.cs extension this Bundle added actually reaches real seeded content,
/// not just unit-level fixtures.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires db/seed_empty.sql
/// applied to the target database.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class AdminDashboardNavigationUiProjectionLiveDbTests
{
    private static readonly Guid AdminDashboardNavigationManifestId =
        new("00000000-0000-0000-0000-0000000ad200");

    [Fact]
    public async Task DispatchAsync_AdminDashboardNavigationManifest_ResolvesSsotComponentTree_NoUnresolvedLeaves()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminDashboardNavigationManifestId}:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        var emission = response.Emission!;

        Assert.Equal(AdminDashboardNavigationManifestId.ToString(), emission.ManifestId);
        Assert.NotNull(emission.LayoutNodes);
        var nodes = emission.LayoutNodes!;

        var shell = Assert.Single(nodes, n => n.NodeId == "target_projection_shell");
        Assert.Equal("catalog_component", shell.NodeKind);
        Assert.Equal("disclosure_structure/panel", shell.ComponentKind);
        Assert.NotNull(shell.ComponentId);

        var list = Assert.Single(nodes, n => n.NodeId == "hub_relation_link_list");
        Assert.Equal("display/card_list", list.ComponentKind);
        Assert.NotNull(list.ComponentId);
        Assert.Equal("target_projection_shell", list.ParentNodeId);

        // render completion: every catalog_component leaf resolved a componentId.
        var unresolvedLeaves = nodes
            .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
            .ToList();
        Assert.Empty(unresolvedLeaves);

        Assert.Empty(emission.Errors);
    }

    [Fact]
    public async Task DispatchAsync_AdminDashboardNavigationManifest_CardListItemsReflectRealAuthoredHubRelation()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var relationUuid = Guid.NewGuid();
        var relatedHubId = Guid.NewGuid();
        var relatedManifestId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            // Ordinary admin/runtime authoring of a hub_relation FROM this manifest -- exactly
            // what an admin would do via /admin/manifests, not seed content
            // (db/seed_empty.sql's admin-dashboard block intentionally carries none).
            await ExecAsync(
                "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)",
                ("id", relatedHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) " +
                "VALUES (@mid, @hid, @key, 'active')",
                ("mid", relatedManifestId), ("hid", relatedHubId), ("key", $"live-db-admin-dashboard-nav-{suffix}"));
            await ExecAsync(
                "INSERT INTO hubs.hub_relations (hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, status) " +
                "VALUES (@rid, @mid, @hid, 1, 'active')",
                ("rid", relationUuid), ("mid", AdminDashboardNavigationManifestId), ("hid", relatedHubId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminDashboardNavigationManifestId}:projection_entry",
            });
            var request = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

            var response = await dispatcher.DispatchAsync(request);

            Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.NotNull(response.Emission);
            var emission = response.Emission!;

            // "current hub relation" candidates resolved for this manifest -- backend authority
            // (ManifestDispatcher.EnrichWithHubNavigationAsync). The frontend's resolution of
            // this into card_list.items (propBindingResolver.ts EMISSION_NAVIGATION_SEQUENCE_SOURCE
            // + navigationLinksToCardItems) is a pure frontend concern, covered by
            // frontend/tests/propBindingResolver.test.ts; this backend proof only needs to confirm
            // NavigationSequence itself is real and that the node's authored propBindings
            // descriptor (what the frontend will resolve) is exactly what the seed configured.
            Assert.NotNull(emission.NavigationSequence);
            var navItem = Assert.Single(emission.NavigationSequence!, n => n.RelatedHubId == relatedHubId.ToString());
            Assert.Equal(relatedManifestId.ToString(), navItem.TargetManifestId);
            // selected_link_payload_required (admin-normal-surface-projection-seed-ssot.yaml):
            // hub_relation_id / source topology_manifest_id / related_hub_id all resolve.
            Assert.Equal(relationUuid.ToString(), navItem.HubRelationId);
            Assert.Equal(AdminDashboardNavigationManifestId.ToString(), navItem.TopologyManifestId);

            var list = Assert.Single(emission.LayoutNodes!, n => n.NodeId == "hub_relation_link_list");
            Assert.NotNull(list.PropBindings);
            var propBindings = list.PropBindings!.Value;
            Assert.True(propBindings.TryGetProperty("items", out var itemsBinding));
            Assert.Equal("emission.navigationSequence", itemsBinding.GetProperty("source").GetString());
            Assert.Equal("navigationLinksToCardItems", itemsBinding.GetProperty("transform").GetString());
        }
        finally
        {
            await ExecAsync("DELETE FROM hubs.hub_relations WHERE hub_relation_id = @rid", ("rid", relationUuid));
            await ExecAsync(
                "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @mid", ("mid", relatedManifestId));
            await ExecAsync("DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", relatedHubId));
        }
    }

    [Fact]
    public async Task AdminDashboardNavigationManifest_OwnsNoHubRelationsRows_SeedOnly()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*)::int FROM hubs.hub_relations WHERE topology_manifest_id = @id";
        cmd.Parameters.AddWithValue("id", AdminDashboardNavigationManifestId);
        var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);

        Assert.Equal(
            0,
            count);
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
