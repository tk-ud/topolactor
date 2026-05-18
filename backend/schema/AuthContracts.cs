using System.Text.Json.Serialization;

namespace Topolactor.Schema;

/// <summary>
/// Inbound DTO for the demo auth login endpoint.
/// </summary>
public record LoginRequestDto(string? Username, string? Password);

/// <summary>
/// Outbound DTO returned after a demo login attempt.
/// Token is null on failure. Errors is empty on success.
/// </summary>
public record LoginResponseDto(bool Success, string? Token, IReadOnlyList<ValidationError> Errors);

/// <summary>
/// A single demo credential record stored in function_parameters under
/// function_name='demo_auth', parameter_key='demo_users'.
/// PasswordHash is a bcrypt hash. Role is the demo role granted on success.
/// </summary>
public record DemoCredential(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password_hash")] string PasswordHash,
    [property: JsonPropertyName("role")] string Role
);
