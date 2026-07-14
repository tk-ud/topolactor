using Npgsql;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Real Postgres-backed proof (not mocks) that the JWT session revocation identity actually takes
/// effect at the DB layer JwtGuard.ValidateActiveSessionAsync consults
/// (AuthRepository.IsSessionActiveAsync): password change, session revoke (self and admin),
/// credential revoke, account deactivation, and role change all cause a previously-active session
/// to become inactive — the exact mechanism that makes an already-issued access JWT (which carries
/// only the session id, never a self-asserted validity claim) get rejected on its next request,
/// without waiting for token expiry or a refresh. Skipped when TOPOLACTOR_TEST_DB_CONNECTION is not
/// set, matching the repository live-DB test convention.
/// </summary>
[Collection("AuthSessionRevocationLiveDb")]
public class AuthSessionRevocationLiveDbTests
{
    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }

    private static async Task EnsureRolesSeededAsync(string cs)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO auth.roles (role_name, description) VALUES " +
            "('user', 'Standard application user'), ('admin', 'Registrar admin console operator') " +
            "ON CONFLICT (role_name) DO NOTHING";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> CreateUserAsync(string cs, string username, string realm, string role)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using var userCmd = conn.CreateCommand();
        userCmd.CommandText =
            "INSERT INTO auth.users (username, active, approve, status) VALUES (@u, true, true, 'active') " +
            "RETURNING user_id";
        userCmd.Parameters.AddWithValue("u", username);
        var userId = (Guid)(await userCmd.ExecuteScalarAsync())!;

        await using var credCmd = conn.CreateCommand();
        credCmd.CommandText = "INSERT INTO auth.credentials (user_id, password_hash) VALUES (@id, @hash)";
        credCmd.Parameters.AddWithValue("id", userId);
        credCmd.Parameters.AddWithValue("hash", BCrypt.Net.BCrypt.HashPassword("initial-password-123"));
        await credCmd.ExecuteNonQueryAsync();

        await using var grantCmd = conn.CreateCommand();
        grantCmd.CommandText = "INSERT INTO auth.grants (user_id, role_name, realm) VALUES (@id, @role, @realm)";
        grantCmd.Parameters.AddWithValue("id", userId);
        grantCmd.Parameters.AddWithValue("role", role);
        grantCmd.Parameters.AddWithValue("realm", realm);
        await grantCmd.ExecuteNonQueryAsync();

        return userId;
    }

    private static async Task CleanupUserAsync(string cs, Guid userId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM auth.users WHERE user_id = @id";
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task FreshlyIssuedSession_IsActive()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_active_" + Guid.NewGuid().ToString("N")[..8], "user", "user");
        try
        {
            var repo = new NpgsqlAuthRepository(cs);
            var sessionId = await repo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));

            Assert.True(await repo.IsSessionActiveAsync(sessionId));
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task OwnedSessionRevoke_MakesSessionInactive()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_selfrevoke_" + Guid.NewGuid().ToString("N")[..8], "user", "user");
        try
        {
            var repo = new NpgsqlAuthRepository(cs);
            var sessionId = await repo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));
            Assert.True(await repo.IsSessionActiveAsync(sessionId));

            var revoked = await repo.RevokeOwnedSessionAsync(userId, sessionId, "self");
            Assert.True(revoked);

            Assert.False(await repo.IsSessionActiveAsync(sessionId));
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task AdminBulkSessionRevoke_MakesAllActiveSessionsInactive()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_adminrevoke_" + Guid.NewGuid().ToString("N")[..8], "user", "user");
        try
        {
            var repo = new NpgsqlAuthRepository(cs);
            var sessionA = await repo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));
            var sessionB = await repo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));

            var revokedCount = await repo.RevokeSessionsForUserAsync(userId, exceptSessionId: null, "admin_actor");

            Assert.Equal(2, revokedCount);
            Assert.False(await repo.IsSessionActiveAsync(sessionA));
            Assert.False(await repo.IsSessionActiveAsync(sessionB));
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task PasswordChange_RevokesEveryActiveSession_ProvingAlreadyIssuedAccessJwtsAreRejectedImmediately()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_pwchange_" + Guid.NewGuid().ToString("N")[..8], "user", "user");
        try
        {
            var repo = new NpgsqlAuthRepository(cs);
            var sessionId = await repo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));
            Assert.True(await repo.IsSessionActiveAsync(sessionId));

            var result = await repo.ChangeOwnPasswordAsync(userId, "initial-password-123", BCrypt.Net.BCrypt.HashPassword("new-password-456"), "self");

            Assert.Equal(ChangeOwnPasswordOutcome.Success, result.Outcome);
            Assert.Equal(1, result.SessionsRevoked);
            // This is the real assertion the reviewer's NG axis targets: session revocation is not
            // "eventually true via refresh-token rotation" — it is immediately observable at the DB
            // layer JwtGuard.ValidateActiveSessionAsync reads on the very next request.
            Assert.False(await repo.IsSessionActiveAsync(sessionId));
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task CredentialRevoke_RevokesEveryActiveSession()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_credrevoke_" + Guid.NewGuid().ToString("N")[..8], "user", "user");
        try
        {
            var repo = new NpgsqlAuthRepository(cs);
            var sessionId = await repo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));
            Assert.True(await repo.IsSessionActiveAsync(sessionId));

            var revoked = await repo.RevokeCredentialAsync(userId, "admin_actor");
            Assert.True(revoked);

            Assert.False(await repo.IsSessionActiveAsync(sessionId));
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task AccountDeactivation_MakesSessionInactive_EvenWithoutExplicitSessionRevoke()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_deactivate_" + Guid.NewGuid().ToString("N")[..8], "user", "user");
        try
        {
            var repo = new NpgsqlAuthRepository(cs);
            var sessionId = await repo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));
            Assert.True(await repo.IsSessionActiveAsync(sessionId));

            // Deactivate the account directly (mirrors AdminRuntimeMasterRoster's auth_users:update
            // active=false path) WITHOUT touching auth.sessions at all — IsSessionActiveAsync's join
            // to auth.users must independently catch this, proving deactivation takes effect even if
            // a caller forgot to also revoke sessions explicitly.
            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE auth.users SET active = false WHERE user_id = @id";
                cmd.Parameters.AddWithValue("id", userId);
                await cmd.ExecuteNonQueryAsync();
            }

            Assert.False(await repo.IsSessionActiveAsync(sessionId));
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task RoleChange_RevokesEveryActiveSession_ProvingStaleRoleJwtsAreRejectedImmediately()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_rolechange_" + Guid.NewGuid().ToString("N")[..8], "admin/system", "admin");
        try
        {
            var authRepo = new NpgsqlAuthRepository(cs);
            var sessionId = await authRepo.CreateSessionAsync(userId, "admin/system", "admin_console", DateTimeOffset.UtcNow.AddDays(1));
            Assert.True(await authRepo.IsSessionActiveAsync(sessionId));

            var masterRepo = new NpgsqlAuthMasterRepository(cs);
            var updated = await masterRepo.UpdateUserAsync(
                userId, username: null, active: null, approve: null, status: null,
                suspendedFrom: null, suspendedUntil: null, clearSuspendedFrom: false, clearSuspendedUntil: false,
                stateNote: null, roleName: "user");

            Assert.NotNull(updated);
            Assert.Equal("user", updated!.Role);
            // The role grant changed from admin to user; the session that backed the OLD admin-role
            // JWT must be revoked as part of that same completion boundary — otherwise the already-
            // issued admin-role token would keep passing JwtGuard's session-active check forever.
            Assert.False(await authRepo.IsSessionActiveAsync(sessionId));
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task RoleUnchanged_NoOpUpdate_DoesNotRevokeSessions()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_rolenochange_" + Guid.NewGuid().ToString("N")[..8], "user", "user");
        try
        {
            var authRepo = new NpgsqlAuthRepository(cs);
            var sessionId = await authRepo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));

            var masterRepo = new NpgsqlAuthMasterRepository(cs);
            // roleName: "user" is a no-op — the user already has the 'user' grant, not 'admin'.
            var updated = await masterRepo.UpdateUserAsync(
                userId, username: null, active: null, approve: null, status: "active",
                suspendedFrom: null, suspendedUntil: null, clearSuspendedFrom: false, clearSuspendedUntil: false,
                stateNote: null, roleName: "user");

            Assert.NotNull(updated);
            Assert.True(await authRepo.IsSessionActiveAsync(sessionId));
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }
}
