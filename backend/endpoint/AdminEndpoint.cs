using Microsoft.Extensions.Logging;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

/// <summary>
/// Thin HTTP boundary for admin operations.
/// All business logic lives in AdminRuntime.
/// All methods require a valid JWT (enforced by the HTTP layer in Program.cs).
/// </summary>
public class AdminEndpoint
{
    private readonly ILogger<AdminEndpoint> _logger;
    private readonly AdminRuntime _adminRuntime;

    public AdminEndpoint(
        ILogger<AdminEndpoint> logger,
        AdminRuntime adminRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _adminRuntime = adminRuntime ?? throw new ArgumentNullException(nameof(adminRuntime));
    }

    public Task<IReadOnlyList<AdminContextTokenDto>> HandleListTokensAsync(
        CancellationToken ct = default) =>
        _adminRuntime.ListTokensAsync(ct);

    public Task<AdminCreateTokenResponseDto> HandleCreateTokenAsync(
        AdminCreateTokenRequestDto request, CancellationToken ct = default) =>
        _adminRuntime.CreateTokenAsync(request, ct);

    public Task<(AdminDeprecateTokenResponseDto Response, bool Found)> HandleDeprecateTokenAsync(
        Guid tokenId, CancellationToken ct = default) =>
        _adminRuntime.DeprecateTokenAsync(tokenId, ct);

    public Task<(AdminRegistryVectorValidateResponseDto Response, int StatusCode)>
        HandleValidateRegistryVectorAsync(
            AdminRegistryVectorValidateRequestDto request,
            CancellationToken ct = default) =>
        _adminRuntime.ValidateRegistryVectorAsync(request, ct);
}
