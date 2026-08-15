using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — credential-management: external_api_credential category
// (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
//  credentials.categories.external_api_credential;
//  docs/design/external-port-substrate-ssot.yaml admin_setting_projection).
//
// Entry: AdminRuntime.ExecuteDataAsync layer=external_api_credential
//        action=search | get | create | update | delete
//
// Boundary invariants:
//   - existing_schema_fields_allowed_for_projection only: credential_vault_id/access_port_id/
//     response_port_id/hook_port_id, provider_kind, required_by_bundle, token_kind, expires_at,
//     refresh_before_seconds, version, active, reference_key, created_at, updated_at, plus the
//     port-only url_or_env_reference/credential_kind/hook_path/header_key/route_key -- token_hash,
//     encrypted_payload, encryption_key_reference, plaintext secret material, and decrypted
//     payload NEVER appear in any response, preview, or diff_log built by this file.
//   - mutation_boundary: "replace or rotate by reference_key and provider/bundle metadata;
//     decrypted material is runtime-only" -- create/update accept a write-only plaintextSecret /
//     newPlaintextSecret input (vault recordKind only), immediately encrypted via
//     IExternalApiCredentialAdminRepository (AesExternalCredentialCrypto), never stored on the
//     response record, never included in the audit before/after envelope.
//   - consumer_reference_binding (AdminRuntime.CredentialManagement.cs) writes ONLY
//     topology.scheduler_jobs.credential_requirement_ref/.external_port_ref by reference; it never
//     writes these four tables. This file is the reverse: it owns create/read/update/delete/search
//     over topology.external_credential_vault/external_access_ports/external_response_ports/
//     external_hook_ports and never touches topology.scheduler_jobs.
//
// mutation_confirmation_contract (create/update/delete): preview/validate (payload.dryRun=true,
// non-mutating, real repository-backed validation -- never a bare unchecked echo) -> explicit_confirm
// (frontend UI) -> write (payload.confirmed=true) -> diff_log (AdminMasterRosterAudit.AppendAsync,
// sanitized envelope). search/get are plain reads, no confirmation stage.
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    private static object ToProjection(ExternalApiCredentialRecord r) => new
    {
        recordKind = r.RecordKind,
        recordId = r.RecordId,
        providerKind = r.ProviderKind,
        requiredByBundle = r.RequiredByBundle,
        referenceKey = r.ReferenceKey,
        active = r.Active,
        createdAt = r.CreatedAt,
        updatedAt = r.UpdatedAt,
        tokenKind = r.TokenKind,
        expiresAt = r.ExpiresAt,
        refreshBeforeSeconds = r.RefreshBeforeSeconds,
        version = r.Version,
        urlOrEnvReference = r.UrlOrEnvReference,
        credentialKind = r.CredentialKind,
        hookPath = r.HookPath,
        headerKey = r.HeaderKey,
        routeKey = r.RouteKey,
    };

    // Sanitized create/update audit envelope -- metadata fields only, matching ToProjection's own
    // field set; PlaintextSecret/NewPlaintextSecret/EncryptionKeyReference are never included,
    // satisfying diff_log_without_secret.
    private static object ToAuditMetadata(
        string? providerKind, string? requiredByBundle, string? referenceKey, bool? active,
        string? tokenKind, int? refreshBeforeSeconds, string? urlOrEnvReference, string? credentialKind,
        string? hookPath, string? headerKey, string? routeKey) => new
    {
        providerKind,
        requiredByBundle,
        referenceKey,
        active,
        tokenKind,
        refreshBeforeSeconds,
        urlOrEnvReference,
        credentialKind,
        hookPath,
        headerKey,
        routeKey,
    };

    private static ValidationError ExternalApiCredentialNotAvailable() =>
        new("EXTERNAL_API_CREDENTIAL_NOT_AVAILABLE", "IExternalApiCredentialAdminRepository is not registered");

    private static ValidationError? ToValidationError(ExternalApiCredentialWriteResult result) => result.Outcome switch
    {
        ExternalApiCredentialOutcome.RecordKindInvalid => new ValidationError(
            "EXTERNAL_API_CREDENTIAL_RECORD_KIND_INVALID", result.Detail ?? "recordKind is invalid."),
        ExternalApiCredentialOutcome.NotFound => new ValidationError(
            "EXTERNAL_API_CREDENTIAL_NOT_FOUND", result.Detail ?? "Record was not found."),
        ExternalApiCredentialOutcome.RequiredFieldMissing => new ValidationError(
            "EXTERNAL_API_CREDENTIAL_REQUIRED_FIELD_MISSING", result.Detail ?? "A required field is missing."),
        ExternalApiCredentialOutcome.ProhibitedFieldPresent => new ValidationError(
            "EXTERNAL_API_CREDENTIAL_PROHIBITED_FIELD_PRESENT", result.Detail ?? "A prohibited field was present."),
        _ => null,
    };

    private async Task<(JsonElement? data, ValidationError? error)>
        DataExternalApiCredentialSearchAsync(OperationVector vector, CancellationToken ct)
    {
        if (_externalApiCredentialAdminRepository is null)
            return (null, ExternalApiCredentialNotAvailable());

        var request = DeserializePayload<ExternalApiCredentialSearchRequestDto>(vector.Payload) ??
            new ExternalApiCredentialSearchRequestDto();
        if (request.RecordKind is not null && !ExternalApiCredentialRecordKinds.All.Contains(request.RecordKind))
            return (null, new ValidationError(
                "EXTERNAL_API_CREDENTIAL_RECORD_KIND_INVALID", $"Unknown recordKind '{request.RecordKind}'."));

        var results = await _externalApiCredentialAdminRepository.SearchAsync(
            request.Query, request.RecordKind, request.ProviderKind, request.RequiredByBundle, request.Active, ct);

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            records = results.Select(ToProjection).ToList(),
        }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataExternalApiCredentialGetAsync(OperationVector vector, CancellationToken ct)
    {
        if (_externalApiCredentialAdminRepository is null)
            return (null, ExternalApiCredentialNotAvailable());

        var request = DeserializePayload<ExternalApiCredentialGetRequestDto>(vector.Payload);
        if (request is null || !ExternalApiCredentialRecordKinds.All.Contains(request.RecordKind) ||
            !Guid.TryParse(request.RecordId, out var recordId))
            return (null, new ValidationError(
                "EXTERNAL_API_CREDENTIAL_GET_PAYLOAD_INVALID", "recordKind (known) and recordId (UUID) are required."));

        var record = await _externalApiCredentialAdminRepository.GetAsync(request.RecordKind, recordId, ct);
        if (record is null)
            return (null, new ValidationError(
                "EXTERNAL_API_CREDENTIAL_NOT_FOUND", $"{request.RecordKind} {recordId} was not found."));

        return (JsonSerializer.SerializeToElement(new { ok = true, record = ToProjection(record) }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataExternalApiCredentialCreateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_externalApiCredentialAdminRepository is null)
            return (null, ExternalApiCredentialNotAvailable());

        var request = DeserializePayload<ExternalApiCredentialCreateRequestDto>(vector.Payload);
        if (request is null)
            return (null, new ValidationError("EXTERNAL_API_CREDENTIAL_CREATE_PAYLOAD_INVALID", "payload could not be parsed."));

        var createRequest = new ExternalApiCredentialCreateRequest(
            request.RecordKind, request.ProviderKind, request.RequiredByBundle, request.ReferenceKey,
            request.TokenKind, request.RefreshBeforeSeconds, request.UrlOrEnvReference, request.CredentialKind,
            request.HookPath, request.HeaderKey, request.RouteKey, request.PlaintextSecret, request.EncryptionKeyReference);

        // validate_policy_and_transaction_boundary: runs identically for dryRun and confirmed -- an
        // invalid candidate is never reported valid without ever having been checked.
        var validation = _externalApiCredentialAdminRepository.ValidateCreateRequest(createRequest);
        var validationError = ToValidationError(validation);
        if (validationError is not null) return (null, validationError);

        var auditMetadata = ToAuditMetadata(
            request.ProviderKind, request.RequiredByBundle, request.ReferenceKey, true,
            request.TokenKind, request.RefreshBeforeSeconds, request.UrlOrEnvReference, request.CredentialKind,
            request.HookPath, request.HeaderKey, request.RouteKey);

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                recordKind = request.RecordKind,
                preview = auditMetadata,
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("EXTERNAL_API_CREDENTIAL_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var result = await _externalApiCredentialAdminRepository.CreateAsync(createRequest, ct);
        var writeError = ToValidationError(result);
        if (writeError is not null) return (null, writeError);

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            $"topology.external_{request.RecordKind}", result.Record!.RecordId.ToString(), "create",
            null, auditMetadata,
            [new AuditChangedField("record", null, auditMetadata)], ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, record = ToProjection(result.Record!) }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataExternalApiCredentialUpdateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_externalApiCredentialAdminRepository is null)
            return (null, ExternalApiCredentialNotAvailable());

        var request = DeserializePayload<ExternalApiCredentialUpdateRequestDto>(vector.Payload);
        if (request is null || !Guid.TryParse(request.RecordId, out var recordId))
            return (null, new ValidationError(
                "EXTERNAL_API_CREDENTIAL_UPDATE_PAYLOAD_INVALID", "recordId must be a valid UUID."));

        var updateRequest = new ExternalApiCredentialUpdateRequest(
            request.RecordKind, recordId, request.ProviderKind, request.RequiredByBundle, request.ReferenceKey,
            request.Active, request.TokenKind, request.RefreshBeforeSeconds, request.UrlOrEnvReference,
            request.CredentialKind, request.HookPath, request.HeaderKey, request.RouteKey,
            request.NewPlaintextSecret, request.EncryptionKeyReference);

        var validation = await _externalApiCredentialAdminRepository.ValidateUpdateRequestAsync(updateRequest, ct);
        var validationError = ToValidationError(validation);
        if (validationError is not null) return (null, validationError);
        var before = validation.Record!;

        var auditMetadata = ToAuditMetadata(
            request.ProviderKind, request.RequiredByBundle, request.ReferenceKey, request.Active,
            request.TokenKind, request.RefreshBeforeSeconds, request.UrlOrEnvReference, request.CredentialKind,
            request.HookPath, request.HeaderKey, request.RouteKey);

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                recordKind = request.RecordKind,
                recordId,
                preview = auditMetadata,
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("EXTERNAL_API_CREDENTIAL_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var result = await _externalApiCredentialAdminRepository.UpdateAsync(updateRequest, ct);
        var writeError = ToValidationError(result);
        if (writeError is not null) return (null, writeError);

        var beforeMetadata = ToAuditMetadata(
            before.ProviderKind, before.RequiredByBundle, before.ReferenceKey, before.Active,
            before.TokenKind, before.RefreshBeforeSeconds, before.UrlOrEnvReference, before.CredentialKind,
            before.HookPath, before.HeaderKey, before.RouteKey);

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            $"topology.external_{request.RecordKind}", recordId.ToString(), "update",
            beforeMetadata, auditMetadata,
            [new AuditChangedField("record", beforeMetadata, auditMetadata)], ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, record = ToProjection(result.Record!) }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataExternalApiCredentialDeleteAsync(OperationVector vector, CancellationToken ct)
    {
        if (_externalApiCredentialAdminRepository is null)
            return (null, ExternalApiCredentialNotAvailable());

        var request = DeserializePayload<ExternalApiCredentialDeleteRequestDto>(vector.Payload);
        if (request is null || !Guid.TryParse(request.RecordId, out var recordId))
            return (null, new ValidationError(
                "EXTERNAL_API_CREDENTIAL_DELETE_PAYLOAD_INVALID", "recordId must be a valid UUID."));

        var existing = await _externalApiCredentialAdminRepository.GetAsync(request.RecordKind, recordId, ct);
        if (existing is null)
            return (null, new ValidationError(
                "EXTERNAL_API_CREDENTIAL_NOT_FOUND", $"{request.RecordKind} {recordId} was not found."));

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                recordKind = request.RecordKind,
                recordId,
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("EXTERNAL_API_CREDENTIAL_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var outcome = await _externalApiCredentialAdminRepository.DeactivateAsync(request.RecordKind, recordId, ct);
        if (outcome != ExternalApiCredentialOutcome.Ok)
            return (null, new ValidationError(
                "EXTERNAL_API_CREDENTIAL_NOT_FOUND", $"{request.RecordKind} {recordId} was not found or already inactive."));

        var beforeMetadata = ToAuditMetadata(
            existing.ProviderKind, existing.RequiredByBundle, existing.ReferenceKey, true,
            existing.TokenKind, existing.RefreshBeforeSeconds, existing.UrlOrEnvReference, existing.CredentialKind,
            existing.HookPath, existing.HeaderKey, existing.RouteKey);
        var afterMetadata = ToAuditMetadata(
            existing.ProviderKind, existing.RequiredByBundle, existing.ReferenceKey, false,
            existing.TokenKind, existing.RefreshBeforeSeconds, existing.UrlOrEnvReference, existing.CredentialKind,
            existing.HookPath, existing.HeaderKey, existing.RouteKey);

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            $"topology.external_{request.RecordKind}", recordId.ToString(), "delete",
            beforeMetadata, afterMetadata,
            [new AuditChangedField("active", true, false)], ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, recordKind = request.RecordKind, recordId }), null);
    }
}
