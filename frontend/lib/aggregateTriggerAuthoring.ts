export const aggregateTriggerRuntimeDestination = "aggregate_trigger_runtime" as const;
export const canonicalTriggerKinds = ["cron", "hook", "client"] as const;
export const triggerSourceDetailKinds = ["client_operation_event", "hook_event", "scheduled_cron_event", "runtime_function_event"] as const;
export const materializationPayloadMapAllowedSources = [
  "function_input_event",
  "aggregate_current_row",
  "selected_step2_entity_fields",
  "selected_step2_5_relation_fields",
  "constant",
  "generated_value",
  "runtime_actor_source_metadata",
] as const;
export const approvalPolicyAllowedValues = [
  "auto_materialize_when_threshold_passes",
  "require_backend_approval_before_materialization",
  "require_human_approval_before_materialization",
] as const;
export const comparisonOperatorAllowedValues = [">", ">=", "<", "<=", "=", "!="] as const;

export type StepTarget = { targetSource: "step2_logical_entity_definition" | "step2_5_relation_definition"; targetId: string; label: string };

export function aggregateTriggerTargetOptions(step2LogicalEntityDefinitions: StepTarget[], step25RelationDefinitions: StepTarget[]): StepTarget[] {
  return [...step2LogicalEntityDefinitions, ...step25RelationDefinitions].filter((target) =>
    target.targetSource === "step2_logical_entity_definition" || target.targetSource === "step2_5_relation_definition"
  );
}

export function previewAggregateTriggerDefinition(input: {
  canonicalTriggerKind: string;
  triggerSourceDetailKind: string;
  aggregateTargetBinding: StepTarget;
  materializationTargetBinding: StepTarget;
}) {
  return {
    runtimeDestination: aggregateTriggerRuntimeDestination,
    triggerSource: {
      canonicalTriggerKind: input.canonicalTriggerKind,
      triggerSourceDetailKind: input.triggerSourceDetailKind,
    },
    aggregateTargetBinding: input.aggregateTargetBinding,
    materializationTargetBinding: input.materializationTargetBinding,
    frontendRole: "structured_selector_and_preview_only",
  };
}
