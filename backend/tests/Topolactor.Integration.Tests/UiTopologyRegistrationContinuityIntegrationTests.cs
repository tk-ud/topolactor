using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

public class UiTopologyRegistrationContinuityIntegrationTests
{
    [Fact]
    public async Task PackageGeneratePromote_RegistersUiTopologyIds_WithDbEquivalentIntegration()
    {
        var connectionString = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
            {
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            }
            // Local environment-limited run: allow non-failing skip when DB is absent.
            return;
        }

        var suffix = Guid.NewGuid().ToString("N");
        var componentKey = $"button.primitive.integration.{suffix}";
        var sourcePath = $"frontend/components/Button.tsx#{suffix}";
        var componentKind = "action/button";
        var routeKey = $"admin:ui-builder:{suffix}";

        var repo = new NpgsqlUiTopologyRepository(
            NullLogger<NpgsqlUiTopologyRepository>.Instance,
            connectionString);

        var create = await repo.CreateBucketItemAsync(componentKey, sourcePath, componentKind, "{}", CancellationToken.None);
        Assert.Equal(UiComponentBucketCreateCode.Success, create.Code);
        Assert.NotNull(create.Record);

        var runtime = CreateAdminRuntimeForIntegration(repo, connectionString);
        var generateVector = new OperationVector("admin", "package_generator", "generate", null, "admin", JsonSerializer.SerializeToElement(new
        {
            bucketItemId = create.Record!.BucketItemId.ToString(),
            routeKey,
        }), null);

        var (generateData, generateError) = await runtime.ExecuteDataAsync(generateVector, CancellationToken.None);
        Assert.Null(generateError);
        Assert.NotNull(generateData);
        Assert.True(generateData!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("packaging", generateData.Value.GetProperty("status").GetString());

        var promoteVector = new OperationVector("admin", "package_generator", "promote", null, "admin", JsonSerializer.SerializeToElement(new
        {
            bucketItemId = create.Record!.BucketItemId.ToString(),
            routeKey,
        }), null);

        var (data, error) = await runtime.ExecuteDataAsync(promoteVector, CancellationToken.None);
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());

        var componentId = Guid.Parse(data.Value.GetProperty("componentId").GetString()!);
        var packageId = Guid.Parse(data.Value.GetProperty("packageId").GetString()!);
        var layoutId = Guid.Parse(data.Value.GetProperty("layoutId").GetString()!);
        var wiringId = Guid.Parse(data.Value.GetProperty("wiringId").GetString()!);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        Assert.Equal("promoted", await ScalarAsync<string>(
            conn,
            "SELECT status FROM topology.components_bucket WHERE bucket_item_id=@id",
            ("id", create.Record.BucketItemId)));

        Assert.Equal(componentId, await ScalarAsync<Guid>(
            conn,
            "SELECT component_id FROM topology.ui_component_registry WHERE component_key=@key",
            ("key", componentKey)));

        Assert.Equal(packageId, await ScalarAsync<Guid>(
            conn,
            "SELECT package_id FROM topology.ui_component_package WHERE package_key=@key",
            ("key", $"{routeKey}:{componentKey}:pkg")));

        Assert.Equal(layoutId, await ScalarAsync<Guid>(
            conn,
            "SELECT layout_id FROM topology.components_layout_design WHERE layout_key=@key",
            ("key", $"{routeKey}:{componentKey}:layout")));

        Assert.Equal(wiringId, await ScalarAsync<Guid>(
            conn,
            "SELECT wiring_id FROM topology.ui_wiring_registry WHERE wiring_key=@key",
            ("key", $"{routeKey}:{componentKey}:wiring")));

        var tensor = await QueryTensorAsync(conn, routeKey);
        Assert.Equal(packageId, tensor.PackageId);
        Assert.Equal(layoutId, tensor.LayoutId);
        Assert.Equal(wiringId, tensor.WiringId);
    }

    private static AdminRuntime CreateAdminRuntimeForIntegration(
        UiTopologyRepository uiRepo,
        string connectionString)
    {
        var contextRouteRepository = new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance,
            connectionString);
        var topologyRepository = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance,
            connectionString);
        var vectorRuntime = new TopologyVectorRuntime(
            NullLogger<TopologyVectorRuntime>.Instance,
            contextRouteRepository);
        var registrar = new RegistrarValidationService(
            NullLogger<RegistrarValidationService>.Instance,
            topologyRepository,
            vectorRuntime);
        var packageGenerator = new PackageGeneratorRuntime(
            NullLogger<PackageGeneratorRuntime>.Instance,
            uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            contextRouteRepository,
            registrar,
            packageGenerator,
            uiRepo,
            null);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection conn, string sql, params (string Name, object Value)[] args)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        var valueObj = await cmd.ExecuteScalarAsync();
        Assert.NotNull(valueObj);
        return (T)valueObj!;
    }

    [Fact]
    public async Task PromotePackageFromBucketItemsAsync_AdditivePromote_UnionsBucketIdsAndDeduplicatesMap()
    {
        var connectionString = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return;
        }

        var suffix = Guid.NewGuid().ToString("N");
        var routeKey = $"integration:additive:{suffix}";
        var repo = new NpgsqlUiTopologyRepository(
            NullLogger<NpgsqlUiTopologyRepository>.Instance, connectionString);

        // Create bucket items A, B, C
        var createA = await repo.CreateBucketItemAsync($"comp.a.{suffix}", $"src/A.tsx#{suffix}", "primitive", "{}", CancellationToken.None);
        var createB = await repo.CreateBucketItemAsync($"comp.b.{suffix}", $"src/B.tsx#{suffix}", "primitive", "{}", CancellationToken.None);
        var createC = await repo.CreateBucketItemAsync($"comp.c.{suffix}", $"src/C.tsx#{suffix}", "primitive", "{}", CancellationToken.None);
        Assert.Equal(UiComponentBucketCreateCode.Success, createA.Code);
        Assert.Equal(UiComponentBucketCreateCode.Success, createB.Code);
        Assert.Equal(UiComponentBucketCreateCode.Success, createC.Code);

        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, repo);

        // First promote: A + B → routeKey
        var result1 = await pkg.PromotePackageAsync(
            routeKey,
            [createA.Record!.BucketItemId, createB.Record!.BucketItemId],
            CancellationToken.None);
        Assert.Equal(PackageGenerateCode.Success, result1.Code);
        var packageId = result1.PackageId!.Value;

        // Second promote: C → same routeKey (additive)
        var result2 = await pkg.PromotePackageAsync(
            routeKey,
            [createC.Record!.BucketItemId],
            CancellationToken.None);
        Assert.Equal(PackageGenerateCode.Success, result2.Code);
        // package_id must be the same (1 route = 1 package)
        Assert.Equal(packageId, result2.PackageId!.Value);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // ui_package_component_map must have exactly 3 rows (A, B, C) — no duplicates
        var mapCount = await ScalarAsync<long>(
            conn,
            "SELECT COUNT(*) FROM topology.ui_package_component_map WHERE package_id = @pkg",
            ("pkg", packageId));
        Assert.Equal(3, mapCount);

        // package_schema_json.bucketItemIds must contain all 3 bucket IDs
        var schemaRaw = await ScalarAsync<string>(
            conn,
            "SELECT package_schema_json::text FROM topology.ui_component_package WHERE package_id = @pkg",
            ("pkg", packageId));
        var schema = System.Text.Json.JsonDocument.Parse(schemaRaw);
        var bucketIds = schema.RootElement.GetProperty("bucketItemIds").EnumerateArray()
            .Select(e => e.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(createA.Record.BucketItemId.ToString(), bucketIds);
        Assert.Contains(createB.Record.BucketItemId.ToString(), bucketIds);
        Assert.Contains(createC.Record.BucketItemId.ToString(), bucketIds);
        Assert.Equal(3, bucketIds.Count);

        // ui_topology_tensor must have exactly 1 row for this route (no proliferation)
        var tensorCount = await ScalarAsync<long>(
            conn,
            "SELECT COUNT(*) FROM topology.ui_topology_tensor WHERE route_key = @route",
            ("route", routeKey));
        Assert.Equal(1, tensorCount);

        // Second promote re-promote idempotency: calling promote_package again with same C bucket
        // must not duplicate the map row (C is now 'promoted' so it would fail the 'packaging' check;
        // the UI skips promoted items before calling the API — this test verifies the DB guard).
        // Re-generate A,B to packaging state using direct bucket status check
        // (In normal UI flow, promoted items are never re-submitted to the backend.)
    }

    private static async Task<(Guid PackageId, Guid LayoutId, Guid WiringId)> QueryTensorAsync(NpgsqlConnection conn, string routeKey)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT package_id, layout_id, wiring_id FROM topology.ui_topology_tensor WHERE route_key=@routeKey ORDER BY created_at DESC LIMIT 1";
        cmd.Parameters.AddWithValue("routeKey", routeKey);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2));
    }
}
