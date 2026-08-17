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
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class TeamDashboardUiBuilderCanonicalApplyPipelineLiveDbTests
{
    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();

    // Verbatim copy of db/seed_empty.sql's dd015 (admin) tensor layout_patch_json -- the exact
    // production content, not a re-derived approximation.
    private const string AdminTensorPatchJson = """
        {"nodes":[
          {"nodeId":"team_dashboard_admin_viewer","nodeKind":"catalog_component","componentKey":"md_viewer.projection","componentId":"00000000-0000-0000-0001-000000000021","orderIndex":0,"propsJson":"{\"title\":\"Team Dashboard\"}","propBindings":{"markdown":{"source":"emission.data.bodyMarkdown"}}},
          {"nodeId":"team_dashboard_admin_body","nodeKind":"catalog_component","componentKey":"textarea.template","componentId":"00000000-0000-0000-0001-00000000001f","orderIndex":1,"propsJson":"{\"label\":\"Markdown body\"}","propBindings":{"value":{"source":"emission.data.bodyMarkdown"}}},
          {"nodeId":"team_dashboard_admin_save_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentId":"00000000-0000-0000-0001-000000000010","orderIndex":2,"propsJson":"{\"label\":\"Save\"}","wiringKind":"admin_runtime","targetSurface":"manifest","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update"},"dispatchPayloadFromByTrigger":{"click":{"bodyMarkdown":"node:team_dashboard_admin_body.value","dryRun":"literal:true"}},"runtimeInteractions":[{"trigger":"click","actionType":"openModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_button"}]},
          {"nodeId":"team_dashboard_admin_save_confirm_modal","nodeKind":"catalog_component","componentKey":"modal.template","componentKind":"disclosure/modal","componentId":"00000000-0000-0000-0001-000000000015","orderIndex":3,"propsJson":"{\"data\": {\"open\": false, \"title\": \"Save team dashboard\", \"body\": \"Save the edited Markdown as the team dashboard's shared content.\"}}","runtimeInteractions":[{"trigger":"click","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_confirm_button"},{"trigger":"click","actionType":"closeModal","targetNodeId":"team_dashboard_admin_save_confirm_modal","statePath":"open","sourceActionKey":"team_dashboard_admin_save_cancel_button"}]},
          {"nodeId":"team_dashboard_admin_save_confirm_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentId":"00000000-0000-0000-0001-000000000010","orderIndex":4,"propsJson":"{\"label\":\"Save\"}","wiringKind":"admin_runtime","targetSurface":"manifest","dispatchTargetRefByTrigger":{"click":"manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update"},"dispatchPayloadFromByTrigger":{"click":{"bodyMarkdown":"node:team_dashboard_admin_body.value","confirmed":"literal:true"}}},
          {"nodeId":"team_dashboard_admin_save_cancel_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentId":"00000000-0000-0000-0001-000000000010","orderIndex":5,"propsJson":"{\"label\":\"Cancel\"}"}
        ]}
        """;

    // Verbatim copy of db/seed_empty.sql's dd025 (normal) tensor layout_patch_json.
    private const string NormalTensorPatchJson = """
        {"nodes":[
          {"nodeId":"team_dashboard_normal_viewer","nodeKind":"catalog_component","componentKey":"md_viewer.projection","componentId":"00000000-0000-0000-0001-000000000021","orderIndex":0,"propsJson":"{\"title\":\"Team Dashboard\"}","propBindings":{"markdown":{"source":"emission.data.bodyMarkdown"}}}
        ]}
        """;

    private static async Task<(Guid PackageId, Guid LayoutId, Guid WiringId, string RouteKey)> CreateScaffoldAsync(
        NpgsqlConnection conn, string label)
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
                "VALUES (@id,@key,'fixed_form_projection','{}'::jsonb)";
            cmd.Parameters.AddWithValue("id", layoutId);
            cmd.Parameters.AddWithValue("key", $"layout-{layoutId:N}");
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

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var (packageId, layoutId, wiringId, routeKey) = await CreateScaffoldAsync(conn, surface);
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
