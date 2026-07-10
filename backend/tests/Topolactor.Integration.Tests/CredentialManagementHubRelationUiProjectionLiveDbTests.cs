using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for the credential-management hub relation / navigation / ui_projection bundle:
/// - manifest 092 (auth.external.credential_management.projection) is registered in
///   hubs.topology_manifests and topology.physical_table_manifest_bindings (existing seed, not
///   newly introduced by this bundle — asserted here as a precondition).
/// - hubs.hub_relations rows for manifest 092 resolve via NpgsqlContentBundleRepository's
///   LoadHubNavigationSequenceAsync SQL (the real query ManifestDispatcher's hub-navigation
///   enrichment calls through HubNavigationResolver), including targetManifestId resolution
///   (exactly-one-manifest-per-hub, fail-closed to null otherwise — no implicit fallback).
/// - manifest 092's manifest.topology[ui_projection].layoutId resolves to real
///   topology.ui_topology_tensor / topology.ui_wiring_registry rows via
///   TopologyRepository.LoadLayoutNodesAsync — the real render-reachability proof, not a
///   refs-only-shape-exists assumption.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires
/// db/seed_empty.sql applied to the target database (manifest 092 + its ui_projection rows).
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class CredentialManagementHubRelationUiProjectionLiveDbTests
{
    private static readonly Guid CredentialManagementManifestId =
        new("00000000-0000-0000-0000-000000000092");
    private static readonly Guid CredentialManagementLayoutId =
        new("00000000-0000-0000-0000-0000000cd002");

    [Fact]
    public async Task Manifest092_IsRegisteredInTopologyManifestsAndPhysicalTableBindings()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT hub_id::text, manifest_key, status FROM hubs.topology_manifests " +
                "WHERE topology_manifest_id = @id";
            cmd.Parameters.AddWithValue("id", CredentialManagementManifestId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "manifest 092 must be registered in hubs.topology_manifests");
            Assert.Equal("auth.external.credential_management.projection", reader.GetString(1));
            Assert.Equal("active", reader.GetString(2));
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT COUNT(*)::int FROM topology.physical_table_manifest_bindings " +
                "WHERE topology_manifest_id = @id AND active = true";
            cmd.Parameters.AddWithValue("id", CredentialManagementManifestId);
            var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            Assert.True(count > 0, "manifest 092 must have active physical_table_manifest_bindings rows");
        }
    }

    [Fact]
    public async Task Manifest092_UiProjectionLayoutId_ResolvesRealLayoutNodesViaTopologyRepository()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var repo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);

        var nodes = await repo.LoadLayoutNodesAsync(CredentialManagementLayoutId);

        Assert.NotEmpty(nodes);
        Assert.Contains(nodes, n => n.NodeId == "instance_settings_import_form");
        // wiring join (topology.ui_wiring_registry) resolved through the tensor row, not absent.
        Assert.Contains(nodes, n => n.WiringKind == "instance_settings_action_bundle");
    }

    [Fact]
    public async Task LoadHubNavigationSequenceAsync_ResolvesTargetManifestId_OnlyWhenExactlyOneManifestPerHub()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var singleHubId = Guid.NewGuid();
        var singleManifestId = Guid.NewGuid();
        var ambiguousHubId = Guid.NewGuid();
        var ambiguousManifestIdA = Guid.NewGuid();
        var ambiguousManifestIdB = Guid.NewGuid();
        var hubRelationSuffix = Guid.NewGuid().ToString("N")[..8];

        // LoadHubNavigationSequenceAsync opens its own connection, so setup/teardown must be
        // committed (not left in an uncommitted transaction) for it to observe the rows.
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
            await ExecAsync(
                "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)",
                ("id", singleHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) " +
                "VALUES (@mid, @hid, @key, 'active')",
                ("mid", singleManifestId), ("hid", singleHubId), ("key", $"live-db-test-single-{hubRelationSuffix}"));

            await ExecAsync(
                "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)",
                ("id", ambiguousHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) " +
                "VALUES (@mid, @hid, @key, 'active')",
                ("mid", ambiguousManifestIdA), ("hid", ambiguousHubId), ("key", $"live-db-test-ambig-a-{hubRelationSuffix}"));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) " +
                "VALUES (@mid, @hid, @key, 'active')",
                ("mid", ambiguousManifestIdB), ("hid", ambiguousHubId), ("key", $"live-db-test-ambig-b-{hubRelationSuffix}"));

            await ExecAsync(
                "INSERT INTO hubs.hub_relations (topology_manifest_id, related_hub_id, sequence_position, status) " +
                "VALUES (@mid, @hid, 9001, 'active')",
                ("mid", CredentialManagementManifestId), ("hid", singleHubId));
            await ExecAsync(
                "INSERT INTO hubs.hub_relations (topology_manifest_id, related_hub_id, sequence_position, status) " +
                "VALUES (@mid, @hid, 9002, 'active')",
                ("mid", CredentialManagementManifestId), ("hid", ambiguousHubId));

            var repo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
            var items = await repo.LoadHubNavigationSequenceAsync(CredentialManagementManifestId);

            var singleItem = Assert.Single(items, i => i.RelatedHubId == singleHubId.ToString());
            Assert.Equal(singleManifestId.ToString(), singleItem.TargetManifestId);

            var ambiguousItem = Assert.Single(items, i => i.RelatedHubId == ambiguousHubId.ToString());
            Assert.Null(ambiguousItem.TargetManifestId);
        }
        finally
        {
            await ExecAsync(
                "DELETE FROM hubs.hub_relations WHERE topology_manifest_id = @mid AND related_hub_id IN (@h1, @h2)",
                ("mid", CredentialManagementManifestId), ("h1", singleHubId), ("h2", ambiguousHubId));
            await ExecAsync(
                "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2, @m3)",
                ("m1", singleManifestId), ("m2", ambiguousManifestIdA), ("m3", ambiguousManifestIdB));
            await ExecAsync(
                "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2)",
                ("h1", singleHubId), ("h2", ambiguousHubId));
        }
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
