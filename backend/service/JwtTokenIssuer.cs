using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Service;

/// <summary>Issues and validates HS256 JWT access tokens with realm/audience claims.</summary>
public class JwtTokenIssuer
{
    private const string EnvJwtSecret = "DEMO_JWT_SECRET";
    private const string EnvJwtIssuer = "DEMO_JWT_ISSUER";
    private const string EnvJwtExpiryHours = "DEMO_JWT_EXPIRY_HOURS";

    public IReadOnlyList<ValidationError> ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvJwtSecret)))
            return [new ValidationError("AUTH_JWT_SECRET_NOT_CONFIGURED",
                "DEMO_JWT_SECRET environment variable is not set.")];

        var expiryHoursStr = Environment.GetEnvironmentVariable(EnvJwtExpiryHours);
        if (!int.TryParse(expiryHoursStr, out var expiryHours) || expiryHours <= 0)
            return [new ValidationError("AUTH_JWT_EXPIRY_NOT_CONFIGURED",
                "DEMO_JWT_EXPIRY_HOURS environment variable is not set or is not a positive integer.")];

        return [];
    }

    /// <summary>
    /// Issues an access token carrying the revocation identity (sid = session id) so JwtGuard can
    /// verify DB-side, on every request, that the session backing this token is still active.
    /// sessionId must be the auth.sessions row created for this login/refresh — never a client-
    /// supplied value.
    /// </summary>
    public string IssueAccessToken(string username, AuthRealmContext realmContext, Guid sessionId)
    {
        var configErrors = ValidateConfiguration();
        if (configErrors.Count > 0)
            throw new InvalidOperationException(configErrors[0].Message);

        var secret = Environment.GetEnvironmentVariable(EnvJwtSecret)!;
        var expiryHoursStr = Environment.GetEnvironmentVariable(EnvJwtExpiryHours)!;
        _ = int.TryParse(expiryHoursStr, out var expiryHours);

        var issuer = Environment.GetEnvironmentVariable(EnvJwtIssuer) ?? "topolactor-demo";

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "HS256", typ = "JWT" }));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = username,
            role = realmContext.Role,
            realm = realmContext.Realm,
            aud = realmContext.Audience,
            sid = sessionId.ToString(),
            iss = issuer,
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            exp = DateTimeOffset.UtcNow.AddHours(expiryHours).ToUnixTimeSeconds(),
        }));

        var signingInput = Encoding.UTF8.GetBytes($"{header}.{payload}");
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);
        var sig = Base64UrlEncode(hmac.ComputeHash(signingInput));

        return $"{header}.{payload}.{sig}";
    }

    public static string HashRefreshToken(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateRefreshTokenPlaintext()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
