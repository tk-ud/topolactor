using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — Scheduler Job Settings surface (list / search / filter / enable / disable).
//
// Entry: AdminRuntime.ExecuteDataAsync layer=scheduler_jobs
//        actions: list_settings | enable | disable  (create/edit below are the /admin/contents
//        generic authoring pipeline's own actions, NOT part of this surface)
//
// SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
//   surface_axes.admin.surfaces.scheduler  (scope_boundary / existing_schema_fields_allowed_for_
//   projection / forbidden_projection_fields / capability_requirements / seed_contract)
// Data authority: docs/design/scheduler-job-manifest-ssot.yaml
//
// Boundary invariants:
//   - list_settings projects ONLY existing_schema_fields_allowed_for_projection. The SSOT's
//     forbidden_projection_fields (credential_requirement_ref / external_port_ref /
//     authority_scope / input_* / output_table_ref / retry_policy / projection_policy) are NOT
//     projected here: they belong to /admin/contents authoring or to the credential-management
//     consumer_reference_binding surface, not to this list/search/filter/toggle-only projection.
//     (Not secret material — but max_batch_size/lease_seconds are likewise outside the allowed
//     set, so they are not projected either.)
//   - search is job_key-only; filter is trigger_kind / schedule_policy_kind / active only.
//   - enable/disable are the ONLY mutations reachable through this surface: no create/edit/
//     step-chain authoring, no credential or external-port binding.
//   - enable/disable follow mutation_confirmation_contract [explicit_confirm, write, diff_log].
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    private readonly ISchedulerJobManifestRepository? _schedulerJobManifestRepository;

    /// <summary>
    /// scheduler_jobs:list_settings. OPTIONAL payload search/filter fields on this SAME existing
    /// read action — no new action and no scheduler-specific runtime lane, the same idiom
    /// enum_dictionary:list_groups established for its own search/filter fields. Absent payload
    /// (every pre-existing call site) is not an error: full list, exactly as before.
    ///
    /// Payload fields are FLAT (payload.search / payload.triggerKind / payload.schedulePolicyKind /
    /// payload.active), matching both list_groups' own flat search/filter fields and the only shape
    /// a seeded payloadFrom descriptor can produce (frontend/runtime/payloadFromResolver.ts maps
    /// flat payload keys to node/literal sources — there is no nested-key form).
    /// </summary>
    private async Task<(JsonElement? data, ValidationError? error)>
        DataListSchedulerJobsSettingsAsync(OperationVector vector, CancellationToken ct)
    {
        if (_schedulerJobManifestRepository is null)
            return (null, new ValidationError(
                "SCHEDULER_JOB_MANIFEST_NOT_CONFIGURED",
                "ISchedulerJobManifestRepository is not registered"));

        string? search = null;
        string? triggerKindFilter = null;
        string? schedulePolicyKindFilter = null;
        bool? activeFilter = null;

        if (vector.Payload.HasValue && vector.Payload.Value.ValueKind != JsonValueKind.Null)
        {
            // fail-close: a bare string/number/array/boolean payload is a malformed request, never
            // silently treated as "no search"/"no filter" (list_groups' own round-37 fail-close).
            if (vector.Payload.Value.ValueKind != JsonValueKind.Object)
                return (null, new ValidationError(
                    "SCHEDULER_LIST_SETTINGS_PAYLOAD_NOT_OBJECT",
                    "payload must be a JSON object when present."));

            var payload = vector.Payload.Value;

            var (searchValue, searchError) = OptionalFilterString(payload, "search");
            if (searchError is not null) return (null, searchError);
            search = searchValue;

            var (triggerKindValue, triggerKindTypeError) = OptionalFilterString(payload, "triggerKind");
            if (triggerKindTypeError is not null) return (null, triggerKindTypeError);
            triggerKindFilter = triggerKindValue;

            var (schedulePolicyKindValue, schedulePolicyKindTypeError) = OptionalFilterString(payload, "schedulePolicyKind");
            if (schedulePolicyKindTypeError is not null) return (null, schedulePolicyKindTypeError);
            schedulePolicyKindFilter = schedulePolicyKindValue;

            if (triggerKindFilter is not null && !ValidTriggerKinds.Contains(triggerKindFilter))
                return (null, new ValidationError("SCHEDULER_JOB_TRIGGER_KIND_INVALID",
                    "payload.triggerKind must be cron|hook|client when present."));
            if (schedulePolicyKindFilter is not null && !ValidSchedulePolicyKinds.Contains(schedulePolicyKindFilter))
                return (null, new ValidationError("SCHEDULER_JOB_SCHEDULE_POLICY_KIND_INVALID",
                    "payload.schedulePolicyKind must be cron|interval_seconds|manual_only when present."));

            var (activeValue, activeError) = OptionalFilterBool(payload, "active");
            if (activeError is not null) return (null, activeError);
            activeFilter = activeValue;
        }

        var jobs = await _schedulerJobManifestRepository.LoadSettingsProjectionAsync(
            search, triggerKindFilter, schedulePolicyKindFilter, activeFilter, ct);

        // existing_schema_fields_allowed_for_projection, and nothing else.
        var projection = jobs.Select(static j => new
        {
            schedulerJobId          = j.SchedulerJobId,
            jobKey                  = j.JobKey,
            triggerKind             = j.TriggerKind,
            schedulePolicyKind      = j.SchedulePolicyKind,
            cronExpression          = j.CronExpression,
            scheduleIntervalSeconds = j.ScheduleIntervalSeconds,
            timezone                = j.Timezone,
            manualRunAllowed        = j.ManualRunAllowed,
            active                  = j.Active,
            updatedAt               = j.UpdatedAt,
        }).ToList();

        // Filter select option domains for the three declared filter axes. These are the fields'
        // own fixed vocabularies (ValidTriggerKinds / ValidSchedulePolicyKinds / active boolean) —
        // never derived from the (possibly search/filter-narrowed) result rows above, so a filter
        // choice can never shrink the option list that produced it (the same options-self-shrinking
        // gap list_groups' groupOptions closes by reading the full unfiltered roster).
        var triggerKindOptions = ValidTriggerKinds.Select(static k => new { value = k, label = k }).ToList();
        var schedulePolicyKindOptions = ValidSchedulePolicyKinds.Select(static k => new { value = k, label = k }).ToList();
        var activeOptions = new[]
        {
            new { value = "true", label = "active" },
            new { value = "false", label = "inactive" },
        };

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            schedulerJobs = projection,
            triggerKindOptions,
            schedulePolicyKindOptions,
            activeOptions,
        }), null);
    }

    /// <summary>
    /// An optional string search/filter field. An absent, null, or empty/whitespace value means
    /// "no filter on this axis" (an empty select/search input is the seeded surface's own real
    /// "all" state, not a request to match the empty string). A PRESENT non-string value (number/
    /// bool/array/object) fails closed with an explicit error — it must never silently collapse to
    /// "no filter on this axis" the way an absent/null/empty value legitimately does, or a caller
    /// whose filter request was malformed would silently receive the full unfiltered list instead
    /// of a rejection. Mirrors OptionalFilterBool's own (value, error) fail-close shape below and
    /// enum_dictionary:list_groups' round-37 "malformed payload field type is a genuine client
    /// defect, never treated as no search/filter" precedent (AdminRuntime.cs
    /// DataEnumDictionaryListGroupsAsync).
    /// </summary>
    private static (string? value, ValidationError? error) OptionalFilterString(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var el)) return (null, null);
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return (null, null);
        if (el.ValueKind != JsonValueKind.String)
            return (null, new ValidationError("SCHEDULER_LIST_SETTINGS_FIELD_NOT_STRING",
                $"payload.{name} must be a string when present."));
        var value = el.GetString();
        return (string.IsNullOrWhiteSpace(value) ? null : value.Trim(), null);
    }

    /// <summary>
    /// payload.active as an optional tri-state filter. Accepts a real JSON boolean AND the
    /// "true"/"false" strings a seeded &lt;select&gt; node necessarily produces (its tracked node
    /// value is always a string); empty string / absent / null means "no active filter". Any other
    /// value fails closed rather than being coerced.
    /// </summary>
    private static (bool? value, ValidationError? error) OptionalFilterBool(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var el)) return (null, null);
        switch (el.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined:
                return (null, null);
            case JsonValueKind.True:
                return (true, null);
            case JsonValueKind.False:
                return (false, null);
            case JsonValueKind.String:
                var raw = el.GetString();
                if (string.IsNullOrWhiteSpace(raw)) return (null, null);
                if (bool.TryParse(raw.Trim(), out var parsed)) return (parsed, null);
                return (null, new ValidationError("SCHEDULER_LIST_SETTINGS_ACTIVE_FILTER_INVALID",
                    $"payload.{name} must be true/false when present."));
            default:
                return (null, new ValidationError("SCHEDULER_LIST_SETTINGS_ACTIVE_FILTER_INVALID",
                    $"payload.{name} must be a boolean or the string \"true\"/\"false\" when present."));
        }
    }

    // ── admin.contents authoring surface ──────────────────────────────────────
    // create / edit scheduler job manifests as data-defined records.
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

    // ── scheduler-settings surface: explicit enable / disable ─────────────────
    // scheduler_jobs:enable is the symmetric counterpart the SSOT's new_operation_note declares
    // required for this surface (set_scheduler_job_active_true mirroring disable's
    // set_scheduler_job_active_false) — the SAME SetJobActiveAsync authority AdminRuntime already
    // owned for disable, never a new authority.
    //
    // Both follow mutation_confirmation_contract [explicit_confirm, write, diff_log], mirroring
    // AdminRuntime.TeamDashboard.cs DataTeamDashboardUpdateAsync's single-row structure exactly:
    //   payload.dryRun=true               -> validate only; no write, no diff_log
    //   neither dryRun nor confirmed       -> fail closed (…_NOT_CONFIRMED); no write
    //   payload.confirmed=true             -> write via SetJobActiveAsync, then diff_log
    // Every validation gate runs identically on both paths — the write path re-runs them itself and
    // never trusts a prior dryRun as proof. Cancel is the frontend simply never sending
    // confirmed=true, a path that never reaches the repository at all.

    private Task<(JsonElement? data, ValidationError? error)>
        DataEnableSchedulerJobAsync(OperationVector vector, CancellationToken ct) =>
        SetSchedulerJobActiveWithConfirmationAsync(vector, targetActive: true, ct);

    private Task<(JsonElement? data, ValidationError? error)>
        DataDisableSchedulerJobAsync(OperationVector vector, CancellationToken ct) =>
        SetSchedulerJobActiveWithConfirmationAsync(vector, targetActive: false, ct);

    private async Task<(JsonElement? data, ValidationError? error)>
        SetSchedulerJobActiveWithConfirmationAsync(OperationVector vector, bool targetActive, CancellationToken ct)
    {
        var operation = targetActive ? "enable" : "disable";

        // Role gate: an explicit, non-spoofable in-method check on the JWT-verified
        // DispatchAuthContext stamp (never a client-supplied context field), the same idiom
        // AdminRuntime.TeamDashboard.cs / AdminRuntime.TeamMarkdown.cs already established for
        // their own mutations. The manifest-level capability_requirement gate
        // (ManifestDispatcher.ValidateCapabilityRequirement, inferred from
        // runtime_mapping.runtime_destination=admin_runtime) still runs first on every dispatch
        // path; this is defense in depth for the two mutations this surface owns, not a substitute.
        if (!string.Equals(vector.AuthenticatedRole, "admin", StringComparison.Ordinal))
            return (null, new ValidationError("AUTH_CAPABILITY_DENIED",
                $"scheduler_jobs:{operation} requires admin role."));

        if (_schedulerJobManifestRepository is null)
            return (null, SchedulerRepoNotConfigured());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object ||
            !vector.Payload.Value.TryGetProperty("schedulerJobId", out var idEl) ||
            idEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(idEl.GetString(), out var schedulerJobId))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.schedulerJobId is required."));

        // Current state is read for BOTH paths: the dryRun preview reports/validates the transition
        // and the confirmed write needs the real `before` value for the diff_log envelope.
        var current = await _schedulerJobManifestRepository.LoadSettingsProjectionByIdAsync(schedulerJobId, ct);
        if (current is null)
            return (null, new ValidationError("SCHEDULER_JOB_NOT_FOUND", "Scheduler job not found."));

        if (current.Active == targetActive)
            return (null, new ValidationError(
                targetActive ? "SCHEDULER_JOB_ALREADY_ACTIVE" : "SCHEDULER_JOB_ALREADY_INACTIVE",
                targetActive
                    ? $"Scheduler job '{current.JobKey}' is already active; nothing to enable."
                    : $"Scheduler job '{current.JobKey}' is already inactive; nothing to disable."));

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
                    operation,
                    jobKey = current.JobKey,
                    activeBefore = current.Active,
                    activeAfter = targetActive,
                },
            }), null);
        }

        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError(
                targetActive ? "SCHEDULER_JOB_ENABLE_NOT_CONFIRMED" : "SCHEDULER_JOB_DISABLE_NOT_CONFIRMED",
                $"scheduler_jobs:{operation} requires payload.confirmed=true after an explicit user confirmation step."));

        var written = await _schedulerJobManifestRepository.SetJobActiveAsync(schedulerJobId, targetActive, ct);
        if (!written)
            return (null, new ValidationError("SCHEDULER_JOB_NOT_FOUND", "Scheduler job not found."));

        await AdminMasterRosterAudit.AppendAsync(
            _sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "topology.scheduler_jobs", schedulerJobId.ToString(), "update",
            new { active = current.Active },
            new { active = targetActive },
            [new AuditChangedField("active", current.Active, targetActive)], ct);

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            schedulerJobId,
            jobKey = current.JobKey,
            active = targetActive,
        }), null);
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
