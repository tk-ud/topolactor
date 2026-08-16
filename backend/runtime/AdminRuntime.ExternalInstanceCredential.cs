using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — credential-management: external_instance_credential category
// (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
//  credentials.categories.external_instance_credential;
//  docs/design/instance-port-substrate-ssot.yaml existing_credential_management_projection_extension).
//
// Entry: AdminRuntime.ExecuteDataAsync layer=external_instance_credential
//        action=get | create | update | delete (search is unified -- see
//        AdminRuntime.CredentialManagementSearch.cs)
//
// Boundary invariants:
//   - existing_schema_fields_allowed_for_projection only: reference_key, provider_kind,
//     required_by_bundle, active, expires_at, version -- token_hash, encrypted_payload,
//     encryption_key_reference, plaintext secret material, and decrypted payload NEVER appear in
//     any response, preview, or diff_log built by this file (they are never even queried --
//     NpgsqlExternalInstanceCredentialAdminRepository's SELECT list structurally excludes them).
//   - mutation_boundary: "instance credential settings may be selected/updated only through
//     existing credential-management projection extension; no new credential table or plane is
//     defined here." create/update accept only a referenceKey pointing at an EXISTING
//     topology.external_credential_vault row (fail-closed with
//     EXTERNAL_INSTANCE_CREDENTIAL_REFERENCE_KEY_NOT_FOUND otherwise) -- this file never accepts a
//     plaintext secret and never writes external_credential_vault.
//
// mutation_confirmation_contract (create/update/delete): preview/validate (payload.dryRun=true,
// non-mutating, real repository-backed validation) -> explicit_confirm (frontend UI) -> write
// (payload.confirmed=true) -> diff_log (AdminMasterRosterAudit.AppendAsync, sanitized envelope).
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    private static object ToProjection(ExternalInstanceCredentialRecord r) => new
    {
        recordKind = r.RecordKind,
        recordId = r.RecordId,
        instanceAuthorityKey = r.InstanceAuthorityKey,
        providerKind = r.ProviderKind,
        requiredByBundle = r.RequiredByBundle,
        referenceKey = r.ReferenceKey,
        active = r.Active,
        createdAt = r.CreatedAt,
        updatedAt = r.UpdatedAt,
        expiresAt = r.ExpiresAt,
        version = r.Version,
    };

    private static object ToAuditMetadata(
        string instanceAuthorityKey, string? providerKind, string? requiredByBundle, string? referenceKey, bool? active) => new
    {
        instanceAuthorityKey,
        providerKind,
        requiredByBundle,
        referenceKey,
        active,
    };

    private static ValidationError ExternalInstanceCredentialNotAvailable() =>
        new("EXTERNAL_INSTANCE_CREDENTIAL_NOT_AVAILABLE", "IExternalInstanceCredentialAdminRepository is not registered");

    private static ValidationError? ToValidationError(ExternalInstanceCredentialWriteResult result) => result.Outcome switch
    {
        ExternalInstanceCredentialOutcome.RecordKindInvalid => new ValidationError(
            "EXTERNAL_INSTANCE_CREDENTIAL_RECORD_KIND_INVALID", result.Detail ?? "recordKind is invalid."),
        ExternalInstanceCredentialOutcome.NotFound => new ValidationError(
            "EXTERNAL_INSTANCE_CREDENTIAL_NOT_FOUND", result.Detail ?? "Record was not found."),
        ExternalInstanceCredentialOutcome.RequiredFieldMissing => new ValidationError(
            "EXTERNAL_INSTANCE_CREDENTIAL_REQUIRED_FIELD_MISSING", result.Detail ?? "A required field is missing."),
        ExternalInstanceCredentialOutcome.ReferenceKeyNotFound => new ValidationError(
            "EXTERNAL_INSTANCE_CREDENTIAL_REFERENCE_KEY_NOT_FOUND", result.Detail ?? "referenceKey does not exist."),
        _ => null,
    };

    private async Task<(JsonElement? data, ValidationError? error)>
        DataExternalInstanceCredentialGetAsync(OperationVector vector, CancellationToken ct)
    {
        if (_externalInstanceCredentialAdminRepository is null)
            return (null, ExternalInstanceCredentialNotAvailable());

        var request = DeserializePayload<ExternalInstanceCredentialGetRequestDto>(vector.Payload);
        if (request is null || !ExternalInstanceCredentialRecordKinds.All.Contains(request.RecordKind) ||
            !Guid.TryParse(request.RecordId, out var recordId))
            return (null, new ValidationError(
                "EXTERNAL_INSTANCE_CREDENTIAL_GET_PAYLOAD_INVALID", "recordKind (known) and recordId (UUID) are required."));

        var record = await _externalInstanceCredentialAdminRepository.GetAsync(request.RecordKind, recordId, ct);
        if (record is null)
            return (null, new ValidationError(
                "EXTERNAL_INSTANCE_CREDENTIAL_NOT_FOUND", $"{request.RecordKind} {recordId} was not found."));

        return (JsonSerializer.SerializeToElement(new { ok = true, record = ToProjection(record) }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataExternalInstanceCredentialCreateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_externalInstanceCredentialAdminRepository is null)
            return (null, ExternalInstanceCredentialNotAvailable());

        var request = DeserializePayload<ExternalInstanceCredentialCreateRequestDto>(vector.Payload);
        if (request is null)
            return (null, new ValidationError("EXTERNAL_INSTANCE_CREDENTIAL_CREATE_PAYLOAD_INVALID", "payload could not be parsed."));

        var createRequest = new ExternalInstanceCredentialCreateRequest(
            request.RecordKind, request.InstanceAuthorityKey, request.ProviderKind, request.RequiredByBundle, request.ReferenceKey);

        // Async validation (field checks + reference_key-exists-in-vault) so dryRun reports the
        // SAME real outcome the confirmed write would reach -- never a partial check that skips
        // the FK-shaped precondition CreateAsync itself enforces.
        var validation = await _externalInstanceCredentialAdminRepository.ValidateCreateRequestAsync(createRequest, ct);
        var validationError = ToValidationError(validation);
        if (validationError is not null) return (null, validationError);

        var auditMetadata = ToAuditMetadata(
            request.InstanceAuthorityKey, request.ProviderKind, request.RequiredByBundle, request.ReferenceKey, true);

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
            return (null, new ValidationError("EXTERNAL_INSTANCE_CREDENTIAL_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var result = await _externalInstanceCredentialAdminRepository.CreateAsync(createRequest, ct);
        var writeError = ToValidationError(result);
        if (writeError is not null) return (null, writeError);

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            ExternalInstanceCredentialRecordKinds.CanonicalPhysicalTableNames[request.RecordKind], result.Record!.RecordId.ToString(), "create",
            null, auditMetadata,
            [new AuditChangedField("record", null, auditMetadata)], ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, record = ToProjection(result.Record!) }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataExternalInstanceCredentialUpdateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_externalInstanceCredentialAdminRepository is null)
            return (null, ExternalInstanceCredentialNotAvailable());

        var request = DeserializePayload<ExternalInstanceCredentialUpdateRequestDto>(vector.Payload);
        if (request is null || !Guid.TryParse(request.RecordId, out var recordId))
            return (null, new ValidationError(
                "EXTERNAL_INSTANCE_CREDENTIAL_UPDATE_PAYLOAD_INVALID", "recordId must be a valid UUID."));

        var updateRequest = new ExternalInstanceCredentialUpdateRequest(
            request.RecordKind, recordId, request.ProviderKind, request.RequiredByBundle, request.ReferenceKey, request.Active);

        var validation = await _externalInstanceCredentialAdminRepository.ValidateUpdateRequestAsync(updateRequest, ct);
        var validationError = ToValidationError(validation);
        if (validationError is not null) return (null, validationError);
        var before = validation.Record!;

        var auditMetadata = ToAuditMetadata(
            before.InstanceAuthorityKey, request.ProviderKind, request.RequiredByBundle, request.ReferenceKey, request.Active);

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
            return (null, new ValidationError("EXTERNAL_INSTANCE_CREDENTIAL_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var result = await _externalInstanceCredentialAdminRepository.UpdateAsync(updateRequest, ct);
        var writeError = ToValidationError(result);
        if (writeError is not null) return (null, writeError);

        var beforeMetadata = ToAuditMetadata(
            before.InstanceAuthorityKey, before.ProviderKind, before.RequiredByBundle, before.ReferenceKey, before.Active);

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            ExternalInstanceCredentialRecordKinds.CanonicalPhysicalTableNames[request.RecordKind], recordId.ToString(), "update",
            beforeMetadata, auditMetadata,
            [new AuditChangedField("record", beforeMetadata, auditMetadata)], ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, record = ToProjection(result.Record!) }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataExternalInstanceCredentialDeleteAsync(OperationVector vector, CancellationToken ct)
    {
        if (_externalInstanceCredentialAdminRepository is null)
            return (null, ExternalInstanceCredentialNotAvailable());

        var request = DeserializePayload<ExternalInstanceCredentialDeleteRequestDto>(vector.Payload);
        if (request is null || !Guid.TryParse(request.RecordId, out var recordId))
            return (null, new ValidationError(
                "EXTERNAL_INSTANCE_CREDENTIAL_DELETE_PAYLOAD_INVALID", "recordId must be a valid UUID."));

        var existing = await _externalInstanceCredentialAdminRepository.GetAsync(request.RecordKind, recordId, ct);
        if (existing is null)
            return (null, new ValidationError(
                "EXTERNAL_INSTANCE_CREDENTIAL_NOT_FOUND", $"{request.RecordKind} {recordId} was not found."));

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
            return (null, new ValidationError("EXTERNAL_INSTANCE_CREDENTIAL_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var outcome = await _externalInstanceCredentialAdminRepository.DeactivateAsync(request.RecordKind, recordId, ct);
        if (outcome != ExternalInstanceCredentialOutcome.Ok)
            return (null, new ValidationError(
                "EXTERNAL_INSTANCE_CREDENTIAL_NOT_FOUND", $"{request.RecordKind} {recordId} was not found or already inactive."));

        var beforeMetadata = ToAuditMetadata(existing.InstanceAuthorityKey, existing.ProviderKind, existing.RequiredByBundle, existing.ReferenceKey, true);
        var afterMetadata = ToAuditMetadata(existing.InstanceAuthorityKey, existing.ProviderKind, existing.RequiredByBundle, existing.ReferenceKey, false);

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            ExternalInstanceCredentialRecordKinds.CanonicalPhysicalTableNames[request.RecordKind], recordId.ToString(), "delete",
            beforeMetadata, afterMetadata,
            [new AuditChangedField("active", true, false)], ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, recordKind = request.RecordKind, recordId }), null);
    }
}
