namespace Topolactor.Schema;

/// <summary>
/// Credential reference metadata DTO.
/// Contains reference identifiers, provider kind, lifecycle status, and timestamps ONLY.
/// No real credential values, API keys, secret keys, connection strings, private keys,
/// endpoint URLs, or credential hashes are ever present in this record.
/// </summary>
public record CredentialReferenceDto(
    Guid CredentialReferenceId,
    string ReferenceKey,
    string ProviderKind,
    string Status,
    string ValidationStatus,
    DateTimeOffset? LastValidatedAt,
    DateTimeOffset? LastRotatedAt,
    DateTimeOffset RegisteredAt,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Command to register a new credential reference.
/// Stores only the reference key and provider kind — not the actual credential value.
/// The credential value must be placed in the secret store independently.
/// </summary>
public record CredentialRegistrationCommand(
    string ReferenceKey,
    string ProviderKind
);

/// <summary>
/// Credential audit event record for reference-only logging.
/// Contains event type, credential reference id, reference key, and outcome.
/// No credential values, hashes, or secret material ever stored here.
/// </summary>
public record CredentialAuditEventRecord(
    string EventType,
    Guid CredentialReferenceId,
    string ReferenceKey,
    string Outcome,
    string? FailureCode
);

/// <summary>
/// Result of a secret store presence check via ICredentialStore.GetAsync.
/// Returns availability and presence status ONLY — never the actual credential value.
/// Per SSOT failure_policy: when StoreAvailable=false, caller must fail-close.
/// Silent fallback to cached or default credentials is prohibited.
/// </summary>
public record CredentialStoreResult(
    bool StoreAvailable,
    bool CredentialPresent,
    string? FailureReason
);

/// <summary>
/// Result of a secret store registration binding via ICredentialStore.SetAsync.
/// </summary>
public record CredentialStoreSetResult(
    bool Success,
    bool StoreAvailable,
    string? FailureReason
);

/// <summary>
/// Result of credential validation from CredentialValidationService.ValidateAsync.
/// Contains reference-only outcome data — no actual credential values.
/// </summary>
public record CredentialValidationResult(
    string ReferenceKey,
    bool IsValid,
    string? FailureCode,
    string? FailureReason
);

/// <summary>
/// Secret store adapter interface.
/// Abstracts over runtime environment variables and future secret manager APIs.
///
/// Failure policy (SSOT runtime-bundle-secret-credential-ssot.yaml failure_policy):
///   - GetAsync: when store is unavailable, StoreAvailable=false must be returned.
///     The caller must fail-close; silent fallback to cached or default credentials is prohibited.
///   - SetAsync: same fail-close policy when store is unavailable.
///
/// Security boundary:
///   - Implementations must never return actual credential values through the public result records.
///   - Internal implementations may hold values transiently only for validation purposes
///     and must not expose them in logs, exceptions, or return values.
/// </summary>
public interface ICredentialStore
{
    /// <summary>
    /// Checks whether a credential is present and accessible in the secret store.
    /// Returns presence and availability status ONLY — never the actual credential value.
    /// When store is unavailable (StoreAvailable=false), caller must fail-close;
    /// silent fallback is prohibited per SSOT on_secret_store_unavailable policy.
    /// </summary>
    Task<CredentialStoreResult> GetAsync(string referenceKey, CancellationToken ct = default);

    /// <summary>
    /// Registers or confirms a credential reference binding in the store adapter.
    /// For env_var provider: validates that the environment variable name is well-formed.
    /// For future secret managers: confirms the reference key is mapped in the store.
    /// Never stores or logs actual credential values.
    /// </summary>
    Task<CredentialStoreSetResult> SetAsync(string referenceKey, string providerKind, CancellationToken ct = default);
}
