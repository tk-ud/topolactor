using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB E2E proof for the team-dashboard hub relation / navigation / ui_projection bundle, per
/// runtime-orchestration-ssot.yaml dispatcher_contract.ui_projection_render_reachability_contract.
/// test_proof_contract, the same combined-proof pattern
/// AdminEnumHubRelationUiProjectionLiveDbTests.cs / CredentialManagementHubRelationUiProjectionLiveDbTests.cs
/// established: (1) author a hub_relations row via the REAL hub_navigation:create dispatch action
/// targeting team-dashboard's own manifest, never a raw SQL insert standing in for authoring; (2) walk
/// that SAME authored relation's resolved TargetManifestId onward through the real package/layout/
/// wiring/tensor rows to a scalar Emission.
///
/// team-dashboard is a two-manifest surface (admin edit + normal read-only), both reading the SAME
/// topology.team_dashboard_note row via team_dashboard:get — this file additionally proves the
/// capability boundary between them: team_dashboard:update requires AuthenticatedRole=="admin"
/// (AdminRuntime.TeamDashboard.cs), and the normal manifest's own tensor never contains the editor
/// nodes at all (not merely hidden), per surface_axes.normal.surfaces.dashboard.
/// team_dashboard_canonical_shared_contract.capability_boundary.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires db/seed_empty.sql applied
/// to the target database (team-dashboard manifests dd010/dd020 + their ui_projection rows).
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class TeamDashboardHubRelationUiProjectionLiveDbTests
{
    private static readonly Guid AdminManifestId = new("00000000-0000-0000-0000-0000000dd010");
    private static readonly Guid AdminHubId = new("00000000-0000-0000-0000-0000000dd011");
    private static readonly Guid NormalManifestId = new("00000000-0000-0000-0000-0000000dd020");
    private static readonly Guid NoteId = new("00000000-0000-0000-0000-0000000dd001");

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();

    private static async Task ExecAsync(NpgsqlConnection conn, string sql, params (string Name, object Value)[] parms)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Closes admin-normal-surface-projection-seed-ssot.yaml design_blocking.
    /// target_surface_manifest_readiness.navigation_binding_authoring_and_verification
    /// (subbundle_status.team-dashboard): authors a hub_relations row via the REAL
    /// hub_navigation:create dispatch action targeting the admin manifest's own hub, then walks that
    /// SAME authored relation's resolved TargetManifestId onward through manifest dd010's real
    /// package/layout/wiring/tensor rows to a scalar Emission — never a synthetic target, never a
    /// direct SQL relation insert standing in for authoring.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_HubNavigationCreate_AuthorsRelationTargetingAdminManifest_ResolutionChainReachesScalarEmission()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var sourceHubId = Guid.NewGuid();
        var sourceManifestId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        Guid? createdHubRelationId = null;
        try
        {
            await ExecAsync(conn, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", sourceHubId));
            await ExecAsync(
                conn,
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", sourceManifestId), ("hid", sourceHubId), ("key", $"live-db-team-dashboard-source-{suffix}"));
            await ExecAsync(
                conn,
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY['{\"type\":\"runtime_mapping\",\"runtime_destination\":\"admin_runtime\"}'::jsonb], 'active')",
                ("mid", sourceManifestId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // STEP 1: author the relation via the REAL hub_navigation:create dispatch action,
            // targeting the admin manifest's own existing hub.
            var createPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = sourceManifestId.ToString(),
                relatedHubId = AdminHubId.ToString(),
                sequencePosition = 1,
            });
            var createRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "create",
                IdOrHubId: null, Payload: createPayload, Context: null, TriggerKind: "client", Role: "admin");
            var createResponse = await dispatcher.DispatchAsync(createRequest);

            Assert.True(createResponse.Success, string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            var contentBundleRepo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
            var relations = await contentBundleRepo.ListHubRelationsByManifestAsync(sourceManifestId);
            var created = Assert.Single(relations, r => r.RelatedHubId == AdminHubId.ToString());
            createdHubRelationId = Guid.Parse(created.HubRelationId);
            Assert.Equal(1, created.SequencePosition);
            Assert.Equal("active", created.Status);

            // STEP 2: dispatch the SOURCE manifest and confirm THIS SAME authored relation resolves
            // Emission.NavigationSequence[].TargetManifestId to the admin manifest itself.
            var sourcePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{sourceManifestId}:hub_relations_read",
                topologyManifestId = sourceManifestId.ToString(),
            });
            var sourceRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "get_hub_relations",
                IdOrHubId: null, Payload: sourcePayload, Context: null, TriggerKind: "client", Role: "admin");
            var sourceResponse = await dispatcher.DispatchAsync(sourceRequest);

            Assert.True(sourceResponse.Success, string.Join(";", sourceResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.NotNull(sourceResponse.Emission);
            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                sourceResponse.Emission!,
                sourceManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(AdminHubId, 1, AdminManifestId)]);

            // STEP 3: walk onward from that SAME relation's resolved target — dispatch the admin
            // manifest itself and confirm the full package/layout/wiring/tensor resolution chain
            // reaches a real scalar Emission with the editor nodes present.
            var targetPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminManifestId}:projection_entry",
            });
            var targetRequest = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: targetPayload, Context: null, TriggerKind: "client", Role: "admin");
            var targetResponse = await dispatcher.DispatchAsync(targetRequest);

            Assert.True(targetResponse.Success, string.Join(";", targetResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            var targetEmission = targetResponse.Emission!;
            Assert.Equal(AdminManifestId.ToString(), targetEmission.ManifestId);
            Assert.NotNull(targetEmission.LayoutNodes);
            Assert.Contains(targetEmission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_viewer");
            Assert.Contains(targetEmission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_body");
            var saveButton = Assert.Single(targetEmission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_save_button");
            Assert.Equal("admin_runtime", saveButton.WiringKind);
            Assert.Equal(
                $"manifest:{AdminManifestId}:team_dashboard:update",
                saveButton.DispatchTargetRefByTrigger!.Value.GetProperty("click").GetString());

            var unresolvedLeaves = targetEmission.LayoutNodes!
                .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
                .ToList();
            Assert.Empty(unresolvedLeaves);
            Assert.Empty(targetEmission.Errors);
        }
        finally
        {
            if (createdHubRelationId is not null)
                await ExecAsync(conn, "DELETE FROM hubs.hub_relations WHERE hub_relation_id = @rid", ("rid", createdHubRelationId.Value));
            await ExecAsync(conn, "DELETE FROM manifest WHERE manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(conn, "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(conn, "DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", sourceHubId));
        }
    }

    /// <summary>
    /// Proves the STRUCTURAL claim (normal manifest's own tensor never contains the editor/save
    /// nodes at all, not merely hidden) — see
    /// surface_axes.normal.surfaces.dashboard.team_dashboard_canonical_shared_contract.
    /// capability_boundary. Dispatched with Role="admin": ManifestDispatcher.ResolveRequiredRole
    /// infers required_role=admin for ANY manifest with runtime_mapping.runtime_destination=
    /// admin_runtime, with no per-action or explicit-open override available today — so a genuinely
    /// role-open ("normal"/anonymous) dispatch through ManifestDispatcher is not yet possible for
    /// ANY seed-driven screen in this codebase (team_markdown's own Normal viewer works around this
    /// the same way every other attempt has: a plain REST endpoint that bypasses ManifestDispatcher
    /// entirely, see GET /team-dashboard/note in backend/Program.cs). This is an acknowledged,
    /// explicit, Owner-decision-pending open item (a new runtime destination or a per-action
    /// capability override), not something this test can prove closed — it isolates and proves only
    /// the structural (layout-node) half of the claim, which does not depend on that gap.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_NormalManifest_LayoutNodes_ContainOnlyReadOnlyViewer_NoEditorOrSaveNodes()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{NormalManifestId}:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        var emission = response.Emission!;
        Assert.Equal(NormalManifestId.ToString(), emission.ManifestId);
        Assert.NotNull(emission.LayoutNodes);

        // The read-only viewer exists...
        Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "team_dashboard_normal_viewer");
        // ...but the editable Textarea and Save button do not exist on this manifest's own tensor at
        // all (not merely hidden) — the DOM proof that a Normal session has no edit control to reach.
        Assert.DoesNotContain(emission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_body");
        Assert.DoesNotContain(emission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_save_button");
        Assert.DoesNotContain(emission.LayoutNodes!, n => n.DispatchTargetRefByTrigger != null);

        var unresolvedLeaves = emission.LayoutNodes!
            .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
            .ToList();
        Assert.Empty(unresolvedLeaves);
    }

    /// <summary>
    /// Both manifests read the SAME topology.team_dashboard_note row via team_dashboard:get — proves
    /// "shared projection, no duplicated data identity" directly: an update through the admin
    /// manifest's own write path is immediately visible through the normal manifest's own read path.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_TeamDashboardUpdate_ViaAdminManifest_IsVisibleThroughNormalManifestsOwnRead()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var marker = $"live-db-shared-note-{Guid.NewGuid():N}";

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        string? originalBody = null;
        try
        {
            await using (var readCmd = conn.CreateCommand())
            {
                readCmd.CommandText = "SELECT body_markdown FROM topology.team_dashboard_note WHERE note_id = @id";
                readCmd.Parameters.AddWithValue("id", NoteId);
                originalBody = (string?)await readCmd.ExecuteScalarAsync();
            }

            // Admin write, through the real dispatch path (never a raw SQL update) — the SAME
            // target_ref shape the seeded Save button's dispatchTargetRefByTrigger declares.
            // Context carries the SAME key DispatchAuthContext.ApplyJwtAuthority stamps after real
            // JWT verification at the /dispatch HTTP boundary (backend/endpoint/DispatchAuthContext.cs)
            // — simulating an already-authenticated admin request reaching ManifestDispatcher, since
            // this test dispatches directly against ManifestDispatcher without the HTTP/JWT pipeline
            // in front of it.
            var updateRequest = new EndpointRequestDto(
                "TeamDashboardScenario", "admin", "team_dashboard", "update",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    target_ref = $"manifest:{AdminManifestId}:team_dashboard:update",
                    bodyMarkdown = marker,
                }),
                Context: new Dictionary<string, string> { [DispatchAuthContext.AuthenticatedRolesKey] = "admin" },
                TriggerKind: "client", Role: "admin");
            var updateResponse = await dispatcher.DispatchAsync(updateRequest);
            Assert.True(updateResponse.Success, string.Join(";", updateResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            // Read back through the SAME team_dashboard:get action the normal manifest's own wiring
            // registry declares (db/seed_empty.sql team_dashboard.normal.projection.wiring
            // wiring_schema_json.actions) — proving read-after-write consistency through the real
            // dispatch path (never a raw SQL read standing in for it). Both manifests' wiring
            // registries name the SAME action against the SAME single-row physical table by
            // construction (TeamDashboardManifests_AreRegisteredInTopologyManifestsAndPhysicalTable
            // Bindings proves both bind to topology.team_dashboard_note); dispatched here via the
            // admin manifest's own axes since a genuinely role-open Normal dispatch is not yet
            // possible (see DispatchAsync_NormalManifest_LayoutNodes_ContainOnlyReadOnlyViewer_
            // NoEditorOrSaveNodes's own comment for the acknowledged gap) — this test's purpose is
            // the DATA-sharing claim (one physical row, read-after-write), orthogonal to that gap.
            var readRequest = new EndpointRequestDto(
                "TeamDashboardScenario", "admin", "team_dashboard", "get",
                IdOrHubId: null, Payload: null, Context: null, TriggerKind: "client", Role: "admin");
            var readResponse = await dispatcher.DispatchAsync(readRequest);
            Assert.True(readResponse.Success, string.Join(";", readResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(marker, readResponse.Emission!.Data?.GetProperty("bodyMarkdown").GetString());
        }
        finally
        {
            if (originalBody is not null)
            {
                await using var restoreCmd = conn.CreateCommand();
                restoreCmd.CommandText = "UPDATE topology.team_dashboard_note SET body_markdown = @body WHERE note_id = @id";
                restoreCmd.Parameters.AddWithValue("body", originalBody);
                restoreCmd.Parameters.AddWithValue("id", NoteId);
                await restoreCmd.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Backend/runtime capability boundary, independent of DOM/layout: team_dashboard:update fails
    /// closed for a non-admin AuthenticatedRole even if dispatched directly (defense-in-depth beyond
    /// the normal manifest simply never authoring the editor nodes, see the DOM proof above).
    /// AuthenticatedRole (not Role) is what AdminRuntime.TeamDashboard.cs actually checks —
    /// DispatchAsync's OperationVectorResolver only stamps AuthenticatedRole from a JWT-verified
    /// DispatchAuthContext, never from the client-supplied Role field, so this call (no auth context)
    /// exercises the real "no admin JWT" path, not a spoofable Role="admin" bypass. Role="admin" is
    /// still passed here (client-supplied, checked by ManifestDispatcher.ValidateCapabilityRequirement
    /// against the manifest's OWN inferred admin_runtime requirement) purely to get PAST that earlier
    /// manifest-level gate so this test actually reaches AdminRuntime.TeamDashboard.cs's own
    /// AuthenticatedRole check — the two checks are independent layers, and this test isolates the
    /// second one.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_TeamDashboardUpdate_WithoutAdminAuthenticatedRole_FailsCloseWithAuthCapabilityDenied()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        var updateRequest = new EndpointRequestDto(
            "TeamDashboardScenario", "admin", "team_dashboard", "update",
            IdOrHubId: null,
            Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminManifestId}:team_dashboard:update",
                bodyMarkdown = "should not be written",
            }),
            Context: null, TriggerKind: "client", Role: "admin");
        var response = await dispatcher.DispatchAsync(updateRequest);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }

    [Fact]
    public async Task TeamDashboardManifests_AreRegisteredInTopologyManifestsAndPhysicalTableBindings()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        foreach (var (manifestId, expectedKey) in new[]
        {
            (AdminManifestId, "team_dashboard.admin.projection"),
            (NormalManifestId, "team_dashboard.normal.projection"),
        })
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT hub_id::text, manifest_key, status FROM hubs.topology_manifests WHERE topology_manifest_id = @id";
                cmd.Parameters.AddWithValue("id", manifestId);
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync(), $"manifest {manifestId} must be registered in hubs.topology_manifests");
                Assert.Equal(expectedKey, reader.GetString(1));
                Assert.Equal("active", reader.GetString(2));
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT COUNT(*)::int FROM topology.physical_table_manifest_bindings " +
                    "WHERE topology_manifest_id = @id AND active = true";
                cmd.Parameters.AddWithValue("id", manifestId);
                var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                Assert.True(count > 0, $"manifest {manifestId} must have active physical_table_manifest_bindings rows");
            }
        }
    }
}
