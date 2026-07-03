using System.Text.Json;
using System.Text.Json.Serialization;

namespace Topolactor.Schema;

public static class AggregateTriggerVocabulary
{
    public const string RuntimeDestination = "aggregate_trigger_runtime";
    public static readonly ISet<string> CanonicalTriggerKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cron", "hook", "client" };
    public static readonly ISet<string> TriggerSourceDetailKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "client_operation_event", "hook_event", "scheduled_cron_event", "runtime_function_event" };
    public static readonly ISet<string> MaterializationPayloadMapAllowedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "function_input_event", "aggregate_current_row", "selected_step2_entity_fields", "selected_step2_5_relation_fields", "constant", "generated_value", "runtime_actor_source_metadata"
    };
    public static readonly ISet<string> ApprovalPolicyAllowedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "auto_materialize_when_threshold_passes", "require_backend_approval_before_materialization", "require_human_approval_before_materialization"
    };
    public static readonly ISet<string> ComparisonOperatorAllowedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ">", ">=", "<", "<=", "=", "!=" };
    public static readonly ISet<string> ExecutionScopeAllowedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "single_event", "aggregate_key_partition", "operation_instance", "tenant_or_workspace_partition" };
    public static readonly ISet<string> TransactionBoundaryAllowedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "event_append_only", "event_append_and_aggregate_upsert", "event_append_aggregate_upsert_and_materialization" };
}

public record AggregateTriggerSource(
    [property: JsonPropertyName("canonical_trigger_kind")] string CanonicalTriggerKind,
    [property: JsonPropertyName("trigger_source_detail_kind")] string TriggerSourceDetailKind);

public record AggregateTriggerTargetBinding(
    [property: JsonPropertyName("target_source")] string TargetSource,
    [property: JsonPropertyName("target_id")] string TargetId);

public record AggregateTriggerProcessingFunctionScope(
    [property: JsonPropertyName("function_id")] string FunctionId,
    [property: JsonPropertyName("operation_definition_id")] string OperationDefinitionId,
    [property: JsonPropertyName("accepted_event_schema_ref")] string AcceptedEventSchemaRef,
    [property: JsonPropertyName("allowed_source_kinds")] IReadOnlyList<string> AllowedSourceKinds,
    [property: JsonPropertyName("materialization_policy_ref")] string MaterializationPolicyRef);

public record AggregateTriggerThresholdPolicy(
    [property: JsonPropertyName("minimum_trial_count")] int MinimumTrialCount,
    [property: JsonPropertyName("ratio_numerator_field")] string RatioNumeratorField,
    [property: JsonPropertyName("ratio_denominator_field")] string RatioDenominatorField,
    [property: JsonPropertyName("comparison_operator")] string ComparisonOperator,
    [property: JsonPropertyName("target_ratio")] decimal TargetRatio);

public record AggregateTriggerPayloadMapEntry(
    [property: JsonPropertyName("target_field")] string TargetField,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("source_field")] string? SourceField = null,
    [property: JsonPropertyName("constant_value")] JsonElement? ConstantValue = null);

public record AggregateTriggerDefinition(
    [property: JsonPropertyName("trigger_definition_id")] Guid TriggerDefinitionId,
    [property: JsonPropertyName("trigger_source")] AggregateTriggerSource TriggerSource,
    [property: JsonPropertyName("processing_function_scope")] AggregateTriggerProcessingFunctionScope ProcessingFunctionScope,
    [property: JsonPropertyName("execution_scope")] string ExecutionScope,
    [property: JsonPropertyName("transaction_boundary")] string TransactionBoundary,
    [property: JsonPropertyName("aggregate_target_binding")] AggregateTriggerTargetBinding AggregateTargetBinding,
    [property: JsonPropertyName("conflict_key_fields")] IReadOnlyList<string> ConflictKeyFields,
    [property: JsonPropertyName("delta_map")] IReadOnlyDictionary<string, decimal> DeltaMap,
    [property: JsonPropertyName("threshold_policy")] AggregateTriggerThresholdPolicy ThresholdPolicy,
    [property: JsonPropertyName("materialization_target_binding")] AggregateTriggerTargetBinding MaterializationTargetBinding,
    [property: JsonPropertyName("materialization_payload_map")] IReadOnlyList<AggregateTriggerPayloadMapEntry> MaterializationPayloadMap,
    [property: JsonPropertyName("approval_policy")] string ApprovalPolicy,
    [property: JsonPropertyName("evidence_policy")] string? EvidencePolicy = null);

public record AggregateTriggerEventEvidence(Guid TriggerDefinitionId, string EventId, string CanonicalTriggerKind, string TriggerSourceDetailKind, JsonElement EventPayload, string? Actor, string? Source);
public record AggregateTriggerCurrentRow(Guid TriggerDefinitionId, string ConflictKey, IReadOnlyDictionary<string, decimal> Counters, DateTimeOffset UpdatedAt);

public record AggregateTriggerRuntimeRequest(
    [property: JsonPropertyName("definition")] AggregateTriggerDefinition Definition,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("conflict_key")] string ConflictKey,
    [property: JsonPropertyName("event_payload")] JsonElement EventPayload,
    [property: JsonPropertyName("declared_step2_logical_entity_definition_ids")] IReadOnlyList<string> DeclaredStep2LogicalEntityDefinitionIds,
    [property: JsonPropertyName("declared_step2_5_relation_definition_ids")] IReadOnlyList<string> DeclaredStep25RelationDefinitionIds,
    [property: JsonPropertyName("actor")] string? Actor = null,
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("approval_granted")] bool ApprovalGranted = false);

public record AggregateTriggerRuntimeResult(bool Accepted, bool EventAppended, bool ThresholdSatisfied, bool Materialized, string Status, Guid? MaterializationId);

public enum AggregateTriggerMaterializationDecision { ThresholdNotSatisfied, ApprovalRequired, Materialize }
