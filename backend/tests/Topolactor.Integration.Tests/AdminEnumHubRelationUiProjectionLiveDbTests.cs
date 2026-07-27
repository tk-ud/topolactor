using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
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

    /// <summary>
    /// Write-side live-DB proof (2026-07-24, admin-runtime-operation-dispatch-lane-determination
    /// remaining_write_payload_capture_gap resolution). Unlike the read-circuit test above, this
    /// dispatches enum_dictionary:create_group / delete_group directly against real PostgreSQL --
    /// the SAME dispatcher chain (ManifestDispatcher -> AdminRuntimeDispatchAdapter ->
    /// AdminRuntime.ExecuteDataAsync -> EnumDictionaryRepository) the frontend's now-payloadFrom-
    /// capable admin_runtime Lane 2 (frontend/runtime/runtimeComponentFactory.ts emitBoundEvent,
    /// frontend/tests/liveNodeValueTrackerAndAdminRuntimePayloadFrom.test.ts) would drive in
    /// production. Proves: (1) write persists to real PostgreSQL, (2) a subsequent
    /// enum_dictionary:list_groups re-dispatch reflects the write (create appears, delete is
    /// gone) -- the diff/evidence boundary -- and (3) two negative cases fail close instead of
    /// silently succeeding: deleting a nonexistent groupId, and a malformed (non-UUID) groupId.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_CreateGroupThenDeleteGroup_PersistsAndReListReflectsDiff()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var groupName = $"live-db-write-proof-{suffix}";
        string? createdGroupId = null;

        async Task<string> ListGroupsRawAsync()
        {
            var listPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
            });
            var listRequest = new EndpointRequestDto(
                "list_groups", "manifest", "enum_dictionary", "list_groups",
                IdOrHubId: null, Payload: listPayload, Context: null, TriggerKind: "client", Role: "admin");
            var listResponse = await dispatcher.DispatchAsync(listRequest);
            Assert.True(listResponse.Success, string.Join(";", listResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            return listResponse.Emission!.Data!.Value.GetRawText();
        }

        try
        {
            // Baseline: the not-yet-created group name is absent from a real re-list.
            Assert.DoesNotContain(groupName, await ListGroupsRawAsync());

            // NEGATIVE (mutation_confirmation_contract explicit_confirm gate): a write dispatched
            // without payload.confirmed=true (the "cancel" path -- the frontend simply never sends
            // it) fails close and never reaches the repository.
            var unconfirmedCreatePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:create_group",
                groupName,
            });
            var unconfirmedCreateRequest = new EndpointRequestDto(
                "create_group", "manifest", "enum_dictionary", "create_group",
                IdOrHubId: null, Payload: unconfirmedCreatePayload, Context: null, TriggerKind: "client", Role: "admin");
            var unconfirmedCreateResponse = await dispatcher.DispatchAsync(unconfirmedCreateRequest);
            Assert.False(unconfirmedCreateResponse.Success);
            Assert.Contains(unconfirmedCreateResponse.Errors, e => e.Code == "ENUM_GROUP_WRITE_NOT_CONFIRMED");
            Assert.DoesNotContain(groupName, await ListGroupsRawAsync());

            // NEGATIVE (preview_dictionary_delta / validate_against_enum_authority): dryRun=true
            // validates without mutating -- the same payload shape, never persisted.
            var dryRunCreatePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:create_group",
                groupName,
                dryRun = true,
            });
            var dryRunCreateRequest = new EndpointRequestDto(
                "create_group", "manifest", "enum_dictionary", "create_group",
                IdOrHubId: null, Payload: dryRunCreatePayload, Context: null, TriggerKind: "client", Role: "admin");
            var dryRunCreateResponse = await dispatcher.DispatchAsync(dryRunCreateRequest);
            Assert.True(dryRunCreateResponse.Success, string.Join(";", dryRunCreateResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Contains("\"dryRun\":true", dryRunCreateResponse.Emission!.Data!.Value.GetRawText());
            Assert.DoesNotContain(groupName, await ListGroupsRawAsync());

            // WRITE: enum_dictionary:create_group -- the payload shape a resolved
            // payloadFrom (node:<nameInputNodeId>.value) would produce.
            var createPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:create_group",
                groupName,
                confirmed = true,
            });
            var createRequest = new EndpointRequestDto(
                "create_group", "manifest", "enum_dictionary", "create_group",
                IdOrHubId: null, Payload: createPayload, Context: null, TriggerKind: "client", Role: "admin");
            var createResponse = await dispatcher.DispatchAsync(createRequest);
            Assert.True(createResponse.Success, string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            var createdText = createResponse.Emission!.Data!.Value.GetRawText();
            using (var createdDoc = System.Text.Json.JsonDocument.Parse(createdText))
            {
                createdGroupId = createdDoc.RootElement.GetProperty("groupId").GetString();
            }
            Assert.False(string.IsNullOrWhiteSpace(createdGroupId));

            // READ REPROJECTION: the write's effect is visible through the SAME read circuit
            // the admin-enum screen dispatches (real Postgres round trip, not an in-memory echo).
            Assert.Contains(groupName, await ListGroupsRawAsync());

            // NEGATIVE: malformed (non-UUID) groupId fails close, never silently "succeeds".
            var malformedDeletePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:delete_group",
                groupId = "not-a-uuid",
            });
            var malformedDeleteRequest = new EndpointRequestDto(
                "delete_group", "manifest", "enum_dictionary", "delete_group",
                IdOrHubId: null, Payload: malformedDeletePayload, Context: null, TriggerKind: "client", Role: "admin");
            var malformedDeleteResponse = await dispatcher.DispatchAsync(malformedDeleteRequest);
            Assert.False(malformedDeleteResponse.Success);
            Assert.Contains(malformedDeleteResponse.Errors, e => e.Code == "ENUM_GROUP_ID_MALFORMED");

            // NEGATIVE: a syntactically valid but nonexistent groupId fails close.
            var nonexistentDeletePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:delete_group",
                groupId = Guid.NewGuid().ToString(),
            });
            var nonexistentDeleteRequest = new EndpointRequestDto(
                "delete_group", "manifest", "enum_dictionary", "delete_group",
                IdOrHubId: null, Payload: nonexistentDeletePayload, Context: null, TriggerKind: "client", Role: "admin");
            var nonexistentDeleteResponse = await dispatcher.DispatchAsync(nonexistentDeleteRequest);
            Assert.False(nonexistentDeleteResponse.Success);
            Assert.Contains(nonexistentDeleteResponse.Errors, e => e.Code == "ENUM_GROUP_NOT_FOUND");

            // The group created above must still be present -- neither negative case above
            // silently deleted it.
            Assert.Contains(groupName, await ListGroupsRawAsync());

            // WRITE: enum_dictionary:delete_group -- the payload shape the trigger's OWN native
            // event payload (a table row-click event's own {row:{groupId:...}}) resolves via
            // event.row.groupId, without needing the node-value-tracking fix at all.
            var deletePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:delete_group",
                groupId = createdGroupId,
                confirmed = true,
            });
            var deleteRequest = new EndpointRequestDto(
                "delete_group", "manifest", "enum_dictionary", "delete_group",
                IdOrHubId: null, Payload: deletePayload, Context: null, TriggerKind: "client", Role: "admin");
            var deleteResponse = await dispatcher.DispatchAsync(deleteRequest);
            Assert.True(deleteResponse.Success, string.Join(";", deleteResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            createdGroupId = null; // deleted -- no cleanup needed in finally.

            // READ REPROJECTION: the delete's effect is visible through a real re-list too.
            Assert.DoesNotContain(groupName, await ListGroupsRawAsync());
        }
        finally
        {
            // Best-effort cleanup if an assertion above failed before the delete step ran.
            if (createdGroupId is not null)
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM enum.groups WHERE group_id = @id";
                cmd.Parameters.AddWithValue("id", Guid.Parse(createdGroupId));
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Real-PostgreSQL proof for the constraint-backed negative cases the action layer's
    /// pre-checks are defense-in-depth for (backend/repository/NpgsqlEnumDictionaryRepository.cs
    /// PostgresException translation of uq_enum_items_index / uq_enum_groups_index /
    /// enum.group_items's FK to enum.items -- db/enum_tables.sql): duplicate index_num on
    /// create_group/create_item, deleting an item that is a real db/enum_seed.sql demo_status
    /// group member, and set_group_items with duplicate/nonexistent membership. None of these
    /// mutate db/enum_seed.sql's existing rows -- every attempt fails close before or during the
    /// write and is asserted not to have persisted.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_ConstraintBackedNegativeCases_FailCloseAgainstRealPostgres()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        async Task<EndpointResponseDto> DispatchAsync(string action, object payload)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(
                System.Text.Json.JsonSerializer.SerializeToElement(payload).GetRawText())!.AsObject();
            node["target_ref"] = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:{action}";
            return await dispatcher.DispatchAsync(new EndpointRequestDto(
                action, "manifest", "enum_dictionary", action,
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(node),
                Context: null, TriggerKind: "client", Role: "admin"));
        }

        // demo_status (index_num=1, db/enum_seed.sql) already exists -- create_group with the
        // same index_num must fail close, dryRun and confirmed alike, never silently succeeding
        // or shadowing the existing row.
        var dupGroupDryRun = await DispatchAsync("create_group",
            new { groupName = "dup-index-group", indexNum = 1, dryRun = true });
        Assert.False(dupGroupDryRun.Success);
        Assert.Contains(dupGroupDryRun.Errors, e => e.Code == "ENUM_GROUP_INDEX_CONFLICT");
        var dupGroupWrite = await DispatchAsync("create_group",
            new { groupName = "dup-index-group", indexNum = 1, confirmed = true });
        Assert.False(dupGroupWrite.Success);
        Assert.Contains(dupGroupWrite.Errors, e => e.Code == "ENUM_GROUP_INDEX_CONFLICT");

        // demo_active (index_num=1, db/enum_seed.sql) already exists -- create_item with the same
        // index_num must fail close the same way.
        var dupItemWrite = await DispatchAsync("create_item",
            new { name = "dup-index-item", indexNum = 1, confirmed = true });
        Assert.False(dupItemWrite.Success);
        Assert.Contains(dupItemWrite.Errors, e => e.Code == "ENUM_ITEM_INDEX_CONFLICT");

        // demo_active (index_num=1) is a real member of demo_status (group_id
        // 22222222-2222-2222-2222-222222222201, db/enum_seed.sql) -- deleting it must fail close
        // rather than orphan enum.group_items, dryRun and confirmed alike.
        var referencedDeleteDryRun = await DispatchAsync("delete_item", new { indexNum = 1, dryRun = true });
        Assert.False(referencedDeleteDryRun.Success);
        Assert.Contains(referencedDeleteDryRun.Errors, e => e.Code == "ENUM_ITEM_IN_USE");
        var referencedDeleteWrite = await DispatchAsync("delete_item", new { indexNum = 1, confirmed = true });
        Assert.False(referencedDeleteWrite.Success);
        Assert.Contains(referencedDeleteWrite.Errors, e => e.Code == "ENUM_ITEM_IN_USE");

        // demo_active (index_num=1) is a real member of demo_status -- changing its index_num must
        // fail close the same way (enum.group_items.enum_index_num REFERENCES enum.items(index_num)
        // with no ON UPDATE CASCADE, db/enum_tables.sql). Renaming (no index_num change, verified via
        // the update_item write-manifest round trip test below) remains allowed for group members;
        // this proves specifically the index_num-change path fails close against a real
        // ForeignKeyViolation rather than leaking a raw PostgresException, dryRun and confirmed alike.
        var referencedIndexChangeDryRun = await DispatchAsync("update_item",
            new { indexNum = 1, newIndexNum = 88_888, dryRun = true });
        Assert.False(referencedIndexChangeDryRun.Success);
        Assert.Contains(referencedIndexChangeDryRun.Errors, e => e.Code == "ENUM_ITEM_IN_USE");
        var referencedIndexChangeWrite = await DispatchAsync("update_item",
            new { indexNum = 1, newIndexNum = 88_888, confirmed = true });
        Assert.False(referencedIndexChangeWrite.Success);
        Assert.Contains(referencedIndexChangeWrite.Errors, e => e.Code == "ENUM_ITEM_IN_USE");

        const string demoStatusGroupId = "22222222-2222-2222-2222-222222222201";

        // Duplicate membership within the same enumIndexNums array must fail close, dryRun and
        // confirmed alike, never partially applying the list.
        var dupMembershipDryRun = await DispatchAsync("set_group_items",
            new { groupId = demoStatusGroupId, enumIndexNums = new[] { 1, 1, 2 }, dryRun = true });
        Assert.False(dupMembershipDryRun.Success);
        Assert.Contains(dupMembershipDryRun.Errors, e => e.Code == "ENUM_GROUP_ITEMS_DUPLICATE_MEMBERSHIP");
        var dupMembershipWrite = await DispatchAsync("set_group_items",
            new { groupId = demoStatusGroupId, enumIndexNums = new[] { 1, 1, 2 }, confirmed = true });
        Assert.False(dupMembershipWrite.Success);
        Assert.Contains(dupMembershipWrite.Errors, e => e.Code == "ENUM_GROUP_ITEMS_DUPLICATE_MEMBERSHIP");

        // A nonexistent item index in enumIndexNums must fail close rather than silently drop it
        // (the pre-fix in-memory repository behavior filtered unknown indexes out silently).
        var nonexistentMemberWrite = await DispatchAsync("set_group_items",
            new { groupId = demoStatusGroupId, enumIndexNums = new[] { 1, 90210 }, confirmed = true });
        Assert.False(nonexistentMemberWrite.Success);
        Assert.Contains(nonexistentMemberWrite.Errors, e => e.Code == "ENUM_ITEM_NOT_FOUND");

        // None of the above negative attempts altered demo_status's real membership.
        var afterPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:get_group",
            groupId = demoStatusGroupId,
        });
        var afterResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "get_group", "manifest", "enum_dictionary", "get_group",
            IdOrHubId: null, Payload: afterPayload, Context: null, TriggerKind: "client", Role: "admin"));
        Assert.True(afterResponse.Success, string.Join(";", afterResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        var afterText = afterResponse.Emission!.Data!.Value.GetRawText();
        Assert.Contains("\"itemsIndexNums\":[1,2,3]", afterText);

        // demo_active's own row (enum.items, index_num=1) is unchanged too -- the failed index-change
        // attempts above did not partially move it before failing close.
        await using (var conn = new NpgsqlConnection(cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM enum.items WHERE index_num = 1";
            Assert.Equal("demo_active", (string?)await cmd.ExecuteScalarAsync());
        }
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

    /// <summary>
    /// Single-purpose write layouts (2026-07-27, PR #600 review round 2 correction):
    /// docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml lane_storage_boundary
    /// remaining_write_payload_capture_gap already settled "a single-purpose write layout,
    /// exactly like the read layout above, is a safe and sufficient composition" -- each of the
    /// 7 enum_dictionary write actions gets its own dedicated manifest (db/seed_empty.sql
    /// ae210/ae220/ae230/ae240/ae250/ae260/ae270), reusing the exact Lane 2
    /// (component_wiring_execution_lane, wiringKind=admin_runtime) + node-level
    /// dispatchPayloadFromByTrigger mechanism ae200's own read circuit and
    /// remaining_write_payload_capture_gap resolution already prove generically. This proves the
    /// SPECIFIC seed content itself (not just the generic mechanism): every write manifest
    /// structurally resolves preview_button/confirm_button to a real componentId, both share the
    /// SAME wiringKind/target_ref (the layout's one canonical operation), and each carries a
    /// distinct, real dispatchPayloadFromByTrigger (dryRun vs confirmed AND every business field
    /// the action's own DTO needs) -- read from the actual dispatched Emission, not a hand-built
    /// map. expectedFieldKeys/expectedFieldSources are parallel arrays (business fields only,
    /// dryRun/confirmed asserted separately below) so every action's exact seed-declared field set
    /// is pinned per action (PR #600 review round 8: create_item's name-only field set --
    /// indexNum is intentionally NOT seed-wired, auto-assigned server-side when absent, same as
    /// create_group's groupName-only set -- and update_item's indexNum+name-only set -- newIndexNum
    /// is intentionally NOT seed-wired, so index-renumbering is reachable only via a direct
    /// dispatch payload today, not through this seeded form -- are asserted explicitly rather than
    /// silently assumed).
    ///
    /// Proven/unproven boundary (this is a backend Integration test, no browser/DOM available
    /// here): this test proves (a) the seed's dispatchPayloadFromByTrigger structurally declares
    /// exactly these node:&lt;id&gt;.value sources for exactly these keys, and (b) dispatching a
    /// payload built with these exact key names reaches AdminRuntime and mutates/previews
    /// correctly (see the per-action round-trip tests below, which use
    /// DispatchViaOwnWriteManifestAsync with these same key names). It does NOT prove that a real
    /// browser's DOM input events are actually tracked into these exact node ids and forwarded
    /// through payloadFromResolver at runtime -- that generic, seed-independent mechanism
    /// (liveNodeValueTracker / payloadFromResolver / emitBoundEvent Lane 2) is covered by
    /// frontend/tests/payloadFromResolver.test.ts, runtimeComponentFactory.test.ts, and
    /// renderEmissionPropBindings.test.ts. Together these compose into full reachability, but no
    /// single test in this repository exercises the whole DOM-to-database path in one run; this
    /// test never uses the word "end-to-end" for that reason.
    /// </summary>
    [Theory]
    [InlineData("00000000-0000-0000-0000-0000000ae210", "create_group",
        new[] { "groupName" }, new[] { "node:group_name_field.value" })]
    [InlineData("00000000-0000-0000-0000-0000000ae220", "update_group",
        new[] { "groupId", "groupName" }, new[] { "node:group_id_field.value", "node:group_name_field.value" })]
    [InlineData("00000000-0000-0000-0000-0000000ae230", "delete_group",
        new[] { "groupId" }, new[] { "node:group_id_field.value" })]
    [InlineData("00000000-0000-0000-0000-0000000ae240", "create_item",
        new[] { "name" }, new[] { "node:item_name_field.value" })]
    [InlineData("00000000-0000-0000-0000-0000000ae250", "update_item",
        new[] { "indexNum", "name" }, new[] { "node:item_index_field.value", "node:item_name_field.value" })]
    [InlineData("00000000-0000-0000-0000-0000000ae260", "delete_item",
        new[] { "indexNum" }, new[] { "node:item_index_field.value" })]
    [InlineData("00000000-0000-0000-0000-0000000ae270", "set_group_items",
        new[] { "groupId", "enumIndexNums" }, new[] { "node:group_id_field.value", "node:items_csv_field.value" })]
    public async Task DispatchAsync_EnumDictionaryWriteManifest_ResolvesSinglePurposeLayout_WithDistinctPreviewAndConfirmPayloads(
        string manifestId, string action, string[] expectedFieldKeys, string[] expectedFieldSources)
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{manifestId}:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        var nodes = response.Emission!.LayoutNodes!;

        var unresolvedLeaves = nodes.Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null).ToList();
        Assert.Empty(unresolvedLeaves);

        var expectedTargetRef = $"manifest:{manifestId}:enum_dictionary:{action}";
        var previewButton = Assert.Single(nodes, n => n.NodeId == "preview_button");
        Assert.Equal("admin_runtime", previewButton.WiringKind);
        Assert.Equal(expectedTargetRef, previewButton.TargetRef);
        Assert.NotNull(previewButton.DispatchPayloadFromByTrigger);
        var previewClick = previewButton.DispatchPayloadFromByTrigger!.Value.GetProperty("click");
        Assert.Equal("literal:true", previewClick.GetProperty("dryRun").GetString());
        Assert.False(previewClick.TryGetProperty("confirmed", out _));

        var confirmButton = Assert.Single(nodes, n => n.NodeId == "confirm_button");
        Assert.Equal("admin_runtime", confirmButton.WiringKind);
        Assert.Equal(expectedTargetRef, confirmButton.TargetRef);
        Assert.NotNull(confirmButton.DispatchPayloadFromByTrigger);
        var confirmClick = confirmButton.DispatchPayloadFromByTrigger!.Value.GetProperty("click");
        Assert.Equal("literal:true", confirmClick.GetProperty("confirmed").GetString());
        Assert.False(confirmClick.TryGetProperty("dryRun", out _));

        // Business fields: the SAME key set + node source on both buttons (only dryRun/confirmed
        // differ between preview and confirm), and NO other keys beyond dryRun/confirmed/these.
        Assert.Equal(expectedFieldKeys.Length, expectedFieldSources.Length);
        for (var i = 0; i < expectedFieldKeys.Length; i++)
        {
            Assert.Equal(expectedFieldSources[i], previewClick.GetProperty(expectedFieldKeys[i]).GetString());
            Assert.Equal(expectedFieldSources[i], confirmClick.GetProperty(expectedFieldKeys[i]).GetString());
        }
        var expectedPreviewKeys = expectedFieldKeys.Append("dryRun").OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var actualPreviewKeys = previewClick.EnumerateObject().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedPreviewKeys, actualPreviewKeys);
        var expectedConfirmKeys = expectedFieldKeys.Append("confirmed").OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var actualConfirmKeys = confirmClick.EnumerateObject().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedConfirmKeys, actualConfirmKeys);
    }

    /// <summary>
    /// Full write round-trip through the SEED's own dedicated create_group manifest (ae210) --
    /// not ae200 directly -- proving manifest-identity resolution for the new single-purpose
    /// write layout works end to end: dryRun preview (non-mutating), unconfirmed fail-close,
    /// confirmed write, and re-list reflects the diff, exactly like
    /// DispatchAsync_AdminEnumManagementManifest_CreateGroupThenDeleteGroup_PersistsAndReListReflectsDiff
    /// proves for the read manifest's own target_ref shape.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionaryCreateGroupWriteManifest_PreviewThenConfirmedWrite_PersistsAndReListReflectsDiff()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        const string createGroupManifestId = "00000000-0000-0000-0000-0000000ae210";
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var groupName = $"write-manifest-proof-{suffix}";
        string? createdGroupId = null;

        async Task<EndpointResponseDto> DispatchCreateAsync(object extra)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(
                System.Text.Json.JsonSerializer.SerializeToElement(extra).GetRawText())!.AsObject();
            node["target_ref"] = $"manifest:{createGroupManifestId}:enum_dictionary:create_group";
            node["groupName"] = groupName;
            return await dispatcher.DispatchAsync(new EndpointRequestDto(
                "create_group", "manifest", "enum_dictionary", "create_group",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(node),
                Context: null, TriggerKind: "client", Role: "admin"));
        }

        try
        {
            var t0 = DateTimeOffset.UtcNow;

            // preview_button's own resolved shape: dryRun:true, non-mutating, no diff_log.
            var previewResponse = await DispatchCreateAsync(new { dryRun = true });
            Assert.True(previewResponse.Success, string.Join(";", previewResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Contains("\"dryRun\":true", previewResponse.Emission!.Data!.Value.GetRawText());

            var listVector = new EndpointRequestDto(
                "list_groups", "manifest", "enum_dictionary", "list_groups",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new { target_ref = $"manifest:{createGroupManifestId}:enum_dictionary:list_groups" }),
                Context: null, TriggerKind: "client", Role: "admin");
            async Task<string> ListGroupsRawAsync()
            {
                var r = await dispatcher.DispatchAsync(listVector);
                Assert.True(r.Success, string.Join(";", r.Errors.Select(e => e.Code + ":" + e.Message)));
                return r.Emission!.Data!.Value.GetRawText();
            }
            Assert.DoesNotContain(groupName, await ListGroupsRawAsync());

            // confirm_button's own resolved shape: confirmed:true, real write, real diff_log.
            var writeResponse = await DispatchCreateAsync(new { confirmed = true });
            Assert.True(writeResponse.Success, string.Join(";", writeResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            using (var doc = System.Text.Json.JsonDocument.Parse(writeResponse.Emission!.Data!.Value.GetRawText()))
                createdGroupId = doc.RootElement.GetProperty("groupId").GetString();
            Assert.False(string.IsNullOrWhiteSpace(createdGroupId));

            Assert.Contains(groupName, await ListGroupsRawAsync());

            // diff_log stage: the preview/dryRun call (before createdGroupId even exists) added
            // zero rows for the real record; the confirmed write added exactly one, with
            // before={} (no prior state) and after carrying the real created group.
            Assert.Equal(1, await CountLogsDiffRowsAsync(cs, "enum.groups", createdGroupId!, "create", t0));
            var write = await ReadLatestLogsDiffAsync(cs, "enum.groups", createdGroupId!, "create", t0);
            Assert.Equal("{}", write.Before);
            Assert.Contains(groupName, write.After);
            Assert.Contains(createdGroupId!, write.After);
            Assert.Equal("client", write.Actor);

            var groupNameField = GetChangedField(write.ChangedFieldsJson, "group_name");
            Assert.Equal("string", groupNameField.GetProperty("type").GetString());
            Assert.Equal(JsonValueKind.Null, groupNameField.GetProperty("before").ValueKind);
            Assert.Equal(groupName, groupNameField.GetProperty("after").GetString());
        }
        finally
        {
            if (createdGroupId is not null)
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM enum.groups WHERE group_id = @id";
                cmd.Parameters.AddWithValue("id", Guid.Parse(createdGroupId));
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Navigation reachability for the write manifests (governance requirement: hub relation
    /// authoring and target manifest resolution proven via the existing /admin/manifests
    /// authority and live-DB resolution-chain helper, same pattern as
    /// DispatchAsync_AdminEnumManagementManifest_HubNavigationCreate_RealAuthoringPath_
    /// ThenResolutionChainReflectsIt above): authors a real hub_relations row FROM ae200 TO the
    /// create_group write manifest via the real hub_navigation:create dispatch action, then
    /// re-dispatches ae200 and asserts the resolution chain reflects it.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_HubNavigationCreate_ToCreateGroupWriteManifest_ResolutionChainReflectsIt()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        const string createGroupManifestId = "00000000-0000-0000-0000-0000000ae210";
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        // create_group's own dedicated hub (db/seed_empty.sql 00000000-0000-0000-0000-0000000ae211
        // -- NOT shared across the 7 write manifests: LoadHubNavigationSequenceAsync only
        // resolves a target manifest when exactly one ACTIVE topology_manifests row exists per
        // hub_id, so a shared hub across multiple active manifests would resolve to NULL).
        var createGroupHubId = Guid.Parse("00000000-0000-0000-0000-0000000ae211");

        Guid? createdHubRelationId = null;
        try
        {
            var createPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = AdminEnumManagementManifestId.ToString(),
                relatedHubId = createGroupHubId.ToString(),
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
            var relations = await contentBundleRepo.ListHubRelationsByManifestAsync(AdminEnumManagementManifestId);
            var created = Assert.Single(relations, r => r.RelatedHubId == createGroupHubId.ToString());
            createdHubRelationId = Guid.Parse(created.HubRelationId);

            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:projection_entry",
            });
            var request = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);
            Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));

            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                response.Emission!,
                AdminEnumManagementManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(
                    createGroupHubId, 1, Guid.Parse(createGroupManifestId))]);
        }
        finally
        {
            if (createdHubRelationId is not null)
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM hubs.hub_relations WHERE hub_relation_id = @rid";
                cmd.Parameters.AddWithValue("rid", createdHubRelationId.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private static readonly IReadOnlyDictionary<string, string> WriteManifestIdByAction = new Dictionary<string, string>
    {
        ["create_group"] = "00000000-0000-0000-0000-0000000ae210",
        ["update_group"] = "00000000-0000-0000-0000-0000000ae220",
        ["delete_group"] = "00000000-0000-0000-0000-0000000ae230",
        ["create_item"] = "00000000-0000-0000-0000-0000000ae240",
        ["update_item"] = "00000000-0000-0000-0000-0000000ae250",
        ["delete_item"] = "00000000-0000-0000-0000-0000000ae260",
        ["set_group_items"] = "00000000-0000-0000-0000-0000000ae270",
    };

    /// <summary>
    /// Dispatches enum_dictionary:{action} through THAT action's own dedicated write manifest's
    /// identity (target_ref = "manifest:{ae2X0}:enum_dictionary:{action}") -- proving manifest
    /// resolution for that specific write manifest, not just the generic backend action.
    /// </summary>
    private static async Task<EndpointResponseDto> DispatchViaOwnWriteManifestAsync(
        ManifestDispatcher dispatcher, string action, object payload)
    {
        var manifestId = WriteManifestIdByAction[action];
        var node = System.Text.Json.Nodes.JsonNode.Parse(
            System.Text.Json.JsonSerializer.SerializeToElement(payload).GetRawText())!.AsObject();
        node["target_ref"] = $"manifest:{manifestId}:enum_dictionary:{action}";
        return await dispatcher.DispatchAsync(new EndpointRequestDto(
            action, "manifest", "enum_dictionary", action,
            IdOrHubId: null,
            Payload: System.Text.Json.JsonSerializer.SerializeToElement(node),
            Context: null, TriggerKind: "client", Role: "admin"));
    }

    /// <summary>
    /// diff_log stage of mutation_confirmation_contract (AdminMasterRosterAudit.AppendAsync ->
    /// logs.diff, source_set_id='admin_master_roster' per docs/design/admin-master-roster-
    /// management-ssot.yaml logs_diff_admin_projection). Counts real logs.diff rows for the given
    /// physical_table_name/record_id/operation_kind observed since a baseline timestamp --
    /// HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync now wires a real
    /// NpgsqlSqlAttentionLogsRepository, so AppendAsync's prior silent no-op (repository null) no
    /// longer masks whether diff_log actually persists.
    /// </summary>
    private static async Task<int> CountLogsDiffRowsAsync(
        string cs, string physicalTableName, string recordId, string operationKind, DateTimeOffset since)
    {
        await using var conn = new NpgsqlConnection(cs);
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

    /// <summary>
    /// Fetches the single most recent matching logs.diff row's before/after/actor/changed_fields_json,
    /// for asserting actual diff content (not just row presence). Covers 4 of the 8
    /// logical_envelope_fields declared by docs/design/admin-master-roster-management-ssot.yaml
    /// logs_diff_admin_projection beyond target_table/target_id/operation (already the query's own
    /// WHERE clause) and timestamp (already bounded by the since-t0 window): before, after, actor,
    /// changed_fields. changed_fields is now physically persisted (2026-07-27, PR #600 review round
    /// 7/8/9 audit-envelope resolution) as the generic changed_fields_json JSONB column
    /// (db/sql_attention_logs_tables.sql) -- AdminMasterRosterAudit.AppendAsync's envelope (previously
    /// built but never passed to AppendLogsDiffAsync) is now actually written; see
    /// ChangedFieldsJson below and its per-action assertions.
    /// The returned Actor asserts only that actor_or_source is physically persisted with the value
    /// ResolveAuditActor actually resolved for this dispatch (here, the TriggerKind "client" fallback,
    /// since these test dispatches carry no JWT/AuthenticatedUserId) -- it is NOT a proof of
    /// authenticated actor authority (a separate, already-resolved gap -- see ResolveAuditActor's own
    /// comment in AdminRuntimeMasterRoster.cs referencing auth-db-session-credential-ssot.yaml).
    /// </summary>
    private static async Task<(string Before, string After, string? Actor, string ChangedFieldsJson)> ReadLatestLogsDiffAsync(
        string cs, string physicalTableName, string recordId, string operationKind, DateTimeOffset since)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT before_state_or_diff_json::text, after_state_or_diff_json::text, actor_or_source,
                   changed_fields_json::text
            FROM logs.diff
            WHERE source_set_id = 'admin_master_roster'
              AND physical_table_name = @t
              AND record_id = @r
              AND operation_kind = @op
              AND observed_at >= @since
            ORDER BY observed_at DESC
            LIMIT 1";
        cmd.Parameters.AddWithValue("t", physicalTableName);
        cmd.Parameters.AddWithValue("r", recordId);
        cmd.Parameters.AddWithValue("op", operationKind);
        cmd.Parameters.AddWithValue("since", since);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"expected a logs.diff row for {physicalTableName}/{recordId}/{operationKind} since {since:o}");
        return (reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3));
    }

    /// <summary>
    /// Parses a persisted changed_fields_json envelope and returns the named field entry (cloned so
    /// it survives the backing JsonDocument's disposal). Asserts schemaVersion=1 (the current
    /// AdminMasterRosterAudit envelope contract) as a side effect on every call.
    /// </summary>
    private static JsonElement GetChangedField(string changedFieldsJson, string name)
    {
        using var doc = JsonDocument.Parse(changedFieldsJson);
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        foreach (var field in doc.RootElement.GetProperty("changedFields").EnumerateArray())
        {
            if (field.GetProperty("name").GetString() == name)
                return field.Clone();
        }
        throw new Xunit.Sdk.XunitException($"changedFields entry '{name}' not found in {changedFieldsJson}");
    }

    /// <summary>
    /// Full preview/confirm/write/re-read round trip through update_group's own dedicated write
    /// manifest (ae220) -- the remaining 6 write manifests' per-action proof requested in PR #600
    /// review (create_group's own round trip is proven above; this is the same pattern applied to
    /// the other 6, not a re-proof of the shared mechanism).
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionaryUpdateGroupWriteManifest_PreviewThenConfirmedWrite_PersistsAndReReadReflectsDiff()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var originalName = $"update-manifest-proof-original-{suffix}";
        var renamedName = $"update-manifest-proof-renamed-{suffix}";
        string? groupId = null;
        try
        {
            var created = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_group",
                new { groupName = originalName, confirmed = true });
            Assert.True(created.Success, string.Join(";", created.Errors.Select(e => e.Code + ":" + e.Message)));
            using (var doc = System.Text.Json.JsonDocument.Parse(created.Emission!.Data!.Value.GetRawText()))
                groupId = doc.RootElement.GetProperty("groupId").GetString();

            var t0 = DateTimeOffset.UtcNow;

            var preview = await DispatchViaOwnWriteManifestAsync(dispatcher, "update_group",
                new { groupId, groupName = renamedName, dryRun = true });
            Assert.True(preview.Success, string.Join(";", preview.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Contains("\"dryRun\":true", preview.Emission!.Data!.Value.GetRawText());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.groups", groupId!, "update", t0));

            var unconfirmed = await DispatchViaOwnWriteManifestAsync(dispatcher, "update_group",
                new { groupId, groupName = renamedName });
            Assert.False(unconfirmed.Success);
            Assert.Contains(unconfirmed.Errors, e => e.Code == "ENUM_GROUP_WRITE_NOT_CONFIRMED");
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.groups", groupId!, "update", t0));

            var write = await DispatchViaOwnWriteManifestAsync(dispatcher, "update_group",
                new { groupId, groupName = renamedName, confirmed = true });
            Assert.True(write.Success, string.Join(";", write.Errors.Select(e => e.Code + ":" + e.Message)));

            var relist = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "list_groups", "manifest", "enum_dictionary", "list_groups",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new { target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups" }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(relist.Success);
            var relistText = relist.Emission!.Data!.Value.GetRawText();
            Assert.Contains(renamedName, relistText);
            Assert.DoesNotContain(originalName, relistText);

            // diff_log stage: exactly one row, before carries the original name, after the renamed one.
            Assert.Equal(1, await CountLogsDiffRowsAsync(cs, "enum.groups", groupId!, "update", t0));
            var diff = await ReadLatestLogsDiffAsync(cs, "enum.groups", groupId!, "update", t0);
            Assert.Contains(originalName, diff.Before);
            Assert.Contains(renamedName, diff.After);

            var groupNameField = GetChangedField(diff.ChangedFieldsJson, "group_name");
            Assert.Equal(originalName, groupNameField.GetProperty("before").GetString());
            Assert.Equal(renamedName, groupNameField.GetProperty("after").GetString());
            Assert.Equal("client", diff.Actor);
        }
        finally
        {
            if (groupId is not null)
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM enum.groups WHERE group_id = @id";
                cmd.Parameters.AddWithValue("id", Guid.Parse(groupId));
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Full preview/confirm/write/re-read round trip through delete_group's own dedicated write
    /// manifest (ae230).
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionaryDeleteGroupWriteManifest_PreviewThenConfirmedWrite_PersistsAndReReadReflectsDiff()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var groupName = $"delete-manifest-proof-{suffix}";
        string? groupId = null;
        try
        {
            var created = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_group",
                new { groupName, confirmed = true });
            Assert.True(created.Success, string.Join(";", created.Errors.Select(e => e.Code + ":" + e.Message)));
            using (var doc = System.Text.Json.JsonDocument.Parse(created.Emission!.Data!.Value.GetRawText()))
                groupId = doc.RootElement.GetProperty("groupId").GetString();

            var t0 = DateTimeOffset.UtcNow;
            var deletedGroupId = groupId!;

            var preview = await DispatchViaOwnWriteManifestAsync(dispatcher, "delete_group", new { groupId, dryRun = true });
            Assert.True(preview.Success, string.Join(";", preview.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Contains("\"dryRun\":true", preview.Emission!.Data!.Value.GetRawText());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.groups", deletedGroupId, "delete", t0));

            var unconfirmed = await DispatchViaOwnWriteManifestAsync(dispatcher, "delete_group", new { groupId });
            Assert.False(unconfirmed.Success);
            Assert.Contains(unconfirmed.Errors, e => e.Code == "ENUM_GROUP_WRITE_NOT_CONFIRMED");
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.groups", deletedGroupId, "delete", t0));

            var write = await DispatchViaOwnWriteManifestAsync(dispatcher, "delete_group", new { groupId, confirmed = true });
            Assert.True(write.Success, string.Join(";", write.Errors.Select(e => e.Code + ":" + e.Message)));
            groupId = null; // deleted -- no cleanup needed.

            var relist = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "list_groups", "manifest", "enum_dictionary", "list_groups",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new { target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups" }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(relist.Success);
            Assert.DoesNotContain(groupName, relist.Emission!.Data!.Value.GetRawText());

            // diff_log stage: exactly one row, before carries the deleted group's name, after={}.
            Assert.Equal(1, await CountLogsDiffRowsAsync(cs, "enum.groups", deletedGroupId, "delete", t0));
            var diff = await ReadLatestLogsDiffAsync(cs, "enum.groups", deletedGroupId, "delete", t0);
            Assert.Contains(groupName, diff.Before);
            Assert.Equal("{}", diff.After);
            Assert.Equal("client", diff.Actor);

            var groupIdField = GetChangedField(diff.ChangedFieldsJson, "group_id");
            Assert.Equal("string", groupIdField.GetProperty("type").GetString());
            Assert.Equal(deletedGroupId, groupIdField.GetProperty("before").GetString());
            Assert.Equal(JsonValueKind.Null, groupIdField.GetProperty("after").ValueKind);
        }
        finally
        {
            if (groupId is not null)
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM enum.groups WHERE group_id = @id";
                cmd.Parameters.AddWithValue("id", Guid.Parse(groupId));
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Full preview/confirm/write/re-read round trip through create_item's own dedicated write
    /// manifest (ae240). No list_items admin_runtime action exists (enum_dictionary read actions
    /// are list_groups/get_group only), so the diff/evidence check queries enum.items directly --
    /// the same real-row-verification pattern the finally-cleanup blocks throughout this file
    /// already use.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionaryCreateItemWriteManifest_PreviewThenConfirmedWrite_PersistsAndRowExists()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var indexNum = 900_000 + Random.Shared.Next(0, 90_000);
        var name = $"create-item-manifest-proof-{Guid.NewGuid():N}"[..40];

        async Task<int> CountRowsAsync()
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*)::int FROM enum.items WHERE index_num = @i";
            cmd.Parameters.AddWithValue("i", indexNum);
            return (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        try
        {
            var t0 = DateTimeOffset.UtcNow;
            Assert.Equal(0, await CountRowsAsync());

            var preview = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_item",
                new { name, indexNum, dryRun = true });
            Assert.True(preview.Success, string.Join(";", preview.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Contains("\"dryRun\":true", preview.Emission!.Data!.Value.GetRawText());
            Assert.Equal(0, await CountRowsAsync());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.items", indexNum.ToString(), "create", t0));

            var unconfirmed = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_item", new { name, indexNum });
            Assert.False(unconfirmed.Success);
            Assert.Contains(unconfirmed.Errors, e => e.Code == "ENUM_ITEM_WRITE_NOT_CONFIRMED");
            Assert.Equal(0, await CountRowsAsync());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.items", indexNum.ToString(), "create", t0));

            var write = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_item",
                new { name, indexNum, confirmed = true });
            Assert.True(write.Success, string.Join(";", write.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(1, await CountRowsAsync());

            // diff_log stage: exactly one row, before={}, after carries the real created item.
            Assert.Equal(1, await CountLogsDiffRowsAsync(cs, "enum.items", indexNum.ToString(), "create", t0));
            var diff = await ReadLatestLogsDiffAsync(cs, "enum.items", indexNum.ToString(), "create", t0);
            Assert.Equal("{}", diff.Before);
            Assert.Contains(name, diff.After);
            Assert.Equal("client", diff.Actor);

            var nameField = GetChangedField(diff.ChangedFieldsJson, "name");
            Assert.Equal(JsonValueKind.Null, nameField.GetProperty("before").ValueKind);
            Assert.Equal(name, nameField.GetProperty("after").GetString());
        }
        finally
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM enum.items WHERE index_num = @i";
            cmd.Parameters.AddWithValue("i", indexNum);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Full preview/confirm/write/re-read round trip through update_item's own dedicated write
    /// manifest (ae250).
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionaryUpdateItemWriteManifest_PreviewThenConfirmedWrite_PersistsAndRowReflectsDiff()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var indexNum = 900_000 + Random.Shared.Next(0, 90_000);
        var originalName = $"update-item-original-{Guid.NewGuid():N}"[..30];
        var renamedName = $"update-item-renamed-{Guid.NewGuid():N}"[..30];

        async Task<string?> ReadNameAsync()
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM enum.items WHERE index_num = @i";
            cmd.Parameters.AddWithValue("i", indexNum);
            return (string?)await cmd.ExecuteScalarAsync();
        }

        try
        {
            var created = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_item",
                new { name = originalName, indexNum, confirmed = true });
            Assert.True(created.Success, string.Join(";", created.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(originalName, await ReadNameAsync());

            var t0 = DateTimeOffset.UtcNow;

            var preview = await DispatchViaOwnWriteManifestAsync(dispatcher, "update_item",
                new { indexNum, name = renamedName, dryRun = true });
            Assert.True(preview.Success, string.Join(";", preview.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Contains("\"dryRun\":true", preview.Emission!.Data!.Value.GetRawText());
            Assert.Equal(originalName, await ReadNameAsync());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.items", indexNum.ToString(), "update", t0));

            var unconfirmed = await DispatchViaOwnWriteManifestAsync(dispatcher, "update_item", new { indexNum, name = renamedName });
            Assert.False(unconfirmed.Success);
            Assert.Contains(unconfirmed.Errors, e => e.Code == "ENUM_ITEM_WRITE_NOT_CONFIRMED");
            Assert.Equal(originalName, await ReadNameAsync());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.items", indexNum.ToString(), "update", t0));

            var write = await DispatchViaOwnWriteManifestAsync(dispatcher, "update_item",
                new { indexNum, name = renamedName, confirmed = true });
            Assert.True(write.Success, string.Join(";", write.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(renamedName, await ReadNameAsync());

            // diff_log stage: exactly one row, before carries the original name, after the renamed one.
            Assert.Equal(1, await CountLogsDiffRowsAsync(cs, "enum.items", indexNum.ToString(), "update", t0));
            var diff = await ReadLatestLogsDiffAsync(cs, "enum.items", indexNum.ToString(), "update", t0);
            Assert.Contains(originalName, diff.Before);
            Assert.Contains(renamedName, diff.After);
            Assert.Equal("client", diff.Actor);

            var nameField = GetChangedField(diff.ChangedFieldsJson, "name");
            Assert.Equal(originalName, nameField.GetProperty("before").GetString());
            Assert.Equal(renamedName, nameField.GetProperty("after").GetString());
        }
        finally
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM enum.items WHERE index_num = @i";
            cmd.Parameters.AddWithValue("i", indexNum);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Full preview/confirm/write/re-read round trip through delete_item's own dedicated write
    /// manifest (ae260).
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionaryDeleteItemWriteManifest_PreviewThenConfirmedWrite_PersistsAndRowGone()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var indexNum = 900_000 + Random.Shared.Next(0, 90_000);
        var name = $"delete-item-proof-{Guid.NewGuid():N}"[..30];

        async Task<int> CountRowsAsync()
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*)::int FROM enum.items WHERE index_num = @i";
            cmd.Parameters.AddWithValue("i", indexNum);
            return (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        try
        {
            var created = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_item",
                new { name, indexNum, confirmed = true });
            Assert.True(created.Success, string.Join(";", created.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(1, await CountRowsAsync());

            var t0 = DateTimeOffset.UtcNow;

            var preview = await DispatchViaOwnWriteManifestAsync(dispatcher, "delete_item", new { indexNum, dryRun = true });
            Assert.True(preview.Success, string.Join(";", preview.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Contains("\"dryRun\":true", preview.Emission!.Data!.Value.GetRawText());
            Assert.Equal(1, await CountRowsAsync());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.items", indexNum.ToString(), "delete", t0));

            var unconfirmed = await DispatchViaOwnWriteManifestAsync(dispatcher, "delete_item", new { indexNum });
            Assert.False(unconfirmed.Success);
            Assert.Contains(unconfirmed.Errors, e => e.Code == "ENUM_ITEM_WRITE_NOT_CONFIRMED");
            Assert.Equal(1, await CountRowsAsync());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.items", indexNum.ToString(), "delete", t0));

            var write = await DispatchViaOwnWriteManifestAsync(dispatcher, "delete_item", new { indexNum, confirmed = true });
            Assert.True(write.Success, string.Join(";", write.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(0, await CountRowsAsync());

            // diff_log stage: exactly one row, before carries the deleted item's name, after={}.
            Assert.Equal(1, await CountLogsDiffRowsAsync(cs, "enum.items", indexNum.ToString(), "delete", t0));
            var diff = await ReadLatestLogsDiffAsync(cs, "enum.items", indexNum.ToString(), "delete", t0);
            Assert.Contains(name, diff.Before);
            Assert.Equal("{}", diff.After);
            Assert.Equal("client", diff.Actor);

            var indexNumField = GetChangedField(diff.ChangedFieldsJson, "index_num");
            Assert.Equal("number", indexNumField.GetProperty("type").GetString());
            Assert.Equal(indexNum, indexNumField.GetProperty("before").GetInt32());
            Assert.Equal(JsonValueKind.Null, indexNumField.GetProperty("after").ValueKind);
        }
        finally
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM enum.items WHERE index_num = @i";
            cmd.Parameters.AddWithValue("i", indexNum);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Full preview/confirm/write/re-read round trip through set_group_items's own dedicated
    /// write manifest (ae270) -- covers update_group_members, the SSOT mutation intent this
    /// action resolves. Uses the CSV-string enumIndexNums shape a single text field's tracked
    /// node value produces (see backend/runtime/AdminRuntimeMasterRoster.cs
    /// TryParseSetGroupItemsPayload), not a native JSON array.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionarySetGroupItemsWriteManifest_PreviewThenConfirmedWrite_PersistsAndGetGroupReflectsDiff()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var groupName = $"set-items-manifest-proof-{suffix}";
        string? groupId = null;

        async Task<string> GetGroupRawAsync()
        {
            var r = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "get_group", "manifest", "enum_dictionary", "get_group",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:get_group",
                    groupId,
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(r.Success, string.Join(";", r.Errors.Select(e => e.Code + ":" + e.Message)));
            return r.Emission!.Data!.Value.GetRawText();
        }

        // get_group fails close on a zero-item group (ENUM_GROUP_ITEMS_EMPTY, existing behavior
        // unrelated to this Bundle) -- so the pre-write "still empty" assertions below query
        // enum.group_items directly, the same real-row-verification pattern this file's cleanup
        // blocks already use; only the post-write (non-empty) state is verified via get_group.
        async Task<int> MembershipCountAsync()
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*)::int FROM enum.group_items WHERE group_id = @g";
            cmd.Parameters.AddWithValue("g", Guid.Parse(groupId!));
            return (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        try
        {
            var created = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_group",
                new { groupName, confirmed = true });
            Assert.True(created.Success, string.Join(";", created.Errors.Select(e => e.Code + ":" + e.Message)));
            using (var doc = System.Text.Json.JsonDocument.Parse(created.Emission!.Data!.Value.GetRawText()))
                groupId = doc.RootElement.GetProperty("groupId").GetString();
            Assert.Equal(0, await MembershipCountAsync());

            var t0 = DateTimeOffset.UtcNow;

            var preview = await DispatchViaOwnWriteManifestAsync(dispatcher, "set_group_items",
                new { groupId, enumIndexNums = "10, 11", dryRun = true });
            Assert.True(preview.Success, string.Join(";", preview.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Contains("\"dryRun\":true", preview.Emission!.Data!.Value.GetRawText());
            Assert.Equal(0, await MembershipCountAsync());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.group_items", groupId!, "update", t0));

            var unconfirmed = await DispatchViaOwnWriteManifestAsync(dispatcher, "set_group_items",
                new { groupId, enumIndexNums = "10, 11" });
            Assert.False(unconfirmed.Success);
            Assert.Contains(unconfirmed.Errors, e => e.Code == "ENUM_GROUP_WRITE_NOT_CONFIRMED");
            Assert.Equal(0, await MembershipCountAsync());
            Assert.Equal(0, await CountLogsDiffRowsAsync(cs, "enum.group_items", groupId!, "update", t0));

            var write = await DispatchViaOwnWriteManifestAsync(dispatcher, "set_group_items",
                new { groupId, enumIndexNums = "10, 11", confirmed = true });
            Assert.True(write.Success, string.Join(";", write.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Contains("\"itemsIndexNums\":[10,11]", await GetGroupRawAsync());

            Assert.Equal(1, await CountLogsDiffRowsAsync(cs, "enum.group_items", groupId!, "update", t0));
            var diff = await ReadLatestLogsDiffAsync(cs, "enum.group_items", groupId!, "update", t0);
            using (var beforeDoc = System.Text.Json.JsonDocument.Parse(diff.Before))
                Assert.Empty(beforeDoc.RootElement.GetProperty("itemsIndexNums").EnumerateArray());
            using (var afterDoc = System.Text.Json.JsonDocument.Parse(diff.After))
                Assert.Equal(
                    new[] { 10, 11 },
                    afterDoc.RootElement.GetProperty("itemsIndexNums").EnumerateArray().Select(e => e.GetInt32()));
            Assert.Equal("client", diff.Actor);

            var itemsField = GetChangedField(diff.ChangedFieldsJson, "itemsIndexNums");
            Assert.Equal("object", itemsField.GetProperty("type").GetString());
            Assert.Empty(itemsField.GetProperty("before").EnumerateArray());
            Assert.Equal(
                new[] { 10, 11 },
                itemsField.GetProperty("after").EnumerateArray().Select(e => e.GetInt32()));
        }
        finally
        {
            if (groupId is not null)
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM enum.groups WHERE group_id = @id";
                cmd.Parameters.AddWithValue("id", Guid.Parse(groupId));
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
