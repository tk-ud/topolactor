using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Real-DB E2E proof for the layout projection continuity:
///   ONE tensor row with layout_patch_json.nodes[] (full canvas JSON)
///   → NpgsqlTopologyRepository.LoadLayoutNodesAsync (parsed from JSON, sorted by orderIndex)
///   → StructureMapResolver builds LayoutNodes with componentId from nodes[].componentId
///   → EmissionBuilder preserves LayoutNodes order and all fields through to Emission
///
/// The JSON nodes array has node-slot-a (orderIndex=1) before node-slot-b (orderIndex=0)
/// to prove sorting by orderIndex — not JSON array order.
///
/// componentId in LayoutNodes comes from nodes[].componentId — NOT positional from
/// structure_maps.component_ids. This is the canonical post-canvas-apply data flow.
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset → explicit local skip.
/// Set TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require live DB in CI.
/// </summary>
public class LayoutProjectionContinuityLiveDbEndToEndTests
{
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task LayoutPatchJsonNodes_ParsedAndSortedByOrderIndex_ReachEmission()
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
        var smId      = Guid.NewGuid();
        var comp1Id   = Guid.NewGuid();  // slot_b (orderIndex=0)
        var comp2Id   = Guid.NewGuid();  // slot_a (orderIndex=1)
        var smPkgId   = Guid.NewGuid();
        var smSchId   = Guid.NewGuid();

        var repo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);

        // layout_patch_json.nodes[] in REVERSE orderIndex order to prove sort-by-orderIndex.
        // node-slot-a (orderIndex=1) appears first in JSON; node-slot-b (orderIndex=0) appears second.
        // After parse-and-sort, node-slot-b must be at index 0 and node-slot-a at index 1.
        var patchJson = JsonSerializer.Serialize(new
        {
            grid = new { cols = 12 },
            nodes = new object[]
            {
                new
                {
                    nodeId = "node-slot-a",
                    nodeKind = "catalog_component",
                    componentKey = "card",
                    componentId = comp2Id.ToString(),
                    slotKey = "slot_a",
                    orderIndex = 1,
                    x = 50.0,
                    y = 200.0,
                    width = 200.0,
                    height = 100.0,
                    layoutClassRefs = new[] { "card-ref" },
                },
                new
                {
                    nodeId = "node-slot-b",
                    nodeKind = "catalog_component",
                    componentKey = "card",
                    componentId = comp1Id.ToString(),
                    slotKey = "slot_b",
                    orderIndex = 0,
                    x = 10.0,
                    y = 20.0,
                    width = 300.0,
                    height = 150.0,
                },
            },
        });

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO topology.components_layout_design " +
                    "  (layout_id, layout_key, layout_kind) " +
                    "VALUES (@id, @key, 'grid')";
                cmd.Parameters.AddWithValue("id",  layoutId);
                cmd.Parameters.AddWithValue("key", $"layout-e2e-{suffix}");
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO topology.ui_component_package " +
                    "  (package_id, package_key, package_kind) " +
                    "VALUES (@id, @key, 'pkg')";
                cmd.Parameters.AddWithValue("id",  packageId);
                cmd.Parameters.AddWithValue("key", $"pkg-e2e-{suffix}");
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO topology.ui_wiring_registry " +
                    "  (wiring_id, wiring_key, wiring_kind, target_surface) " +
                    "VALUES (@id, @key, 'evt', 'ui')";
                cmd.Parameters.AddWithValue("id",  wiringId);
                cmd.Parameters.AddWithValue("key", $"wiring-e2e-{suffix}");
                await cmd.ExecuteNonQueryAsync();
            }

            // ONE tensor row with full layout_patch_json nodes[].
            // This is the canonical post-layout_patch:apply shape.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO topology.ui_topology_tensor " +
                    "  (tensor_id, route_key, package_id, layout_id, wiring_id, layout_patch_json) " +
                    "VALUES (@tid, @route, @pkg, @layout, @wiring, @patch::jsonb)";
                cmd.Parameters.AddWithValue("tid",    Guid.NewGuid());
                cmd.Parameters.AddWithValue("route",  $"test-e2e-{suffix}");
                cmd.Parameters.AddWithValue("pkg",    packageId);
                cmd.Parameters.AddWithValue("layout", layoutId);
                cmd.Parameters.AddWithValue("wiring", wiringId);
                cmd.Parameters.AddWithValue("patch",  patchJson);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO topology.structure_maps
  (structure_map_id, name, attractor_key, package_id, schema_id,
   component_ids, layout_id, active)
VALUES
  (@smid, @name, @key, @pkg, @sch,
   ARRAY[@comp1, @comp2]::uuid[], @layout, true)";
                cmd.Parameters.AddWithValue("smid",   smId);
                cmd.Parameters.AddWithValue("name",   $"test-e2e-{suffix}");
                cmd.Parameters.AddWithValue("key",    $"test:e2e:{suffix}");
                cmd.Parameters.AddWithValue("pkg",    smPkgId);
                cmd.Parameters.AddWithValue("sch",    smSchId);
                cmd.Parameters.AddWithValue("comp1",  comp1Id);
                cmd.Parameters.AddWithValue("comp2",  comp2Id);
                cmd.Parameters.AddWithValue("layout", layoutId);
                await cmd.ExecuteNonQueryAsync();
            }

            // ── 1. LoadLayoutNodesAsync: parses nodes[], sorts by orderIndex ──
            var nodes = await repo.LoadLayoutNodesAsync(layoutId);

            Assert.Equal(2, nodes.Count);

            // node-slot-b is second in JSON but has orderIndex=0 → must be first after sort
            Assert.Equal("node-slot-b",    nodes[0].NodeId);
            Assert.Equal("catalog_component", nodes[0].NodeKind);
            Assert.Equal("card",           nodes[0].ComponentKey);
            Assert.Equal(comp1Id.ToString(), nodes[0].ComponentId);
            Assert.Equal("slot_b",         nodes[0].SlotKey);
            Assert.Equal(0,                nodes[0].OrderIndex);
            Assert.Equal(10.0,             nodes[0].X);
            Assert.Equal(20.0,             nodes[0].Y);
            Assert.Equal(300.0,            nodes[0].Width);
            Assert.Equal(150.0,            nodes[0].Height);
            Assert.Null(nodes[0].LayoutClassRefs);

            // node-slot-a is first in JSON but has orderIndex=1 → must be second after sort
            Assert.Equal("node-slot-a",    nodes[1].NodeId);
            Assert.Equal("catalog_component", nodes[1].NodeKind);
            Assert.Equal(comp2Id.ToString(), nodes[1].ComponentId);
            Assert.Equal("slot_a",         nodes[1].SlotKey);
            Assert.Equal(1,                nodes[1].OrderIndex);
            Assert.Equal(50.0,             nodes[1].X);
            Assert.Equal(200.0,            nodes[1].Y);
            Assert.Equal(200.0,            nodes[1].Width);
            Assert.Equal(100.0,            nodes[1].Height);
            Assert.NotNull(nodes[1].LayoutClassRefs);
            Assert.Single(nodes[1].LayoutClassRefs!);
            Assert.Equal("card-ref",       nodes[1].LayoutClassRefs![0]);

            // ── 2. StructureMapResolver: LayoutNodes with componentId from nodes[].componentId ──
            var resolver  = new StructureMapResolver(repo);
            var attractor = new AttractorResult(
                AttractorKey:   $"test:e2e:{suffix}",
                StructureMapId: smId.ToString(),
                PackageId:      smPkgId,
                SchemaId:       smSchId);

            var shape = await resolver.Resolve(attractor);

            Assert.Equal(layoutId.ToString(), shape.LayoutId);
            Assert.Null(shape.Errors);
            Assert.NotNull(shape.LayoutNodes);
            Assert.Equal(2, shape.LayoutNodes!.Count);

            // componentId comes from nodes[].componentId — NOT positional from component_ids
            Assert.Equal("node-slot-b",      shape.LayoutNodes[0].NodeId);
            Assert.Equal("slot_b",           shape.LayoutNodes[0].SlotKey);
            Assert.Equal(0,                  shape.LayoutNodes[0].OrderIndex);
            Assert.Equal(comp1Id.ToString(), shape.LayoutNodes[0].ComponentId);
            Assert.Equal(10.0,               shape.LayoutNodes[0].X);
            Assert.Equal(20.0,               shape.LayoutNodes[0].Y);
            Assert.Equal(300.0,              shape.LayoutNodes[0].Width);
            Assert.Equal(150.0,              shape.LayoutNodes[0].Height);

            Assert.Equal("node-slot-a",      shape.LayoutNodes[1].NodeId);
            Assert.Equal("slot_a",           shape.LayoutNodes[1].SlotKey);
            Assert.Equal(1,                  shape.LayoutNodes[1].OrderIndex);
            Assert.Equal(comp2Id.ToString(), shape.LayoutNodes[1].ComponentId);
            Assert.Equal(50.0,               shape.LayoutNodes[1].X);
            Assert.Equal(200.0,              shape.LayoutNodes[1].Y);
            Assert.NotNull(shape.LayoutNodes[1].LayoutClassRefs);
            Assert.Equal("card-ref",         shape.LayoutNodes[1].LayoutClassRefs![0]);

            // ── 3. EmissionBuilder: all fields survive to Emission ──
            var builder  = new EmissionBuilder();
            var emission = builder.Build(shape);

            Assert.Equal(layoutId.ToString(), emission.LayoutId);
            Assert.NotNull(emission.LayoutNodes);
            Assert.Equal(2, emission.LayoutNodes!.Count);

            Assert.Equal("node-slot-b",      emission.LayoutNodes[0].NodeId);
            Assert.Equal("slot_b",           emission.LayoutNodes[0].SlotKey);
            Assert.Equal(0,                  emission.LayoutNodes[0].OrderIndex);
            Assert.Equal(comp1Id.ToString(), emission.LayoutNodes[0].ComponentId);
            Assert.Equal(10.0,               emission.LayoutNodes[0].X);
            Assert.Equal(20.0,               emission.LayoutNodes[0].Y);
            Assert.Equal(300.0,              emission.LayoutNodes[0].Width);
            Assert.Equal(150.0,              emission.LayoutNodes[0].Height);

            Assert.Equal("node-slot-a",      emission.LayoutNodes[1].NodeId);
            Assert.Equal("slot_a",           emission.LayoutNodes[1].SlotKey);
            Assert.Equal(1,                  emission.LayoutNodes[1].OrderIndex);
            Assert.Equal(comp2Id.ToString(), emission.LayoutNodes[1].ComponentId);
            Assert.NotNull(emission.LayoutNodes[1].LayoutClassRefs);
            Assert.Equal("card-ref",         emission.LayoutNodes[1].LayoutClassRefs![0]);
        }
        finally
        {
            try
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
DELETE FROM topology.structure_maps WHERE structure_map_id = @smid;
DELETE FROM topology.ui_topology_tensor WHERE layout_id = @layout;
DELETE FROM topology.components_layout_design WHERE layout_id = @layout;
DELETE FROM topology.ui_component_package WHERE package_id = @pkg;
DELETE FROM topology.ui_wiring_registry WHERE wiring_id = @wiring;";
                cmd.Parameters.AddWithValue("smid",   smId);
                cmd.Parameters.AddWithValue("layout", layoutId);
                cmd.Parameters.AddWithValue("pkg",    packageId);
                cmd.Parameters.AddWithValue("wiring", wiringId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Cleanup failure does not replace the test result.
            }
        }
    }
}
