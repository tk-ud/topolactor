export const aggregateTriggerRuntimeDestination =
  "aggregate_trigger_runtime" as const;
export const canonicalTriggerKinds = ["cron", "hook", "client"] as const;
export const triggerSourceDetailKinds = [
  "client_operation_event",
  "hook_event",
  "scheduled_cron_event",
  "runtime_function_event",
] as const;
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
export const comparisonOperatorAllowedValues = [
  ">",
  ">=",
  "<",
  "<=",
  "=",
  "!=",
] as const;
export const executionScopeAllowedValues = [
  "single_event",
  "aggregate_key_partition",
  "operation_instance",
  "tenant_or_workspace_partition",
] as const;
export const transactionBoundaryAllowedValues = [
  "event_append_only",
  "event_append_and_aggregate_upsert",
  "event_append_aggregate_upsert_and_materialization",
] as const;

export type StepTarget = {
  targetSource:
    | "step2_logical_entity_definition"
    | "step2_5_relation_definition";
  targetId: string;
  label: string;
};
export type AggregateTriggerTargetBindingPayload = {
  target_source: StepTarget["targetSource"];
  target_id: string;
};
export type AggregateTriggerDefinitionPayload = {
  trigger_definition_id: string;
  trigger_source: {
    canonical_trigger_kind: typeof canonicalTriggerKinds[number];
    trigger_source_detail_kind: typeof triggerSourceDetailKinds[number];
  };
  processing_function_scope: {
    function_id: string;
    operation_definition_id: string;
    accepted_event_schema_ref: string;
    allowed_source_kinds: string[];
    materialization_policy_ref: string;
  };
  execution_scope: string;
  transaction_boundary: string;
  aggregate_target_binding: AggregateTriggerTargetBindingPayload;
  conflict_key_fields: string[];
  delta_map: Record<string, number>;
  threshold_policy: {
    minimum_trial_count: number;
    ratio_numerator_field: string;
    ratio_denominator_field: string;
    comparison_operator: typeof comparisonOperatorAllowedValues[number];
    target_ratio: number;
  };
  materialization_target_binding: AggregateTriggerTargetBindingPayload;
  materialization_payload_map: Array<{
    target_field: string;
    source: typeof materializationPayloadMapAllowedSources[number];
    source_field?: string;
    constant_value?: unknown;
  }>;
  approval_policy: typeof approvalPolicyAllowedValues[number];
  evidence_policy?: string;
};

export function aggregateTriggerTargetOptions(
  step2LogicalEntityDefinitions: StepTarget[],
  step25RelationDefinitions: StepTarget[],
): StepTarget[] {
  return [...step2LogicalEntityDefinitions, ...step25RelationDefinitions]
    .filter((target) =>
      (target.targetSource === "step2_logical_entity_definition" ||
        target.targetSource === "step2_5_relation_definition") &&
      target.targetId.trim().length > 0
    );
}

export function aggregateTriggerTargetKey(target: StepTarget): string {
  return `${target.targetSource}:${target.targetId}`;
}

export function aggregateTriggerTargetFromKey(
  targets: StepTarget[],
  key: string,
): StepTarget | null {
  return targets.find((target) => aggregateTriggerTargetKey(target) === key) ??
    null;
}

function toTargetBinding(
  target: StepTarget,
): AggregateTriggerTargetBindingPayload {
  return { target_source: target.targetSource, target_id: target.targetId };
}

export function buildAggregateTriggerDefinition(input: {
  triggerDefinitionId: string;
  canonicalTriggerKind: typeof canonicalTriggerKinds[number];
  triggerSourceDetailKind: typeof triggerSourceDetailKinds[number];
  executionScope?: typeof executionScopeAllowedValues[number];
  transactionBoundary?: typeof transactionBoundaryAllowedValues[number];
  approvalPolicy?: typeof approvalPolicyAllowedValues[number];
  aggregateTargetBinding: StepTarget;
  materializationTargetBinding: StepTarget;
  functionId: string;
  operationDefinitionId: string;
  acceptedEventSchemaRef: string;
  materializationPolicyRef: string;
  conflictKeyFields: string[];
  deltaMap: Record<string, number>;
  thresholdPolicy: AggregateTriggerDefinitionPayload["threshold_policy"];
  materializationPayloadMap:
    AggregateTriggerDefinitionPayload["materialization_payload_map"];
}): AggregateTriggerDefinitionPayload {
  return {
    trigger_definition_id: input.triggerDefinitionId,
    trigger_source: {
      canonical_trigger_kind: input.canonicalTriggerKind,
      trigger_source_detail_kind: input.triggerSourceDetailKind,
    },
    processing_function_scope: {
      function_id: input.functionId,
      operation_definition_id: input.operationDefinitionId,
      accepted_event_schema_ref: input.acceptedEventSchemaRef,
      allowed_source_kinds: [...triggerSourceDetailKinds],
      materialization_policy_ref: input.materializationPolicyRef,
    },
    execution_scope: input.executionScope ?? executionScopeAllowedValues[0],
    transaction_boundary: input.transactionBoundary ??
      transactionBoundaryAllowedValues[0],
    aggregate_target_binding: toTargetBinding(input.aggregateTargetBinding),
    conflict_key_fields: input.conflictKeyFields,
    delta_map: input.deltaMap,
    threshold_policy: input.thresholdPolicy,
    materialization_target_binding: toTargetBinding(
      input.materializationTargetBinding,
    ),
    materialization_payload_map: input.materializationPayloadMap,
    approval_policy: input.approvalPolicy ?? approvalPolicyAllowedValues[0],
    evidence_policy: "structured_authoring_preview_only",
  };
}

export function previewAggregateTriggerDefinition(input: {
  canonicalTriggerKind: typeof canonicalTriggerKinds[number];
  triggerSourceDetailKind: typeof triggerSourceDetailKinds[number];
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
