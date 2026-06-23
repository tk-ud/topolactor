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
        public Task<IReadOnlyList<SchedulerJobRecord>> LoadSettingsProjectionAsync(CancellationToken ct = default) =>
            Task.FromResult(ProjectionJobs);

        public Task<IReadOnlyList<SchedulerJobRecord>> LoadActiveJobsAsync(CancellationToken ct = default) => Task.FromResult(ProjectionJobs);
        public Task<IReadOnlyList<SchedulerJobStepRecord>> LoadStepsAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SchedulerJobStepRecord>>(Array.Empty<SchedulerJobStepRecord>());
        public Task<SchedulerJobRunRecord> CreateRunAsync(Guid a, string b, string c, string d, string? e, DateTimeOffset f, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateRunStatusAsync(Guid a, string b, string? c, string? d, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SchedulerInputRow>> LeaseDueInputRowsAsync(SchedulerJobRecord job, DateTimeOffset now, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SchedulerInputRow>>(Array.Empty<SchedulerInputRow>());
        public Task UpdateInputRowStatusAsync(SchedulerJobRecord job, string id, string s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpsertAuthorizedOutputAsync(SchedulerJobRecord job, SchedulerStepResultBinding b, IReadOnlyDictionary<string, object?> v, CancellationToken ct = default) => Task.CompletedTask;
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

    [Fact]
    public async Task Disable_ValidPayload_SetsInactive()
    {
        var repo = new StubSchedulerRepo();
        var runtime = CreateRuntime(repo);
        var id = Guid.NewGuid();
        var (data, error) = await runtime.ExecuteDataAsync(Vector("disable", new { schedulerJobId = id.ToString() }), default);

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

    [Fact]
    public async Task ListSettings_Projection_NoSecretMaterialLeaked()
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
        // References are shown; secret material is never present.
        Assert.Contains("weather_credential_requirement", raw);
        Assert.Contains("weather_access_port", raw);
        Assert.DoesNotContain("api_key", raw);
        Assert.DoesNotContain("access_token", raw);
        Assert.DoesNotContain("client_secret", raw);
    }
}
