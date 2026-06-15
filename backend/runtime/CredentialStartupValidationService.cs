using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;

namespace Topolactor.Runtime;

/// <summary>
/// Hosted service that validates all registered credential references on application startup.
///
/// SSOT: docs/design/runtime-bundle-secret-credential-ssot.yaml
///   credential_management_flow.validation.required_at: [registration, rotation, runtime_startup]
///   failure_policy.on_credential_validation_failure: expose_failure_explicitly, do_not_silently_fallback
///   failure_policy.on_secret_store_unavailable: fail_close_with_explicit_error
///
/// Validation outcomes are recorded in logs.credential_audit_log (reference-only, no credential values).
/// Startup validation does not block application startup — failures are logged and tracked in DB.
/// </summary>
public sealed class CredentialStartupValidationService : IHostedService
{
    private readonly ILogger<CredentialStartupValidationService> _logger;
    private readonly CredentialReferenceRepository _repository;
    private readonly CredentialValidationService _validationService;

    public CredentialStartupValidationService(
        ILogger<CredentialStartupValidationService> logger,
        CredentialReferenceRepository repository,
        CredentialValidationService validationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Schema.CredentialReferenceDto> references;
        try
        {
            references = await _repository.ListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CredentialStartupValidationService: failed to list credential references. Startup validation skipped.");
            return;
        }

        if (references.Count == 0)
        {
            _logger.LogDebug("CredentialStartupValidationService: no credential references registered. Skipping startup validation.");
            return;
        }

        _logger.LogInformation(
            "CredentialStartupValidationService: validating {Count} credential reference(s) at startup.",
            references.Count);

        var failCount = 0;
        foreach (var reference in references)
        {
            try
            {
                var result = await _validationService.ValidateAsync(reference.ReferenceKey, cancellationToken);
                if (result.IsValid)
                {
                    _logger.LogDebug(
                        "CredentialStartupValidationService: referenceKey={ReferenceKey} validation succeeded.",
                        reference.ReferenceKey);
                }
                else
                {
                    failCount++;
                    _logger.LogWarning(
                        "CredentialStartupValidationService: referenceKey={ReferenceKey} validation failed. failureCode={FailureCode}",
                        reference.ReferenceKey, result.FailureCode);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogError(ex,
                    "CredentialStartupValidationService: referenceKey={ReferenceKey} validation threw exception.",
                    reference.ReferenceKey);
            }
        }

        if (failCount > 0)
        {
            _logger.LogWarning(
                "CredentialStartupValidationService: startup validation complete. {FailCount}/{TotalCount} credential reference(s) failed validation. Check logs.credential_audit_log for details.",
                failCount, references.Count);
        }
        else
        {
            _logger.LogInformation(
                "CredentialStartupValidationService: startup validation complete. All {Count} credential reference(s) valid.",
                references.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
