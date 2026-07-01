using System.Text.Json;
using System.Text.Json.Serialization;

namespace Topolactor.Schema;

public static class AggregateTriggerVocabulary
{
    public const string RuntimeDestination = "aggregate_trigger_runtime";
    public static readonly ISet<string> TriggerKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cron", "hook", "client" };
    public static readonly ISet<string> SourceDetailKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "schedule", "webhook", "ui_operation", "system_operation", "component_event" };
    public static readonly ISet<string> PayloadSourceKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "function_input_event", "aggregate_current_row", "step2_entity_field", "step2_5_relation_field", "constant", "generated_value", "runtime_actor_metadata", "runtime_source_metadata"
    };
    public static readonly ISet<string> ApprovalPolicies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "none", "required" };
    public static readonly ISet<string> ComparisonOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ">=", ">", "=", "<=", "<" };
}

public record AggregateTriggerTargetBinding(
    [property: JsonPropertyName("target_kind")] string TargetKind,
    [property: JsonPropertyName("target_id")] string TargetId);

public record AggregateTriggerThresholdPolicy(
    [property: JsonPropertyName("minimum_trial_count")] int MinimumTrialCount,
    [property: JsonPropertyName("ratio_numerator_field")] string RatioNumeratorField,
    [property: JsonPropertyName("ratio_denominator_field")] string RatioDenominatorField,
    [property: JsonPropertyName("comparison_operator")] string ComparisonOperator,
    [property: JsonPropertyName("target_ratio")] decimal TargetRatio);

public record AggregateTriggerPayloadMapEntry(
    [property: JsonPropertyName("target_field")] string TargetField,
    [property: JsonPropertyName("source_kind")] string SourceKind,
    [property: JsonPropertyName("source_field")] string? SourceField = null,
    [property: JsonPropertyName("constant_value")] JsonElement? ConstantValue = null);

public record AggregateTriggerDefinition(
    [property: JsonPropertyName("definition_id")] Guid DefinitionId,
    [property: JsonPropertyName("trigger_kind")] string TriggerKind,
    [property: JsonPropertyName("source_detail_kind")] string SourceDetailKind,
    [property: JsonPropertyName("processing_function_ref")] string ProcessingFunctionRef,
    [property: JsonPropertyName("execution_scope")] string ExecutionScope,
    [property: JsonPropertyName("transaction_boundary")] string TransactionBoundary,
    [property: JsonPropertyName("aggregate_target_binding")] AggregateTriggerTargetBinding AggregateTargetBinding,
    [property: JsonPropertyName("conflict_key_fields")] IReadOnlyList<string> ConflictKeyFields,
    [property: JsonPropertyName("delta_map")] IReadOnlyDictionary<string, decimal> DeltaMap,
    [property: JsonPropertyName("threshold_policy")] AggregateTriggerThresholdPolicy ThresholdPolicy,
    [property: JsonPropertyName("materialization_target_binding")] AggregateTriggerTargetBinding MaterializationTargetBinding,
    [property: JsonPropertyName("materialization_payload_map")] IReadOnlyList<AggregateTriggerPayloadMapEntry> MaterializationPayloadMap,
    [property: JsonPropertyName("approval_policy")] string ApprovalPolicy);

public record AggregateTriggerEventEvidence(
    Guid DefinitionId,
    string EventId,
    string TriggerKind,
    string SourceDetailKind,
    JsonElement EventPayload,
    string? Actor,
    string? Source);

public record AggregateTriggerCurrentRow(
    Guid DefinitionId,
    string ConflictKey,
    IReadOnlyDictionary<string, decimal> Counters,
    DateTimeOffset UpdatedAt);

public record AggregateTriggerRuntimeRequest(
    [property: JsonPropertyName("definition")] AggregateTriggerDefinition Definition,
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("conflict_key")] string ConflictKey,
    [property: JsonPropertyName("event_payload")] JsonElement EventPayload,
    [property: JsonPropertyName("actor")] string? Actor = null,
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("approval_granted")] bool ApprovalGranted = false);

public record AggregateTriggerRuntimeResult(bool Accepted, bool EventAppended, bool ThresholdSatisfied, bool Materialized, string Status, Guid? MaterializationId);
