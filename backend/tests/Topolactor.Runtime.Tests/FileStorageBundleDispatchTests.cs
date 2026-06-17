using System.Text.Json;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class FileStorageBundleDispatchTests
{
    [Fact]
    public void FileStorageBundleStepHandler_SupportedOperationKeys_ContainsAllDomainKeys()
    {
        var handler = new FileStorageBundleStepHandler(new FakeFileStorageRepository());
        Assert.Contains("record_export_job", handler.SupportedOperationKeys);
        Assert.Contains("compute_checksum", handler.SupportedOperationKeys);
        Assert.Contains("record_file_artifact", handler.SupportedOperationKeys);
        Assert.Contains("write_manifest_record", handler.SupportedOperationKeys);
        Assert.Contains("authorize_signed_download", handler.SupportedOperationKeys);
    }

    [Fact]
    public void CoreAllowedOperationKeys_DoesNotContainFileStorageBundleKeys()
    {
        Assert.DoesNotContain("record_export_job", ExternalPortPolicyStepExecutor.AllowedOperationKeys);
        Assert.DoesNotContain("record_file_artifact", ExternalPortPolicyStepExecutor.AllowedOperationKeys);
    }

    private static ExternalPortPolicyStepExecutor BuildExecutorWithFileStorage(IFileStorageRepository repo) =>
        new(bundleHandlers: [new FileStorageBundleStepHandler(repo)]);

    [Fact]
    public async Task ExecutePolicyAsync_WithFileStorageBundleHandler_ExecutesAllDomainSteps()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var policy = BuildFileStoragePolicy(accessPort: true);
        var context = new ExternalPortExecutionContext
        {
            RequestPayload = BuildPayload("job-001", "system", "2026-06", "pdf"),
            PortRecord = BuildPortRecord()
        };

        context.HttpResponse = new ExternalPortHttpResponse(200, "test-export-response-body");
        await executor.ExecutePolicyAsync(policy, context);

        Assert.Contains("record_export_job", context.ExecutedOperationKeys);
        Assert.Contains("compute_checksum", context.ExecutedOperationKeys);
        Assert.Contains("record_file_artifact", context.ExecutedOperationKeys);
        Assert.Contains("write_manifest_record", context.ExecutedOperationKeys);
        Assert.Contains("authorize_signed_download", context.ExecutedOperationKeys);
        Assert.NotNull(context.ExportJobId);
        Assert.NotNull(context.ChecksumValue);
        Assert.NotNull(context.FileArtifactId);
        Assert.NotNull(context.AuthorizationKey);
        Assert.NotNull(context.OutputProp);
        Assert.StartsWith("auth-ref:", context.AuthorizationKey);
        Assert.Contains("authorization_key", context.OutputProp);
    }

    [Fact]
    public async Task ExecutePolicyAsync_WithoutBundleHandler_FileStoragePolicySteps_FailClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var policy = BuildFileStoragePolicy(accessPort: true);
        var context = new ExternalPortExecutionContext
        {
            RequestPayload = BuildPayload("job-002", "system", "2026-06", "json")
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecutePolicyAsync(policy, context));
        Assert.Contains("EXTERNAL_PORT_POLICY_OPERATION_UNSUPPORTED", ex.Message);
    }

    [Fact]
    public async Task RecordExportJob_UsesIdempotencyKeyFromPayload()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var context = new ExternalPortExecutionContext
        {
            RequestPayload = BuildPayload("job-idem", "user1", "2026-06", "csv", idempotencyKey: "idem-key-123"),
            PortRecord = BuildPortRecord()
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 6, "record_export_job", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        await executor.ExecuteAsync(step, context);

        Assert.Equal("idem-key-123", repo.LastCommand?.IdempotencyKey);
    }

    [Fact]
    public async Task ComputeChecksum_ProduceSha256PrefixedValue()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = Guid.NewGuid(),
            HttpResponse = new ExternalPortHttpResponse(200, "test-response-body")
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 7, "compute_checksum", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        await executor.ExecuteAsync(step, context);

        Assert.NotNull(context.ChecksumValue);
        Assert.StartsWith("sha256:", context.ChecksumValue);
    }

    [Fact]
    public async Task RecordFileArtifact_UsesEnvVarReferenceNotPlaintextUrl()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var portRecord = BuildPortRecord(referenceKey: "vault:ref:file_storage_credential");
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = Guid.NewGuid(),
            ChecksumValue = "sha256:abc123",
            PortRecord = portRecord,
            RequestPayload = BuildPayload("job-003", "system", "2026-06", "pdf")
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 8, "record_file_artifact", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        await executor.ExecuteAsync(step, context);

        Assert.NotNull(repo.LastArtifactCommand);
        Assert.Equal("vault:ref:file_storage_credential", repo.LastArtifactCommand!.StorageRef);
        Assert.DoesNotContain("http", repo.LastArtifactCommand.StorageRef, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("s3", repo.LastArtifactCommand.StorageRef, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordFileArtifact_DoesNotGenerateStorageLocationFallback()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = Guid.NewGuid(),
            ChecksumValue = "sha256:abc123",
            PortRecord = BuildPortRecord(referenceKey: null),
            RequestPayload = BuildPayload("job-005", "system", "2026-06", "pdf")
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 8, "record_file_artifact", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("STORAGE_REF_REQUIRED_FROM_PORT_RECORD", ex.Message);
    }

    [Fact]
    public async Task RecordFileArtifact_TreatsLocationAsOpaquePortCredentialReference()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var portRecord = BuildPortRecord(referenceKey: "opaque-ref:some-arbitrary-identifier");
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = Guid.NewGuid(),
            ChecksumValue = "sha256:abc123",
            PortRecord = portRecord,
            RequestPayload = BuildPayload("job-006", "system", "2026-06", "csv")
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 8, "record_file_artifact", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        await executor.ExecuteAsync(step, context);

        Assert.NotNull(repo.LastArtifactCommand);
        Assert.Equal("opaque-ref:some-arbitrary-identifier", repo.LastArtifactCommand!.StorageRef);
    }

    [Fact]
    public async Task ComputeChecksum_WithoutArtifactInput_FailsClose()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = Guid.NewGuid()
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 7, "compute_checksum", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("CHECKSUM_INPUT_REQUIRED", ex.Message);
    }

    [Fact]
    public async Task ComputeChecksum_DoesNotHashExportJobIdFallback()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var exportJobId = Guid.NewGuid();
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = exportJobId,
            HttpResponse = null
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 7, "compute_checksum", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("CHECKSUM_INPUT_REQUIRED", ex.Message);
        Assert.Null(context.ChecksumValue);
    }

    [Fact]
    public async Task WriteManifestRecord_WithoutFileArtifactId_FailsClose()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = Guid.NewGuid(),
            ChecksumValue = "sha256:abc123",
            FileArtifactId = null
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 9, "write_manifest_record", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(step, context));
        Assert.Contains("FILE_ARTIFACT_ID_REQUIRED_FOR_MANIFEST", ex.Message);
    }

    [Fact]
    public async Task AuthorizeSignedDownload_SetsAuthorizationKeyReferenceNotSignedUrl()
    {
        var repo = new FakeFileStorageRepository();
        var executor = BuildExecutorWithFileStorage(repo);
        var context = new ExternalPortExecutionContext
        {
            ExportJobId = Guid.NewGuid(),
            FileArtifactId = Guid.NewGuid(),
            RequestPayload = BuildPayload("job-004", "system", "2026-06", "pdf")
        };

        var step = new ExternalPortPolicyStep(Guid.NewGuid(), Guid.NewGuid(), 10, "authorize_signed_download", new Dictionary<string, string>(), true);
        context.Policy = BuildPolicy("access_port", "file_storage_bundle");
        await executor.ExecuteAsync(step, context);

        Assert.NotNull(context.AuthorizationKey);
        Assert.StartsWith("auth-ref:", context.AuthorizationKey);
        Assert.DoesNotContain("https://", context.AuthorizationKey, StringComparison.Ordinal);
        Assert.NotNull(context.OutputProp);
        Assert.Contains("authorization_key", context.OutputProp);
        Assert.DoesNotContain("signed_url", context.OutputProp, StringComparison.Ordinal);
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
    public void FileStorageContracts_DoNotContainPlaintextCredential()
    {
        var contractSource = File.ReadAllText(FindRepositoryFile("backend/schema/FileStorageContracts.cs"));
        Assert.DoesNotContain("access_key", contractSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret_key", contractSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket_name", contractSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint_url", contractSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", contractSource, StringComparison.OrdinalIgnoreCase);
    }

    private static ExternalPortPolicy BuildFileStoragePolicy(bool accessPort) =>
        new(Guid.NewGuid(), "file_storage_test_policy", accessPort ? "access_port" : "response_port", "file_storage_bundle",
        [
            NewStep(6, "record_export_job"),
            NewStep(7, "compute_checksum"),
            NewStep(8, "record_file_artifact"),
            NewStep(9, "write_manifest_record"),
            NewStep(10, "authorize_signed_download")
        ], true);

    private static ExternalPortPolicy BuildPolicy(string portKind, string bundle) =>
        new(Guid.NewGuid(), "test_policy", portKind, bundle, Array.Empty<ExternalPortPolicyStep>(), true);

    private static ExternalPortPolicyStep NewStep(int order, string operationKey) =>
        new(Guid.NewGuid(), Guid.NewGuid(), order, operationKey, new Dictionary<string, string>(), true);

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

    private sealed class FakeFileStorageRepository : IFileStorageRepository
    {
        private Guid _nextJobId = Guid.NewGuid();
        private Guid _nextArtifactId = Guid.NewGuid();
        private Guid _nextChecksumId = Guid.NewGuid();
        private Guid _nextManifestId = Guid.NewGuid();
        private Guid _nextAuthId = Guid.NewGuid();

        public RecordExportJobCommand? LastCommand { get; private set; }
        public RecordFileArtifactCommand? LastArtifactCommand { get; private set; }

        public Task<Guid> RecordExportJobAsync(RecordExportJobCommand command, CancellationToken ct = default)
        {
            LastCommand = command;
            return Task.FromResult(_nextJobId);
        }

        public Task UpdateExportJobStatusAsync(Guid exportJobId, string status, string? failureCode = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<FileChecksumRecord> RecordChecksumAsync(RecordChecksumCommand command, CancellationToken ct = default) =>
            Task.FromResult(new FileChecksumRecord(_nextChecksumId, command.ExportJobId, command.FileArtifactId, command.Algorithm, command.ChecksumValue, command.VerifiedAt, command.VerificationStatus));

        public Task<FileArtifactRecord> RecordFileArtifactAsync(RecordFileArtifactCommand command, CancellationToken ct = default)
        {
            LastArtifactCommand = command;
            return Task.FromResult(new FileArtifactRecord(_nextArtifactId, command.ExportJobId, command.FileName, command.FileType, command.StorageRef, command.ByteSize, null));
        }

        public Task<ExportManifestRecord> WriteManifestRecordAsync(WriteManifestCommand command, CancellationToken ct = default) =>
            Task.FromResult(new ExportManifestRecord(_nextManifestId, command.ExportJobId, command.ManifestVersion, command.GeneratedAt, command.GeneratedBy, command.Period, command.ExportFormat, command.Checksum, command.FileArtifactIds));

        public Task<SignedDownloadAuthorizationRecord> AuthorizeSignedDownloadAsync(AuthorizeSignedDownloadCommand command, CancellationToken ct = default) =>
            Task.FromResult(new SignedDownloadAuthorizationRecord(_nextAuthId, command.FileArtifactId, command.AuthorizedBy, command.AuthorizedAt, command.ExpiresAt, command.AuthorizationKey, "active"));
    }
}
