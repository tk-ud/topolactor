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
/// Success:      token inserted, TokenId is set.
/// NotConnected: in-memory skeleton — no DB wired.
/// Conflict:     UNIQUE(label, "group") violated.
/// </summary>
public enum CreateTokenCode { Success, NotConnected, Conflict }

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
