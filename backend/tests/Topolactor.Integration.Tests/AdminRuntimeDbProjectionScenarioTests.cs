using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Representative scenario fixtures for admin runtime DB→projection continuity.
///
/// Policy (pipeline-continuity-ssot.yaml backend_db_sse_scenario_harness_policy):
///   unique_side_effect_runtime_requires_individual_scenario
///   runtime_without_sse_contract_may_use_refetch_or_projection_response_assertion
///
/// Route: backend runtime → DB state changed → projection response assertion
/// (full dispatch path covered by DefaultEntitySearchIntegrationTests;
///  admin runtime unit stubs covered by AdminRuntimeLayoutPatchTests)
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset → explicit local skip.
/// Set TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require live DB in CI.
/// </summary>
public class AdminRuntimeDbProjectionScenarioTests
{
    /// <summary>
    /// Representative scenario: layout_patch:apply action class.
    /// Unique DB side effect: ui_topology_tensor.layout_patch_json updated.
    /// Projection assertion: LoadLayoutNodesAsync returns updated nodes (DB state readable).
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task LayoutPatchApply_DbRoundTrip_NodesVisibleInProjection()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return;
        }

        var suffix    = Guid.NewGuid().ToString("N")[..12];
        var layoutId  = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var wiringId  = Guid.NewGuid();
        var routeKey  = $"/test/apply-scenario-{suffix}";
        var comp1Id   = Guid.NewGuid().ToString();
        var comp2Id   = Guid.NewGuid().ToString();

        var uiRepo   = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, cs);
        var topoRepo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);

        // Tensor payload with two catalog_component nodes (no _draftOnly, no runtimeInteractions).
        var tensorPatchJson = JsonSerializer.Serialize(new
        {
            nodes = new object[]
            {
                new
                {
                    nodeId = "apply-node-a",
                    nodeKind = "catalog_component",
                    componentKey = "card.primitive",
                    componentId = comp1Id,
                    slotKey = "main",
                    orderIndex = 0,
                    parentNodeId = (string?)null,
                },
                new
                {
                    nodeId = "apply-node-b",
                    nodeKind = "catalog_component",
                    componentKey = "card.primitive",
                    componentId = comp2Id,
                    slotKey = "main",
                    orderIndex = 1,
                    parentNodeId = (string?)null,
                },
            }
        });

        try
        {
            // ── 1. Insert prerequisites ──────────────────────────────────────────────
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind) " +
                    "VALUES (@id, @key, 'grid')";
                cmd.Parameters.AddWithValue("id",  layoutId);
                cmd.Parameters.AddWithValue("key", $"apply-scenario-{suffix}");
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO topology.ui_component_package (package_id, package_key, package_kind) " +
                    "VALUES (@id, @key, 'pkg')";
                cmd.Parameters.AddWithValue("id",  packageId);
                cmd.Parameters.AddWithValue("key", $"apply-pkg-{suffix}");
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface) " +
                    "VALUES (@id, @key, 'evt', 'ui')";
                cmd.Parameters.AddWithValue("id",  wiringId);
                cmd.Parameters.AddWithValue("key", $"apply-wiring-{suffix}");
                await cmd.ExecuteNonQueryAsync();
            }

            // Tensor row with empty layout_patch_json — apply will update it.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO topology.ui_topology_tensor " +
                    "  (tensor_id, route_key, package_id, layout_id, wiring_id, layout_patch_json) " +
                    "VALUES (@tid, @route, @pkg, @layout, @wiring, '{}'::jsonb)";
                cmd.Parameters.AddWithValue("tid",    Guid.NewGuid());
                cmd.Parameters.AddWithValue("route",  routeKey);
                cmd.Parameters.AddWithValue("pkg",    packageId);
                cmd.Parameters.AddWithValue("layout", layoutId);
                cmd.Parameters.AddWithValue("wiring", wiringId);
                await cmd.ExecuteNonQueryAsync();
            }

            // ── 2. Apply (backend runtime → DB state changed) ────────────────────────
            var applyResult = await uiRepo.ApplyConfirmedLayoutPatchAsync(
                packageId, layoutId, routeKey, tensorPatchJson, null, null);

            Assert.True(applyResult.Ok,    $"apply must succeed; got: {applyResult.Message}");
            Assert.True(applyResult.Valid, "apply must be valid");

            // ── 3. Projection assertion: LoadLayoutNodesAsync reads updated tensor row ─
            var nodes = await topoRepo.LoadLayoutNodesAsync(layoutId);

            Assert.Equal(2, nodes.Count);

            // orderIndex=0 must be first after sort
            Assert.Equal("apply-node-a", nodes[0].NodeId);
            Assert.Equal("catalog_component", nodes[0].NodeKind);
            Assert.Equal(0, nodes[0].OrderIndex);

            // orderIndex=1 must be second
            Assert.Equal("apply-node-b", nodes[1].NodeId);
            Assert.Equal("catalog_component", nodes[1].NodeKind);
            Assert.Equal(1, nodes[1].OrderIndex);
        }
        finally
        {
            try
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
DELETE FROM topology.ui_topology_tensor WHERE layout_id = @layout;
DELETE FROM topology.components_layout_design WHERE layout_id = @layout;
DELETE FROM topology.ui_component_package WHERE package_id = @pkg;
DELETE FROM topology.ui_wiring_registry WHERE wiring_id = @wiring;";
                cmd.Parameters.AddWithValue("layout", layoutId);
                cmd.Parameters.AddWithValue("pkg",    packageId);
                cmd.Parameters.AddWithValue("wiring", wiringId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Cleanup failure does not affect test result.
            }
        }
    }

    /// <summary>
    /// Representative scenario: component_style_design:upsert action class.
    /// Unique DB side effect: components_style_design upserted, components_package_design merged.
    /// Projection assertion: ListComponentStyleDesignsAsync returns design entry visible for package.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ComponentStyleDesignUpsert_DbRoundTrip_DesignVisibleInProjection()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return;
        }

        var suffix     = Guid.NewGuid().ToString("N")[..12];
        var packageId  = Guid.NewGuid();
        var designName = $"design-scenario-{suffix}";
        var layoutNodeId = $"node-{suffix}";

        var uiRepo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, cs);

        var designJson = JsonSerializer.Serialize(new
        {
            componentId    = (string?)null,
            layoutNodeId,
            classname      = "",
            tailwind       = "",
            cssTokenRefs   = new[] { "color.action.primary.background" },
            responsiveTokenRefs = new Dictionary<string, string[]>(),
            inlineText     = "テストテキスト",
            linkHref       = "",
            linkTarget     = "",
            reactionIntent = "",
        });

        try
        {
            // ── 1. Insert prerequisite ───────────────────────────────────────────────
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO topology.ui_component_package (package_id, package_key, package_kind) " +
                    "VALUES (@id, @key, 'pkg')";
                cmd.Parameters.AddWithValue("id",  packageId);
                cmd.Parameters.AddWithValue("key", $"design-pkg-{suffix}");
                await cmd.ExecuteNonQueryAsync();
            }

            // ── 2. Upsert design (backend runtime → DB state changed) ────────────────
            var (designId, error) = await uiRepo.UpsertComponentStyleDesignForPackageAsync(
                packageId, componentId: null, layoutNodeId, designName, designJson);

            Assert.Null(error);
            Assert.NotEqual(Guid.Empty, designId);

            // ── 3. Projection assertion: ListComponentStyleDesignsAsync returns entry ─
            var designs = await uiRepo.ListComponentStyleDesignsAsync(packageId);

            var entry = designs.FirstOrDefault(d => d.Name == designName);
            Assert.NotNull(entry);
            Assert.Equal(designName, entry.Name);
            Assert.Equal(layoutNodeId, entry.LayoutNodeId);
            Assert.NotNull(entry.CssTokenRefs);
            Assert.Contains("color.action.primary.background", entry.CssTokenRefs!);
            Assert.Equal("テストテキスト", entry.InlineText);
        }
        finally
        {
            try
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
DELETE FROM topology.components_package_design WHERE package_id = @pkg;
DELETE FROM topology.components_style_design WHERE name = @name;
DELETE FROM topology.ui_component_package WHERE package_id = @pkg;";
                cmd.Parameters.AddWithValue("pkg",  packageId);
                cmd.Parameters.AddWithValue("name", designName);
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Cleanup failure does not affect test result.
            }
        }
    }
}
