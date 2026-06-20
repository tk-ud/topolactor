using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — Scheduler Job Settings Projection (read-only).
//
// Entry: AdminRuntime.ExecuteDataAsync layer=scheduler_jobs action=list_settings
//
// Returns the full scheduler job manifest from DB (all jobs, including inactive).
// Boundary invariants:
//   - read-only projection: no create/update/delete through this surface
//   - credential_requirement_ref and external_port_ref are returned as reference keys only
//     (no credential plaintext, no decrypted payload)
//   - payload-derived table/column/output authority must NOT appear in the projection
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    private readonly ISchedulerJobManifestRepository? _schedulerJobManifestRepository;

    private async Task<(JsonElement? data, ValidationError? error)>
        DataListSchedulerJobsSettingsAsync(CancellationToken ct)
    {
        if (_schedulerJobManifestRepository is null)
            return (null, new ValidationError(
                "SCHEDULER_JOB_MANIFEST_NOT_CONFIGURED",
                "ISchedulerJobManifestRepository is not registered"));

        var jobs = await _schedulerJobManifestRepository.LoadSettingsProjectionAsync(ct);

        var projection = jobs.Select(static j => new
        {
            schedulerJobId          = j.SchedulerJobId,
            jobKey                  = j.JobKey,
            triggerKind             = j.TriggerKind,
            schedulePolicyKind      = j.SchedulePolicyKind,
            cronExpression          = j.CronExpression,
            scheduleIntervalSeconds = j.ScheduleIntervalSeconds,
            manualRunAllowed        = j.ManualRunAllowed,
            active                  = j.Active,
            maxBatchSize            = j.MaxBatchSize,
            leaseSeconds            = j.LeaseSeconds,
            authorityScope          = j.AuthorityScope,
            credentialRequirementRef = j.CredentialRequirementRef,
            externalPortRef         = j.ExternalPortRef,
        }).ToList();

        return (JsonSerializer.SerializeToElement(new { ok = true, schedulerJobs = projection }), null);
    }
}
