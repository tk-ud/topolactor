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

    /// <summary>
    /// Projection surface login: tries admin realm first (admin capability preserved),
    /// falls through to user realm when the user has no admin grant.
    /// Returns the first non-realm-denied failure verbatim (bad credentials, account state, etc.).
    /// </summary>
    public async Task<(LoginResponseDto Response, string? RefreshTokenPlaintext)> LoginProjectionAsync(
        LoginRequestDto? request,
        CancellationToken ct = default)
    {
        var adminResult = await LoginAsync(request, AuthRealm.Admin, ct);
        if (adminResult.Response.Success)
            return adminResult;

        var isRealmDenied = adminResult.Response.Errors.Count == 1 &&
            adminResult.Response.Errors[0].Code == "AUTH_REALM_DENIED";
        if (!isRealmDenied)
            return adminResult;

        return await LoginAsync(request, AuthRealm.User, ct);
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

    // ─── Self-service credential/session lifecycle ───────────────────────────────────────────
    // Every method below resolves the target account exclusively from jwtSubject (the validated
    // JWT sub claim = username). No method accepts a userId/username argument from the caller.

    public async Task<CurrentAccountResponseDto> GetCurrentAccountAsync(
        string jwtSubject, string jwtRole, string jwtRealm, CancellationToken ct = default)
    {
        var user = await _authRepository.FindUserByUsernameAsync(jwtSubject, ct);
        if (user is null)
            return new CurrentAccountResponseDto(false, null, null, null, null, null, null,
                [new ValidationError("AUTH_USER_NOT_FOUND", "Authenticated account no longer exists.")]);

        return new CurrentAccountResponseDto(
            true, user.Username, jwtRole, jwtRealm, user.Active, user.Approve, user.Status, []);
    }

    public const int MinimumPasswordLength = 8;

    public async Task<ChangeOwnPasswordResponseDto> ChangeOwnPasswordAsync(
        string jwtSubject, ChangeOwnPasswordRequestDto? request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request?.CurrentPassword) || string.IsNullOrWhiteSpace(request?.NewPassword))
            return new ChangeOwnPasswordResponseDto(false, 0,
                [new ValidationError("AUTH_PASSWORD_CHANGE_FIELDS_REQUIRED", "currentPassword and newPassword are required.")]);

        if (request.NewPassword.Length < MinimumPasswordLength)
            return new ChangeOwnPasswordResponseDto(false, 0,
                [new ValidationError("AUTH_PASSWORD_POLICY_TOO_SHORT", $"newPassword must be at least {MinimumPasswordLength} characters.")]);

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            return new ChangeOwnPasswordResponseDto(false, 0,
                [new ValidationError("AUTH_PASSWORD_POLICY_UNCHANGED", "newPassword must differ from currentPassword.")]);

        var user = await _authRepository.FindUserByUsernameAsync(jwtSubject, ct);
        if (user is null)
            return new ChangeOwnPasswordResponseDto(false, 0,
                [new ValidationError("AUTH_USER_NOT_FOUND", "Authenticated account no longer exists.")]);

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        var result = await _authRepository.ChangeOwnPasswordAsync(user.UserId, request.CurrentPassword, newHash, jwtSubject, ct);

        return result.Outcome switch
        {
            ChangeOwnPasswordOutcome.Success => new ChangeOwnPasswordResponseDto(true, result.SessionsRevoked, []),
            ChangeOwnPasswordOutcome.CurrentPasswordInvalid => new ChangeOwnPasswordResponseDto(false, 0,
                [new ValidationError("AUTH_CURRENT_PASSWORD_INVALID", "Current password is incorrect.")]),
            _ => new ChangeOwnPasswordResponseDto(false, 0,
                [new ValidationError("AUTH_CREDENTIAL_NOT_FOUND", "No credential is registered for this account.")]),
        };
    }

    public async Task<ListSessionsResponseDto> ListOwnSessionsAsync(
        string jwtSubject, Guid? currentSessionId, CancellationToken ct = default)
    {
        var user = await _authRepository.FindUserByUsernameAsync(jwtSubject, ct);
        if (user is null)
            return new ListSessionsResponseDto(false, null,
                [new ValidationError("AUTH_USER_NOT_FOUND", "Authenticated account no longer exists.")]);

        var sessions = await _authRepository.ListActiveSessionsByUserAsync(user.UserId, ct);
        var dtos = sessions.Select(s => new SessionSummaryDto(
            s.SessionId, s.Realm, s.Audience, s.ExpiresAt, s.CreatedAt,
            IsCurrent: currentSessionId.HasValue && s.SessionId == currentSessionId.Value)).ToArray();
        return new ListSessionsResponseDto(true, dtos, []);
    }

    public async Task<RevokeOwnSessionResponseDto> RevokeOwnSessionAsync(
        string jwtSubject, RevokeOwnSessionRequestDto? request, CancellationToken ct = default)
    {
        if (!Guid.TryParse(request?.SessionId, out var sessionId))
            return new RevokeOwnSessionResponseDto(false,
                [new ValidationError("AUTH_SESSION_ID_MALFORMED", "sessionId must be a valid UUID.")]);

        var user = await _authRepository.FindUserByUsernameAsync(jwtSubject, ct);
        if (user is null)
            return new RevokeOwnSessionResponseDto(false,
                [new ValidationError("AUTH_USER_NOT_FOUND", "Authenticated account no longer exists.")]);

        var revoked = await _authRepository.RevokeOwnedSessionAsync(user.UserId, sessionId, jwtSubject, ct);
        return revoked
            ? new RevokeOwnSessionResponseDto(true, [])
            : new RevokeOwnSessionResponseDto(false,
                [new ValidationError("AUTH_SESSION_NOT_FOUND", "Session was not found for this account.")]);
    }

    public async Task<RevokeOtherSessionsResponseDto> RevokeOtherSessionsAsync(
        string jwtSubject, Guid? currentSessionId, CancellationToken ct = default)
    {
        var user = await _authRepository.FindUserByUsernameAsync(jwtSubject, ct);
        if (user is null)
            return new RevokeOtherSessionsResponseDto(false, 0,
                [new ValidationError("AUTH_USER_NOT_FOUND", "Authenticated account no longer exists.")]);

        var count = await _authRepository.RevokeSessionsForUserAsync(user.UserId, currentSessionId, jwtSubject, ct);
        return new RevokeOtherSessionsResponseDto(true, count, []);
    }

    /// <summary>Resolves the session bound to the caller's refresh-token cookie, for "current session" identity.</summary>
    public async Task<Guid?> ResolveSessionIdFromRefreshTokenAsync(string? refreshTokenPlain, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain)) return null;
        var hash = JwtTokenIssuer.HashRefreshToken(refreshTokenPlain);
        return await _authRepository.FindActiveSessionIdByRefreshTokenHashAsync(hash, ct);
    }

    // ─── Admin-driven credential/session operations ──────────────────────────────────────────
    // userId is always an explicit route parameter (the target account), resolved and validated
    // independently of the admin caller's own identity. None of these ever read/set a password value.

    public async Task<AdminListSessionsResponseDto> AdminListSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _authRepository.GetUserStateByIdAsync(userId, ct);
        if (user is null)
            return new AdminListSessionsResponseDto(false, null,
                [new ValidationError("AUTH_USER_NOT_FOUND", $"User {userId} was not found.")]);

        var sessions = await _authRepository.ListActiveSessionsByUserAsync(userId, ct);
        var dtos = sessions.Select(s => new SessionSummaryDto(
            s.SessionId, s.Realm, s.Audience, s.ExpiresAt, s.CreatedAt, IsCurrent: false)).ToArray();
        return new AdminListSessionsResponseDto(true, dtos, []);
    }

    public async Task<AdminRevokeSessionsResponseDto> AdminRevokeSessionsAsync(
        Guid userId, AdminRevokeSessionsRequestDto? request, string actorUsername, CancellationToken ct = default)
    {
        var user = await _authRepository.GetUserStateByIdAsync(userId, ct);
        if (user is null)
            return new AdminRevokeSessionsResponseDto(false, 0,
                [new ValidationError("AUTH_USER_NOT_FOUND", $"User {userId} was not found.")]);

        if (!string.IsNullOrWhiteSpace(request?.SessionId))
        {
            if (!Guid.TryParse(request.SessionId, out var sessionId))
                return new AdminRevokeSessionsResponseDto(false, 0,
                    [new ValidationError("AUTH_SESSION_ID_MALFORMED", "sessionId must be a valid UUID.")]);
            var revoked = await _authRepository.RevokeOwnedSessionAsync(userId, sessionId, actorUsername, ct);
            return revoked
                ? new AdminRevokeSessionsResponseDto(true, 1, [])
                : new AdminRevokeSessionsResponseDto(false, 0,
                    [new ValidationError("AUTH_SESSION_NOT_FOUND", "Session was not found for this account.")]);
        }

        var count = await _authRepository.RevokeSessionsForUserAsync(userId, exceptSessionId: null, actorUsername, ct);
        return new AdminRevokeSessionsResponseDto(true, count, []);
    }

    public async Task<AdminRevokeCredentialResponseDto> AdminRevokeCredentialAsync(
        Guid userId, string actorUsername, CancellationToken ct = default)
    {
        var user = await _authRepository.GetUserStateByIdAsync(userId, ct);
        if (user is null)
            return new AdminRevokeCredentialResponseDto(false,
                [new ValidationError("AUTH_USER_NOT_FOUND", $"User {userId} was not found.")]);

        var revoked = await _authRepository.RevokeCredentialAsync(userId, actorUsername, ct);
        return revoked
            ? new AdminRevokeCredentialResponseDto(true, [])
            : new AdminRevokeCredentialResponseDto(false,
                [new ValidationError("AUTH_CREDENTIAL_NOT_FOUND", "No credential is registered for this account.")]);
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
