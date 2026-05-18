using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

/// <summary>
/// Admin endpoint for context_token_registry management.
/// All methods require a valid JWT (enforced by the HTTP layer in Program.cs).
/// Contains no business logic — delegates to ContextRouteRepository.
/// </summary>
public class AdminEndpoint
{
    private readonly ILogger<AdminEndpoint> _logger;
    private readonly ContextRouteRepository _contextRouteRepository;

    public AdminEndpoint(
        ILogger<AdminEndpoint> logger,
        ContextRouteRepository contextRouteRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextRouteRepository = contextRouteRepository ?? throw new ArgumentNullException(nameof(contextRouteRepository));
    }

    /// <summary>
    /// Lists all context tokens regardless of status.
    /// </summary>
    public async Task<IReadOnlyList<AdminContextTokenDto>> HandleListTokensAsync(
        CancellationToken ct = default)
    {
        var records = await _contextRouteRepository.ListAllContextTokensAsync(ct);
        return records
            .Select(r => new AdminContextTokenDto(r.TokenId, r.Label, r.Group, r.Value, r.Status))
            .ToList();
    }

    /// <summary>
    /// Creates a new context token with status='active'.
    /// Returns an error response when input is invalid, when the repository is not connected,
    /// or when UNIQUE(label, "group") is violated.
    /// </summary>
    public async Task<AdminCreateTokenResponseDto> HandleCreateTokenAsync(
        AdminCreateTokenRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return new AdminCreateTokenResponseDto(false, null, "label is required.", "LABEL_REQUIRED");

        if (request.Value < -1.0f || request.Value > 1.0f)
            return new AdminCreateTokenResponseDto(false, null, "value must be in [-1.0, 1.0].", "VALUE_OUT_OF_RANGE");

        _logger.LogDebug("AdminEndpoint.HandleCreateTokenAsync: label={Label}", request.Label);

        var result = await _contextRouteRepository.CreateContextTokenAsync(
            request.Label, request.Group, request.Value, ct);

        return result.Code switch
        {
            CreateTokenCode.Success =>
                new AdminCreateTokenResponseDto(true, result.TokenId!.Value.ToString(), "Token created."),
            CreateTokenCode.Conflict =>
                new AdminCreateTokenResponseDto(false, null,
                    "A token with this label and group already exists.", "DUPLICATE_LABEL_GROUP"),
            CreateTokenCode.NotConnected =>
                new AdminCreateTokenResponseDto(false, null, "Token registry not connected.", "NOT_CONNECTED"),
            _ =>
                new AdminCreateTokenResponseDto(false, null, "Unexpected error.", "UNEXPECTED_ERROR"),
        };
    }

    /// <summary>
    /// Deprecates the given token. Returns not-found when tokenId does not exist.
    /// </summary>
    public async Task<(AdminDeprecateTokenResponseDto Response, bool Found)> HandleDeprecateTokenAsync(
        Guid tokenId, CancellationToken ct = default)
    {
        _logger.LogDebug("AdminEndpoint.HandleDeprecateTokenAsync: tokenId={TokenId}", tokenId);

        var found = await _contextRouteRepository.DeprecateContextTokenAsync(tokenId, ct);
        if (!found)
            return (new AdminDeprecateTokenResponseDto(false, "Token not found."), false);

        return (new AdminDeprecateTokenResponseDto(true, "Token deprecated."), true);
    }
}
