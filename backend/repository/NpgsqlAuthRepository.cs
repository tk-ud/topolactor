using Npgsql;

namespace Topolactor.Repository;

public class NpgsqlAuthRepository : AuthRepository
{
    public NpgsqlAuthRepository(string connectionString) : base(connectionString) { }

    public override async Task<AuthUserRecord?> FindUserByUsernameAsync(
        string username, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT user_id, username, active, approve, status, suspended_from, suspended_until
            FROM auth.users WHERE username = @u LIMIT 1
            """;
        cmd.Parameters.AddWithValue("u", username);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new AuthUserRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetBoolean(2),
            reader.GetBoolean(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
    }

    public override async Task<string?> GetPasswordHashAsync(
        Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT password_hash FROM auth.credentials WHERE user_id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("id", userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public override async Task<AuthUserRecord> CreatePendingUserWithCredentialAsync(
        string username, string passwordHash, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            await using var userCmd = conn.CreateCommand();
            userCmd.Transaction = tx;
            userCmd.CommandText =
                """
                INSERT INTO auth.users (username, active, approve, status, state_note)
                VALUES (@u, true, false, 'active', 'normal_user_registration_pending_approval')
                RETURNING user_id, username, active, approve, status, suspended_from, suspended_until
                """;
            userCmd.Parameters.AddWithValue("u", username);

            AuthUserRecord record;
            await using (var reader = await userCmd.ExecuteReaderAsync(ct))
            {
                await reader.ReadAsync(ct);
                record = new AuthUserRecord(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetBoolean(2),
                    reader.GetBoolean(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                    reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
            }

            await using var credentialCmd = conn.CreateCommand();
            credentialCmd.Transaction = tx;
            credentialCmd.CommandText =
                "INSERT INTO auth.credentials (user_id, password_hash) VALUES (@uid, @hash)";
            credentialCmd.Parameters.AddWithValue("uid", record.UserId);
            credentialCmd.Parameters.AddWithValue("hash", passwordHash);
            await credentialCmd.ExecuteNonQueryAsync(ct);

            await using var grantCmd = conn.CreateCommand();
            grantCmd.Transaction = tx;
            grantCmd.CommandText =
                "INSERT INTO auth.grants (user_id, role_name, realm) VALUES (@uid, 'user', 'user')";
            grantCmd.Parameters.AddWithValue("uid", record.UserId);
            await grantCmd.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);
            return record;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public override async Task<string?> GetGrantRoleForRealmAsync(
        Guid userId, string realm, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT role_name FROM auth.grants WHERE user_id = @id AND realm = @realm LIMIT 1";
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("realm", realm);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public override async Task<Guid> CreateSessionAsync(
        Guid userId, string realm, string audience, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO auth.sessions (user_id, realm, audience, expires_at) " +
            "VALUES (@uid, @realm, @aud, @exp) RETURNING session_id";
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("realm", realm);
        cmd.Parameters.AddWithValue("aud", audience);
        cmd.Parameters.AddWithValue("exp", expiresAt);
        var id = await cmd.ExecuteScalarAsync(ct);
        return (Guid)id!;
    }

    public override async Task<Guid> CreateRefreshTokenAsync(
        Guid sessionId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO auth.refresh_tokens (session_id, token_hash, expires_at) " +
            "VALUES (@sid, @hash, @exp) RETURNING refresh_token_id";
        cmd.Parameters.AddWithValue("sid", sessionId);
        cmd.Parameters.AddWithValue("hash", tokenHash);
        cmd.Parameters.AddWithValue("exp", expiresAt);
        var id = await cmd.ExecuteScalarAsync(ct);
        return (Guid)id!;
    }

    public override async Task<AuthRefreshTokenRecord?> FindActiveRefreshTokenByHashAsync(
        string tokenHash, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT rt.refresh_token_id, s.session_id, u.user_id, u.username, s.realm, s.audience, g.role_name " +
            "FROM auth.refresh_tokens rt " +
            "JOIN auth.sessions s ON s.session_id = rt.session_id " +
            "JOIN auth.users u ON u.user_id = s.user_id " +
            "JOIN auth.grants g ON g.user_id = u.user_id AND g.realm = s.realm " +
            "WHERE rt.token_hash = @hash AND rt.revoked_at IS NULL AND s.revoked_at IS NULL " +
            "  AND rt.expires_at > now() AND s.expires_at > now() " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("hash", tokenHash);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new AuthRefreshTokenRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6));
    }

    public override async Task RevokeRefreshTokenAsync(
        Guid refreshTokenId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE auth.refresh_tokens SET revoked_at = now() WHERE refresh_token_id = @id";
        cmd.Parameters.AddWithValue("id", refreshTokenId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public override async Task RevokeSessionAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE auth.sessions SET revoked_at = now() WHERE session_id = @id";
        cmd.Parameters.AddWithValue("id", sessionId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public override async Task InsertLoginEventAsync(
        Guid? userId, string realm, bool success, string? failureCode, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO auth.login_events (user_id, realm, success, failure_code) " +
            "VALUES (@uid, @realm, @ok, @code)";
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("realm", realm);
        cmd.Parameters.AddWithValue("ok", success);
        cmd.Parameters.AddWithValue("code", (object?)failureCode ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
