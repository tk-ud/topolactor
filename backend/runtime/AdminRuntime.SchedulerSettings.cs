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

    private static readonly string[] ValidOnError = { "fail_run", "retry", "skip_input", "mark_input_failed" };

    private static (SchedulerJobDraft? draft, ValidationError? error) ParseDraft(JsonElement payload)
    {
        // Fail-close on any prohibited secret field anywhere in the draft (header or steps).
        if (ContainsSecretKey(payload, out var secretKey))
            return (null, new ValidationError("SCHEDULER_JOB_SECRET_FIELD_FORBIDDEN",
                $"Field '{secretKey}' is forbidden in scheduler job authoring; use credentialRequirementRef / externalPortRef references only."));

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

        string? cron = Str(payload, "cronExpression");
        long? interval = payload.TryGetProperty("scheduleIntervalSeconds", out var iv) && iv.ValueKind == JsonValueKind.Number ? iv.GetInt64() : null;

        if (policyKind == "cron" && string.IsNullOrWhiteSpace(cron))
            return (null, new ValidationError("SCHEDULER_JOB_CRON_REQUIRED", "cronExpression is required when schedulePolicyKind=cron."));
        if (policyKind == "interval_seconds" && (interval is null || interval <= 0))
            return (null, new ValidationError("SCHEDULER_JOB_INTERVAL_REQUIRED", "scheduleIntervalSeconds (>0) is required when schedulePolicyKind=interval_seconds."));

        var manual = Bool(payload, "manualRunAllowed");
        var active = Bool(payload, "active");
        var batch = payload.TryGetProperty("maxBatchSize", out var bs) && bs.ValueKind == JsonValueKind.Number ? bs.GetInt32() : 10;
        var lease = payload.TryGetProperty("leaseSeconds", out var ls) && ls.ValueKind == JsonValueKind.Number ? ls.GetInt32() : 300;
        string? credRef = Str(payload, "credentialRequirementRef");
        string? portRef = Str(payload, "externalPortRef");

        // Input source / lifecycle. Table/column authority must be valid identifiers —
        // this fail-closes raw SQL / payload-derived table authority at authoring time.
        string? inputTable = Str(payload, "inputTableRef");
        if (inputTable is not null && !NpgsqlSchedulerJobManifestRepository.IsValidTableRef(inputTable))
            return (null, new ValidationError("SCHEDULER_JOB_TABLE_AUTHORITY_INVALID", "inputTableRef must be a schema-qualified identifier (no raw SQL)."));
        string? outputTable = Str(payload, "outputTableRef");
        if (outputTable is not null && !NpgsqlSchedulerJobManifestRepository.IsValidTableRef(outputTable))
            return (null, new ValidationError("SCHEDULER_JOB_TABLE_AUTHORITY_INVALID", "outputTableRef must be a schema-qualified identifier (no raw SQL)."));

        var inputIdColumn = Str(payload, "inputIdColumn") ?? "id";
        if (!NpgsqlSchedulerJobManifestRepository.IsValidColumn(inputIdColumn))
            return (null, new ValidationError("SCHEDULER_JOB_COLUMN_AUTHORITY_INVALID", "inputIdColumn must be a bare column identifier (no raw SQL)."));
        var inputStatusColumn = Str(payload, "inputStatusColumn");
        if (inputStatusColumn is not null && !NpgsqlSchedulerJobManifestRepository.IsValidColumn(inputStatusColumn))
            return (null, new ValidationError("SCHEDULER_JOB_COLUMN_AUTHORITY_INVALID", "inputStatusColumn must be a bare column identifier (no raw SQL)."));
        var inputDueColumn = Str(payload, "inputDueColumn");
        if (inputDueColumn is not null && !NpgsqlSchedulerJobManifestRepository.IsValidColumn(inputDueColumn))
            return (null, new ValidationError("SCHEDULER_JOB_COLUMN_AUTHORITY_INVALID", "inputDueColumn must be a bare column identifier (no raw SQL)."));

        // retry_policy / projection_policy: accept JSON objects only.
        string? retryPolicyJson = null;
        if (payload.TryGetProperty("retryPolicy", out var rp))
        {
            if (rp.ValueKind != JsonValueKind.Object)
                return (null, new ValidationError("SCHEDULER_JOB_RETRY_POLICY_INVALID", "retryPolicy must be a JSON object."));
            retryPolicyJson = rp.GetRawText();
        }
        string? projectionPolicyJson = null;
        if (payload.TryGetProperty("projectionPolicy", out var pp))
        {
            if (pp.ValueKind != JsonValueKind.Object)
                return (null, new ValidationError("SCHEDULER_JOB_PROJECTION_POLICY_INVALID", "projectionPolicy must be a JSON object."));
            projectionPolicyJson = pp.GetRawText();
        }

        // Steps[]
        var steps = new List<SchedulerJobStepDraft>();
        if (payload.TryGetProperty("steps", out var stepsEl))
        {
            if (stepsEl.ValueKind != JsonValueKind.Array)
                return (null, new ValidationError("SCHEDULER_JOB_STEPS_INVALID", "steps must be an array."));
            var order = 0;
            foreach (var s in stepsEl.EnumerateArray())
            {
                order++;
                if (s.ValueKind != JsonValueKind.Object)
                    return (null, new ValidationError("SCHEDULER_JOB_STEPS_INVALID", "each step must be an object."));
                var fnKey = Str(s, "abstractFunctionKey");
                if (string.IsNullOrWhiteSpace(fnKey))
                    return (null, new ValidationError("SCHEDULER_JOB_STEP_FUNCTION_KEY_REQUIRED", $"step {order}: abstractFunctionKey is required."));
                var onError = Str(s, "onError") ?? "fail_run";
                if (!ValidOnError.Contains(onError))
                    return (null, new ValidationError("SCHEDULER_JOB_STEP_ON_ERROR_INVALID", $"step {order}: onError must be fail_run|retry|skip_input|mark_input_failed."));
                var stepOrder = s.TryGetProperty("stepOrder", out var so) && so.ValueKind == JsonValueKind.Number ? so.GetInt32() : order;
                var resultKey = Str(s, "resultContextKey");
                var stepAuthority = Str(s, "authorityScope");
                var stepActive = !s.TryGetProperty("active", out var sa) || sa.ValueKind != JsonValueKind.False;

                var inputBinding = s.TryGetProperty("inputBinding", out var ib) && ib.ValueKind == JsonValueKind.Object ? ib.GetRawText() : "{}";
                var resultBinding = s.TryGetProperty("resultBinding", out var rb) && rb.ValueKind == JsonValueKind.Object ? rb.GetRawText() : "{}";

                // Guard output-binding column authority at authoring time.
                if (s.TryGetProperty("resultBinding", out var rb2) && rb2.ValueKind == JsonValueKind.Object)
                {
                    if (rb2.TryGetProperty("column_map", out var cm) && cm.ValueKind == JsonValueKind.Object)
                        foreach (var col in cm.EnumerateObject())
                            if (!NpgsqlSchedulerJobManifestRepository.IsValidColumn(col.Name))
                                return (null, new ValidationError("SCHEDULER_JOB_COLUMN_AUTHORITY_INVALID", $"step {order}: result_binding column '{col.Name}' must be a bare column identifier (no raw SQL)."));
                    if (rb2.TryGetProperty("conflict_columns", out var cc) && cc.ValueKind == JsonValueKind.Array)
                        foreach (var col in cc.EnumerateArray())
                            if (col.ValueKind == JsonValueKind.String && !NpgsqlSchedulerJobManifestRepository.IsValidColumn(col.GetString()))
                                return (null, new ValidationError("SCHEDULER_JOB_COLUMN_AUTHORITY_INVALID", $"step {order}: result_binding conflict column must be a bare column identifier (no raw SQL)."));
                }

                steps.Add(new SchedulerJobStepDraft(stepOrder, fnKey!, onError, resultKey, inputBinding, resultBinding, stepAuthority, stepActive));
            }
        }

        return (new SchedulerJobDraft(
            jobKey!, triggerKind, policyKind, cron, interval, manual, active,
            authorityScope!, batch, lease, credRef, portRef)
        {
            InputTableRef = inputTable,
            InputIdColumn = inputIdColumn,
            InputStatusColumn = inputStatusColumn,
            InputStatusPendingValue = Str(payload, "inputStatusPendingValue"),
            InputStatusProcessingValue = Str(payload, "inputStatusProcessingValue"),
            InputStatusCompletedValue = Str(payload, "inputStatusCompletedValue"),
            InputStatusFailedValue = Str(payload, "inputStatusFailedValue"),
            InputStatusSkippedValue = Str(payload, "inputStatusSkippedValue"),
            InputStatusRetryWaitValue = Str(payload, "inputStatusRetryWaitValue"),
            InputDueColumn = inputDueColumn,
            OutputTableRef = outputTable,
            Timezone = Str(payload, "timezone"),
            RetryPolicyJson = retryPolicyJson,
            ProjectionPolicyJson = projectionPolicyJson,
            Steps = steps,
        }, null);
    }

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    // Recursively scan for any prohibited secret field name (case-insensitive) anywhere in the draft.
    private static bool ContainsSecretKey(JsonElement el, out string secretKey)
    {
        secretKey = string.Empty;
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (ProhibitedSecretFields.Any(s => string.Equals(s, prop.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        secretKey = prop.Name;
                        return true;
                    }
                    if (ContainsSecretKey(prop.Value, out secretKey)) return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    if (ContainsSecretKey(item, out secretKey)) return true;
                return false;
            default:
                return false;
        }
    }
}
