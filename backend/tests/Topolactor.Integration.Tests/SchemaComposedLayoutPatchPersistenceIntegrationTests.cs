using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Round 40: proves the persistence-boundary hazard round 40's own acceptance of schema-composed
/// override-delta patches would otherwise newly open. Before round 40, ApplyConfirmedLayoutPatchAsync
/// unconditionally overwrote components_layout_design.layout_schema_json with the SAME
/// tensorPatchJson it also wrote to ui_topology_tensor.layout_patch_json — harmless for a
/// tensor-only layout (layout_schema_json there is never structurally authoritative), but for a
/// schema-composed layout (layout_schema_json.records[] IS the structural authority tree — see
/// LayoutSchemaTensorComposer) this write would destroy records[] the moment a schema-composed
/// override delta could ever pass ValidateLayoutPatchAsync at all, which round 40 is the first
/// change to make possible. These tests exercise the real ApplyConfirmedLayoutPatchAsync entry
/// point against a live database, proving records[] survives and the tensor's own override-delta
/// carrier is updated correctly.
/// </summary>
public class SchemaComposedLayoutPatchPersistenceIntegrationTests
{
    private static string? ConnectionStringOrSkip()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(cs)) return cs;
        if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
            throw new InvalidOperationException("TOPOLACTOR_TEST_DB_CONNECTION required.");
        return null;
    }

    private const string SchemaComposedRecordsJson = """
        {"records":[
          {"type":"topology_ui_seed_record","seedKey":"test.schema.composed.persistence","parentKey":null,
           "record":{"recordType":"topology_ui_field","key":"schema_search_field","label":"Test search",
             "sourceYamlRefs":["test-ssot.yaml#x"],"sourceReactPath":"$.root.children[0]","knownGapRefs":[],
             "control":"form_input/search_input"}}
        ]}
        """;

    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ApplyConfirmedLayoutPatchAsync_SchemaComposedLayout_PreservesRecordsAndAppliesTensorOverrideDelta()
    {
        var cs = ConnectionStringOrSkip();
        if (cs is null) return;

        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, cs);
        var layoutId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var routeKey = $"route-{Guid.NewGuid():N}";

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.components_layout_design(layout_id, layout_key, layout_kind, layout_schema_json) " +
                "VALUES (@id,@key,'schema_composed',@schema::jsonb)";
            cmd.Parameters.AddWithValue("id", layoutId);
            cmd.Parameters.AddWithValue("key", $"layout-{layoutId:N}");
            cmd.Parameters.AddWithValue("schema", SchemaComposedRecordsJson);
            await cmd.ExecuteNonQueryAsync();
        }

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

        // Override-delta patch: no componentKey (schema-composed authoring), positive-integer
        // debounceMs — exactly the shape round 40's persistence-boundary fix newly accepts.
        var overrideDeltaPatchJson = """
            {"nodes":[
              {"nodeId":"schema_search_field","nodeKind":"catalog_component","debounceMs":300,
               "dispatchTargetRefByTrigger":{"change":"manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups"},
               "dispatchPayloadFromByTrigger":{"change":{"search":"node:schema_search_field.value"}}}
            ]}
            """;

        var applyResult = await repo.ApplyConfirmedLayoutPatchAsync(
            packageId, layoutId, routeKey, overrideDeltaPatchJson, null, null);
        Assert.True(applyResult.Ok, applyResult.Message);
        Assert.True(applyResult.Valid, applyResult.Message);

        await using (var verifySchema = conn.CreateCommand())
        {
            verifySchema.CommandText =
                "SELECT layout_schema_json::text FROM topology.components_layout_design WHERE layout_id=@id";
            verifySchema.Parameters.AddWithValue("id", layoutId);
            var afterSchema = (string)(await verifySchema.ExecuteScalarAsync())!;
            // The structural authority tree must survive byte-for-byte content-wise (still carries
            // its own Field record) — proving the CASE guard preserved it rather than replacing it
            // with the tensor's flat override-delta nodes[] shape.
            Assert.Contains("schema_search_field", afterSchema);
            Assert.Contains("\"records\"", afterSchema);
            Assert.Contains("form_input/search_input", afterSchema);
        }

        await using (var verifyTensor = conn.CreateCommand())
        {
            verifyTensor.CommandText =
                "SELECT layout_patch_json::text FROM topology.ui_topology_tensor WHERE layout_id=@id AND route_key=@route";
            verifyTensor.Parameters.AddWithValue("id", layoutId);
            verifyTensor.Parameters.AddWithValue("route", routeKey);
            var afterTensor = (string)(await verifyTensor.ExecuteScalarAsync())!;
            // The tensor's own override-delta carrier DID get the new patch applied.
            Assert.Contains("schema_search_field", afterTensor);
            Assert.Contains("300", afterTensor);
        }
    }

    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ApplyConfirmedLayoutPatchAsync_TensorOnlyLayout_StillReplacesLayoutSchemaJson()
    {
        // Regression guard: a genuinely tensor-only layout (layout_schema_json has no records[] —
        // the default '{}'::jsonb shape) still gets layout_schema_json replaced with the applied
        // tensorPatchJson exactly as before round 40 — the CASE guard only preserves an EXISTING
        // non-empty records[] array, it does not change behavior for the tensor-only case.
        var cs = ConnectionStringOrSkip();
        if (cs is null) return;

        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, cs);
        var layoutId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var routeKey = $"route-{Guid.NewGuid():N}";

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.components_layout_design(layout_id, layout_key, layout_kind) VALUES (@id,@key,'ui_builder_canvas')";
            cmd.Parameters.AddWithValue("id", layoutId);
            cmd.Parameters.AddWithValue("key", $"layout-{layoutId:N}");
            await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO topology.ui_component_package(package_id, package_key, package_kind) VALUES (@id,@key,'pkg')";
            cmd.Parameters.AddWithValue("id", packageId);
            cmd.Parameters.AddWithValue("key", $"pkg-{packageId:N}");
            await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.ui_wiring_registry(wiring_id, wiring_key, wiring_kind, target_surface) VALUES (@id,@key,'read_only_no_actions','ui')";
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

        var tensorOnlyPatchJson = """
            {"nodes":[{"nodeId":"panel-1","nodeKind":"catalog_component","componentKey":"panel.alias"}]}
            """;

        var applyResult = await repo.ApplyConfirmedLayoutPatchAsync(
            packageId, layoutId, routeKey, tensorOnlyPatchJson, null, null);
        Assert.True(applyResult.Ok, applyResult.Message);
        Assert.True(applyResult.Valid, applyResult.Message);

        await using var verifySchema = conn.CreateCommand();
        verifySchema.CommandText =
            "SELECT layout_schema_json::text FROM topology.components_layout_design WHERE layout_id=@id";
        verifySchema.Parameters.AddWithValue("id", layoutId);
        var afterSchema = (string)(await verifySchema.ExecuteScalarAsync())!;
        Assert.Contains("panel-1", afterSchema);
        Assert.Contains("panel.alias", afterSchema);
    }
}
