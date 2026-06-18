using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
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
    private const string AccessPortId  = "00000000-0000-0000-0000-000000000f01";
    private const string ResponsePortId = "00000000-0000-0000-0000-000000000f02";

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
        Assert.Equal("execute_abstract_function", step11.OperationKey);
        Assert.Equal("file_storage.record_export_job", step11.StepConfig["abstract_function_key"]);
        Assert.False(step11.StepConfig.ContainsKey("compatibility_function"));

        var step12 = steps[11];
        Assert.Equal("append_runtime_event_log", step12.OperationKey);
        Assert.Equal("export_job_initiated", step12.StepConfig["event_type"]);
        Assert.Equal("ExportJobId", step12.StepConfig["entity_ref_key"]);

        var step13 = steps[12];
        Assert.Equal("execute_abstract_function", step13.OperationKey);
        Assert.Equal("file_storage.record_file_artifact", step13.StepConfig["abstract_function_key"]);
        Assert.False(step13.StepConfig.ContainsKey("compatibility_function"));

        var step14 = steps[13];
        Assert.Equal("append_runtime_event_log", step14.OperationKey);
        Assert.Equal("file_write_completed", step14.StepConfig["event_type"]);
        Assert.Equal("FileArtifactId", step14.StepConfig["entity_ref_key"]);

        var step16 = steps[15];
        Assert.Equal("execute_abstract_function", step16.OperationKey);
        Assert.Equal("file_storage.authorize_signed_download", step16.StepConfig["abstract_function_key"]);
        Assert.False(step16.StepConfig.ContainsKey("compatibility_function"));

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
    public async Task SeededFileStorageAbstractFunctions_LoadAndExecuteThroughManifestRepository()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var policyRepo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);
        var manifestRepo = new NpgsqlAbstractFunctionManifestRepository(cs);
        var executor = new AbstractFunctionExecutor(manifestRepo, new IAbstractFunctionPrimitiveAdapter[]
        {
            new CallPostgresFunctionPrimitiveAdapter(cs),
            new ProjectionPrimitiveAdapter(),
            new FailClosePrimitiveAdapter()
        });

        var port = await policyRepo.LoadPortRecordAsync("file_storage_bundle", "access_port", null);
        Assert.NotNull(port);
        var policy = await policyRepo.LoadPolicyAsync(port);
        Assert.NotNull(policy);

        var step = policy.PolicySteps.Single(s => s.StepOrder == 11);
        Assert.Equal("execute_abstract_function", step.OperationKey);
        Assert.Equal("file_storage.record_export_job", step.StepConfig["abstract_function_key"]);

        var manifest = await manifestRepo.LoadAsync(step.StepConfig["abstract_function_key"]);
        Assert.NotNull(manifest);
        Assert.Equal("external_port_runtime", manifest.RuntimeLane);
        Assert.Equal("file_storage_bundle", manifest.AuthorityScope);
        Assert.Equal("call_postgres_function", manifest.Steps.Single().PrimitiveKey);

        var suffix = Guid.NewGuid().ToString("N");
        using var payload = System.Text.Json.JsonDocument.Parse($$"""
            {
              "idempotency_key": "abstract-{{suffix}}",
              "requested_by": "integration-test",
              "export_format": "json",
              "period": "2026-06",
              "file_name": "abstract-{{suffix}}.json",
              "file_type": "json"
            }
            """);
        var context = new ExternalPortExecutionContext
        {
            RequiredByBundle = "file_storage_bundle",
            PortKind = "access_port",
            PortRecord = port,
            RequestPayload = payload.RootElement,
            ChecksumValue = $"sha256:{suffix}"
        };

        await executor.ExecuteAsync("file_storage.record_export_job", new AbstractFunctionExecutionContext("file_storage_bundle", payload.RootElement, context));
        Assert.NotNull(context.ExportJobId);
        await executor.ExecuteAsync("file_storage.record_file_artifact", new AbstractFunctionExecutionContext("file_storage_bundle", payload.RootElement, context));
        Assert.NotNull(context.FileArtifactId);
        await executor.ExecuteAsync("file_storage.write_manifest_record", new AbstractFunctionExecutionContext("file_storage_bundle", payload.RootElement, context));
        await executor.ExecuteAsync("file_storage.authorize_signed_download", new AbstractFunctionExecutionContext("file_storage_bundle", payload.RootElement, context));
        Assert.StartsWith("auth-ref:", context.AuthorizationKey);
    }

    [Fact]
    public async Task SeededAbstractFunctions_LoadAuthorityBindings_AndEnforceFailClose()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var manifestRepo = new NpgsqlAbstractFunctionManifestRepository(cs);

        var manifest = await manifestRepo.LoadAsync("file_storage.record_export_job");
        Assert.NotNull(manifest);

        var authorityBindings = manifest.AuthorityBindings;
        Assert.NotNull(authorityBindings);
        Assert.NotEmpty(authorityBindings);

        var activeBindings = authorityBindings.Where(b => b.Active).ToList();
        Assert.NotEmpty(activeBindings);
        Assert.Contains(activeBindings, b => b.AuthorityKind == "policy" && b.AuthorityRef == "file_storage_access_port_generic");
        Assert.Contains(activeBindings, b => b.AuthorityKind == "table" && b.AuthorityRef == "topology.export_jobs");

        // Verify executor fail-closes when no authority bindings
        var noAuthorityManifest = new AbstractFunctionManifest(
            manifest.AbstractFunctionId, manifest.FunctionKey, manifest.RuntimeLane, manifest.AuthorityScope,
            manifest.Steps, manifest.DeniedProjectionKeys, manifest.Active,
            Array.Empty<AbstractFunctionAuthorityBinding>());
        var executor = new AbstractFunctionExecutor(
            new StaticManifest(noAuthorityManifest),
            Array.Empty<IAbstractFunctionPrimitiveAdapter>());
        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync(manifest.FunctionKey, new AbstractFunctionExecutionContext("file_storage_bundle")));
        Assert.Equal(AbstractFunctionFailCloseStatus.MissingAuthority, ex.Status);
    }

    private sealed class StaticManifest : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest _manifest;
        public StaticManifest(AbstractFunctionManifest manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) => Task.FromResult<AbstractFunctionManifest?>(_manifest);
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

    [Fact]
    public async Task AttachmentFsFunctions_BindListUnbind_ExecuteAgainstDb()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var recordTableRef = "public.test_attachment_record";
        var recordId = $"record-{suffix}";
        var exportJobId = await ScalarAsync<Guid>(conn,
            "SELECT topology.fs_record_export_job(@idempotency, 'file_storage_bundle', NULL, 'response_port', 'integration-test', 'json', NULL)",
            ("idempotency", $"attachment-{suffix}"));
        var fileArtifactId = await ScalarAsync<Guid>(conn,
            "SELECT topology.fs_record_file_artifact(@export_job_id, @file_name, 'json', 'vault:ref:file_storage_credential', @checksum)",
            ("export_job_id", exportJobId),
            ("file_name", $"attachment-{suffix}.json"),
            ("checksum", $"sha256:{suffix}"));

        var bindingId = await ScalarAsync<Guid>(conn,
            "SELECT topology.fs_bind_record_file_attachment(@record_table_ref, @record_id, @file_artifact_id, 'attachment', 'integration-test', @metadata::jsonb)",
            ("record_table_ref", recordTableRef),
            ("record_id", recordId),
            ("file_artifact_id", fileArtifactId),
            ("metadata", "{\"source\":\"integration\"}"));

        var listJson = await ScalarAsync<string>(conn,
            "SELECT topology.fs_list_record_file_attachments(@record_table_ref, @record_id)::text",
            ("record_table_ref", recordTableRef),
            ("record_id", recordId));

        Assert.Contains(bindingId.ToString(), listJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fileArtifactId.ToString(), listJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"attachment-{suffix}.json", listJson);
        Assert.DoesNotContain("storage_ref", listJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization_key", listJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signed_url", listJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vault:ref:file_storage_credential", listJson, StringComparison.OrdinalIgnoreCase);

        var removed = await ScalarAsync<int>(conn,
            "SELECT topology.fs_unbind_record_file_attachment(@record_table_ref, @record_id, @file_artifact_id, 'attachment')",
            ("record_table_ref", recordTableRef),
            ("record_id", recordId),
            ("file_artifact_id", fileArtifactId));
        Assert.Equal(1, removed);

        var afterUnbind = await ScalarAsync<string>(conn,
            "SELECT topology.fs_list_record_file_attachments(@record_table_ref, @record_id)::text",
            ("record_table_ref", recordTableRef),
            ("record_id", recordId));
        Assert.Equal("[]", afterUnbind);
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

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection conn, string sql, params (string Name, object? Value)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var result = await cmd.ExecuteScalarAsync();
        Assert.NotNull(result);
        return (T)result;
    }
}
