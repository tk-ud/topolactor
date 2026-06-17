using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live DB proof for file_storage_bundle port record / policy / step projection.
/// Reads the seeded e4/e5 port records to prove the 17-step pipeline is projected
/// from DB records — not from handwritten test fixtures.
/// TOPOLACTOR_TEST_DB_CONNECTION unset means explicit local skip.
/// Set TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class FileStoragePortConsumerLiveDbTests
{
    private const string AccessPortId  = "00000000-0000-0000-0000-0000000000e4";
    private const string ResponsePortId = "00000000-0000-0000-0000-0000000000e5";

    [Fact]
    public async Task SeededFileStorageAccessPort_ProjectsPortRecordFromDb()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);

        var port = await repo.LoadPortRecordAsync("file_storage_bundle", "access_port", null);

        Assert.NotNull(port);
        Assert.Equal(Guid.Parse(AccessPortId), port.PortId);
        Assert.Equal("access_port", port.PortKind);
        Assert.Equal("file_storage_bundle", port.RequiredByBundle);
        Assert.Equal("object_storage", port.ProviderKind);
        Assert.Equal("external", port.CredentialKind);
        Assert.NotNull(port.ReferenceKey);
        Assert.StartsWith("vault:ref:", port.ReferenceKey);
    }

    [Fact]
    public async Task SeededFileStorageAccessPort_ProjectsPolicy17StepsFromDb()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);

        var port = await repo.LoadPortRecordAsync("file_storage_bundle", "access_port", null);
        Assert.NotNull(port);

        var policy = await repo.LoadPolicyAsync(port);
        Assert.NotNull(policy);
        Assert.Equal("file_storage_bundle", policy.RequiredByBundle);

        var steps = policy.PolicySteps.OrderBy(s => s.StepOrder).ToList();
        Assert.Equal(17, steps.Count);

        Assert.Equal("compute_checksum", steps[8].OperationKey);

        var step10 = steps[9];
        Assert.Equal("append_runtime_event_log", step10.OperationKey);
        Assert.Equal("checksum_verified", step10.StepConfig["event_type"]);
        Assert.Equal("ChecksumValue", step10.StepConfig["entity_ref_key"]);

        var step11 = steps[10];
        Assert.Equal("execute_db_function", step11.OperationKey);
        Assert.Equal("topology.fs_record_export_job", step11.StepConfig["function"]);

        var step12 = steps[11];
        Assert.Equal("append_runtime_event_log", step12.OperationKey);
        Assert.Equal("export_job_initiated", step12.StepConfig["event_type"]);
        Assert.Equal("ExportJobId", step12.StepConfig["entity_ref_key"]);

        var step13 = steps[12];
        Assert.Equal("execute_db_function", step13.OperationKey);
        Assert.Equal("topology.fs_record_file_artifact", step13.StepConfig["function"]);

        var step14 = steps[13];
        Assert.Equal("append_runtime_event_log", step14.OperationKey);
        Assert.Equal("file_write_completed", step14.StepConfig["event_type"]);
        Assert.Equal("FileArtifactId", step14.StepConfig["entity_ref_key"]);

        var step16 = steps[15];
        Assert.Equal("execute_db_function", step16.OperationKey);
        Assert.Equal("topology.fs_authorize_signed_download", step16.StepConfig["function"]);

        var step17 = steps[16];
        Assert.Equal("append_runtime_event_log", step17.OperationKey);
        Assert.Equal("signed_url_generated", step17.StepConfig["event_type"]);
        Assert.Equal("AuthorizationKey", step17.StepConfig["entity_ref_key"]);
    }

    [Fact]
    public async Task SeededFileStorageResponsePort_Projects17StepsFromDb()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);

        var port = await repo.LoadPortRecordAsync("file_storage_bundle", "response_port", null);
        Assert.NotNull(port);
        Assert.Equal(Guid.Parse(ResponsePortId), port.PortId);
        Assert.Equal("response_port", port.PortKind);

        var policy = await repo.LoadPolicyAsync(port);
        Assert.NotNull(policy);

        var steps = policy.PolicySteps.OrderBy(s => s.StepOrder).ToList();
        Assert.Equal(17, steps.Count);

        var appendSteps = steps.Where(s => s.OperationKey == "append_runtime_event_log").ToList();
        Assert.Equal(4, appendSteps.Count);

        var eventTypes = appendSteps.Select(s => s.StepConfig["event_type"]).OrderBy(e => e).ToList();
        Assert.Contains("checksum_verified", eventTypes);
        Assert.Contains("export_job_initiated", eventTypes);
        Assert.Contains("file_write_completed", eventTypes);
        Assert.Contains("signed_url_generated", eventTypes);
    }

    [Fact]
    public async Task RuntimeEventLogRepository_AppendAsync_WritesToDb()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var repo = new NpgsqlExternalPortRuntimeEventLogRepository(cs);
        var eventType = $"test_event_{Guid.NewGuid():N}";
        var entityId = Guid.NewGuid().ToString();
        var bundle = "file_storage_bundle";

        await repo.AppendAsync(eventType, entityId, bundle);

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT event_type, entity_id, required_by_bundle
            FROM topology.runtime_event_log
            WHERE event_type = @event_type AND entity_id = @entity_id
            """;
        cmd.Parameters.AddWithValue("event_type", eventType);
        cmd.Parameters.AddWithValue("entity_id", entityId);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected row in topology.runtime_event_log");
        Assert.Equal(eventType, reader.GetString(0));
        Assert.Equal(entityId, reader.GetString(1));
        Assert.Equal(bundle, reader.GetString(2));

        await reader.CloseAsync();
        await using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM topology.runtime_event_log WHERE event_type = @event_type AND entity_id = @entity_id";
        del.Parameters.AddWithValue("event_type", eventType);
        del.Parameters.AddWithValue("entity_id", entityId);
        await del.ExecuteNonQueryAsync();
    }

    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }
}
