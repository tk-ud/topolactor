using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — credential-management: external_api_credential.consumer_reference_binding
// (docs/design/admin-normal-surface-projection-seed-ssot.yaml
//  surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding,
//  docs/design/scheduler-job-manifest-ssot.yaml credential_boundary /
//  authoring_surface.admin_surface_split_2026_07_22.credential_and_external_port_binding).
//
// Entry: AdminRuntime.ExecuteDataAsync layer=credential_management
//        action=configure_scheduler_job_credential_or_port_binding
//
// Boundary invariants (mutation_boundary, admin-normal-surface-projection-seed-ssot.yaml):
//   - writes ONLY topology.scheduler_jobs.credential_requirement_ref / .external_port_ref,
//     identified by scheduler_job_id plus the given credential/port row's reference_key
//   - never writes topology.external_credential_vault / external_access_ports /
//     external_response_ports / external_hook_ports rows (those stay the external_api_credential
//     category's own create/read/update/delete/search operations)
//   - never authors scheduler_jobs.job_key / trigger_kind / schedule_policy_kind /
//     cron_expression / abstract_function_step_chain or any other /admin/contents-authored field
//     — job body/step-chain authoring stays entirely on
//     AdminRuntime.SchedulerSettings.cs's DataCreateSchedulerJobAsync/DataEditSchedulerJobAsync
//   - reference_key values are identifiers only; no credential plaintext ever appears in payload,
//     preview, or diff_log
//
// mutation_confirmation_contract: preview/validate (payload.dryRun=true, non-mutating) ->
// explicit_confirm (frontend UI) -> write (payload.confirmed=true) -> diff_log
// (AdminMasterRosterAudit.AppendAsync) — the same generic confirmation contract
// AdminRuntimeMasterRoster.cs's enum-dictionary write actions already use, not a new mechanism.
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    private async Task<(JsonElement? data, ValidationError? error)>
        DataConfigureSchedulerJobCredentialOrPortBindingAsync(OperationVector vector, CancellationToken ct)
    {
        if (_schedulerJobManifestRepository is null)
            return (null, SchedulerRepoNotConfigured());

        var request = DeserializePayload<CredentialManagementSchedulerBindingRequestDto>(vector.Payload);
        if (request is null || !Guid.TryParse(request.SchedulerJobId, out var schedulerJobId))
            return (null, new ValidationError(
                "CREDENTIAL_MANAGEMENT_SCHEDULER_JOB_ID_MALFORMED",
                "payload.schedulerJobId must be a valid UUID."));

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                schedulerJobId,
                preview = new
                {
                    credentialRequirementRef = request.CredentialRequirementRef,
                    externalPortRef = request.ExternalPortRef,
                },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError(
                "CREDENTIAL_MANAGEMENT_SCHEDULER_BINDING_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var result = await _schedulerJobManifestRepository.UpdateCredentialBindingAsync(
            schedulerJobId, request.CredentialRequirementRef, request.ExternalPortRef, ct);

        var error = result.Outcome switch
        {
            SchedulerJobCredentialBindingOutcome.SchedulerJobNotFound => new ValidationError(
                "SCHEDULER_JOB_NOT_FOUND", $"Scheduler job {schedulerJobId} was not found."),
            SchedulerJobCredentialBindingOutcome.CredentialRequirementRefNotFound => new ValidationError(
                "CREDENTIAL_REQUIREMENT_REF_NOT_FOUND",
                $"credentialRequirementRef '{request.CredentialRequirementRef}' does not match any existing " +
                "external_credential_vault reference_key."),
            SchedulerJobCredentialBindingOutcome.ExternalPortRefNotFound => new ValidationError(
                "EXTERNAL_PORT_REF_NOT_FOUND",
                $"externalPortRef '{request.ExternalPortRef}' does not match any existing " +
                "access_port/response_port/hook_port reference_key."),
            _ => null,
        };
        if (error is not null) return (null, error);

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "topology.scheduler_jobs", schedulerJobId.ToString(), "configure_credential_binding",
            new { credentialRequirementRef = result.PreviousCredentialRequirementRef, externalPortRef = result.PreviousExternalPortRef },
            new { credentialRequirementRef = request.CredentialRequirementRef, externalPortRef = request.ExternalPortRef },
            [
                new AuditChangedField(
                    "credential_requirement_ref", result.PreviousCredentialRequirementRef, request.CredentialRequirementRef),
                new AuditChangedField(
                    "external_port_ref", result.PreviousExternalPortRef, request.ExternalPortRef),
            ], ct);

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            schedulerJobId,
            credentialRequirementRef = request.CredentialRequirementRef,
            externalPortRef = request.ExternalPortRef,
        }), null);
    }
}
