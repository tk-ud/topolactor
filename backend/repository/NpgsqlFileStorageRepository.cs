using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of IFileStorageRepository.
/// Writes export_job / file_artifact / checksum / manifest / signed download authorization
/// metadata records. Plaintext credentials, bucket names, endpoints, and actual signed URLs
/// are prohibited from all columns; storage_ref and authorization_key are reference identifiers only.
/// </summary>
public sealed class NpgsqlFileStorageRepository : IFileStorageRepository
{
    private readonly ILogger<NpgsqlFileStorageRepository> _logger;
    private readonly string _connectionString;

    public NpgsqlFileStorageRepository(
        ILogger<NpgsqlFileStorageRepository> logger,
        string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<Guid> RecordExportJobAsync(RecordExportJobCommand command, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.export_jobs
                (export_job_id, port_id, port_kind, requested_by, requested_at,
                 period, target_scope, export_format, status, idempotency_key)
            VALUES
                (@export_job_id, @port_id, @port_kind, @requested_by, @requested_at,
                 @period, @target_scope, @export_format, @status, @idempotency_key)
            ON CONFLICT (idempotency_key) DO UPDATE
                SET status     = EXCLUDED.status,
                    updated_at = now()
            RETURNING export_job_id
            """;

        cmd.Parameters.AddWithValue("export_job_id", command.ExportJobId);
        cmd.Parameters.AddWithValue("port_id", command.PortId.HasValue ? (object)command.PortId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("port_kind", command.PortKind);
        cmd.Parameters.AddWithValue("requested_by", command.RequestedBy);
        cmd.Parameters.AddWithValue("requested_at", command.RequestedAt);
        cmd.Parameters.AddWithValue("period", command.Period is not null ? (object)command.Period : DBNull.Value);
        cmd.Parameters.AddWithValue("target_scope", command.TargetScope is not null ? (object)command.TargetScope : DBNull.Value);
        cmd.Parameters.AddWithValue("export_format", command.ExportFormat is not null ? (object)command.ExportFormat : DBNull.Value);
        cmd.Parameters.AddWithValue("status", command.Status);
        cmd.Parameters.AddWithValue("idempotency_key", command.IdempotencyKey);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (Guid)result!;
    }

    public async Task UpdateExportJobStatusAsync(Guid exportJobId, string status, string? failureCode = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = status == "completed"
            ? "UPDATE topology.export_jobs SET status = @status, completed_at = now(), updated_at = now() WHERE export_job_id = @id"
            : "UPDATE topology.export_jobs SET status = @status, failed_at = now(), failure_code = @failure_code, updated_at = now() WHERE export_job_id = @id";

        cmd.Parameters.AddWithValue("id", exportJobId);
        cmd.Parameters.AddWithValue("status", status);
        if (status != "completed")
            cmd.Parameters.AddWithValue("failure_code", failureCode is not null ? (object)failureCode : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<FileChecksumRecord> RecordChecksumAsync(RecordChecksumCommand command, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.file_checksum_records
                (export_job_id, file_artifact_id, algorithm, checksum_value, verified_at, verification_status)
            VALUES
                (@export_job_id, @file_artifact_id, @algorithm, @checksum_value, @verified_at, @verification_status)
            RETURNING checksum_record_id, export_job_id, file_artifact_id, algorithm, checksum_value, verified_at, verification_status
            """;

        cmd.Parameters.AddWithValue("export_job_id", command.ExportJobId);
        cmd.Parameters.AddWithValue("file_artifact_id", command.FileArtifactId);
        cmd.Parameters.AddWithValue("algorithm", command.Algorithm);
        cmd.Parameters.AddWithValue("checksum_value", command.ChecksumValue);
        cmd.Parameters.AddWithValue("verified_at", command.VerifiedAt);
        cmd.Parameters.AddWithValue("verification_status", command.VerificationStatus);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new FileChecksumRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetString(6));
    }

    public async Task<FileArtifactRecord> RecordFileArtifactAsync(RecordFileArtifactCommand command, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using var artifactCmd = conn.CreateCommand();
        artifactCmd.Transaction = tx;
        artifactCmd.CommandText = """
            INSERT INTO topology.file_artifacts
                (export_job_id, file_name, file_type, storage_ref, byte_size, checksum_value)
            VALUES
                (@export_job_id, @file_name, @file_type, @storage_ref, @byte_size, @checksum_value)
            RETURNING file_artifact_id, export_job_id, file_name, file_type, storage_ref, byte_size, checksum_record_id
            """;

        artifactCmd.Parameters.AddWithValue("export_job_id", command.ExportJobId);
        artifactCmd.Parameters.AddWithValue("file_name", command.FileName);
        artifactCmd.Parameters.AddWithValue("file_type", command.FileType);
        artifactCmd.Parameters.AddWithValue("storage_ref", command.StorageRef);
        artifactCmd.Parameters.AddWithValue("byte_size", command.ByteSize.HasValue ? (object)command.ByteSize.Value : DBNull.Value);
        artifactCmd.Parameters.AddWithValue("checksum_value", command.ChecksumValue);

        Guid fileArtifactId;
        long? byteSize;
        Guid? checksumRecordId;
        string fileName, fileType, storageRef;

        await using (var artifactReader = await artifactCmd.ExecuteReaderAsync(ct))
        {
            await artifactReader.ReadAsync(ct);
            fileArtifactId = artifactReader.GetGuid(0);
            _ = artifactReader.GetGuid(1);
            fileName = artifactReader.GetString(2);
            fileType = artifactReader.GetString(3);
            storageRef = artifactReader.GetString(4);
            byteSize = artifactReader.IsDBNull(5) ? null : artifactReader.GetInt64(5);
            checksumRecordId = artifactReader.IsDBNull(6) ? null : artifactReader.GetGuid(6);
        }

        await using var checksumCmd = conn.CreateCommand();
        checksumCmd.Transaction = tx;
        checksumCmd.CommandText = """
            INSERT INTO topology.file_checksum_records
                (export_job_id, file_artifact_id, algorithm, checksum_value, verified_at, verification_status)
            VALUES
                (@export_job_id, @file_artifact_id, 'sha256', @checksum_value, now(), 'verified')
            RETURNING checksum_record_id
            """;

        checksumCmd.Parameters.AddWithValue("export_job_id", command.ExportJobId);
        checksumCmd.Parameters.AddWithValue("file_artifact_id", fileArtifactId);
        checksumCmd.Parameters.AddWithValue("checksum_value", command.ChecksumValue);

        var checksumRecordIdNew = (Guid)(await checksumCmd.ExecuteScalarAsync(ct))!;

        await using var updateCmd = conn.CreateCommand();
        updateCmd.Transaction = tx;
        updateCmd.CommandText = "UPDATE topology.file_artifacts SET checksum_record_id = @cid WHERE file_artifact_id = @aid";
        updateCmd.Parameters.AddWithValue("cid", checksumRecordIdNew);
        updateCmd.Parameters.AddWithValue("aid", fileArtifactId);
        await updateCmd.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);

        return new FileArtifactRecord(fileArtifactId, command.ExportJobId, fileName, fileType, storageRef, byteSize, checksumRecordIdNew);
    }

    public async Task<ExportManifestRecord> WriteManifestRecordAsync(WriteManifestCommand command, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var fileArtifactIdsJson = System.Text.Json.JsonSerializer.Serialize(command.FileArtifactIds);
        var manifestJsonb = System.Text.Json.JsonSerializer.Serialize(new
        {
            export_job_id = command.ExportJobId,
            manifest_version = command.ManifestVersion,
            generated_by = command.GeneratedBy,
            period = command.Period,
            export_format = command.ExportFormat,
            checksum = command.Checksum,
            file_artifact_ids = command.FileArtifactIds
        });

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.export_manifests
                (export_job_id, manifest_version, generated_at, generated_by,
                 period, export_format, checksum, file_artifact_ids, manifest_jsonb)
            VALUES
                (@export_job_id, @manifest_version, @generated_at, @generated_by,
                 @period, @export_format, @checksum, @file_artifact_ids::jsonb, @manifest_jsonb::jsonb)
            ON CONFLICT (export_job_id) DO UPDATE
                SET manifest_version = EXCLUDED.manifest_version,
                    checksum         = EXCLUDED.checksum,
                    manifest_jsonb   = EXCLUDED.manifest_jsonb,
                    updated_at       = now()
            RETURNING manifest_id, export_job_id, manifest_version, generated_at, generated_by,
                      period, export_format, checksum, file_artifact_ids
            """;

        cmd.Parameters.AddWithValue("export_job_id", command.ExportJobId);
        cmd.Parameters.AddWithValue("manifest_version", command.ManifestVersion);
        cmd.Parameters.AddWithValue("generated_at", command.GeneratedAt);
        cmd.Parameters.AddWithValue("generated_by", command.GeneratedBy);
        cmd.Parameters.AddWithValue("period", command.Period is not null ? (object)command.Period : DBNull.Value);
        cmd.Parameters.AddWithValue("export_format", command.ExportFormat is not null ? (object)command.ExportFormat : DBNull.Value);
        cmd.Parameters.AddWithValue("checksum", command.Checksum is not null ? (object)command.Checksum : DBNull.Value);
        cmd.Parameters.AddWithValue("file_artifact_ids", fileArtifactIdsJson);
        cmd.Parameters.AddWithValue("manifest_jsonb", manifestJsonb);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        var artifactIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(
            reader.GetString(8)) ?? new List<Guid>();

        return new ExportManifestRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            artifactIds);
    }

    public async Task<SignedDownloadAuthorizationRecord> AuthorizeSignedDownloadAsync(AuthorizeSignedDownloadCommand command, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.signed_download_authorizations
                (file_artifact_id, authorized_by, authorized_at, expires_at, authorization_key, status)
            VALUES
                (@file_artifact_id, @authorized_by, @authorized_at, @expires_at, @authorization_key, 'active')
            ON CONFLICT (authorization_key) DO UPDATE
                SET expires_at = EXCLUDED.expires_at,
                    updated_at = now()
            RETURNING authorization_id, file_artifact_id, authorized_by, authorized_at, expires_at, authorization_key, status
            """;

        cmd.Parameters.AddWithValue("file_artifact_id", command.FileArtifactId);
        cmd.Parameters.AddWithValue("authorized_by", command.AuthorizedBy);
        cmd.Parameters.AddWithValue("authorized_at", command.AuthorizedAt);
        cmd.Parameters.AddWithValue("expires_at", command.ExpiresAt);
        cmd.Parameters.AddWithValue("authorization_key", command.AuthorizationKey);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new SignedDownloadAuthorizationRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetString(5),
            reader.GetString(6));
    }
}
