using Npgsql;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for hubs.hub.entity_id — the optional converged-entity pointer required by
/// docs/framework-core.yaml layers.hub.fields and docs/design/db-schema.yaml
/// tables.hubs (role: fk_entities_nullable). No FK constraint is declared on this column
/// (topology.entities is created after hubs.hub and itself references hubs.hub, so a hard FK in
/// this direction would be circular — see the COMMENT ON COLUMN in db/topology_tables.sql);
/// topology.entities remains the existence authority. This test proves the column persists a
/// real topology.entities.entity_id round-trip and stays NULL until explicitly set.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class HubConvergedEntityPointerLiveDbTests
{
    [Fact]
    public async Task HubEntityId_PersistsRoundTrip_AndDefaultsNull()
    {
        var cs = AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
        if (cs is null) return;

        var hubId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            await ExecAsync(
                "INSERT INTO hubs.hub (hub_id, relation) VALUES (@hid, '{}'::jsonb)",
                ("hid", hubId));

            // entity_id defaults to NULL until a hub is explicitly pointed at a converged entity.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT entity_id FROM hubs.hub WHERE hub_id = @hid";
                cmd.Parameters.AddWithValue("hid", hubId);
                var value = await cmd.ExecuteScalarAsync();
                Assert.True(value is null or DBNull, "hubs.hub.entity_id must default to NULL");
            }

            await ExecAsync(
                "INSERT INTO topology.entities (entity_id, hub_id) VALUES (@eid, @hid)",
                ("eid", entityId), ("hid", hubId));
            await ExecAsync(
                "UPDATE hubs.hub SET entity_id = @eid WHERE hub_id = @hid",
                ("eid", entityId), ("hid", hubId));

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT entity_id::text FROM hubs.hub WHERE hub_id = @hid";
                cmd.Parameters.AddWithValue("hid", hubId);
                var value = (string?)await cmd.ExecuteScalarAsync();
                Assert.Equal(entityId.ToString(), value);
            }
        }
        finally
        {
            await ExecAsync("DELETE FROM topology.entities WHERE hub_id = @hid", ("hid", hubId));
            await ExecAsync("DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", hubId));
        }
    }
}
