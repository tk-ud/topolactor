using System.Text.Json;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class FileStorageBundleDispatchTests
{
    [Fact]
    public void FileStorageBundleStepHandler_SupportedOperationKeys_OnlyContainsComputeChecksum()
    {
        var handler = new FileStorageBundleStepHandler();
        Assert.Contains("compute_checksum", handler.SupportedOperationKeys);
        Assert.DoesNotContain("record_export_job", handler.SupportedOperationKeys);
        Assert.DoesNotContain("record_file_artifact", handler.SupportedOperationKeys);
        Assert.DoesNotContain("write_manifest_record", handler.SupportedOperationKeys);
        Assert.DoesNotContain("authorize_signed_download", handler.SupportedOperationKeys);
        Assert.Single(handler.SupportedOperationKeys);
    }

    [Fact]
    public void CoreAllowedOperationKeys_ContainsExecuteDbFunction()
    {
        Assert.Contains("execute_db_function", ExternalPortPolicyStepExecutor.AllowedOperationKeys);
    }

    [Fact]
    public void CoreAllowedOperationKeys_DoesNotContainFileStorageBundleSpecificKeys()
    {
        Assert.DoesNotContain("record_export_job", ExternalPortPolicyStepExecutor.AllowedOperationKeys);
        Assert.DoesNotContain("record_file_artifact", ExternalPortPolicyStepExecutor.AllowedOperationKeys);
        Assert.DoesNotContain("write_manifest_record", ExternalPortPolicyStepExecutor.AllowedOperationKeys);
        Assert.DoesNotContain("authorize_signed_download", ExternalPortPolicyStepExecutor.AllowedOperationKeys);
    }

    [Fact]
    public async Task ComputeChecksum_ProduceSha256PrefixedValue()
    {
        var executor = new ExternalPortPolicyStepExecutor(bundleHandlers: [new FileStorageBundleStepHandler()]);
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = Guid.NewGuid(),
            HttpResponse = new ExternalPortHttpResponse(200, "test-response-body")
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 9, "compute_checksum", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        await executor.ExecuteAsync(step, context);

        Assert.NotNull(context.ChecksumValue);
        Assert.StartsWith("sha256:", context.ChecksumValue);
    }

    [Fact]
    public async Task ComputeChecksum_WithoutArtifactInput_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor(bundleHandlers: [new FileStorageBundleStepHandler()]);
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = Guid.NewGuid()
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 9, "compute_checksum", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("CHECKSUM_INPUT_REQUIRED", ex.Message);
    }

    [Fact]
    public async Task ComputeChecksum_DoesNotHashExportJobIdFallback()
    {
        var executor = new ExternalPortPolicyStepExecutor(bundleHandlers: [new FileStorageBundleStepHandler()]);
        var exportJobId = Guid.NewGuid();
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = exportJobId,
            HttpResponse = null
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 9, "compute_checksum", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("CHECKSUM_INPUT_REQUIRED", ex.Message);
        Assert.Null(context.ChecksumValue);
    }

    [Fact]
    public async Task ExecuteDbFunction_WithFakeRepository_SetsExportJobIdOnContext()
    {
        var fakeDb = new FakeDbFunctionRepository();
        var executor = new ExternalPortPolicyStepExecutor(dbFunctionRepository: fakeDb);
        var context = new ExternalPortExecutionContext
        {
            RequestPayload = BuildPayload("job-001", "user1", "2026-06", "pdf"),
            PortRecord = BuildPortRecord()
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 10, "execute_db_function",
            new Dictionary<string, string> { ["function"] = "topology.fs_record_export_job", ["output"] = "ExportJobId" }, true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        await executor.ExecuteAsync(step, context);

        Assert.Contains("execute_db_function", context.ExecutedOperationKeys);
        Assert.Equal("topology.fs_record_export_job", fakeDb.LastFunctionName);
    }

    [Fact]
    public async Task ExecuteDbFunction_WithoutRepository_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext();

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 10, "execute_db_function",
            new Dictionary<string, string> { ["function"] = "topology.fs_record_export_job" }, true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("EXTERNAL_PORT_DB_FUNCTION_REPOSITORY_MISSING", ex.Message);
    }

    [Fact]
    public async Task ExecuteDbFunction_WithoutFunctionName_FailsClose()
    {
        var fakeDb = new FakeDbFunctionRepository();
        var executor = new ExternalPortPolicyStepExecutor(dbFunctionRepository: fakeDb);
        var context = new ExternalPortExecutionContext();

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 10, "execute_db_function",
            new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("EXTERNAL_PORT_DB_FUNCTION_NAME_MISSING", ex.Message);
    }

    [Fact]
    public void ExternalPortExecutionContext_ExposesFileStorageProps()
    {
        var props = typeof(ExternalPortExecutionContext).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("ExportJobId", props);
        Assert.Contains("ChecksumValue", props);
        Assert.Contains("FileArtifactId", props);
        Assert.Contains("AuthorizationKey", props);
    }

    [Fact]
    public void Source_DoesNotContainProviderKindBranchingInFileStorageHandlers()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/runtime/FileStorageBundleStepHandler.cs"));
        Assert.DoesNotContain("object_storage", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProviderKind ==", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"object_storage\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("s3.amazonaws.com", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storage.googleapis.com", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Source_FileStorageBundleStepHandler_DoesNotContainRemovedDomainMutations()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/runtime/FileStorageBundleStepHandler.cs"));
        Assert.DoesNotContain("RecordExportJob", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RecordFileArtifact", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WriteManifestRecord", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AuthorizeSignedDownload", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IFileStorageRepository", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compute_checksum", source);
    }

    [Fact]
    public void FileStorageContracts_DoNotContainPlaintextCredential()
    {
        var contractSource = File.ReadAllText(FindRepositoryFile("backend/schema/FileStorageContracts.cs"));
        Assert.DoesNotContain("access_key", contractSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret_key", contractSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket_name", contractSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint_url", contractSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", contractSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NpgsqlExternalPortDbFunctionRepository_AfterMigration_HasNoConcreteFsStarFunctions()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/repository/NpgsqlExternalPortDbFunctionRepository.cs"));
        Assert.Contains("IExternalPortDbFunctionRepository", source);
        Assert.Contains("EXTERNAL_PORT_DB_FUNCTION_UNKNOWN", source);
        // All fs_* concrete implementations migrated to execute_abstract_function manifest path
        Assert.DoesNotContain("topology.fs_record_export_job", source);
        Assert.DoesNotContain("topology.fs_record_file_artifact", source);
        Assert.DoesNotContain("topology.fs_write_manifest_record", source);
        Assert.DoesNotContain("topology.fs_authorize_signed_download", source);
        Assert.DoesNotContain("topology.fs_bind_record_file_attachment", source);
        Assert.DoesNotContain("topology.fs_list_record_file_attachments", source);
        Assert.DoesNotContain("topology.fs_unbind_record_file_attachment", source);
        // Prohibited patterns absent
        Assert.DoesNotContain("record_table_ref", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("object_storage", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider_kind", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("plaintext", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CoreAllowedOperationKeys_ContainsAppendRuntimeEventLog()
    {
        Assert.Contains("append_runtime_event_log", ExternalPortPolicyStepExecutor.AllowedOperationKeys);
    }

    [Fact]
    public async Task AppendRuntimeEventLog_WithEventTypeConfig_CallsRepository()
    {
        var fakeLog = new FakeRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: fakeLog);
        var context = new ExternalPortExecutionContext
        {
            ChecksumValue = "sha256:abc123"
        };
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 10, "append_runtime_event_log",
            new Dictionary<string, string>
            {
                ["event_type"] = "checksum_verified",
                ["entity_ref_key"] = "ChecksumValue"
            }, true);

        await executor.ExecuteAsync(step, context);

        Assert.Contains("append_runtime_event_log", context.ExecutedOperationKeys);
        Assert.Equal("checksum_verified", fakeLog.LastEventType);
        Assert.Equal("sha256:abc123", fakeLog.LastEntityId);
    }

    [Fact]
    public async Task AppendRuntimeEventLog_WithExportJobIdRef_ResolvesFromContext()
    {
        var fakeLog = new FakeRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: fakeLog);
        var exportJobId = Guid.NewGuid();
        var context = new ExternalPortExecutionContext { ExportJobId = exportJobId };
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 12, "append_runtime_event_log",
            new Dictionary<string, string>
            {
                ["event_type"] = "export_job_initiated",
                ["entity_ref_key"] = "ExportJobId"
            }, true);

        await executor.ExecuteAsync(step, context);

        Assert.Equal("export_job_initiated", fakeLog.LastEventType);
        Assert.Equal(exportJobId.ToString(), fakeLog.LastEntityId);
    }

    [Fact]
    public async Task AppendRuntimeEventLog_WithoutRepository_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext();
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 10, "append_runtime_event_log",
            new Dictionary<string, string> { ["event_type"] = "checksum_verified" }, true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("EXTERNAL_PORT_RUNTIME_EVENT_LOG_REPOSITORY_MISSING", ex.Message);
    }

    [Fact]
    public async Task AppendRuntimeEventLog_WithEmptyEventType_FailsClose()
    {
        var fakeLog = new FakeRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: fakeLog);
        var context = new ExternalPortExecutionContext();
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 10, "append_runtime_event_log",
            new Dictionary<string, string> { ["event_type"] = "" }, true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("EXTERNAL_PORT_RUNTIME_EVENT_LOG_EVENT_TYPE_MISSING", ex.Message);
    }

    [Fact]
    public async Task AppendRuntimeEventLog_WithUnknownEntityRefKey_FailsClose()
    {
        var fakeLog = new FakeRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: fakeLog);
        var context = new ExternalPortExecutionContext();
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 10, "append_runtime_event_log",
            new Dictionary<string, string>
            {
                ["event_type"] = "checksum_verified",
                ["entity_ref_key"] = "UnknownKey"
            }, true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("EXTERNAL_PORT_RUNTIME_EVENT_LOG_ENTITY_REF_KEY_UNRESOLVABLE", ex.Message);
    }

    [Fact]
    public async Task AppendRuntimeEventLog_WithKnownEntityRefKeyButNullContext_FailsClose()
    {
        var fakeLog = new FakeRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: fakeLog);
        var context = new ExternalPortExecutionContext { ChecksumValue = null };
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 10, "append_runtime_event_log",
            new Dictionary<string, string>
            {
                ["event_type"] = "checksum_verified",
                ["entity_ref_key"] = "ChecksumValue"
            }, true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("EXTERNAL_PORT_RUNTIME_EVENT_LOG_ENTITY_REF_KEY_UNRESOLVABLE", ex.Message);
    }

    [Fact]
    public void NpgsqlExternalPortRuntimeEventLogRepository_Source_InsertsToRuntimeEventLog()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/repository/NpgsqlExternalPortRuntimeEventLogRepository.cs"));
        Assert.Contains("topology.runtime_event_log", source);
        Assert.Contains("IExternalPortRuntimeEventLogRepository", source);
        Assert.Contains("event_type", source);
        Assert.Contains("entity_id", source);
        Assert.Contains("required_by_bundle", source);
        Assert.DoesNotContain("plaintext", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signed_url", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SeedSql_FileStoragePolicies_ContainAppendRuntimeEventLogSteps()
    {
        var seed = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));
        Assert.Contains("checksum_verified", seed);
        Assert.Contains("export_job_initiated", seed);
        Assert.Contains("file_write_completed", seed);
        Assert.Contains("signed_url_generated", seed);
        Assert.Contains("entity_ref_key", seed);
    }

    [Fact]
    public void SeedSql_AttachmentPolicies_UseExecuteAbstractFunctionNotDbFunction()
    {
        var seed = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));
        Assert.Contains("file_storage.bind_record_file_attachment", seed);
        Assert.Contains("file_storage.list_record_file_attachments", seed);
        Assert.Contains("file_storage.unbind_record_file_attachment", seed);
        // Attachment policies must NOT use execute_db_function for these operations
        Assert.DoesNotContain("'execute_db_function', '{\"function\":\"topology.fs_bind_record_file_attachment\"", seed);
        Assert.DoesNotContain("'execute_db_function', '{\"function\":\"topology.fs_list_record_file_attachments\"", seed);
        Assert.DoesNotContain("'execute_db_function', '{\"function\":\"topology.fs_unbind_record_file_attachment\"", seed);
    }

    [Fact]
    public void SeedSql_AttachmentManifests_HasStepConfigRecordTableRef_NotPayloadBinding()
    {
        var seed = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));
        // Verify step_config binding source exists for record_table_ref (manifest-authority, not payload)
        Assert.Contains("step_config", seed);
        Assert.Contains("record_table_ref", seed);
        // record_table_ref must NOT appear as a payload binding source in attachment steps
        Assert.DoesNotContain("'payload',     'record_table_ref'", seed);
        Assert.DoesNotContain("'payload', 'record_table_ref'", seed);
    }

    [Fact]
    public void SeedSql_Af02Af04_HaveProjectionSteps_WithUpdatedOutputShape()
    {
        var seed = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));
        // af02 output_shape must include step2_result:OutputProp
        Assert.Contains("\"result\":\"FileArtifactId\",\"step2_result\":\"OutputProp\"", seed);
        // af04 output_shape must include step2_result:OutputProp
        Assert.Contains("\"result\":\"AuthorizationKey\",\"step2_result\":\"OutputProp\"", seed);
        // Projection steps bf0a and bf0b must be present
        Assert.Contains("00000000-0000-0000-0000-00000000bf0a", seed);
        Assert.Contains("00000000-0000-0000-0000-00000000bf0b", seed);
        // Projection input bindings must be present
        Assert.Contains("00000000-0000-0000-0000-00000000c022", seed);
        Assert.Contains("00000000-0000-0000-0000-00000000c025", seed);
    }

    [Fact]
    public void SeedSql_Af02ProjectionBindings_OmitStorageRefAndSignedUrl()
    {
        var seed = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));
        // Verify that bf0a input bindings exist for non-secret fields only
        Assert.Contains("bf0a", seed);
        Assert.Contains("'file_artifact_id', 'result_context'", seed);
        Assert.Contains("'file_name',        'payload'", seed);
        Assert.Contains("'file_type',        'payload'", seed);
        // storage_ref must NOT appear as a non-secret binding in the projection step
        Assert.DoesNotContain("'storage_ref',      'result_context'", seed);
        Assert.DoesNotContain("'storage_ref',  'result_context'", seed);
    }

    [Fact]
    public void SeedSql_Af04ProjectionBindings_OmitSignedUrlAndCredential()
    {
        var seed = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));
        // Verify bf0b input bindings: authorization_key (opaque) and file_artifact_id only
        Assert.Contains("bf0b", seed);
        Assert.Contains("'authorization_key', 'result_context'", seed);
        // No signed_url or credential row in the bf0b binding section (deny keys in ARRAY[] don't count)
        Assert.DoesNotContain("'00000000-0000-0000-0000-00000000bf0b', 'signed_url'", seed);
        Assert.DoesNotContain("'00000000-0000-0000-0000-00000000bf0b', 'credential'", seed);
        Assert.DoesNotContain("'00000000-0000-0000-0000-00000000bf0b', 'storage_ref'", seed);
    }

    [Fact]
    public async Task ProjectionPrimitiveAdapter_DeniesSecretInputKey()
    {
        // Proves AbstractFunctionExecutor refuses a projection step with secret=true binding.
        // This guards the af02/af04 projection steps from accidentally binding signed_url or storage_ref.
        // No policy set in context — executor skips per-key policy check and only validates authority binding exists.
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 2, "projection", new Dictionary<string, string>(),
            [new AbstractFunctionInputBinding("signed_url", "result_context", "SignedUrl", false, true)],
            "OutputProp", true);

        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "file_storage.test", "external_port_runtime", "file_storage_bundle",
            [step], ["credential", "signed_url"], true,
            [new AbstractFunctionAuthorityBinding("policy", "file_storage_access_port_generic", true)],
            new Dictionary<string, string> { ["step2_result"] = "OutputProp" });

        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), [new ProjectionPrimitiveAdapter()]);
        // No Policy in context → policyKey is null → executor checks only that a policy authority binding exists
        var context = new AbstractFunctionExecutionContext("file_storage_bundle", externalPortContext: new ExternalPortExecutionContext());

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("file_storage.test", context));
        Assert.Equal(AbstractFunctionFailCloseStatus.SecretProjectionDenied, ex.Status);
    }

    [Fact]
    public async Task ProjectionPrimitiveAdapter_SafeFields_SetsOutputProp()
    {
        // Proves the projection primitive correctly populates OutputProp with non-secret fields,
        // mirroring what af02 step 2 (bf0a) does for file_artifact_id / file_name / file_type.
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 2, "projection", new Dictionary<string, string>(),
            [
                new AbstractFunctionInputBinding("file_artifact_id", "result_context", "FileArtifactId", true,  false),
                new AbstractFunctionInputBinding("file_name",        "payload",        "file_name",       true,  false),
                new AbstractFunctionInputBinding("file_type",        "payload",        "file_type",       false, false),
            ],
            "OutputProp", true);

        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "file_storage.test_projection", "external_port_runtime", "file_storage_bundle",
            [step], ["credential", "signed_url", "bucket", "endpoint", "storage_path", "storage_ref", "raw_storage_ref"], true,
            [new AbstractFunctionAuthorityBinding("policy", "file_storage_access_port_generic", true)],
            new Dictionary<string, string> { ["step2_result"] = "OutputProp" });

        var executor = new AbstractFunctionExecutor(new StaticManifestRepository(manifest), [new ProjectionPrimitiveAdapter()]);
        using var payloadDoc = JsonDocument.Parse("""{"file_name":"export-2026-06.pdf","file_type":"pdf"}""");
        // No Policy in context → policyKey is null → skips per-key policy check
        var externalCtx = new ExternalPortExecutionContext();
        var context = new AbstractFunctionExecutionContext("file_storage_bundle", requestPayload: payloadDoc.RootElement, externalPortContext: externalCtx);
        context.StoreResult("FileArtifactId", Guid.Parse("11111111-0000-0000-0000-000000000001"));

        await executor.ExecuteAsync("file_storage.test_projection", context);

        Assert.NotNull(externalCtx.OutputProp);
        var doc = JsonDocument.Parse(externalCtx.OutputProp!);
        Assert.True(doc.RootElement.TryGetProperty("file_artifact_id", out _));
        Assert.True(doc.RootElement.TryGetProperty("file_name", out var fileNameEl));
        Assert.Equal("export-2026-06.pdf", fileNameEl.GetString());
        Assert.False(doc.RootElement.TryGetProperty("signed_url", out _));
        Assert.False(doc.RootElement.TryGetProperty("storage_ref", out _));
        Assert.False(doc.RootElement.TryGetProperty("credential", out _));
    }

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest _manifest;
        public StaticManifestRepository(AbstractFunctionManifest manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) => Task.FromResult<AbstractFunctionManifest?>(_manifest);
    }

    private static ExternalPortPolicy BuildFileStoragePolicy(bool accessPort) =>
        new(Guid.NewGuid(), "file_storage_test_policy", accessPort ? "access_port" : "response_port", "file_storage_bundle",
        [
            NewStep(9, "compute_checksum"),
            NewStep(10, "execute_db_function", new Dictionary<string, string> { ["function"] = "topology.fs_record_export_job", ["output"] = "ExportJobId" }),
            NewStep(11, "execute_db_function", new Dictionary<string, string> { ["function"] = "topology.fs_record_file_artifact", ["output"] = "FileArtifactId" }),
            NewStep(12, "execute_db_function", new Dictionary<string, string> { ["function"] = "topology.fs_write_manifest_record", ["output"] = "ManifestId" }),
            NewStep(13, "execute_db_function", new Dictionary<string, string> { ["function"] = "topology.fs_authorize_signed_download", ["output"] = "AuthorizationKey" })
        ], true);

    private static ExternalPortPolicy BuildPolicy(string portKind, string bundle) =>
        new(Guid.NewGuid(), "test_policy", portKind, bundle, Array.Empty<ExternalPortPolicyStep>(), true);

    private static ExternalPortPolicyStep NewStep(int order, string operationKey, IReadOnlyDictionary<string, string>? config = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), order, operationKey, config ?? new Dictionary<string, string>(), true);

    private static ExternalPortRecord BuildPortRecord(string? referenceKey = "vault:ref:file_storage_credential") =>
        new(Guid.NewGuid(), "access_port", "file_storage_bundle", "object_storage",
            "env:FILE_STORAGE_ENDPOINT_REF", null, null, null, "external", referenceKey, true);

    private static JsonElement BuildPayload(string jobId, string requestedBy, string period, string exportFormat, string? idempotencyKey = null)
    {
        var payload = new
        {
            export_job_id = jobId,
            requested_by = requestedBy,
            period,
            export_format = exportFormat,
            idempotency_key = idempotencyKey ?? jobId
        };
        return JsonSerializer.SerializeToElement(new { dispatch_payload = payload });
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relativePath);
    }

    private sealed class FakeRuntimeEventLogRepository : IExternalPortRuntimeEventLogRepository
    {
        public string? LastEventType { get; private set; }
        public string? LastEntityId { get; private set; }
        public string? LastBundle { get; private set; }

        public Task AppendAsync(string eventType, string? entityId, string? requiredByBundle, CancellationToken ct = default)
        {
            LastEventType = eventType;
            LastEntityId = entityId;
            LastBundle = requiredByBundle;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDbFunctionRepository : IExternalPortDbFunctionRepository
    {
        public string? LastFunctionName { get; private set; }
        public IReadOnlyDictionary<string, string>? LastStepConfig { get; private set; }

        public Task ExecuteAsync(
            string functionName,
            IReadOnlyDictionary<string, string> stepConfig,
            ExternalPortExecutionContext context,
            CancellationToken ct = default)
        {
            LastFunctionName = functionName;
            LastStepConfig = stepConfig;
            if (functionName == "topology.fs_record_export_job")
                context.ExportJobId = Guid.NewGuid();
            else if (functionName == "topology.fs_record_file_artifact")
                context.FileArtifactId = Guid.NewGuid();
            else if (functionName == "topology.fs_authorize_signed_download")
            {
                context.AuthorizationKey = $"auth-ref:{Guid.NewGuid():N}";
                context.OutputProp = $"{{\"authorization_key\":\"{context.AuthorizationKey}\"}}";
            }
            return Task.CompletedTask;
        }
    }
}
