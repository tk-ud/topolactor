using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Runtime;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql repository for the DB-guarded external credential vault.
/// This is a generic external_port_substrate attachment boundary: it does not
/// branch by provider_kind and never exposes plaintext credential material.
/// </summary>
public sealed class NpgsqlExternalCredentialVaultRepository : IExternalCredentialVaultRepository
{
    private readonly ILogger<NpgsqlExternalCredentialVaultRepository> _logger;
    private readonly string _connectionString;

    public NpgsqlExternalCredentialVaultRepository(
        ILogger<NpgsqlExternalCredentialVaultRepository> logger,
        string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<ExternalCredentialVaultRecord?> LoadAsync(Guid credentialVaultId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT credential_vault_id,
                   provider_kind,
                   required_by_bundle,
                   token_kind,
                   token_hash,
                   encrypted_payload,
                   encryption_key_reference,
                   expires_at,
                   refresh_before_seconds,
                   version,
                   locked_until,
                   active
            FROM topology.external_credential_vault
            WHERE credential_vault_id = @credentialVaultId
              AND active = true
            """;
        cmd.Parameters.AddWithValue("credentialVaultId", credentialVaultId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapVaultRecord(reader) : null;
    }

    public async Task<ExternalCredentialVaultRecord?> LoadByProviderAndBundleAsync(string providerKind, string requiredByBundle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerKind))
            throw new ArgumentException("providerKind is required.", nameof(providerKind));
        if (string.IsNullOrWhiteSpace(requiredByBundle))
            throw new ArgumentException("requiredByBundle is required.", nameof(requiredByBundle));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT credential_vault_id,
                   provider_kind,
                   required_by_bundle,
                   token_kind,
                   token_hash,
                   encrypted_payload,
                   encryption_key_reference,
                   expires_at,
                   refresh_before_seconds,
                   version,
                   locked_until,
                   active
            FROM topology.external_credential_vault
            WHERE provider_kind = @providerKind
              AND required_by_bundle = @requiredByBundle
              AND active = true
            ORDER BY credential_vault_id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("providerKind", providerKind);
        cmd.Parameters.AddWithValue("requiredByBundle", requiredByBundle);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapVaultRecord(reader) : null;
    }

    public async Task<ExternalCredentialRefreshLease?> AcquireRefreshLeaseAsync(
        Guid credentialVaultId,
        string leaseOwner,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leaseOwner))
            throw new ArgumentException("leaseOwner is required.", nameof(leaseOwner));

        var lockedUntil = now.Add(leaseDuration);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        Guid? attemptId = null;
        int? version = null;
        await using (var updateCmd = conn.CreateCommand())
        {
            updateCmd.Transaction = tx;
            updateCmd.CommandText = """
                UPDATE topology.external_credential_vault
                   SET locked_until = @lockedUntil,
                       updated_at = @now
                 WHERE credential_vault_id = @credentialVaultId
                   AND active = true
                   AND (locked_until IS NULL OR locked_until <= @now)
                RETURNING version
                """;
            updateCmd.Parameters.AddWithValue("credentialVaultId", credentialVaultId);
            updateCmd.Parameters.AddWithValue("lockedUntil", lockedUntil);
            updateCmd.Parameters.AddWithValue("now", now);

            var result = await updateCmd.ExecuteScalarAsync(ct);
            if (result is null)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            version = (int)result;
        }

        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.Transaction = tx;
            insertCmd.CommandText = """
                INSERT INTO topology.external_credential_refresh_attempt
                    (credential_vault_id, lease_owner, locked_until, attempt_status, created_at, updated_at)
                VALUES
                    (@credentialVaultId, @leaseOwner, @lockedUntil, 'acquired', @now, @now)
                RETURNING credential_refresh_attempt_id
                """;
            insertCmd.Parameters.AddWithValue("credentialVaultId", credentialVaultId);
            insertCmd.Parameters.AddWithValue("leaseOwner", leaseOwner);
            insertCmd.Parameters.AddWithValue("lockedUntil", lockedUntil);
            insertCmd.Parameters.AddWithValue("now", now);
            attemptId = (Guid?)await insertCmd.ExecuteScalarAsync(ct);
        }

        if (!attemptId.HasValue || !version.HasValue)
            throw new InvalidOperationException("EXTERNAL_CREDENTIAL_REFRESH_ATTEMPT_NOT_CREATED");

        await tx.CommitAsync(ct);
        return new ExternalCredentialRefreshLease(attemptId.Value, credentialVaultId, leaseOwner, lockedUntil, version.Value);
    }

    public async Task WriteEncryptedCredentialPayloadAsync(
        Guid credentialVaultId,
        int expectedVersion,
        byte[] encryptedPayload,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        if (encryptedPayload.Length == 0)
            throw new ArgumentException("encryptedPayload is required.", nameof(encryptedPayload));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("tokenHash is required.", nameof(tokenHash));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE topology.external_credential_vault
               SET encrypted_payload = @encryptedPayload,
                   token_hash = @tokenHash,
                   expires_at = @expiresAt,
                   version = version + 1,
                   locked_until = NULL,
                   updated_at = now()
             WHERE credential_vault_id = @credentialVaultId
               AND active = true
               AND version = @expectedVersion
            """;
        cmd.Parameters.AddWithValue("credentialVaultId", credentialVaultId);
        cmd.Parameters.AddWithValue("expectedVersion", expectedVersion);
        cmd.Parameters.AddWithValue("encryptedPayload", encryptedPayload);
        cmd.Parameters.AddWithValue("tokenHash", tokenHash);
        cmd.Parameters.AddWithValue("expiresAt", expiresAt);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows != 1)
            throw new InvalidOperationException("EXTERNAL_CREDENTIAL_STALE_VERSION_OR_INACTIVE");
    }

    public async Task ReleaseRefreshLeaseAsync(ExternalCredentialRefreshLease lease, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        await CompleteLeaseAsync(lease, "succeeded", failureCode: null, ct);
    }

    public async Task FailRefreshLeaseAsync(ExternalCredentialRefreshLease lease, string failureCode, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (string.IsNullOrWhiteSpace(failureCode))
            throw new ArgumentException("failureCode is required.", nameof(failureCode));

        await CompleteLeaseAsync(lease, "failed", failureCode, ct);
    }

    private async Task CompleteLeaseAsync(
        ExternalCredentialRefreshLease lease,
        string attemptStatus,
        string? failureCode,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var attemptCmd = conn.CreateCommand())
        {
            attemptCmd.Transaction = tx;
            attemptCmd.CommandText = """
                UPDATE topology.external_credential_refresh_attempt
                   SET attempt_status = @attemptStatus,
                       failure_code = @failureCode,
                       updated_at = now()
                 WHERE credential_refresh_attempt_id = @attemptId
                   AND credential_vault_id = @credentialVaultId
                   AND attempt_status = 'acquired'
                """;
            attemptCmd.Parameters.AddWithValue("attemptStatus", attemptStatus);
            attemptCmd.Parameters.AddWithValue("failureCode", (object?)failureCode ?? DBNull.Value);
            attemptCmd.Parameters.AddWithValue("attemptId", lease.CredentialRefreshAttemptId);
            attemptCmd.Parameters.AddWithValue("credentialVaultId", lease.CredentialVaultId);
            var rows = await attemptCmd.ExecuteNonQueryAsync(ct);
            if (rows != 1)
                throw new InvalidOperationException("EXTERNAL_CREDENTIAL_REFRESH_ATTEMPT_NOT_ACTIVE");
        }

        await using (var vaultCmd = conn.CreateCommand())
        {
            vaultCmd.Transaction = tx;
            vaultCmd.CommandText = """
                UPDATE topology.external_credential_vault
                   SET locked_until = NULL,
                       updated_at = now()
                 WHERE credential_vault_id = @credentialVaultId
                   AND locked_until = @lockedUntil
                """;
            vaultCmd.Parameters.AddWithValue("credentialVaultId", lease.CredentialVaultId);
            vaultCmd.Parameters.AddWithValue("lockedUntil", lease.LockedUntil);
            var rows = await vaultCmd.ExecuteNonQueryAsync(ct);
            if (rows != 1)
                _logger.LogWarning("External credential vault lease release did not match active lock for {CredentialVaultId}.", lease.CredentialVaultId);
        }

        await tx.CommitAsync(ct);
    }

    private static ExternalCredentialVaultRecord MapVaultRecord(NpgsqlDataReader reader) => new(
        reader.GetGuid(reader.GetOrdinal("credential_vault_id")),
        reader.GetString(reader.GetOrdinal("provider_kind")),
        reader.GetString(reader.GetOrdinal("required_by_bundle")),
        reader.GetString(reader.GetOrdinal("token_kind")),
        GetNullableString(reader, "token_hash"),
        GetNullableBytes(reader, "encrypted_payload"),
        GetNullableString(reader, "encryption_key_reference"),
        GetNullableDateTimeOffset(reader, "expires_at"),
        reader.GetInt32(reader.GetOrdinal("refresh_before_seconds")),
        reader.GetInt32(reader.GetOrdinal("version")),
        GetNullableDateTimeOffset(reader, "locked_until"),
        reader.GetBoolean(reader.GetOrdinal("active")));

    private static string? GetNullableString(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static byte[]? GetNullableBytes(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : (byte[])reader.GetValue(ordinal);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }
}
