using System.Text.Json;

using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Consumer bundle step handler for export_sftp_bundle.
///
/// Owns the transfer lifecycle operation_key (record_transfer_lifecycle_evidence) so the
/// generic ExternalPortPolicyStepExecutor stays free of Export/SFTP-specific lifecycle
/// meaning. Registered via the IExternalPortBundleStepHandler extension contract
/// (external_port_substrate consumer_bundle_step_handler_surface), the same mechanism
/// FileStorageBundleStepHandler uses.
///
/// Event types, evidence/projection table refs, and the retry trigger target are all
/// data-defined via policy step_config (seed). The remaining C# is the irreducible runtime
/// branching (success / checksum_mismatch / failed / retry plus scheduler enqueue), which an
/// abstract function manifest cannot express (no conditionals; scheduler enqueue is not SQL).
/// Evidence/event-log writes go through the seed-directed, manifest-binding-validated
/// IExternalPortConsumerEvidenceRepository / IExternalPortRuntimeEventLogRepository boundaries;
/// no raw tableRef dynamic SQL and no provider-specific SFTP client are introduced.
/// Prohibited: provider_kind / required_by_bundle C# branching, silent fallback.
/// </summary>
public sealed class ExportSftpBundleStepHandler : IExternalPortBundleStepHandler
{
    private static readonly IReadOnlySet<string> _supportedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "record_transfer_lifecycle_evidence"
    };

    private readonly IExternalPortRuntimeEventLogRepository? _runtimeEventLogRepository;
    private readonly IExternalPortConsumerEvidenceRepository? _consumerEvidenceRepository;
    private readonly ISchedulerEnqueueBoundary? _schedulerEnqueueBoundary;

    public ExportSftpBundleStepHandler(
        IExternalPortRuntimeEventLogRepository? runtimeEventLogRepository = null,
        IExternalPortConsumerEvidenceRepository? consumerEvidenceRepository = null,
        ISchedulerEnqueueBoundary? schedulerEnqueueBoundary = null)
    {
        _runtimeEventLogRepository = runtimeEventLogRepository;
        _consumerEvidenceRepository = consumerEvidenceRepository;
        _schedulerEnqueueBoundary = schedulerEnqueueBoundary;
    }

    public IReadOnlySet<string> SupportedOperationKeys => _supportedKeys;

    public Task ExecuteAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct = default) =>
        step.OperationKey switch
        {
            "record_transfer_lifecycle_evidence" => RecordTransferLifecycleEvidenceAsync(step, context, ct),
            _ => throw new InvalidOperationException("EXTERNAL_PORT_POLICY_OPERATION_UNSUPPORTED")
        };

    private async Task RecordTransferLifecycleEvidenceAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct)
    {
        if (_runtimeEventLogRepository is null)
            throw new InvalidOperationException("EXTERNAL_PORT_RUNTIME_EVENT_LOG_REPOSITORY_MISSING");
        if (_consumerEvidenceRepository is null)
            throw new InvalidOperationException("EXTERNAL_PORT_CONSUMER_EVIDENCE_REPOSITORY_MISSING");

        var tableRef = ReadConfig(step.StepConfig, "evidence_table_ref")
            ?? throw new InvalidOperationException("TRANSFER_LIFECYCLE_EVIDENCE_TABLE_REF_MISSING");
        var projectionTableRef = ReadConfig(step.StepConfig, "projection_table_ref");
        var bundle = context.RequiredByBundle ?? context.PortRecord?.RequiredByBundle;
        var entityId = context.DispatchId;

        async Task AppendLifecycleAsync(string eventType, IReadOnlyDictionary<string, string> config)
        {
            await _consumerEvidenceRepository.AppendEvidenceAsync(tableRef, eventType, entityId, bundle, context, config, ct);
            await _runtimeEventLogRepository.AppendAsync(eventType, entityId, bundle, ct);
        }

        if (ReadBoolProperty(context.RequestPayload, "retry_requested"))
        {
            if (_schedulerEnqueueBoundary is null)
                throw new InvalidOperationException("EXTERNAL_PORT_SCHEDULER_ENQUEUE_BOUNDARY_MISSING");
            var retryRequest = ExternalPortPolicyStepExecutor.BuildRetrySchedulerRequest(step.StepConfig, context);
            if (!_schedulerEnqueueBoundary.TryEnqueueHookTrigger(retryRequest))
                throw new InvalidOperationException("SCHEDULER_QUEUE_FULL");
            context.SchedulerEventEnqueued = true;
            var retryEvent = ReadConfig(step.StepConfig, "retry_event_type") ?? "retry_attempted";
            var retryConfig = new Dictionary<string, string>(step.StepConfig, StringComparer.Ordinal)
            {
                ["event_type"] = retryEvent,
                ["status_value"] = "retry_attempted",
                ["scheduler_enqueue_result"] = "enqueued",
                ["scheduler_enqueue_boundary_ref"] = ReadConfig(step.StepConfig, "retry_trigger_target") ?? "scheduler_enqueue_boundary"
            };
            await AppendLifecycleAsync(retryEvent, retryConfig);
            await LoadLifecycleProjectionAsync(_consumerEvidenceRepository, projectionTableRef, bundle, entityId, context, ct);
            context.MarkExecuted(step.OperationKey);
            return;
        }

        var initiatedEvent = ReadConfig(step.StepConfig, "initiated_event_type") ?? "transfer_initiated";
        var completedEvent = ReadConfig(step.StepConfig, "completed_event_type") ?? "transfer_completed";
        try
        {
            await AppendLifecycleAsync(initiatedEvent, MergeConfig(step.StepConfig, initiatedEvent, "transfer_initiated"));
            await AppendLifecycleAsync(completedEvent, MergeConfig(step.StepConfig, completedEvent, "transfer_completed"));
            await LoadLifecycleProjectionAsync(_consumerEvidenceRepository, projectionTableRef, bundle, entityId, context, ct);
            context.MarkExecuted(step.OperationKey);
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "SFTP_TRANSFER_CHECKSUM_MISMATCH", StringComparison.Ordinal))
        {
            var mismatchEvent = ReadConfig(step.StepConfig, "checksum_mismatch_event_type") ?? "checksum_mismatch";
            await AppendLifecycleAsync(mismatchEvent, MergeConfig(step.StepConfig, mismatchEvent, "checksum_mismatch", "checksum_mismatch"));
            context.MarkExecuted(step.OperationKey);
            throw;
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "SFTP_TRANSFER_EXPORT_JOB_MANIFEST_CHECKSUM_REQUIRED", StringComparison.Ordinal) ||
                                                   string.Equals(ex.Message, "SFTP_TRANSFER_EXPORT_JOB_ID_REQUIRED", StringComparison.Ordinal))
        {
            var failedEvent = ReadConfig(step.StepConfig, "failed_event_type") ?? "transfer_failed";
            await AppendLifecycleAsync(failedEvent, MergeConfig(step.StepConfig, failedEvent, "transfer_failed", ex.Message));
            context.MarkExecuted(step.OperationKey);
            throw;
        }
    }

    private static async Task LoadLifecycleProjectionAsync(
        IExternalPortConsumerEvidenceRepository consumerEvidenceRepository,
        string? projectionTableRef,
        string? bundle,
        string? entityId,
        ExternalPortExecutionContext context,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(projectionTableRef)) return;
        var projection = await consumerEvidenceRepository.LoadProjectionAsync(projectionTableRef, bundle, entityId, limit: 20, ct);
        context.OutputProp = JsonSerializer.Serialize(new
        {
            projectionTableRef,
            responseLane = "projection_response",
            rows = projection
        });
    }

    private static IReadOnlyDictionary<string, string> MergeConfig(
        IReadOnlyDictionary<string, string> config,
        string eventType,
        string statusValue,
        string? failureReason = null)
    {
        var merged = new Dictionary<string, string>(config, StringComparer.Ordinal)
        {
            ["event_type"] = eventType,
            ["status_value"] = statusValue
        };
        if (!string.IsNullOrWhiteSpace(failureReason))
            merged["failure_reason"] = failureReason;
        return merged;
    }

    private static bool ReadBoolProperty(JsonElement? payload, string name)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } element) return false;
        if (!element.TryGetProperty(name, out var prop)) return false;
        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(prop.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static string? ReadConfig(IReadOnlyDictionary<string, string> config, string key) =>
        config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
