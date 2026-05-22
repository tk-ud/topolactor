using Microsoft.Extensions.Logging;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Canonical admin runtime. Owns all admin business logic.
/// AdminEndpoint delegates to the typed methods here.
/// In manifest-driven production path, AdminRuntimeDispatchAdapter calls ExecuteDataAsync
/// when runtime_destination=admin_runtime is resolved from the active manifest.
/// </summary>
public class AdminRuntime
{
    private readonly ILogger<AdminRuntime> _logger;
    private readonly ContextRouteRepository _contextRouteRepository;
    private readonly RegistrarValidationService _registrarValidationService;
    private readonly PackageGeneratorRuntime _packageGeneratorRuntime;
    private readonly UiTopologyRepository _uiTopologyRepository;
    private readonly SeedRuntime? _seedRuntime;

    public AdminRuntime(
        ILogger<AdminRuntime> logger,
        ContextRouteRepository contextRouteRepository,
        RegistrarValidationService registrarValidationService,
        PackageGeneratorRuntime packageGeneratorRuntime,
        UiTopologyRepository uiTopologyRepository,
        SeedRuntime? seedRuntime = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextRouteRepository = contextRouteRepository ?? throw new ArgumentNullException(nameof(contextRouteRepository));
        _registrarValidationService = registrarValidationService ?? throw new ArgumentNullException(nameof(registrarValidationService));
        _packageGeneratorRuntime = packageGeneratorRuntime ?? throw new ArgumentNullException(nameof(packageGeneratorRuntime));
        _uiTopologyRepository = uiTopologyRepository ?? throw new ArgumentNullException(nameof(uiTopologyRepository));
        _seedRuntime = seedRuntime;
    }

    // ---------------------------------------------------------------------------
    // Typed methods — called by AdminEndpoint (thin wrapper)
    // ---------------------------------------------------------------------------

    public async Task<IReadOnlyList<AdminContextTokenDto>> ListTokensAsync(CancellationToken ct = default)
    {
        var records = await _contextRouteRepository.ListAllContextTokensAsync(ct);
        return records
            .Select(r => new AdminContextTokenDto(r.TokenId, r.Label, r.Group, r.Value, r.Status))
            .ToList();
    }

    public async Task<AdminCreateTokenResponseDto> CreateTokenAsync(
        AdminCreateTokenRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return new AdminCreateTokenResponseDto(false, null, "label is required.", "LABEL_REQUIRED");

        if (request.Value < -1.0f || request.Value > 1.0f)
            return new AdminCreateTokenResponseDto(false, null, "value must be in [-1.0, 1.0].", "VALUE_OUT_OF_RANGE");

        _logger.LogDebug("AdminRuntime.CreateTokenAsync: label={Label}", request.Label);

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

    public async Task<(AdminDeprecateTokenResponseDto Response, bool Found)> DeprecateTokenAsync(
        Guid tokenId, CancellationToken ct = default)
    {
        _logger.LogDebug("AdminRuntime.DeprecateTokenAsync: tokenId={TokenId}", tokenId);

        var found = await _contextRouteRepository.DeprecateContextTokenAsync(tokenId, ct);
        if (!found)
            return (new AdminDeprecateTokenResponseDto(false, "Token not found."), false);

        return (new AdminDeprecateTokenResponseDto(true, "Token deprecated."), true);
    }

    public async Task<(AdminRegistryVectorValidateResponseDto Response, int StatusCode)>
        ValidateRegistryVectorAsync(
            AdminRegistryVectorValidateRequestDto request, CancellationToken ct = default)
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
            "AdminRuntime.ValidateRegistryVectorAsync: table={Table} queryIds={Count}",
            request.RegistryTable, parsedIds.Count);

        var result = await _registrarValidationService.ValidateAsync(
            request.RegistryTable, parsedIds, ct);

        var validationClass = result.ValidationClass switch
        {
            RegistryVectorValidationClass.Pass                    => "pass",
            RegistryVectorValidationClass.RelatedExistingRegistry => "related_existing_registry",
            RegistryVectorValidationClass.NearDuplicateVector     => "near_duplicate_vector",
            RegistryVectorValidationClass.DuplicateVector         => "duplicate_vector",
            RegistryVectorValidationClass.ZeroVector              => "zero_vector",
            RegistryVectorValidationClass.ExplicitError           => "explicit_error",
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

    // ---------------------------------------------------------------------------
    // Manifest-driven dispatch entry — called by AdminRuntimeDispatchAdapter
    // when runtime_destination=admin_runtime is resolved from the active manifest.
    // In dev/demo bypass (null manifest repo), also called via TargetDispatchOverride.
    // Returns (data, null) on success or (null, error) on failure.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Executes the admin operation data step for the given vector's layer+action.
    /// </summary>
    public async Task<(JsonElement? data, ValidationError? error)> ExecuteDataAsync(
        OperationVector vector, CancellationToken ct = default)
    {
        var layerAction = $"{vector.Layer?.ToLowerInvariant()}:{vector.Action?.ToLowerInvariant()}";
        _logger.LogInformation("AdminRuntime.ExecuteDataAsync: layerAction={LayerAction}", layerAction);

        return layerAction switch
        {
            "context_token_registry:list"      => await DataListTokensAsync(ct),
            "context_token_registry:create"    => await DataCreateTokenAsync(vector, ct),
            "context_token_registry:deprecate" => await DataDeprecateTokenAsync(vector, ct),
            "registry_vector:validate"         => await DataValidateRegistryVectorAsync(vector, ct),
            "ui_component_bucket:list"         => await DataListBucketItemsAsync(vector, ct),
            "package_generator:generate"       => await DataGenerateAsync(vector, ct),
            "seed_runtime:save"                => await DataSeedSaveAsync(vector, ct),
            "seed_runtime:load"                => await DataSeedLoadAsync(ct),
            "seed_runtime:validate"            => await DataSeedValidateAsync(ct),
            "seed_runtime:preview"             => await DataSeedPreviewAsync(ct),
            "seed_runtime:import"              => await DataSeedImportAsync(ct),
            _ => (null, new ValidationError("ADMIN_OPERATION_NOT_FOUND",
                $"Unknown admin operation: {layerAction}"))
        };
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataListTokensAsync(
        CancellationToken ct)
    {
        var tokens = await ListTokensAsync(ct);
        return (JsonSerializer.SerializeToElement(tokens), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataCreateTokenAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for context_token_registry:create"));

        AdminCreateTokenRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AdminCreateTokenRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));

        var result = await CreateTokenAsync(request, ct);
        return (JsonSerializer.SerializeToElement(result), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataDeprecateTokenAsync(
        OperationVector vector, CancellationToken ct)
    {
        Guid tokenId;
        if (vector.IdOrHubId.HasValue)
        {
            tokenId = vector.IdOrHubId.Value;
        }
        else if (vector.Payload.HasValue &&
                 vector.Payload.Value.TryGetProperty("id", out var idEl) &&
                 Guid.TryParse(idEl.GetString(), out var parsedFromPayload))
        {
            tokenId = parsedFromPayload;
        }
        else
        {
            return (null, new ValidationError("TOKEN_ID_REQUIRED",
                "idOrHubId or payload.id UUID is required for deprecate"));
        }

        var (response, found) = await DeprecateTokenAsync(tokenId, ct);
        if (!found)
            return (null, new ValidationError("TOKEN_NOT_FOUND", response.Message));

        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataValidateRegistryVectorAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for registry_vector:validate"));

        AdminRegistryVectorValidateRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AdminRegistryVectorValidateRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));

        var (response, statusCode) = await ValidateRegistryVectorAsync(request, ct);
        if (statusCode >= 400)
            return (null, new ValidationError(response.StatusDetail ?? "VALIDATION_ERROR",
                response.ValidationClass));

        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataListBucketItemsAsync(
        OperationVector vector, CancellationToken ct)
    {
        string status = "bucketed";
        if (vector.Payload.HasValue &&
            vector.Payload.Value.TryGetProperty("status", out var statusEl) &&
            !string.IsNullOrWhiteSpace(statusEl.GetString()))
        {
            status = statusEl.GetString()!;
        }

        IReadOnlyList<UiComponentBucketRecord> records;
        try
        {
            records = await _uiTopologyRepository.ListBucketItemsAsync(status, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminRuntime.DataListBucketItemsAsync: repository unavailable.");
            return (null, new ValidationError("REPOSITORY_UNAVAILABLE", ex.Message));
        }

        var items = records
            .Select(r => new UiComponentBucketItemDto(
                r.BucketItemId.ToString(),
                r.ComponentKey,
                r.SourcePath,
                r.ComponentKind,
                r.Status))
            .ToList();

        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataGenerateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for package_generator:generate"));

        PackageGenerateRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<PackageGenerateRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));

        if (string.IsNullOrWhiteSpace(request.BucketItemId))
            return (null, new ValidationError("BUCKET_ITEM_ID_REQUIRED", "bucketItemId is required"));

        if (!Guid.TryParse(request.BucketItemId, out var bucketItemGuid))
            return (null, new ValidationError("MALFORMED_BUCKET_ITEM_ID",
                "bucketItemId must be a valid UUID"));

        if (string.IsNullOrWhiteSpace(request.RouteKey))
            return (null, new ValidationError("ROUTE_KEY_REQUIRED", "routeKey is required"));

        _logger.LogDebug(
            "AdminRuntime.DataGenerateAsync: bucketItemId={Id}, routeKey={Route}",
            bucketItemGuid, request.RouteKey);

        var result = await _packageGeneratorRuntime.GenerateAsync(bucketItemGuid, request.RouteKey, ct);

        if (result.Code != PackageGenerateCode.Success)
        {
            var errorCode = result.Code switch
            {
                PackageGenerateCode.NotFound            => "PACKAGE_NOT_FOUND",
                PackageGenerateCode.NotBucketed         => "PACKAGE_NOT_BUCKETED",
                PackageGenerateCode.ConstraintViolation => "CONSTRAINT_VIOLATION",
                _                                       => "PACKAGE_GENERATE_FAILED"
            };
            return (null, new ValidationError(errorCode, result.Message ?? "Operation failed."));
        }

        var responseDto = new PackageGenerateResponseDto(
            true,
            result.TensorId!.Value.ToString(),
            result.ComponentId!.Value.ToString(),
            result.PackageId!.Value.ToString(),
            result.LayoutId!.Value.ToString(),
            result.WiringId!.Value.ToString(),
            "Package generated successfully.");

        return (JsonSerializer.SerializeToElement(responseDto), null);
    }

    // ---------------------------------------------------------------------------
    // Seed Runtime operations — Issue #84
    // ---------------------------------------------------------------------------

    private ValidationError SeedRuntimeNotAvailable() =>
        new("SEED_RUNTIME_NOT_AVAILABLE",
            "SeedRuntime is not configured. Ensure SEED_STORAGE_PATH is set and /storage is mounted.");

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedSaveAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload with content field is required for seed_runtime:save"));

        string? content;
        try
        {
            if (!vector.Payload.Value.TryGetProperty("content", out var contentEl) ||
                contentEl.ValueKind != JsonValueKind.String)
                return (null, new ValidationError("CONTENT_REQUIRED",
                    "payload.content (string) is required"));
            content = contentEl.GetString();
        }
        catch (Exception ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (string.IsNullOrWhiteSpace(content))
            return (null, new ValidationError("CONTENT_EMPTY", "payload.content must not be empty"));

        var result = await _seedRuntime.SaveAsync(content, ct);
        if (!result.Success)
            return (null, new ValidationError(result.ErrorCode!, result.ErrorMessage!));

        return (JsonSerializer.SerializeToElement(
            new SeedSaveResponseDto(true)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedLoadAsync(
        CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        var result = await _seedRuntime.LoadAsync(ct);
        if (!result.Success)
            return (null, new ValidationError(result.ErrorCode!, result.ErrorMessage!));

        return (JsonSerializer.SerializeToElement(
            new SeedLoadResponseDto(true, result.Json)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedValidateAsync(
        CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        var result = await _seedRuntime.ValidateAsync(ct);
        var errorDtos = result.Errors
            .Select(e => new SeedValidationErrorDto(e.Code, e.Message))
            .ToList();

        return (JsonSerializer.SerializeToElement(
            new SeedValidationResponseDto(true, result.IsValid, errorDtos)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedPreviewAsync(
        CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        var result = await _seedRuntime.PreviewAsync(ct);
        if (!result.Success)
        {
            var errDtos = result.Errors
                .Select(e => new SeedValidationErrorDto(e.Code, e.Message))
                .ToList();
            return (null, new ValidationError(errDtos[0].Code, errDtos[0].Message));
        }

        var runtimeDtos = result.Data!.Runtimes
            .Select(r => new SeedRuntimePreviewDto(r.Name, r.Target, r.Layer, r.Action))
            .ToList();

        return (JsonSerializer.SerializeToElement(
            new SeedPreviewResponseDto(true, result.Data.RuntimeCount, runtimeDtos, [])), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedImportAsync(
        CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        var result = await _seedRuntime.ImportAsync(ct);
        if (!result.Success)
        {
            var errDtos = result.Errors
                .Select(e => new SeedValidationErrorDto(e.Code, e.Message))
                .ToList();
            return (null, new ValidationError(errDtos[0].Code, errDtos[0].Message));
        }

        return (JsonSerializer.SerializeToElement(
            new SeedImportResponseDto(
                true,
                result.ValidatedRuntimeCount,
                "Seed import completed via canonical runtime route.",
                [])), null);
    }
}
