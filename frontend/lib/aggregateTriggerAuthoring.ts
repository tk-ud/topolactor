export const aggregateTriggerRuntimeDestination = "aggregate_trigger_runtime" as const;
export const aggregateTriggerKinds = ["cron", "hook", "client"] as const;
export const sourceDetailKinds = ["schedule", "webhook", "ui_operation", "system_operation", "component_event"] as const;
export const materializationPayloadSourceKinds = [
  "function_input_event",
  "aggregate_current_row",
  "step2_entity_field",
  "step2_5_relation_field",
  "constant",
  "generated_value",
  "runtime_actor_metadata",
  "runtime_source_metadata",
] as const;

export type StepTarget = { kind: "step2_entity" | "step2_5_relation"; id: string; label: string };

export function aggregateTriggerTargetOptions(step2Entities: StepTarget[], step25Relations: StepTarget[]): StepTarget[] {
  return [...step2Entities, ...step25Relations].filter((target) =>
    target.kind === "step2_entity" || target.kind === "step2_5_relation"
  );
}

export function previewAggregateTriggerDefinition(input: {
  triggerKind: string;
  sourceDetailKind: string;
  aggregateTarget: StepTarget;
  materializationTarget: StepTarget;
}) {
  return {
    runtimeDestination: aggregateTriggerRuntimeDestination,
    triggerKind: input.triggerKind,
    sourceDetailKind: input.sourceDetailKind,
    aggregateTarget: input.aggregateTarget,
    materializationTarget: input.materializationTarget,
    frontendJudgment: "authoring_preview_only_no_threshold_materialization_or_approval_decision",
  };
}
