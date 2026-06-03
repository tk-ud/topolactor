using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Integration.Tests;

public class UiTopologyLayoutPatchRollbackIntegrationTests
{
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ApplyConfirmedLayoutPatchAsync_TensorMissing_RollsBackLayoutUpdate()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException("TOPOLACTOR_TEST_DB_CONNECTION required.");
            return;
        }

        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, cs);
        var layoutId = Guid.NewGuid();
        var routeSeed = $"route-seed-{Guid.NewGuid():N}";
        var originalSchema = "{\"before\":1}";

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO ui_layout_registry(layout_id, layout_key, layout_kind, layout_schema_json) VALUES (@id,@key,'grid',@schema::jsonb)";
            cmd.Parameters.AddWithValue("id", layoutId);
            cmd.Parameters.AddWithValue("key", $"layout-{layoutId:N}");
            cmd.Parameters.AddWithValue("schema", originalSchema);
            await cmd.ExecuteNonQueryAsync();
        }

        // seed tensor with a different route key so apply routeKey mismatch yields 0 rows on tensor update
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        await using (var c1 = conn.CreateCommand())
        {
            c1.CommandText = "INSERT INTO ui_component_package(package_id,package_key,package_kind) VALUES(@id,@k,'pkg')";
            c1.Parameters.AddWithValue("id", packageId);
            c1.Parameters.AddWithValue("k", $"pkg-{packageId:N}");
            await c1.ExecuteNonQueryAsync();
        }
        await using (var c2 = conn.CreateCommand())
        {
            c2.CommandText = "INSERT INTO ui_wiring_registry(wiring_id,wiring_key,wiring_kind,target_surface) VALUES(@id,@k,'evt','ui')";
            c2.Parameters.AddWithValue("id", wiringId);
            c2.Parameters.AddWithValue("k", $"wiring-{wiringId:N}");
            await c2.ExecuteNonQueryAsync();
        }
        await using (var c3 = conn.CreateCommand())
        {
            c3.CommandText = "INSERT INTO ui_topology_tensor(route_key,package_id,layout_id,wiring_id) VALUES(@r,@p,@l,@w)";
            c3.Parameters.AddWithValue("r", routeSeed);
            c3.Parameters.AddWithValue("p", packageId);
            c3.Parameters.AddWithValue("l", layoutId);
            c3.Parameters.AddWithValue("w", wiringId);
            await c3.ExecuteNonQueryAsync();
        }

        var res = await repo.ApplyConfirmedLayoutPatchAsync(
            packageId, layoutId, "route-mismatch", "{\"after\":2}", ["color.action.primary.background"], null);
        Assert.False(res.Ok);
        Assert.Contains("package", res.Message, StringComparison.OrdinalIgnoreCase);

        await using var verify = conn.CreateCommand();
        verify.CommandText = "SELECT layout_schema_json::text FROM ui_layout_registry WHERE layout_id=@id";
        verify.Parameters.AddWithValue("id", layoutId);
        var after = (string)(await verify.ExecuteScalarAsync())!;
        Assert.Contains("before", after); // rollback preserved original
    }
}
