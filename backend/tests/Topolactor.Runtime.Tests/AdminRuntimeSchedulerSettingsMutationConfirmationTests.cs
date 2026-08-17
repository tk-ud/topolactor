using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// scheduler-settings subBundle (admin-surface-topology-seed-conversion).
/// SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
///   surface_axes.admin.surfaces.scheduler
///     - scope_boundary            (list/search/filter/enable/disable ONLY)
///     - capability_requirements   (search: [job_key]; filter: [trigger_kind, schedule_policy_kind, active])
///     - existing_schema_fields_allowed_for_projection / forbidden_projection_fields
///     - seed_contract.mutation_confirmation_contract [explicit_confirm, write, diff_log]
///     - new_operation_note        (scheduler_jobs:enable mirrors disable)
/// Data authority: docs/design/scheduler-job-manifest-ssot.yaml
///
/// Unit level (no DB): AdminRuntime + a recording ISchedulerJobManifestRepository double and a
/// recording SqlAttentionLogsRepository double, so the diff_log envelope the confirmed write builds
/// is actually captured rather than assumed. Live-DB proof of the same surface's manifest/dispatch
/// resolution is backend/tests/Topolactor.Integration.Tests/
/// SchedulerSettingsHubRelationUiProjectionLiveDbTests.cs.
/// </summary>
public class AdminRuntimeSchedulerSettingsMutationConfirmationTests
{
    // ── doubles ───────────────────────────────────────────────────────────────

    private sealed class RecordingSqlAttentionLogsRepository()
        : SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "test-double")
    {
        public readonly List<LogsDiffAppendRequest> Requests = [];

        public override Task AppendLogsDiffAsync(LogsDiffAppendRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSchedulerRepo : ISchedulerJobManifestRepository
    {
        public List<SchedulerJobRecord> Jobs { get; init; } = new();
        public List<(Guid Id, bool Active)> ActiveSet { get; } = new();
        public List<(string? Search, string? TriggerKind, string? SchedulePolicyKind, bool? Active)> Queries { get; } = new();
        /// <summary>When set, LoadSettingsProjectionAsync throws it — the DB/runtime-failure path.</summary>
        public Exception? ThrowOnProjection { get; set; }

        public Task<IReadOnlyList<SchedulerJobRecord>> LoadSettingsProjectionAsync(
            string? search = null, string? triggerKind = null, string? schedulePolicyKind = null,
            bool? active = null, CancellationToken ct = default)
        {
            Queries.Add((search, triggerKind, schedulePolicyKind, active));
            if (ThrowOnProjection is not null) throw ThrowOnProjection;
            IEnumerable<SchedulerJobRecord> rows = Jobs;
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
            Task.FromResult(Jobs.FirstOrDefault(j => j.SchedulerJobId == schedulerJobId));

        public Task<bool> SetJobActiveAsync(Guid id, bool active, CancellationToken ct = default)
        {
            ActiveSet.Add((id, active));
            var idx = Jobs.FindIndex(j => j.SchedulerJobId == id);
            if (idx < 0) return Task.FromResult(false);
            Jobs[idx] = Jobs[idx] with { Active = active };
            return Task.FromResult(true);
        }

        // Out-of-scope members: this surface never reaches them (create/edit/step chain are the
        // /admin/contents pipeline's; credential/port binding is credential-management's).
        public Task<Guid> CreateJobAsync(SchedulerJobDraft draft, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateJobAsync(Guid id, SchedulerJobDraft draft, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SchedulerJobRecord>> LoadActiveJobsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchedulerJobRecord>>(Jobs.Where(j => j.Active).ToList());
        public Task<IReadOnlyList<SchedulerJobStepRecord>> LoadStepsAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchedulerJobStepRecord>>(Array.Empty<SchedulerJobStepRecord>());
        public Task<SchedulerJobRunRecord> CreateRunAsync(Guid a, string b, string c, string d, string? e, DateTimeOffset f, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateRunStatusAsync(Guid a, string b, string? c, string? d, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SchedulerInputRow>> LeaseDueInputRowsAsync(SchedulerJobRecord job, DateTimeOffset now, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchedulerInputRow>>(Array.Empty<SchedulerInputRow>());
        public Task UpdateInputRowStatusAsync(SchedulerJobRecord job, string id, string s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpsertAuthorizedOutputAsync(SchedulerJobRecord job, SchedulerStepResultBinding b, IReadOnlyDictionary<string, object?> v, CancellationToken ct = default) => Task.CompletedTask;
        public Task<SchedulerJobCredentialBindingResult> UpdateCredentialBindingAsync(Guid id, string? c, string? p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SchedulerJobCredentialBindingResult> ValidateCredentialBindingAsync(Guid id, string? c, string? p, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static (AdminRuntime runtime, RecordingSchedulerRepo repo, RecordingSqlAttentionLogsRepository logs)
        CreateRuntime(params SchedulerJobRecord[] jobs)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var repo = new RecordingSchedulerRepo { Jobs = jobs.ToList() };
        var logs = new RecordingSqlAttentionLogsRepository();
        var runtime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            schedulerJobManifestRepository: repo,
            sqlAttentionLogsRepository: logs);
        return (runtime, repo, logs);
    }

    private static OperationVector Mutation(string action, object payload, string? authenticatedRole = "admin") =>
        new("admin", "scheduler_jobs", action, null, "admin",
            JsonSerializer.SerializeToElement(payload), null, AuthenticatedRole: authenticatedRole);

    private static OperationVector ListSettings(object? payload = null) =>
        new("admin", "scheduler_jobs", "list_settings", null, "admin",
            payload is null ? null : JsonSerializer.SerializeToElement(payload), null, AuthenticatedRole: "admin");

    // ── enable: mutation_confirmation_contract ────────────────────────────────

    [Fact]
    public async Task Enable_DryRun_PreviewsWithoutWritingOrLogging()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: false));

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation("enable", new { schedulerJobId = id.ToString(), dryRun = true }), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.True(data.Value.GetProperty("dryRun").GetBoolean());
        Assert.True(data.Value.GetProperty("valid").GetBoolean());
        var preview = data.Value.GetProperty("preview");
        Assert.Equal("enable", preview.GetProperty("operation").GetString());
        Assert.False(preview.GetProperty("activeBefore").GetBoolean());
        Assert.True(preview.GetProperty("activeAfter").GetBoolean());
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    [Fact]
    public async Task Enable_WithoutConfirmed_FailsCloseAndNeverWrites()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: false));

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation("enable", new { schedulerJobId = id.ToString() }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_ENABLE_NOT_CONFIRMED", error!.Code);
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    [Fact]
    public async Task Enable_Confirmed_WritesAndAppendsDiffLog()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: false));

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation("enable", new { schedulerJobId = id.ToString(), confirmed = true }), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.True(data.Value.GetProperty("active").GetBoolean());
        Assert.Equal("weather_24h", data.Value.GetProperty("jobKey").GetString());
        Assert.Equal((id, true), Assert.Single(repo.ActiveSet));

        var diff = Assert.Single(logs.Requests);
        Assert.Equal("topology.scheduler_jobs", diff.PhysicalTableName);
        Assert.Equal(id.ToString(), diff.RecordId);
        Assert.Equal("update", diff.OperationKind);
        Assert.Contains("\"active\":false", diff.BeforeStateOrDiffJson);
        Assert.Contains("\"active\":true", diff.AfterStateOrDiffJson);
        Assert.Contains("\"name\":\"active\"", diff.ChangedFieldsJson);
    }

    [Fact]
    public async Task Enable_AlreadyActive_FailsCloseWithoutWriting()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: true));

        var (dryData, dryError) = await runtime.ExecuteDataAsync(
            Mutation("enable", new { schedulerJobId = id.ToString(), dryRun = true }), default);
        Assert.Null(dryData);
        Assert.Equal("SCHEDULER_JOB_ALREADY_ACTIVE", dryError!.Code);

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation("enable", new { schedulerJobId = id.ToString(), confirmed = true }), default);
        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_ALREADY_ACTIVE", error!.Code);
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    // ── disable: same contract, mirrored ─────────────────────────────────────

    [Fact]
    public async Task Disable_DryRun_PreviewsWithoutWritingOrLogging()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: true));

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation("disable", new { schedulerJobId = id.ToString(), dryRun = true }), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("dryRun").GetBoolean());
        var preview = data.Value.GetProperty("preview");
        Assert.Equal("disable", preview.GetProperty("operation").GetString());
        Assert.True(preview.GetProperty("activeBefore").GetBoolean());
        Assert.False(preview.GetProperty("activeAfter").GetBoolean());
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    [Fact]
    public async Task Disable_WithoutConfirmed_FailsCloseAndNeverWrites()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: true));

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation("disable", new { schedulerJobId = id.ToString() }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_DISABLE_NOT_CONFIRMED", error!.Code);
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    [Fact]
    public async Task Disable_Confirmed_WritesAndAppendsDiffLog()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: true));

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation("disable", new { schedulerJobId = id.ToString(), confirmed = true }), default);

        Assert.Null(error);
        Assert.False(data!.Value.GetProperty("active").GetBoolean());
        Assert.Equal((id, false), Assert.Single(repo.ActiveSet));
        var diff = Assert.Single(logs.Requests);
        Assert.Equal("topology.scheduler_jobs", diff.PhysicalTableName);
        Assert.Contains("\"active\":true", diff.BeforeStateOrDiffJson);
        Assert.Contains("\"active\":false", diff.AfterStateOrDiffJson);
    }

    [Fact]
    public async Task Disable_AlreadyInactive_FailsCloseWithoutWriting()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: false));

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation("disable", new { schedulerJobId = id.ToString(), confirmed = true }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_ALREADY_INACTIVE", error!.Code);
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    [Fact]
    public async Task EnableAndDisable_AreSymmetric_OverTheSameJob()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, _) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: false));

        var (enabled, enableError) = await runtime.ExecuteDataAsync(
            Mutation("enable", new { schedulerJobId = id.ToString(), confirmed = true }), default);
        Assert.Null(enableError);
        Assert.True(enabled!.Value.GetProperty("active").GetBoolean());

        var (disabled, disableError) = await runtime.ExecuteDataAsync(
            Mutation("disable", new { schedulerJobId = id.ToString(), confirmed = true }), default);
        Assert.Null(disableError);
        Assert.False(disabled!.Value.GetProperty("active").GetBoolean());

        Assert.Equal(new[] { (id, true), (id, false) }, repo.ActiveSet);
    }

    // ── negative / fail-close matrix on the two mutations ────────────────────

    [Theory]
    [InlineData("enable")]
    [InlineData("disable")]
    public async Task Mutation_WithoutAdminAuthenticatedRole_FailsCloseWithAuthCapabilityDenied(string action)
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: action == "disable"));

        // Client-supplied UserRole is "admin" in Mutation(); only the JWT-verified AuthenticatedRole
        // stamp counts, so a non-admin (or absent) authenticated role must fail closed.
        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation(action, new { schedulerJobId = id.ToString(), confirmed = true }, authenticatedRole: "normal"), default);

        Assert.Null(data);
        Assert.Equal("AUTH_CAPABILITY_DENIED", error!.Code);
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);

        var (nullRoleData, nullRoleError) = await runtime.ExecuteDataAsync(
            Mutation(action, new { schedulerJobId = id.ToString(), confirmed = true }, authenticatedRole: null), default);
        Assert.Null(nullRoleData);
        Assert.Equal("AUTH_CAPABILITY_DENIED", nullRoleError!.Code);
        Assert.Empty(repo.ActiveSet);
    }

    [Theory]
    [InlineData("enable")]
    [InlineData("disable")]
    public async Task Mutation_UnknownSchedulerJobId_FailsCloseWithNotFound(string action)
    {
        var (runtime, repo, _) = CreateRuntime(SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "weather_24h", active: true));

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation(action, new { schedulerJobId = Guid.NewGuid().ToString(), confirmed = true }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_NOT_FOUND", error!.Code);
        Assert.Empty(repo.ActiveSet);
    }

    [Theory]
    [InlineData("enable")]
    [InlineData("disable")]
    public async Task Mutation_MissingOrMalformedIdentity_FailsClose(string action)
    {
        var (runtime, repo, _) = CreateRuntime();

        var (noId, noIdError) = await runtime.ExecuteDataAsync(Mutation(action, new { confirmed = true }), default);
        Assert.Null(noId);
        Assert.Equal("MALFORMED_PAYLOAD", noIdError!.Code);

        var (badId, badIdError) = await runtime.ExecuteDataAsync(
            Mutation(action, new { schedulerJobId = "not-a-guid", confirmed = true }), default);
        Assert.Null(badId);
        Assert.Equal("MALFORMED_PAYLOAD", badIdError!.Code);

        Assert.Empty(repo.ActiveSet);
    }

    [Fact]
    public async Task Enable_RepositoryNotConfigured_FailsCloseExplicitly()
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var runtime = new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Mutation("enable", new { schedulerJobId = Guid.NewGuid().ToString(), confirmed = true }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_MANIFEST_NOT_CONFIGURED", error!.Code);
    }

    // ── list / search / filter ───────────────────────────────────────────────

    [Fact]
    public async Task ListSettings_NoPayload_ReturnsFullRosterAndFilterOptionDomains()
    {
        var (runtime, repo, _) = CreateRuntime(
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "alpha_sweep", active: true),
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "beta_sweep", active: false));

        var (data, error) = await runtime.ExecuteDataAsync(ListSettings(), default);

        Assert.Null(error);
        Assert.Equal(2, data!.Value.GetProperty("schedulerJobs").GetArrayLength());
        Assert.Equal((null, null, null, (bool?)null), Assert.Single(repo.Queries));
        // Option domains are the fields' own fixed vocabularies, never derived from the result rows.
        Assert.Equal(3, data.Value.GetProperty("triggerKindOptions").GetArrayLength());
        Assert.Equal(3, data.Value.GetProperty("schedulePolicyKindOptions").GetArrayLength());
        Assert.Equal(2, data.Value.GetProperty("activeOptions").GetArrayLength());
    }

    [Fact]
    public async Task ListSettings_Search_MatchesJobKeySubstringOnly()
    {
        var (runtime, repo, _) = CreateRuntime(
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "weather_24h", active: true),
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "log_retention_sweep", active: true));

        var (data, error) = await runtime.ExecuteDataAsync(ListSettings(new { search = "weather" }), default);

        Assert.Null(error);
        var rows = data!.Value.GetProperty("schedulerJobs");
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("weather_24h", rows[0].GetProperty("jobKey").GetString());
        Assert.Equal("weather", Assert.Single(repo.Queries).Search);
    }

    [Fact]
    public async Task ListSettings_Filter_TriggerKind()
    {
        var (runtime, repo, _) = CreateRuntime(
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "cron_job", active: true, triggerKind: "cron"),
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "hook_job", active: true, triggerKind: "hook"));

        var (data, error) = await runtime.ExecuteDataAsync(ListSettings(new { triggerKind = "hook" }), default);

        Assert.Null(error);
        var rows = data!.Value.GetProperty("schedulerJobs");
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("hook_job", rows[0].GetProperty("jobKey").GetString());
        Assert.Equal("hook", Assert.Single(repo.Queries).TriggerKind);
    }

    [Fact]
    public async Task ListSettings_Filter_SchedulePolicyKind()
    {
        var (runtime, repo, _) = CreateRuntime(
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "cron_job", active: true, schedulePolicyKind: "cron"),
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "manual_job", active: true, schedulePolicyKind: "manual_only"));

        var (data, error) = await runtime.ExecuteDataAsync(
            ListSettings(new { schedulePolicyKind = "manual_only" }), default);

        Assert.Null(error);
        var rows = data!.Value.GetProperty("schedulerJobs");
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("manual_job", rows[0].GetProperty("jobKey").GetString());
        Assert.Equal("manual_only", Assert.Single(repo.Queries).SchedulePolicyKind);
    }

    [Fact]
    public async Task ListSettings_Filter_Active_AcceptsBooleanAndSelectNodeString()
    {
        var (runtime, repo, _) = CreateRuntime(
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "active_job", active: true),
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "inactive_job", active: false));

        var (boolData, boolError) = await runtime.ExecuteDataAsync(ListSettings(new { active = true }), default);
        Assert.Null(boolError);
        Assert.Equal(1, boolData!.Value.GetProperty("schedulerJobs").GetArrayLength());
        Assert.Equal("active_job", boolData.Value.GetProperty("schedulerJobs")[0].GetProperty("jobKey").GetString());

        // A seeded <select> node's tracked value is always a string.
        var (strData, strError) = await runtime.ExecuteDataAsync(ListSettings(new { active = "false" }), default);
        Assert.Null(strError);
        Assert.Equal(1, strData!.Value.GetProperty("schedulerJobs").GetArrayLength());
        Assert.Equal("inactive_job", strData.Value.GetProperty("schedulerJobs")[0].GetProperty("jobKey").GetString());

        Assert.Equal(new bool?[] { true, false }, repo.Queries.Select(q => q.Active));
    }

    [Fact]
    public async Task ListSettings_EmptySearchAndFilterValues_MeanNoFilter_NotEmptyStringMatch()
    {
        var (runtime, repo, _) = CreateRuntime(
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "alpha_sweep", active: true),
            SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "beta_sweep", active: false));

        var (data, error) = await runtime.ExecuteDataAsync(
            ListSettings(new { search = "", triggerKind = "", schedulePolicyKind = "", active = "" }), default);

        Assert.Null(error);
        Assert.Equal(2, data!.Value.GetProperty("schedulerJobs").GetArrayLength());
        Assert.Equal((null, null, null, (bool?)null), Assert.Single(repo.Queries));
    }

    [Fact]
    public async Task ListSettings_InvalidFilterValues_FailClose()
    {
        var (runtime, repo, _) = CreateRuntime(SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "alpha", active: true));

        var (_, triggerError) = await runtime.ExecuteDataAsync(
            ListSettings(new { triggerKind = "not_a_trigger_kind" }), default);
        Assert.Equal("SCHEDULER_JOB_TRIGGER_KIND_INVALID", triggerError!.Code);

        var (_, policyError) = await runtime.ExecuteDataAsync(
            ListSettings(new { schedulePolicyKind = "not_a_policy" }), default);
        Assert.Equal("SCHEDULER_JOB_SCHEDULE_POLICY_KIND_INVALID", policyError!.Code);

        var (_, activeError) = await runtime.ExecuteDataAsync(
            ListSettings(new { active = "sometimes" }), default);
        Assert.Equal("SCHEDULER_LIST_SETTINGS_ACTIVE_FILTER_INVALID", activeError!.Code);

        // No read was even attempted for any of the three rejected requests.
        Assert.Empty(repo.Queries);
    }

    [Fact]
    public async Task ListSettings_NonObjectPayload_FailsClose()
    {
        var (runtime, repo, _) = CreateRuntime(SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "alpha", active: true));

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "scheduler_jobs", "list_settings", null, "admin",
                JsonSerializer.SerializeToElement("weather"), null, AuthenticatedRole: "admin"), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_LIST_SETTINGS_PAYLOAD_NOT_OBJECT", error!.Code);
        Assert.Empty(repo.Queries);
    }

    [Fact]
    public async Task ListSettings_ProjectsExactlyTheAllowedFieldSet()
    {
        var id = Guid.NewGuid();
        var (runtime, _, _) = CreateRuntime(SchedulerSettingsTestJobs.Job(id, "weather_24h", active: true));

        var (data, error) = await runtime.ExecuteDataAsync(ListSettings(), default);

        Assert.Null(error);
        var row = data!.Value.GetProperty("schedulerJobs")[0];
        var projected = row.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var allowed = new[]
        {
            "active", "cronExpression", "jobKey", "manualRunAllowed", "schedulePolicyKind",
            "scheduleIntervalSeconds", "schedulerJobId", "timezone", "triggerKind", "updatedAt",
        }.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(allowed, projected);
    }

    [Fact]
    public async Task ListSettings_ForbiddenProjectionFieldsAreAbsent()
    {
        var (runtime, _, _) = CreateRuntime(SchedulerSettingsTestJobs.Job(
            Guid.NewGuid(), "weather_24h", active: true,
            credentialRequirementRef: "weather_credential_requirement",
            externalPortRef: "weather_access_port"));

        var (data, error) = await runtime.ExecuteDataAsync(ListSettings(), default);

        Assert.Null(error);
        var raw = data!.Value.GetRawText();
        foreach (var forbidden in new[]
        {
            "credentialRequirementRef", "externalPortRef", "authorityScope",
            "inputTableRef", "inputIdColumn", "inputStatusColumn", "outputTableRef",
            "retryPolicy", "projectionPolicy", "maxBatchSize", "leaseSeconds",
            "weather_credential_requirement", "weather_access_port",
        })
        {
            Assert.DoesNotContain(forbidden, raw);
        }
    }

    [Fact]
    public async Task ListSettings_RepositoryFailure_SurfacesAsErrorNotSilentSuccess()
    {
        var (runtime, repo, _) = CreateRuntime(SchedulerSettingsTestJobs.Job(Guid.NewGuid(), "alpha", active: true));
        repo.ThrowOnProjection = new InvalidOperationException("scheduler settings read failed");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runtime.ExecuteDataAsync(ListSettings(), default));
    }
}

/// <summary>
/// Shared SchedulerJobRecord builder for the scheduler-settings surface's tests. Only the fields the
/// surface actually projects/filters on are parameterized; every out-of-scope field
/// (authority/credential/port/table/policy) is given a fixed non-null value on purpose, so a
/// forbidden-field-absence assertion is meaningful rather than passing because the value was null.
/// </summary>
internal static class SchedulerSettingsTestJobs
{
    public static SchedulerJobRecord Job(
        Guid id, string jobKey, bool active,
        string triggerKind = "cron", string schedulePolicyKind = "cron",
        string? credentialRequirementRef = "some_credential_requirement",
        string? externalPortRef = "some_external_port") =>
        new(
            id, jobKey, triggerKind, schedulePolicyKind, "0 * * * *", null,
            ManualRunAllowed: true, Active: active,
            InputTableRef: "weather.region_inputs", InputStatusColumn: "status",
            InputStatusPendingValue: "pending", InputStatusProcessingValue: "processing",
            InputStatusCompletedValue: "completed", InputStatusFailedValue: "failed",
            MaxBatchSize: 10, LeaseSeconds: 300, AuthorityScope: "weather_job",
            CredentialRequirementRef: credentialRequirementRef, ExternalPortRef: externalPortRef,
            ProjectionPolicy: new Dictionary<string, object?>(StringComparer.Ordinal))
        {
            OutputTableRef = "weather.observations",
            Timezone = "UTC",
            UpdatedAt = new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero),
        };
}
