using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;
using Topolactor.Service;

namespace Topolactor.Runtime;

/// <summary>
/// auth_runtime — login, refresh, logout. Not dispatched via manifest_dispatcher.
/// </summary>
public class AuthRuntime
{
    private readonly AuthService _authService;
    private readonly ManifestRepository _manifestRepository;

    public AuthRuntime(AuthService authService, ManifestRepository manifestRepository)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
    }

    public Task<(LoginResponseDto Response, string? RefreshTokenPlaintext)> LoginUserAsync(
        LoginRequestDto? request, CancellationToken ct = default) =>
        _authService.LoginAsync(request, AuthRealm.User, ct);

    public Task<RegisterResponseDto> RegisterUserAsync(
        RegisterRequestDto? request, CancellationToken ct = default) =>
        _authService.RegisterUserAsync(request, ct);

    /// <summary>
    /// Projection surface login: admin capability if the user has admin grants, user otherwise.
    /// Login surface and authority are orthogonal — callers must not assume realm from surface.
    /// </summary>
    public Task<(LoginResponseDto Response, string? RefreshTokenPlaintext)> LoginProjectionAsync(
        LoginRequestDto? request, CancellationToken ct = default) =>
        _authService.LoginProjectionAsync(request, ct);

    public Task<(LoginResponseDto Response, string? RefreshTokenPlaintext)> LoginAdminAsync(
        LoginRequestDto? request, CancellationToken ct = default) =>
        _authService.LoginAsync(request, AuthRealm.Admin, ct);

    public Task<(RefreshResponseDto Response, string? RefreshTokenPlaintext)> RefreshUserAsync(
        string? refreshToken, CancellationToken ct = default) =>
        _authService.RefreshAsync(refreshToken, AuthRealm.User, ct);

    public Task<(RefreshResponseDto Response, string? RefreshTokenPlaintext)> RefreshAdminAsync(
        string? refreshToken, CancellationToken ct = default) =>
        _authService.RefreshAsync(refreshToken, AuthRealm.Admin, ct);

    public Task<LogoutResponseDto> LogoutAsync(string? refreshToken, CancellationToken ct = default) =>
        _authService.LogoutAsync(refreshToken, ct);

    public async Task<LoginManifestResponseDto> LoadUserLoginManifestAsync(CancellationToken ct = default)
    {
        var manifest = await _manifestRepository.LoadByIdAsync(AuthManifestIds.UserLoginManifestId, ct);
        if (manifest is null)
            return new LoginManifestResponseDto(false, null, null,
                [new ValidationError("LOGIN_MANIFEST_NOT_FOUND", "User login manifest not found.")]);

        JsonElement? authBinding = null;
        JsonElement? uiProjection = null;
        foreach (var entry in manifest.Topology)
        {
            if (!entry.TryGetProperty("type", out var typeProp)) continue;
            var type = typeProp.GetString();
            if (type == "auth_action_binding") authBinding = entry;
            if (type == "ui_projection_mapping") uiProjection = entry;
        }

        if (authBinding is null || uiProjection is null)
            return new LoginManifestResponseDto(false, null, null,
                [new ValidationError("LOGIN_MANIFEST_MALFORMED",
                    "Login manifest must include auth_action_binding and ui_projection_mapping.")]);

        return new LoginManifestResponseDto(true, authBinding, uiProjection, []);
    }
}

public record LoginManifestResponseDto(
    bool Success,
    JsonElement? AuthActionBinding,
    JsonElement? UiProjection,
    IReadOnlyList<ValidationError> Errors);
