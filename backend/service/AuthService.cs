using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Service;

public class AuthService
{
    private const int RefreshTokenDays = 7;

    private readonly ILogger<AuthService> _logger;
    private readonly AuthRepository _authRepository;
    private readonly JwtTokenIssuer _jwtTokenIssuer;

    public AuthService(
        ILogger<AuthService> logger,
        AuthRepository authRepository,
        JwtTokenIssuer jwtTokenIssuer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        _jwtTokenIssuer = jwtTokenIssuer ?? throw new ArgumentNullException(nameof(jwtTokenIssuer));
    }

    public async Task<RegisterResponseDto> RegisterUserAsync(
        RegisterRequestDto? request,
        CancellationToken ct = default)
    {
        if (request is null)
            return RegisterFail("AUTH_REGISTER_REQUEST_NULL", "Registration request must not be null.");

        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
            return RegisterFail("AUTH_REGISTER_CREDENTIALS_REQUIRED", "Username and password are required.");

        var existing = await _authRepository.FindUserByUsernameAsync(username, ct);
        if (existing is not null)
            return RegisterFail("AUTH_REGISTER_USERNAME_EXISTS", "Username is already registered.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = await _authRepository.CreatePendingUserWithCredentialAsync(username, passwordHash, ct);

        _logger.LogDebug("Normal user registration created pending approval username='{Username}'.", user.Username);
        return new RegisterResponseDto(true, user.Username, user.Approve, user.Status, []);
    }

    public async Task<(LoginResponseDto Response, string? RefreshTokenPlaintext)> LoginAsync(
        LoginRequestDto? request,
        AuthRealmContext realmContext,
        CancellationToken ct = default)
    {
        if (request is null)
            return (Fail("AUTH_REQUEST_NULL", "Login request must not be null."), null);

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return (Fail("AUTH_CREDENTIALS_REQUIRED", "Username and password are required."), null);

        var configErrors = _jwtTokenIssuer.ValidateConfiguration();
        if (configErrors.Count > 0)
            return (Fail(configErrors[0].Code, configErrors[0].Message), null);

        var user = await _authRepository.FindUserByUsernameAsync(request.Username.Trim(), ct);
        if (user is null)
        {
            await _authRepository.InsertLoginEventAsync(null, realmContext.Realm, false,
                "AUTH_INVALID_CREDENTIALS", ct);
            return (Fail("AUTH_INVALID_CREDENTIALS", "Invalid username or password."), null);
        }

        var loginBlock = EvaluateLoginState(user);
        if (loginBlock is not null)
        {
            await _authRepository.InsertLoginEventAsync(user.UserId, realmContext.Realm, false,
                loginBlock.Value.Code, ct);
            return (Fail(loginBlock.Value.Code, loginBlock.Value.Message), null);
        }

        var hash = await _authRepository.GetPasswordHashAsync(user.UserId, ct);
        if (hash is null || !BCrypt.Net.BCrypt.Verify(request.Password, hash))
        {
            await _authRepository.InsertLoginEventAsync(user.UserId, realmContext.Realm, false,
                "AUTH_INVALID_CREDENTIALS", ct);
            _logger.LogWarning("Login failed for username='{Username}' realm='{Realm}'.",
                request.Username, realmContext.Realm);
            return (Fail("AUTH_INVALID_CREDENTIALS", "Invalid username or password."), null);
        }

        var grantRole = await _authRepository.GetGrantRoleForRealmAsync(user.UserId, realmContext.Realm, ct);
        if (grantRole is null || !string.Equals(grantRole, realmContext.Role, StringComparison.Ordinal))
        {
            await _authRepository.InsertLoginEventAsync(user.UserId, realmContext.Realm, false,
                "AUTH_REALM_DENIED", ct);
            return (Fail("AUTH_REALM_DENIED", "User is not authorized for this login realm."), null);
        }

        var sessionExpires = DateTimeOffset.UtcNow.AddDays(RefreshTokenDays);
        var sessionId = await _authRepository.CreateSessionAsync(
            user.UserId, realmContext.Realm, realmContext.Audience, sessionExpires, ct);

        var refreshPlain = JwtTokenIssuer.GenerateRefreshTokenPlaintext();
        var refreshHash = JwtTokenIssuer.HashRefreshToken(refreshPlain);
        await _authRepository.CreateRefreshTokenAsync(
            sessionId, refreshHash, sessionExpires, ct);

        var token = _jwtTokenIssuer.IssueAccessToken(user.Username, realmContext);
        await _authRepository.InsertLoginEventAsync(user.UserId, realmContext.Realm, true, null, ct);

        _logger.LogDebug("Login succeeded username='{Username}' realm='{Realm}'.",
            user.Username, realmContext.Realm);
        return (new LoginResponseDto(true, token, []), refreshPlain);
    }

    public async Task<(RefreshResponseDto Response, string? RefreshTokenPlaintext)> RefreshAsync(
        string? refreshTokenPlain,
        AuthRealmContext expectedRealm,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return (RefreshFail("AUTH_REFRESH_TOKEN_REQUIRED", "Refresh token is required."), null);

        var configErrors = _jwtTokenIssuer.ValidateConfiguration();
        if (configErrors.Count > 0)
            return (RefreshFail(configErrors[0].Code, configErrors[0].Message), null);

        var hash = JwtTokenIssuer.HashRefreshToken(refreshTokenPlain);
        var record = await _authRepository.FindActiveRefreshTokenByHashAsync(hash, ct);
        if (record is null)
            return (RefreshFail("AUTH_REFRESH_TOKEN_INVALID", "Refresh token is invalid or expired."), null);

        if (!string.Equals(record.Realm, expectedRealm.Realm, StringComparison.Ordinal) ||
            !string.Equals(record.Audience, expectedRealm.Audience, StringComparison.Ordinal))
            return (RefreshFail("AUTH_REALM_MISMATCH", "Refresh token realm does not match."), null);

        var user = await _authRepository.GetUserStateByIdAsync(record.UserId, ct);
        if (user is null)
        {
            await _authRepository.RevokeRefreshTokenAsync(record.RefreshTokenId, ct);
            await _authRepository.RevokeSessionAsync(record.SessionId, ct);
            return (RefreshFail("AUTH_REFRESH_USER_NOT_FOUND", "User account no longer exists."), null);
        }

        var stateBlock = EvaluateLoginState(user);
        if (stateBlock is not null)
        {
            await _authRepository.RevokeRefreshTokenAsync(record.RefreshTokenId, ct);
            await _authRepository.RevokeSessionAsync(record.SessionId, ct);
            return (RefreshFail(stateBlock.Value.Code, stateBlock.Value.Message), null);
        }

        await _authRepository.RevokeRefreshTokenAsync(record.RefreshTokenId, ct);

        var sessionExpires = DateTimeOffset.UtcNow.AddDays(RefreshTokenDays);
        var newRefreshPlain = JwtTokenIssuer.GenerateRefreshTokenPlaintext();
        var newRefreshHash = JwtTokenIssuer.HashRefreshToken(newRefreshPlain);
        await _authRepository.CreateRefreshTokenAsync(record.SessionId, newRefreshHash, sessionExpires, ct);

        var realmContext = new AuthRealmContext(record.Realm, record.Audience, record.Role);
        var accessToken = _jwtTokenIssuer.IssueAccessToken(record.Username, realmContext);
        return (new RefreshResponseDto(true, accessToken, []), newRefreshPlain);
    }

    public async Task<LogoutResponseDto> LogoutAsync(
        string? refreshTokenPlain,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return new LogoutResponseDto(false,
                [new ValidationError("AUTH_REFRESH_TOKEN_REQUIRED", "Refresh token is required.")]);

        var hash = JwtTokenIssuer.HashRefreshToken(refreshTokenPlain);
        var record = await _authRepository.FindActiveRefreshTokenByHashAsync(hash, ct);
        if (record is null)
            return new LogoutResponseDto(true, []);

        await _authRepository.RevokeRefreshTokenAsync(record.RefreshTokenId, ct);
        await _authRepository.RevokeSessionAsync(record.SessionId, ct);
        return new LogoutResponseDto(true, []);
    }

    private static (string Code, string Message)? EvaluateLoginState(AuthUserRecord user)
    {
        if (!user.Active)
            return ("AUTH_USER_INACTIVE", "User account is inactive.");
        if (!user.Approve)
            return ("AUTH_USER_NOT_APPROVED", "User account is not approved.");
        if (string.Equals(user.Status, "suspended", StringComparison.OrdinalIgnoreCase))
            return ("AUTH_USER_SUSPENDED", "User account is suspended.");
        if (user.SuspendedFrom.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            var from = user.SuspendedFrom.Value;
            if (from <= now && (user.SuspendedUntil is null || now <= user.SuspendedUntil.Value))
            {
                return ("AUTH_USER_SUSPENDED", "User account is within a suspension window.");
            }
        }

        return null;
    }

    private static LoginResponseDto Fail(string code, string message) =>
        new(false, null, [new ValidationError(code, message)]);

    private static RegisterResponseDto RegisterFail(string code, string message) =>
        new(false, null, null, null, [new ValidationError(code, message)]);

    private static RefreshResponseDto RefreshFail(string code, string message) =>
        new(false, null, [new ValidationError(code, message)]);
}
