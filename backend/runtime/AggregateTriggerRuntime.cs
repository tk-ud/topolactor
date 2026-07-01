using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

public class AggregateTriggerRuntime(AggregateTriggerRepository repository) : IDispatchableRuntime
{
    public async Task<EndpointResponseDto> ExecuteAsync(EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default)
    {
        try
        {
            if (!request.Payload.HasValue) return Error("AGGREGATE_TRIGGER_PAYLOAD_REQUIRED", "aggregate trigger payload is required.");
            var runtimeRequest = request.Payload.Value.Deserialize<AggregateTriggerRuntimeRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (runtimeRequest is null) return Error("AGGREGATE_TRIGGER_PAYLOAD_INVALID", "aggregate trigger payload is invalid.");
            var validation = AggregateTriggerDefinitionValidator.Validate(
                runtimeRequest.Definition,
                new HashSet<string>(runtimeRequest.DeclaredStep2LogicalEntityDefinitionIds, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(runtimeRequest.DeclaredStep25RelationDefinitionIds, StringComparer.OrdinalIgnoreCase)).ToList();
            if (!AggregateTriggerVocabulary.CanonicalTriggerKinds.Contains(request.TriggerKind ?? runtimeRequest.Definition.TriggerSource.CanonicalTriggerKind)) validation.Add(new("AGGREGATE_TRIGGER_KIND_INVALID", "request trigger_kind must be cron, hook, or client."));
            if (validation.Count > 0) return new(false, null, validation);

            var evidence = new AggregateTriggerEventEvidence(runtimeRequest.Definition.TriggerDefinitionId, runtimeRequest.EventId, runtimeRequest.Definition.TriggerSource.CanonicalTriggerKind, runtimeRequest.Definition.TriggerSource.TriggerSourceDetailKind, runtimeRequest.EventPayload, runtimeRequest.Actor, runtimeRequest.Source);
            var append = await repository.AppendEventEvidenceAsync(evidence, ct);
            if (!append.Appended) return Data(new AggregateTriggerRuntimeResult(true, false, false, false, "duplicate_event_evidence", null));
            var current = await repository.AtomicUpsertCurrentAsync(runtimeRequest.Definition.TriggerDefinitionId, runtimeRequest.ConflictKey, runtimeRequest.Definition.DeltaMap, ct);
            var threshold = AggregateTriggerConditionEvaluator.Evaluate(runtimeRequest.Definition.ThresholdPolicy, current);
            if (!threshold) return Data(new AggregateTriggerRuntimeResult(true, true, false, false, "threshold_not_satisfied", null));
            if ((string.Equals(runtimeRequest.Definition.ApprovalPolicy, "require_backend_approval_before_materialization", StringComparison.OrdinalIgnoreCase) || string.Equals(runtimeRequest.Definition.ApprovalPolicy, "require_human_approval_before_materialization", StringComparison.OrdinalIgnoreCase)) && !runtimeRequest.ApprovalGranted) return Data(new AggregateTriggerRuntimeResult(true, true, true, false, "approval_required", null));
            var materialized = await repository.TryMaterializeAsync(runtimeRequest.Definition, current, runtimeRequest.EventId, ct);
            return Data(new AggregateTriggerRuntimeResult(true, true, true, materialized.Created, materialized.Created ? "materialized" : "duplicate_materialization_guard", materialized.MaterializationId == Guid.Empty ? null : materialized.MaterializationId));
        }
        catch (JsonException ex) { return Error("AGGREGATE_TRIGGER_PAYLOAD_INVALID", ex.Message); }
    }
    private static EndpointResponseDto Error(string code, string msg) => new(false, null, [new ValidationError(code, msg)]);
    private static EndpointResponseDto Data(AggregateTriggerRuntimeResult result) => new(true, new Emission(null, null, null, [], JsonSerializer.SerializeToElement(result), []), []);
}
