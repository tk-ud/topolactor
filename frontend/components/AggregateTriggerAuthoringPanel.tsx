import { JSX } from "preact";
import { useEffect } from "preact/hooks";
import {
  type AggregateTriggerDefinitionPayload,
  aggregateTriggerTargetOptions,
  approvalPolicyAllowedValues,
  buildAggregateTriggerDefinition,
  canonicalTriggerKinds,
  comparisonOperatorAllowedValues,
  executionScopeAllowedValues,
  materializationPayloadMapAllowedSources,
  type StepTarget,
  transactionBoundaryAllowedValues,
  triggerSourceDetailKinds,
} from "../lib/aggregateTriggerAuthoring.ts";

type Props = {
  step2LogicalEntityDefinitions: StepTarget[];
  step25RelationDefinitions: StepTarget[];
  onPayloadChange?: (payload: AggregateTriggerDefinitionPayload[]) => void;
};

const FIRST = 0;

export default function AggregateTriggerAuthoringPanel({
  step2LogicalEntityDefinitions,
  step25RelationDefinitions,
  onPayloadChange,
}: Props): JSX.Element {
  const targets = aggregateTriggerTargetOptions(
    step2LogicalEntityDefinitions,
    step25RelationDefinitions,
  );
  const aggregateTarget = targets[FIRST];
  const materializationTarget = targets[1] ?? targets[FIRST];
  const payload = aggregateTarget && materializationTarget
    ? [buildAggregateTriggerDefinition({
      triggerDefinitionId: "00000000-0000-0000-0000-000000000001",
      canonicalTriggerKind: "client",
      triggerSourceDetailKind: "client_operation_event",
      aggregateTargetBinding: aggregateTarget,
      materializationTargetBinding: materializationTarget,
      operationDefinitionId: "contents_step3_operation",
      functionId: "aggregate_trigger_authoring_function",
      acceptedEventSchemaRef: "contents.step3.aggregate_trigger.event.v1",
      materializationPolicyRef: "backend_runtime_authority_required",
      conflictKeyFields: ["operation_definition_id"],
      deltaMap: { event_count: 1 },
      thresholdPolicy: {
        minimum_trial_count: 1,
        ratio_numerator_field: "event_count",
        ratio_denominator_field: "event_count",
        comparison_operator: ">=",
        target_ratio: 1,
      },
      materializationPayloadMap: [
        {
          target_field: "operation_definition_id",
          source: "function_input_event",
          source_field: "operation_definition_id",
        },
      ],
    })]
    : [];
  useEffect(() => {
    onPayloadChange?.(payload);
  }, [JSON.stringify(payload)]);

  return (
    <section
      class="mb-4 rounded border border-indigo-200 bg-indigo-50 p-3 text-xs"
      data-ui-assurance-component="aggregate-trigger-step3-authoring"
      data-ui-assurance-boundary="structured-authoring-preview-only"
    >
      <h3 class="text-sm font-semibold text-indigo-950">
        Aggregate trigger structured authoring
      </h3>
      <p class="mt-1 text-indigo-900">
        Step 3 は structured payload の選択と preview
        のみを担当します。threshold / materialization / approval の final
        judgment は backend runtime authority です。
      </p>

      <div class="mt-3 grid gap-2 sm:grid-cols-2">
        <label class="block">
          canonical trigger kind
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            aria-label="aggregate canonical trigger kind"
            value="client"
          >
            {canonicalTriggerKinds.map((kind) => (
              <option key={kind} value={kind}>{kind}</option>
            ))}
          </select>
        </label>
        <label class="block">
          trigger_source_detail_kind
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            aria-label="aggregate trigger source detail kind"
            value="client_operation_event"
          >
            {triggerSourceDetailKinds.map((kind) => (
              <option key={kind} value={kind}>{kind}</option>
            ))}
          </select>
        </label>
        <label class="block">
          aggregate target binding
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            aria-label="aggregate target binding"
            value={aggregateTarget
              ? `${aggregateTarget.targetSource}:${aggregateTarget.targetId}`
              : ""}
          >
            {targets.map((target) => (
              <option
                key={`${target.targetSource}:${target.targetId}`}
                value={`${target.targetSource}:${target.targetId}`}
              >
                {target.label}
              </option>
            ))}
          </select>
        </label>
        <label class="block">
          materialization target binding
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            aria-label="aggregate materialization target binding"
            value={materializationTarget
              ? `${materializationTarget.targetSource}:${materializationTarget.targetId}`
              : ""}
          >
            {targets.map((target) => (
              <option
                key={`${target.targetSource}:${target.targetId}`}
                value={`${target.targetSource}:${target.targetId}`}
              >
                {target.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      <details class="mt-3 rounded border border-indigo-100 bg-white p-2" open>
        <summary class="cursor-pointer font-semibold">
          structured payload preview
        </summary>
        <pre
          class="mt-2 overflow-auto rounded bg-slate-950 p-2 text-[10px] text-slate-50"
          data-ui-assurance-payload="aggregate-trigger-step3"
        >{JSON.stringify(payload, null, 2)}</pre>
      </details>

      <p class="mt-2 text-[10px] text-indigo-900">
        allowed sources: {materializationPayloadMapAllowedSources.join(", ")}
        {" "}
        / execution_scope: {executionScopeAllowedValues[0]}{" "}
        / transaction_boundary: {transactionBoundaryAllowedValues[0]}{" "}
        / approval_policy: {approvalPolicyAllowedValues[0]} / comparison:{" "}
        {comparisonOperatorAllowedValues.join(" ")}
      </p>
    </section>
  );
}
