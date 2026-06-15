using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// PostgreSQL implementation of CredentialReferenceRepository.
/// Persists credential reference metadata to topology.credential_references
/// and audit events to logs.credential_audit_log.
///
/// Security invariant: no credential values are ever written to or read from the DB.
/// Only reference_key (logical name), provider_kind, status, validation_status,
/// and timestamps are stored.
/// </summary>
public sealed class NpgsqlCredentialReferenceRepository : CredentialReferenceRepository
{
    public NpgsqlCredentialReferenceRepository(string connectionString)
        : base(connectionString) { }

    public override async Task<CredentialReferenceDto> RegisterAsync(
        string referenceKey,
        string providerKind,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.credential_references
                (reference_key, provider_kind, status, validation_status)
            VALUES (@rk, @pk, 'registered', 'unchecked')
            RETURNING
                credential_reference_id,
                reference_key,
                provider_kind,
                status,
                validation_status,
                last_validated_at,
                last_rotated_at,
                registered_at,
                updated_at
            """;
        cmd.Parameters.AddWithValue("rk", referenceKey);
        cmd.Parameters.AddWithValue("pk", providerKind);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException($"CREDENTIAL_REGISTER_FAILED: insert returned no row for reference_key='{referenceKey}'.");

        return ReadDto(reader);
    }

    public override async Task<CredentialReferenceDto?> GetByKeyAsync(
        string referenceKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                credential_reference_id,
                reference_key,
                provider_kind,
                status,
                validation_status,
                last_validated_at,
                last_rotated_at,
                registered_at,
                updated_at
            FROM topology.credential_references
            WHERE reference_key = @rk
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("rk", referenceKey);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadDto(reader) : null;
    }

    public override async Task<IReadOnlyList<CredentialReferenceDto>> ListAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                credential_reference_id,
                reference_key,
                provider_kind,
                status,
                validation_status,
                last_validated_at,
                last_rotated_at,
                registered_at,
                updated_at
            FROM topology.credential_references
            ORDER BY registered_at DESC
            """;

        var list = new List<CredentialReferenceDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(ReadDto(reader));
        return list;
    }

    public override async Task<bool> UpdateValidationStatusAsync(
        string referenceKey,
        string validationStatus,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE topology.credential_references
            SET validation_status = @vs,
                last_validated_at = now(),
                status = CASE WHEN @vs = 'valid' THEN 'active' ELSE status END,
                updated_at = now()
            WHERE reference_key = @rk
            """;
        cmd.Parameters.AddWithValue("vs", validationStatus);
        cmd.Parameters.AddWithValue("rk", referenceKey);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public override async Task<bool> UpdateRotationTimestampAsync(
        string referenceKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE topology.credential_references
            SET last_rotated_at = now(),
                status = 'active',
                validation_status = 'unchecked',
                updated_at = now()
            WHERE reference_key = @rk
            """;
        cmd.Parameters.AddWithValue("rk", referenceKey);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public override async Task AppendAuditEventAsync(
        CredentialAuditEventRecord record,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO logs.credential_audit_log
                (event_type, credential_reference_id, reference_key, outcome, failure_code)
            VALUES (@et, @crid, @rk, @oc, @fc)
            """;
        cmd.Parameters.AddWithValue("et", record.EventType);
        cmd.Parameters.AddWithValue("crid", record.CredentialReferenceId);
        cmd.Parameters.AddWithValue("rk", record.ReferenceKey);
        cmd.Parameters.AddWithValue("oc", record.Outcome);
        cmd.Parameters.AddWithValue("fc", (object?)record.FailureCode ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static CredentialReferenceDto ReadDto(NpgsqlDataReader reader) =>
        new(
            CredentialReferenceId: reader.GetGuid(0),
            ReferenceKey: reader.GetString(1),
            ProviderKind: reader.GetString(2),
            Status: reader.GetString(3),
            ValidationStatus: reader.GetString(4),
            LastValidatedAt: reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            LastRotatedAt: reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            RegisteredAt: reader.GetFieldValue<DateTimeOffset>(7),
            UpdatedAt: reader.GetFieldValue<DateTimeOffset>(8)
        );
}
