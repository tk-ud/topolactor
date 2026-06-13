using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Text.Json;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Representative scenario fixtures for admin runtime DB→projection continuity.
///
/// Policy (pipeline-continuity-ssot.yaml backend_db_sse_scenario_harness_policy):
///   representative_route: frontend_payload_to_dispatch_to_runtime_to_db_to_projection_or_sse_to_frontend_final_assertion
///   unique_side_effect_runtime_requires_individual_scenario
///   runtime_without_sse_contract_may_use_refetch_or_projection_response_assertion
///
/// Route covered here:
///   EndpointRequestDto frontend payload → DispatchEndpoint → RuntimeTimelineScheduler
///   → ManifestDispatcher dev/admin override → AdminRuntime action → DB state changed
///   → runtime refetch/projection assertion.
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset → explicit local skip.
/// Set TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require live DB in CI.
/// </summary>
public class AdminRuntimeDbProjectionScenarioTests
{
    /// <summary>
    /// Representative scenario: layout_patch:apply action class.
    /// Unique DB side effect: ui_topology_tensor.layout_patch_json updated.
    /// Projection/refetch assertion: ui_topology:get_layout_patch_draft returns the applied tensor and
    /// LoadLayoutNodesAsync returns updated nodes from the DB projection source.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task LayoutPatchApply_DispatchRuntimeDbRoundTrip_NodesVisibleInProjection()
    {
        var cs = RequireConnectionStringOrSkip();
        if (cs is null) return;

        var suffix    = Guid.NewGuid().ToString("N")[..12];
        var layoutId  = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var wiringId  = Guid.NewGuid();
        var routeKey  = $"/test/apply-scenario-{suffix}";
        var comp1Id   = Guid.NewGuid().ToString();
        var comp2Id   = Guid.NewGuid().ToString();

        var uiRepo   = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, cs);
        var topoRepo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);

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
            await InsertTopologyTensorPrerequisitesAsync(
                cs, layoutId, packageId, wiringId, routeKey,
                layoutKey: $"apply-scenario-{suffix}",
                packageKey: $"apply-pkg-{suffix}",
                wiringKey: $"apply-wiring-{suffix}",
                initialLayoutPatchJson: "{}");

            await using var route = await StartAdminDispatchRouteAsync(cs);

            var applyResponse = await DispatchAdminAsync(route.Endpoint, "layout_patch", "apply", new
            {
                packageId = packageId.ToString(),
                layoutId = layoutId.ToString(),
                routeKey,
                tensorPatchJson,
            });

            Assert.True(applyResponse.Success, FormatErrors(applyResponse));
            Assert.NotNull(applyResponse.Emission);
            Assert.True(applyResponse.Emission!.Data.HasValue);
            Assert.True(applyResponse.Emission.Data.Value.GetProperty("Ok").GetBoolean());
            Assert.True(applyResponse.Emission.Data.Value.GetProperty("Valid").GetBoolean());

            var draftResponse = await DispatchAdminAsync(route.Endpoint, "ui_topology", "get_layout_patch_draft", new
            {
                packageId = packageId.ToString(),
                layoutId = layoutId.ToString(),
                routeKey,
            });

            Assert.True(draftResponse.Success, FormatErrors(draftResponse));
            Assert.NotNull(draftResponse.Emission);
            Assert.True(draftResponse.Emission!.Data.HasValue);
            var appliedJson = draftResponse.Emission.Data.Value.GetProperty("tensorPatchJson").GetString();
            Assert.Contains("apply-node-a", appliedJson);
            Assert.Contains("apply-node-b", appliedJson);

            var nodes = await topoRepo.LoadLayoutNodesAsync(layoutId);
            Assert.Equal(2, nodes.Count);
            Assert.Equal("apply-node-a", nodes[0].NodeId);
            Assert.Equal("catalog_component", nodes[0].NodeKind);
            Assert.Equal(0, nodes[0].OrderIndex);
            Assert.Equal("apply-node-b", nodes[1].NodeId);
            Assert.Equal("catalog_component", nodes[1].NodeKind);
            Assert.Equal(1, nodes[1].OrderIndex);
        }
        finally
        {
            await CleanupTopologyTensorScenarioAsync(cs, layoutId, packageId, wiringId);
        }
    }

    /// <summary>
    /// Representative scenario: component_style_design:upsert action class.
    /// Unique DB side effect: components_style_design upserted, components_package_design merged.
    /// Projection/refetch assertion: component_style_design:list returns the design entry through the
    /// same dispatch/runtime route after DB mutation.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ComponentStyleDesignUpsert_DispatchRuntimeDbRoundTrip_DesignVisibleInProjection()
    {
        var cs = RequireConnectionStringOrSkip();
        if (cs is null) return;

        var suffix       = Guid.NewGuid().ToString("N")[..12];
        var layoutId     = Guid.NewGuid();
        var packageId    = Guid.NewGuid();
        var wiringId     = Guid.NewGuid();
        var designName   = $"design-scenario-{suffix}";
        var layoutNodeId = $"node-{suffix}";
        var routeKey     = $"/test/design-scenario-{suffix}";

        var initialLayoutPatchJson = JsonSerializer.Serialize(new
        {
            nodes = new object[]
            {
                new
                {
                    nodeId = layoutNodeId,
                    nodeKind = "catalog_component",
                    componentKey = "card.primitive",
                    componentId = Guid.NewGuid().ToString(),
                    slotKey = "main",
                    orderIndex = 0,
                    parentNodeId = (string?)null,
                },
            }
        });

        try
        {
            await InsertTopologyTensorPrerequisitesAsync(
                cs, layoutId, packageId, wiringId, routeKey,
                layoutKey: $"design-layout-{suffix}",
                packageKey: $"design-pkg-{suffix}",
                wiringKey: $"design-wiring-{suffix}",
                initialLayoutPatchJson);

            await using var route = await StartAdminDispatchRouteAsync(cs);

            var upsertResponse = await DispatchAdminAsync(route.Endpoint, "component_style_design", "upsert", new
            {
                packageId = packageId.ToString(),
                componentId = (string?)null,
                layoutNodeId,
                name = designName,
                classname = "",
                tailwind = "",
                cssTokenRefs = new[] { "color.action.primary.background" },
                responsiveTokenRefs = new Dictionary<string, IReadOnlyList<string>>(),
                inlineText = "テストテキスト",
                linkHref = "",
                linkTarget = "",
                reactionIntent = "",
            });

            Assert.True(upsertResponse.Success, FormatErrors(upsertResponse));
            Assert.NotNull(upsertResponse.Emission);
            Assert.True(upsertResponse.Emission!.Data.HasValue);
            Assert.True(upsertResponse.Emission.Data.Value.GetProperty("ok").GetBoolean());
            Assert.True(upsertResponse.Emission.Data.Value.TryGetProperty("designId", out _));

            var listResponse = await DispatchAdminAsync(route.Endpoint, "component_style_design", "list", new
            {
                packageId = packageId.ToString(),
            });

            Assert.True(listResponse.Success, FormatErrors(listResponse));
            Assert.NotNull(listResponse.Emission);
            Assert.True(listResponse.Emission!.Data.HasValue);

            var designs = listResponse.Emission.Data.Value.EnumerateArray().ToList();
            var entry = designs.FirstOrDefault(d => d.GetProperty("name").GetString() == designName);
            Assert.NotEqual(default, entry);
            Assert.Equal(layoutNodeId, entry.GetProperty("layoutNodeId").GetString());
            Assert.Equal("テストテキスト", entry.GetProperty("inlineText").GetString());
            Assert.Contains("color.action.primary.background",
                entry.GetProperty("cssTokenRefs").EnumerateArray().Select(e => e.GetString()));
        }
        finally
        {
            await CleanupDesignScenarioAsync(cs, layoutId, packageId, wiringId, designName);
        }
    }

    private static string? RequireConnectionStringOrSkip()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(cs)) return cs;
        if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
            throw new InvalidOperationException(
                "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
        return null;
    }

    private static async Task InsertTopologyTensorPrerequisitesAsync(
        string cs,
        Guid layoutId,
        Guid packageId,
        Guid wiringId,
        string routeKey,
        string layoutKey,
        string packageKey,
        string wiringKey,
        string initialLayoutPatchJson)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.components_layout_design (layout_id, layout_key, layout_kind) " +
                "VALUES (@id, @key, 'grid')";
            cmd.Parameters.AddWithValue("id", layoutId);
            cmd.Parameters.AddWithValue("key", layoutKey);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.ui_component_package (package_id, package_key, package_kind) " +
                "VALUES (@id, @key, 'pkg')";
            cmd.Parameters.AddWithValue("id", packageId);
            cmd.Parameters.AddWithValue("key", packageKey);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.ui_wiring_registry (wiring_id, wiring_key, wiring_kind, target_surface) " +
                "VALUES (@id, @key, 'evt', 'ui')";
            cmd.Parameters.AddWithValue("id", wiringId);
            cmd.Parameters.AddWithValue("key", wiringKey);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO topology.ui_topology_tensor " +
                "  (tensor_id, route_key, package_id, layout_id, wiring_id, layout_patch_json) " +
                "VALUES (@tid, @route, @pkg, @layout, @wiring, @patch::jsonb)";
            cmd.Parameters.AddWithValue("tid", Guid.NewGuid());
            cmd.Parameters.AddWithValue("route", routeKey);
            cmd.Parameters.AddWithValue("pkg", packageId);
            cmd.Parameters.AddWithValue("layout", layoutId);
            cmd.Parameters.AddWithValue("wiring", wiringId);
            cmd.Parameters.AddWithValue("patch", initialLayoutPatchJson);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<AdminDispatchRoute> StartAdminDispatchRouteAsync(string cs)
    {
        var uiRepo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, cs);
        var topoRepo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, cs);
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var adminRuntime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            topologyRepository: topoRepo);

        var vectorResolver = new OperationVectorResolver();
        var targetOverride = new TargetDispatchOverride(
            NullLogger<TargetDispatchOverride>.Instance,
            topoRepo,
            adminRuntime);
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            new Dictionary<string, IDispatchableRuntime>(StringComparer.OrdinalIgnoreCase),
            vectorResolver,
            targetOverride);
        var scheduler = new RuntimeTimelineScheduler(
            NullLogger<RuntimeTimelineScheduler>.Instance,
            dispatcher);
        var endpoint = new DispatchEndpoint(
            NullLogger<DispatchEndpoint>.Instance,
            scheduler);

        await scheduler.StartAsync(CancellationToken.None);
        return new AdminDispatchRoute(endpoint, scheduler);
    }

    private static async Task<EndpointResponseDto> DispatchAdminAsync(
        DispatchEndpoint endpoint,
        string layer,
        string action,
        object payload)
    {
        var request = new EndpointRequestDto(
            OperationType: "AdminRuntimeScenario",
            Target: "admin",
            Layer: layer,
            Action: action,
            IdOrHubId: null,
            Payload: JsonSerializer.SerializeToElement(payload),
            Context: null,
            TriggerKind: "client",
            Role: "admin");

        return await endpoint.HandleAsync(request);
    }

    private static string FormatErrors(EndpointResponseDto response) =>
        string.Join("; ", response.Errors.Select(e => $"{e.Code}:{e.Message}"));

    private static async Task CleanupTopologyTensorScenarioAsync(
        string cs,
        Guid layoutId,
        Guid packageId,
        Guid wiringId)
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
            cmd.Parameters.AddWithValue("pkg", packageId);
            cmd.Parameters.AddWithValue("wiring", wiringId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Cleanup failure does not affect test result.
        }
    }

    private static async Task CleanupDesignScenarioAsync(
        string cs,
        Guid layoutId,
        Guid packageId,
        Guid wiringId,
        string designName)
    {
        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
DELETE FROM topology.components_package_design WHERE package_id = @pkg;
DELETE FROM topology.components_style_design WHERE name = @name;
DELETE FROM topology.ui_topology_tensor WHERE layout_id = @layout;
DELETE FROM topology.components_layout_design WHERE layout_id = @layout;
DELETE FROM topology.ui_component_package WHERE package_id = @pkg;
DELETE FROM topology.ui_wiring_registry WHERE wiring_id = @wiring;";
            cmd.Parameters.AddWithValue("layout", layoutId);
            cmd.Parameters.AddWithValue("pkg", packageId);
            cmd.Parameters.AddWithValue("wiring", wiringId);
            cmd.Parameters.AddWithValue("name", designName);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Cleanup failure does not affect test result.
        }
    }

    private sealed class AdminDispatchRoute : IAsyncDisposable
    {
        public AdminDispatchRoute(DispatchEndpoint endpoint, RuntimeTimelineScheduler scheduler)
        {
            Endpoint = endpoint;
            Scheduler = scheduler;
        }

        public DispatchEndpoint Endpoint { get; }
        private RuntimeTimelineScheduler Scheduler { get; }

        public async ValueTask DisposeAsync()
        {
            await Scheduler.StopAsync(CancellationToken.None);
            Scheduler.Dispose();
        }
    }
}
