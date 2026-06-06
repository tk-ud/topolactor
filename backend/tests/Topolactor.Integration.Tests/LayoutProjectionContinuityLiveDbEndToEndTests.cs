using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Real-DB E2E proof for the layout application projection continuity:
///   structure_maps.layout_id + ui_topology_tensor rows
///   → NpgsqlTopologyRepository.LoadLayoutNodesAsync (ORDER BY order_index)
///   → StructureMapResolver builds LayoutNodes with positional componentId assignment
///   → EmissionBuilder preserves LayoutNodes order through to Emission
///
/// Insertion order is reversed relative to order_index (slot_a inserted before slot_b
/// but slot_b has order_index=0) to prove ORDER BY order_index — not insertion order.
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset → explicit local skip.
/// Set TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require live DB in CI.
/// </summary>
public class LayoutProjectionContinuityLiveDbEndToEndTests
{
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task RealTensorRows_LayoutNodesOrderedByOrderIndex_ReachEmission()
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
        var layoutId   = Guid.NewGuid();
        var packageId  = Guid.NewGuid();
        var wiringId   = Guid.NewGuid();
        var smId       = Guid.NewGuid();
        var comp1Id    = Guid.NewGuid();
        var comp2Id    = Guid.NewGuid();
        // Deterministic seed IDs present on any bootstrapped DB (seed_empty.sql)
        var seedPkgId  = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var seedSchId  = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var repo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);

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

            // Insert two tensor rows in REVERSE order_index insertion order.
            // slot_a is inserted first but has order_index=1; slot_b has order_index=0.
            // This proves LoadLayoutNodesAsync uses ORDER BY order_index, not insertion order.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO topology.ui_topology_tensor
  (tensor_id, route_key, package_id, layout_id, wiring_id, slot_key, order_index)
VALUES
  (@tid1, @route, @pkg, @layout, @wiring, 'slot_a', 1),
  (@tid2, @route, @pkg, @layout, @wiring, 'slot_b', 0)";
                cmd.Parameters.AddWithValue("tid1",   Guid.NewGuid());
                cmd.Parameters.AddWithValue("tid2",   Guid.NewGuid());
                cmd.Parameters.AddWithValue("route",  $"test-e2e-{suffix}");
                cmd.Parameters.AddWithValue("pkg",    packageId);
                cmd.Parameters.AddWithValue("layout", layoutId);
                cmd.Parameters.AddWithValue("wiring", wiringId);
                await cmd.ExecuteNonQueryAsync();
            }

            // structure_map references layout_id and uses seed package/schema IDs.
            // component_ids=[comp1Id, comp2Id] — positional assignment maps
            //   tensor[0] (slot_b, order=0) → comp1Id
            //   tensor[1] (slot_a, order=1) → comp2Id
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO topology.structure_maps
  (structure_map_id, attractor_key, package_id, schema_id,
   component_ids, layout_id, active)
VALUES
  (@smid, @key, @pkg, @sch,
   ARRAY[@comp1, @comp2]::uuid[], @layout, true)";
                cmd.Parameters.AddWithValue("smid",   smId);
                cmd.Parameters.AddWithValue("key",    $"test:e2e:{suffix}");
                cmd.Parameters.AddWithValue("pkg",    seedPkgId);
                cmd.Parameters.AddWithValue("sch",    seedSchId);
                cmd.Parameters.AddWithValue("comp1",  comp1Id);
                cmd.Parameters.AddWithValue("comp2",  comp2Id);
                cmd.Parameters.AddWithValue("layout", layoutId);
                await cmd.ExecuteNonQueryAsync();
            }

            // ── 1. LoadLayoutNodesAsync: verify ORDER BY order_index, not insertion order ──
            var nodes = await repo.LoadLayoutNodesAsync(layoutId);

            Assert.Equal(2, nodes.Count);
            // slot_b was inserted second but has order_index=0 → must be first
            Assert.Equal("slot_b", nodes[0].SlotKey);
            Assert.Equal(0,        nodes[0].OrderIndex);
            // slot_a was inserted first but has order_index=1 → must be second
            Assert.Equal("slot_a", nodes[1].SlotKey);
            Assert.Equal(1,        nodes[1].OrderIndex);

            // ── 2. StructureMapResolver: LayoutNodes built with positional componentId assignment ──
            var resolver = new StructureMapResolver(repo);
            var attractor = new AttractorResult(
                AttractorKey:   $"test:e2e:{suffix}",
                StructureMapId: smId.ToString(),
                PackageId:      seedPkgId,
                SchemaId:       seedSchId);

            var shape = await resolver.Resolve(attractor);

            Assert.Equal(layoutId.ToString(), shape.LayoutId);
            Assert.Null(shape.Errors);
            Assert.NotNull(shape.LayoutNodes);
            Assert.Equal(2, shape.LayoutNodes!.Count);

            // tensor[0] = slot_b (order=0) → positionally assigned to comp1Id (component_ids[0])
            Assert.Equal("slot_b",           shape.LayoutNodes[0].SlotKey);
            Assert.Equal(0,                  shape.LayoutNodes[0].OrderIndex);
            Assert.Equal(comp1Id.ToString(), shape.LayoutNodes[0].ComponentId);

            // tensor[1] = slot_a (order=1) → positionally assigned to comp2Id (component_ids[1])
            Assert.Equal("slot_a",           shape.LayoutNodes[1].SlotKey);
            Assert.Equal(1,                  shape.LayoutNodes[1].OrderIndex);
            Assert.Equal(comp2Id.ToString(), shape.LayoutNodes[1].ComponentId);

            // ── 3. EmissionBuilder: LayoutNodes order survives through to Emission ──
            var builder  = new EmissionBuilder();
            var emission = builder.Build(shape);

            Assert.Equal(layoutId.ToString(), emission.LayoutId);
            Assert.NotNull(emission.LayoutNodes);
            Assert.Equal(2, emission.LayoutNodes!.Count);

            Assert.Equal("slot_b",           emission.LayoutNodes[0].SlotKey);
            Assert.Equal(0,                  emission.LayoutNodes[0].OrderIndex);
            Assert.Equal(comp1Id.ToString(), emission.LayoutNodes[0].ComponentId);

            Assert.Equal("slot_a",           emission.LayoutNodes[1].SlotKey);
            Assert.Equal(1,                  emission.LayoutNodes[1].OrderIndex);
            Assert.Equal(comp2Id.ToString(), emission.LayoutNodes[1].ComponentId);
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
