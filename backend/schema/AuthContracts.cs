using System.Text.Json.Serialization;

namespace Topolactor.Schema;

/// <summary>
/// Inbound DTO for the demo auth login endpoint.
/// </summary>
public record LoginRequestDto(string? Username, string? Password);

/// <summary>
/// Outbound DTO returned after a login attempt.
/// Token is null on failure. Errors is empty on success.
/// </summary>
public record LoginResponseDto(bool Success, string? Token, IReadOnlyList<ValidationError> Errors);

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

public static class AuthManifestIds
{
    public static readonly Guid UserLoginManifestId =
        Guid.Parse("00000000-0000-0000-0000-000000000090");
}
