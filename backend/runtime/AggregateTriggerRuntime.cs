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

            // transaction_boundary (processing_function_or_operation_definition authority) gates how far
            // this event's processing proceeds; it is never broadened by the event itself.
            if (string.Equals(runtimeRequest.Definition.TransactionBoundary, "event_append_only", StringComparison.OrdinalIgnoreCase))
                return Data(new AggregateTriggerRuntimeResult(true, true, false, false, "event_appended_only", null));

            if (string.Equals(runtimeRequest.Definition.TransactionBoundary, "event_append_and_aggregate_upsert", StringComparison.OrdinalIgnoreCase))
            {
                var upserted = await repository.AtomicUpsertCurrentAsync(runtimeRequest.Definition.TriggerDefinitionId, runtimeRequest.ConflictKey, runtimeRequest.Definition.DeltaMap, ct);
                var satisfied = AggregateTriggerConditionEvaluator.Evaluate(runtimeRequest.Definition.ThresholdPolicy, upserted);
                return Data(new AggregateTriggerRuntimeResult(true, true, satisfied, false, satisfied ? "threshold_satisfied_materialization_out_of_scope" : "threshold_not_satisfied", null));
            }

            var approvalRequired = string.Equals(runtimeRequest.Definition.ApprovalPolicy, "require_backend_approval_before_materialization", StringComparison.OrdinalIgnoreCase)
                || string.Equals(runtimeRequest.Definition.ApprovalPolicy, "require_human_approval_before_materialization", StringComparison.OrdinalIgnoreCase);

            // aggregate current upsert + materialization evidence append run in one backend transaction
            // (aggregate_trigger_substrate_contract.transaction_boundary.atomicity_rule); the decision
            // delegate is evaluated in-process against the freshly upserted row while that transaction
            // is still open, so this never becomes an app-side read -> count++ -> update race.
            var result = await repository.AtomicUpsertAndMaterializeAsync(
                runtimeRequest.Definition,
                runtimeRequest.ConflictKey,
                runtimeRequest.EventId,
                row => !AggregateTriggerConditionEvaluator.Evaluate(runtimeRequest.Definition.ThresholdPolicy, row)
                    ? AggregateTriggerMaterializationDecision.ThresholdNotSatisfied
                    : approvalRequired && !runtimeRequest.ApprovalGranted
                        ? AggregateTriggerMaterializationDecision.ApprovalRequired
                        : AggregateTriggerMaterializationDecision.Materialize,
                ct);

            return result.Decision switch
            {
                AggregateTriggerMaterializationDecision.ThresholdNotSatisfied => Data(new AggregateTriggerRuntimeResult(true, true, false, false, "threshold_not_satisfied", null)),
                AggregateTriggerMaterializationDecision.ApprovalRequired => Data(new AggregateTriggerRuntimeResult(true, true, true, false, "approval_required", null)),
                _ => Data(new AggregateTriggerRuntimeResult(true, true, true, result.Materialization!.Created, result.Materialization.Created ? "materialized" : "duplicate_materialization_guard", result.Materialization.MaterializationId == Guid.Empty ? null : result.Materialization.MaterializationId)),
            };
        }
        catch (JsonException ex) { return Error("AGGREGATE_TRIGGER_PAYLOAD_INVALID", ex.Message); }
    }
    private static EndpointResponseDto Error(string code, string msg) => new(false, null, [new ValidationError(code, msg)]);
    private static EndpointResponseDto Data(AggregateTriggerRuntimeResult result) => new(true, new Emission(null, null, null, [], JsonSerializer.SerializeToElement(result), []), []);
}
