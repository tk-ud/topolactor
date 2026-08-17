using Microsoft.Extensions.Logging;

namespace Topolactor.Repository;

// ---------------------------------------------------------------------------
// TeamDashboardRepository — abstract base for topology.team_dashboard_note.
// SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
//   surface_axes.admin.surfaces.team_dashboard /
//   surface_axes.normal.surfaces.dashboard.team_dashboard_canonical_shared_contract
// Production implementation: NpgsqlTeamDashboardRepository
//
// Deliberately a single-row, single-method-pair repository (Get/Update) — the
// team-dashboard note is one shared canonical row, not a collection.
// ---------------------------------------------------------------------------

public record TeamDashboardNote(string NoteId, string Title, string BodyMarkdown, DateTimeOffset UpdatedAt);

public abstract class TeamDashboardRepository
{
    protected readonly ILogger<TeamDashboardRepository> _logger;

    protected TeamDashboardRepository(ILogger<TeamDashboardRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Returns the single shared team-dashboard note, creating it with defaults if none exists yet.</summary>
    public abstract Task<TeamDashboardNote> GetNoteAsync(CancellationToken ct = default);

    /// <summary>Updates the shared note's body_markdown (and optional title). Returns the updated row.</summary>
    public abstract Task<TeamDashboardNote> UpdateNoteAsync(string bodyMarkdown, string? title, CancellationToken ct = default);
}
