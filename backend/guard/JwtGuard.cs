using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

            if (!expProp.TryGetInt64(out var expUnix))
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
