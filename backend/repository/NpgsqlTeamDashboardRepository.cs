using Microsoft.Extensions.Logging;
using Npgsql;

namespace Topolactor.Repository;

// ---------------------------------------------------------------------------
// NpgsqlTeamDashboardRepository — production Npgsql implementation.
// Operates against topology.team_dashboard_note (db/team_dashboard_tables.sql).
// The table holds exactly one canonical shared row (seeded at
// db/seed_empty.sql, note_id CanonicalNoteId below) — Get/Update always
// target that same row; no silent fallback to an implicit "first row" query.
// ---------------------------------------------------------------------------

public class NpgsqlTeamDashboardRepository : TeamDashboardRepository
{
    private readonly string _connectionString;

    /// <summary>The single canonical row's fixed identity, matching db/seed_empty.sql.</summary>
    public static readonly Guid CanonicalNoteId = new("00000000-0000-0000-0000-0000000dd001");

    public NpgsqlTeamDashboardRepository(ILogger<NpgsqlTeamDashboardRepository> logger, string connectionString)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public override async Task<TeamDashboardNote> GetNoteAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO topology.team_dashboard_note (note_id) VALUES (@id) " +
            "ON CONFLICT (note_id) DO NOTHING;" +
            "SELECT note_id::text, title, body_markdown, updated_at " +
            "FROM topology.team_dashboard_note WHERE note_id = @id";
        cmd.Parameters.AddWithValue("id", CanonicalNoteId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("team_dashboard_note canonical row could not be created or read.");

        return new TeamDashboardNote(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }

    public override async Task<TeamDashboardNote> UpdateNoteAsync(string bodyMarkdown, string? title, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Ensure the canonical row exists before updating it (same defensive upsert as GetNoteAsync)
        // rather than assuming seed data has already run.
        await using (var ensureCmd = conn.CreateCommand())
        {
            ensureCmd.CommandText =
                "INSERT INTO topology.team_dashboard_note (note_id) VALUES (@id) ON CONFLICT (note_id) DO NOTHING";
            ensureCmd.Parameters.AddWithValue("id", CanonicalNoteId);
            await ensureCmd.ExecuteNonQueryAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        if (title is not null)
        {
            cmd.CommandText =
                "UPDATE topology.team_dashboard_note SET body_markdown = @body, title = @title, updated_at = now() " +
                "WHERE note_id = @id " +
                "RETURNING note_id::text, title, body_markdown, updated_at";
            cmd.Parameters.AddWithValue("title", title);
        }
        else
        {
            cmd.CommandText =
                "UPDATE topology.team_dashboard_note SET body_markdown = @body, updated_at = now() " +
                "WHERE note_id = @id " +
                "RETURNING note_id::text, title, body_markdown, updated_at";
        }
        cmd.Parameters.AddWithValue("body", bodyMarkdown);
        cmd.Parameters.AddWithValue("id", CanonicalNoteId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("team_dashboard_note canonical row update returned no row.");

        return new TeamDashboardNote(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }
}
