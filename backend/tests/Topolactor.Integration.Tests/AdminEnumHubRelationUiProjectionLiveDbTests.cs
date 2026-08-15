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

        // enum_confirm_form/enum_form/enum_confirm_button (the original structural-leaf-only
        // edit-and-confirm stub, predating the per-operation write manifests ae210-ae270 and
        // their real dryRun-preview/confirm-modal wiring) were removed as orphans: zero frontend
        // consumers, and enum_confirm_button's own dispatch was a ui-local no-op with no backend
        // target and no consumer of the local "confirm" state it opened. Fully superseded by the
        // 7 real per-operation confirm modals below (see .agent/tasks/todo.md admin-enum
        // subBundle closure record).

        // render completion: every catalog_component leaf resolved either a registry componentId
        // (Field/Table/Action/WorkflowStep) or, for a Modal, its own literal componentKind
        // directly -- Modal is a built-in runtime primitive, never registry-backed. A leaf with
        // BOTH ComponentId and ComponentKind null is the real gap this assertion exists to catch.
        var unresolvedLeaves = nodes
            .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null && n.ComponentKind is null)
            .ToList();
        Assert.Empty(unresolvedLeaves);

        var confirmModal = Assert.Single(nodes, n => n.NodeId == "enum_delete_group_confirm_modal");
        Assert.Equal("catalog_component", confirmModal.NodeKind);
        Assert.Equal("disclosure/modal", confirmModal.ComponentKind);
        // ComponentId is the Modal's own resolved NodeId (round 25 fix): frontend/runtime/
        // runtimeComponentAdapter.ts adaptComponentDataHub fails closed
        // (RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_ID) on an empty/null componentId before
        // componentKind is even inspected -- round 24 left this null, which would have silently
        // rendered every Modal node as a render-time error instead of a working dialog, never
        // reaching modalFactory at all. Caught here, and by a real DOM mount test, before this
        // round's fix.
        Assert.Equal("enum_delete_group_confirm_modal", confirmModal.ComponentId);

        Assert.Empty(emission.Errors);
    }

    /// <summary>
    /// admin-enum subBundle closure round (.agent/tasks/todo.md): mirrors CredentialManagement
    /// HubRelationUiProjectionLiveDbTests's own manifest_0092_bare_entry_layout_nodes.json pattern.
    /// frontend/tests/fixtures/admin_enum_ae200_layout_nodes.json is a checked-in snapshot of
    /// THIS EXACT dispatch's Emission.LayoutNodes (camelCase-serialized, the same shape the
    /// frontend receives over HTTP -- backend/Program.cs JsonNamingPolicy.CamelCase), consumed by
    /// frontend/tests/projectionShellAdminEnumCanonicalSeedShape.test.ts as its mocked initial
    /// dispatch response -- a production DOM proof driven by the REAL canonical translator-
    /// generated ae200 layout, never a hand-built layoutNodes object standing in for it. If
    /// ae200's seed ever changes, this test fails HERE (a live-DB dispatch against real
    /// PostgreSQL), not by the frontend fixture silently going stale against real data.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_LayoutNodesMatchCheckedInFrontendFixture()
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
        Assert.NotNull(response.Emission!.LayoutNodes);

        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../../frontend/tests/fixtures/admin_enum_ae200_layout_nodes.json"));
        var expectedJson = await File.ReadAllTextAsync(fixturePath);
        // Options must mirror backend/Program.cs's actual wire serialization exactly (including
        // DefaultIgnoreCondition) -- the fixture is a snapshot of what the frontend really
        // receives over HTTP, not an arbitrary debug dump. A field-shape drift (e.g. a new
        // LayoutNode field, or a null-handling change) must fail HERE, not silently pass while
        // the checked-in fixture and the real DTO diverge.
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
    /// Round 19 (admin-write-surface-selection-context-and-mode-composition-gap Bundle): the
    /// FIRST of the 7 write actions (create_group) wired directly into ae200's own single-surface
    /// layout via a node-local dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger override
    /// (db/seed_empty.sql enum_create_group_confirm_button node -- generated by
    /// .agent/tools/react-schema-topology-seed-translator from
    /// admin-enum-ae200.input.json/admin-enum-ae200.topology-seed.input.json, adopted verbatim,
    /// not hand-invented). Round 26: create_group now goes through the SAME disclosure/modal
    /// explicit-confirm pattern as delete_group (round 24/25) -- the visible
    /// enum_create_group_button only opens enum_create_group_confirm_modal (no dispatch of its
    /// own, fixing a round-25 NG-axis violation where this button sent confirmed:true directly on
    /// a single click); the real dispatch moved onto enum_create_group_confirm_button inside the
    /// modal. Unlike DispatchAsync_AdminEnumManagementManifest_CreateGroupThenDeleteGroup_
    /// PersistsAndReListReflectsDiff below (which proves the create_group EXECUTION semantics by
    /// constructing the target_ref/payload directly), this test proves the SEED -> DB ->
    /// StructureMapResolver -> Emission.LayoutNodes pipeline actually SURFACES the new node's
    /// override fields when ae200's own layout renders -- i.e. what
    /// frontend/runtime/renderEmission.ts would read to build the real dispatch a user's click
    /// produces -- and that dispatching through that EXACT node-local override (not a
    /// test-authored target_ref pointing straight at ae210) persists a real row and is reflected
    /// in a subsequent list_groups reread through ae200's own uniform read circuit.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_CreateGroupFormNode_SurfacesDispatchOverride_AndExecutesViaAe210Authority()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        // Step 1: render ae200's own layout and confirm enum_create_group_confirm_button (the LEAF
        // Action node inside the Modal -- not enum_create_group_button, which only opens the
        // dialog, same shape as delete_group's confirm modal) surfaces the EXACT override the seed
        // declares -- this is the node-local data a real click would read.
        var entryPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:projection_entry",
        });
        var entryRequest = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: entryPayload, Context: null, TriggerKind: "client", Role: "admin");
        var entryResponse = await dispatcher.DispatchAsync(entryRequest);
        Assert.True(entryResponse.Success, string.Join(";", entryResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(entryResponse.Emission!.LayoutNodes);
        var createGroupConfirmButtonNode = Assert.Single(
            entryResponse.Emission!.LayoutNodes!, n => n.NodeId == "enum_create_group_confirm_button");
        Assert.NotNull(createGroupConfirmButtonNode.DispatchTargetRefByTrigger);
        var targetRefByTriggerText = createGroupConfirmButtonNode.DispatchTargetRefByTrigger!.Value.GetRawText();
        Assert.Contains("manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group", targetRefByTriggerText);
        Assert.NotNull(createGroupConfirmButtonNode.DispatchPayloadFromByTrigger);
        var payloadFromByTriggerText = createGroupConfirmButtonNode.DispatchPayloadFromByTrigger!.Value.GetRawText();
        Assert.Contains("node:enum_create_group_name_input.value", payloadFromByTriggerText);
        Assert.Contains("literal:true", payloadFromByTriggerText);

        // preview-gap round: the visible Create trigger now ALSO carries a non-mutating dryRun
        // preview dispatch to the SAME ae210 target -- the modal only opens once that preview
        // dispatch settles successfully (deferred secondaryDisclosureActionType=openModal), never
        // unconditionally on click.
        var createGroupButtonNode = Assert.Single(
            entryResponse.Emission!.LayoutNodes!, n => n.NodeId == "enum_create_group_button");
        Assert.NotNull(createGroupButtonNode.DispatchTargetRefByTrigger);
        Assert.Contains(
            "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group",
            createGroupButtonNode.DispatchTargetRefByTrigger!.Value.GetRawText());
        Assert.NotNull(createGroupButtonNode.DispatchPayloadFromByTrigger);
        var previewPayloadFromText = createGroupButtonNode.DispatchPayloadFromByTrigger!.Value.GetRawText();
        Assert.Contains("node:enum_create_group_name_input.value", previewPayloadFromText);
        Assert.Contains("\"dryRun\"", previewPayloadFromText);
        Assert.Contains("literal:true", previewPayloadFromText);
        Assert.DoesNotContain("confirmed", previewPayloadFromText);
        Assert.NotNull(createGroupButtonNode.RuntimeInteractions);
        Assert.Contains("openModal", createGroupButtonNode.RuntimeInteractions!.Value.GetRawText());

        var createGroupConfirmModalNode = Assert.Single(
            entryResponse.Emission!.LayoutNodes!, n => n.NodeId == "enum_create_group_confirm_modal");
        Assert.Equal("disclosure/modal", createGroupConfirmModalNode.ComponentKind);
        Assert.NotNull(createGroupConfirmModalNode.RuntimeInteractions);
        Assert.Contains("\"trigger\":\"toggle\"", createGroupConfirmModalNode.RuntimeInteractions!.Value.GetRawText());

        // Step 2: dispatch through that EXACT node-local override target_ref (Target="manifest"
        // matching the layout's own uniform target_surface, Layer/Action parsed from the
        // override's OWN embedded layer:action -- exactly what
        // buildAdminRuntimeTargetRefOverrideByTrigger produces for a real "click"), proving the
        // seeded override is not just present but genuinely authorizes and executes under ae210's
        // OWN dispatcher_mapping/capability_requirement, and that the write is visible through
        // ae200's own separate uniform list_groups read circuit afterward.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var groupName = $"live-db-ae200-create-group-form-node-{suffix}";

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

        Assert.DoesNotContain(groupName, await ListGroupsRawAsync());

        var overrideTargetRef = "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group";
        var createPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = overrideTargetRef,
            groupName,
            confirmed = true,
        });
        var createRequest = new EndpointRequestDto(
            "create_group", "manifest", "enum_dictionary", "create_group",
            IdOrHubId: null, Payload: createPayload, Context: null, TriggerKind: "client", Role: "admin");
        var createResponse = await dispatcher.DispatchAsync(createRequest);
        Assert.True(createResponse.Success, string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

        Assert.Contains(groupName, await ListGroupsRawAsync());

        // NEGATIVE: a user-role request against the SAME override target_ref is rejected --
        // ae210's own dispatcher_mapping declares role="admin", and this is checked on the
        // target_ref path regardless of which layout's node authored the override (round 18/19
        // TARGET_REF_ROLE_UNAUTHORIZED).
        var userRoleSuffix = Guid.NewGuid().ToString("N")[..8];
        var userRoleGroupName = $"live-db-ae200-create-group-form-node-user-role-{userRoleSuffix}";
        var userRolePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = overrideTargetRef,
            groupName = userRoleGroupName,
            confirmed = true,
        });
        var userRoleRequest = new EndpointRequestDto(
            "create_group", "manifest", "enum_dictionary", "create_group",
            IdOrHubId: null, Payload: userRolePayload, Context: null, TriggerKind: "client", Role: "user");
        var userRoleResponse = await dispatcher.DispatchAsync(userRoleRequest);
        Assert.False(userRoleResponse.Success);
        Assert.Contains(userRoleResponse.Errors, e => e.Code == "TARGET_REF_ROLE_UNAUTHORIZED");
        Assert.DoesNotContain(userRoleGroupName, await ListGroupsRawAsync());
    }

    /// <summary>
    /// Round 20 -- the SECOND operation wired into ae200's own single-surface layout, and the
    /// first that needs an EXISTING record's identity rather than fresh user input.
    /// enum_delete_group_confirm_button's (round 24: retargeted from enum_delete_group_button,
    /// which now only opens the confirm modal -- see below) dispatchPayloadFromByTrigger sources
    /// groupId from "node:enum_table.value.groupId" -- the tracked value of ae200's OWN enum_table
    /// node after a row select (frontend/runtime/runtimeComponentFactory.ts tableFactory's
    /// onRowClick now also supplies `value: row` so emitBoundEvent's existing, universal Lane 3
    /// node-value tracking fires for a table select the same way it already does for every
    /// input's change/input event; frontend/runtime/payloadFromResolver.ts's new
    /// node:&lt;id&gt;.value.&lt;path&gt; dotted-path extension then extracts .groupId from that
    /// tracked row object). This backend test cannot exercise that frontend resolution step
    /// (EndpointRequestDto's payload is already resolved by the time it reaches ManifestDispatcher)
    /// -- it proves the seed itself carries the EXACT override the tensor patch declares, and that
    /// dispatching THROUGH that override target_ref genuinely authorizes and executes real
    /// enum_dictionary:delete_group under ae230's OWN dispatcher_mapping/capability_requirement,
    /// exactly as DispatchAsync_AdminEnumManagementManifest_CreateGroupFormNode_... proves for
    /// create_group. The frontend-side node:&lt;id&gt;.value.&lt;path&gt; resolution itself is
    /// proven separately in frontend/tests/payloadFromResolver.test.ts and
    /// frontend/tests/projectionShellAdminRuntimeSelectedRowDeleteCapture.test.ts. Round 24 also
    /// proves the confirm-dialog gating itself: enum_delete_group_button (the visible trigger)
    /// carries NO dispatch override at all (openModal only, targeting
    /// enum_delete_group_confirm_modal) -- the write moved entirely onto
    /// enum_delete_group_confirm_button, inside the modal.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_DeleteGroupConfirmModalNode_SurfacesDispatchOverride_AndExecutesViaAe230Authority()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        // Step 1: render ae200's own layout and confirm enum_delete_group_confirm_button (the LEAF
        // Action node inside the Modal -- not enum_delete_group_button, which only opens the
        // dialog) surfaces the EXACT override the seed declares, sourced from ae200's OWN
        // enum_table node's tracked selected-row value.
        var entryPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:projection_entry",
        });
        var entryRequest = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: entryPayload, Context: null, TriggerKind: "client", Role: "admin");
        var entryResponse = await dispatcher.DispatchAsync(entryRequest);
        Assert.True(entryResponse.Success, string.Join(";", entryResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(entryResponse.Emission!.LayoutNodes);
        var confirmButtonNode = Assert.Single(
            entryResponse.Emission!.LayoutNodes!, n => n.NodeId == "enum_delete_group_confirm_button");
        Assert.NotNull(confirmButtonNode.DispatchTargetRefByTrigger);
        var targetRefByTriggerText = confirmButtonNode.DispatchTargetRefByTrigger!.Value.GetRawText();
        Assert.Contains("manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group", targetRefByTriggerText);
        Assert.NotNull(confirmButtonNode.DispatchPayloadFromByTrigger);
        var payloadFromByTriggerText = confirmButtonNode.DispatchPayloadFromByTrigger!.Value.GetRawText();
        Assert.Contains("node:enum_table.value.groupId", payloadFromByTriggerText);
        Assert.Contains("literal:true", payloadFromByTriggerText);

        // preview-gap round: the visible Delete trigger now ALSO carries a non-mutating dryRun
        // preview dispatch to the SAME ae230 target (groupId re-resolved fresh from enum_table's
        // own tracked selected-row value, same source the Confirm button itself uses) -- the modal
        // only opens once that preview dispatch settles successfully.
        var deleteGroupButtonNode = Assert.Single(
            entryResponse.Emission!.LayoutNodes!, n => n.NodeId == "enum_delete_group_button");
        Assert.NotNull(deleteGroupButtonNode.DispatchTargetRefByTrigger);
        Assert.Contains(
            "manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group",
            deleteGroupButtonNode.DispatchTargetRefByTrigger!.Value.GetRawText());
        Assert.NotNull(deleteGroupButtonNode.DispatchPayloadFromByTrigger);
        var deletePreviewPayloadFromText = deleteGroupButtonNode.DispatchPayloadFromByTrigger!.Value.GetRawText();
        Assert.Contains("node:enum_table.value.groupId", deletePreviewPayloadFromText);
        Assert.Contains("\"dryRun\"", deletePreviewPayloadFromText);
        Assert.Contains("literal:true", deletePreviewPayloadFromText);
        Assert.DoesNotContain("confirmed", deletePreviewPayloadFromText);
        // Round 26: this is the leaf whose RuntimeInteractions (openModal) previously resolved to
        // NULL against a real Compose pipeline -- see round 26 todo.md entry for the full story.
        // Its owning tensor entry must now be scoped to its RESOLVED PARENT ("enum_dictionary_roster"),
        // not to a node keyed by its own name, or Compose's "{parentNodeId}::{row.Key}" lookup misses it.
        Assert.NotNull(deleteGroupButtonNode.RuntimeInteractions);
        Assert.Contains("openModal", deleteGroupButtonNode.RuntimeInteractions!.Value.GetRawText());

        // The Cancel button inside the modal is present and, like the Delete trigger, carries no
        // write authority -- only the Confirm button's node does.
        var cancelButtonNode = Assert.Single(
            entryResponse.Emission!.LayoutNodes!, n => n.NodeId == "enum_delete_group_cancel_button");
        Assert.Null(cancelButtonNode.DispatchTargetRefByTrigger);
        Assert.Null(cancelButtonNode.DispatchPayloadFromByTrigger);
        // Round 26: same class of bug as the open button above -- Cancel's owning parent is the
        // MODAL, not itself, so its closeModal entry must be scoped under the modal's tensor node.
        Assert.NotNull(cancelButtonNode.RuntimeInteractions);
        Assert.Contains("closeModal", cancelButtonNode.RuntimeInteractions!.Value.GetRawText());

        var confirmModalNode = Assert.Single(
            entryResponse.Emission!.LayoutNodes!, n => n.NodeId == "enum_delete_group_confirm_modal");
        Assert.Equal("disclosure/modal", confirmModalNode.ComponentKind);
        // The modal's own self-close (toggle trigger) also resolves correctly.
        Assert.NotNull(confirmModalNode.RuntimeInteractions);
        Assert.Contains("\"trigger\":\"toggle\"", confirmModalNode.RuntimeInteractions!.Value.GetRawText());

        // Round 26: the Confirm button's SECONDARY closeModal (applied only after backend dispatch
        // success, per round 25's deferLocalStateMutationToDispatchSuccess) is scoped under the
        // Confirm button's owning parent (the modal), same class of fix as open/cancel above.
        Assert.NotNull(confirmButtonNode.RuntimeInteractions);
        Assert.Contains("closeModal", confirmButtonNode.RuntimeInteractions!.Value.GetRawText());

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

        var overrideTargetRef = "manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group";

        // Step 2: real ae210 create_group dispatch (never raw SQL) seeds the row this test will
        // then delete through ae200's own override.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var groupName = $"live-db-ae200-delete-group-form-node-{suffix}";
        var createPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group",
            groupName,
            confirmed = true,
        });
        var createResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "create_group", "manifest", "enum_dictionary", "create_group",
            IdOrHubId: null, Payload: createPayload, Context: null, TriggerKind: "client", Role: "admin"));
        Assert.True(createResponse.Success, string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        string? createdGroupId;
        using (var createdDoc = System.Text.Json.JsonDocument.Parse(createResponse.Emission!.Data!.Value.GetRawText()))
            createdGroupId = createdDoc.RootElement.GetProperty("groupId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(createdGroupId));
        Assert.Contains(groupName, await ListGroupsRawAsync());

        // NEGATIVE (role): a user-role request against the SAME override target_ref is rejected --
        // ae230's own dispatcher_mapping declares role="admin" -- and the row survives untouched.
        var userRolePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = overrideTargetRef,
            groupId = createdGroupId,
            confirmed = true,
        });
        var userRoleResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "delete_group", "manifest", "enum_dictionary", "delete_group",
            IdOrHubId: null, Payload: userRolePayload, Context: null, TriggerKind: "client", Role: "user"));
        Assert.False(userRoleResponse.Success);
        Assert.Contains(userRoleResponse.Errors, e => e.Code == "TARGET_REF_ROLE_UNAUTHORIZED");
        Assert.Contains(groupName, await ListGroupsRawAsync());

        // NEGATIVE (missing group): a groupId that does not exist fails close as
        // ENUM_GROUP_NOT_FOUND (not a silent no-op success) through the SAME override target_ref --
        // DataEnumDictionaryDeleteGroupAsync's own before-lookup, exercised here via the override
        // path exactly like the direct-manifest delete_group path already proves elsewhere in this
        // file. (IsGroupReferencedInManifestsAsync checks for a manifest topology JSONB reference
        // to the groupId -- a real seeded group like demo_status is never referenced that way, so
        // it is not usable as an ENUM_GROUP_IN_USE fixture here; that fail-close path is already
        // proven directly against ae230 elsewhere in this test class's negative-case coverage.)
        var missingGroupPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = overrideTargetRef,
            groupId = Guid.NewGuid().ToString(),
            confirmed = true,
        });
        var missingGroupResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "delete_group", "manifest", "enum_dictionary", "delete_group",
            IdOrHubId: null, Payload: missingGroupPayload, Context: null, TriggerKind: "client", Role: "admin"));
        Assert.False(missingGroupResponse.Success);
        Assert.Contains(missingGroupResponse.Errors, e => e.Code == "ENUM_GROUP_NOT_FOUND");

        // Step 3: dispatch through the override target_ref with admin role and the real created
        // groupId -- proving the seeded override genuinely authorizes and executes under ae230's
        // OWN dispatcher_mapping/capability_requirement, and the deletion is visible through
        // ae200's own separate uniform list_groups read circuit afterward.
        var deletePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = overrideTargetRef,
            groupId = createdGroupId,
            confirmed = true,
        });
        var deleteResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "delete_group", "manifest", "enum_dictionary", "delete_group",
            IdOrHubId: null, Payload: deletePayload, Context: null, TriggerKind: "client", Role: "admin"));
        Assert.True(deleteResponse.Success, string.Join(";", deleteResponse.Errors.Select(e => e.Code + ":" + e.Message)));

        Assert.DoesNotContain(groupName, await ListGroupsRawAsync());
    }

    /// <summary>
    /// Round 26 -- shared structural proof, generalized across all 7 write actions, that each is
    /// now embedded in ae200's OWN single surface behind the SAME disclosure/modal explicit-confirm
    /// pattern create_group/delete_group already proved (round 24/25). Preview-gap round: the open
    /// button now ALSO carries a non-mutating dryRun preview dispatch to the SAME target manifest
    /// the Confirm button writes to (same field-source mapping, "dryRun" instead of "confirmed"),
    /// deferring its own openModal to that dispatch's own success -- proven here structurally for
    /// all 7 operations via the SAME dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger seed
    /// mechanism the Confirm button already used (no new wiring lane, actionType, or per-operation
    /// branch). The modal resolves componentKind=disclosure/modal AND its own required toggle
    /// self-close (modalFactory's requireBinding(spec,"toggle") -- round 26 found and fixed a
    /// translator bug where this was silently dropped from every Modal, including delete_group's
    /// already-shipped one, never caught because the existing DOM tests use hand-built mock
    /// layoutNodes that bypass the real Compose pipeline entirely), the confirm button inside the
    /// modal carries the exact node-local dispatch override (confirmed:true), and the cancel
    /// button carries NO dispatch, only closeModal. One shared theory, not 7 duplicated test
    /// bodies, per round 26's instruction to reuse a shared scenario contract.
    /// </summary>
    [Theory]
    [InlineData(
        "enum_create_group", "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group",
        new[] { "groupName" }, new[] { "node:enum_create_group_name_input.value" })]
    [InlineData(
        "enum_update_group", "manifest:00000000-0000-0000-0000-0000000ae220:enum_dictionary:update_group",
        new[] { "groupId", "groupName" },
        new[] { "node:enum_table.value.groupId", "node:enum_update_group_name_input.value" })]
    [InlineData(
        "enum_delete_group", "manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group",
        new[] { "groupId" }, new[] { "node:enum_table.value.groupId" })]
    [InlineData(
        "enum_create_item", "manifest:00000000-0000-0000-0000-0000000ae240:enum_dictionary:create_item",
        new[] { "name" }, new[] { "node:enum_create_item_name_input.value" })]
    [InlineData(
        "enum_update_item", "manifest:00000000-0000-0000-0000-0000000ae250:enum_dictionary:update_item",
        new[] { "indexNum", "name" },
        new[] { "node:enum_update_item_index_input.value", "node:enum_update_item_name_input.value" })]
    [InlineData(
        "enum_delete_item", "manifest:00000000-0000-0000-0000-0000000ae260:enum_dictionary:delete_item",
        new[] { "indexNum" }, new[] { "node:enum_delete_item_index_input.value" })]
    [InlineData(
        "enum_set_group_items", "manifest:00000000-0000-0000-0000-0000000ae270:enum_dictionary:set_group_items",
        new[] { "groupId", "enumIndexNums" },
        new[] { "node:enum_table.value.groupId", "node:enum_set_group_items_input.value" })]
    public async Task DispatchAsync_AdminEnumManagementManifest_EachWriteActionEmbeddedBehindOwnConfirmModal_StructurallyResolves(
        string prefix, string expectedTargetRef, string[] expectedFieldKeys, string[] expectedFieldSources)
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var entryPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:projection_entry",
        });
        var entryRequest = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: entryPayload, Context: null, TriggerKind: "client", Role: "admin");
        var entryResponse = await dispatcher.DispatchAsync(entryRequest);
        Assert.True(entryResponse.Success, string.Join(";", entryResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        var nodes = entryResponse.Emission!.LayoutNodes!;

        Assert.Equal(expectedFieldKeys.Length, expectedFieldSources.Length);

        var openButton = Assert.Single(nodes, n => n.NodeId == $"{prefix}_button");
        Assert.NotNull(openButton.DispatchTargetRefByTrigger);
        Assert.Contains(expectedTargetRef, openButton.DispatchTargetRefByTrigger!.Value.GetRawText());
        Assert.NotNull(openButton.DispatchPayloadFromByTrigger);
        var openClick = openButton.DispatchPayloadFromByTrigger!.Value.GetProperty("click");
        Assert.Equal("literal:true", openClick.GetProperty("dryRun").GetString());
        Assert.False(openClick.TryGetProperty("confirmed", out _));
        Assert.NotNull(openButton.RuntimeInteractions);
        Assert.Contains("openModal", openButton.RuntimeInteractions!.Value.GetRawText());

        var modal = Assert.Single(nodes, n => n.NodeId == $"{prefix}_confirm_modal");
        Assert.Equal("disclosure/modal", modal.ComponentKind);
        Assert.NotNull(modal.RuntimeInteractions);
        Assert.Contains("\"trigger\":\"toggle\"", modal.RuntimeInteractions!.Value.GetRawText());

        var confirmButton = Assert.Single(nodes, n => n.NodeId == $"{prefix}_confirm_button");
        Assert.NotNull(confirmButton.DispatchTargetRefByTrigger);
        Assert.Contains(expectedTargetRef, confirmButton.DispatchTargetRefByTrigger!.Value.GetRawText());
        Assert.NotNull(confirmButton.DispatchPayloadFromByTrigger);
        var confirmClick = confirmButton.DispatchPayloadFromByTrigger!.Value.GetProperty("click");
        Assert.Equal("literal:true", confirmClick.GetProperty("confirmed").GetString());
        Assert.False(confirmClick.TryGetProperty("dryRun", out _));
        Assert.NotNull(confirmButton.RuntimeInteractions);
        Assert.Contains("closeModal", confirmButton.RuntimeInteractions!.Value.GetRawText());

        // Full business-field key/source-set parity between the preview and confirm payloadFrom --
        // the SAME key set + node source on both buttons (only dryRun/confirmed differ), and NO
        // other keys beyond dryRun/confirmed/these -- mirroring
        // DispatchAsync_EnumDictionaryWriteManifest_ResolvesSinglePurposeLayout_WithDistinctPreviewAndConfirmPayloads's
        // rigor, now against THIS surface's own ae200-embedded preview/confirm button pair rather
        // than the dedicated per-action write manifest's own preview_button/confirm_button.
        for (var i = 0; i < expectedFieldKeys.Length; i++)
        {
            Assert.Equal(expectedFieldSources[i], openClick.GetProperty(expectedFieldKeys[i]).GetString());
            Assert.Equal(expectedFieldSources[i], confirmClick.GetProperty(expectedFieldKeys[i]).GetString());
        }
        var expectedOpenKeys = expectedFieldKeys.Append("dryRun").OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var actualOpenKeys = openClick.EnumerateObject().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedOpenKeys, actualOpenKeys);
        var expectedConfirmKeys = expectedFieldKeys.Append("confirmed").OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var actualConfirmKeys = confirmClick.EnumerateObject().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedConfirmKeys, actualConfirmKeys);

        var cancelButton = Assert.Single(nodes, n => n.NodeId == $"{prefix}_cancel_button");
        Assert.Null(cancelButton.DispatchTargetRefByTrigger);
        Assert.Null(cancelButton.DispatchPayloadFromByTrigger);
        Assert.NotNull(cancelButton.RuntimeInteractions);
        Assert.Contains("closeModal", cancelButton.RuntimeInteractions!.Value.GetRawText());
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

            // See the same-named assertion's comment above (Modal's null ComponentId with a
            // non-null ComponentKind is its expected resolved shape, not an unresolved gap).
            var unresolvedLeaves = emission.LayoutNodes!
                .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null && n.ComponentKind is null)
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
        // items-browse UX (admin-enum subBundle closure round): folded into enum_table's own
        // displayColumns, no new list_items action, no cross-manifest dispatch.
        Assert.Contains("itemsSummary", table.PropsJson);
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
        // items-browse UX: list_groups' own response now carries each group's items as a
        // flattened "index:name" summary (db/enum_seed.sql: demo_status's real member
        // demo_active, index_num=1) -- folded into the SAME action, never a separate
        // list_items dispatch.
        Assert.Contains("itemsSummary", dataText);
        Assert.Contains("1:demo_active", dataText);
    }

    /// <summary>
    /// generic list_groups search/filter (admin-enum subBundle closure round): a data-defined
    /// OPTIONAL payload field on this SAME existing list_groups read action -- no new action, no
    /// enum-specific runtime lane. Proves against REAL PostgreSQL (db/enum_seed.sql's own
    /// demo_status/user_status rows, not hand-built data): enum_search's own generated wiring
    /// carries the search payloadFrom override, a real dispatch with payload.search filters
    /// group_name case-insensitively, an empty/absent search returns the canonical full list, and
    /// a non-string search fails close rather than being silently ignored.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_ListGroupsSearch_FiltersRealRowsEmptyReturnsFullListNonStringFailsClose()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        // STEP 1: structural proof -- enum_search's own generated wiring (db/seed_empty.sql
        // ae206, produced by the translator from admin-enum-ae200.input.json/
        // admin-enum-ae200.topology-seed.input.json, adopted verbatim) carries the search
        // payloadFrom override on its own change trigger.
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
        var searchField = Assert.Single(structureNodes, n => n.NodeId == "enum_search");
        Assert.NotNull(searchField.DispatchPayloadFromByTrigger);
        var payloadFromText = searchField.DispatchPayloadFromByTrigger!.Value.GetRawText();
        Assert.Contains("\"change\"", payloadFromText);
        Assert.Contains("\"search\"", payloadFromText);
        Assert.Contains("\"node:enum_search.value\"", payloadFromText);

        // STEP 2: a real dispatch with payload.search="demo" must return ONLY demo_status, not
        // user_status -- both real db/enum_seed.sql rows, proving a genuine SQL-level filter, not
        // a coincidence of there being only one row.
        var searchDataPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
            search = "demo",
        });
        var searchDataRequest = new EndpointRequestDto(
            "list_groups", "manifest", "enum_dictionary", "list_groups",
            IdOrHubId: null, Payload: searchDataPayload, Context: null, TriggerKind: "client", Role: "admin");
        var searchDataResponse = await dispatcher.DispatchAsync(searchDataRequest);
        Assert.True(searchDataResponse.Success, string.Join(";", searchDataResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        // round 37: list_groups' response envelope is {groups, groupOptions} -- groups is the
        // search-narrowed roster, groupOptions is ALWAYS the full unfiltered list (closes the
        // options-self-shrinking gap where enum_group_filter's own select choices previously came
        // from this SAME narrowed array).
        using var searchDataDoc = System.Text.Json.JsonDocument.Parse(searchDataResponse.Emission!.Data!.Value.GetRawText());
        var searchGroupsText = searchDataDoc.RootElement.GetProperty("groups").GetRawText();
        Assert.Contains("demo_status", searchGroupsText);
        Assert.DoesNotContain("user_status", searchGroupsText);
        var searchGroupOptionsText = searchDataDoc.RootElement.GetProperty("groupOptions").GetRawText();
        Assert.Contains("demo_status", searchGroupOptionsText);
        Assert.Contains("user_status", searchGroupOptionsText);

        // STEP 3: an empty search returns the SAME canonical full list an absent payload does.
        var emptySearchDataPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
            search = "",
        });
        var emptySearchDataRequest = new EndpointRequestDto(
            "list_groups", "manifest", "enum_dictionary", "list_groups",
            IdOrHubId: null, Payload: emptySearchDataPayload, Context: null, TriggerKind: "client", Role: "admin");
        var emptySearchDataResponse = await dispatcher.DispatchAsync(emptySearchDataRequest);
        Assert.True(emptySearchDataResponse.Success, string.Join(";", emptySearchDataResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        var emptySearchDataText = emptySearchDataResponse.Emission!.Data!.Value.GetRawText();
        Assert.Contains("demo_status", emptySearchDataText);
        Assert.Contains("user_status", emptySearchDataText);

        // STEP 4: fail-close -- a non-string search must never be silently ignored/treated as "no
        // search"; the real dispatch chain surfaces an explicit validation error.
        var malformedDataPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
            search = 12345,
        });
        var malformedDataRequest = new EndpointRequestDto(
            "list_groups", "manifest", "enum_dictionary", "list_groups",
            IdOrHubId: null, Payload: malformedDataPayload, Context: null, TriggerKind: "client", Role: "admin");
        var malformedDataResponse = await dispatcher.DispatchAsync(malformedDataRequest);
        Assert.False(malformedDataResponse.Success);
        Assert.Contains(malformedDataResponse.Errors, e => e.Code == "ENUM_LIST_GROUPS_PAYLOAD_MALFORMED");

        // STEP 5 (round 37): fail-close -- a payload whose OWN shape is not a JSON object (here a
        // bare array) must never be silently treated as "no search"/"no filter" either, through
        // the real dispatch chain.
        var nonObjectPayload = System.Text.Json.JsonSerializer.SerializeToElement(new[] { "not", "an", "object" });
        var nonObjectRequest = new EndpointRequestDto(
            "list_groups", "manifest", "enum_dictionary", "list_groups",
            IdOrHubId: null, Payload: nonObjectPayload, Context: null, TriggerKind: "client", Role: "admin");
        var nonObjectResponse = await dispatcher.DispatchAsync(nonObjectRequest);
        Assert.False(nonObjectResponse.Success);
        Assert.Contains(nonObjectResponse.Errors, e => e.Code == "ENUM_LIST_GROUPS_PAYLOAD_NOT_OBJECT");
    }

    /// <summary>
    /// round 36 (admin-enum subBundle closure): the owning SSOT (docs/design/
    /// admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.enum.
    /// capability_requirements.search) declares FIVE search target fields (not group_name alone),
    /// and capability_requirements.filter declares its OWN three fields (enum.groups.group_name,
    /// enum.groups.index_num, enum.group_items.position -- round 37 replaced round 36's
    /// groupIdFilter, an exact match on enum.groups.group_id, a field the owning SSOT never
    /// actually declares as part of capability_requirements.filter). Proves against REAL
    /// PostgreSQL: (1) search matches via group_items.position specifically (an item whose own
    /// name/index_num do NOT contain the search term, only its position within a freshly created
    /// group does), (2) groupNameFilter/groupIndexNumFilter/itemPositionFilter each independently
    /// scope the roster to exactly this one group, excluding db/enum_seed.sql's own demo_status/
    /// user_status rows, while groupOptions stays the full unfiltered list throughout (proves the
    /// options-self-shrinking gap is closed against real Postgres, not only the in-memory
    /// fixture), and (3) a malformed (non-integer) groupIndexNumFilter fails close through the
    /// real dispatch chain.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_AdminEnumManagementManifest_ListGroupsSearchAndFilterFields_MatchPositionAndScopeToExactGroupRealRows()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        // "2" is the search token this test proves matches ONLY via group_items.position -- every
        // other numeric identity below (group's own indexNum, every item's own indexNum, the
        // group name's random suffix) is deliberately generated to avoid the digit '2' entirely,
        // so a match can only be explained by the target item's computed position (index 2, the
        // 3rd item passed to set_group_items).
        const string positionSearchTerm = "2";
        static int NextIndexNumAvoidingDigit(char digit)
        {
            int candidate;
            do { candidate = 970_000 + Random.Shared.Next(0, 9_000); }
            while (candidate.ToString().Contains(digit));
            return candidate;
        }
        var groupNameSuffix = Guid.NewGuid().ToString("N").Replace('2', 'x');
        var groupName = $"position-search-proof-{groupNameSuffix}"[..40];
        var groupIndexNum = NextIndexNumAvoidingDigit('2');
        var itemAIndex = NextIndexNumAvoidingDigit('2');
        var itemBIndex = NextIndexNumAvoidingDigit('2');
        var itemCIndex = NextIndexNumAvoidingDigit('2'); // lands at position 2 (3rd in enumIndexNums order)
        var itemDIndex = NextIndexNumAvoidingDigit('2');
        // round 37: db/enum_seed.sql's own demo_status carries positions 0-2 and user_status
        // carries positions 0-3 -- itemEIndex lands at position 4 (5th in enumIndexNums order), the
        // lowest position value NEITHER real seed group has, so itemPositionFilter=4 below can only
        // match this freshly created group.
        var itemEIndex = NextIndexNumAvoidingDigit('2');
        string? groupId = null;
        var createdItemIndexes = new List<int>();

        try
        {
            var createdGroup = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_group",
                new { groupName, indexNum = groupIndexNum, confirmed = true });
            Assert.True(createdGroup.Success, string.Join(";", createdGroup.Errors.Select(e => e.Code + ":" + e.Message)));
            using (var doc = System.Text.Json.JsonDocument.Parse(createdGroup.Emission!.Data!.Value.GetRawText()))
                groupId = doc.RootElement.GetProperty("groupId").GetString();

            foreach (var idx in new[] { itemAIndex, itemBIndex, itemCIndex, itemDIndex, itemEIndex })
            {
                var createdItem = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_item",
                    new { name = $"position-proof-item-{idx}", indexNum = idx, confirmed = true });
                Assert.True(createdItem.Success, string.Join(";", createdItem.Errors.Select(e => e.Code + ":" + e.Message)));
                createdItemIndexes.Add(idx);
            }

            var setItems = await DispatchViaOwnWriteManifestAsync(dispatcher, "set_group_items",
                new { groupId, enumIndexNums = $"{itemAIndex},{itemBIndex},{itemCIndex},{itemDIndex},{itemEIndex}", confirmed = true });
            Assert.True(setItems.Success, string.Join(";", setItems.Errors.Select(e => e.Code + ":" + e.Message)));

            // Sanity: none of this group's own identity fields (name/indexNum) or any of its
            // items' own indexNums contain the search token -- a match below can only be the
            // position dimension.
            Assert.DoesNotContain(positionSearchTerm, groupName);
            Assert.DoesNotContain(positionSearchTerm, groupIndexNum.ToString());
            Assert.DoesNotContain(positionSearchTerm, itemAIndex.ToString());
            Assert.DoesNotContain(positionSearchTerm, itemBIndex.ToString());
            Assert.DoesNotContain(positionSearchTerm, itemCIndex.ToString());

            var positionSearchPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
                search = positionSearchTerm,
            });
            var positionSearchResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "list_groups", "manifest", "enum_dictionary", "list_groups",
                IdOrHubId: null, Payload: positionSearchPayload, Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(positionSearchResponse.Success, string.Join(";", positionSearchResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            using (var positionSearchDoc = System.Text.Json.JsonDocument.Parse(positionSearchResponse.Emission!.Data!.Value.GetRawText()))
            {
                Assert.Contains(groupName, positionSearchDoc.RootElement.GetProperty("groups").GetRawText());
            }

            // round 37: the owning SSOT's own three declared filter target fields, each
            // independently exact-match scoping to this one group, excluding the real
            // db/enum_seed.sql demo_status/user_status rows -- while groupOptions stays the FULL
            // unfiltered list every time (proves the options-self-shrinking gap is closed against
            // real Postgres, not only the in-memory fixture).
            async Task AssertFilterScopesToThisGroupAndOptionsStayFullAsync(System.Text.Json.JsonElement payload)
            {
                var response = await dispatcher.DispatchAsync(new EndpointRequestDto(
                    "list_groups", "manifest", "enum_dictionary", "list_groups",
                    IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin"));
                Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
                using var doc = System.Text.Json.JsonDocument.Parse(response.Emission!.Data!.Value.GetRawText());
                var groupsText = doc.RootElement.GetProperty("groups").GetRawText();
                Assert.Contains(groupName, groupsText);
                Assert.DoesNotContain("demo_status", groupsText);
                Assert.DoesNotContain("user_status", groupsText);
                var groupOptionsText = doc.RootElement.GetProperty("groupOptions").GetRawText();
                Assert.Contains(groupName, groupOptionsText);
                Assert.Contains("demo_status", groupOptionsText);
                Assert.Contains("user_status", groupOptionsText);
            }

            await AssertFilterScopesToThisGroupAndOptionsStayFullAsync(System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
                groupNameFilter = groupName,
            }));
            await AssertFilterScopesToThisGroupAndOptionsStayFullAsync(System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
                groupIndexNumFilter = groupIndexNum,
            }));
            await AssertFilterScopesToThisGroupAndOptionsStayFullAsync(System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
                itemPositionFilter = 4,
            }));

            // fail-close: a non-integer groupIndexNumFilter through the real dispatch chain.
            var malformedFilterPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups",
                groupIndexNumFilter = "not-a-number",
            });
            var malformedFilterResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "list_groups", "manifest", "enum_dictionary", "list_groups",
                IdOrHubId: null, Payload: malformedFilterPayload, Context: null, TriggerKind: "client", Role: "admin"));
            Assert.False(malformedFilterResponse.Success);
            Assert.Contains(malformedFilterResponse.Errors, e => e.Code == "ENUM_LIST_GROUPS_PAYLOAD_MALFORMED");
        }
        finally
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            if (groupId is not null)
            {
                await using var delGroupCmd = conn.CreateCommand();
                delGroupCmd.CommandText = "DELETE FROM enum.groups WHERE group_id = @id";
                delGroupCmd.Parameters.AddWithValue("id", Guid.Parse(groupId));
                await delGroupCmd.ExecuteNonQueryAsync();
            }
            foreach (var idx in createdItemIndexes)
            {
                await using var delItemCmd = conn.CreateCommand();
                delItemCmd.CommandText = "DELETE FROM enum.items WHERE index_num = @idx";
                delItemCmd.Parameters.AddWithValue("idx", idx);
                await delItemCmd.ExecuteNonQueryAsync();
            }
        }
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

        // Round 17 hardening (ManifestDispatcher now authorizes a target_ref's layer:action
        // against the resolved manifest's OWN dispatcher_mapping): create_group/delete_group must
        // dispatch through their own dedicated write manifests (ae210/ae230), not ae200 (which
        // only declares list_groups) -- same discipline DispatchViaOwnWriteManifestAsync below
        // already uses for the newer per-action-manifest tests in this file.
        const string createGroupManifestId = "00000000-0000-0000-0000-0000000ae210";
        const string deleteGroupManifestId = "00000000-0000-0000-0000-0000000ae230";
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
                target_ref = $"manifest:{createGroupManifestId}:enum_dictionary:create_group",
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
                target_ref = $"manifest:{createGroupManifestId}:enum_dictionary:create_group",
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
                target_ref = $"manifest:{createGroupManifestId}:enum_dictionary:create_group",
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
                target_ref = $"manifest:{deleteGroupManifestId}:enum_dictionary:delete_group",
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
                target_ref = $"manifest:{deleteGroupManifestId}:enum_dictionary:delete_group",
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
                target_ref = $"manifest:{deleteGroupManifestId}:enum_dictionary:delete_group",
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

        // Round 17 hardening: each write action must dispatch through its OWN dedicated manifest
        // (ManifestDispatcher now authorizes a target_ref's layer:action against that manifest's
        // own dispatcher_mapping) -- delegates to the same DispatchViaOwnWriteManifestAsync the
        // newer per-action-manifest tests in this file already use, instead of a bare
        // AdminEnumManagementManifestId (ae200) carrier that only declares list_groups.
        async Task<EndpointResponseDto> DispatchAsync(string action, object payload)
        {
            return await DispatchViaOwnWriteManifestAsync(dispatcher, action, payload);
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
        // get_group is ae280's own action (not ae200's) -- see round 17 hardening note above.
        var afterPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = "manifest:00000000-0000-0000-0000-0000000ae280:enum_dictionary:get_group",
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

    /// <summary>
    /// admin-enum subBundle closure round negative-boundary matrix gap-fill: the constraint-backed
    /// negative test above proves missing/duplicate-index/referenced-delete for create_group/
    /// create_item/delete_item/set_group_items, but update_group/update_item's OWN missing-ID and
    /// duplicate-index checks (AdminRuntimeMasterRoster.cs DataEnumDictionaryUpdateGroupAsync/
    /// DataEnumDictionaryUpdateItemAsync -- separate method bodies, not shared code with create_*)
    /// had never been independently live-DB-proven. Uses fresh, UNREFERENCED rows (never demo_status/
    /// demo_active) so this is a genuine "two ordinary rows, no group-membership entanglement"
    /// duplicate-index case, distinct from the referenced-row ENUM_ITEM_IN_USE case already proven
    /// above.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionaryUpdateGroupAndUpdateItem_OwnMissingIdAndDuplicateIndexNegativeCases_FailCloseAgainstRealPostgres()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        async Task<EndpointResponseDto> DispatchAsync(string action, object payload) =>
            await DispatchViaOwnWriteManifestAsync(dispatcher, action, payload);

        await using var cleanupConn = new NpgsqlConnection(cs);
        await cleanupConn.OpenAsync();
        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = cleanupConn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        Guid? groupAId = null, groupBId = null;
        int? itemAIndex = null, itemBIndex = null;
        try
        {
            var createGroupA = await DispatchAsync("create_group",
                new { groupName = $"neg-matrix-group-a-{suffix}", confirmed = true });
            Assert.True(createGroupA.Success, string.Join(";", createGroupA.Errors.Select(e => e.Code)));
            groupAId = createGroupA.Emission!.Data!.Value.GetProperty("groupId").GetGuid();
            var groupAIndex = createGroupA.Emission!.Data!.Value.GetProperty("indexNum").GetInt32();

            var createGroupB = await DispatchAsync("create_group",
                new { groupName = $"neg-matrix-group-b-{suffix}", confirmed = true });
            Assert.True(createGroupB.Success, string.Join(";", createGroupB.Errors.Select(e => e.Code)));
            groupBId = createGroupB.Emission!.Data!.Value.GetProperty("groupId").GetGuid();
            var groupBIndex = createGroupB.Emission!.Data!.Value.GetProperty("indexNum").GetInt32();

            var createItemA = await DispatchAsync("create_item",
                new { name = $"neg-matrix-item-a-{suffix}", confirmed = true });
            Assert.True(createItemA.Success, string.Join(";", createItemA.Errors.Select(e => e.Code)));
            itemAIndex = createItemA.Emission!.Data!.Value.GetProperty("indexNum").GetInt32();

            var createItemB = await DispatchAsync("create_item",
                new { name = $"neg-matrix-item-b-{suffix}", confirmed = true });
            Assert.True(createItemB.Success, string.Join(";", createItemB.Errors.Select(e => e.Code)));
            itemBIndex = createItemB.Emission!.Data!.Value.GetProperty("indexNum").GetInt32();

            // update_group: missing groupId (never created / already deleted) fails close, dryRun
            // and confirmed alike.
            var missingGroupDryRun = await DispatchAsync("update_group",
                new { groupId = Guid.NewGuid().ToString(), groupName = "x", dryRun = true });
            Assert.False(missingGroupDryRun.Success);
            Assert.Contains(missingGroupDryRun.Errors, e => e.Code == "ENUM_GROUP_NOT_FOUND");
            var missingGroupWrite = await DispatchAsync("update_group",
                new { groupId = Guid.NewGuid().ToString(), groupName = "x", confirmed = true });
            Assert.False(missingGroupWrite.Success);
            Assert.Contains(missingGroupWrite.Errors, e => e.Code == "ENUM_GROUP_NOT_FOUND");

            // update_group: changing group A's own indexNum to group B's (unreferenced, ordinary
            // rows -- no FK entanglement) fails close, dryRun and confirmed alike, group A untouched.
            var dupGroupIndexDryRun = await DispatchAsync("update_group",
                new { groupId = groupAId.Value.ToString(), indexNum = groupBIndex, dryRun = true });
            Assert.False(dupGroupIndexDryRun.Success);
            Assert.Contains(dupGroupIndexDryRun.Errors, e => e.Code == "ENUM_GROUP_INDEX_CONFLICT");
            var dupGroupIndexWrite = await DispatchAsync("update_group",
                new { groupId = groupAId.Value.ToString(), indexNum = groupBIndex, confirmed = true });
            Assert.False(dupGroupIndexWrite.Success);
            Assert.Contains(dupGroupIndexWrite.Errors, e => e.Code == "ENUM_GROUP_INDEX_CONFLICT");

            // update_item: missing indexNum (never created) fails close, dryRun and confirmed alike.
            var missingItemDryRun = await DispatchAsync("update_item",
                new { indexNum = 987_654_321, name = "x", dryRun = true });
            Assert.False(missingItemDryRun.Success);
            Assert.Contains(missingItemDryRun.Errors, e => e.Code == "ENUM_ITEM_NOT_FOUND");
            var missingItemWrite = await DispatchAsync("update_item",
                new { indexNum = 987_654_321, name = "x", confirmed = true });
            Assert.False(missingItemWrite.Success);
            Assert.Contains(missingItemWrite.Errors, e => e.Code == "ENUM_ITEM_NOT_FOUND");

            // update_item: changing item A's own indexNum to item B's (unreferenced, ordinary rows)
            // fails close, dryRun and confirmed alike, item A untouched.
            var dupItemIndexDryRun = await DispatchAsync("update_item",
                new { indexNum = itemAIndex.Value, newIndexNum = itemBIndex.Value, dryRun = true });
            Assert.False(dupItemIndexDryRun.Success);
            Assert.Contains(dupItemIndexDryRun.Errors, e => e.Code == "ENUM_ITEM_INDEX_CONFLICT");
            var dupItemIndexWrite = await DispatchAsync("update_item",
                new { indexNum = itemAIndex.Value, newIndexNum = itemBIndex.Value, confirmed = true });
            Assert.False(dupItemIndexWrite.Success);
            Assert.Contains(dupItemIndexWrite.Errors, e => e.Code == "ENUM_ITEM_INDEX_CONFLICT");

            // None of the above negative attempts altered group A's or item A's real rows.
            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.OpenAsync();
                await using var groupCmd = conn.CreateCommand();
                groupCmd.CommandText = "SELECT index_num FROM enum.groups WHERE group_id = @id";
                groupCmd.Parameters.AddWithValue("id", groupAId.Value);
                Assert.Equal(groupAIndex, (int)(await groupCmd.ExecuteScalarAsync())!);

                await using var itemCmd = conn.CreateCommand();
                itemCmd.CommandText = "SELECT index_num FROM enum.items WHERE index_num = @idx";
                itemCmd.Parameters.AddWithValue("idx", itemAIndex.Value);
                Assert.Equal(itemAIndex.Value, (int)(await itemCmd.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            if (groupAId is not null)
                await ExecAsync("DELETE FROM enum.groups WHERE group_id = @id", ("id", groupAId.Value));
            if (groupBId is not null)
                await ExecAsync("DELETE FROM enum.groups WHERE group_id = @id", ("id", groupBId.Value));
            if (itemAIndex is not null)
                await ExecAsync("DELETE FROM enum.items WHERE index_num = @idx", ("idx", itemAIndex.Value));
            if (itemBIndex is not null)
                await ExecAsync("DELETE FROM enum.items WHERE index_num = @idx", ("idx", itemBIndex.Value));
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

            // list_groups is ae200's own action, not create_group's own manifest (ae210) -- round
            // 17 hardening now authorizes a target_ref's layer:action against the resolved
            // manifest's own dispatcher_mapping, so this must resolve through ae200.
            var listVector = new EndpointRequestDto(
                "list_groups", "manifest", "enum_dictionary", "list_groups",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new { target_ref = $"manifest:{AdminEnumManagementManifestId}:enum_dictionary:list_groups" }),
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
    /// Live-DB proof for the ae220 pre-fill mechanism added in PR #600 review round 10-11
    /// (see db/seed_empty.sql's ae226 tensor comment): update_group's own canonical action,
    /// dispatched with dryRun=true and groupId only (groupName deliberately omitted), returns
    /// preview.groupName carrying the group's real CURRENT name (before-value fallback) rather
    /// than blank -- the exact response group_name_field's propBindings.value pre-fills from.
    /// This proves the backend half (the response shape group_name_field's propBindings
    /// depends on); the frontend half (propBindings resolving it into props.value, and
    /// ProjectionShell.tsx's seedTrackerFromPropBindingsValue making that value dispatch-ready
    /// without the user re-typing it) is proven separately by
    /// frontend/tests/propBindingResolver.test.ts and
    /// frontend/tests/liveNodeValueTracker.test.ts -- no single test spans both, so this is not
    /// described as end-to-end.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionaryUpdateGroupWriteManifest_DryRunWithGroupIdOnly_PreviewCarriesCurrentGroupName()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var groupName = $"prefill-proof-{Guid.NewGuid():N}"[..30];
        string? groupId = null;
        try
        {
            var created = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_group",
                new { groupName, confirmed = true });
            Assert.True(created.Success, string.Join(";", created.Errors.Select(e => e.Code + ":" + e.Message)));
            using (var doc = System.Text.Json.JsonDocument.Parse(created.Emission!.Data!.Value.GetRawText()))
                groupId = doc.RootElement.GetProperty("groupId").GetString();

            // load_button's own dispatchPayloadFromByTrigger shape: {groupId, dryRun} only --
            // groupName is NOT included, mirroring an untouched group_name_field.
            var loadResult = await DispatchViaOwnWriteManifestAsync(dispatcher, "update_group",
                new { groupId, dryRun = true });
            Assert.True(loadResult.Success, string.Join(";", loadResult.Errors.Select(e => e.Code + ":" + e.Message)));
            using var doc2 = System.Text.Json.JsonDocument.Parse(loadResult.Emission!.Data!.Value.GetRawText());
            Assert.Equal(
                groupName,
                doc2.RootElement.GetProperty("preview").GetProperty("groupName").GetString());
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
            // get_group is ae280's own action (not ae200's) -- round 17 hardening note above.
            var r = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "get_group", "manifest", "enum_dictionary", "get_group",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    target_ref = "manifest:00000000-0000-0000-0000-0000000ae280:enum_dictionary:get_group",
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

    /// <summary>
    /// Live-DB proof for the ae280 read-detail manifest (enum_dictionary:get_group, PR #600 review
    /// round 9 -- "existing contract, unconnected in production" gap, see db/seed_empty.sql's ae280
    /// block comment). Dispatches through ae280's OWN manifest identity
    /// (target_ref = "manifest:{ae280}:enum_dictionary:get_group"), the same discipline
    /// DispatchViaOwnWriteManifestAsync uses for the 7 write manifests -- proving manifest
    /// resolution for THIS specific manifest, not just the generic backend action (which
    /// AdminEnumHubRelationUiProjectionLiveDbTests.cs's read_circuit tests already cover for
    /// list_groups). Asserts the real EnumDictionaryGroupDetailDto shape (groupName/indexNum/items)
    /// reaches the caller, and that a nonexistent groupId fails close with ENUM_GROUP_NOT_FOUND
    /// through this same manifest identity (not just the bare dispatcher).
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnumDictionaryGetGroupReadDetailManifest_ResolvesAndReturnsGroupDetail()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        const string getGroupManifestId = "00000000-0000-0000-0000-0000000ae280";
        var groupName = $"get-group-manifest-proof-{Guid.NewGuid():N}"[..30];
        var itemName = $"get-group-manifest-item-{Guid.NewGuid():N}"[..30];
        var indexNum = 910_000 + Random.Shared.Next(0, 90_000);
        string? groupId = null;
        try
        {
            var createdGroup = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_group",
                new { groupName, confirmed = true });
            Assert.True(createdGroup.Success, string.Join(";", createdGroup.Errors.Select(e => e.Code + ":" + e.Message)));
            using (var doc = System.Text.Json.JsonDocument.Parse(createdGroup.Emission!.Data!.Value.GetRawText()))
                groupId = doc.RootElement.GetProperty("groupId").GetString();

            var createdItem = await DispatchViaOwnWriteManifestAsync(dispatcher, "create_item",
                new { name = itemName, indexNum, confirmed = true });
            Assert.True(createdItem.Success, string.Join(";", createdItem.Errors.Select(e => e.Code + ":" + e.Message)));

            var setItems = await DispatchViaOwnWriteManifestAsync(dispatcher, "set_group_items",
                new { groupId, enumIndexNums = indexNum.ToString(), confirmed = true });
            Assert.True(setItems.Success, string.Join(";", setItems.Errors.Select(e => e.Code + ":" + e.Message)));

            var getResult = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "get_group", "manifest", "enum_dictionary", "get_group",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    groupId,
                    target_ref = $"manifest:{getGroupManifestId}:enum_dictionary:get_group",
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(getResult.Success, string.Join(";", getResult.Errors.Select(e => e.Code + ":" + e.Message)));
            var detailJson = getResult.Emission!.Data!.Value.GetRawText();
            Assert.Contains(groupName, detailJson);
            Assert.Contains(itemName, detailJson);
            Assert.Contains(indexNum.ToString(), detailJson);

            var notFound = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "get_group", "manifest", "enum_dictionary", "get_group",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    groupId = Guid.NewGuid().ToString(),
                    target_ref = $"manifest:{getGroupManifestId}:enum_dictionary:get_group",
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.False(notFound.Success);
            Assert.Contains(notFound.Errors, e => e.Code == "ENUM_GROUP_NOT_FOUND");
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
            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM enum.items WHERE index_num = @i";
                cmd.Parameters.AddWithValue("i", indexNum);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
