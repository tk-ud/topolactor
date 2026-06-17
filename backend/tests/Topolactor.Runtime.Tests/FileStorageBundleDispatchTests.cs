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
    public void NpgsqlExternalPortDbFunctionRepository_Source_DispatchesFsStarFunctions()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/repository/NpgsqlExternalPortDbFunctionRepository.cs"));
        Assert.Contains("topology.fs_record_export_job", source);
        Assert.Contains("topology.fs_record_file_artifact", source);
        Assert.Contains("topology.fs_write_manifest_record", source);
        Assert.Contains("topology.fs_authorize_signed_download", source);
        Assert.Contains("IExternalPortDbFunctionRepository", source);
        Assert.DoesNotContain("object_storage", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider_kind", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("plaintext", source, StringComparison.OrdinalIgnoreCase);
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
