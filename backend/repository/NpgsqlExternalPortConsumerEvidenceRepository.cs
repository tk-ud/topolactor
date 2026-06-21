using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Topolactor.Runtime;

namespace Topolactor.Repository;

public sealed class NpgsqlExternalPortConsumerEvidenceRepository : IExternalPortConsumerEvidenceRepository
{
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
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = InsertSql(tableRef);
        AddCommonParameters(cmd, eventType, entityId, requiredByBundle, context, stepConfig, evidenceJson);
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
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
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
        "topology.sftp_transfer_log" => "INSERT INTO topology.sftp_transfer_log (export_job_id, transfer_status, checksum_before, checksum_after, manifest_ref, response_port_ref, evidence_json) VALUES (NULL, @statusValue, @checksumBefore, @checksumAfter, @manifestRef, @entityId, @evidenceJson)",
        _ => throw new InvalidOperationException("EXTERNAL_PORT_CONSUMER_EVIDENCE_TABLE_REF_UNSUPPORTED")
    };

    private static string SelectSql(string tableRef) => tableRef switch
    {
        "topology.email_delivery_evidence" => "SELECT event_type, email_draft_id::text AS entity_id, delivery_status AS status, evidence_json::text FROM topology.email_delivery_evidence ORDER BY recorded_at DESC LIMIT @limit",
        "topology.webhook_intake_snapshots" => "SELECT 'webhook_received' AS event_type, scheduler_event_ref AS entity_id, 'received' AS status, evidence_json::text FROM topology.webhook_intake_snapshots WHERE (@requiredByBundle IS NULL OR required_by_bundle = @requiredByBundle) ORDER BY received_at DESC LIMIT @limit",
        "topology.signature_verification_evidence" => "SELECT 'signature_verification' AS event_type, webhook_intake_snapshot_id::text AS entity_id, verification_status AS status, evidence_json::text FROM topology.signature_verification_evidence WHERE (@requiredByBundle IS NULL OR required_by_bundle = @requiredByBundle) ORDER BY recorded_at DESC LIMIT @limit",
        "topology.payment_state_projections" => "SELECT 'payment_state_projected' AS event_type, webhook_intake_snapshot_id::text AS entity_id, payment_state AS status, evidence_json::text FROM topology.payment_state_projections ORDER BY projected_at DESC LIMIT @limit",
        "topology.scheduler_external_event_evidence" => "SELECT 'scheduler_enqueued' AS event_type, trigger_ref AS entity_id, scheduler_status AS status, evidence_json::text FROM topology.scheduler_external_event_evidence WHERE (@requiredByBundle IS NULL OR required_by_bundle = @requiredByBundle) ORDER BY recorded_at DESC LIMIT @limit",
        "topology.audit_approval_evidence" => "SELECT event_type, approval_request_id::text AS entity_id, approval_status AS status, evidence_json::text FROM topology.audit_approval_evidence ORDER BY recorded_at DESC LIMIT @limit",
        "topology.audit_notification_evidence" => "SELECT 'notification_recorded' AS event_type, response_port_ref AS entity_id, notification_status AS status, evidence_json::text FROM topology.audit_notification_evidence ORDER BY recorded_at DESC LIMIT @limit",
        "topology.sftp_transfer_log" => "SELECT 'transfer_projected' AS event_type, response_port_ref AS entity_id, transfer_status AS status, evidence_json::text FROM topology.sftp_transfer_log ORDER BY recorded_at DESC LIMIT @limit",
        _ => throw new InvalidOperationException("EXTERNAL_PORT_CONSUMER_EVIDENCE_TABLE_REF_UNSUPPORTED")
    };

    private static void AddCommonParameters(NpgsqlCommand cmd, string eventType, string? entityId, string? requiredByBundle, ExternalPortExecutionContext context, IReadOnlyDictionary<string, string> stepConfig, string evidenceJson)
    {
        cmd.Parameters.AddWithValue("eventType", eventType);
        cmd.Parameters.AddWithValue("entityId", (object?)entityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("requiredByBundle", (object?)requiredByBundle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("routeKey", (object?)context.RouteKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hookPath", (object?)context.PortRecord?.HookPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("payloadHash", ComputePayloadHash(context.RequestPayload));
        cmd.Parameters.AddWithValue("statusValue", FirstNonBlank(Read(stepConfig, "status_value"), eventType)!);
        cmd.Parameters.AddWithValue("checksumBefore", (object?)context.ChecksumValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("checksumAfter", (object?)Read(stepConfig, "checksum_after_ref") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("manifestRef", (object?)Read(stepConfig, "manifest_ref") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("evidenceJson", NpgsqlDbType.Jsonb, evidenceJson);
    }

    private static string BuildEvidenceJson(string eventType, string? entityId, string? requiredByBundle, ExternalPortExecutionContext context, IReadOnlyDictionary<string, string> stepConfig) =>
        JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["event_type"] = eventType,
            ["entity_id"] = entityId,
            ["required_by_bundle"] = requiredByBundle,
            ["port_kind"] = context.PortKind,
            ["route_key"] = context.RouteKey,
            ["projection_table_ref"] = Read(stepConfig, "projection_table_ref")
        }.Where(static kvp => !string.IsNullOrWhiteSpace(kvp.Value)).ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value!));

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
