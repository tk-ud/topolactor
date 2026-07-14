using System.Text.Json.Serialization;

namespace Topolactor.Schema;

/// <summary>
/// Inbound DTO for the auth login endpoint.
/// </summary>
public record LoginRequestDto(string? Username, string? Password);

/// <summary>
/// Outbound DTO returned after a login attempt.
/// Token is null on failure. Errors is empty on success.
/// </summary>
public record LoginResponseDto(bool Success, string? Token, IReadOnlyList<ValidationError> Errors);

public record RegisterRequestDto(string? Username, string? Password);

public record RegisterResponseDto(
    bool Success,
    string? Username,
    bool? Approve,
    string? Status,
    IReadOnlyList<ValidationError> Errors);

public record RefreshRequestDto(string? RefreshToken);

public record RefreshResponseDto(bool Success, string? Token, IReadOnlyList<ValidationError> Errors);

public record LogoutRequestDto(string? RefreshToken);

public record LogoutResponseDto(bool Success, IReadOnlyList<ValidationError> Errors);

/// <summary>
/// Outbound DTO for GET /auth/session — validates Bearer JWT without side effects.
/// </summary>
public record SessionResponseDto(
    bool Success,
    string? Subject,
    string? Role,
    string? Realm,
    string? Audience,
    IReadOnlyList<ValidationError> Errors);

public static class AuthCookieNames
{
    public const string RefreshToken = "topolactor_refresh_token";
}

// ─── Self-service credential/session lifecycle (auth_runtime, not manifest-dispatched) ─────────
// Target account for every DTO in this section is always resolved server-side from the validated
// JWT subject — none of these DTOs carry a userId/username field, by design, so a caller can never
// name a different account to act on.

/// <summary>GET /auth/me — current authenticated account, sourced from validated JWT + auth.users.</summary>
public record CurrentAccountResponseDto(
    bool Success,
    string? Username,
    string? Role,
    string? Realm,
    bool? Active,
    bool? Approve,
    string? Status,
    IReadOnlyList<ValidationError> Errors);

/// <summary>POST /auth/me/password — self-service password change. No userId field: target is always the JWT subject.</summary>
public record ChangeOwnPasswordRequestDto(string? CurrentPassword, string? NewPassword);

public record ChangeOwnPasswordResponseDto(
    bool Success, int SessionsRevoked, IReadOnlyList<ValidationError> Errors);

public record SessionSummaryDto(
    [property: JsonPropertyName("sessionId")] Guid SessionId,
    [property: JsonPropertyName("realm")] string Realm,
    [property: JsonPropertyName("audience")] string Audience,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("isCurrent")] bool IsCurrent);

public record ListSessionsResponseDto(
    bool Success, IReadOnlyList<SessionSummaryDto>? Sessions, IReadOnlyList<ValidationError> Errors);

/// <summary>POST /auth/me/sessions/revoke — revoke one of the caller's own sessions by id.</summary>
public record RevokeOwnSessionRequestDto(string? SessionId);

public record RevokeOwnSessionResponseDto(bool Success, IReadOnlyList<ValidationError> Errors);

public record RevokeOtherSessionsResponseDto(
    bool Success, int SessionsRevoked, IReadOnlyList<ValidationError> Errors);

// ─── Admin-driven credential/session operations (thin HTTP boundary, admin JWT required) ───────
// Admin can act on any userId (route parameter), but never supplies or reads a password value —
// these operations only revoke; they never set/replace another user's password.

public record AdminRevokeSessionsRequestDto(string? SessionId);

public record AdminRevokeSessionsResponseDto(
    bool Success, int SessionsRevoked, IReadOnlyList<ValidationError> Errors);

public record AdminRevokeCredentialResponseDto(bool Success, IReadOnlyList<ValidationError> Errors);

public record AdminListSessionsResponseDto(
    bool Success, IReadOnlyList<SessionSummaryDto>? Sessions, IReadOnlyList<ValidationError> Errors);

public static class AuthManifestIds
{
    public static readonly Guid UserLoginManifestId =
        Guid.Parse("00000000-0000-0000-0000-000000000090");
}
