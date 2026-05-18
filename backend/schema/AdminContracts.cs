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
/// Admin API response for a create-token operation.
/// </summary>
public record AdminCreateTokenResponseDto(
    bool Ok,
    string? TokenId,
    string Message
);

/// <summary>
/// Admin API response for a deprecate-token operation.
/// </summary>
public record AdminDeprecateTokenResponseDto(
    bool Ok,
    string Message
);
