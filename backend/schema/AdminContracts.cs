using System.Text.Json.Serialization;

namespace Topolactor.Schema;

/// <summary>
/// Admin API response for a single context_token_registry row.
/// </summary>
public record AdminContextTokenDto(
    Guid TokenId,
    string Label,
    string? Group,
    float Value,
    string Status
);

/// <summary>
/// Admin API request to create a new context token.
/// value must be in [-1.0, 1.0].
/// </summary>
public record AdminCreateTokenRequestDto(
    string Label,
    string? Group,
    float Value
);

/// <summary>
/// Result code for CreateContextTokenAsync.
/// Success:  token inserted, TokenId is set.
/// Conflict: UNIQUE(label, "group") violated.
/// Not-connected behavior is an explicit exception (InvalidOperationException) in the
/// in-memory base class, not a return code. Production always uses NpgsqlContextRouteRepository.
/// </summary>
public enum CreateTokenCode { Success, Conflict }

/// <summary>
/// Result of CreateContextTokenAsync.
/// TokenId is non-null only when Code = Success.
/// </summary>
public record CreateTokenResult(CreateTokenCode Code, Guid? TokenId);

/// <summary>
/// Admin API response for a create-token operation.
/// ErrorCode carries a machine-readable failure reason when Ok=false.
/// </summary>
public record AdminCreateTokenResponseDto(
    bool Ok,
    string? TokenId,
    string Message,
    string? ErrorCode = null
);

/// <summary>
/// Admin API response for a deprecate-token operation.
/// </summary>
public record AdminDeprecateTokenResponseDto(
    bool Ok,
    string Message
);

// ---------------------------------------------------------------------------
// Registrar vector validation DTOs
// ---------------------------------------------------------------------------

/// <summary>
/// Admin API request to validate a candidate registry ID array against existing
/// registry rows using multi-hot cosine similarity.
/// registryTable must be one of the supported tables (e.g. "relation_registry").
/// queryIds must be valid UUID strings.
/// </summary>
public record AdminRegistryVectorValidateRequestDto(
    [property: JsonPropertyName("registryTable")] string RegistryTable,
    [property: JsonPropertyName("queryIds")]      IReadOnlyList<string> QueryIds
);

/// <summary>
/// A single neighbor returned in the registry vector validation result.
/// </summary>
public record AdminRegistryVectorNeighborDto(
    [property: JsonPropertyName("registryId")]   string RegistryId,
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("cosineScore")]  float CosineScore,
    [property: JsonPropertyName("matchedIds")]   IReadOnlyList<string> MatchedIds,
    [property: JsonPropertyName("reason")]       string Reason
);

/// <summary>
/// Admin API response for a registry vector validation operation.
/// validationClass: one of pass, related_existing_registry, near_duplicate_vector,
///   duplicate_vector, zero_vector, explicit_error.
/// isBlocking: true when the validation result should block promotion.
/// neighbors: existing registries with high cosine similarity to the candidate.
/// statusDetail: machine-readable code when validationClass is explicit_error.
/// </summary>
public record AdminRegistryVectorValidateResponseDto(
    [property: JsonPropertyName("validationClass")] string ValidationClass,
    [property: JsonPropertyName("isBlocking")]      bool IsBlocking,
    [property: JsonPropertyName("neighbors")]        IReadOnlyList<AdminRegistryVectorNeighborDto> Neighbors,
    [property: JsonPropertyName("statusDetail")]     string? StatusDetail = null
);
