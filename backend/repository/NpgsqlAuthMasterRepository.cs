using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public class NpgsqlAuthMasterRepository : AuthMasterRepository
{
    private const string UserSelectSql =
        """
        SELECT u.user_id, u.username, u.active, u.approve, u.status,
               u.suspended_from, u.suspended_until, u.state_note,
               u.created_at, u.updated_at,
               (SELECT MAX(le.created_at) FROM auth.login_events le
                WHERE le.user_id = u.user_id AND le.success = true) AS last_login_at
        FROM auth.users u
        """;

    public NpgsqlAuthMasterRepository(string connectionString) : base(connectionString) { }

    public override async Task<IReadOnlyList<AuthUserRosterDto>> ListUsersAsync(
        string? query, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        if (string.IsNullOrWhiteSpace(query))
        {
            cmd.CommandText = UserSelectSql + " ORDER BY u.username";
        }
        else
        {
            cmd.CommandText = UserSelectSql +
                " WHERE u.username ILIKE @q OR COALESCE(u.status, '') ILIKE @q ORDER BY u.username";
            cmd.Parameters.AddWithValue("q", $"%{query.Trim()}%");
        }

        return await ReadRosterListAsync(cmd, ct);
    }

    public override async Task<AuthUserRosterDto?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = UserSelectSql + " WHERE u.user_id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("id", userId);
        var list = await ReadRosterListAsync(cmd, ct);
        return list.FirstOrDefault();
    }

    public override async Task<AuthUserRosterDto> CreateUserAsync(
        string username,
        string passwordHash,
        bool approve,
        string? status,
        string roleName,
        string realm,
        DateTimeOffset? suspendedFrom,
        DateTimeOffset? suspendedUntil,
        string? stateNote,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        Guid userId;
        await using (var userCmd = conn.CreateCommand())
        {
            userCmd.Transaction = tx;
            userCmd.CommandText =
                """
                INSERT INTO auth.users (username, active, approve, status, suspended_from, suspended_until, state_note)
                VALUES (@u, true, @approve, @status, @sf, @su, @note)
                RETURNING user_id
                """;
            userCmd.Parameters.AddWithValue("u", username);
            userCmd.Parameters.AddWithValue("approve", approve);
            userCmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
            userCmd.Parameters.AddWithValue("sf", (object?)suspendedFrom ?? DBNull.Value);
            userCmd.Parameters.AddWithValue("su", (object?)suspendedUntil ?? DBNull.Value);
            userCmd.Parameters.AddWithValue("note", (object?)stateNote ?? DBNull.Value);
            userId = (Guid)(await userCmd.ExecuteScalarAsync(ct))!;
        }

        await using (var credCmd = conn.CreateCommand())
        {
            credCmd.Transaction = tx;
            credCmd.CommandText =
                "INSERT INTO auth.credentials (user_id, password_hash) VALUES (@id, @hash)";
            credCmd.Parameters.AddWithValue("id", userId);
            credCmd.Parameters.AddWithValue("hash", passwordHash);
            await credCmd.ExecuteNonQueryAsync(ct);
        }

        await using (var grantCmd = conn.CreateCommand())
        {
            grantCmd.Transaction = tx;
            grantCmd.CommandText =
                "INSERT INTO auth.grants (user_id, role_name, realm) VALUES (@id, @role, @realm)";
            grantCmd.Parameters.AddWithValue("id", userId);
            grantCmd.Parameters.AddWithValue("role", roleName);
            grantCmd.Parameters.AddWithValue("realm", realm);
            await grantCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return (await GetUserAsync(userId, ct))!;
    }

    public override async Task<AuthUserRosterDto?> UpdateUserAsync(
        Guid userId,
        string? username,
        bool? active,
        bool? approve,
        string? status,
        DateTimeOffset? suspendedFrom,
        DateTimeOffset? suspendedUntil,
        bool clearSuspendedFrom,
        bool clearSuspendedUntil,
        string? stateNote,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        var sets = new List<string> { "updated_at = now()" };
        if (username is not null) { sets.Add("username = @username"); cmd.Parameters.AddWithValue("username", username); }
        if (active.HasValue) { sets.Add("active = @active"); cmd.Parameters.AddWithValue("active", active.Value); }
        if (approve.HasValue) { sets.Add("approve = @approve"); cmd.Parameters.AddWithValue("approve", approve.Value); }
        if (status is not null) { sets.Add("status = @status"); cmd.Parameters.AddWithValue("status", status); }
        if (clearSuspendedFrom) sets.Add("suspended_from = NULL");
        else if (suspendedFrom.HasValue) { sets.Add("suspended_from = @sf"); cmd.Parameters.AddWithValue("sf", suspendedFrom.Value); }
        if (clearSuspendedUntil) sets.Add("suspended_until = NULL");
        else if (suspendedUntil.HasValue) { sets.Add("suspended_until = @su"); cmd.Parameters.AddWithValue("su", suspendedUntil.Value); }
        if (stateNote is not null) { sets.Add("state_note = @note"); cmd.Parameters.AddWithValue("note", stateNote); }

        cmd.CommandText = $"UPDATE auth.users SET {string.Join(", ", sets)} WHERE user_id = @id";
        cmd.Parameters.AddWithValue("id", userId);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0) return null;
        return await GetUserAsync(userId, ct);
    }

    public override async Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM auth.users WHERE user_id = @id";
        cmd.Parameters.AddWithValue("id", userId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public override async Task<bool> UsernameExistsAsync(
        string username, Guid? excludeUserId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = excludeUserId.HasValue
            ? "SELECT 1 FROM auth.users WHERE username = @u AND user_id <> @id LIMIT 1"
            : "SELECT 1 FROM auth.users WHERE username = @u LIMIT 1";
        cmd.Parameters.AddWithValue("u", username);
        if (excludeUserId.HasValue) cmd.Parameters.AddWithValue("id", excludeUserId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    private static async Task<IReadOnlyList<AuthUserRosterDto>> ReadRosterListAsync(
        NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<AuthUserRosterDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AuthUserRosterDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetBoolean(2),
                reader.GetBoolean(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
                reader.IsDBNull(6) ? null : new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                new DateTimeOffset(reader.GetDateTime(8), TimeSpan.Zero),
                new DateTimeOffset(reader.GetDateTime(9), TimeSpan.Zero),
                reader.IsDBNull(10) ? null : new DateTimeOffset(reader.GetDateTime(10), TimeSpan.Zero)));
        }

        return list;
    }
}
