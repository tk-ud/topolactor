using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Consumer bundle step handler for file_storage_bundle operation_keys.
/// Registered via IExternalPortBundleStepHandler extension contract; not injected directly
/// into ExternalPortPolicyStepExecutor.
/// Operation_key definitions live in docs/design/runtime-bundle-file-storage-ssot.yaml#operation_key_surface.
/// </summary>
public sealed class FileStorageBundleStepHandler : IExternalPortBundleStepHandler
{
    private readonly IFileStorageRepository _repository;

    private static readonly IReadOnlySet<string> _supportedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "record_export_job",
        "compute_checksum",
        "record_file_artifact",
        "write_manifest_record",
        "authorize_signed_download"
    };

    public FileStorageBundleStepHandler(IFileStorageRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public IReadOnlySet<string> SupportedOperationKeys => _supportedKeys;

    public Task ExecuteAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct = default) =>
        step.OperationKey switch
        {
            "record_export_job" => RecordExportJobAsync(step, context, ct),
            "compute_checksum" => ComputeChecksumAsync(step, context, ct),
            "record_file_artifact" => RecordFileArtifactAsync(step, context, ct),
            "write_manifest_record" => WriteManifestRecordAsync(step, context, ct),
            "authorize_signed_download" => AuthorizeSignedDownloadAsync(step, context, ct),
            _ => throw new InvalidOperationException("EXTERNAL_PORT_POLICY_OPERATION_UNSUPPORTED")
        };

    private async Task RecordExportJobAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct)
    {
        var exportJobId = ExtractGuid(context.RequestPayload, "export_job_id") ?? Guid.NewGuid();
        var idempotencyKey = ExtractString(context.RequestPayload, "idempotency_key") ?? exportJobId.ToString("N");
        var portKind = context.PortRecord?.PortKind ?? context.PortKind ?? "access_port";
        var requestedBy = ExtractString(context.RequestPayload, "requested_by") ?? "system";

        var command = new RecordExportJobCommand(
            exportJobId,
            context.PortRecord?.PortId,
            portKind,
            requestedBy,
            DateTimeOffset.UtcNow,
            ExtractString(context.RequestPayload, "period"),
            ExtractString(context.RequestPayload, "target_scope"),
            ExtractString(context.RequestPayload, "export_format"),
            "in_progress",
            idempotencyKey);

        context.ExportJobId = await _repository.RecordExportJobAsync(command, ct);
        context.MarkExecuted(step.OperationKey);
    }

    private static Task ComputeChecksumAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct)
    {
        if (context.ExportJobId is null)
            throw new InvalidOperationException("EXPORT_JOB_ID_REQUIRED");

        var input = context.HttpResponse?.Body ?? context.ExportJobId.Value.ToString("N");
        context.ChecksumValue = ComputeSha256(input);
        context.MarkExecuted(step.OperationKey);
        return Task.CompletedTask;
    }

    private async Task RecordFileArtifactAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct)
    {
        if (context.ExportJobId is null)
            throw new InvalidOperationException("EXPORT_JOB_ID_REQUIRED");
        if (context.ChecksumValue is null)
            throw new InvalidOperationException("CHECKSUM_VALUE_REQUIRED");

        var fileName = ExtractString(context.RequestPayload, "file_name") ?? $"export_{context.ExportJobId:N}.dat";
        var fileType = ExtractString(context.RequestPayload, "file_type") ?? "json";
        // storage_ref is env-var reference identifier only, NOT plaintext storage URL/path
        var storageRef = context.PortRecord?.ReferenceKey ?? "env:FILE_STORAGE_ENDPOINT_REF";

        var artifact = await _repository.RecordFileArtifactAsync(
            new RecordFileArtifactCommand(
                context.ExportJobId.Value,
                fileName,
                fileType,
                storageRef,
                null,
                context.ChecksumValue),
            ct);
        context.FileArtifactId = artifact.FileArtifactId;
        context.MarkExecuted(step.OperationKey);
    }

    private async Task WriteManifestRecordAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct)
    {
        if (context.ExportJobId is null)
            throw new InvalidOperationException("EXPORT_JOB_ID_REQUIRED");

        var fileArtifactIds = context.FileArtifactId.HasValue
            ? new[] { context.FileArtifactId.Value }
            : Array.Empty<Guid>();

        await _repository.WriteManifestRecordAsync(
            new WriteManifestCommand(
                context.ExportJobId.Value,
                "1.0",
                DateTimeOffset.UtcNow,
                ExtractString(context.RequestPayload, "requested_by") ?? "system",
                ExtractString(context.RequestPayload, "period"),
                ExtractString(context.RequestPayload, "export_format"),
                context.ChecksumValue,
                fileArtifactIds),
            ct);
        context.MarkExecuted(step.OperationKey);
    }

    private async Task AuthorizeSignedDownloadAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct)
    {
        if (context.FileArtifactId is null)
            throw new InvalidOperationException("FILE_ARTIFACT_ID_REQUIRED");

        // authorization_key is a non-guessable reference identifier, NOT the actual signed URL
        var authorizationKey = $"auth-ref:{context.FileArtifactId:N}:{Guid.NewGuid():N}";
        var authorizedBy = ExtractString(context.RequestPayload, "requested_by") ?? "system";

        var authorization = await _repository.AuthorizeSignedDownloadAsync(
            new AuthorizeSignedDownloadCommand(
                context.FileArtifactId.Value,
                authorizedBy,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1),
                authorizationKey),
            ct);
        context.AuthorizationKey = authorization.AuthorizationKey;
        // OutputProp carries authorization_key reference only; actual signed URL is runtime-secret-store concern
        context.OutputProp = JsonSerializer.Serialize(new
        {
            authorization_key = authorization.AuthorizationKey,
            expires_at = authorization.ExpiresAt
        });
        context.MarkExecuted(step.OperationKey);
    }

    private static string? ExtractString(JsonElement? payload, string key)
    {
        if (payload is null) return null;
        if (payload.Value.TryGetProperty("dispatch_payload", out var dp) && dp.TryGetProperty(key, out var v1) && v1.ValueKind == JsonValueKind.String)
            return v1.GetString();
        if (payload.Value.TryGetProperty(key, out var v2) && v2.ValueKind == JsonValueKind.String)
            return v2.GetString();
        return null;
    }

    private static Guid? ExtractGuid(JsonElement? payload, string key)
    {
        var str = ExtractString(payload, key);
        return str is not null && Guid.TryParse(str, out var id) ? id : null;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
