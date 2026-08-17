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

        public Task<Guid> CreateJobAsync(SchedulerJobDraft draft, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateJobAsync(Guid id, SchedulerJobDraft draft, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SchedulerJobRecord>> LoadActiveJobsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SchedulerJobRecord>>(Jobs);
        public Task<IReadOnlyList<SchedulerJobStepRecord>> LoadStepsAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SchedulerJobStepRecord>>(Array.Empty<SchedulerJobStepRecord>());
        public Task<SchedulerJobRunRecord> CreateRunAsync(Guid a, string b, string c, string d, string? e, DateTimeOffset f, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateRunStatusAsync(Guid a, string b, string? c, string? d, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SchedulerInputRow>> LeaseDueInputRowsAsync(SchedulerJobRecord job, DateTimeOffset now, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SchedulerInputRow>>(Array.Empty<SchedulerInputRow>());
        public Task UpdateInputRowStatusAsync(SchedulerJobRecord job, string id, string s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpsertAuthorizedOutputAsync(SchedulerJobRecord job, SchedulerStepResultBinding b, IReadOnlyDictionary<string, object?> v, CancellationToken ct = default) => Task.CompletedTask;
        public Task<SchedulerJobCredentialBindingResult> UpdateCredentialBindingAsync(Guid id, string? c, string? p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SchedulerJobCredentialBindingResult> ValidateCredentialBindingAsync(Guid id, string? c, string? p, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static SchedulerJobRecord Job(Guid id, string jobKey, bool active, string triggerKind = "cron", string schedulePolicyKind = "cron") =>
        new(id, jobKey, triggerKind, schedulePolicyKind, "0 * * * *", null, true, active,
            null, null, null, null, null, null,
            10, 300, "test_scope", null, null, new Dictionary<string, object?>(StringComparer.Ordinal));

    private (AdminRuntime runtime, RecordingSchedulerRepo repo, RecordingSqlAttentionLogsRepository logs) CreateRuntime(
        IReadOnlyList<SchedulerJobRecord>? jobs = null)
    {
        var repo = new RecordingSchedulerRepo { Jobs = (jobs ?? Array.Empty<SchedulerJobRecord>()).ToList() };
        var logs = new RecordingSqlAttentionLogsRepository();
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var runtime = new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            schedulerJobManifestRepository: repo, sqlAttentionLogsRepository: logs);
        return (runtime, repo, logs);
    }

    private static OperationVector Vector(
        string action, object? payload, string? authenticatedRole = "admin") =>
        new("admin", "scheduler_jobs", action, null, "admin",
            payload is null ? null : JsonSerializer.SerializeToElement(payload), null)
        {
            AuthenticatedRole = authenticatedRole,
        };

    // ── enable/disable: dryRun preview ──────────────────────────────────────

    [Fact]
    public async Task Enable_DryRun_PreviewsWithoutWriting()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime([Job(id, "weather_24h", active: false)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("enable", new { schedulerJobId = id.ToString(), dryRun = true }), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.True(data.Value.GetProperty("dryRun").GetBoolean());
        Assert.True(data.Value.GetProperty("valid").GetBoolean());
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    [Fact]
    public async Task Disable_DryRun_PreviewsWithoutWriting()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime([Job(id, "weather_24h", active: true)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("disable", new { schedulerJobId = id.ToString(), dryRun = true }), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("valid").GetBoolean());
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    // ── enable/disable: not confirmed fail-close (cancel path never writes) ──

    [Fact]
    public async Task Enable_NeitherDryRunNorConfirmed_FailsClosedWithoutWriting()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, _) = CreateRuntime([Job(id, "weather_24h", active: false)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("enable", new { schedulerJobId = id.ToString() }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_ENABLE_NOT_CONFIRMED", error!.Code);
        Assert.Empty(repo.ActiveSet);
    }

    [Fact]
    public async Task Disable_NeitherDryRunNorConfirmed_FailsClosedWithoutWriting()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, _) = CreateRuntime([Job(id, "weather_24h", active: true)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("disable", new { schedulerJobId = id.ToString() }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_DISABLE_NOT_CONFIRMED", error!.Code);
        Assert.Empty(repo.ActiveSet);
    }

    // ── enable/disable: confirmed write + diff_log envelope ──────────────────

    [Fact]
    public async Task Enable_Confirmed_WritesAndAppendsDiffLog()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime([Job(id, "weather_24h", active: false)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("enable", new { schedulerJobId = id.ToString(), confirmed = true }), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.True(data.Value.GetProperty("active").GetBoolean());
        Assert.Single(repo.ActiveSet);
        Assert.Equal((id, true), repo.ActiveSet[0]);

        Assert.Single(logs.Requests);
        var envelope = logs.Requests[0];
        Assert.Equal("topology.scheduler_jobs", envelope.PhysicalTableName);
        Assert.Equal(id.ToString(), envelope.RecordId);
        Assert.Equal("update", envelope.OperationKind);
        Assert.Contains("false", envelope.BeforeStateOrDiffJson);
        Assert.Contains("true", envelope.AfterStateOrDiffJson);
    }

    [Fact]
    public async Task Disable_Confirmed_WritesAndAppendsDiffLog()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime([Job(id, "weather_24h", active: true)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("disable", new { schedulerJobId = id.ToString(), confirmed = true }), default);

        Assert.Null(error);
        Assert.False(data!.Value.GetProperty("active").GetBoolean());
        Assert.Single(repo.ActiveSet);
        Assert.Equal((id, false), repo.ActiveSet[0]);
        Assert.Single(logs.Requests);
    }

    // ── enable/disable: already-in-target-state fail-close (no no-op write) ──

    [Fact]
    public async Task Enable_AlreadyActive_FailsClosedEvenWhenConfirmed()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime([Job(id, "weather_24h", active: true)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("enable", new { schedulerJobId = id.ToString(), confirmed = true }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_ALREADY_ACTIVE", error!.Code);
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    [Fact]
    public async Task Disable_AlreadyInactive_FailsClosedEvenWhenConfirmed()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, logs) = CreateRuntime([Job(id, "weather_24h", active: false)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("disable", new { schedulerJobId = id.ToString(), confirmed = true }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_ALREADY_INACTIVE", error!.Code);
        Assert.Empty(repo.ActiveSet);
        Assert.Empty(logs.Requests);
    }

    // ── enable/disable: missing identity / not found ──────────────────────────

    [Fact]
    public async Task Enable_UnknownSchedulerJobId_FailsClosedNotFound()
    {
        var (runtime, _, _) = CreateRuntime();

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("enable", new { schedulerJobId = Guid.NewGuid().ToString(), confirmed = true }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task Enable_MalformedPayload_MissingSchedulerJobId_FailsClosed()
    {
        var (runtime, _, _) = CreateRuntime();

        var (data, error) = await runtime.ExecuteDataAsync(Vector("enable", new { }), default);

        Assert.Null(data);
        Assert.Equal("MALFORMED_PAYLOAD", error!.Code);
    }

    // ── enable/disable: unauthorized mutation fail-close ──────────────────────

    [Fact]
    public async Task Enable_NonAdminRole_FailsClosedWithAuthCapabilityDenied()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, _) = CreateRuntime([Job(id, "weather_24h", active: false)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("enable", new { schedulerJobId = id.ToString(), confirmed = true }, authenticatedRole: "normal"), default);

        Assert.Null(data);
        Assert.Equal("AUTH_CAPABILITY_DENIED", error!.Code);
        Assert.Empty(repo.ActiveSet);
    }

    [Fact]
    public async Task Disable_NoAuthenticatedRole_FailsClosedWithAuthCapabilityDenied()
    {
        var id = Guid.NewGuid();
        var (runtime, repo, _) = CreateRuntime([Job(id, "weather_24h", active: true)]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("disable", new { schedulerJobId = id.ToString(), confirmed = true }, authenticatedRole: null), default);

        Assert.Null(data);
        Assert.Equal("AUTH_CAPABILITY_DENIED", error!.Code);
        Assert.Empty(repo.ActiveSet);
    }

    // ── list_settings: search / filter axes ────────────────────────────────────

    [Fact]
    public async Task ListSettings_Search_FiltersByJobKeySubstring()
    {
        var (runtime, _, _) = CreateRuntime([
            Job(Guid.NewGuid(), "weather_24h", active: true),
            Job(Guid.NewGuid(), "log_retention_sweep", active: true),
        ]);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("list_settings", new { search = "weather" }), default);

        Assert.Null(error);
        var jobs = data!.Value.GetProperty("schedulerJobs");
        Assert.Equal(1, jobs.GetArrayLength());
        Assert.Equal("weather_24h", jobs[0].GetProperty("jobKey").GetString());
    }

    [Theory]
    [InlineData("triggerKind", "hook")]
    [InlineData("schedulePolicyKind", "manual_only")]
    public async Task ListSettings_Filter_ByTriggerKindOrSchedulePolicyKind(string field, string value)
    {
        var matching = field == "triggerKind"
            ? Job(Guid.NewGuid(), "hook_job", active: true, triggerKind: "hook")
            : Job(Guid.NewGuid(), "manual_job", active: true, schedulePolicyKind: "manual_only");
        var (runtime, _, _) = CreateRuntime([
            matching,
            Job(Guid.NewGuid(), "other_job", active: true),
        ]);

        var payload = field == "triggerKind"
            ? (object)new { triggerKind = value }
            : new { schedulePolicyKind = value };
        var (data, error) = await runtime.ExecuteDataAsync(Vector("list_settings", payload), default);

        Assert.Null(error);
        var jobs = data!.Value.GetProperty("schedulerJobs");
        Assert.Equal(1, jobs.GetArrayLength());
    }

    [Fact]
    public async Task ListSettings_Filter_ByActive()
    {
        var (runtime, _, _) = CreateRuntime([
            Job(Guid.NewGuid(), "active_job", active: true),
            Job(Guid.NewGuid(), "inactive_job", active: false),
        ]);

        var (data, error) = await runtime.ExecuteDataAsync(Vector("list_settings", new { active = false }), default);

        Assert.Null(error);
        var jobs = data!.Value.GetProperty("schedulerJobs");
        Assert.Equal(1, jobs.GetArrayLength());
        Assert.Equal("inactive_job", jobs[0].GetProperty("jobKey").GetString());
    }

    [Fact]
    public async Task ListSettings_InvalidTriggerKindFilter_FailsClosed()
    {
        var (runtime, _, _) = CreateRuntime();

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("list_settings", new { triggerKind = "not_a_real_kind" }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_TRIGGER_KIND_INVALID", error!.Code);
    }

    [Fact]
    public async Task ListSettings_NonObjectPayload_FailsClosed()
    {
        var (runtime, _, _) = CreateRuntime();

        var vector = new OperationVector("admin", "scheduler_jobs", "list_settings", null, "admin",
            JsonSerializer.SerializeToElement("not-an-object"), null);
        var (data, error) = await runtime.ExecuteDataAsync(vector, default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_LIST_SETTINGS_PAYLOAD_NOT_OBJECT", error!.Code);
    }

    [Fact]
    public async Task ListSettings_AbsentPayload_ReturnsFullUnfilteredList()
    {
        var (runtime, _, _) = CreateRuntime([
            Job(Guid.NewGuid(), "job_a", active: true),
            Job(Guid.NewGuid(), "job_b", active: false),
        ]);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "scheduler_jobs", "list_settings", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.Equal(2, data!.Value.GetProperty("schedulerJobs").GetArrayLength());
    }

    // ── list_settings: forbidden fields / secret projection denial ────────────

    [Fact]
    public async Task ListSettings_Projection_ForbiddenFieldsAbsentAndNoSecretMaterialLeaked()
    {
        var id = Guid.NewGuid();
        var job = new SchedulerJobRecord(
            id, "weather_24h", "cron", "cron", "0 * * * *", null, true, true,
            "weather.region_inputs", "status", "pending", "processing", "completed", "failed",
            10, 300, "weather_job", "weather_credential_requirement", "weather_access_port",
            new Dictionary<string, object?>(StringComparer.Ordinal));
        var (runtime, _, _) = CreateRuntime([job]);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "scheduler_jobs", "list_settings", null, "admin", null, null), default);

        Assert.Null(error);
        var raw = data!.Value.GetRawText();
        Assert.DoesNotContain("weather_credential_requirement", raw);
        Assert.DoesNotContain("weather_access_port", raw);
        Assert.DoesNotContain("credentialRequirementRef", raw);
        Assert.DoesNotContain("externalPortRef", raw);
        Assert.DoesNotContain("authorityScope", raw);
        Assert.DoesNotContain("maxBatchSize", raw);
        Assert.DoesNotContain("leaseSeconds", raw);
        Assert.DoesNotContain("api_key", raw);
        Assert.DoesNotContain("access_token", raw);
        Assert.DoesNotContain("client_secret", raw);
    }

    // ── DB/runtime failure surfaces as explicit error, never silent success ───

    [Fact]
    public async Task ListSettings_RepositoryThrows_PropagatesAsFailureNotSilentEmptySuccess()
    {
        var (runtime, repo, _) = CreateRuntime();
        repo.ThrowOnProjection = new InvalidOperationException("db unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.ExecuteDataAsync(
                new OperationVector("admin", "scheduler_jobs", "list_settings", null, "admin", null, null), default));
    }

    [Fact]
    public async Task Enable_Unconfigured_ReturnsNotConfigured()
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var runtime = new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            schedulerJobManifestRepository: null);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("enable", new { schedulerJobId = Guid.NewGuid().ToString(), confirmed = true }), default);

        Assert.Null(data);
        Assert.Equal("SCHEDULER_JOB_MANIFEST_NOT_CONFIGURED", error!.Code); // role gate passes (admin), then repo-configured check fails
    }
}
