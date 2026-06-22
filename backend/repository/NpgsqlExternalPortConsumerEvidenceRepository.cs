using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Topolactor.Runtime;

namespace Topolactor.Repository;

public sealed class NpgsqlExternalPortConsumerEvidenceRepository : IExternalPortConsumerEvidenceRepository
{
    // Fixed SQL below is a DDL shape adapter only: tableRef authority is still data-defined
    // and must be present in active topology.physical_table_manifest_bindings before use.
    // This adapter does not select provider_kind or required_by_bundle runtime/client behavior.
    private static readonly IReadOnlySet<string> AllowedTableRefs = new HashSet<string>(StringComparer.Ordinal)
    {
        "topology.email_delivery_evidence",
        "topology.webhook_intake_snapshots",
        "topology.signature_verification_evidence",
        "topology.payment_state_projections",
        "topology.scheduler_external_event_evidence",
        "topology.audit_approval_evidence",
        "topology.audit_notification_evidence",
        "topology.sftp_transfer_log"
    };

    private readonly string _connectionString;

    public NpgsqlExternalPortConsumerEvidenceRepository(string connectionString) => _connectionString = connectionString;

    public async Task AppendEvidenceAsync(
        string tableRef,
        string eventType,
        string? entityId,
        string? requiredByBundle,
        ExternalPortExecutionContext context,
        IReadOnlyDictionary<string, string> stepConfig,
        CancellationToken ct = default)
    {
        EnsureAllowed(tableRef);
        var evidenceJson = BuildEvidenceJson(eventType, entityId, requiredByBundle, context, stepConfig);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await ValidateTableRefBoundToActiveManifestAsync(conn, tableRef, ct);
        SftpTransferSourceRefs? sftpRefs = tableRef == "topology.sftp_transfer_log"
            ? await ResolveSftpTransferSourceRefsAsync(conn, eventType, context, ct)
            : null;
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = InsertSql(tableRef);
        AddCommonParameters(cmd, eventType, entityId, requiredByBundle, context, stepConfig, evidenceJson, sftpRefs);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ExternalPortConsumerEvidenceProjection>> LoadProjectionAsync(
        string tableRef,
        string? requiredByBundle,
        string? entityId,
        int limit = 20,
        CancellationToken ct = default)
    {
        EnsureAllowed(tableRef);
        if (string.IsNullOrWhiteSpace(entityId))
            throw new InvalidOperationException("EXTERNAL_PORT_CONSUMER_EVIDENCE_ENTITY_ID_REQUIRED");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await ValidateTableRefBoundToActiveManifestAsync(conn, tableRef, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectSql(tableRef);
        cmd.Parameters.AddWithValue("requiredByBundle", (object?)requiredByBundle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("entityId", (object?)entityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 100));

        var rows = new List<ExternalPortConsumerEvidenceProjection>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ExternalPortConsumerEvidenceProjection(
                tableRef,
                reader.GetString(reader.GetOrdinal("event_type")),
                reader.IsDBNull(reader.GetOrdinal("entity_id")) ? null : reader.GetString(reader.GetOrdinal("entity_id")),
                reader.IsDBNull(reader.GetOrdinal("status")) ? null : reader.GetString(reader.GetOrdinal("status")),
                ParseEvidence(reader.GetString(reader.GetOrdinal("evidence_json")))));
        }
        return rows;
    }

    private static void EnsureAllowed(string tableRef)
    {
        if (!AllowedTableRefs.Contains(tableRef))
            throw new InvalidOperationException("EXTERNAL_PORT_CONSUMER_EVIDENCE_TABLE_REF_UNSUPPORTED");
    }

    private static string InsertSql(string tableRef) => tableRef switch
    {
        "topology.email_delivery_evidence" => "INSERT INTO topology.email_delivery_evidence (email_draft_id, event_type, delivery_status, evidence_json) VALUES (NULL, @eventType, @statusValue, @evidenceJson)",
        "topology.webhook_intake_snapshots" => "INSERT INTO topology.webhook_intake_snapshots (required_by_bundle, route_key, hook_path, payload_hash, scheduler_event_ref, evidence_json) VALUES (@requiredByBundle, @routeKey, @hookPath, @payloadHash, @entityId, @evidenceJson)",
        "topology.signature_verification_evidence" => "INSERT INTO topology.signature_verification_evidence (required_by_bundle, verification_status, evidence_json) VALUES (@requiredByBundle, @statusValue, @evidenceJson)",
        "topology.payment_state_projections" => "INSERT INTO topology.payment_state_projections (payment_state, evidence_json) VALUES (@statusValue, @evidenceJson)",
        "topology.scheduler_external_event_evidence" => "INSERT INTO topology.scheduler_external_event_evidence (required_by_bundle, trigger_ref, scheduler_status, evidence_json) VALUES (@requiredByBundle, @entityId, @statusValue, @evidenceJson)",
        "topology.audit_approval_evidence" => "INSERT INTO topology.audit_approval_evidence (event_type, approval_status, evidence_json) VALUES (@eventType, @statusValue, @evidenceJson)",
        "topology.audit_notification_evidence" => "INSERT INTO topology.audit_notification_evidence (notification_status, response_port_ref, evidence_json) VALUES (@statusValue, @entityId, @evidenceJson)",
        "topology.sftp_transfer_log" => "INSERT INTO topology.sftp_transfer_log (export_job_id, file_artifact_id, manifest_id, checksum_record_id, transfer_status, failure_reason, checksum_before, checksum_after, manifest_ref, response_port_ref, retry_evidence_json, evidence_json) VALUES (@exportJobId, @fileArtifactId, @manifestId, @checksumRecordId, @statusValue, @failureReason, @checksumBefore, @checksumAfter, @manifestRef, @entityId, @retryEvidenceJson, @evidenceJson)",
        _ => throw new InvalidOperationException("EXTERNAL_PORT_CONSUMER_EVIDENCE_TABLE_REF_UNSUPPORTED")
    };

    private static string SelectSql(string tableRef) => tableRef switch
    {
        "topology.email_delivery_evidence" => "SELECT event_type, evidence_json->>'dispatch_id' AS entity_id, delivery_status AS status, evidence_json::text FROM topology.email_delivery_evidence WHERE evidence_json->>'dispatch_id' = @entityId ORDER BY recorded_at DESC LIMIT @limit",
        "topology.webhook_intake_snapshots" => "SELECT 'webhook_received' AS event_type, evidence_json->>'dispatch_id' AS entity_id, 'received' AS status, evidence_json::text FROM topology.webhook_intake_snapshots WHERE evidence_json->>'dispatch_id' = @entityId AND (@requiredByBundle IS NULL OR required_by_bundle = @requiredByBundle) ORDER BY received_at DESC LIMIT @limit",
        "topology.signature_verification_evidence" => "SELECT 'signature_verification' AS event_type, evidence_json->>'dispatch_id' AS entity_id, verification_status AS status, evidence_json::text FROM topology.signature_verification_evidence WHERE evidence_json->>'dispatch_id' = @entityId AND (@requiredByBundle IS NULL OR required_by_bundle = @requiredByBundle) ORDER BY recorded_at DESC LIMIT @limit",
        "topology.payment_state_projections" => "SELECT 'payment_state_projected' AS event_type, evidence_json->>'dispatch_id' AS entity_id, payment_state AS status, evidence_json::text FROM topology.payment_state_projections WHERE evidence_json->>'dispatch_id' = @entityId ORDER BY projected_at DESC LIMIT @limit",
        "topology.scheduler_external_event_evidence" => "SELECT 'scheduler_enqueued' AS event_type, evidence_json->>'dispatch_id' AS entity_id, scheduler_status AS status, evidence_json::text FROM topology.scheduler_external_event_evidence WHERE evidence_json->>'dispatch_id' = @entityId AND (@requiredByBundle IS NULL OR required_by_bundle = @requiredByBundle) ORDER BY recorded_at DESC LIMIT @limit",
        "topology.audit_approval_evidence" => "SELECT event_type, evidence_json->>'dispatch_id' AS entity_id, approval_status AS status, evidence_json::text FROM topology.audit_approval_evidence WHERE evidence_json->>'dispatch_id' = @entityId ORDER BY recorded_at DESC LIMIT @limit",
        "topology.audit_notification_evidence" => "SELECT 'notification_recorded' AS event_type, evidence_json->>'dispatch_id' AS entity_id, notification_status AS status, evidence_json::text FROM topology.audit_notification_evidence WHERE evidence_json->>'dispatch_id' = @entityId ORDER BY recorded_at DESC LIMIT @limit",
        "topology.sftp_transfer_log" => "SELECT 'transfer_projected' AS event_type, evidence_json->>'dispatch_id' AS entity_id, transfer_status AS status, evidence_json::text FROM topology.sftp_transfer_log WHERE evidence_json->>'dispatch_id' = @entityId ORDER BY recorded_at DESC LIMIT @limit",
        _ => throw new InvalidOperationException("EXTERNAL_PORT_CONSUMER_EVIDENCE_TABLE_REF_UNSUPPORTED")
    };

    private static void AddCommonParameters(NpgsqlCommand cmd, string eventType, string? entityId, string? requiredByBundle, ExternalPortExecutionContext context, IReadOnlyDictionary<string, string> stepConfig, string evidenceJson, SftpTransferSourceRefs? sftpRefs = null)
    {
        cmd.Parameters.AddWithValue("eventType", eventType);
        cmd.Parameters.AddWithValue("entityId", (object?)entityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("requiredByBundle", (object?)requiredByBundle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("routeKey", (object?)context.RouteKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hookPath", (object?)context.PortRecord?.HookPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("payloadHash", ComputePayloadHash(context.RequestPayload));
        cmd.Parameters.AddWithValue("statusValue", FirstNonBlank(Read(stepConfig, "status_value"), eventType)!);
        cmd.Parameters.AddWithValue("checksumBefore", (object?)sftpRefs?.ChecksumValue ?? (object?)context.ChecksumValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("checksumAfter", (object?)Read(stepConfig, "checksum_after_ref") ?? (object?)sftpRefs?.ChecksumValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("manifestRef", (object?)sftpRefs?.ManifestId.ToString() ?? (object?)Read(stepConfig, "manifest_ref") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("exportJobId", sftpRefs is not null ? sftpRefs.ExportJobId : DBNull.Value);
        cmd.Parameters.AddWithValue("fileArtifactId", sftpRefs is not null ? sftpRefs.FileArtifactId : DBNull.Value);
        cmd.Parameters.AddWithValue("manifestId", sftpRefs is not null ? sftpRefs.ManifestId : DBNull.Value);
        cmd.Parameters.AddWithValue("checksumRecordId", sftpRefs is not null ? sftpRefs.ChecksumRecordId : DBNull.Value);
        cmd.Parameters.AddWithValue("failureReason", (object?)Read(stepConfig, "failure_reason") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("retryEvidenceJson", NpgsqlDbType.Jsonb, BuildRetryEvidenceJson(eventType, stepConfig));
        cmd.Parameters.AddWithValue("evidenceJson", NpgsqlDbType.Jsonb, evidenceJson);
    }

    private static async Task<SftpTransferSourceRefs> ResolveSftpTransferSourceRefsAsync(NpgsqlConnection conn, string eventType, ExternalPortExecutionContext context, CancellationToken ct)
    {
        var exportJobId = ReadGuidProperty(context.RequestPayload, "export_job_id")
            ?? context.ExportJobId
            ?? throw new InvalidOperationException("SFTP_TRANSFER_EXPORT_JOB_ID_REQUIRED");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT ej.export_job_id, fa.file_artifact_id, em.manifest_id, cr.checksum_record_id, cr.checksum_value, em.checksum
            FROM topology.export_jobs ej
            JOIN topology.export_manifests em
              ON em.export_job_id = ej.export_job_id
            JOIN topology.file_artifacts fa
              ON fa.export_job_id = ej.export_job_id
            JOIN topology.file_checksum_records cr
              ON cr.export_job_id = ej.export_job_id
             AND cr.file_artifact_id = fa.file_artifact_id
            WHERE ej.export_job_id = @exportJobId
              AND ej.status = 'completed'
              AND cr.verification_status = 'verified'
            ORDER BY fa.created_at DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("exportJobId", exportJobId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("SFTP_TRANSFER_EXPORT_JOB_MANIFEST_CHECKSUM_REQUIRED");

        var refs = new SftpTransferSourceRefs(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));

        if (!string.Equals(eventType, "checksum_mismatch", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(refs.ManifestChecksum) &&
            !string.Equals(refs.ManifestChecksum, refs.ChecksumValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SFTP_TRANSFER_CHECKSUM_MISMATCH");
        }

        return refs;
    }

    private static string BuildEvidenceJson(string eventType, string? entityId, string? requiredByBundle, ExternalPortExecutionContext context, IReadOnlyDictionary<string, string> stepConfig) =>
        JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["event_type"] = eventType,
            ["dispatch_id"] = entityId ?? context.DispatchId,
            ["entity_id"] = entityId,
            ["required_by_bundle"] = requiredByBundle,
            ["port_kind"] = context.PortKind,
            ["route_key"] = context.RouteKey,
            ["projection_table_ref"] = Read(stepConfig, "projection_table_ref")
        }.Where(static kvp => !string.IsNullOrWhiteSpace(kvp.Value)).ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value!));

    private static async Task ValidateTableRefBoundToActiveManifestAsync(NpgsqlConnection conn, string tableRef, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1
            FROM topology.physical_tables pt
            JOIN topology.physical_table_manifest_bindings pmb
              ON pmb.physical_table_id = pt.physical_table_id
             AND pmb.active = true
            JOIN hubs.topology_manifests tm
              ON tm.topology_manifest_id = pmb.topology_manifest_id
             AND tm.status = 'active'
            WHERE pt.active = true
              AND pt.table_ref = @tableRef
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tableRef", tableRef);
        if (await cmd.ExecuteScalarAsync(ct) is null)
            throw new InvalidOperationException("EXTERNAL_PORT_CONSUMER_EVIDENCE_TABLE_REF_NOT_MANIFEST_BOUND");
    }

    private static string BuildRetryEvidenceJson(string eventType, IReadOnlyDictionary<string, string> stepConfig)
    {
        if (!string.Equals(eventType, "retry_attempted", StringComparison.Ordinal))
            return "{}";
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["event_type"] = eventType,
            ["retry_policy_ref"] = Read(stepConfig, "retry_policy_ref") ?? "explicit_retry_evidence"
        });
    }

    private static Guid? ReadGuidProperty(JsonElement? payload, string name)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } element) return null;
        if (!element.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String) return null;
        return Guid.TryParse(prop.GetString(), out var value) ? value : null;
    }

    private sealed record SftpTransferSourceRefs(
        Guid ExportJobId,
        Guid FileArtifactId,
        Guid ManifestId,
        Guid ChecksumRecordId,
        string ChecksumValue,
        string? ManifestChecksum);

    private static string ComputePayloadHash(JsonElement? payload)
    {
        var raw = payload?.GetRawText() ?? "{}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static IReadOnlyDictionary<string, string> ParseEvidence(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object
            ? doc.RootElement.EnumerateObject().Where(static p => p.Value.ValueKind == JsonValueKind.String).ToDictionary(static p => p.Name, static p => p.Value.GetString() ?? string.Empty)
            : new Dictionary<string, string>();
    }

    private static string? Read(IReadOnlyDictionary<string, string> config, string key) =>
        config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string? FirstNonBlank(params string?[] values) => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
}
