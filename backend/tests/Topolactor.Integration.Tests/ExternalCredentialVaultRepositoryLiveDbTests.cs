using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Integration.Tests;

[Trait("Category", "RequiresDatabase")]
public class ExternalCredentialVaultRepositoryLiveDbTests
{
    [Fact]
    public async Task CredentialVaultRepository_LeaseAndAtomicWrite_FailCloseOnInactiveLockedAndStaleVersion()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var bundle = $"external-vault-live-{suffix}";
        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var repo = new NpgsqlExternalCredentialVaultRepository(NullLogger<NpgsqlExternalCredentialVaultRepository>.Instance, cs);
        var now = DateTimeOffset.Parse("2026-06-16T00:00:00Z");

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await ExecuteAsync(conn, """
                INSERT INTO topology.external_credential_vault
                    (credential_vault_id, provider_kind, required_by_bundle, token_kind, token_hash,
                     encrypted_payload, encryption_key_reference, expires_at, version, active)
                VALUES
                    (@activeId, 'generic-oauth', @bundle, 'oauth_refresh_token', 'sha256:old', decode('CAFE', 'hex'), 'test-key-ref', @expiresAt, 7, true),
                    (@inactiveId, 'generic-oauth', @bundle, 'oauth_refresh_token', 'sha256:inactive', decode('BEEF', 'hex'), 'test-key-ref', @expiresAt, 3, false);
                """,
                ("activeId", activeId),
                ("inactiveId", inactiveId),
                ("bundle", bundle),
                ("expiresAt", now.AddMinutes(10)));

            var missing = await repo.LoadAsync(Guid.NewGuid());
            var inactive = await repo.LoadAsync(inactiveId);
            Assert.Null(missing);
            Assert.Null(inactive);

            var lease = await repo.AcquireRefreshLeaseAsync(activeId, "live-test", TimeSpan.FromMinutes(5), now);
            Assert.NotNull(lease);
            Assert.Equal(7, lease.Version);

            var unavailable = await repo.AcquireRefreshLeaseAsync(activeId, "live-test-2", TimeSpan.FromMinutes(5), now.AddMinutes(1));
            Assert.Null(unavailable);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.WriteEncryptedCredentialPayloadAsync(activeId, expectedVersion: 6, new byte[] { 0x01 }, "sha256:stale", now.AddHours(1)));

            await repo.WriteEncryptedCredentialPayloadAsync(activeId, lease.Version, new byte[] { 0x02, 0x03 }, "sha256:new", now.AddHours(2));
            await repo.ReleaseRefreshLeaseAsync(lease);

            var updated = await repo.LoadAsync(activeId);
            Assert.NotNull(updated);
            Assert.Equal("sha256:new", updated.TokenHash);
            Assert.Equal(8, updated.Version);
            Assert.Equal(new byte[] { 0x02, 0x03 }, updated.EncryptedPayload);
            Assert.Null(updated.LockedUntil);

            var attemptStatus = await ScalarAsync<string>(conn, """
                SELECT attempt_status
                FROM topology.external_credential_refresh_attempt
                WHERE credential_refresh_attempt_id = @attemptId
                """, ("attemptId", lease.CredentialRefreshAttemptId));
            Assert.Equal("succeeded", attemptStatus);
        }
        finally
        {
            await CleanupAsync(cs, bundle);
        }
    }

    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(cs)) return cs;
        if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
            throw new InvalidOperationException("TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
        return null;
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<T?> ScalarAsync<T>(NpgsqlConnection conn, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var scalar = await cmd.ExecuteScalarAsync();
        return scalar is null or DBNull ? default : (T)scalar;
    }

    private static async Task CleanupAsync(string cs, string bundle)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await ExecuteAsync(conn, """
            DELETE FROM topology.external_credential_vault WHERE required_by_bundle = @bundle;
            """, ("bundle", bundle));
    }
}
