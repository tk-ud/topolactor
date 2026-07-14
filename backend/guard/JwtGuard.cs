using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Guard;

/// <summary>
/// Validates demo JWT Bearer tokens for the demo auth scaffold.
/// JWT secret is read from the DEMO_JWT_SECRET environment variable.
/// Returns explicit validation errors on any failure — no silent fallback.
/// Algorithm: HS256 (HMAC-SHA256).
/// Not for production authentication.
/// </summary>
public class JwtGuard
{
    private const string EnvJwtSecret = "DEMO_JWT_SECRET";

    /// <summary>
    /// Validates a raw JWT token string (without "Bearer " prefix).
    /// Returns empty list on success; returns errors on invalid, missing, or expired token.
    /// </summary>
    public IReadOnlyList<ValidationError> Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return [new ValidationError("AUTH_TOKEN_MISSING", "Authorization token is required.")];

        var secret = Environment.GetEnvironmentVariable(EnvJwtSecret);
        if (string.IsNullOrWhiteSpace(secret))
            return [new ValidationError("AUTH_JWT_SECRET_NOT_CONFIGURED",
                "DEMO_JWT_SECRET environment variable is not set.")];

        var parts = token.Split('.');
        if (parts.Length != 3)
            return [new ValidationError("AUTH_TOKEN_MALFORMED", "Token is not a valid JWT structure.")];

        var signingInput = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);
        var expectedSig = Base64UrlEncode(hmac.ComputeHash(signingInput));

        if (!ConstantTimeEquals(expectedSig, parts[2]))
            return [new ValidationError("AUTH_TOKEN_INVALID_SIGNATURE", "Token signature is invalid.")];

        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);

            if (!doc.RootElement.TryGetProperty("exp", out var expProp))
                return [new ValidationError("AUTH_TOKEN_EXP_MISSING",
                    "Token is missing required exp claim.")];

            if (expProp.ValueKind != JsonValueKind.Number || !expProp.TryGetInt64(out var expUnix))
                return [new ValidationError("AUTH_TOKEN_EXP_INVALID",
                    "Token exp claim is not a valid integer.")];

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expUnix)
                return [new ValidationError("AUTH_TOKEN_EXPIRED", "Token has expired.")];
        }
        catch
        {
            return [new ValidationError("AUTH_TOKEN_MALFORMED", "Token payload could not be decoded.")];
        }

        return [];
    }


    /// <summary>
    /// Extracts JWT subject (sub) from a previously validated token.
    /// Returns null when token/payload is malformed or sub is absent.
    /// </summary>
    public string? TryGetSubject(string? token) => TryGetStringClaim(token, "sub");

    public string? TryGetRole(string? token) => TryGetStringClaim(token, "role");

    public string? TryGetRealm(string? token) => TryGetStringClaim(token, "realm");

    public string? TryGetAudience(string? token) => TryGetStringClaim(token, "aud");

    /// <summary>Extracts the sid (session id / revocation identity) claim as a Guid, or null.</summary>
    public Guid? TryGetSessionId(string? token) =>
        Guid.TryParse(TryGetStringClaim(token, "sid"), out var sessionId) ? sessionId : null;

    public IReadOnlyList<string> TryGetCapabilities(string? token) =>
        TryGetStringArrayClaim(token, "capabilities").Count > 0
            ? TryGetStringArrayClaim(token, "capabilities")
            : TryGetStringArrayClaim(token, "capability");

    /// <summary>Validates token and required realm/audience/role for a surface.</summary>
    public IReadOnlyList<ValidationError> ValidateForContext(
        string? token,
        string expectedRealm,
        string expectedAudience,
        string expectedRole)
    {
        var errors = Validate(token);
        if (errors.Count > 0) return errors;

        var realm = TryGetRealm(token);
        if (!string.Equals(realm, expectedRealm, StringComparison.Ordinal))
            return [new ValidationError("AUTH_TOKEN_REALM_MISMATCH",
                $"Token realm must be '{expectedRealm}'.")];

        var aud = TryGetAudience(token);
        if (!string.Equals(aud, expectedAudience, StringComparison.Ordinal))
            return [new ValidationError("AUTH_TOKEN_AUDIENCE_MISMATCH",
                $"Token audience must be '{expectedAudience}'.")];

        var role = TryGetRole(token);
        if (!string.Equals(role, expectedRole, StringComparison.Ordinal))
            return [new ValidationError("AUTH_TOKEN_ROLE_MISMATCH",
                $"Token role must be '{expectedRole}'.")];

        return [];
    }

    /// <summary>
    /// Full access-JWT validation boundary: signature + exp (via Validate) plus a DB-side check,
    /// through the token's sid claim, that the backing auth.sessions row is still active and the
    /// owning account is still active/approved/not-suspended. Signature+exp alone is NOT sufficient
    /// — a session revoked (password change, session revoke, credential revoke) or an account
    /// deactivated after the token was issued must be rejected here, not only on refresh.
    /// Every endpoint that authenticates a caller must call this (or
    /// ValidateForContextActiveSessionAsync) instead of the signature/exp-only Validate/ValidateForContext.
    /// </summary>
    public async Task<IReadOnlyList<ValidationError>> ValidateActiveSessionAsync(
        string? token, AuthRepository authRepository, CancellationToken ct = default)
    {
        var errors = Validate(token);
        if (errors.Count > 0) return errors;
        return await CheckSessionActiveAsync(token, authRepository, ct);
    }

    /// <summary>Combines ValidateForContext (realm/audience/role) with the DB-side session-active check.</summary>
    public async Task<IReadOnlyList<ValidationError>> ValidateForContextActiveSessionAsync(
        string? token,
        string expectedRealm,
        string expectedAudience,
        string expectedRole,
        AuthRepository authRepository,
        CancellationToken ct = default)
    {
        var errors = ValidateForContext(token, expectedRealm, expectedAudience, expectedRole);
        if (errors.Count > 0) return errors;
        return await CheckSessionActiveAsync(token, authRepository, ct);
    }

    private async Task<IReadOnlyList<ValidationError>> CheckSessionActiveAsync(
        string? token, AuthRepository authRepository, CancellationToken ct)
    {
        var sessionId = TryGetSessionId(token);
        if (sessionId is null)
            return [new ValidationError("AUTH_TOKEN_SID_MISSING", "Token is missing required sid claim.")];

        var active = await authRepository.IsSessionActiveAsync(sessionId.Value, ct);
        if (!active)
            return [new ValidationError("AUTH_SESSION_REVOKED",
                "Session has been revoked, or the account is no longer active.")];

        return [];
    }

    private string? TryGetStringClaim(string? token, string claimName)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty(claimName, out var prop)) return null;
            if (prop.ValueKind != JsonValueKind.String) return null;
            var value = prop.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private IReadOnlyList<string> TryGetStringArrayClaim(string? token, string claimName)
    {
        if (string.IsNullOrWhiteSpace(token)) return [];

        var parts = token.Split('.');
        if (parts.Length != 3) return [];

        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty(claimName, out var prop)) return [];
            if (prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                return string.IsNullOrWhiteSpace(value)
                    ? []
                    : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            if (prop.ValueKind == JsonValueKind.Array)
            {
                return prop.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    .Select(item => item.GetString()!)
                    .ToArray();
            }
        }
        catch
        {
            return [];
        }

        return [];
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
