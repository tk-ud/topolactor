using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — team-dashboard shared note actions.
// SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
//   surface_axes.admin.surfaces.team_dashboard /
//   surface_axes.normal.surfaces.dashboard.team_dashboard_canonical_shared_contract
// Entry: AdminRuntime.ExecuteDataAsync layer=team_dashboard actions: get | update
//
// get is open to any authenticated caller reaching admin_runtime (both the admin
// edit surface and the normal read-only surface dispatch it against their own
// manifest). update additionally requires AuthenticatedRole=="admin" — an
// explicit, non-spoofable, in-method check (AuthenticatedRole is sourced only
// from the JWT-verified DispatchAuthContext stamp, never from client-supplied
// context), the SAME idiom AdminRuntime.TeamMarkdown.cs ExecuteTeamMarkdownAsync
// already established for its own mutation actions, reused verbatim for a
// second domain rather than re-invented.
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    internal async Task<(JsonElement? data, ValidationError? error)>
        ExecuteTeamDashboardAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamDashboardRepository is null)
            return (null, new ValidationError("TEAM_DASHBOARD_NOT_CONFIGURED", "TeamDashboardRepository is not registered"));

        var action = vector.Action ?? "";

        if (string.Equals(action, "update", StringComparison.Ordinal) &&
            !string.Equals(vector.AuthenticatedRole, "admin", StringComparison.Ordinal))
        {
            return (null, new ValidationError("AUTH_CAPABILITY_DENIED",
                $"team_dashboard:{action} requires admin role."));
        }

        return action switch
        {
            "get" => await DataTeamDashboardGetAsync(ct),
            "update" => await DataTeamDashboardUpdateAsync(vector, ct),
            _ => (null, new ValidationError("TEAM_DASHBOARD_UNKNOWN_ACTION", $"Unknown team_dashboard action: {action}"))
        };
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataTeamDashboardGetAsync(CancellationToken ct)
    {
        var note = await _teamDashboardRepository!.GetNoteAsync(ct);
        return (JsonSerializer.SerializeToElement(new
        {
            noteId = note.NoteId,
            title = note.Title,
            bodyMarkdown = note.BodyMarkdown,
            updatedAt = note.UpdatedAt,
        }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataTeamDashboardUpdateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("TEAM_DASHBOARD_PAYLOAD_REQUIRED", "payload is required."));

        var payload = vector.Payload.Value;
        if (!payload.TryGetProperty("bodyMarkdown", out var bodyEl) || bodyEl.ValueKind != JsonValueKind.String)
            return (null, new ValidationError("BODY_MARKDOWN_REQUIRED", "payload.bodyMarkdown must be a string."));

        string? title = payload.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String
            ? titleEl.GetString()
            : null;

        var note = await _teamDashboardRepository!.UpdateNoteAsync(bodyEl.GetString() ?? "", title, ct);
        return (JsonSerializer.SerializeToElement(new
        {
            noteId = note.NoteId,
            title = note.Title,
            bodyMarkdown = note.BodyMarkdown,
            updatedAt = note.UpdatedAt,
        }), null);
    }
}
