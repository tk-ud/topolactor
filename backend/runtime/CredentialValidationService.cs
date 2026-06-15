using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Validates credential references against the secret store.
/// Records validation outcomes as reference-only audit events.
///
/// SSOT: docs/design/runtime-bundle-secret-credential-ssot.yaml
///   validation.description: credential参照の有効性検証。registration / rotation / runtime_startup で実行。
///   failure_policy.on_credential_validation_failure: expose_failure_explicitly, do_not_silently_fallback
///   failure_policy.on_secret_store_unavailable: fail_close_with_explicit_error, do_not_use_cached_credential_silently
///
/// Audit log boundary:
///   All validation events are recorded to logs.credential_audit_log with credential_reference_id
///   and reference_key (logical name) only. No credential values are ever logged.
/// </summary>
public sealed class CredentialValidationService
{
    private readonly ICredentialStore _credentialStore;
    private readonly CredentialReferenceRepository _repository;
    private readonly ILogger<CredentialValidationService> _logger;

    public CredentialValidationService(
        ILogger<CredentialValidationService> logger,
        ICredentialStore credentialStore,
        CredentialReferenceRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Validates a credential reference against the secret store.
    ///
    /// Steps:
    ///   1. Look up the credential reference in DB. Missing reference → explicit error.
    ///   2. Call store.GetAsync(referenceKey). Store unavailable → fail-close (explicit error).
    ///   3. If credential is not present → explicit validation failure (no silent fallback).
    ///   4. Update validation_status in DB.
    ///   5. Append audit event (reference-only, no credential value).
    ///   6. Return CredentialValidationResult with reference-only outcome.
    /// </summary>
    public async Task<CredentialValidationResult> ValidateAsync(
        string referenceKey,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceKey);

        var reference = await _repository.GetByKeyAsync(referenceKey, ct);
        if (reference is null)
        {
            _logger.LogWarning(
                "CredentialValidationService: credential reference not found for referenceKey={ReferenceKey}",
                referenceKey);

            return new CredentialValidationResult(
                ReferenceKey: referenceKey,
                IsValid: false,
                FailureCode: "CREDENTIAL_REFERENCE_NOT_FOUND",
                FailureReason: $"No credential reference registered for key '{referenceKey}'.");
        }

        CredentialStoreResult storeResult;
        try
        {
            storeResult = await _credentialStore.GetAsync(referenceKey, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CredentialValidationService: secret store threw exception for referenceKey={ReferenceKey}",
                referenceKey);

            await AppendAuditAsync(
                reference.CredentialReferenceId, referenceKey,
                eventType: "credential_validation_failed",
                outcome: "failure",
                failureCode: "SECRET_STORE_EXCEPTION",
                ct);

            return new CredentialValidationResult(
                ReferenceKey: referenceKey,
                IsValid: false,
                FailureCode: "SECRET_STORE_EXCEPTION",
                FailureReason: "Secret store threw an unexpected exception. Credential validation cannot proceed.");
        }

        if (!storeResult.StoreAvailable)
        {
            _logger.LogError(
                "CredentialValidationService: secret store unavailable for referenceKey={ReferenceKey}. Fail-close.",
                referenceKey);

            await AppendAuditAsync(
                reference.CredentialReferenceId, referenceKey,
                eventType: "credential_validation_failed",
                outcome: "failure",
                failureCode: "SECRET_STORE_UNAVAILABLE",
                ct);

            await _repository.UpdateValidationStatusAsync(referenceKey, "invalid", ct);

            return new CredentialValidationResult(
                ReferenceKey: referenceKey,
                IsValid: false,
                FailureCode: "SECRET_STORE_UNAVAILABLE",
                FailureReason: "Secret store is unavailable. Fail-close: no silent fallback to cached credentials.");
        }

        if (!storeResult.CredentialPresent)
        {
            _logger.LogWarning(
                "CredentialValidationService: credential not present in store for referenceKey={ReferenceKey}",
                referenceKey);

            await _repository.UpdateValidationStatusAsync(referenceKey, "invalid", ct);
            await AppendAuditAsync(
                reference.CredentialReferenceId, referenceKey,
                eventType: "credential_validation_failed",
                outcome: "failure",
                failureCode: "CREDENTIAL_NOT_PRESENT_IN_STORE",
                ct);

            return new CredentialValidationResult(
                ReferenceKey: referenceKey,
                IsValid: false,
                FailureCode: "CREDENTIAL_NOT_PRESENT_IN_STORE",
                FailureReason: storeResult.FailureReason ?? "Credential is not present in the secret store.");
        }

        await _repository.UpdateValidationStatusAsync(referenceKey, "valid", ct);
        await AppendAuditAsync(
            reference.CredentialReferenceId, referenceKey,
            eventType: "credential_validated",
            outcome: "success",
            failureCode: null,
            ct);

        _logger.LogInformation(
            "CredentialValidationService: credential validated successfully for referenceKey={ReferenceKey}",
            referenceKey);

        return new CredentialValidationResult(
            ReferenceKey: referenceKey,
            IsValid: true,
            FailureCode: null,
            FailureReason: null);
    }

    private async Task AppendAuditAsync(
        Guid credentialReferenceId,
        string referenceKey,
        string eventType,
        string outcome,
        string? failureCode,
        CancellationToken ct)
    {
        try
        {
            await _repository.AppendAuditEventAsync(
                new CredentialAuditEventRecord(
                    EventType: eventType,
                    CredentialReferenceId: credentialReferenceId,
                    ReferenceKey: referenceKey,
                    Outcome: outcome,
                    FailureCode: failureCode),
                ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CredentialValidationService: failed to append audit event eventType={EventType} referenceKey={ReferenceKey}",
                eventType, referenceKey);
        }
    }
}
