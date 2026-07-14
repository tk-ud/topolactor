using Npgsql;
using Topolactor.Guard;
using Topolactor.Repository;
using Topolactor.Schema;
using Topolactor.Service;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// End-to-end proof (real JwtTokenIssuer-issued token + real Postgres, not mocks) that JwtGuard
/// rejects an access JWT whose session has been revoked/the account deactivated — the exact
/// boundary every authenticated endpoint in Program.cs now calls (ValidateActiveSessionAsync /
/// ValidateForContextActiveSessionAsync) instead of the signature/exp-only Validate/ValidateForContext.
/// Skipped when TOPOLACTOR_TEST_DB_CONNECTION is not set, matching the repository live-DB test
/// convention. Placed in the "Auth tests" collection so DEMO_JWT_SECRET/DEMO_JWT_EXPIRY_HOURS env
/// mutation runs sequentially with the other Auth-tests-collection fixtures.
/// </summary>
[Collection("Auth tests")]
public class JwtGuardSessionRevocationLiveDbTests
{
    private const string TestSecret = "test-jwt-secret-for-session-revocation-livedb-tests";

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

    private static async Task<Guid> CreateUserAsync(string cs, string username)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var userCmd = conn.CreateCommand();
        userCmd.CommandText =
            "INSERT INTO auth.users (username, active, approve, status) VALUES (@u, true, true, 'active') " +
            "RETURNING user_id";
        userCmd.Parameters.AddWithValue("u", username);
        var userId = (Guid)(await userCmd.ExecuteScalarAsync())!;

        await using var grantCmd = conn.CreateCommand();
        grantCmd.CommandText = "INSERT INTO auth.grants (user_id, role_name, realm) VALUES (@id, 'user', 'user')";
        grantCmd.Parameters.AddWithValue("id", userId);
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
    public async Task ActiveSession_TokenPassesFullGuardCheck()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret).Set("DEMO_JWT_EXPIRY_HOURS", "8");
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_guard_active_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var authRepo = new NpgsqlAuthRepository(cs);
            var sessionId = await authRepo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));
            var token = new JwtTokenIssuer().IssueAccessToken("livedb_guard_active", AuthRealm.User, sessionId);

            var guard = new JwtGuard();
            var errors = await guard.ValidateActiveSessionAsync(token, authRepo);

            Assert.Empty(errors);
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task RevokedSession_TokenIsRejectedByGuard_NotJustSignatureAndExp()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret).Set("DEMO_JWT_EXPIRY_HOURS", "8");
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_guard_revoked_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var authRepo = new NpgsqlAuthRepository(cs);
            var sessionId = await authRepo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));
            var token = new JwtTokenIssuer().IssueAccessToken("livedb_guard_revoked", AuthRealm.User, sessionId);

            var guard = new JwtGuard();
            // Signature + exp alone would pass — prove that first, to isolate that the rejection
            // below comes specifically from the DB-side session-active check, not a malformed token.
            Assert.Empty(guard.Validate(token));

            await authRepo.RevokeSessionAsync(sessionId);

            var errors = await guard.ValidateActiveSessionAsync(token, authRepo);

            Assert.Contains(errors, e => e.Code == "AUTH_SESSION_REVOKED");
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task PasswordChange_AlreadyIssuedAccessJwt_IsRejectedByGuardOnItsNextRequest()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret).Set("DEMO_JWT_EXPIRY_HOURS", "8");
        await EnsureRolesSeededAsync(cs);

        var userId = await CreateUserAsync(cs, "livedb_guard_pwchange_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var authRepo = new NpgsqlAuthRepository(cs);

            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.OpenAsync();
                await using var credCmd = conn.CreateCommand();
                credCmd.CommandText = "INSERT INTO auth.credentials (user_id, password_hash) VALUES (@id, @hash)";
                credCmd.Parameters.AddWithValue("id", userId);
                credCmd.Parameters.AddWithValue("hash", BCrypt.Net.BCrypt.HashPassword("initial-password-123"));
                await credCmd.ExecuteNonQueryAsync();
            }

            var sessionId = await authRepo.CreateSessionAsync(userId, "user", "user_app", DateTimeOffset.UtcNow.AddDays(1));
            var token = new JwtTokenIssuer().IssueAccessToken("livedb_guard_pwchange", AuthRealm.User, sessionId);

            var guard = new JwtGuard();
            Assert.Empty(await guard.ValidateActiveSessionAsync(token, authRepo));

            var changeResult = await authRepo.ChangeOwnPasswordAsync(
                userId, "initial-password-123", BCrypt.Net.BCrypt.HashPassword("new-password-456"), "self");
            Assert.Equal(ChangeOwnPasswordOutcome.Success, changeResult.Outcome);

            // No new token was issued, no refresh happened — this is the SAME already-issued access
            // JWT from before the password change, now hitting the Guard on its "next request".
            var errorsAfterPasswordChange = await guard.ValidateActiveSessionAsync(token, authRepo);
            Assert.Contains(errorsAfterPasswordChange, e => e.Code == "AUTH_SESSION_REVOKED");
        }
        finally
        {
            await CleanupUserAsync(cs, userId);
        }
    }

    [Fact]
    public async Task TokenWithoutSidClaim_IsRejected_FailClosedNotFailOpen()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        // A token built without a sid claim (e.g. a legacy/forged token) must fail closed — the
        // Guard must never treat "no revocation identity" as "trust it anyway".
        var token = TestJwtBuilder.BuildToken(TestSecret, role: "user", realm: "user", audience: "user_app");
        var guard = new JwtGuard();

        // AuthRepository is never consulted (no DB call should even happen) when sid is absent;
        // any repository double, mock or otherwise, is fine here since it must not be reached.
        var errors = await guard.ValidateActiveSessionAsync(token, new NeverCalledAuthRepository());

        Assert.Contains(errors, e => e.Code == "AUTH_TOKEN_SID_MISSING");
    }

    private sealed class NeverCalledAuthRepository : AuthRepository
    {
        public NeverCalledAuthRepository() : base("unused") { }
        public override Task<AuthUserRecord?> FindUserByUsernameAsync(string username, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<string?> GetPasswordHashAsync(Guid userId, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<AuthUserRecord> CreatePendingUserWithCredentialAsync(string username, string passwordHash, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<string?> GetGrantRoleForRealmAsync(Guid userId, string realm, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<Guid> CreateSessionAsync(Guid userId, string realm, string audience, DateTimeOffset expiresAt, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<Guid> CreateRefreshTokenAsync(Guid sessionId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<AuthUserRecord?> GetUserStateByIdAsync(Guid userId, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<AuthRefreshTokenRecord?> FindActiveRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task RevokeRefreshTokenAsync(Guid refreshTokenId, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task InsertLoginEventAsync(Guid? userId, string realm, bool success, string? failureCode, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<ChangeOwnPasswordResult> ChangeOwnPasswordAsync(Guid userId, string currentPasswordPlain, string newPasswordHash, string actorUsername, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<IReadOnlyList<AuthSessionRecord>> ListActiveSessionsByUserAsync(Guid userId, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<bool> RevokeOwnedSessionAsync(Guid userId, Guid sessionId, string actorUsername, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<int> RevokeSessionsForUserAsync(Guid userId, Guid? exceptSessionId, string actorUsername, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<Guid?> FindActiveSessionIdByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<bool> RevokeCredentialAsync(Guid userId, string actorUsername, CancellationToken ct = default) => throw new InvalidOperationException();
        public override Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken ct = default) => throw new InvalidOperationException();
    }
}
