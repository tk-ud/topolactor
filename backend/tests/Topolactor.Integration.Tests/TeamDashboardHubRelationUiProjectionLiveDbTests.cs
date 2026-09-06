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
            // The Save button is the mutation_confirmation_contract's preview half: same
            // dispatchTargetRefByTrigger as the confirm button below, but payloadFrom sends
            // dryRun:true (never a direct write) and opens the confirm modal.
            var saveButton = Assert.Single(targetEmission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_save_button");
            Assert.Equal("admin_runtime", saveButton.WiringKind);
            var resolvedUpdateTargetRef = saveButton.DispatchTargetRefByTrigger!.Value.GetProperty("click").GetString();
            Assert.Equal($"manifest:{AdminManifestId}:team_dashboard:update", resolvedUpdateTargetRef);
            Assert.Equal(
                "literal:true",
                saveButton.DispatchPayloadFromByTrigger!.Value.GetProperty("click").GetProperty("dryRun").GetString());

            // The confirm modal and its own confirm/cancel buttons exist on the SAME tensor -- the
            // preview/confirm pairing validate_admin_runtime_preview_action_pairing requires is
            // structurally present, not just the preview half.
            Assert.Contains(targetEmission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_save_confirm_modal");
            var confirmButton = Assert.Single(
                targetEmission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_save_confirm_button");
            Assert.Equal(
                resolvedUpdateTargetRef,
                confirmButton.DispatchTargetRefByTrigger!.Value.GetProperty("click").GetString());
            Assert.Equal(
                "literal:true",
                confirmButton.DispatchPayloadFromByTrigger!.Value.GetProperty("click").GetProperty("confirmed").GetString());
            Assert.Contains(targetEmission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_save_cancel_button");

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
    /// Composed production-path proof (Normal ManifestDispatcher reachability round, 2026-08-17):
    /// dispatched with Role="normal" — the SAME resolution/dispatch mechanism the real /dashboard
    /// route uses (frontend/routes/dashboard/index.tsx ProjectionShell manifestId=dd020 →
    /// target_ref=manifest:{id}:projection_entry, Layer=screen_list/Action=Search — the exact axes
    /// frontend/runtime/projectionEntry.ts resolveProjectionEntryAxes sends). Previously this test
    /// dispatched as Role="admin" because ManifestDispatcher.ResolveRequiredRole inferred
    /// required_role=admin for ANY manifest with runtime_mapping.runtime_destination=admin_runtime,
    /// with no per-action override — that gap is now closed by the layer/action-scoped
    /// capability_requirement mechanism (docs/design/auth-db-session-credential-ssot.yaml
    /// manifest_capability_requirement.layer_action_scoped_override): dd020 declares scoped-open
    /// entries for (screen_list, Search) and (team_dashboard, get), so a genuinely non-admin "normal"
    /// role now reaches this manifest's structural read successfully — proving BOTH the STRUCTURAL
    /// claim (normal manifest's own tensor never contains the editor/save nodes at all, not merely
    /// hidden — surface_axes.normal.surfaces.dashboard.team_dashboard_canonical_shared_contract.
    /// capability_boundary) and, via Emission.Data, that the default_screen_read_override (docs/
    /// design/runtime-orchestration-ssot.yaml ui_projection_render_reachability_contract) actually
    /// invokes team_dashboard:get for real on this same structural dispatch — the manifest is a
    /// production display-lane authority, not merely a seed/proof artifact.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_NormalManifest_ProjectionEntry_AsNormalRole_ReturnsReadOnlyViewerWithRealData()
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
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "normal");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.DoesNotContain(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
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

        // default_screen_read_override: the SAME structural dispatch also carries real Data (the
        // md_viewer node's propBindings.markdown source, "emission.data.bodyMarkdown", resolves from
        // exactly this) — not merely an empty-Data structural success.
        Assert.NotNull(emission.Data);
        Assert.Equal(NoteId.ToString(), emission.Data!.Value.GetProperty("noteId").GetString());
        Assert.False(string.IsNullOrEmpty(emission.Data!.Value.GetProperty("bodyMarkdown").GetString()));
    }

    /// <summary>
    /// Generic route→Manifest identity resolution closure round (2026-08-17): the SAME production
    /// dd020 manifest, reached WITHOUT its UUID appearing anywhere in the request -- target_ref
    /// names it only by hubs.topology_manifests.manifest_key
    /// ("manifest_key:team_dashboard.normal.projection:projection_entry"), the exact shape
    /// frontend/routes/dashboard/index.tsx now sends (ProjectionShell manifestKey prop →
    /// frontend/runtime/projectionEntry.ts resolveProjectionEntryAxes) instead of a hardcoded UUID
    /// constant. Proves the generic manifest_key_target_ref_resolution_contract
    /// (ManifestDispatcher.cs / ContentBundleRepository.ResolveActiveManifestIdByManifestKeyAsync)
    /// resolves against REAL production hubs.topology_manifests data end to end, reaching the exact
    /// same Emission (same ManifestId, same viewer node, same real Data) as the UUID-based dispatch
    /// above.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_NormalManifest_ProjectionEntry_ByManifestKey_ResolvesSameProductionManifest()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = "manifest_key:team_dashboard.normal.projection:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "normal");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        var emission = response.Emission!;
        Assert.Equal(NormalManifestId.ToString(), emission.ManifestId);
        Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "team_dashboard_normal_viewer");
        Assert.NotNull(emission.Data);
        Assert.Equal(NoteId.ToString(), emission.Data!.Value.GetProperty("noteId").GetString());
    }

    /// <summary>
    /// The Admin surface's own equivalent -- manifest_key:team_dashboard.admin.projection resolves
    /// to dd010, the SAME manifest_key/route-entry pairing frontend/routes/admin/team-dashboard/
    /// index.tsx now sends, reaching the Admin tensor's editor nodes.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminManifest_ProjectionEntry_ByManifestKey_ResolvesSameProductionManifest()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = "manifest_key:team_dashboard.admin.projection:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        var emission = response.Emission!;
        Assert.Equal(AdminManifestId.ToString(), emission.ManifestId);
        Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "team_dashboard_admin_body");
    }

    /// <summary>
    /// Negative proof, symmetric to the positive one above: the SAME Role="normal" caller dispatching
    /// the ADMIN manifest's own structural read is still denied — the scoped-open capability_
    /// requirement entries live only on dd020's own topology, never on dd010's, so opening Normal
    /// read did not implicitly widen the Admin edit surface. Admin mutation (team_dashboard:update)
    /// fail-closing for non-admin is separately proven by
    /// DispatchAsync_TeamDashboardUpdate_WithoutAdminAuthenticatedRole_FailsCloseWithAuthCapabilityDenied
    /// below; this test covers the Admin manifest's own READ reachability.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminManifest_ProjectionEntry_AsNormalRole_StillFailsClosedWithAuthCapabilityDenied()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminManifestId}:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "normal");

        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
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
                    confirmed = true,
                }),
                Context: new Dictionary<string, string> { [DispatchAuthContext.AuthenticatedRolesKey] = "admin" },
                TriggerKind: "client", Role: "admin");
            var updateResponse = await dispatcher.DispatchAsync(updateRequest);
            Assert.True(updateResponse.Success, string.Join(";", updateResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            // Read back through the Normal manifest's OWN production structural dispatch, as a
            // genuinely non-admin "normal" role — the SAME target_ref/axes shape the real /dashboard
            // route sends (frontend/routes/dashboard/index.tsx ProjectionShell manifestId=dd020) —
            // proving read-after-write consistency through the ACTUAL canonical Normal reachability
            // path (never a raw SQL read, and never the admin manifest's own axes standing in for
            // it). Both manifests' wiring registries name the SAME team_dashboard:get action against
            // the SAME single-row physical table by construction
            // (TeamDashboardManifests_AreRegisteredInTopologyManifestsAndPhysicalTableBindings proves
            // both bind to topology.team_dashboard_note); this test additionally proves the SAME row
            // identity is what a Normal reader actually observes after an Admin write.
            var readPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{NormalManifestId}:projection_entry",
            });
            var readRequest = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: readPayload, Context: null, TriggerKind: "client", Role: "normal");
            var readResponse = await dispatcher.DispatchAsync(readRequest);
            Assert.True(readResponse.Success, string.Join(";", readResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(marker, readResponse.Emission!.Data?.GetProperty("bodyMarkdown").GetString());
            Assert.Equal(NoteId.ToString(), readResponse.Emission!.Data?.GetProperty("noteId").GetString());
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

    /// <summary>
    /// diff_log + canonical history read stage of team_dashboard:update's
    /// mutation_confirmation_contract, mirroring AdminEnumHubRelationUiProjectionLiveDbTests.cs's own
    /// CountLogsDiffRowsAsync-based proof for enum_dictionary: (1) a dryRun preview writes NO logs.diff
    /// row at all -- proving the preview half is genuinely non-mutating, not just non-visible; (2) the
    /// SAME action with payload.confirmed=true writes exactly one logs.diff row via
    /// AdminMasterRosterAudit.AppendAsync (source_set_id='admin_master_roster',
    /// physical_table_name='topology.team_dashboard_note') -- never a team-dashboard-specific diff
    /// writer, the identical generic helper enum_dictionary/credential_management/external_api_
    /// credential/external_instance_credential/auth_users already share; (3) that same history is
    /// readable back through physical_record:list_history -- the SSOT-designated canonical history
    /// read path (backend/runtime/PhysicalRecordHistoryRuntimeTests.cs), never a raw SQL SELECT on
    /// logs.diff standing in as the sole read proof.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_TeamDashboardUpdate_WritesLogsDiffEvidence_ReadableViaCanonicalPhysicalRecordHistory()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var marker = $"live-db-diff-history-{Guid.NewGuid():N}";
        var t0 = DateTimeOffset.UtcNow;

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

            var adminContext = new Dictionary<string, string> { [DispatchAuthContext.AuthenticatedRolesKey] = "admin" };

            // STEP 1: dryRun preview -- proves the preview half writes no logs.diff row at all.
            var dryRunRequest = new EndpointRequestDto(
                "TeamDashboardScenario", "admin", "team_dashboard", "update",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    target_ref = $"manifest:{AdminManifestId}:team_dashboard:update",
                    bodyMarkdown = marker,
                    dryRun = true,
                }),
                Context: adminContext, TriggerKind: "client", Role: "admin");
            var dryRunResponse = await dispatcher.DispatchAsync(dryRunRequest);
            Assert.True(dryRunResponse.Success, string.Join(";", dryRunResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(
                0, await CountLogsDiffRowsAsync(cs, "topology.team_dashboard_note", NoteId.ToString(), "update", t0));

            // STEP 2: confirmed write -- the SAME action, real mutation, exactly one logs.diff row.
            var confirmedRequest = new EndpointRequestDto(
                "TeamDashboardScenario", "admin", "team_dashboard", "update",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    target_ref = $"manifest:{AdminManifestId}:team_dashboard:update",
                    bodyMarkdown = marker,
                    confirmed = true,
                }),
                Context: adminContext, TriggerKind: "client", Role: "admin");
            var confirmedResponse = await dispatcher.DispatchAsync(confirmedRequest);
            Assert.True(confirmedResponse.Success, string.Join(";", confirmedResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(
                1, await CountLogsDiffRowsAsync(cs, "topology.team_dashboard_note", NoteId.ToString(), "update", t0));

            // STEP 3: read the SAME evidence back through the canonical physical_record:list_history
            // action (AdminRuntime.ExecuteDataAsync layer=physical_record action=list_history), not a
            // raw SQL SELECT standing in as the sole read proof. Dispatched directly against a real
            // AdminRuntime instance (grep-confirmed: no hubs.topology_manifests row anywhere in
            // db/seed_empty.sql declares a dispatcher_mapping for layer=physical_record
            // action=list_history -- this action has never been manifest-wired for ANY domain, a
            // pre-existing, generic gap that predates and is outside team-dashboard's own scope, not
            // something to newly wire here) rather than through ManifestDispatcher's axes-based
            // manifest resolution, which would otherwise MANIFEST_NOT_FOUND on this exact axes
            // combination for every domain, not only team-dashboard.
            var historyRuntime = BuildRealAdminRuntimeWithLogsRepository(cs);
            var (historyData, historyError) = await historyRuntime.ExecuteDataAsync(new OperationVector(
                "admin", "physical_record", "list_history", null, "admin",
                System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    tableId = "topology.team_dashboard_note",
                    recordId = NoteId.ToString(),
                }),
                null, AuthenticatedRole: "admin"));
            Assert.Null(historyError);
            Assert.NotNull(historyData);
            var history = historyData!.Value.GetProperty("history");
            var matchingEntry = Enumerable.Range(0, history.GetArrayLength())
                .Select(i => history[i])
                .Where(e => e.GetProperty("operation_kind").GetString() == "update")
                .Where(e => e.GetProperty("after_state_or_diff_json").GetString()!.Contains(marker))
                .ToList();
            Assert.Single(matchingEntry);
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
    /// Builds a real AdminRuntime wired only to the minimum repositories physical_record:list_history
    /// needs, against a real live-DB SqlAttentionLogsRepository -- physical_record:list_history has
    /// no manifest dispatcher_mapping anywhere in db/seed_empty.sql (for any domain, not just
    /// team-dashboard), so it is unreachable through ManifestDispatcher's axes-based manifest
    /// resolution; this is the same "dispatch the AdminRuntime action directly" pattern
    /// PhysicalRecordHistoryRuntimeTests.cs already uses at the unit level, extended here with a
    /// real Postgres-backed logs repository instead of a stub.
    /// </summary>
    private static AdminRuntime BuildRealAdminRuntimeWithLogsRepository(string connectionString)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, connectionString);
        var topoRepo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, connectionString);
        var uiRepo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, connectionString);
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            sqlAttentionLogsRepository: new NpgsqlSqlAttentionLogsRepository(
                NullLogger<NpgsqlSqlAttentionLogsRepository>.Instance, connectionString));
    }

    /// <summary>
    /// Counts real logs.diff rows for the given physical_table_name/record_id/operation_kind observed
    /// since a baseline timestamp -- mirrors AdminEnumHubRelationUiProjectionLiveDbTests.cs's own
    /// CountLogsDiffRowsAsync (a separate copy since xunit test classes don't share private helpers;
    /// same query shape, same generic logs.diff table, not a team-dashboard-specific read).
    /// </summary>
    private static async Task<int> CountLogsDiffRowsAsync(
        string connectionString, string physicalTableName, string recordId, string operationKind, DateTimeOffset since)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*)::int FROM logs.diff
            WHERE source_set_id = 'admin_master_roster'
              AND physical_table_name = @t
              AND record_id = @r
              AND operation_kind = @op
              AND observed_at >= @since";
        cmd.Parameters.AddWithValue("t", physicalTableName);
        cmd.Parameters.AddWithValue("r", recordId);
        cmd.Parameters.AddWithValue("op", operationKind);
        cmd.Parameters.AddWithValue("since", since);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
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

    /// <summary>
    /// team-dashboard-physical-layout-adoption round: dd013 is now physically adopted as the
    /// PRIMARY structural authority (Owner "Judgment B") -- this is the required backend live-DB
    /// structural-composition proof (docs/design/admin-normal-surface-projection-seed-ssot.yaml
    /// physical_layout_schema_adoption_acceptance_criteria.
    /// required_backend_live_db_structural_composition_proof), the SAME
    /// checked-in-fixture-snapshot pattern CredentialManagementHubRelationUiProjectionLiveDbTests.
    /// DispatchAsync_BareDefaultEntry_NoTargetRef_ResolvesManifest0092ViaCanonicalDefaultEntryRelation
    /// already established for manifest 092: dispatch through the REAL production path (the exact
    /// axes frontend/routes/admin/team-dashboard/index.tsx sends), camelCase-serialize
    /// Emission.LayoutNodes (the same shape the frontend receives over HTTP), and assert it
    /// equals a checked-in snapshot frontend/tests/teamDashboardProductionProjectionBoundary.
    /// test.ts consumes through the real renderEmission() -&gt; LayoutProjectionTree -&gt;
    /// runtimeComponentFactory -&gt; real DOM pipeline. If team-dashboard's seed content ever
    /// drifts from this shape, this test fails HERE, not silently leaving the frontend fixture
    /// stale against real data.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminManifest_ProjectionEntry_ComposedFromPhysicallyAdoptedSchema_MatchesCheckedInFixtureSnapshot()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = "manifest_key:team_dashboard.admin.projection:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        Assert.Equal(AdminManifestId.ToString(), response.Emission!.ManifestId);
        Assert.NotNull(response.Emission.LayoutNodes);
        Assert.NotEmpty(response.Emission.LayoutNodes!);

        var unresolvedLeaves = response.Emission.LayoutNodes!
            .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
            .ToList();
        Assert.Empty(unresolvedLeaves);

        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../../frontend/tests/fixtures/team_dashboard_admin_composed_layout_nodes.json"));
        var expectedJson = await File.ReadAllTextAsync(fixturePath);
        var actualJson = System.Text.Json.JsonSerializer.Serialize(
            response.Emission.LayoutNodes,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
            });
        Assert.Equal(expectedJson.Trim(), actualJson.Trim());
    }

    /// <summary>
    /// Same proof, Normal read-only axis (dd023/dd025) -- zero Action/Modal content, Category >
    /// Section > Field(viewer) only.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_NormalManifest_ProjectionEntry_ComposedFromPhysicallyAdoptedSchema_MatchesCheckedInFixtureSnapshot()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = "manifest_key:team_dashboard.normal.projection:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "normal");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        Assert.Equal(NormalManifestId.ToString(), response.Emission!.ManifestId);
        Assert.NotNull(response.Emission.LayoutNodes);
        Assert.NotEmpty(response.Emission.LayoutNodes!);

        var unresolvedLeaves = response.Emission.LayoutNodes!
            .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
            .ToList();
        Assert.Empty(unresolvedLeaves);

        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../../frontend/tests/fixtures/team_dashboard_normal_composed_layout_nodes.json"));
        var expectedJson = await File.ReadAllTextAsync(fixturePath);
        var actualJson = System.Text.Json.JsonSerializer.Serialize(
            response.Emission.LayoutNodes,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
            });
        Assert.Equal(expectedJson.Trim(), actualJson.Trim());
    }
}
