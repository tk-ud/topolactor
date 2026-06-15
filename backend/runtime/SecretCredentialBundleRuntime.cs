using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// IDispatchableRuntime handler for secret_credential_runtime.
/// Registered as "secret_credential_runtime" in the ManifestDispatcher handler dictionary.
///
/// SSOT: docs/design/runtime-bundle-secret-credential-ssot.yaml
///   authority_boundary: admin_config_and_runtime_secret_store
///   trigger_kind.allowed: admin_config_credential_registration, api_credential_rotation, system_credential_validation
///
/// Supported actions (request.Action):
///   register  — register a new credential reference (reference key + provider kind only, no credential value)
///   validate  — validate a credential reference against the secret store
///   rotate    — mark credential as rotation_pending and record rotation event (explicit admin action only)
///   list      — list credential references (reference metadata only, no credential values)
///
/// Security invariants enforced by this runtime:
///   - No credential values appear in responses, logs, or DB
///   - Secret store unavailable → fail-close with explicit error (no silent fallback)
///   - Validation failure → explicit structured error (no silent success)
///   - Rotation requires explicit admin client trigger (no autonomous rotation)
///   - CLI/MCP credential read is not implemented here
/// </summary>
public sealed class SecretCredentialBundleRuntime : IDispatchableRuntime
{
    private readonly ILogger<SecretCredentialBundleRuntime> _logger;
    private readonly ICredentialStore _credentialStore;
    private readonly CredentialReferenceRepository _repository;
    private readonly CredentialValidationService _validationService;

    public SecretCredentialBundleRuntime(
        ILogger<SecretCredentialBundleRuntime> logger,
        ICredentialStore credentialStore,
        CredentialReferenceRepository repository,
        CredentialValidationService validationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    public Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request,
        Guid? manifestId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "SecretCredentialBundleRuntime: action={Action} manifestId={ManifestId}",
            request.Action, manifestId);

        return request.Action?.ToLowerInvariant() switch
        {
            "register" => HandleRegisterAsync(request, ct),
            "validate" => HandleValidateAsync(request, ct),
            "rotate"   => HandleRotateAsync(request, ct),
            "list"     => HandleListAsync(request, ct),
            _ => Task.FromResult(new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError(
                    "CREDENTIAL_ACTION_UNKNOWN",
                    $"secret_credential_runtime: unknown action '{request.Action}'. Supported: register, validate, rotate, list.")]))
        };
    }

    // ─── register ────────────────────────────────────────────────────────────

    private async Task<EndpointResponseDto> HandleRegisterAsync(
        EndpointRequestDto request,
        CancellationToken ct)
    {
        if (!TryExtractString(request.Payload, "reference_key", out var referenceKey) || string.IsNullOrWhiteSpace(referenceKey))
        {
            return ExplicitError("CREDENTIAL_REFERENCE_KEY_REQUIRED",
                "register: payload must include non-empty 'reference_key'.");
        }

        if (!TryExtractString(request.Payload, "provider_kind", out var providerKind) || string.IsNullOrWhiteSpace(providerKind))
        {
            return ExplicitError("CREDENTIAL_PROVIDER_KIND_REQUIRED",
                "register: payload must include non-empty 'provider_kind'.");
        }

        _logger.LogDebug(
            "SecretCredentialBundleRuntime.register: referenceKey={ReferenceKey} providerKind={ProviderKind}",
            referenceKey, providerKind);

        // Check store binding before DB registration. Store unavailable → fail-close (no silent fallback).
        CredentialStoreSetResult storeBinding;
        try
        {
            storeBinding = await _credentialStore.SetAsync(referenceKey, providerKind, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SecretCredentialBundleRuntime.register: secret store threw exception referenceKey={ReferenceKey}",
                referenceKey);
            return ExplicitError("CREDENTIAL_STORE_EXCEPTION",
                $"Secret store threw an unexpected exception for key '{referenceKey}'.");
        }

        if (!storeBinding.StoreAvailable)
        {
            _logger.LogError(
                "SecretCredentialBundleRuntime.register: secret store unavailable referenceKey={ReferenceKey}. Fail-close.",
                referenceKey);
            return ExplicitError("SECRET_STORE_UNAVAILABLE",
                $"Secret store is unavailable. Registration failed for key '{referenceKey}'.");
        }

        if (!storeBinding.Success)
        {
            _logger.LogWarning(
                "SecretCredentialBundleRuntime.register: store binding failed referenceKey={ReferenceKey}",
                referenceKey);
            return ExplicitError("CREDENTIAL_STORE_BINDING_FAILED",
                storeBinding.FailureReason ?? $"Secret store binding failed for key '{referenceKey}'.");
        }

        CredentialReferenceDto registered;
        try
        {
            registered = await _repository.RegisterAsync(referenceKey, providerKind, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex.Message.Contains(referenceKey, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex,
                "SecretCredentialBundleRuntime.register: duplicate referenceKey={ReferenceKey}",
                referenceKey);
            return ExplicitError("CREDENTIAL_REFERENCE_KEY_CONFLICT",
                $"A credential reference with key '{referenceKey}' already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SecretCredentialBundleRuntime.register: repository error for referenceKey={ReferenceKey}",
                referenceKey);
            return ExplicitError("CREDENTIAL_REGISTER_FAILED",
                $"Failed to register credential reference for key '{referenceKey}'.");
        }

        // Audit write failure is not silent — return explicit error rather than swallow.
        try
        {
            await _repository.AppendAuditEventAsync(
                new CredentialAuditEventRecord(
                    EventType: "credential_registered",
                    CredentialReferenceId: registered.CredentialReferenceId,
                    ReferenceKey: referenceKey,
                    Outcome: "success",
                    FailureCode: null),
                ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SecretCredentialBundleRuntime.register: audit log append failed referenceKey={ReferenceKey}",
                referenceKey);
            return ExplicitError("CREDENTIAL_AUDIT_WRITE_FAILED",
                $"Failed to record registration audit for credential '{referenceKey}'.");
        }

        _logger.LogInformation(
            "SecretCredentialBundleRuntime.register: credential reference registered referenceKey={ReferenceKey}",
            referenceKey);

        var data = BuildReferenceData(registered);
        return Success(data);
    }

    // ─── validate ────────────────────────────────────────────────────────────

    private async Task<EndpointResponseDto> HandleValidateAsync(
        EndpointRequestDto request,
        CancellationToken ct)
    {
        if (!TryExtractString(request.Payload, "reference_key", out var referenceKey) || string.IsNullOrWhiteSpace(referenceKey))
        {
            return ExplicitError("CREDENTIAL_REFERENCE_KEY_REQUIRED",
                "validate: payload must include non-empty 'reference_key'.");
        }

        _logger.LogDebug(
            "SecretCredentialBundleRuntime.validate: referenceKey={ReferenceKey}",
            referenceKey);

        CredentialValidationResult result;
        try
        {
            result = await _validationService.ValidateAsync(referenceKey, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SecretCredentialBundleRuntime.validate: unexpected exception referenceKey={ReferenceKey}",
                referenceKey);
            return ExplicitError("CREDENTIAL_AUDIT_WRITE_FAILED",
                $"Failed to record audit event during credential validation for '{referenceKey}'.");
        }

        if (!result.IsValid)
        {
            _logger.LogWarning(
                "SecretCredentialBundleRuntime.validate: validation failed referenceKey={ReferenceKey} failureCode={FailureCode}",
                referenceKey, result.FailureCode);

            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError(
                    result.FailureCode ?? "CREDENTIAL_VALIDATION_FAILED",
                    result.FailureReason ?? "Credential validation failed.")]);
        }

        _logger.LogInformation(
            "SecretCredentialBundleRuntime.validate: validation succeeded referenceKey={ReferenceKey}",
            referenceKey);

        var data = JsonSerializer.SerializeToElement(new
        {
            reference_key = result.ReferenceKey,
            is_valid = result.IsValid,
        });

        return Success(data);
    }

    // ─── rotate ──────────────────────────────────────────────────────────────

    private async Task<EndpointResponseDto> HandleRotateAsync(
        EndpointRequestDto request,
        CancellationToken ct)
    {
        if (!TryExtractString(request.Payload, "reference_key", out var referenceKey) || string.IsNullOrWhiteSpace(referenceKey))
        {
            return ExplicitError("CREDENTIAL_REFERENCE_KEY_REQUIRED",
                "rotate: payload must include non-empty 'reference_key'.");
        }

        if (!TryExtractString(request.Payload, "rotation_actor_id", out var rotationActorId) || string.IsNullOrWhiteSpace(rotationActorId))
        {
            return ExplicitError("CREDENTIAL_ROTATION_ACTOR_REQUIRED",
                "rotate: payload must include non-empty 'rotation_actor_id'. Rotation requires explicit admin actor identity.");
        }

        // rotation_actor_id must be a valid UUID — admin user IDs in this system are UUIDs.
        if (!Guid.TryParse(rotationActorId, out _))
        {
            return ExplicitError("CREDENTIAL_ROTATION_ACTOR_INVALID",
                "rotate: 'rotation_actor_id' must be a valid admin user UUID.");
        }

        _logger.LogDebug(
            "SecretCredentialBundleRuntime.rotate: referenceKey={ReferenceKey} rotationActorId={RotationActorId}",
            referenceKey, rotationActorId);

        var reference = await _repository.GetByKeyAsync(referenceKey, ct);
        if (reference is null)
        {
            return ExplicitError("CREDENTIAL_REFERENCE_NOT_FOUND",
                $"No credential reference found for key '{referenceKey}'.");
        }

        var updated = await _repository.UpdateRotationTimestampAsync(referenceKey, ct);
        if (!updated)
        {
            return ExplicitError("CREDENTIAL_ROTATION_FAILED",
                $"Failed to record rotation for credential reference '{referenceKey}'.");
        }

        // Audit write failure is not silent — return explicit error rather than swallow.
        try
        {
            await _repository.AppendAuditEventAsync(
                new CredentialAuditEventRecord(
                    EventType: "credential_rotated",
                    CredentialReferenceId: reference.CredentialReferenceId,
                    ReferenceKey: referenceKey,
                    Outcome: "success",
                    FailureCode: null),
                ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SecretCredentialBundleRuntime.rotate: audit log append failed referenceKey={ReferenceKey}",
                referenceKey);
            return ExplicitError("CREDENTIAL_AUDIT_WRITE_FAILED",
                $"Failed to record rotation audit for credential '{referenceKey}'.");
        }

        // Validate that the rotated credential is present in the secret store.
        CredentialValidationResult validation;
        try
        {
            validation = await _validationService.ValidateAsync(referenceKey, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SecretCredentialBundleRuntime.rotate: post-rotation validation threw exception referenceKey={ReferenceKey}",
                referenceKey);
            return ExplicitError("CREDENTIAL_AUDIT_WRITE_FAILED",
                $"Failed to record post-rotation validation audit for '{referenceKey}'.");
        }

        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "SecretCredentialBundleRuntime.rotate: post-rotation validation failed referenceKey={ReferenceKey} failureCode={FailureCode}",
                referenceKey, validation.FailureCode);
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError(
                    validation.FailureCode ?? "CREDENTIAL_POST_ROTATION_VALIDATION_FAILED",
                    validation.FailureReason ?? "Post-rotation credential validation failed.")]);
        }

        _logger.LogInformation(
            "SecretCredentialBundleRuntime.rotate: rotation recorded and validated for referenceKey={ReferenceKey}",
            referenceKey);

        var data = JsonSerializer.SerializeToElement(new
        {
            reference_key = referenceKey,
            rotated = true,
        });

        return Success(data);
    }

    // ─── list ────────────────────────────────────────────────────────────────

    private async Task<EndpointResponseDto> HandleListAsync(
        EndpointRequestDto request,
        CancellationToken ct)
    {
        _logger.LogDebug("SecretCredentialBundleRuntime.list");

        var references = await _repository.ListAsync(ct);

        var items = references.Select(r => new
        {
            credential_reference_id = r.CredentialReferenceId,
            reference_key = r.ReferenceKey,
            provider_kind = r.ProviderKind,
            status = r.Status,
            validation_status = r.ValidationStatus,
            last_validated_at = r.LastValidatedAt,
            last_rotated_at = r.LastRotatedAt,
            registered_at = r.RegisteredAt,
        }).ToArray();

        var data = JsonSerializer.SerializeToElement(new { items });
        return Success(data);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static EndpointResponseDto ExplicitError(string code, string message) =>
        new(Success: false, Emission: null, Errors: [new ValidationError(code, message)]);

    private static EndpointResponseDto Success(JsonElement data) =>
        new(Success: true,
            Emission: new Emission(
                StructureMapId: null,
                PackageId: null,
                SchemaId: null,
                ComponentIds: [],
                Data: data,
                Errors: []),
            Errors: []);

    private static JsonElement BuildReferenceData(CredentialReferenceDto r) =>
        JsonSerializer.SerializeToElement(new
        {
            credential_reference_id = r.CredentialReferenceId,
            reference_key = r.ReferenceKey,
            provider_kind = r.ProviderKind,
            status = r.Status,
            validation_status = r.ValidationStatus,
            registered_at = r.RegisteredAt,
        });

    private static bool TryExtractString(JsonElement? payload, string key, out string? value)
    {
        value = null;
        if (!payload.HasValue || payload.Value.ValueKind != JsonValueKind.Object)
            return false;
        if (!payload.Value.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String)
            return false;
        value = el.GetString();
        return true;
    }
}
