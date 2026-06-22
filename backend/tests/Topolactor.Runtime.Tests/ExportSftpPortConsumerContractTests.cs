using System.Runtime.CompilerServices;
using System.Text.Json;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ExportSftpPortConsumerContractTests
{
    [Fact]
    public void TopologySchema_SftpTransferLog_DependsOnFileStorageAndRejectsUnsafeSecretFields()
    {
        var sql = File.ReadAllText(Path.Combine(RepoRoot(), "db", "topology_tables.sql"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS topology.sftp_transfer_log", sql);
        Assert.Contains("export_job_id        UUID REFERENCES topology.export_jobs", sql);
        Assert.Contains("file_artifact_id     UUID REFERENCES topology.file_artifacts", sql);
        Assert.Contains("manifest_id          UUID REFERENCES topology.export_manifests", sql);
        Assert.Contains("checksum_record_id   UUID REFERENCES topology.file_checksum_records", sql);
        Assert.Contains("checksum_mismatch", sql);
        Assert.Contains("retry_attempted", sql);
        Assert.Contains("failure_reason", sql);
        Assert.Contains("retry_evidence_json", sql);
        Assert.DoesNotContain("sftp_host", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sftp_private_key", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remote_path        ", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SeedEmpty_ExportSftpPolicy_RecordsFullLifecycleRuntimeEventsWithoutNewDbFunctionMutation()
    {
        var sql = File.ReadAllText(Path.Combine(RepoRoot(), "db", "seed_empty.sql"));
        Assert.Contains("'export_sftp_response_port_generic'", sql);
        Assert.Contains("record_transfer_lifecycle_evidence", sql);
        Assert.Contains("transfer_initiated", sql);
        Assert.Contains("transfer_completed", sql);
        Assert.Contains("transfer_failed", sql);
        Assert.Contains("checksum_mismatch", sql);
        Assert.Contains("retry_attempted", sql);
        Assert.Contains("\"initiated_event_type\":\"transfer_initiated\"", sql);
        Assert.DoesNotContain("'append_runtime_event_log',     '{\"event_type\":\"transfer_completed", sql);
        Assert.DoesNotContain("'append_runtime_event_log',     '{\"event_type\":\"transfer_failed", sql);
        Assert.DoesNotContain("'append_runtime_event_log',     '{\"event_type\":\"checksum_mismatch", sql);
        Assert.DoesNotContain("'append_runtime_event_log',     '{\"event_type\":\"retry_attempted", sql);
        Assert.Contains("\"requires_export_job\":\"true\"", sql);
        Assert.Contains("\"requires_manifest\":\"true\"", sql);
        Assert.Contains("\"requires_checksum\":\"true\"", sql);
        Assert.DoesNotContain("execute_db_function',     '{\"function\":\"export_sftp", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"retry_trigger_target\":\"external-port:response_port:00000000-0000-0000-0000-000000000f08\"", sql);
    }

    [Fact]
    public void ImplementationSsot_DoesNotSpecifyProviderSecretsOrSpecificClient()
    {
        var ssot = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "design", "runtime-bundle-export-sftp-implementation-ssot.yaml"));
        Assert.Contains("provider_specific_sftp_client_or_runtime", ssot);
        Assert.Contains("external-port:response_port:00000000-0000-0000-0000-000000000f08", ssot);
        Assert.Contains("topology.file_checksum_records", ssot);
        Assert.Contains("raw_provider_response", ssot);
        Assert.DoesNotContain("sftp://", ssot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN OPENSSH", ssot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NpgsqlEvidenceRepository_SftpTransferLog_RequiresCanonicalFileStorageRefsBeforeInsert()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "backend", "repository", "NpgsqlExternalPortConsumerEvidenceRepository.cs"));
        Assert.Contains("ResolveSftpTransferSourceRefsAsync", source);
        Assert.Contains("SFTP_TRANSFER_EXPORT_JOB_ID_REQUIRED", source);
        Assert.Contains("SFTP_TRANSFER_EXPORT_JOB_MANIFEST_CHECKSUM_REQUIRED", source);
        Assert.Contains("SFTP_TRANSFER_CHECKSUM_MISMATCH", source);
        Assert.Contains("JOIN topology.export_manifests", source);
        Assert.Contains("JOIN topology.file_artifacts", source);
        Assert.Contains("JOIN topology.file_checksum_records", source);
        Assert.Contains("ej.status = 'completed'", source);
        Assert.Contains("cr.verification_status = 'verified'", source);
        Assert.DoesNotContain("INSERT INTO topology.sftp_transfer_log (export_job_id, transfer_status", source);
        Assert.DoesNotContain("VALUES (NULL, @statusValue", source);
    }


    [Fact]
    public async Task TransferLifecycleOperation_SuccessFailureMismatchAndRetry_AreMutuallyExclusiveBranches()
    {
        var step = NewLifecycleStep();

        var successLog = new CapturingRuntimeEventLogRepository();
        var successEvidence = new BranchingEvidenceRepository();
        var successExecutor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: successLog, consumerEvidenceRepository: successEvidence);
        var successContext = NewLifecycleContext(new { export_job_id = Guid.NewGuid().ToString() });
        await successExecutor.ExecuteAsync(step, successContext);
        Assert.Equal(new[] { "transfer_initiated", "transfer_completed" }, successLog.EventTypes);
        Assert.Equal(new[] { "transfer_initiated", "transfer_completed" }, successEvidence.AppendedEventTypes);
        Assert.False(successContext.SchedulerEventEnqueued);

        var missingLog = new CapturingRuntimeEventLogRepository();
        var missingEvidence = new BranchingEvidenceRepository(failCompletedWith: "SFTP_TRANSFER_EXPORT_JOB_MANIFEST_CHECKSUM_REQUIRED");
        var missingExecutor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: missingLog, consumerEvidenceRepository: missingEvidence);
        var missingContext = NewLifecycleContext(new { export_job_id = Guid.NewGuid().ToString() });
        var missingEx = await Assert.ThrowsAsync<InvalidOperationException>(() => missingExecutor.ExecuteAsync(step, missingContext));
        Assert.Equal("SFTP_TRANSFER_EXPORT_JOB_MANIFEST_CHECKSUM_REQUIRED", missingEx.Message);
        Assert.Equal(new[] { "transfer_initiated", "transfer_failed" }, missingLog.EventTypes);
        Assert.Equal(new[] { "transfer_initiated", "transfer_completed", "transfer_failed" }, missingEvidence.AppendedEventTypes);

        var mismatchLog = new CapturingRuntimeEventLogRepository();
        var mismatchEvidence = new BranchingEvidenceRepository(failCompletedWith: "SFTP_TRANSFER_CHECKSUM_MISMATCH");
        var mismatchExecutor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: mismatchLog, consumerEvidenceRepository: mismatchEvidence);
        var mismatchContext = NewLifecycleContext(new { export_job_id = Guid.NewGuid().ToString() });
        var mismatchEx = await Assert.ThrowsAsync<InvalidOperationException>(() => mismatchExecutor.ExecuteAsync(step, mismatchContext));
        Assert.Equal("SFTP_TRANSFER_CHECKSUM_MISMATCH", mismatchEx.Message);
        Assert.Equal(new[] { "transfer_initiated", "checksum_mismatch" }, mismatchLog.EventTypes);
        Assert.Equal(new[] { "transfer_initiated", "transfer_completed", "checksum_mismatch" }, mismatchEvidence.AppendedEventTypes);

        var retryLog = new CapturingRuntimeEventLogRepository();
        var retryEvidence = new BranchingEvidenceRepository();
        var retryScheduler = new CapturingSchedulerEnqueueBoundary();
        var retryExecutor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: retryLog, consumerEvidenceRepository: retryEvidence, schedulerEnqueueBoundary: retryScheduler);
        var retryContext = NewLifecycleContext(new { export_job_id = Guid.NewGuid().ToString(), retry_requested = true });
        await retryExecutor.ExecuteAsync(step, retryContext);
        Assert.True(retryContext.SchedulerEventEnqueued);
        Assert.Equal(1, retryScheduler.EnqueueCount);
        Assert.Equal("external-port:response_port:00000000-0000-0000-0000-000000000f08", retryScheduler.LastEnqueuedTarget);
        Assert.Equal(new[] { "retry_attempted" }, retryLog.EventTypes);
        Assert.Equal(new[] { "retry_attempted" }, retryEvidence.AppendedEventTypes);
    }

    [Fact]
    public async Task RetryBranch_WithoutSchedulerBoundary_ThrowsExplicitFailure()
    {
        var step = NewLifecycleStep();
        var log = new CapturingRuntimeEventLogRepository();
        var evidence = new BranchingEvidenceRepository();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: log, consumerEvidenceRepository: evidence);
        var context = NewLifecycleContext(new { export_job_id = Guid.NewGuid().ToString(), retry_requested = true });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(step, context));
        Assert.Equal("EXTERNAL_PORT_SCHEDULER_ENQUEUE_BOUNDARY_MISSING", ex.Message);
        Assert.False(context.SchedulerEventEnqueued);
    }

    [Fact]
    public async Task RetryBranch_SchedulerQueueFull_ThrowsExplicitFailure()
    {
        var step = NewLifecycleStep();
        var log = new CapturingRuntimeEventLogRepository();
        var evidence = new BranchingEvidenceRepository();
        var fullScheduler = new CapturingSchedulerEnqueueBoundary(queueFull: true);
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: log, consumerEvidenceRepository: evidence, schedulerEnqueueBoundary: fullScheduler);
        var context = NewLifecycleContext(new { export_job_id = Guid.NewGuid().ToString(), retry_requested = true });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(step, context));
        Assert.Equal("SCHEDULER_QUEUE_FULL", ex.Message);
        Assert.False(context.SchedulerEventEnqueued);
    }

    [Fact]
    public async Task RetryBranch_SchedulerEnqueueSucceeds_SetsEnqueuedAndRecordsEvidence()
    {
        var step = NewLifecycleStep();
        var log = new CapturingRuntimeEventLogRepository();
        var evidence = new BranchingEvidenceRepository();
        var scheduler = new CapturingSchedulerEnqueueBoundary();
        var executor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: log, consumerEvidenceRepository: evidence, schedulerEnqueueBoundary: scheduler);
        var context = NewLifecycleContext(new { export_job_id = Guid.NewGuid().ToString(), retry_requested = true });
        await executor.ExecuteAsync(step, context);
        Assert.True(context.SchedulerEventEnqueued);
        Assert.Equal(1, scheduler.EnqueueCount);
        Assert.Equal("external-port:response_port:00000000-0000-0000-0000-000000000f08", scheduler.LastEnqueuedTarget);
        Assert.Equal(new[] { "retry_attempted" }, log.EventTypes);
        Assert.Contains("scheduler_enqueue_result", evidence.LastStepConfig?.Keys ?? []);
        Assert.Equal("enqueued", evidence.LastStepConfig?.GetValueOrDefault("scheduler_enqueue_result"));
    }

    private static string RepoRoot([CallerFilePath] string sourceFile = "")
    {
        var fromSource = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", ".."));
        if (File.Exists(Path.Combine(fromSource, "db", "seed_empty.sql"))) return fromSource;
        const string workspaceRoot = "/workspace/topolactor";
        if (File.Exists(Path.Combine(workspaceRoot, "db", "seed_empty.sql"))) return workspaceRoot;
        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "db", "seed_empty.sql"))) return cwd;
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "db", "seed_empty.sql")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    private static ExternalPortPolicyStep NewLifecycleStep() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        9,
        "record_transfer_lifecycle_evidence",
        new Dictionary<string, string>
        {
            ["evidence_table_ref"] = "topology.sftp_transfer_log",
            ["projection_table_ref"] = "topology.sftp_transfer_log",
            ["initiated_event_type"] = "transfer_initiated",
            ["completed_event_type"] = "transfer_completed",
            ["failed_event_type"] = "transfer_failed",
            ["checksum_mismatch_event_type"] = "checksum_mismatch",
            ["retry_event_type"] = "retry_attempted",
            ["retry_trigger_target"] = "external-port:response_port:00000000-0000-0000-0000-000000000f08",
            ["retry_policy_ref"] = "scheduler_retry_requested"
        },
        true);

    private static ExternalPortExecutionContext NewLifecycleContext(object payload) => new()
    {
        DispatchId = Guid.NewGuid().ToString("N"),
        RequiredByBundle = "export_sftp_bundle",
        PortKind = "response_port",
        RequestPayload = JsonSerializer.SerializeToElement(payload)
    };

    private sealed class CapturingRuntimeEventLogRepository : IExternalPortRuntimeEventLogRepository
    {
        private readonly List<string> _eventTypes = new();
        public IReadOnlyList<string> EventTypes => _eventTypes;
        public Task AppendAsync(string eventType, string? entityId, string? requiredByBundle, CancellationToken ct = default)
        {
            _eventTypes.Add(eventType);
            return Task.CompletedTask;
        }
    }

    private sealed class BranchingEvidenceRepository : IExternalPortConsumerEvidenceRepository
    {
        private readonly string? _failCompletedWith;
        private readonly List<string> _appendedEventTypes = new();
        public IReadOnlyList<string> AppendedEventTypes => _appendedEventTypes;
        public IReadOnlyDictionary<string, string>? LastStepConfig { get; private set; }
        public BranchingEvidenceRepository(string? failCompletedWith = null) => _failCompletedWith = failCompletedWith;

        public Task AppendEvidenceAsync(string tableRef, string eventType, string? entityId, string? requiredByBundle, ExternalPortExecutionContext context, IReadOnlyDictionary<string, string> stepConfig, CancellationToken ct = default)
        {
            _appendedEventTypes.Add(eventType);
            LastStepConfig = stepConfig;
            if (eventType == "transfer_completed" && _failCompletedWith is not null)
                throw new InvalidOperationException(_failCompletedWith);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExternalPortConsumerEvidenceProjection>> LoadProjectionAsync(string tableRef, string? requiredByBundle, string? entityId, int limit = 20, CancellationToken ct = default)
        {
            IReadOnlyList<ExternalPortConsumerEvidenceProjection> rows =
            [
                new(tableRef, _appendedEventTypes.LastOrDefault() ?? "none", entityId, _appendedEventTypes.LastOrDefault(), new Dictionary<string, string>())
            ];
            return Task.FromResult(rows);
        }
    }

    private sealed class CapturingSchedulerEnqueueBoundary : ISchedulerEnqueueBoundary
    {
        private readonly bool _queueFull;
        private int _enqueueCount;
        private EndpointRequestDto? _lastRequest;

        public CapturingSchedulerEnqueueBoundary(bool queueFull = false) => _queueFull = queueFull;
        public int EnqueueCount => _enqueueCount;
        public string? LastEnqueuedTarget => _lastRequest?.Target;

        public bool TryEnqueueHookTrigger(EndpointRequestDto request)
        {
            if (_queueFull) return false;
            _lastRequest = request;
            _enqueueCount++;
            return true;
        }
    }

}
