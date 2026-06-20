using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Xunit;

namespace Topolactor.Runtime.Tests;

// ---------------------------------------------------------------------------
// SchedulerJobRunner substrate tests.
//
// Proves:
//   1. Seed/DB manifest → runner → AbstractFunctionExecutor → step chain passes.
//   2. scheduler_context binding source resolves job_key, run_id, trigger_kind.
//   3. Payload-derived table/column authority is absent (no requestPayload in ctx).
//   4. Secret not in run log — projection_policy.allowed_result_keys sanitizes result_context.
//   5. Fail-close path: AbstractFunctionFailCloseException → run status "failed", explicit code.
//   6. manual_only policy is never auto-triggered by the poll loop.
//   7. Runtime lane mismatch (external_port_runtime ≠ scheduler_job_runtime) → fail-close.
// ---------------------------------------------------------------------------

public class SchedulerJobRunnerTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AbstractFunctionStep MakeStep(
        int order,
        string primitive,
        IReadOnlyList<AbstractFunctionInputBinding> bindings,
        string? resultKey,
        IReadOnlyDictionary<string, string>? stepConfig = null) =>
        new(Guid.NewGuid(), order, primitive, stepConfig ?? new Dictionary<string, string>(), bindings, resultKey, true);

    private static SchedulerJobRecord MakeJob(
        string jobKey = "demo_schedule",
        string authorityScope = "demo_scheduler_job",
        string schedulePolicyKind = "manual_only")
    {
        var projPolicy = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["allowed_result_keys"] = "[\"scheduler_projection\"]",
        };
        return new SchedulerJobRecord(
            SchedulerJobId: Guid.NewGuid(),
            JobKey: jobKey,
            TriggerKind: "cron",
            SchedulePolicyKind: schedulePolicyKind,
            CronExpression: null,
            ScheduleIntervalSeconds: null,
            ManualRunAllowed: true,
            Active: true,
            InputTableRef: null,
            InputStatusColumn: null,
            InputStatusPendingValue: null,
            InputStatusProcessingValue: null,
            InputStatusCompletedValue: null,
            InputStatusFailedValue: null,
            MaxBatchSize: 1,
            LeaseSeconds: 60,
            AuthorityScope: authorityScope,
            CredentialRequirementRef: null,
            ExternalPortRef: null,
            ProjectionPolicy: projPolicy);
    }

    // ─── Fakes ───────────────────────────────────────────────────────────────

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest? _manifest;
        public StaticManifestRepository(AbstractFunctionManifest? manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) =>
            Task.FromResult(_manifest);
    }

    private sealed class EchoPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public string PrimitiveKey => "echo";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default) =>
            Task.FromResult(inputs.TryGetValue("source", out var value) ? value : null);
    }

    private sealed class FailClosePrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
    {
        public string PrimitiveKey => "fail_close";
        public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default) =>
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "fail_close_triggered");
    }

    private sealed class FakeSchedulerJobManifestRepository : ISchedulerJobManifestRepository
    {
        private readonly IReadOnlyList<SchedulerJobRecord> _jobs;
        private readonly IReadOnlyList<SchedulerJobStepRecord> _steps;

        public List<(Guid RunId, string Status, string? LastError, string? ResultContext)> UpdateCalls { get; } = new();
        public List<SchedulerJobRunRecord> CreatedRuns { get; } = new();

        public FakeSchedulerJobManifestRepository(
            IReadOnlyList<SchedulerJobRecord> jobs,
            IReadOnlyList<SchedulerJobStepRecord> steps)
        {
            _jobs = jobs;
            _steps = steps;
        }

        public Task<IReadOnlyList<SchedulerJobRecord>> LoadActiveJobsAsync(CancellationToken ct = default) =>
            Task.FromResult(_jobs);

        public Task<IReadOnlyList<SchedulerJobStepRecord>> LoadStepsAsync(Guid schedulerJobId, CancellationToken ct = default) =>
            Task.FromResult(_steps);

        public Task<SchedulerJobRunRecord> CreateRunAsync(
            Guid schedulerJobId, string jobKey, string triggerKind, string schedulePolicyKind,
            string? inputRef, DateTimeOffset leaseUntil, CancellationToken ct = default)
        {
            var run = new SchedulerJobRunRecord(Guid.NewGuid(), schedulerJobId, jobKey, "queued");
            CreatedRuns.Add(run);
            return Task.FromResult(run);
        }

        public Task UpdateRunStatusAsync(Guid schedulerJobRunId, string runStatus, string? lastErrorJson, string? resultContextJson, CancellationToken ct = default)
        {
            UpdateCalls.Add((schedulerJobRunId, runStatus, lastErrorJson, resultContextJson));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SchedulerJobRecord>> LoadSettingsProjectionAsync(CancellationToken ct = default) =>
            Task.FromResult(_jobs);
    }

    // ─── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryExecuteJobAsync_SchedulerContextBinding_ResolvesWithoutMissingInput()
    {
        // Proves: scheduler_context binding source resolves job_key and run_id (required bindings)
        // without throwing MissingInput. If the scheduler_context were absent or bindings unsupported,
        // the executor would throw AbstractFunctionFailCloseException(MissingInput, ...).
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[]
            {
                MakeStep(1, "echo",
                    new[]
                    {
                        new AbstractFunctionInputBinding("job_key", "scheduler_context", "job_key", true, false),
                        new AbstractFunctionInputBinding("run_id", "scheduler_context", "run_id", true, false),
                        new AbstractFunctionInputBinding("trigger_kind", "scheduler_context", "trigger_kind", false, false),
                    },
                    "scheduler_projection"),
            },
            Array.Empty<string>(), true, authority);

        var job = MakeJob();
        var steps = new[]
        {
            new SchedulerJobStepRecord(
                Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", "scheduler_projection", true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.TryExecuteJobAsync(job, CancellationToken.None);

        // Run was created
        Assert.Single(fakeRepo.CreatedRuns);
        Assert.Equal(job.JobKey, fakeRepo.CreatedRuns[0].JobKey);
        // UpdateRunStatus called twice: processing then completed (no MissingInput failure)
        Assert.Equal(2, fakeRepo.UpdateCalls.Count);
        Assert.Equal("processing", fakeRepo.UpdateCalls[0].Status);
        Assert.Equal("completed", fakeRepo.UpdateCalls[1].Status);
    }

    [Fact]
    public async Task TryExecuteJobAsync_NoRequestPayload_PayloadBindingReturnsNullNotAuthority()
    {
        // Proves: scheduler execution context has no request payload.
        // A "payload" source binding returns null (no payload-derived table/column authority).
        // Optional payload binding must NOT cause a fail-close failure.
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[]
            {
                MakeStep(1, "echo",
                    new[]
                    {
                        // optional payload binding — returns null because no payload exists
                        new AbstractFunctionInputBinding("table_ref", "payload", "table_ref", false, false),
                    },
                    "scheduler_projection"),
            },
            Array.Empty<string>(), true, authority);

        var job = MakeJob();
        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", "scheduler_projection", true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.TryExecuteJobAsync(job, CancellationToken.None);

        Assert.Equal("completed", fakeRepo.UpdateCalls.Last().Status);
    }

    [Fact]
    public async Task TryExecuteJobAsync_ProjectionPolicy_SecretKeyNotInRunLog()
    {
        // Proves: projection_policy.allowed_result_keys sanitizes result_context before DB write.
        // "internal_secret_key" is produced by a manifest step but must NOT appear in run log.
        // Only "scheduler_projection" (the allowed key) appears in result_context.
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[]
            {
                MakeStep(1, "echo",
                    new[] { new AbstractFunctionInputBinding("source", "constant", "allowed_output", false, false) },
                    "scheduler_projection"),
                MakeStep(2, "echo",
                    new[] { new AbstractFunctionInputBinding("source", "constant", "MUST_NOT_APPEAR_IN_LOG", false, false) },
                    "internal_secret_key"),
            },
            Array.Empty<string>(), true, authority);

        var job = MakeJob();
        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", "scheduler_projection", true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.TryExecuteJobAsync(job, CancellationToken.None);

        var finalCall = fakeRepo.UpdateCalls.Last();
        Assert.Equal("completed", finalCall.Status);

        var resultJson = finalCall.ResultContext;
        Assert.NotNull(resultJson);
        Assert.Contains("scheduler_projection", resultJson);
        Assert.DoesNotContain("internal_secret_key", resultJson);
        Assert.DoesNotContain("MUST_NOT_APPEAR_IN_LOG", resultJson);
    }

    [Fact]
    public async Task TryExecuteJobAsync_FailClosePath_ExplicitFailedStatusWithErrorCode()
    {
        // Proves: AbstractFunctionFailCloseException causes run status "failed" with explicit
        // error code in last_error. Failure is never silent — always explicit status + code.
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[] { MakeStep(1, "fail_close", new[] { new AbstractFunctionInputBinding("source", "constant", "x", false, false) }, null) },
            Array.Empty<string>(), true, authority);

        var job = MakeJob();
        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", null, true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new FailClosePrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.TryExecuteJobAsync(job, CancellationToken.None);

        var finalCall = fakeRepo.UpdateCalls.Last();
        Assert.Equal("failed", finalCall.Status);
        Assert.NotNull(finalCall.LastError);
        Assert.Contains("fail_close_triggered", finalCall.LastError);
    }

    [Fact]
    public async Task RunDueJobsAsync_CronPolicy_ExecutesAndFiresDbNotify()
    {
        // Proves: cron job with a matching cron_expression and no prior run in the current
        // minute slot is due. Full representative path:
        //   cron poll → CronScheduleEvaluator.IsDue("* * * * *")=true, no same-slot run
        //   → TryExecuteJobAsync → AbstractFunctionExecutor (demo.scheduler_projection)
        //   → DB NOTIFY(manifest_id=0000000000f0).
        // Downstream from DB NOTIFY proven by SsotWiringAuditSchedulerRuntimeTests:
        //   DbNotifyListener → RuntimeTimelineScheduler.EnqueueHookTrigger
        //   → ManifestDispatcher(db_notify_projection_mapping → sse_projection_runtime)
        //   → SseEventBroadcaster → frontend SSE event {event:projection, data:{manifest_id:...}}.
        var notifyManifestId = Guid.Parse("00000000-0000-0000-0000-0000000000f0");
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[] { MakeStep(1, "echo", new[] { new AbstractFunctionInputBinding("source", "constant", "ok", false, false) }, "scheduler_projection") },
            Array.Empty<string>(), true, authority);

        var projPolicy = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["allowed_result_keys"] = "[\"scheduler_projection\"]",
            ["notify_manifest_id"] = $"\"{notifyManifestId}\"",
        };
        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "demo_schedule", "cron", "cron", "* * * * *", null, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null, projPolicy);
        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", "scheduler_projection", true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var spy = new SpyDbNotifyRepository();
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor, spy);

        await runner.RunDueJobsAsync(CancellationToken.None);

        // cron policy with cron_expression set is due: run must be created and reach completed
        Assert.Single(fakeRepo.CreatedRuns);
        Assert.Equal("completed", fakeRepo.UpdateCalls.Last().Status);
        // DB NOTIFY fired with notify_manifest_id from projection_policy
        Assert.Equal(1, spy.CallCount);
        Assert.Equal(notifyManifestId, spy.LastManifestId);
        Assert.Null(spy.LastTableId);
        Assert.Null(spy.LastTableRegistryId);
    }

    [Fact]
    public async Task RunDueJobsAsync_ManualOnlyPolicy_NeverAutoTriggered()
    {
        // Proves: manual_only schedule policy skips the auto-poll loop entirely.
        // No run ledger is created; substrate does not auto-trigger manual_only jobs.
        var job = MakeJob(schedulePolicyKind: "manual_only");
        var fakeRepo = new FakeSchedulerJobManifestRepository([job], []);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(null),
            Array.Empty<IAbstractFunctionPrimitiveAdapter>());
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.RunDueJobsAsync(CancellationToken.None);

        Assert.Empty(fakeRepo.CreatedRuns);
        Assert.Empty(fakeRepo.UpdateCalls);
    }

    [Fact]
    public async Task TryExecuteJobAsync_NotifyManifestId_DbNotifyCalledOnSuccess()
    {
        // Proves: after successful step chain, SchedulerJobRunner fires DB NOTIFY with the
        // manifest_id from projection_policy.notify_manifest_id.
        // This is the representative path: cron poll → AbstractFunctionExecutor → DB NOTIFY
        // → DbNotifyListener → RuntimeTimelineScheduler → ManifestDispatcher → SSE.
        var notifyManifestId = Guid.Parse("00000000-0000-0000-0000-0000000000f0");
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[] { MakeStep(1, "echo", new[] { new AbstractFunctionInputBinding("source", "constant", "ok", false, false) }, "scheduler_projection") },
            Array.Empty<string>(), true, authority);

        var projPolicy = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["allowed_result_keys"] = "[\"scheduler_projection\"]",
            // Raw JSON text matching ParseJsonObject(GetRawText()) format
            ["notify_manifest_id"] = $"\"{notifyManifestId}\"",
        };
        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "demo_schedule", "cron", "cron", null, null, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null, projPolicy);
        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", "scheduler_projection", true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var spy = new SpyDbNotifyRepository();
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor, spy);

        await runner.TryExecuteJobAsync(job, CancellationToken.None);

        Assert.Equal("completed", fakeRepo.UpdateCalls.Last().Status);
        Assert.Equal(1, spy.CallCount);
        Assert.Equal(notifyManifestId, spy.LastManifestId);
        Assert.Null(spy.LastTableId);
        Assert.Null(spy.LastTableRegistryId);
    }

    [Fact]
    public async Task TryExecuteJobAsync_NotifyManifestId_DbNotifyNotCalledOnFailure()
    {
        // Proves: DB NOTIFY is NOT sent when the step chain fails.
        // Only a completed run triggers the NOTIFY path.
        var notifyManifestId = Guid.Parse("00000000-0000-0000-0000-0000000000f0");
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[] { MakeStep(1, "fail_close", new[] { new AbstractFunctionInputBinding("source", "constant", "x", false, false) }, null) },
            Array.Empty<string>(), true, authority);

        var projPolicy = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["allowed_result_keys"] = "[\"scheduler_projection\"]",
            ["notify_manifest_id"] = $"\"{notifyManifestId}\"",
        };
        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "demo_schedule", "cron", "cron", null, null, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null, projPolicy);
        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", null, true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var spy = new SpyDbNotifyRepository();
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new FailClosePrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor, spy);

        await runner.TryExecuteJobAsync(job, CancellationToken.None);

        Assert.Equal("failed", fakeRepo.UpdateCalls.Last().Status);
        Assert.Equal(0, spy.CallCount);
    }

    [Fact]
    public async Task TryExecuteJobAsync_NoNotifyManifestId_DbNotifyNotCalled()
    {
        // Proves: when projection_policy has no notify_manifest_id, DB NOTIFY is skipped.
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[] { MakeStep(1, "echo", new[] { new AbstractFunctionInputBinding("source", "constant", "ok", false, false) }, "scheduler_projection") },
            Array.Empty<string>(), true, authority);

        var job = MakeJob(); // projection_policy has no notify_manifest_id
        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", "scheduler_projection", true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var spy = new SpyDbNotifyRepository();
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor, spy);

        await runner.TryExecuteJobAsync(job, CancellationToken.None);

        Assert.Equal("completed", fakeRepo.UpdateCalls.Last().Status);
        Assert.Equal(0, spy.CallCount);
    }

    [Fact]
    public async Task TryExecuteJobAsync_WrongRuntimeLane_FailsWithRuntimeLaneInvalid()
    {
        // Proves: abstract function manifests with runtime_lane != "scheduler_job_runtime"
        // cannot be executed from the scheduler. Fail-close with explicit RUNTIME_LANE_INVALID.
        // This enforces lane isolation between external_port_runtime and scheduler_job_runtime.
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "wrong.lane.function",
            "external_port_runtime", // wrong lane — must be scheduler_job_runtime
            "demo_scheduler_job",
            new[] { MakeStep(1, "echo", new[] { new AbstractFunctionInputBinding("x", "constant", "ok", false, false) }, null) },
            Array.Empty<string>(), true, authority);

        var job = MakeJob();
        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "wrong.lane.function", "fail_run", null, true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.TryExecuteJobAsync(job, CancellationToken.None);

        var finalCall = fakeRepo.UpdateCalls.Last();
        Assert.Equal("failed", finalCall.Status);
        Assert.NotNull(finalCall.LastError);
        Assert.Contains("ABSTRACT_FUNCTION_RUNTIME_LANE_INVALID", finalCall.LastError);
    }

    [Fact]
    public async Task RunDueJobsAsync_CronPolicy_SameMinuteCompleted_Skipped()
    {
        // Proves: cron job is skipped when a completed run already exists in the current
        // minute slot. Prevents multiple executions within the same cron-minute boundary.
        var now = DateTimeOffset.UtcNow;
        var thisMinute = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);

        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "demo_schedule", "cron", "cron", "* * * * *", null, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null,
            new Dictionary<string, object?>(StringComparer.Ordinal))
        {
            LastCompletedAt = thisMinute, // already ran this minute slot
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], []);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(null),
            Array.Empty<IAbstractFunctionPrimitiveAdapter>());
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.RunDueJobsAsync(CancellationToken.None);

        Assert.Empty(fakeRepo.CreatedRuns);
        Assert.Empty(fakeRepo.UpdateCalls);
    }

    [Fact]
    public async Task RunDueJobsAsync_CronPolicy_PriorMinuteCompleted_IsDue()
    {
        // Proves: cron job is due when the last completed run was in a prior minute slot.
        // The same-slot guard only blocks within the current minute boundary.
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[] { MakeStep(1, "echo", new[] { new AbstractFunctionInputBinding("source", "constant", "ok", false, false) }, "scheduler_projection") },
            Array.Empty<string>(), true, authority);

        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "demo_schedule", "cron", "cron", "* * * * *", null, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null,
            new Dictionary<string, object?>(StringComparer.Ordinal))
        {
            LastCompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1), // previous minute → not same slot
        };

        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", "scheduler_projection", true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.RunDueJobsAsync(CancellationToken.None);

        Assert.Single(fakeRepo.CreatedRuns);
        Assert.Equal("completed", fakeRepo.UpdateCalls.Last().Status);
    }

    [Fact]
    public async Task RunDueJobsAsync_CronPolicy_InvalidCronExpression_SkipsJob()
    {
        // Proves: malformed/unsupported cron_expression causes the job to be skipped.
        // CronScheduleEvaluator.IsDue returns false for any invalid expression.
        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "demo_schedule", "cron", "cron",
            CronExpression: "not-a-cron", // invalid — evaluator returns false → skip
            ScheduleIntervalSeconds: null, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null,
            new Dictionary<string, object?>(StringComparer.Ordinal));

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], []);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(null),
            Array.Empty<IAbstractFunctionPrimitiveAdapter>());
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.RunDueJobsAsync(CancellationToken.None);

        Assert.Empty(fakeRepo.CreatedRuns);
        Assert.Empty(fakeRepo.UpdateCalls);
    }

    [Fact]
    public async Task RunDueJobsAsync_CronPolicy_NullCronExpression_SkipsJob()
    {
        // Proves: cron job with no cron_expression set is skipped — not due.
        // Guard: job author must set cron_expression before the job fires on any poll cycle.
        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "demo_schedule", "cron", "cron",
            CronExpression: null, // not configured — must be skipped
            ScheduleIntervalSeconds: null, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null,
            new Dictionary<string, object?>(StringComparer.Ordinal));

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], []);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(null),
            Array.Empty<IAbstractFunctionPrimitiveAdapter>());
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.RunDueJobsAsync(CancellationToken.None);

        Assert.Empty(fakeRepo.CreatedRuns);
        Assert.Empty(fakeRepo.UpdateCalls);
    }

    [Fact]
    public async Task RunDueJobsAsync_IntervalSeconds_NoLastRun_IsDue()
    {
        // Proves: interval_seconds job with no prior completed run (LastCompletedAt=null) is due.
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[] { MakeStep(1, "echo", new[] { new AbstractFunctionInputBinding("source", "constant", "ok", false, false) }, "scheduler_projection") },
            Array.Empty<string>(), true, authority);

        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "interval_job", "cron", "interval_seconds",
            CronExpression: null,
            ScheduleIntervalSeconds: 300, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null,
            new Dictionary<string, object?>(StringComparer.Ordinal));
        // LastCompletedAt defaults to null — no prior run

        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", "scheduler_projection", true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.RunDueJobsAsync(CancellationToken.None);

        Assert.Single(fakeRepo.CreatedRuns);
        Assert.Equal("completed", fakeRepo.UpdateCalls.Last().Status);
    }

    [Fact]
    public async Task RunDueJobsAsync_IntervalSeconds_GapNotMet_Skipped()
    {
        // Proves: interval_seconds job is skipped when the gap since LastCompletedAt < interval.
        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "interval_job", "cron", "interval_seconds",
            CronExpression: null,
            ScheduleIntervalSeconds: 3600, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null,
            new Dictionary<string, object?>(StringComparer.Ordinal))
        {
            LastCompletedAt = DateTimeOffset.UtcNow.AddSeconds(-60), // only 60s ago, interval is 3600s
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], []);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(null),
            Array.Empty<IAbstractFunctionPrimitiveAdapter>());
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.RunDueJobsAsync(CancellationToken.None);

        Assert.Empty(fakeRepo.CreatedRuns);
        Assert.Empty(fakeRepo.UpdateCalls);
    }

    [Fact]
    public async Task RunDueJobsAsync_IntervalSeconds_GapMet_IsDue()
    {
        // Proves: interval_seconds job is due when the gap since LastCompletedAt >= interval.
        var authority = new AbstractFunctionAuthorityBinding[] { new("policy", "demo_scheduler_job_policy", true) };
        var manifest = new AbstractFunctionManifest(
            Guid.NewGuid(), "demo.scheduler_projection", "scheduler_job_runtime", "demo_scheduler_job",
            new[] { MakeStep(1, "echo", new[] { new AbstractFunctionInputBinding("source", "constant", "ok", false, false) }, "scheduler_projection") },
            Array.Empty<string>(), true, authority);

        var job = new SchedulerJobRecord(
            Guid.NewGuid(), "interval_job", "cron", "interval_seconds",
            CronExpression: null,
            ScheduleIntervalSeconds: 300, true, true,
            null, null, null, null, null, null, 1, 60, "demo_scheduler_job", null, null,
            new Dictionary<string, object?>(StringComparer.Ordinal))
        {
            LastCompletedAt = DateTimeOffset.UtcNow.AddSeconds(-400), // 400s ago, interval is 300s → due
        };

        var steps = new[]
        {
            new SchedulerJobStepRecord(Guid.NewGuid(), job.SchedulerJobId, 1, "demo.scheduler_projection", "fail_run", "scheduler_projection", true)
        };

        var fakeRepo = new FakeSchedulerJobManifestRepository([job], steps);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { new EchoPrimitiveAdapter() });
        var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, fakeRepo, executor);

        await runner.RunDueJobsAsync(CancellationToken.None);

        Assert.Single(fakeRepo.CreatedRuns);
        Assert.Equal("completed", fakeRepo.UpdateCalls.Last().Status);
    }

    // ─── Spy ─────────────────────────────────────────────────────────────────

    private sealed class SpyDbNotifyRepository() : DbNotifyRepository(NullLogger<DbNotifyRepository>.Instance)
    {
        public int CallCount { get; private set; }
        public Guid? LastManifestId { get; private set; }
        public string? LastTableId { get; private set; }
        public string? LastTableRegistryId { get; private set; }

        public override Task NotifyAsync(string? tableId, string? tableRegistryId, Guid? manifestId, CancellationToken ct = default)
        {
            CallCount++;
            LastTableId = tableId;
            LastTableRegistryId = tableRegistryId;
            LastManifestId = manifestId;
            return Task.CompletedTask;
        }
    }
}
