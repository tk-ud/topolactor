using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Sets environment variables for the duration of a test and restores originals on Dispose.
/// Not thread-safe — use one scope per test.
/// </summary>
internal sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = [];

    public EnvScope Set(string name, string? value)
    {
        _previous.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
        return this;
    }

    public void Dispose()
    {
        foreach (var (name, prev) in _previous)
            Environment.SetEnvironmentVariable(name, prev);
    }
}

/// <summary>
/// Builds minimal HS256 JWT tokens for test fixtures.
/// Mirrors the encoding logic in AuthEndpoint.IssueJwt — tests must not depend on
/// AuthEndpoint internals, so the builder is kept here as an independent fixture tool.
/// </summary>
internal static class TestJwtBuilder
{
    public static string BuildToken(
        string secret,
        string subject = "testuser",
        string role = "test",
        long? expUnix = null,
        bool includeExp = true)
    {
        var exp = expUnix ?? DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        object payloadObj = includeExp
            ? new { sub = subject, role, exp }
            : (object)new { sub = subject, role };
        return BuildTokenFromPayloadObject(secret, payloadObj);
    }

    /// <summary>
    /// Builds a token where the exp field is a JSON string (invalid type) instead of a number.
    /// Tests AUTH_TOKEN_EXP_INVALID handling.
    /// </summary>
    public static string BuildTokenWithStringExp(string secret)
    {
        var rawPayloadJson = """{"sub":"testuser","role":"test","exp":"not-a-number"}""";
        return BuildTokenFromRawPayload(secret, rawPayloadJson);
    }

    /// <summary>
    /// Builds a token signed with a different secret. Tests AUTH_TOKEN_INVALID_SIGNATURE handling.
    /// </summary>
    public static string BuildTokenWithWrongSignature(string rightSecret, string wrongSecret)
    {
        var token = BuildToken(wrongSecret);
        // Use the payload from the wrong-secret token, but split and rebuild with tampered signature
        var parts = token.Split('.');
        // Sign with wrong key so the guard (using rightSecret) rejects it
        return token; // already signed with wrongSecret
    }

    private static string BuildTokenFromPayloadObject(string secret, object payloadObj)
    {
        var rawPayloadJson = JsonSerializer.Serialize(payloadObj);
        return BuildTokenFromRawPayload(secret, rawPayloadJson);
    }

    private static string BuildTokenFromRawPayload(string secret, string rawPayloadJson)
    {
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "HS256", typ = "JWT" }));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(rawPayloadJson));

        var signingInput = Encoding.UTF8.GetBytes($"{header}.{payload}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = Base64UrlEncode(hmac.ComputeHash(signingInput));

        return $"{header}.{payload}.{sig}";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
