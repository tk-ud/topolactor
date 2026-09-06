using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// UIBuilder-lineage closure round (2026-08-17, second pass): proves team-dashboard's exact
/// production node/wiring shape is not merely translator-CLI-shaped, but is ITSELF acceptable to,
/// and reproducible through, the REAL, live, DB-writing production authoring pipeline —
/// layout_patch:preview -&gt; layout_patch:validate -&gt; layout_patch:apply
/// (backend/runtime/AdminRuntime.cs DataLayoutPatchPreviewAsync/DataLayoutPatchValidateAsync/
/// DataLayoutPatchApplyAsync -&gt; NpgsqlUiTopologyRepository.PreviewLayoutPatchAsync/
/// ValidateLayoutPatchAsync/ApplyConfirmedLayoutPatchAsync) — the SAME dispatch actions
/// frontend/islands/UiBuilderAdmin.tsx's real "Apply" button calls in production, reachable
/// through a real seeded dispatcher_mapping (db/seed_empty.sql layer=layout_patch
/// action=preview/validate/apply, role=admin, target=admin) via the full ManifestDispatcher axes
/// resolution — never AdminRuntime.ExecuteDataAsync called directly, and never a raw SQL UPDATE
/// standing in for "apply."
///
/// Distinct from db/seed_empty.sql's own actual bootstrap content: db/init.sql applies *.sql files
/// directly against a fresh, empty database, before any backend process exists to serve an admin_
/// runtime dispatch request — so a live apply CANNOT be fresh-bootstrap's own persistence
/// mechanism for ANY manifest in this system (this is a structural, repo-wide property, not a
/// team-dashboard-specific shortfall: every existing production surface -- ae200, manifest 092 --
/// has the identical bootstrap-vs-live-apply split). What this test closes is the narrower, real
/// gap: whether team-dashboard's own node/wiring content was ever verified against the actual
/// production apply pipeline's own validation/persistence gates, rather than merely asserted
/// "translator-shaped." It now is -- against a fresh, disposable route_key/package/layout/wiring
/// scaffold (never touching the real dd0xx production rows), the SAME exact layout_patch_json
/// content db/seed_empty.sql's dd015 (admin) and dd025 (normal) tensor rows carry is proven to
/// preview/validate/apply cleanly through the real pipeline, and the resulting real DB rows are
/// asserted to carry that content byte-for-byte.
///
/// UPDATED (team-dashboard-physical-layout-adoption round): dd013/dd023 are now physically
/// adopted (Owner "Judgment B" -- layoutAdoptionCandidates is the PRIMARY structural authority;
/// see docs/design/react-schema-topology-seed-translator-ssot.yaml
/// storage_adoption_contract.structural_authority_precedence_contract), so dd015/dd025 are no
/// longer self-sufficient tensor-only patches -- they are now the DERIVED runtime carrier this
/// same contract requires, meaningful only paired with the SAME schema content dd013/dd023
/// physically carry. This test's scaffold now seeds a matching layout_schema_json.records[] (the
/// SAME clean generator output physically adopted into dd013/dd023) alongside the tensor patch,
/// so "preview/validate/apply cleanly through the real pipeline" is proven for the ACTUAL shape
/// these rows carry today, not the pre-adoption tensor-only shape.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class TeamDashboardUiBuilderCanonicalApplyPipelineLiveDbTests
{
    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();

    // Verbatim copy of db/seed_empty.sql's dd013 (admin) layout_schema_json.records[] -- the
    // SAME clean react_schema_topology_seed_translator.py generation output
    // (team-dashboard-admin.topology-seed.input.json adoptionCandidates.layoutAdoptionCandidates)
    // physically adopted as the PRIMARY structural authority. Category > Section > Field(viewer)/
    // Field(body)/Action(save_button)/Modal(confirm_modal) > Action(confirm_button)/
    // Action(cancel_button) -- zero Form anywhere, a Section-owned dryRun-preview Action
    // (SECTION_OWNABLE_ACTION_LANES/section_owned_dryrun_preview_pairing) and Modal-owned
    // Confirm/Cancel (VALID_ACTION_OWNER_NODE_KINDS), the SAME already-legal authoring shape as
    // authored in the canonical react_schema DSL -- never a synthesized shape for this test.
    private const string AdminLayoutSchemaJson = """
        {"records":[{"type":"topology_ui_seed_record","seedKey":"team_dashboard.admin.projection","parentKey":"team_dashboard_admin_projection","record":{"recordType":"topology_ui_category","key":"team_dashboard","label":"チームダッシュボード","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.team_dashboard"],"sourceReactPath":"$.root.children[0]","knownGapRefs":[],"categoryKey":"team_dashboard","sectionKeys":["team_dashboard_admin_editor"]}},{"type":"topology_ui_seed_record","seedKey":"team_dashboard.admin.projection","parentKey":"team_dashboard","record":{"recordType":"topology_ui_section","key":"team_dashboard_admin_editor","label":"チームダッシュボード本文","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.team_dashboard.seed_contract"],"sourceReactPath":"$.root.children[0].children[0]","knownGapRefs":[],"sectionKey":"team_dashboard_admin_editor","sectionKind":"team_dashboard_admin_edit_projection","childKeys":["team_dashboard_admin_viewer","team_dashboard_admin_body","team_dashboard_admin_save_button","team_dashboard_admin_save_confirm_modal"]}},{"type":"topology_ui_seed_record","seedKey":"team_dashboard.admin.projection","parentKey":"team_dashboard_admin_editor","record":{"recordType":"topology_ui_field","key":"team_dashboard_admin_viewer","label":"プレビュー","sourceYamlRefs":["ui-builder-preset-ecosystem-ssot.yaml#physical_details_inline_editor_md_generator_preset.layout_tree.tab3_markdown"],"sourceReactPath":"$.root.children[0].children[0].children[0]","knownGapRefs":[],"fieldKey":"team_dashboard_admin_viewer","control":"data_display/md_viewer","required":false,"validationRefs":[],"valueFrom":"emission.data.bodyMarkdown","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"team_dashboard.admin.projection","parentKey":"team_dashboard_admin_editor","record":{"recordType":"topology_ui_field","key":"team_dashboard_admin_body","label":"Markdown本文","sourceYamlRefs":["ui-builder-preset-ecosystem-ssot.yaml#physical_details_inline_editor_md_generator_preset.layout_tree.tab3_markdown"],"sourceReactPath":"$.root.children[0].children[0].children[1]","knownGapRefs":[],"fieldKey":"team_dashboard_admin_body","control":"form_input/textarea_template","required":false,"validationRefs":[],"valueFrom":"emission.data.bodyMarkdown","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}},{"type":"topology_ui_seed_record","seedKey":"team_dashboard.admin.projection","parentKey":"team_dashboard_admin_editor","record":{"recordType":"topology_ui_action","key":"team_dashboard_admin_save_button","label":"保存","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.team_dashboard.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[2]","knownGapRefs":[],"authorityMarker":"preview_only","actionKey":"team_dashboard_admin_save_button","actionRef":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update","authority":"preview_only","payloadFrom":{"bodyMarkdown":"node:team_dashboard_admin_body.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update","payloadFrom":{"bodyMarkdown":"node:team_dashboard_admin_body.value","dryRun":"literal:true"},"sourceActionKey":"team_dashboard_admin_save_button"}}},{"type":"topology_ui_seed_record","seedKey":"team_dashboard.admin.projection","parentKey":"team_dashboard_admin_editor","record":{"recordType":"topology_ui_modal","key":"team_dashboard_admin_save_confirm_modal","label":"保存確認","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.team_dashboard.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[3]","knownGapRefs":[],"modalKey":"team_dashboard_admin_save_confirm_modal","componentKind":"disclosure/modal","title":"保存確認","body":"編集したMarkdownをチームダッシュボードの共有本文として保存します。","runtimeInteractions":[{"trigger":"toggle","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_confirm_modal"}],"childKeys":["team_dashboard_admin_save_confirm_button","team_dashboard_admin_save_cancel_button"]}},{"type":"topology_ui_seed_record","seedKey":"team_dashboard.admin.projection","parentKey":"team_dashboard_admin_save_confirm_modal","record":{"recordType":"topology_ui_action","key":"team_dashboard_admin_save_confirm_button","label":"保存","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.team_dashboard.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[3].children[0]","knownGapRefs":[],"authorityMarker":"draft_apply_not_execution_authority","actionKey":"team_dashboard_admin_save_confirm_button","actionRef":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update","eventBinding":{"trigger":"click","wiringLane":"admin_runtime_dispatch_override_wiring","targetRef":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update","authority":"draft_apply_not_execution_authority","payloadFrom":{"bodyMarkdown":"node:team_dashboard_admin_body.value","confirmed":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_confirm_button"}],"adminRuntimeDispatchOverride":{"trigger":"click","targetRef":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update","payloadFrom":{"bodyMarkdown":"node:team_dashboard_admin_body.value","confirmed":"literal:true"},"sourceActionKey":"team_dashboard_admin_save_confirm_button"}}},{"type":"topology_ui_seed_record","seedKey":"team_dashboard.admin.projection","parentKey":"team_dashboard_admin_save_confirm_modal","record":{"recordType":"topology_ui_action","key":"team_dashboard_admin_save_cancel_button","label":"キャンセル","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.admin.surfaces.team_dashboard.seed_contract.mutation_confirmation_contract"],"sourceReactPath":"$.root.children[0].children[0].children[3].children[1]","knownGapRefs":[],"authorityMarker":"draft_or_projection_only","actionKey":"team_dashboard_admin_save_cancel_button","actionRef":"ui-local:team_dashboard_admin_save_confirm_modal.close","eventBinding":{"trigger":"click","wiringLane":"disclosure_state_wiring","targetRef":"ui-local:team_dashboard_admin_save_confirm_modal.open","authority":"draft_or_projection_only","disclosureActionType":"closeModal","disclosureTargetNodeId":"team_dashboard_admin_save_confirm_modal","disclosureStatePath":"open"},"runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_cancel_button"}]}}]}
        """;

    // Verbatim copy of db/seed_empty.sql's dd023 (normal) layout_schema_json.records[] -- same
    // adoption, read-only axis: Category > Section > Field(viewer) only, zero Form/Action/Modal.
    private const string NormalLayoutSchemaJson = """
        {"records":[{"type":"topology_ui_seed_record","seedKey":"team_dashboard.normal.projection","parentKey":"team_dashboard_normal_projection","record":{"recordType":"topology_ui_category","key":"team_dashboard_normal","label":"チームダッシュボード","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.normal.surfaces.dashboard.team_dashboard_canonical_shared_contract"],"sourceReactPath":"$.root.children[0]","knownGapRefs":[],"categoryKey":"team_dashboard_normal","sectionKeys":["team_dashboard_normal_viewer_section"]}},{"type":"topology_ui_seed_record","seedKey":"team_dashboard.normal.projection","parentKey":"team_dashboard_normal","record":{"recordType":"topology_ui_section","key":"team_dashboard_normal_viewer_section","label":"チームダッシュボード本文","sourceYamlRefs":["admin-normal-surface-projection-seed-ssot.yaml#surface_axes.normal.surfaces.dashboard.team_dashboard_canonical_shared_contract.seed_contract"],"sourceReactPath":"$.root.children[0].children[0]","knownGapRefs":[],"sectionKey":"team_dashboard_normal_viewer_section","sectionKind":"team_dashboard_normal_readonly_projection","childKeys":["team_dashboard_normal_viewer"]}},{"type":"topology_ui_seed_record","seedKey":"team_dashboard.normal.projection","parentKey":"team_dashboard_normal_viewer_section","record":{"recordType":"topology_ui_field","key":"team_dashboard_normal_viewer","label":"プレビュー","sourceYamlRefs":["ui-builder-preset-ecosystem-ssot.yaml#physical_details_inline_editor_md_generator_preset.layout_tree.tab3_markdown"],"sourceReactPath":"$.root.children[0].children[0].children[0]","knownGapRefs":[],"fieldKey":"team_dashboard_normal_viewer","control":"data_display/md_viewer","required":false,"validationRefs":[],"valueFrom":"emission.data.bodyMarkdown","optionsSource":"","optionsLabelPath":"","optionsValuePath":""}}]}
        """;

    // Verbatim copy of db/seed_empty.sql's dd015 (admin) tensor layout_patch_json -- the exact
    // production content, not a re-derived approximation. UPDATED (team-dashboard-physical-
    // layout-adoption round): reshaped from the earlier tensor-only per-leaf shape into the
    // DERIVED runtime carrier structural_authority_precedence_contract requires now that
    // AdminLayoutSchemaJson above is the PRIMARY structural authority -- componentKey/
    // componentKind/label are no longer carried here at all (LayoutSchemaTensorComposer.Compose
    // resolves componentId/componentKind/Label from AdminLayoutSchemaJson's own records
    // directly); team_dashboard_admin_save_button's own runtimeInteraction is attributed to the
    // owning SECTION's tensor node (team_dashboard_admin_editor, per
    // section_owned_dryrun_preview_pairing -- this Action is Section-owned via the existing
    // admin_runtime_dispatch_override_wiring lane, an authoring legality this contract does not
    // change); team_dashboard_admin_save_confirm_button's/_cancel_button's own runtimeInteractions
    // are attributed to the owning MODAL's tensor node -- the SAME resolved-authored-parentKey
    // addressing admin.enum.management.projection's own already-proven tensor row uses (see
    // docs/design/react-schema-topology-seed-translator-ssot.yaml
    // storage_adoption_contract.structural_authority_precedence_contract.
    // interaction_ownership_and_addressing_contract). Each button's own adminRuntimeDispatchOverride
    // (dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger) stays on ITS OWN individual node
    // (NodeLocalData direct-NodeId match, a separate mechanism from the sourceActionKey/owning-
    // parent one, action-authority-vs-effect-data separation unchanged).
    private const string AdminTensorPatchJson = """
        {"nodes":[
          {"nodeId":"team_dashboard_admin_viewer","nodeKind":"catalog_component","runtimeInteractions":[],"propsJson":"{\"data\": {\"label\": \"プレビュー\"}}","propBindings":{"markdown":{"source":"emission.data.bodyMarkdown"}}},
          {"nodeId":"team_dashboard_admin_body","nodeKind":"catalog_component","runtimeInteractions":[],"propsJson":"{\"data\": {\"label\": \"Markdown本文\"}}","propBindings":{"value":{"source":"emission.data.bodyMarkdown"}}},
          {"nodeId":"team_dashboard_admin_save_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update"},"dispatchPayloadFromByTrigger":{"click":{"bodyMarkdown":"node:team_dashboard_admin_body.value","dryRun":"literal:true"}}},
          {"nodeId":"team_dashboard_admin_editor","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_button"},{"trigger":"toggle","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_confirm_modal"}]},
          {"nodeId":"team_dashboard_admin_save_confirm_modal","nodeKind":"catalog_component","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_confirm_button"},{"trigger":"click","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_cancel_button"}],"propsJson":"{\"data\": {\"open\": false, \"title\": \"保存確認\", \"body\": \"編集したMarkdownをチームダッシュボードの共有本文として保存します。\"}}"},
          {"nodeId":"team_dashboard_admin_save_confirm_button","nodeKind":"catalog_component","runtimeInteractions":[],"dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update"},"dispatchPayloadFromByTrigger":{"click":{"bodyMarkdown":"node:team_dashboard_admin_body.value","confirmed":"literal:true"}}}
        ]}
        """;

    // Verbatim copy of db/seed_empty.sql's dd025 (normal) tensor layout_patch_json -- same round,
    // same reasoning: componentKey removed (resolved from NormalLayoutSchemaJson's own Field
    // record instead), propsJson.data.label unchanged (data_display/md_viewer's own production
    // default props never consult the schema record's label -- see runtime-orchestration-ssot.yaml
    // field_control_resolution / this file's own frontend counterpart
    // teamDashboardProductionProjectionBoundary.test.ts for the full reasoning).
    private const string NormalTensorPatchJson = """
        {"nodes":[
          {"nodeId":"team_dashboard_normal_viewer","nodeKind":"catalog_component","runtimeInteractions":[],"propsJson":"{\"data\": {\"label\": \"プレビュー\"}}","propBindings":{"markdown":{"source":"emission.data.bodyMarkdown"}}}
        ]}
        """;

    private static async Task<(Guid PackageId, Guid LayoutId, Guid WiringId, string RouteKey)> CreateScaffoldAsync(
        NpgsqlConnection conn, string label, string layoutSchemaJson)
    {
        var packageId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var routeKey = $"test-uibuilder-apply-{label}-{Guid.NewGuid():N}#default";

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.ui_component_package(package_id, package_key, package_kind) VALUES (@id,@key,'pkg')";
            cmd.Parameters.AddWithValue("id", packageId);
            cmd.Parameters.AddWithValue("key", $"pkg-{packageId:N}");
            await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.components_layout_design(layout_id, layout_key, layout_kind, layout_schema_json) " +
                "VALUES (@id,@key,'fixed_form_projection',@schema::jsonb)";
            cmd.Parameters.AddWithValue("id", layoutId);
            cmd.Parameters.AddWithValue("key", $"layout-{layoutId:N}");
            cmd.Parameters.AddWithValue("schema", layoutSchemaJson);
            await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.ui_wiring_registry(wiring_id, wiring_key, wiring_kind, target_surface) " +
                "VALUES (@id,@key,'admin_runtime','manifest')";
            cmd.Parameters.AddWithValue("id", wiringId);
            cmd.Parameters.AddWithValue("key", $"wiring-{wiringId:N}");
            await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.ui_topology_tensor(route_key, package_id, layout_id, wiring_id, layout_patch_json) " +
                "VALUES (@route,@pkg,@layout,@wiring,'{\"nodes\":[]}'::jsonb)";
            cmd.Parameters.AddWithValue("route", routeKey);
            cmd.Parameters.AddWithValue("pkg", packageId);
            cmd.Parameters.AddWithValue("layout", layoutId);
            cmd.Parameters.AddWithValue("wiring", wiringId);
            await cmd.ExecuteNonQueryAsync();
        }

        return (packageId, layoutId, wiringId, routeKey);
    }

    private static async Task CleanupScaffoldAsync(
        NpgsqlConnection conn, Guid packageId, Guid layoutId, Guid wiringId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "DELETE FROM topology.ui_topology_tensor WHERE package_id=@pkg; " +
            "DELETE FROM topology.ui_wiring_registry WHERE wiring_id=@wiring; " +
            "DELETE FROM topology.components_layout_design WHERE layout_id=@layout; " +
            "DELETE FROM topology.ui_component_package WHERE package_id=@pkg;";
        cmd.Parameters.AddWithValue("pkg", packageId);
        cmd.Parameters.AddWithValue("layout", layoutId);
        cmd.Parameters.AddWithValue("wiring", wiringId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RunPreviewValidateApplyAsync(
        ManifestDispatcher dispatcher, Guid packageId, Guid layoutId, string routeKey, string tensorPatchJson)
    {
        var context = new Dictionary<string, string> { [DispatchAuthContext.AuthenticatedRolesKey] = "admin" };
        foreach (var action in new[] { "preview", "validate", "apply" })
        {
            var request = new EndpointRequestDto(
                "UiBuilderCanonicalApplyScenario", "admin", "layout_patch", action,
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    packageId = packageId.ToString(),
                    layoutId = layoutId.ToString(),
                    routeKey,
                    tensorPatchJson,
                }),
                Context: context, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);
            Assert.True(response.Success, $"{action} failed: " + string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        }
    }

    /// <summary>
    /// Order-independent (object properties), escaping-independent (raw string content only)
    /// structural JSON subset check: every property/value declared in `expected` must be present
    /// with an equal value in `actual`. `actual` MAY carry additional object properties `expected`
    /// does not declare -- this is deliberate, not laxness: ApplyConfirmedLayoutPatchAsync's real
    /// pipeline legitimately enriches applied runtimeInteractions with a system-assigned
    /// `runtimeInteractionId` (NpgsqlUiTopologyRepositoryRuntimeInteractionIdentityTests.cs's own
    /// AssignRuntimeInteractionIds mechanism, generic to every consumer of this pipeline, not
    /// team-dashboard-specific) that hand-authored bootstrap SQL never carries, since bootstrap
    /// never runs the live enrichment step (see this file's own class-level doc comment on why
    /// fresh-bootstrap SQL and live apply can never be the same mechanism). A strict two-way
    /// equality would incorrectly fail on this legitimate, pipeline-added metadata.
    /// </summary>
    private static bool JsonElementIsSubsetOf(System.Text.Json.JsonElement expected, System.Text.Json.JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind) return false;
        switch (expected.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                var actualProps = actual.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                return expected.EnumerateObject().All(p =>
                    actualProps.TryGetValue(p.Name, out var av) && JsonElementIsSubsetOf(p.Value, av));
            case System.Text.Json.JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();
                return expectedItems.Count == actualItems.Count &&
                    expectedItems.Zip(actualItems, JsonElementIsSubsetOf).All(eq => eq);
            case System.Text.Json.JsonValueKind.String:
                return expected.GetString() == actual.GetString();
            case System.Text.Json.JsonValueKind.Number:
                return expected.GetRawText() == actual.GetRawText();
            default:
                return expected.GetRawText() == actual.GetRawText();
        }
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("normal")]
    public async Task LayoutPatchPreviewValidateApply_TeamDashboardTensorContent_SucceedsThroughRealProductionPipeline(
        string surface)
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var tensorPatchJson = surface == "admin" ? AdminTensorPatchJson : NormalTensorPatchJson;
        var layoutSchemaJson = surface == "admin" ? AdminLayoutSchemaJson : NormalLayoutSchemaJson;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var (packageId, layoutId, wiringId, routeKey) = await CreateScaffoldAsync(conn, surface, layoutSchemaJson);
        try
        {
            // The SAME dispatch actions frontend/islands/UiBuilderAdmin.tsx's real Apply button
            // calls (layout_patch:preview -> layout_patch:validate -> layout_patch:apply), reached
            // through the full ManifestDispatcher axes resolution (db/seed_empty.sql's generic
            // admin:layout_patch:preview/validate/apply dispatcher_mapping), never AdminRuntime
            // called directly and never a raw SQL UPDATE standing in for "apply."
            await RunPreviewValidateApplyAsync(dispatcher, packageId, layoutId, routeKey, tensorPatchJson);

            await using var verifyTensor = conn.CreateCommand();
            verifyTensor.CommandText =
                "SELECT layout_patch_json::text FROM topology.ui_topology_tensor WHERE layout_id=@id AND route_key=@route";
            verifyTensor.Parameters.AddWithValue("id", layoutId);
            verifyTensor.Parameters.AddWithValue("route", routeKey);
            var persisted = (string)(await verifyTensor.ExecuteScalarAsync())!;

            // Real production content survived the real pipeline with full structural (not
            // merely substring) fidelity -- the exact node/wiring shape db/seed_empty.sql ships is
            // not merely "translator-shaped" but is itself accepted and persisted by the real,
            // live, tested apply mechanism. A semantic (order-independent, escaping-independent)
            // JSON comparison is used rather than raw string equality -- ApplyConfirmedLayoutPatchAsync
            // round-trips the patch through its own C# object model, which re-serializes object
            // property order and quote-escaping style (e.g. `\"` vs `"`) without changing any
            // value; a byte-for-byte string comparison would fail on this cosmetic difference alone.
            using var expectedDoc = System.Text.Json.JsonDocument.Parse(tensorPatchJson);
            using var actualDoc = System.Text.Json.JsonDocument.Parse(persisted);
            Assert.True(
                JsonElementIsSubsetOf(expectedDoc.RootElement, actualDoc.RootElement),
                $"Expected (subset):\n{tensorPatchJson}\n\nActual:\n{persisted}");
        }
        finally
        {
            await CleanupScaffoldAsync(conn, packageId, layoutId, wiringId);
        }
    }
}
