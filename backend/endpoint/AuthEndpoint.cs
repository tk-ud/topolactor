using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

/// <summary>Thin HTTP adapter delegating to AuthRuntime (auth_runtime).</summary>
public class AuthEndpoint
{
    private readonly AuthRuntime _authRuntime;

    public AuthEndpoint(AuthRuntime authRuntime) =>
        _authRuntime = authRuntime ?? throw new ArgumentNullException(nameof(authRuntime));

    public Task<(LoginResponseDto Response, string? RefreshTokenPlaintext)> LoginUserAsync(
        LoginRequestDto? request, CancellationToken ct = default) =>
        _authRuntime.LoginUserAsync(request, ct);

    public Task<RegisterResponseDto> RegisterUserAsync(
        RegisterRequestDto? request, CancellationToken ct = default) =>
        _authRuntime.RegisterUserAsync(request, ct);

    public Task<(LoginResponseDto Response, string? RefreshTokenPlaintext)> LoginProjectionAsync(
        LoginRequestDto? request, CancellationToken ct = default) =>
        _authRuntime.LoginProjectionAsync(request, ct);

    public Task<(LoginResponseDto Response, string? RefreshTokenPlaintext)> LoginAdminAsync(
        LoginRequestDto? request, CancellationToken ct = default) =>
        _authRuntime.LoginAdminAsync(request, ct);

    public Task<(RefreshResponseDto Response, string? RefreshTokenPlaintext)> RefreshUserAsync(
        RefreshRequestDto? request, CancellationToken ct = default) =>
        _authRuntime.RefreshUserAsync(request?.RefreshToken, ct);

    public Task<(RefreshResponseDto Response, string? RefreshTokenPlaintext)> RefreshAdminAsync(
        RefreshRequestDto? request, CancellationToken ct = default) =>
        _authRuntime.RefreshAdminAsync(request?.RefreshToken, ct);

    public Task<LogoutResponseDto> LogoutAsync(
        LogoutRequestDto? request, CancellationToken ct = default) =>
        _authRuntime.LogoutAsync(request?.RefreshToken, ct);

    public Task<LoginManifestResponseDto> LoadUserLoginManifestAsync(CancellationToken ct = default) =>
        _authRuntime.LoadUserLoginManifestAsync(ct);

    // ─── Self-service credential/session lifecycle ───────────────────────────────────────────

    public Task<CurrentAccountResponseDto> GetCurrentAccountAsync(
        string jwtSubject, string jwtRole, string jwtRealm, CancellationToken ct = default) =>
        _authRuntime.GetCurrentAccountAsync(jwtSubject, jwtRole, jwtRealm, ct);

    public Task<ChangeOwnPasswordResponseDto> ChangeOwnPasswordAsync(
        string jwtSubject, ChangeOwnPasswordRequestDto? request, CancellationToken ct = default) =>
        _authRuntime.ChangeOwnPasswordAsync(jwtSubject, request, ct);

    public Task<ListSessionsResponseDto> ListOwnSessionsAsync(
        string jwtSubject, string? refreshTokenPlain, CancellationToken ct = default) =>
        _authRuntime.ListOwnSessionsAsync(jwtSubject, refreshTokenPlain, ct);

    public Task<RevokeOwnSessionResponseDto> RevokeOwnSessionAsync(
        string jwtSubject, RevokeOwnSessionRequestDto? request, CancellationToken ct = default) =>
        _authRuntime.RevokeOwnSessionAsync(jwtSubject, request, ct);

    public Task<RevokeOtherSessionsResponseDto> RevokeOtherSessionsAsync(
        string jwtSubject, string? refreshTokenPlain, CancellationToken ct = default) =>
        _authRuntime.RevokeOtherSessionsAsync(jwtSubject, refreshTokenPlain, ct);

    // ─── Admin-driven credential/session operations ──────────────────────────────────────────

    public Task<AdminListSessionsResponseDto> AdminListSessionsAsync(Guid userId, CancellationToken ct = default) =>
        _authRuntime.AdminListSessionsAsync(userId, ct);

    public Task<AdminRevokeSessionsResponseDto> AdminRevokeSessionsAsync(
        Guid userId, AdminRevokeSessionsRequestDto? request, string actorUsername, CancellationToken ct = default) =>
        _authRuntime.AdminRevokeSessionsAsync(userId, request, actorUsername, ct);

    public Task<AdminRevokeCredentialResponseDto> AdminRevokeCredentialAsync(
        Guid userId, string actorUsername, CancellationToken ct = default) =>
        _authRuntime.AdminRevokeCredentialAsync(userId, actorUsername, ct);
}
