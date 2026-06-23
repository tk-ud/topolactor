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

    // ── admin.contents authoring surface ──────────────────────────────────────
    // create / edit / disable scheduler job manifests as data-defined records.
    // Frontend holds no runtime judgment / SQL / credential authority; it submits a
    // manifest draft to the admin_runtime authority boundary. Only references and
    // policy data are accepted — secret material is fail-closed.

    private static readonly string[] ProhibitedSecretFields =
    {
        "api_key", "access_token", "refresh_token", "client_secret",
        "decrypted_payload", "token_response", "token_body", "credential", "credential_payload",
    };

    private static readonly string[] ValidTriggerKinds = { "cron", "hook", "client" };
    private static readonly string[] ValidSchedulePolicyKinds = { "cron", "interval_seconds", "manual_only" };

    private async Task<(JsonElement? data, ValidationError? error)>
        DataCreateSchedulerJobAsync(OperationVector vector, CancellationToken ct)
    {
        if (_schedulerJobManifestRepository is null)
            return (null, SchedulerRepoNotConfigured());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload is required."));

        var (draft, error) = ParseDraft(vector.Payload.Value);
        if (error is not null) return (null, error);

        var id = await _schedulerJobManifestRepository.CreateJobAsync(draft!, ct);
        return (JsonSerializer.SerializeToElement(new { ok = true, schedulerJobId = id, jobKey = draft!.JobKey }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataEditSchedulerJobAsync(OperationVector vector, CancellationToken ct)
    {
        if (_schedulerJobManifestRepository is null)
            return (null, SchedulerRepoNotConfigured());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload is required."));

        var payload = vector.Payload.Value;
        if (!payload.TryGetProperty("schedulerJobId", out var idEl) || !Guid.TryParse(idEl.GetString(), out var schedulerJobId))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.schedulerJobId is required."));

        var (draft, error) = ParseDraft(payload);
        if (error is not null) return (null, error);

        var updated = await _schedulerJobManifestRepository.UpdateJobAsync(schedulerJobId, draft!, ct);
        if (!updated)
            return (null, new ValidationError("SCHEDULER_JOB_NOT_EDITABLE",
                "Scheduler job not found or is active. Disable the job before editing."));
        return (JsonSerializer.SerializeToElement(new { ok = true, schedulerJobId, jobKey = draft!.JobKey }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)>
        DataDisableSchedulerJobAsync(OperationVector vector, CancellationToken ct)
    {
        if (_schedulerJobManifestRepository is null)
            return (null, SchedulerRepoNotConfigured());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object ||
            !vector.Payload.Value.TryGetProperty("schedulerJobId", out var idEl) ||
            !Guid.TryParse(idEl.GetString(), out var schedulerJobId))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.schedulerJobId is required."));

        var disabled = await _schedulerJobManifestRepository.SetJobActiveAsync(schedulerJobId, false, ct);
        if (!disabled)
            return (null, new ValidationError("SCHEDULER_JOB_NOT_FOUND", "Scheduler job not found."));
        return (JsonSerializer.SerializeToElement(new { ok = true, schedulerJobId, active = false }), null);
    }

    private static ValidationError SchedulerRepoNotConfigured() =>
        new("SCHEDULER_JOB_MANIFEST_NOT_CONFIGURED", "ISchedulerJobManifestRepository is not registered");

    private static (SchedulerJobDraft? draft, ValidationError? error) ParseDraft(JsonElement payload)
    {
        // Fail-close on any prohibited secret field — credential plaintext is never authored here.
        foreach (var secret in ProhibitedSecretFields)
            if (payload.TryGetProperty(secret, out _))
                return (null, new ValidationError("SCHEDULER_JOB_SECRET_FIELD_FORBIDDEN",
                    $"Field '{secret}' is forbidden in scheduler job authoring; use credentialRequirementRef / externalPortRef references only."));

        var jobKey = payload.TryGetProperty("jobKey", out var jk) ? jk.GetString() : null;
        if (string.IsNullOrWhiteSpace(jobKey))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.jobKey is required."));

        var authorityScope = payload.TryGetProperty("authorityScope", out var asEl) ? asEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(authorityScope))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.authorityScope is required."));

        var triggerKind = payload.TryGetProperty("triggerKind", out var tk) ? tk.GetString() ?? "cron" : "cron";
        if (!ValidTriggerKinds.Contains(triggerKind))
            return (null, new ValidationError("SCHEDULER_JOB_TRIGGER_KIND_INVALID", "triggerKind must be cron|hook|client."));

        var policyKind = payload.TryGetProperty("schedulePolicyKind", out var pk) ? pk.GetString() ?? "manual_only" : "manual_only";
        if (!ValidSchedulePolicyKinds.Contains(policyKind))
            return (null, new ValidationError("SCHEDULER_JOB_SCHEDULE_POLICY_KIND_INVALID", "schedulePolicyKind must be cron|interval_seconds|manual_only."));

        string? cron = payload.TryGetProperty("cronExpression", out var ce) && ce.ValueKind == JsonValueKind.String ? ce.GetString() : null;
        long? interval = payload.TryGetProperty("scheduleIntervalSeconds", out var iv) && iv.ValueKind == JsonValueKind.Number ? iv.GetInt64() : null;

        if (policyKind == "cron" && string.IsNullOrWhiteSpace(cron))
            return (null, new ValidationError("SCHEDULER_JOB_CRON_REQUIRED", "cronExpression is required when schedulePolicyKind=cron."));
        if (policyKind == "interval_seconds" && (interval is null || interval <= 0))
            return (null, new ValidationError("SCHEDULER_JOB_INTERVAL_REQUIRED", "scheduleIntervalSeconds (>0) is required when schedulePolicyKind=interval_seconds."));

        var manual = payload.TryGetProperty("manualRunAllowed", out var mr) && (mr.ValueKind == JsonValueKind.True || mr.ValueKind == JsonValueKind.False) && mr.GetBoolean();
        var active = payload.TryGetProperty("active", out var ac) && (ac.ValueKind == JsonValueKind.True || ac.ValueKind == JsonValueKind.False) && ac.GetBoolean();
        var batch = payload.TryGetProperty("maxBatchSize", out var bs) && bs.ValueKind == JsonValueKind.Number ? bs.GetInt32() : 10;
        var lease = payload.TryGetProperty("leaseSeconds", out var ls) && ls.ValueKind == JsonValueKind.Number ? ls.GetInt32() : 300;
        string? credRef = payload.TryGetProperty("credentialRequirementRef", out var cr) && cr.ValueKind == JsonValueKind.String ? cr.GetString() : null;
        string? portRef = payload.TryGetProperty("externalPortRef", out var pr) && pr.ValueKind == JsonValueKind.String ? pr.GetString() : null;

        return (new SchedulerJobDraft(
            jobKey!, triggerKind, policyKind, cron, interval, manual, active,
            authorityScope!, batch, lease, credRef, portRef), null);
    }
}
