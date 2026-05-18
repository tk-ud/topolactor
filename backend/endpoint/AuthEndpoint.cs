using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

/// <summary>
/// Demo auth endpoint scaffold. Validates demo credentials stored in
/// function_parameters (function_name='demo_auth', parameter_key='demo_users')
/// and issues a minimal HS256 JWT on success.
///
/// Environment variables (required / optional):
///   DEMO_JWT_SECRET       — required; shared secret for HS256 signing
///   DEMO_JWT_ISSUER       — optional; defaults to "topolactor-demo"
///   DEMO_JWT_EXPIRY_HOURS — optional; integer; defaults to 8
///
/// Demo credentials are stored as a JSON array in function_parameters.
/// Password hashes are bcrypt (BCrypt.Net-Next). Not for production auth.
/// </summary>
public class AuthEndpoint
{
    private const string EnvJwtSecret = "DEMO_JWT_SECRET";
    private const string EnvJwtIssuer = "DEMO_JWT_ISSUER";
    private const string EnvJwtExpiryHours = "DEMO_JWT_EXPIRY_HOURS";
    private const string DemoAuthFunction = "demo_auth";
    private const string DemoUsersKey = "demo_users";

    private readonly ILogger<AuthEndpoint> _logger;
    private readonly TopologyRepository _topologyRepository;

    public AuthEndpoint(ILogger<AuthEndpoint> logger, TopologyRepository topologyRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    public async Task<LoginResponseDto> HandleAsync(
        LoginRequestDto request,
        CancellationToken ct = default)
    {
        if (request is null)
            return Fail("AUTH_REQUEST_NULL", "Login request must not be null.");

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Fail("AUTH_CREDENTIALS_REQUIRED", "Username and password are required.");

        var secret = Environment.GetEnvironmentVariable(EnvJwtSecret);
        if (string.IsNullOrWhiteSpace(secret))
            return Fail("AUTH_JWT_SECRET_NOT_CONFIGURED", "DEMO_JWT_SECRET environment variable is not set.");

        var credentialsJson = await _topologyRepository.LoadFunctionParameterAsync(
            DemoAuthFunction, DemoUsersKey, ct);
        if (credentialsJson is null)
            return Fail("AUTH_CREDENTIAL_NOT_CONFIGURED",
                "Demo credentials not found in function_parameters (demo_auth/demo_users).");

        DemoCredential[]? credentials;
        try
        {
            credentials = JsonSerializer.Deserialize<DemoCredential[]>(credentialsJson);
        }
        catch (JsonException)
        {
            return Fail("AUTH_CREDENTIAL_MALFORMED",
                "Demo credentials in function_parameters could not be parsed.");
        }

        if (credentials is null || credentials.Length == 0)
            return Fail("AUTH_CREDENTIAL_NOT_CONFIGURED", "Demo credential list is empty.");

        var expiryHoursStr = Environment.GetEnvironmentVariable(EnvJwtExpiryHours);
        if (!int.TryParse(expiryHoursStr, out var expiryHours) || expiryHours <= 0)
            return Fail("AUTH_JWT_EXPIRY_NOT_CONFIGURED",
                "DEMO_JWT_EXPIRY_HOURS environment variable is not set or is not a positive integer.");

        var match = Array.Find(credentials,
            c => string.Equals(c.Username, request.Username, StringComparison.OrdinalIgnoreCase));

        if (match is null || !BCrypt.Net.BCrypt.Verify(request.Password, match.PasswordHash))
        {
            _logger.LogWarning("Demo login failed for username='{Username}'.", request.Username);
            return Fail("AUTH_INVALID_CREDENTIALS", "Invalid username or password.");
        }

        var token = IssueJwt(match.Username, match.Role, secret, expiryHours);
        _logger.LogDebug("Demo login succeeded for username='{Username}' role='{Role}'.",
            match.Username, match.Role);
        return new LoginResponseDto(Success: true, Token: token, Errors: []);
    }

    private string IssueJwt(string username, string role, string secret, int expiryHours)
    {
        var issuer = Environment.GetEnvironmentVariable(EnvJwtIssuer) ?? "topolactor-demo";

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "HS256", typ = "JWT" }));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = username,
            role,
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

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static LoginResponseDto Fail(string code, string message) =>
        new(Success: false, Token: null, Errors: [new ValidationError(code, message)]);
}
