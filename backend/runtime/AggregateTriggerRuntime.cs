using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

public class AggregateTriggerRuntime(AggregateTriggerRepository repository) : IDispatchableRuntime
{
    private static readonly HashSet<string> Empty = new(StringComparer.OrdinalIgnoreCase);
    public async Task<EndpointResponseDto> ExecuteAsync(EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default)
    {
        try
        {
            if (!request.Payload.HasValue) return Error("AGGREGATE_TRIGGER_PAYLOAD_REQUIRED", "aggregate trigger payload is required.");
            var runtimeRequest = request.Payload.Value.Deserialize<AggregateTriggerRuntimeRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (runtimeRequest is null) return Error("AGGREGATE_TRIGGER_PAYLOAD_INVALID", "aggregate trigger payload is invalid.");
            var validation = AggregateTriggerDefinitionValidator.Validate(runtimeRequest.Definition, Empty, Empty)
                .Where(e => e.Code is not "AGGREGATE_TARGET_INVALID" and not "AGGREGATE_MATERIALIZATION_TARGET_INVALID").ToList();
            if (!AggregateTriggerVocabulary.TriggerKinds.Contains(request.TriggerKind ?? runtimeRequest.Definition.TriggerKind)) validation.Add(new("AGGREGATE_TRIGGER_KIND_INVALID", "request trigger_kind must be cron, hook, or client."));
            if (validation.Count > 0) return new(false, null, validation);

            var evidence = new AggregateTriggerEventEvidence(runtimeRequest.Definition.DefinitionId, runtimeRequest.EventId, runtimeRequest.Definition.TriggerKind, runtimeRequest.Definition.SourceDetailKind, runtimeRequest.EventPayload, runtimeRequest.Actor, runtimeRequest.Source);
            var append = await repository.AppendEventEvidenceAsync(evidence, ct);
            if (!append.Appended) return Data(new AggregateTriggerRuntimeResult(true, false, false, false, "duplicate_event_evidence", null));
            var current = await repository.AtomicUpsertCurrentAsync(runtimeRequest.Definition.DefinitionId, runtimeRequest.ConflictKey, runtimeRequest.Definition.DeltaMap, ct);
            var threshold = AggregateTriggerConditionEvaluator.Evaluate(runtimeRequest.Definition.ThresholdPolicy, current);
            if (!threshold) return Data(new AggregateTriggerRuntimeResult(true, true, false, false, "threshold_not_satisfied", null));
            if (string.Equals(runtimeRequest.Definition.ApprovalPolicy, "required", StringComparison.OrdinalIgnoreCase) && !runtimeRequest.ApprovalGranted) return Data(new AggregateTriggerRuntimeResult(true, true, true, false, "approval_required", null));
            var materialized = await repository.TryMaterializeAsync(runtimeRequest.Definition, current, runtimeRequest.EventId, ct);
            return Data(new AggregateTriggerRuntimeResult(true, true, true, materialized.Created, materialized.Created ? "materialized" : "duplicate_materialization_guard", materialized.MaterializationId == Guid.Empty ? null : materialized.MaterializationId));
        }
        catch (JsonException ex) { return Error("AGGREGATE_TRIGGER_PAYLOAD_INVALID", ex.Message); }
    }
    private static EndpointResponseDto Error(string code, string msg) => new(false, null, [new ValidationError(code, msg)]);
    private static EndpointResponseDto Data(AggregateTriggerRuntimeResult result) => new(true, new Emission(null, null, null, [], JsonSerializer.SerializeToElement(result), []), []);
}
