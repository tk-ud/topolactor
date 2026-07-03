import { JSX } from "preact";
import { useEffect, useMemo, useState } from "preact/hooks";
import {
  type AggregateTriggerDefinitionPayload,
  aggregateTriggerTargetFromKey,
  aggregateTriggerTargetKey,
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

export default function AggregateTriggerAuthoringPanel({
  step2LogicalEntityDefinitions,
  step25RelationDefinitions,
  onPayloadChange,
}: Props): JSX.Element {
  const targets = useMemo(
    () =>
      aggregateTriggerTargetOptions(
        step2LogicalEntityDefinitions,
        step25RelationDefinitions,
      ),
    [step2LogicalEntityDefinitions, step25RelationDefinitions],
  );
  const defaultAggregateKey = targets[0]
    ? aggregateTriggerTargetKey(targets[0])
    : "";
  const defaultMaterializationKey = targets[1]
    ? aggregateTriggerTargetKey(targets[1])
    : defaultAggregateKey;
  const [canonicalTriggerKind, setCanonicalTriggerKind] = useState<
    typeof canonicalTriggerKinds[number]
  >("client");
  const [triggerSourceDetailKind, setTriggerSourceDetailKind] = useState<
    typeof triggerSourceDetailKinds[number]
  >("client_operation_event");
  const [executionScope, setExecutionScope] = useState<
    typeof executionScopeAllowedValues[number]
  >(executionScopeAllowedValues[0]);
  const [transactionBoundary, setTransactionBoundary] = useState<
    typeof transactionBoundaryAllowedValues[number]
  >(transactionBoundaryAllowedValues[0]);
  const [approvalPolicy, setApprovalPolicy] = useState<
    typeof approvalPolicyAllowedValues[number]
  >(approvalPolicyAllowedValues[0]);
  const [aggregateTargetKey, setAggregateTargetKey] = useState(
    defaultAggregateKey,
  );
  const [materializationTargetKey, setMaterializationTargetKey] = useState(
    defaultMaterializationKey,
  );

  const aggregateTarget = aggregateTriggerTargetFromKey(
    targets,
    aggregateTargetKey,
  );
  const materializationTarget = aggregateTriggerTargetFromKey(
    targets,
    materializationTargetKey,
  );
  const hasInvalidTargetSelection = targets.length > 0 &&
    (!aggregateTarget || !materializationTarget);
  const payload = aggregateTarget && materializationTarget
    ? [buildAggregateTriggerDefinition({
      triggerDefinitionId: "00000000-0000-0000-0000-000000000001",
      canonicalTriggerKind,
      triggerSourceDetailKind,
      executionScope,
      transactionBoundary,
      approvalPolicy,
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

      {(targets.length === 0 || hasInvalidTargetSelection) && (
        <p
          class="mt-2 rounded border border-amber-200 bg-amber-50 p-2 text-amber-900"
          role="alert"
        >
          {targets.length === 0
            ? "Step2 logical entity または Step2.5 relation の保存済み対象がないため、aggregate trigger payload は作成されません。"
            : "未定義targetが選択されたため、aggregate trigger payload は作成されません。"}
        </p>
      )}

      <div class="mt-3 grid gap-2 sm:grid-cols-2">
        <label class="block">
          canonical trigger kind
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            aria-label="aggregate canonical trigger kind"
            value={canonicalTriggerKind}
            onInput={(e) =>
              setCanonicalTriggerKind(
                (e.target as HTMLSelectElement)
                  .value as typeof canonicalTriggerKinds[number],
              )}
            onChange={(e) =>
              setCanonicalTriggerKind(
                (e.target as HTMLSelectElement)
                  .value as typeof canonicalTriggerKinds[number],
              )}
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
            value={triggerSourceDetailKind}
            onInput={(e) =>
              setTriggerSourceDetailKind(
                (e.target as HTMLSelectElement)
                  .value as typeof triggerSourceDetailKinds[number],
              )}
            onChange={(e) =>
              setTriggerSourceDetailKind(
                (e.target as HTMLSelectElement)
                  .value as typeof triggerSourceDetailKinds[number],
              )}
          >
            {triggerSourceDetailKinds.map((kind) => (
              <option key={kind} value={kind}>{kind}</option>
            ))}
          </select>
        </label>
        <label class="block">
          execution_scope
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            aria-label="aggregate execution scope"
            value={executionScope}
            onInput={(e) =>
              setExecutionScope(
                (e.target as HTMLSelectElement)
                  .value as typeof executionScopeAllowedValues[number],
              )}
            onChange={(e) =>
              setExecutionScope(
                (e.target as HTMLSelectElement)
                  .value as typeof executionScopeAllowedValues[number],
              )}
          >
            {executionScopeAllowedValues.map((scope) => (
              <option key={scope} value={scope}>{scope}</option>
            ))}
          </select>
        </label>
        <label class="block">
          transaction_boundary
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            aria-label="aggregate transaction boundary"
            value={transactionBoundary}
            onInput={(e) =>
              setTransactionBoundary(
                (e.target as HTMLSelectElement)
                  .value as typeof transactionBoundaryAllowedValues[number],
              )}
            onChange={(e) =>
              setTransactionBoundary(
                (e.target as HTMLSelectElement)
                  .value as typeof transactionBoundaryAllowedValues[number],
              )}
          >
            {transactionBoundaryAllowedValues.map((boundary) => (
              <option key={boundary} value={boundary}>{boundary}</option>
            ))}
          </select>
        </label>
        <label class="block">
          approval_policy
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            aria-label="aggregate approval policy"
            value={approvalPolicy}
            onInput={(e) =>
              setApprovalPolicy(
                (e.target as HTMLSelectElement)
                  .value as typeof approvalPolicyAllowedValues[number],
              )}
            onChange={(e) =>
              setApprovalPolicy(
                (e.target as HTMLSelectElement)
                  .value as typeof approvalPolicyAllowedValues[number],
              )}
          >
            {approvalPolicyAllowedValues.map((policy) => (
              <option key={policy} value={policy}>{policy}</option>
            ))}
          </select>
        </label>
        <label class="block">
          aggregate target binding
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            aria-label="aggregate target binding"
            value={aggregateTarget
              ? aggregateTriggerTargetKey(aggregateTarget)
              : ""}
            onInput={(e) =>
              setAggregateTargetKey((e.target as HTMLSelectElement).value)}
            onChange={(e) =>
              setAggregateTargetKey((e.target as HTMLSelectElement).value)}
          >
            {targets.map((target) => (
              <option
                key={aggregateTriggerTargetKey(target)}
                value={aggregateTriggerTargetKey(target)}
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
              ? aggregateTriggerTargetKey(materializationTarget)
              : ""}
            onInput={(e) =>
              setMaterializationTargetKey(
                (e.target as HTMLSelectElement).value,
              )}
            onChange={(e) =>
              setMaterializationTargetKey(
                (e.target as HTMLSelectElement).value,
              )}
          >
            {targets.map((target) => (
              <option
                key={aggregateTriggerTargetKey(target)}
                value={aggregateTriggerTargetKey(target)}
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
        / execution_scope: {executionScope}{" "}
        / transaction_boundary: {transactionBoundary}{" "}
        / approval_policy: {approvalPolicy} / comparison:{" "}
        {comparisonOperatorAllowedValues.join(" ")}
      </p>
    </section>
  );
}
