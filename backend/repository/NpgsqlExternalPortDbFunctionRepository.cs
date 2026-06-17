using System.Text.Json;
using Npgsql;
using Topolactor.Runtime;

namespace Topolactor.Repository;

/// <summary>
/// Abstract function boundary for file_storage_bundle domain record operations.
/// Executes named topology.fs_* PostgreSQL functions; maps context-derived parameters
/// and stores results back into ExternalPortExecutionContext.
/// No provider-specific object storage client is introduced here.
/// </summary>
public sealed class NpgsqlExternalPortDbFunctionRepository : IExternalPortDbFunctionRepository
{
    private readonly string _connectionString;

    public NpgsqlExternalPortDbFunctionRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public Task ExecuteAsync(
        string functionName,
        IReadOnlyDictionary<string, string> stepConfig,
        ExternalPortExecutionContext context,
        CancellationToken ct = default) =>
        functionName switch
        {
            "topology.fs_record_export_job" => ExecuteRecordExportJobAsync(stepConfig, context, ct),
            "topology.fs_record_file_artifact" => ExecuteRecordFileArtifactAsync(stepConfig, context, ct),
            "topology.fs_write_manifest_record" => ExecuteWriteManifestRecordAsync(stepConfig, context, ct),
            "topology.fs_authorize_signed_download" => ExecuteAuthorizeSignedDownloadAsync(stepConfig, context, ct),
            _ => throw new InvalidOperationException($"EXTERNAL_PORT_DB_FUNCTION_UNKNOWN: {functionName}")
        };

    private async Task ExecuteRecordExportJobAsync(
        IReadOnlyDictionary<string, string> stepConfig,
        ExternalPortExecutionContext context,
        CancellationToken ct)
    {
        var idempotencyKey = ExtractString(context.RequestPayload, "idempotency_key")
            ?? ExtractString(context.RequestPayload, "export_job_id")
            ?? Guid.NewGuid().ToString("N");
        var requestedBy = ExtractString(context.RequestPayload, "requested_by") ?? "system";
        var exportFormat = ExtractString(context.RequestPayload, "export_format");
        var period = ExtractString(context.RequestPayload, "period");
        var portId = context.PortRecord?.PortId;
        var portKind = context.PortRecord?.PortKind ?? context.PortKind ?? "access_port";
        var requiredByBundle = context.RequiredByBundle ?? "file_storage_bundle";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT topology.fs_record_export_job(@p_idempotency_key, @p_required_by_bundle, @p_port_id, @p_port_kind, @p_requested_by, @p_export_format, @p_period)";
        cmd.Parameters.AddWithValue("p_idempotency_key", idempotencyKey);
        cmd.Parameters.AddWithValue("p_required_by_bundle", requiredByBundle);
        cmd.Parameters.AddWithValue("p_port_id", (object?)portId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("p_port_kind", portKind);
        cmd.Parameters.AddWithValue("p_requested_by", requestedBy);
        cmd.Parameters.AddWithValue("p_export_format", (object?)exportFormat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("p_period", (object?)period ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is Guid exportJobId)
            context.ExportJobId = exportJobId;
    }

    private async Task ExecuteRecordFileArtifactAsync(
        IReadOnlyDictionary<string, string> stepConfig,
        ExternalPortExecutionContext context,
        CancellationToken ct)
    {
        if (context.ExportJobId is null)
            throw new InvalidOperationException("EXPORT_JOB_ID_REQUIRED");
        if (context.ChecksumValue is null)
            throw new InvalidOperationException("CHECKSUM_VALUE_REQUIRED");

        var fileName = ExtractString(context.RequestPayload, "file_name")
            ?? $"export_{context.ExportJobId:N}.dat";
        var fileType = ExtractString(context.RequestPayload, "file_type") ?? "json";
        var storageRef = context.PortRecord?.ReferenceKey
            ?? throw new InvalidOperationException("STORAGE_REF_REQUIRED_FROM_PORT_RECORD");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT topology.fs_record_file_artifact(@p_export_job_id, @p_file_name, @p_file_type, @p_storage_ref, @p_checksum_value)";
        cmd.Parameters.AddWithValue("p_export_job_id", context.ExportJobId.Value);
        cmd.Parameters.AddWithValue("p_file_name", fileName);
        cmd.Parameters.AddWithValue("p_file_type", fileType);
        cmd.Parameters.AddWithValue("p_storage_ref", storageRef);
        cmd.Parameters.AddWithValue("p_checksum_value", context.ChecksumValue);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is Guid fileArtifactId)
            context.FileArtifactId = fileArtifactId;
    }

    private async Task ExecuteWriteManifestRecordAsync(
        IReadOnlyDictionary<string, string> stepConfig,
        ExternalPortExecutionContext context,
        CancellationToken ct)
    {
        if (context.ExportJobId is null)
            throw new InvalidOperationException("EXPORT_JOB_ID_REQUIRED");
        if (context.FileArtifactId is null)
            throw new InvalidOperationException("FILE_ARTIFACT_ID_REQUIRED_FOR_MANIFEST");

        var requestedBy = ExtractString(context.RequestPayload, "requested_by") ?? "system";
        var period = ExtractString(context.RequestPayload, "period");
        var exportFormat = ExtractString(context.RequestPayload, "export_format");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT topology.fs_write_manifest_record(@p_export_job_id, @p_file_artifact_id, @p_requested_by, @p_period, @p_export_format, @p_checksum_value)";
        cmd.Parameters.AddWithValue("p_export_job_id", context.ExportJobId.Value);
        cmd.Parameters.AddWithValue("p_file_artifact_id", context.FileArtifactId.Value);
        cmd.Parameters.AddWithValue("p_requested_by", requestedBy);
        cmd.Parameters.AddWithValue("p_period", (object?)period ?? DBNull.Value);
        cmd.Parameters.AddWithValue("p_export_format", (object?)exportFormat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("p_checksum_value", (object?)context.ChecksumValue ?? DBNull.Value);

        await cmd.ExecuteScalarAsync(ct);
    }

    private async Task ExecuteAuthorizeSignedDownloadAsync(
        IReadOnlyDictionary<string, string> stepConfig,
        ExternalPortExecutionContext context,
        CancellationToken ct)
    {
        if (context.FileArtifactId is null)
            throw new InvalidOperationException("FILE_ARTIFACT_ID_REQUIRED");

        var authorizedBy = ExtractString(context.RequestPayload, "requested_by") ?? "system";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT topology.fs_authorize_signed_download(@p_file_artifact_id, @p_authorized_by)";
        cmd.Parameters.AddWithValue("p_file_artifact_id", context.FileArtifactId.Value);
        cmd.Parameters.AddWithValue("p_authorized_by", authorizedBy);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is string authKey)
        {
            context.AuthorizationKey = authKey;
            // authorization_key is a non-guessable reference identifier; actual signed URL is runtime-secret-store concern
            context.OutputProp = JsonSerializer.Serialize(new { authorization_key = authKey });
        }
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
}
