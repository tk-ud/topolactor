using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB E2E proof for the credential-management hub relation / navigation / ui_projection
/// bundle, per runtime-orchestration-ssot.yaml dispatcher_contract.
/// ui_projection_render_reachability_contract.test_proof_contract: dispatch through the REAL
/// ManifestDispatcher.DispatchAsync entry point (not a hardcoded layoutId constant fed directly
/// into TopologyRepository.LoadLayoutNodesAsync), using real Npgsql repositories, walking the
/// full relation-vector -> scalar-Emission chain from an explicit
/// relation_uuid / hub_ids[] / package_ids[] test input:
///
///   relation_uuid (hubs.hub_relations.hub_relation_id)
///   -> source topology_manifest (manifest 092, via hub_relations.topology_manifest_id)
///   -> hub_ids[] (hubs.hub_relations.related_hub_id, ordered by sequence_position — the
///      manifest-scoped relation vector / route vector, not a global hub-to-hub graph)
///   -> package_ids[] (manifest.topology[ui_projection].packageIds, read from the manifest
///      topology just loaded from the DB, not a test constant)
///   -> topology.components_package_design / topology.components_layout_design /
///      topology.ui_wiring_registry / topology.ui_topology_tensor
///      (TopologyRepository.LoadLayoutNodesAsync)
///   -> backend dispatch (ManifestDispatcher.DispatchAsync)
///   -> scalar Emission.ManifestId / NavigationSequence / PackageId / LayoutId / LayoutNodes,
///      with NavigationSequence[].RelatedHubId observably corresponding to hub_ids[] and
///      NavigationSequence[].TargetManifestId resolved via the exactly-one-ACTIVE-manifest rule
///
/// The admin_runtime leg uses a REAL AdminRuntime (minimal constructor, same pattern as
/// AdminRuntimeDbProjectionScenarioTests.StartAdminDispatchRouteAsync) so the
/// ADMIN_OPERATION_NOT_FOUND -> structural-render-only-entry fallback in ManifestDispatcher is
/// exercised against AdminRuntime's actual ExecuteDataAsync switch statement, not a stub that
/// merely assumes that behavior.
///
/// The frontend counterpart of this round-trip (NavigationSequence[].TargetManifestId ->
/// resolveHubNavigationLinks -> ?manifest=<uuid> -> parseProjectionEntrySelection ->
/// resolveProjectionEntryAxes -> payload.target_ref) is proven in
/// frontend/tests/projectionEntry.test.ts ("hub navigation round-trip: ...").
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires
/// db/seed_empty.sql applied to the target database (manifest 092 + its ui_projection rows).
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class CredentialManagementHubRelationUiProjectionLiveDbTests
{
    private static readonly Guid CredentialManagementManifestId =
        new("00000000-0000-0000-0000-000000000092");
    private static readonly Guid CredentialManagementPackageId =
        new("00000000-0000-0000-0000-0000000cd005");

    private static async Task<ManifestDispatcher> BuildRealDispatcherAsync(string cs)
    {
        var uiRepo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, cs);
        var topoRepo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, cs);
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var adminRuntime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            topologyRepository: topoRepo);
        var adminAdapter = new AdminRuntimeDispatchAdapter(adminRuntime, new OperationVectorResolver());

        var targetOverride = new TargetDispatchOverride(NullLogger<TargetDispatchOverride>.Instance, adminRuntime);
        var manifestRepo = new NpgsqlManifestRepository(NullLogger<NpgsqlManifestRepository>.Instance, cs);
        var contentBundleRepo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
        var hubNavResolver = new HubNavigationResolver(contentBundleRepo);

        return await Task.FromResult(new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            new Dictionary<string, IDispatchableRuntime>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin_runtime"] = adminAdapter,
            },
            new OperationVectorResolver(),
            targetOverride,
            manifestRepo,
            errorAppender: null,
            topologyRepository: topoRepo,
            hubNavigationResolver: hubNavResolver));
    }

    [Fact]
    public async Task DispatchAsync_CredentialManagementManifest_E2E_RelationVectorToScalarEmission()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        // Explicit relation-vector test input, per runtime-orchestration-ssot.yaml
        // dispatcher_contract.ui_projection_render_reachability_contract.test_proof_contract.
        // test_input_shape: relation_uuid / hub_ids[] / package_ids[].
        var relationUuid = Guid.NewGuid();
        var relatedHubId = Guid.NewGuid();
        var relatedManifestId = Guid.NewGuid();
        var hubIds = new[] { relatedHubId };
        var packageIds = new[] { CredentialManagementPackageId };
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
            // hub_relation(relation_uuid) whose source topology_manifest is manifest 092, whose
            // related hub has exactly one active topology_manifest — the relation vector this
            // proof walks: relation_uuid -> hub_ids[] -> package_ids[] -> scalar Emission.
            await ExecAsync(
                "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)",
                ("id", relatedHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) " +
                "VALUES (@mid, @hid, @key, 'active')",
                ("mid", relatedManifestId), ("hid", relatedHubId), ("key", $"live-db-relation-vector-{suffix}"));
            await ExecAsync(
                "INSERT INTO hubs.hub_relations (hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, status) " +
                "VALUES (@rid, @mid, @hid, 9201, 'active')",
                ("rid", relationUuid), ("mid", CredentialManagementManifestId), ("hid", relatedHubId));

            var dispatcher = await BuildRealDispatcherAsync(cs);

            // Same target_ref shape frontend/runtime/projectionEntry.ts resolveProjectionEntryAxes
            // produces for ?manifest=<uuid> selection, and the same screen_list/Search axes the
            // default projection entry dispatches — this is the real production entry shape, not
            // a test-only shortcut.
            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{CredentialManagementManifestId}:projection_entry",
            });
            var request = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

            var response = await dispatcher.DispatchAsync(request);

            Assert.True(response.Success);
            Assert.NotNull(response.Emission);
            var emission = response.Emission!;

            // "current topology phase" identity — the topology_manifest relation_uuid's
            // hub_relation row belongs to.
            Assert.Equal(CredentialManagementManifestId.ToString(), emission.ManifestId);

            // package_ids[] connects through manifest.topology[ui_projection].packageIds to
            // topology.components_package_design (the manifest-facing package authority).
            Assert.NotNull(emission.PackageId);
            Assert.Contains(emission.PackageId!.Value, packageIds);

            // ui_projection refs resolved from the manifest topology just loaded from the DB (not
            // a hardcoded layoutId test constant) -> real topology.ui_topology_tensor /
            // topology.ui_wiring_registry rows -> real LayoutNodes.
            Assert.NotNull(emission.LayoutId);
            Assert.NotNull(emission.LayoutNodes);
            Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "instance_settings_import_form");
            Assert.Contains(emission.LayoutNodes!, n => n.WiringKind == "instance_settings_action_bundle");

            // hub_ids[] connects through hubs.hub_relations.sequence_position (manifest-scoped
            // relation vector) to Emission.NavigationSequence, and the related hub's sole active
            // topology_manifest resolves as TargetManifestId — the same identity the frontend
            // round-trip (?manifest=<TargetManifestId>) would dispatch next.
            Assert.NotNull(emission.NavigationSequence);
            var navItem = Assert.Single(
                emission.NavigationSequence!, i => hubIds.Select(h => h.ToString()).Contains(i.RelatedHubId));
            Assert.Equal(relatedHubId.ToString(), navItem.RelatedHubId);
            Assert.Equal(9201, navItem.SequencePosition);
            Assert.Equal(relatedManifestId.ToString(), navItem.TargetManifestId);

            // No errors — the ADMIN_OPERATION_NOT_FOUND real-AdminRuntime routing gap for this
            // screen-read axes combination was converted to a structural success, exactly the
            // admin_runtime_structural_read_fallback contract in runtime-orchestration-ssot.yaml.
            Assert.Empty(emission.Errors);
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
    public async Task LoadHubNavigationSequenceAsync_TargetManifestId_ResolvesOnlyExactlyOneActiveManifestPerHub()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        // Four related-hub scenarios in one hub_relations sequence, per reviewer requirement:
        //   1. exactly one ACTIVE manifest (+ zero deprecated siblings)              -> resolves
        //   2. one ACTIVE + one DEPRECATED manifest under the same hub               -> resolves (active)
        //   3. only a DEPRECATED manifest, no active one                            -> null
        //   4. two ACTIVE manifests under the same hub (genuinely ambiguous)         -> null
        var singleActiveHubId = Guid.NewGuid();
        var singleActiveManifestId = Guid.NewGuid();

        var activePlusDeprecatedHubId = Guid.NewGuid();
        var activeAmongDeprecatedManifestId = Guid.NewGuid();
        var deprecatedSiblingManifestId = Guid.NewGuid();

        var deprecatedOnlyHubId = Guid.NewGuid();
        var deprecatedOnlyManifestId = Guid.NewGuid();

        var twoActiveHubId = Guid.NewGuid();
        var twoActiveManifestIdA = Guid.NewGuid();
        var twoActiveManifestIdB = Guid.NewGuid();

        var suffix = Guid.NewGuid().ToString("N")[..8];

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

        async Task InsertHubAsync(Guid hubId) =>
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", hubId));

        async Task InsertManifestAsync(Guid manifestId, Guid hubId, string key, string status) =>
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) " +
                "VALUES (@mid, @hid, @key, @status)",
                ("mid", manifestId), ("hid", hubId), ("key", key), ("status", status));

        async Task InsertHubRelationAsync(Guid relatedHubId, int sequencePosition) =>
            await ExecAsync(
                "INSERT INTO hubs.hub_relations (topology_manifest_id, related_hub_id, sequence_position, status) " +
                "VALUES (@mid, @hid, @seq, 'active')",
                ("mid", CredentialManagementManifestId), ("hid", relatedHubId), ("seq", sequencePosition));

        try
        {
            await InsertHubAsync(singleActiveHubId);
            await InsertManifestAsync(singleActiveManifestId, singleActiveHubId, $"live-db-single-active-{suffix}", "active");

            await InsertHubAsync(activePlusDeprecatedHubId);
            await InsertManifestAsync(activeAmongDeprecatedManifestId, activePlusDeprecatedHubId, $"live-db-active-{suffix}", "active");
            await InsertManifestAsync(deprecatedSiblingManifestId, activePlusDeprecatedHubId, $"live-db-deprecated-sibling-{suffix}", "deprecated");

            await InsertHubAsync(deprecatedOnlyHubId);
            await InsertManifestAsync(deprecatedOnlyManifestId, deprecatedOnlyHubId, $"live-db-deprecated-only-{suffix}", "deprecated");

            await InsertHubAsync(twoActiveHubId);
            await InsertManifestAsync(twoActiveManifestIdA, twoActiveHubId, $"live-db-two-active-a-{suffix}", "active");
            await InsertManifestAsync(twoActiveManifestIdB, twoActiveHubId, $"live-db-two-active-b-{suffix}", "active");

            await InsertHubRelationAsync(singleActiveHubId, 9001);
            await InsertHubRelationAsync(activePlusDeprecatedHubId, 9002);
            await InsertHubRelationAsync(deprecatedOnlyHubId, 9003);
            await InsertHubRelationAsync(twoActiveHubId, 9004);

            var repo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
            var items = await repo.LoadHubNavigationSequenceAsync(CredentialManagementManifestId);

            var singleActiveItem = Assert.Single(items, i => i.RelatedHubId == singleActiveHubId.ToString());
            Assert.Equal(singleActiveManifestId.ToString(), singleActiveItem.TargetManifestId);

            var activePlusDeprecatedItem = Assert.Single(items, i => i.RelatedHubId == activePlusDeprecatedHubId.ToString());
            Assert.Equal(activeAmongDeprecatedManifestId.ToString(), activePlusDeprecatedItem.TargetManifestId);

            var deprecatedOnlyItem = Assert.Single(items, i => i.RelatedHubId == deprecatedOnlyHubId.ToString());
            Assert.Null(deprecatedOnlyItem.TargetManifestId);

            var twoActiveItem = Assert.Single(items, i => i.RelatedHubId == twoActiveHubId.ToString());
            Assert.Null(twoActiveItem.TargetManifestId);
        }
        finally
        {
            await ExecAsync(
                "DELETE FROM hubs.hub_relations WHERE topology_manifest_id = @mid AND related_hub_id IN (@h1, @h2, @h3, @h4)",
                ("mid", CredentialManagementManifestId),
                ("h1", singleActiveHubId), ("h2", activePlusDeprecatedHubId),
                ("h3", deprecatedOnlyHubId), ("h4", twoActiveHubId));
            await ExecAsync(
                "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2, @m3, @m4, @m5, @m6)",
                ("m1", singleActiveManifestId), ("m2", activeAmongDeprecatedManifestId), ("m3", deprecatedSiblingManifestId),
                ("m4", deprecatedOnlyManifestId), ("m5", twoActiveManifestIdA), ("m6", twoActiveManifestIdB));
            await ExecAsync(
                "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2, @h3, @h4)",
                ("h1", singleActiveHubId), ("h2", activePlusDeprecatedHubId),
                ("h3", deprecatedOnlyHubId), ("h4", twoActiveHubId));
        }
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
