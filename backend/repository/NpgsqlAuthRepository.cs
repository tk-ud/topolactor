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

    public override async Task<AuthUserRecord?> GetUserStateByIdAsync(
        Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT user_id, username, active, approve, status, suspended_from, suspended_until
            FROM auth.users WHERE user_id = @id LIMIT 1
            """;
        cmd.Parameters.AddWithValue("id", userId);
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

    // Non-secret completion evidence for credential/session mutations, appended into the same
    // transaction as the mutation itself (logs.diff — the same append-only evidence table
    // AdminMasterRosterAudit writes, but here committed atomically instead of best-effort).
    // beforeJson/afterJson must never contain password material — callers only pass field names
    // and non-secret metadata (session ids, counts, timestamps).
    private static async Task AppendCredentialEvidenceAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx,
        string physicalTableName, string recordId, string operationKind,
        object? before, object? after, string? actor, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO logs.diff (
                source_set_id, basis_window, physical_table_id, physical_table_name,
                record_id, operation_kind, before_state_or_diff_json, after_state_or_diff_json,
                observed_at, actor_or_source, archive_policy
            ) VALUES (
                'self_credential', 'auth_operation', @physical_table_name, @physical_table_name,
                @record_id, @operation_kind, @before::jsonb, @after::jsonb,
                now(), @actor, 'required'
            )
            """;
        cmd.Parameters.AddWithValue("physical_table_name", physicalTableName);
        cmd.Parameters.AddWithValue("record_id", recordId);
        cmd.Parameters.AddWithValue("operation_kind", operationKind);
        cmd.Parameters.AddWithValue("before", System.Text.Json.JsonSerializer.Serialize(before ?? new { }));
        cmd.Parameters.AddWithValue("after", System.Text.Json.JsonSerializer.Serialize(after ?? new { }));
        cmd.Parameters.AddWithValue("actor", (object?)actor ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public override async Task<ChangeOwnPasswordResult> ChangeOwnPasswordAsync(
        Guid userId, string currentPasswordPlain, string newPasswordHash, string actorUsername,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using var readCmd = conn.CreateCommand();
            readCmd.Transaction = tx;
            readCmd.CommandText =
                "SELECT password_hash FROM auth.credentials WHERE user_id = @id FOR UPDATE";
            readCmd.Parameters.AddWithValue("id", userId);
            var currentHash = (string?)await readCmd.ExecuteScalarAsync(ct);
            if (currentHash is null)
            {
                await tx.RollbackAsync(ct);
                return new ChangeOwnPasswordResult(ChangeOwnPasswordOutcome.CredentialNotFound, 0);
            }

            if (!BCrypt.Net.BCrypt.Verify(currentPasswordPlain, currentHash))
            {
                await tx.RollbackAsync(ct);
                return new ChangeOwnPasswordResult(ChangeOwnPasswordOutcome.CurrentPasswordInvalid, 0);
            }

            await using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = "UPDATE auth.credentials SET password_hash = @hash WHERE user_id = @id";
            updateCmd.Parameters.AddWithValue("hash", newPasswordHash);
            updateCmd.Parameters.AddWithValue("id", userId);
            await updateCmd.ExecuteNonQueryAsync(ct);

            await using var revokeCmd = conn.CreateCommand();
            revokeCmd.Transaction = tx;
            revokeCmd.CommandText =
                "UPDATE auth.sessions SET revoked_at = now() WHERE user_id = @id AND revoked_at IS NULL";
            revokeCmd.Parameters.AddWithValue("id", userId);
            var revokedCount = await revokeCmd.ExecuteNonQueryAsync(ct);

            await AppendCredentialEvidenceAsync(
                conn, tx, "auth.credentials", userId.ToString(), "self_password_change",
                before: new { }, after: new { sessionsRevoked = revokedCount },
                actorUsername, ct);

            await tx.CommitAsync(ct);
            return new ChangeOwnPasswordResult(ChangeOwnPasswordOutcome.Success, revokedCount);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public override async Task<IReadOnlyList<AuthSessionRecord>> ListActiveSessionsByUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT session_id, realm, audience, expires_at, created_at, revoked_at
            FROM auth.sessions
            WHERE user_id = @id AND revoked_at IS NULL AND expires_at > now()
            ORDER BY created_at DESC
            """;
        cmd.Parameters.AddWithValue("id", userId);
        var results = new List<AuthSessionRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new AuthSessionRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                Revoked: !reader.IsDBNull(5)));
        }
        return results;
    }

    public override async Task<bool> RevokeOwnedSessionAsync(
        Guid userId, Guid sessionId, string actorUsername, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                UPDATE auth.sessions SET revoked_at = now()
                WHERE session_id = @sid AND user_id = @uid AND revoked_at IS NULL
                """;
            cmd.Parameters.AddWithValue("sid", sessionId);
            cmd.Parameters.AddWithValue("uid", userId);
            var affected = await cmd.ExecuteNonQueryAsync(ct);
            if (affected == 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            await AppendCredentialEvidenceAsync(
                conn, tx, "auth.sessions", sessionId.ToString(), "session_revoke",
                before: new { }, after: new { userId }, actorUsername, ct);

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public override async Task<int> RevokeSessionsForUserAsync(
        Guid userId, Guid? exceptSessionId, string actorUsername, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = exceptSessionId is null
                ? "UPDATE auth.sessions SET revoked_at = now() WHERE user_id = @uid AND revoked_at IS NULL"
                : "UPDATE auth.sessions SET revoked_at = now() WHERE user_id = @uid AND revoked_at IS NULL AND session_id <> @except";
            cmd.Parameters.AddWithValue("uid", userId);
            if (exceptSessionId is not null)
                cmd.Parameters.AddWithValue("except", exceptSessionId.Value);
            var revokedCount = await cmd.ExecuteNonQueryAsync(ct);

            await AppendCredentialEvidenceAsync(
                conn, tx, "auth.sessions", userId.ToString(), "session_revoke_bulk",
                before: new { }, after: new { sessionsRevoked = revokedCount, exceptSessionId },
                actorUsername, ct);

            await tx.CommitAsync(ct);
            return revokedCount;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public override async Task<Guid?> FindActiveSessionIdByRefreshTokenHashAsync(
        string tokenHash, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT s.session_id
            FROM auth.refresh_tokens rt
            JOIN auth.sessions s ON s.session_id = rt.session_id
            WHERE rt.token_hash = @hash AND rt.revoked_at IS NULL AND s.revoked_at IS NULL
              AND rt.expires_at > now() AND s.expires_at > now()
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("hash", tokenHash);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as Guid?;
    }

    public override async Task<bool> RevokeCredentialAsync(
        Guid userId, string actorUsername, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using var deleteCmd = conn.CreateCommand();
            deleteCmd.Transaction = tx;
            deleteCmd.CommandText = "DELETE FROM auth.credentials WHERE user_id = @id";
            deleteCmd.Parameters.AddWithValue("id", userId);
            var deleted = await deleteCmd.ExecuteNonQueryAsync(ct);
            if (deleted == 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            await using var revokeCmd = conn.CreateCommand();
            revokeCmd.Transaction = tx;
            revokeCmd.CommandText =
                "UPDATE auth.sessions SET revoked_at = now() WHERE user_id = @id AND revoked_at IS NULL";
            revokeCmd.Parameters.AddWithValue("id", userId);
            var revokedCount = await revokeCmd.ExecuteNonQueryAsync(ct);

            await AppendCredentialEvidenceAsync(
                conn, tx, "auth.credentials", userId.ToString(), "admin_credential_revoke",
                before: new { }, after: new { sessionsRevoked = revokedCount }, actorUsername, ct);

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
