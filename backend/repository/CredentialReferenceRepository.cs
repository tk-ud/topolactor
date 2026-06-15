using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Abstract repository for credential reference metadata.
/// SSOT: docs/design/runtime-bundle-secret-credential-ssot.yaml
///
/// Security invariant: this repository stores REFERENCE METADATA ONLY.
/// No actual credential values, API keys, secret keys, connection strings,
/// private keys, endpoint URLs, or credential hashes are ever persisted
/// through this interface.
/// </summary>
public abstract class CredentialReferenceRepository
{
    protected readonly string _connectionString;

    protected CredentialReferenceRepository(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    /// <summary>
    /// Registers a new credential reference.
    /// Persists reference_key, provider_kind, and initial status only.
    /// Throws InvalidOperationException when reference_key already exists.
    /// </summary>
    public abstract Task<CredentialReferenceDto> RegisterAsync(
        string referenceKey,
        string providerKind,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a credential reference by its logical reference key.
    /// Returns null when not found.
    /// </summary>
    public abstract Task<CredentialReferenceDto?> GetByKeyAsync(
        string referenceKey,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all credential references.
    /// Returns reference metadata only — no credential values.
    /// </summary>
    public abstract Task<IReadOnlyList<CredentialReferenceDto>> ListAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Updates the validation status and last_validated_at timestamp.
    /// Returns false when reference_key not found.
    /// </summary>
    public abstract Task<bool> UpdateValidationStatusAsync(
        string referenceKey,
        string validationStatus,
        CancellationToken ct = default);

    /// <summary>
    /// Records a rotation attempt: sets status to 'rotation_pending' and updates last_rotated_at.
    /// Caller must call ConfirmRotationAsync after successful post-rotation validation.
    /// Returns false when reference_key not found.
    /// </summary>
    public abstract Task<bool> UpdateRotationTimestampAsync(
        string referenceKey,
        CancellationToken ct = default);

    /// <summary>
    /// Confirms a completed rotation by setting status to 'active'.
    /// Must only be called after post-rotation validation succeeds.
    /// Returns false when reference_key not found.
    /// </summary>
    public abstract Task<bool> ConfirmRotationAsync(
        string referenceKey,
        CancellationToken ct = default);

    /// <summary>
    /// Appends a credential lifecycle audit event.
    /// Stores credential_reference_id, reference_key (logical name), event_type, and outcome.
    /// Never stores credential values.
    /// </summary>
    public abstract Task AppendAuditEventAsync(
        CredentialAuditEventRecord record,
        CancellationToken ct = default);
}
