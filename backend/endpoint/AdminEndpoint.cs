using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

/// <summary>
/// Admin endpoint for context_token_registry management and Registrar validation.
/// All methods require a valid JWT (enforced by the HTTP layer in Program.cs).
/// Contains no business logic — delegates to repositories and services.
/// </summary>
public class AdminEndpoint
{
    private readonly ILogger<AdminEndpoint> _logger;
    private readonly ContextRouteRepository _contextRouteRepository;
    private readonly RegistrarValidationService _registrarValidationService;

    public AdminEndpoint(
        ILogger<AdminEndpoint> logger,
        ContextRouteRepository contextRouteRepository,
        RegistrarValidationService registrarValidationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextRouteRepository = contextRouteRepository ?? throw new ArgumentNullException(nameof(contextRouteRepository));
        _registrarValidationService = registrarValidationService ?? throw new ArgumentNullException(nameof(registrarValidationService));
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

    // ---------------------------------------------------------------------------
    // Registrar vector validation
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates a candidate registry ID array against existing registry rows.
    /// Returns a structured AdminRegistryVectorValidateResponseDto.
    ///
    /// registryTable: must be non-empty.
    /// queryIds: each element must be a valid UUID string.
    ///
    /// Returns explicit error on policy missing, invalid, or DB unavailability.
    /// Never returns a silent fallback result.
    /// </summary>
    public async Task<(AdminRegistryVectorValidateResponseDto Response, int StatusCode)>
        HandleValidateRegistryVectorAsync(
            AdminRegistryVectorValidateRequestDto request,
            CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RegistryTable))
        {
            return (new AdminRegistryVectorValidateResponseDto(
                ValidationClass: "explicit_error",
                IsBlocking: true,
                Neighbors: [],
                StatusDetail: "REGISTRY_TABLE_REQUIRED"), 400);
        }

        var parsedIds = new List<Guid>();
        foreach (var raw in request.QueryIds ?? [])
        {
            if (!Guid.TryParse(raw, out var id))
            {
                return (new AdminRegistryVectorValidateResponseDto(
                    ValidationClass: "explicit_error",
                    IsBlocking: true,
                    Neighbors: [],
                    StatusDetail: $"INVALID_QUERY_ID:{raw}"), 400);
            }
            parsedIds.Add(id);
        }

        _logger.LogDebug(
            "AdminEndpoint.HandleValidateRegistryVectorAsync: table={Table} queryIds={Count}",
            request.RegistryTable, parsedIds.Count);

        var result = await _registrarValidationService.ValidateAsync(
            request.RegistryTable, parsedIds, ct);

        var validationClass = result.ValidationClass switch
        {
            RegistryVectorValidationClass.Pass                   => "pass",
            RegistryVectorValidationClass.RelatedExistingRegistry => "related_existing_registry",
            RegistryVectorValidationClass.NearDuplicateVector    => "near_duplicate_vector",
            RegistryVectorValidationClass.DuplicateVector        => "duplicate_vector",
            RegistryVectorValidationClass.ZeroVector             => "zero_vector",
            RegistryVectorValidationClass.ExplicitError          => "explicit_error",
            _ => "explicit_error"
        };

        var neighbors = result.Neighbors
            .Select(n => new AdminRegistryVectorNeighborDto(
                RegistryId:  n.RegistryId.ToString(),
                Name:        n.Name,
                CosineScore: n.CosineScore,
                MatchedIds:  n.MatchedIds.Select(id => id.ToString()).ToList(),
                Reason:      n.Reason))
            .ToList();

        var statusCode = result.IsBlocking && result.ValidationClass == RegistryVectorValidationClass.ExplicitError
            ? 422
            : 200;

        return (new AdminRegistryVectorValidateResponseDto(
            ValidationClass: validationClass,
            IsBlocking:      result.IsBlocking,
            Neighbors:       neighbors,
            StatusDetail:    result.StatusDetail), statusCode);
    }
}
