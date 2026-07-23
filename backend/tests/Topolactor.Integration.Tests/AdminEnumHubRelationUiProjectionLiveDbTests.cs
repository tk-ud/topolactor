using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for the admin-enum subBundle seed (admin-surface-topology-seed-conversion,
/// .agent/tasks/todo.md), db/seed_empty.sql manifest 00000000-0000-0000-0000-0000000ae200
/// ("admin.enum.management.projection"). Reuses
/// HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync (manifest-agnostic) the
/// same way AdminDashboardNavigationUiProjectionLiveDbTests / CredentialManagementHubRelation
/// UiProjectionLiveDbTests do for their own manifests.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires db/seed_empty.sql
/// (and db/ui_component_registry_preset_catalog_bootstrap.sql, for the table.primitive
/// promotion this subBundle added) applied to the target database.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class AdminEnumHubRelationUiProjectionLiveDbTests
{
    private static readonly Guid AdminEnumManagementManifestId =
        new("00000000-0000-0000-0000-0000000ae200");

    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_ResolvesSsotComponentTree_NoUnresolvedLeaves()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        var emission = response.Emission!;

        Assert.Equal(AdminEnumManagementManifestId.ToString(), emission.ManifestId);
        Assert.NotNull(emission.LayoutNodes);
        var nodes = emission.LayoutNodes!;

        // Structural authority nodes (Category/Section/Form) carry no componentId.
        var category = Assert.Single(nodes, n => n.NodeId == "enum_dictionary");
        Assert.Equal("structural_node", category.NodeKind);
        Assert.Equal("topology_ui_category", category.RecordType);
        Assert.Null(category.ComponentId);

        var section = Assert.Single(nodes, n => n.NodeId == "enum_dictionary_roster");
        Assert.Equal("structural_node", section.NodeKind);
        Assert.Equal("topology_ui_section", section.RecordType);
        Assert.Null(section.ComponentId);

        // SSOT seed_contract.component_tree leaves: every one resolves a real componentId from
        // the existing ui_component_registry preset catalog -- including table.primitive, which
        // this subBundle promoted from code_only_drift to active
        // (db/ui_component_registry_preset_catalog_bootstrap.sql) instead of substituting an
        // SSOT-unjustified card_list.primitive/data_grid.alias.
        var searchField = Assert.Single(nodes, n => n.NodeId == "enum_search");
        Assert.Equal("catalog_component", searchField.NodeKind);
        Assert.Equal("form_input/search_input", searchField.ComponentKind);
        Assert.NotNull(searchField.ComponentId);

        var groupFilterField = Assert.Single(nodes, n => n.NodeId == "enum_group_filter");
        Assert.Equal("catalog_component", groupFilterField.NodeKind);
        Assert.Equal("form_input/select", groupFilterField.ComponentKind);
        Assert.NotNull(groupFilterField.ComponentId);

        var table = Assert.Single(nodes, n => n.NodeId == "enum_table");
        Assert.Equal("catalog_component", table.NodeKind);
        Assert.Equal("data_display/table", table.ComponentKind);
        Assert.NotNull(table.ComponentId);

        var formField = Assert.Single(nodes, n => n.NodeId == "enum_form");
        Assert.Equal("catalog_component", formField.NodeKind);
        Assert.Equal("form_input/form_field", formField.ComponentKind);
        Assert.NotNull(formField.ComponentId);

        var confirmButton = Assert.Single(nodes, n => n.NodeId == "enum_confirm_button");
        Assert.Equal("catalog_component", confirmButton.NodeKind);
        Assert.Equal("action/button", confirmButton.ComponentKind);
        Assert.NotNull(confirmButton.ComponentId);
        // The one real, functioning interaction: opens local confirm state
        // (internal_instance_wiring localStateMutation) -- explicit_confirm stage of
        // mutation_confirmation_contract. The write stage has no runtimeInteractions here (see
        // db/seed_empty.sql admin-enum header comment / enum_write_dispatch_gap validation
        // record -- known gap, not fabricated).
        Assert.NotNull(confirmButton.RuntimeInteractions);
        Assert.Contains("localStateMutation", confirmButton.RuntimeInteractions!.Value.GetRawText());

        // render completion: every catalog_component leaf resolved a componentId.
        var unresolvedLeaves = nodes
            .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
            .ToList();
        Assert.Empty(unresolvedLeaves);

        Assert.Empty(emission.Errors);
    }

    /// <summary>
    /// Combined resolution_chain + hub_navigation:create authoring dispatch proof, per the
    /// navigation_binding_authoring_and_verification resolution criterion confirmed in
    /// .agent/tasks/todo.md (2026-07-22): the SAME live-DB test must (1) author a hub_relation
    /// FROM this subBundle's own manifest via the real hub_navigation:create dispatch action
    /// (never a raw SQL insert standing in for the authoring path), and (2) re-dispatch this
    /// subBundle's own manifest and confirm the full resolution chain (hub_relation ->
    /// topology_manifest -> hub_ids[]/package_ids[] -> package/layout/wiring/tensor ->
    /// ManifestDispatcher.DispatchAsync -> scalar Emission) reflects the authored relation.
    /// Mirrors AdminDashboardNavigationUiProjectionLiveDbTests's pattern (the reference
    /// implementation the todo.md criterion names) but additionally authors the relation via
    /// hub_navigation:create instead of direct SQL, so this single test satisfies both halves the
    /// criterion requires together -- neither credential-management's split two-test pair (one
    /// authoring-path test against a synthetic non-092 source manifest, one resolution-chain test
    /// against a raw-SQL-inserted relation) counted as meeting it.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_HubNavigationCreate_RealAuthoringPath_ThenResolutionChainReflectsIt()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var targetHubId = Guid.NewGuid();
        var targetManifestId = Guid.NewGuid();
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

        Guid? createdHubRelationId = null;
        try
        {
            // Ordinary target for the relation -- any existing manifest an admin could pick via
            // /admin/manifests.
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", targetHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", targetManifestId), ("hid", targetHubId), ("key", $"live-db-admin-enum-nav-target-{suffix}"));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // STEP 1: author the relation FROM admin-enum's own manifest via the REAL
            // hub_navigation:create dispatch action (frontend/api/adminApi.ts createHubRelation()
            // -> HubNavigationAdmin.tsx path) -- never a raw SQL insert.
            var createPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = AdminEnumManagementManifestId.ToString(),
                relatedHubId = targetHubId.ToString(),
                sequencePosition = 1,
            });
            var createRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "create",
                IdOrHubId: null, Payload: createPayload, Context: null, TriggerKind: "client", Role: "admin");
            var createResponse = await dispatcher.DispatchAsync(createRequest);

            Assert.True(
                createResponse.Success,
                string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            var contentBundleRepo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
            var relations = await contentBundleRepo.ListHubRelationsByManifestAsync(AdminEnumManagementManifestId);
            var created = Assert.Single(relations, r => r.RelatedHubId == targetHubId.ToString());
            createdHubRelationId = Guid.Parse(created.HubRelationId);
            Assert.Equal(1, created.SequencePosition);
            Assert.Equal("active", created.Status);

            // STEP 2: resolution chain -- dispatch admin-enum's own manifest via target_ref
            // (projection_entry, the same ?manifest= explicit-selection shape
            // frontend/runtime/projectionEntry.ts produces), and confirm BOTH halves in one call:
            // (a) the full SSOT component_tree resolves (package/layout/wiring/tensor ->
            // ManifestDispatcher.DispatchAsync -> scalar Emission.LayoutNodes, no unresolved
            // leaves), and (b) Emission.NavigationSequence reflects the relation just authored
            // through the real hub_navigation:create action above.
            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:projection_entry",
            });
            var request = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);

            Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.NotNull(response.Emission);
            var emission = response.Emission!;

            var unresolvedLeaves = emission.LayoutNodes!
                .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
                .ToList();
            Assert.Empty(unresolvedLeaves);

            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                emission,
                AdminEnumManagementManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(targetHubId, 1, targetManifestId)]);
        }
        finally
        {
            if (createdHubRelationId is not null)
                await ExecAsync("DELETE FROM hubs.hub_relations WHERE hub_relation_id = @rid", ("rid", createdHubRelationId.Value));
            await ExecAsync("DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @mid", ("mid", targetManifestId));
            await ExecAsync("DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", targetHubId));
        }
    }

    /// <summary>
    /// Read-circuit proof (2026-07-23, admin-runtime-operation-dispatch-lane-determination
    /// concrete boundary consumption phase 1 of 2): this manifest's wiring row now dispatches the
    /// real, existing enum_dictionary:list_groups admin_runtime action -- not the
    /// ADMIN_OPERATION_NOT_FOUND structural-render fallback. Real db/enum_seed.sql rows
    /// (group_name "demo_status") reach emission.data, and the enum_table node's composed
    /// PropsJson/PropBindings (LayoutSchemaTensorComposer.BuildNodeLocalDataByNodeId merge, added
    /// this pass) carry the static columns + emission.data-bound rows a frontend table render
    /// needs -- this is the render-time wiring proof; frontend/tests/renderEmission tests already
    /// cover resolvePropBindings/tableFactory consuming this shape once emitted.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_DispatchesRealListGroups_EmissionDataAndTablePropsCarryRealRows()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        // STEP 1: structural resolution (same "...projection_entry" convention every other
        // admin-enum test uses) -- confirms the wiring row's flat columns/composed
        // PropsJson/PropBindings, independent of whether a data dispatch also ran.
        var structurePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:projection_entry",
        });
        var structureRequest = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: structurePayload, Context: null, TriggerKind: "client", Role: "admin");
        var structureResponse = await dispatcher.DispatchAsync(structureRequest);
        Assert.True(structureResponse.Success, string.Join(";", structureResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        var structureNodes = structureResponse.Emission!.LayoutNodes!;

        var table = Assert.Single(structureNodes, n => n.NodeId == "enum_table");
        Assert.Equal("admin_runtime", table.WiringKind);
        Assert.Equal(
            $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
            table.TargetRef);

        Assert.NotNull(table.PropsJson);
        Assert.Contains("groupName", table.PropsJson);
        Assert.NotNull(table.PropBindings);
        var propBindingsText = table.PropBindings!.Value.GetRawText();
        Assert.Contains("\"rows\"", propBindingsText);
        Assert.Contains("emission.data", propBindingsText);

        // search_input/group_filter inherit the SAME layout-wide admin_runtime dispatch spec
        // (idempotent no-payload re-list) -- not a per-node override, and not left unconfigured
        // either (both still resolve a WiringKind).
        var searchField = Assert.Single(structureNodes, n => n.NodeId == "enum_search");
        Assert.Equal("admin_runtime", searchField.WiringKind);
        var groupFilterField = Assert.Single(structureNodes, n => n.NodeId == "enum_group_filter");
        Assert.Equal("admin_runtime", groupFilterField.WiringKind);

        // enum_confirm_button keeps its own explicit_confirm-only interaction, unaffected by the
        // layout's admin_runtime read binding (Lane 3 overrides Lane 2 on its own click trigger).
        var confirmButton = Assert.Single(structureNodes, n => n.NodeId == "enum_confirm_button");
        Assert.NotNull(confirmButton.RuntimeInteractions);
        Assert.Contains("localStateMutation", confirmButton.RuntimeInteractions!.Value.GetRawText());

        // STEP 2: the REAL data dispatch -- Target/Layer/Action set directly (exactly what
        // frontend/runtime/frontendScheduler.ts enqueueRuntimeComponentCommand sends as the
        // request's own top-level target/layer/action, NOT derived from target_ref), with
        // payload.target_ref carrying the SAME manifest-resolving reference the wiring row
        // stores (ManifestDispatcher.TryParseManifestTargetRef resolves the manifest from its
        // "manifest:<uuid>:" prefix; AdminRuntime.ExecuteDataAsync's layerAction switch uses
        // request.Layer/request.Action directly -- these are two independent axes, not one
        // encoded inside the other).
        var dataPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
        });
        var dataRequest = new EndpointRequestDto(
            "list_groups", "manifest", "enum_dictionary", "list_groups",
            IdOrHubId: null, Payload: dataPayload, Context: null, TriggerKind: "client", Role: "admin");
        var dataResponse = await dispatcher.DispatchAsync(dataRequest);

        Assert.True(dataResponse.Success, string.Join(";", dataResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(dataResponse.Emission);
        var dataEmission = dataResponse.Emission!;

        // Real enum.groups data (db/enum_seed.sql demo_status row), not the structural-render
        // fallback's empty/absent data -- this is what proves the dispatch actually reached
        // AdminRuntime.ExecuteDataAsync's "enum_dictionary:list_groups" case, not just that the
        // manifest/wiring resolve structurally.
        Assert.NotNull(dataEmission.Data);
        var dataText = dataEmission.Data!.Value.GetRawText();
        Assert.Contains("demo_status", dataText);
        Assert.Contains("groupName", dataText);
    }

    [Fact]
    public async Task AdminEnumManagementManifest_OwnsNoHubRelationsRows_SeedOnly()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*)::int FROM hubs.hub_relations WHERE topology_manifest_id = @id";
        cmd.Parameters.AddWithValue("id", AdminEnumManagementManifestId);
        var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);

        Assert.Equal(0, count);
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
