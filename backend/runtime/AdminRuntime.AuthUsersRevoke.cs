using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — credentials.users revoke_credential / revoke_sessions
// (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
//  credentials.categories.users.transaction_contract).
//
// Entry: AdminRuntime.ExecuteDataAsync layer=auth_users action=revoke_credential|revoke_sessions
//
// continuation_refinement_not_duplicate_authority: these two actions are thin admin_runtime
// wrappers around the SAME AuthService.AdminRevokeCredentialAsync/AdminRevokeSessionsAsync methods
// backend/Program.cs's existing POST /admin/auth/users/{userId}/credential/revoke and
// /admin/auth/users/{userId}/sessions/revoke REST routes already call -- no revoke business logic
// is duplicated here. This is purely an additional, seed-driven admin_runtime dispatch entry point
// onto that one canonical authority, so credential-management's UI can reach it through the same
// generic admin_runtime_dispatch_override_wiring lane every other action in this Bundle uses,
// instead of a credential-management-specific raw fetch bypassing /dispatch.
//
// Never accepts or stores a replacement password value (revoke_credential is a kill-switch:
// deletes the credential row; it does not take or persist any secret material). No
// mutation_confirmation_contract dryRun preview is added here for the same reason
// AdminRuntimeMasterRoster.cs's auth_users:create/update/delete additions explain: these
// underlying REST routes have no dryRun concept today and are called directly, unconfirmed, by
// their own existing (non-credential-management) callers if any exist -- adding a hard
// confirmed-required gate onto AuthService itself is out of scope. credential-management's OWN
// seed UI still shows an explicit confirm modal before dispatching (client-side confirm gate),
// consistent with never issuing a revoke from a single accidental click.
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    private static ValidationError AuthServiceNotAvailable() =>
        new("AUTH_SERVICE_NOT_AVAILABLE", "AuthService is not registered.");

    private async Task<(JsonElement? data, ValidationError? error)>
        DataAuthUsersRevokeCredentialAsync(OperationVector vector, CancellationToken ct)
    {
        if (_authService is null)
            return (null, AuthServiceNotAvailable());

        var userId = ParseUserIdFromPayload(vector.Payload);
        if (userId is null)
            return (null, new ValidationError("AUTH_USER_ID_MALFORMED", "userId must be a valid UUID."));

        var actor = ResolveAuditActor(vector) ?? "admin";
        var result = await _authService.AdminRevokeCredentialAsync(userId.Value, actor, ct);
        if (!result.Success)
        {
            var error = result.Errors.Count > 0
                ? result.Errors[0]
                : new ValidationError("AUTH_CREDENTIAL_REVOKE_FAILED", "Credential revoke did not succeed.");
            return (null, error);
        }

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, actor,
            "auth.credentials", userId.Value.ToString(), "revoke_credential",
            new { userId = userId.Value }, new { userId = userId.Value, revoked = true },
            [new AuditChangedField("credential_revoked", false, true)], ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, userId = userId.Value }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataAuthUsersRevokeSessionsAsync(OperationVector vector, CancellationToken ct)
    {
        if (_authService is null)
            return (null, AuthServiceNotAvailable());

        var userId = ParseUserIdFromPayload(vector.Payload);
        if (userId is null)
            return (null, new ValidationError("AUTH_USER_ID_MALFORMED", "userId must be a valid UUID."));

        string? sessionId = null;
        if (vector.Payload is { ValueKind: JsonValueKind.Object } payload &&
            payload.TryGetProperty("sessionId", out var sessionIdEl) && sessionIdEl.ValueKind == JsonValueKind.String)
            sessionId = sessionIdEl.GetString();

        var actor = ResolveAuditActor(vector) ?? "admin";
        var result = await _authService.AdminRevokeSessionsAsync(
            userId.Value, new AdminRevokeSessionsRequestDto(sessionId), actor, ct);
        if (!result.Success)
        {
            var error = result.Errors.Count > 0
                ? result.Errors[0]
                : new ValidationError("AUTH_SESSIONS_REVOKE_FAILED", "Session revoke did not succeed.");
            return (null, error);
        }

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, actor,
            "auth.sessions", userId.Value.ToString(), "revoke_sessions",
            new { userId = userId.Value, sessionId }, new { userId = userId.Value, sessionsRevoked = result.SessionsRevoked },
            [new AuditChangedField("sessions_revoked", 0, result.SessionsRevoked)], ct);

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            userId = userId.Value,
            sessionsRevoked = result.SessionsRevoked,
        }), null);
    }
}
