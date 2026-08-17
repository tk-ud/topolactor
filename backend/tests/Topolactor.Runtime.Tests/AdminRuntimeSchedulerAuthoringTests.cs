using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for AdminRuntime layer=scheduler_jobs authoring + projection surface.
/// SSOT: docs/design/scheduler-job-manifest-ssot.yaml (authoring_surface, credential_boundary).
///
/// Verifies:
///   - create / edit / disable happy paths reach the manifest repository authority boundary.
///   - secret material (api_key, access_token, ...) is fail-closed in authoring payload.
///   - read-only projection exposes references/status only — never credential plaintext.
///   - unconfigured repository returns SCHEDULER_JOB_MANIFEST_NOT_CONFIGURED.
/// </summary>
public class AdminRuntimeSchedulerAuthoringTests
{
    private static AdminRuntime CreateRuntime(ISchedulerJobManifestRepository? repo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            schedulerJobManifestRepository: repo);
    }

    private static OperationVector Vector(string action, object payload) =>
        new("admin", "scheduler_jobs", action, null, "admin", JsonSerializer.SerializeToElement(payload), null);

    private sealed class StubSchedulerRepo : ISchedulerJobManifestRepository
    {
        public List<SchedulerJobDraft> Created { get; } = new();
        public List<(Guid Id, SchedulerJobDraft Draft)> Updated { get; } = new();
        public List<(Guid Id, bool Active)> ActiveSet { get; } = new();
        public IReadOnlyList<SchedulerJobRecord> ProjectionJobs { get; init; } = Array.Empty<SchedulerJobRecord>();

        public Task<Guid> CreateJobAsync(SchedulerJobDraft draft, CancellationToken ct = default)
        { Created.Add(draft); return Task.FromResult(Guid.NewGuid()); }
        public Task<bool> UpdateJobAsync(Guid id, SchedulerJobDraft draft, CancellationToken ct = default)
        { Updated.Add((id, draft)); return Task.FromResult(true); }
        public Task<bool> SetJobActiveAsync(Guid id, bool active, CancellationToken ct = default)
        { ActiveSet.Add((id, active)); return Task.FromResult(true); }
        /// <summary>Recorded (search, triggerKind, schedulePolicyKind, active) arguments of every
        /// LoadSettingsProjectionAsync call — the scheduler-settings surface's declared search/filter
        /// axes are asserted against these in AdminRuntimeSchedulerSettingsMutationConfirmationTests.</summary>
        public List<(string? Search, string? TriggerKind, string? SchedulePolicyKind, bool? Active)> ProjectionQueries { get; } = new();

        public Task<IReadOnlyList<SchedulerJobRecord>> LoadSettingsProjectionAsync(
            string? search = null, string? triggerKind = null, string? schedulePolicyKind = null,
            bool? active = null, CancellationToken ct = default)
        {
            ProjectionQueries.Add((search, triggerKind, schedulePolicyKind, active));
            IEnumerable<SchedulerJobRecord> rows = ProjectionJobs;
            if (!string.IsNullOrWhiteSpace(search))
                rows = rows.Where(j => j.JobKey.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(triggerKind))
                rows = rows.Where(j => j.TriggerKind == triggerKind);
            if (!string.IsNullOrWhiteSpace(schedulePolicyKind))
                rows = rows.Where(j => j.SchedulePolicyKind == schedulePolicyKind);
            if (active is not null)
                rows = rows.Where(j => j.Active == active.Value);
            return Task.FromResult<IReadOnlyList<SchedulerJobRecord>>(rows.ToList());
        }

        public Task<SchedulerJobRecord?> LoadSettingsProjectionByIdAsync(Guid schedulerJobId, CancellationToken ct = default) =>
            Task.FromResult(ProjectionJobs.FirstOrDefault(j => j.SchedulerJobId == schedulerJobId));

        public Task<IReadOnlyList<SchedulerJobRecord>> LoadActiveJobsAsync(CancellationToken ct = default) => Task.FromResult(ProjectionJobs);
        public Task<IReadOnlyList<SchedulerJobStepRecord>> LoadStepsAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SchedulerJobStepRecord>>(Array.Empty<SchedulerJobStepRecord>());
        public Task<SchedulerJobRunRecord> CreateRunAsync(Guid a, string b, string c, string d, string? e, DateTimeOffset f, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateRunStatusAsync(Guid a, string b, string? c, string? d, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SchedulerInputRow>> LeaseDueInputRowsAsync(SchedulerJobRecord job, DateTimeOffset now, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SchedulerInputRow>>(Array.Empty<SchedulerInputRow>());
        public Task UpdateInputRowStatusAsync(SchedulerJobRecord job, string id, string s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpsertAuthorizedOutputAsync(SchedulerJobRecord job, SchedulerStepResultBinding b, IReadOnlyDictionary<string, object?> v, CancellationToken ct = default) => Task.CompletedTask;

        public List<(Guid SchedulerJobId, string? CredentialRequirementRef, string? ExternalPortRef)> CredentialBindingUpdates { get; } = new();
        public List<(Guid SchedulerJobId, string? CredentialRequirementRef, string? ExternalPortRef)> CredentialBindingValidations { get; } = new();
        public SchedulerJobCredentialBindingResult CredentialBindingResult { get; set; } =
            new(SchedulerJobCredentialBindingOutcome.Updated);
        /// <summary>Defaults to CredentialBindingResult's outcome when null (the common case: validate and write agree).</summary>
        public SchedulerJobCredentialBindingResult? CredentialBindingValidationResult { get; set; }
        public Task<SchedulerJobCredentialBindingResult> UpdateCredentialBindingAsync(
            Guid schedulerJobId, string? credentialRequirementRef, string? externalPortRef, CancellationToken ct = default)
        {
            CredentialBindingUpdates.Add((schedulerJobId, credentialRequirementRef, externalPortRef));
            return Task.FromResult(CredentialBindingResult);
        }
        public Task<SchedulerJobCredentialBindingResult> ValidateCredentialBindingAsync(
            Guid schedulerJobId, string? credentialRequirementRef, string? externalPortRef, CancellationToken ct = default)
        {
            CredentialBindingValidations.Add((schedulerJobId, credentialRequirementRef, externalPortRef));
            return Task.FromResult(CredentialBindingValidationResult ?? CredentialBindingResult);
        }
    }

    [Fact]
    public async Task Create_ValidCronJob_ReachesRepository()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var (data, error) = await runtime.ExecuteDataAsync(Vector("create", new
        {
            jobKey = "weather_24h",
            triggerKind = "cron",
            schedulePolicyKind = "cron",
            cronExpression = "0 * * * *",
            authorityScope = "weather_job",
            active = true,
        }), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Single(repo.Created);
        Assert.Equal("weather_24h", repo.Created[0].JobKey);
    }

    [Fact]
    public async Task Create_SecretFieldInPayload_FailClosed()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var (data, error) = await runtime.ExecuteDataAsync(Vector("create", new
        {
            jobKey = "weather_24h",
            schedulePolicyKind = "manual_only",
            authorityScope = "weather_job",
            api_key = "sk-secret-123",
        }), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SCHEDULER_JOB_SECRET_FIELD_FORBIDDEN", error!.Code);
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task Create_FullManifest_SavesHeaderInputSourceOutputAndSteps()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var (data, error) = await runtime.ExecuteDataAsync(Vector("create", new
        {
            jobKey = "weather_24h",
            triggerKind = "cron",
            schedulePolicyKind = "interval_seconds",
            scheduleIntervalSeconds = 3600,
            authorityScope = "weather_job",
            active = true,
            inputTableRef = "weather.region_inputs",
            inputIdColumn = "id",
            inputStatusColumn = "status",
            inputDueColumn = "due_at",
            inputStatusPendingValue = "pending",
            inputStatusProcessingValue = "processing",
            inputStatusCompletedValue = "completed",
            inputStatusFailedValue = "failed",
            inputStatusSkippedValue = "skipped",
            inputStatusRetryWaitValue = "retry_wait",
            outputTableRef = "weather.observations",
            retryPolicy = new { max_attempts = 3, backoff_seconds = 60 },
            projectionPolicy = new { allowed_result_keys = new[] { "observation" } },
            steps = new object[]
            {
                new
                {
                    stepOrder = 1,
                    abstractFunctionKey = "weather.fetch",
                    onError = "retry",
                    resultContextKey = "observation",
                    inputBinding = new { region = new { source = "input", path = "region" } },
                    resultBinding = new { kind = "output_upsert", result_context_key = "observation", conflict_columns = new[] { "region" }, column_map = new { region = "observation" } },
                    active = true,
                },
            },
        }), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Single(repo.Created);
        var d = repo.Created[0];
        Assert.Equal("weather.region_inputs", d.InputTableRef);
        Assert.Equal("status", d.InputStatusColumn);
        Assert.Equal("retry_wait", d.InputStatusRetryWaitValue);
        Assert.Equal("weather.observations", d.OutputTableRef);
        Assert.NotNull(d.RetryPolicyJson);
        Assert.Single(d.Steps);
        Assert.Equal("weather.fetch", d.Steps[0].AbstractFunctionKey);
        Assert.Equal("retry", d.Steps[0].OnError);
        Assert.Contains("output_upsert", d.Steps[0].ResultBindingJson);
        Assert.Contains("region", d.Steps[0].InputBindingJson);
    }

    [Fact]
    public async Task Edit_FullManifest_UpdatesHeaderAndSteps()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var id = Guid.NewGuid();
        var (_, error) = await runtime.ExecuteDataAsync(Vector("edit", new
        {
            schedulerJobId = id.ToString(),
            jobKey = "weather_24h",
            schedulePolicyKind = "manual_only",
            authorityScope = "weather_job",
            steps = new object[]
            {
                new { abstractFunctionKey = "weather.fetch", onError = "fail_run", resultContextKey = "obs" },
            },
        }), default);

        Assert.Null(error);
        Assert.Single(repo.Updated);
        Assert.Equal(id, repo.Updated[0].Id);
        Assert.Single(repo.Updated[0].Draft.Steps);
    }

    [Fact]
    public async Task Create_PayloadDerivedTableAuthority_FailClosed()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var (_, error) = await runtime.ExecuteDataAsync(Vector("create", new
        {
            jobKey = "j",
            schedulePolicyKind = "manual_only",
            authorityScope = "scope",
            inputTableRef = "users; DROP TABLE x",
        }), default);

        Assert.NotNull(error);
        Assert.Equal("SCHEDULER_JOB_TABLE_AUTHORITY_INVALID", error!.Code);
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task Create_PayloadDerivedColumnAuthorityInResultBinding_FailClosed()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var (_, error) = await runtime.ExecuteDataAsync(Vector("create", new
        {
            jobKey = "j",
            schedulePolicyKind = "manual_only",
            authorityScope = "scope",
            steps = new object[]
            {
                new
                {
                    abstractFunctionKey = "f",
                    onError = "fail_run",
                    resultBinding = new { kind = "output_upsert", column_map = new Dictionary<string, string> { ["bad col;"] = "x" } },
                },
            },
        }), default);

        Assert.NotNull(error);
        Assert.Equal("SCHEDULER_JOB_COLUMN_AUTHORITY_INVALID", error!.Code);
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task Create_InvalidStepOnError_FailClosed()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var (_, error) = await runtime.ExecuteDataAsync(Vector("create", new
        {
            jobKey = "j",
            schedulePolicyKind = "manual_only",
            authorityScope = "scope",
            steps = new object[] { new { abstractFunctionKey = "f", onError = "skip_step" } },
        }), default);

        Assert.NotNull(error);
        Assert.Equal("SCHEDULER_JOB_STEP_ON_ERROR_INVALID", error!.Code);
    }

    [Fact]
    public async Task Create_SecretFieldNestedInStep_FailClosed()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var (_, error) = await runtime.ExecuteDataAsync(Vector("create", new
        {
            jobKey = "j",
            schedulePolicyKind = "manual_only",
            authorityScope = "scope",
            steps = new object[]
            {
                new { abstractFunctionKey = "f", onError = "fail_run", inputBinding = new { api_key = "sk-secret" } },
            },
        }), default);

        Assert.NotNull(error);
        Assert.Equal("SCHEDULER_JOB_SECRET_FIELD_FORBIDDEN", error!.Code);
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task Create_CronPolicyWithoutCronExpression_FailClosed()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var (_, error) = await runtime.ExecuteDataAsync(Vector("create", new
        {
            jobKey = "j",
            schedulePolicyKind = "cron",
            authorityScope = "scope",
        }), default);

        Assert.NotNull(error);
        Assert.Equal("SCHEDULER_JOB_CRON_REQUIRED", error!.Code);
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task Edit_ValidPayload_ReachesRepository()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var id = Guid.NewGuid();
        var (data, error) = await runtime.ExecuteDataAsync(Vector("edit", new
        {
            schedulerJobId = id.ToString(),
            jobKey = "weather_24h",
            schedulePolicyKind = "interval_seconds",
            scheduleIntervalSeconds = 3600,
            authorityScope = "weather_job",
        }), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Single(repo.Updated);
        Assert.Equal(id, repo.Updated[0].Id);
    }

    // scheduler-settings subBundle: disable is no longer a bare one-shot write. It now carries the
    // surface's mutation_confirmation_contract [explicit_confirm, write, diff_log] and the admin
    // role gate (AdminRuntime.SchedulerSettings.cs). The full enable/disable matrix — dryRun preview,
    // not-confirmed fail-close, already-in-target-state fail-close, diff_log envelope, role denial —
    // lives in AdminRuntimeSchedulerSettingsMutationConfirmationTests.cs; this case stays here as the
    // authoring-file-local proof that the confirmed write still reaches SetJobActiveAsync(false).
    [Fact]
    public async Task Disable_ConfirmedPayload_SetsInactive()
    {
        var id = Guid.NewGuid();
        var repo = new StubSchedulerRepo { ProjectionJobs = new[] { SchedulerSettingsTestJobs.Job(id, "weather_24h", active: true) } };
        var runtime = CreateRuntime(repo);
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("disable", new { schedulerJobId = id.ToString(), confirmed = true }) with { AuthenticatedRole = "admin" },
            default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Single(repo.ActiveSet);
        Assert.Equal((id, false), repo.ActiveSet[0]);
    }

    [Fact]
    public async Task Create_Unconfigured_ReturnsNotConfigured()
    {
        var runtime = CreateRuntime(null);
        var (_, error) = await runtime.ExecuteDataAsync(Vector("create", new { jobKey = "j", authorityScope = "s" }), default);
        Assert.NotNull(error);
        Assert.Equal("SCHEDULER_JOB_MANIFEST_NOT_CONFIGURED", error!.Code);
    }

    // scheduler-settings subBundle: list_settings' projection is now bounded by the owning surface's
    // existing_schema_fields_allowed_for_projection / forbidden_projection_fields sets
    // (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces
    // .scheduler), so credentialRequirementRef / externalPortRef / authorityScope are now ABSENT
    // rather than projected as reference keys — they belong to /admin/contents authoring and to the
    // credential-management consumer_reference_binding surface. Secret material was never present
    // and still is not (that assertion is unchanged).
    [Fact]
    public async Task ListSettings_Projection_ForbiddenFieldsAbsentAndNoSecretMaterialLeaked()
    {
        var projPolicy = new Dictionary<string, object?>(StringComparer.Ordinal);
        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "weather_24h", "cron", "cron", "0 * * * *", null, true, true,
            "weather.region_inputs", "status", "pending", "processing", "completed", "failed",
            10, 300, "weather_job", "weather_credential_requirement", "weather_access_port", projPolicy);
        var repo = new StubSchedulerRepo { ProjectionJobs = new[] { job } };
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "scheduler_jobs", "list_settings", null, "admin", null, null), default);

        Assert.Null(error);
        var raw = data!.Value.GetRawText();
        // forbidden_projection_fields: neither the field names nor their values are projected.
        Assert.DoesNotContain("weather_credential_requirement", raw);
        Assert.DoesNotContain("weather_access_port", raw);
        Assert.DoesNotContain("credentialRequirementRef", raw);
        Assert.DoesNotContain("externalPortRef", raw);
        Assert.DoesNotContain("authorityScope", raw);
        // Secret material is never present.
        Assert.DoesNotContain("api_key", raw);
        Assert.DoesNotContain("access_token", raw);
        Assert.DoesNotContain("client_secret", raw);
    }

    // ─── credential-management: configure_scheduler_job_credential_or_port_binding ────────────
    // docs/design/admin-normal-surface-projection-seed-ssot.yaml
    // surface_axes.admin.surfaces.credentials.categories.external_api_credential.consumer_reference_binding

    private static OperationVector CredentialManagementVector(string action, object payload) =>
        new("admin", "credential_management", action, null, "admin", JsonSerializer.SerializeToElement(payload), null);

    [Fact]
    public async Task ConfigureSchedulerJobCredentialOrPortBinding_DryRun_ValidatesButDoesNotWrite()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var id = Guid.NewGuid();

        var (data, error) = await runtime.ExecuteDataAsync(CredentialManagementVector(
            "configure_scheduler_job_credential_or_port_binding",
            new { schedulerJobId = id.ToString(), credentialRequirementRef = "weather_credential_requirement", dryRun = true }),
            default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.True(data.Value.GetProperty("dryRun").GetBoolean());
        Assert.Equal(
            "weather_credential_requirement",
            data.Value.GetProperty("preview").GetProperty("credentialRequirementRef").GetString());
        // real validation ran (ValidateCredentialBindingAsync) but no write (UpdateCredentialBindingAsync).
        var validated = Assert.Single(repo.CredentialBindingValidations);
        Assert.Equal(id, validated.SchedulerJobId);
        Assert.Equal("weather_credential_requirement", validated.CredentialRequirementRef);
        Assert.Empty(repo.CredentialBindingUpdates);
    }

    [Fact]
    public async Task ConfigureSchedulerJobCredentialOrPortBinding_DryRun_InvalidCandidate_FailsClosed_NeverReportsValidTrue()
    {
        var repo = new StubSchedulerRepo
        {
            CredentialBindingValidationResult = new SchedulerJobCredentialBindingResult(
                SchedulerJobCredentialBindingOutcome.CredentialRequirementRefNotFound),
        };
        var runtime = CreateRuntime(repo);
        var id = Guid.NewGuid();

        var (data, error) = await runtime.ExecuteDataAsync(CredentialManagementVector(
            "configure_scheduler_job_credential_or_port_binding",
            new { schedulerJobId = id.ToString(), credentialRequirementRef = "does_not_exist", dryRun = true }),
            default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("CREDENTIAL_REQUIREMENT_REF_NOT_FOUND", error!.Code);
        Assert.Empty(repo.CredentialBindingUpdates);
    }

    [Fact]
    public async Task ConfigureSchedulerJobCredentialOrPortBinding_NotConfirmed_FailsClosed()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var id = Guid.NewGuid();

        var (_, error) = await runtime.ExecuteDataAsync(CredentialManagementVector(
            "configure_scheduler_job_credential_or_port_binding",
            new { schedulerJobId = id.ToString(), externalPortRef = "weather_access_port" }),
            default);

        Assert.NotNull(error);
        Assert.Equal("CREDENTIAL_MANAGEMENT_SCHEDULER_BINDING_WRITE_NOT_CONFIRMED", error!.Code);
        Assert.Empty(repo.CredentialBindingUpdates);
    }

    [Fact]
    public async Task ConfigureSchedulerJobCredentialOrPortBinding_Confirmed_WritesOnlyReferenceColumns()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var id = Guid.NewGuid();

        var (data, error) = await runtime.ExecuteDataAsync(CredentialManagementVector(
            "configure_scheduler_job_credential_or_port_binding",
            new
            {
                schedulerJobId = id.ToString(),
                credentialRequirementRef = "weather_credential_requirement",
                externalPortRef = "weather_access_port",
                confirmed = true,
            }),
            default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        var update = Assert.Single(repo.CredentialBindingUpdates);
        Assert.Equal(id, update.SchedulerJobId);
        Assert.Equal("weather_credential_requirement", update.CredentialRequirementRef);
        Assert.Equal("weather_access_port", update.ExternalPortRef);
    }

    [Fact]
    public async Task ConfigureSchedulerJobCredentialOrPortBinding_SchedulerJobNotFound_FailsClosed()
    {
        var repo = new StubSchedulerRepo
        {
            CredentialBindingResult = new SchedulerJobCredentialBindingResult(SchedulerJobCredentialBindingOutcome.SchedulerJobNotFound),
        };
        var runtime = CreateRuntime(repo);

        var (_, error) = await runtime.ExecuteDataAsync(CredentialManagementVector(
            "configure_scheduler_job_credential_or_port_binding",
            new { schedulerJobId = Guid.NewGuid().ToString(), confirmed = true }),
            default);

        Assert.NotNull(error);
        Assert.Equal("SCHEDULER_JOB_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task ConfigureSchedulerJobCredentialOrPortBinding_UnknownReferenceKey_FailsClosed()
    {
        var repo = new StubSchedulerRepo
        {
            CredentialBindingResult = new SchedulerJobCredentialBindingResult(
                SchedulerJobCredentialBindingOutcome.CredentialRequirementRefNotFound),
        };
        var runtime = CreateRuntime(repo);

        var (_, error) = await runtime.ExecuteDataAsync(CredentialManagementVector(
            "configure_scheduler_job_credential_or_port_binding",
            new { schedulerJobId = Guid.NewGuid().ToString(), credentialRequirementRef = "does_not_exist", confirmed = true }),
            default);

        Assert.NotNull(error);
        Assert.Equal("CREDENTIAL_REQUIREMENT_REF_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task ConfigureSchedulerJobCredentialOrPortBinding_Unconfigured_ReturnsNotConfigured()
    {
        var runtime = CreateRuntime(null);
        var (_, error) = await runtime.ExecuteDataAsync(CredentialManagementVector(
            "configure_scheduler_job_credential_or_port_binding",
            new { schedulerJobId = Guid.NewGuid().ToString(), confirmed = true }),
            default);
        Assert.NotNull(error);
        Assert.Equal("SCHEDULER_JOB_MANIFEST_NOT_CONFIGURED", error!.Code);
    }
}
