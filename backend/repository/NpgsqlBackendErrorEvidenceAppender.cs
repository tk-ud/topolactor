using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Npgsql implementation of the append-only backend system error evidence appender.
///
/// SSOT: docs/design/backend-error-evidence-report-notify-ssot.yaml (appender_contract / logs_error_contract).
///
/// Boundary guarantees:
///   - Validates the envelope (required fields non-empty) and throws on an invalid envelope —
///     this exposes appender failure rather than silently dropping evidence.
///   - Redacts and bounds message_public and evidence_json (no secrets, bounded size).
///   - Appends to logs.error on its OWN connection, independent of any failing business
///     transaction, so a business rollback cannot erase already-classified system error evidence.
///   - Does not catch/convert the original failure: callers stay responsible for rethrow/return.
/// </summary>
public sealed class NpgsqlBackendErrorEvidenceAppender : IBackendErrorEvidenceAppender
{
    private const int MaxMessageLength = 2000;
    private const int MaxEvidenceKeys = 32;
    private const int MaxEvidenceValueLength = 1000;

    // Evidence/message redaction: drop any evidence key whose name signals a secret-bearing value.
    private static readonly string[] SecretKeyMarkers =
    {
        "credential", "token", "secret", "password", "authorization", "decrypted",
        "signed_url", "private_key", "api_key", "bearer",
    };

    private readonly string _connectionString;

    public NpgsqlBackendErrorEvidenceAppender(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task AppendAsync(BackendErrorEvidence evidence, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Validate(evidence);

        var evidenceJson = BuildRedactedEvidenceJson(evidence.EvidenceJson);
        var messagePublic = Bound(evidence.MessagePublic, MaxMessageLength);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO logs.error (
                occurred_at, origin_layer, boundary_key, function_key, runtime_lane,
                primitive_key, step_order, error_kind, error_code, exception_type,
                message_public, stack_hash, severity, retryable, evidence_json)
            VALUES (
                COALESCE(@occurredAt, now()), @originLayer, @boundaryKey, @functionKey, @runtimeLane,
                @primitiveKey, @stepOrder, @errorKind, @errorCode, @exceptionType,
                @messagePublic, @stackHash, @severity, @retryable, @evidenceJson)
            """;

        cmd.Parameters.AddWithValue("occurredAt", NpgsqlDbType.TimestampTz,
            evidence.OccurredAt.HasValue ? evidence.OccurredAt.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("originLayer", evidence.OriginLayer);
        cmd.Parameters.AddWithValue("boundaryKey", evidence.BoundaryKey);
        cmd.Parameters.AddWithValue("functionKey", (object?)evidence.FunctionKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("runtimeLane", (object?)evidence.RuntimeLane ?? DBNull.Value);
        cmd.Parameters.AddWithValue("primitiveKey", (object?)evidence.PrimitiveKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("stepOrder", NpgsqlDbType.Integer,
            evidence.StepOrder.HasValue ? evidence.StepOrder.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("errorKind", evidence.ErrorKind);
        cmd.Parameters.AddWithValue("errorCode", evidence.ErrorCode);
        cmd.Parameters.AddWithValue("exceptionType", (object?)evidence.ExceptionType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("messagePublic", messagePublic);
        cmd.Parameters.AddWithValue("stackHash", evidence.StackHash);
        cmd.Parameters.AddWithValue("severity", evidence.Severity);
        cmd.Parameters.AddWithValue("retryable", evidence.Retryable);
        cmd.Parameters.AddWithValue("evidenceJson", NpgsqlDbType.Jsonb, evidenceJson);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void Validate(BackendErrorEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence.OriginLayer))
            throw new ArgumentException("BACKEND_ERROR_EVIDENCE_ORIGIN_LAYER_REQUIRED", nameof(evidence));
        if (string.IsNullOrWhiteSpace(evidence.BoundaryKey))
            throw new ArgumentException("BACKEND_ERROR_EVIDENCE_BOUNDARY_KEY_REQUIRED", nameof(evidence));
        if (string.IsNullOrWhiteSpace(evidence.ErrorKind))
            throw new ArgumentException("BACKEND_ERROR_EVIDENCE_ERROR_KIND_REQUIRED", nameof(evidence));
        if (string.IsNullOrWhiteSpace(evidence.ErrorCode))
            throw new ArgumentException("BACKEND_ERROR_EVIDENCE_ERROR_CODE_REQUIRED", nameof(evidence));
        if (string.IsNullOrWhiteSpace(evidence.MessagePublic))
            throw new ArgumentException("BACKEND_ERROR_EVIDENCE_MESSAGE_REQUIRED", nameof(evidence));
        if (string.IsNullOrWhiteSpace(evidence.StackHash))
            throw new ArgumentException("BACKEND_ERROR_EVIDENCE_STACK_HASH_REQUIRED", nameof(evidence));
        if (string.IsNullOrWhiteSpace(evidence.Severity))
            throw new ArgumentException("BACKEND_ERROR_EVIDENCE_SEVERITY_REQUIRED", nameof(evidence));
    }

    private static string BuildRedactedEvidenceJson(IReadOnlyDictionary<string, string>? evidence)
    {
        if (evidence is null || evidence.Count == 0)
            return "{}";

        var redacted = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in evidence)
        {
            if (redacted.Count >= MaxEvidenceKeys) break;
            if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
            if (IsSecretKey(kvp.Key))
            {
                redacted[kvp.Key] = "[redacted]";
                continue;
            }
            redacted[kvp.Key] = Bound(kvp.Value ?? string.Empty, MaxEvidenceValueLength);
        }
        return JsonSerializer.Serialize(redacted);
    }

    private static bool IsSecretKey(string key)
    {
        foreach (var marker in SecretKeyMarkers)
        {
            if (key.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string Bound(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
