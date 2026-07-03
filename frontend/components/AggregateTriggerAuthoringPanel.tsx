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
  isSafeAggregateTriggerIdentifier,
  materializationPayloadMapAllowedSources,
  type StepTarget,
  transactionBoundaryAllowedValues,
  triggerSourceDetailKinds,
} from "../lib/aggregateTriggerAuthoring.ts";

type Props = {
  step2LogicalEntityDefinitions: StepTarget[];
  step25RelationDefinitions: StepTarget[];
  topologySystemName?: string;
  onPayloadChange?: (payload: AggregateTriggerDefinitionPayload[]) => void;
};

type DeltaMapRow = { name: string; amount: number };
type PayloadMapRow = {
  targetField: string;
  source: typeof materializationPayloadMapAllowedSources[number];
  sourceField: string;
  constantValue: string;
};

const generatedValueOptions = [
  "uuid",
  "current_timestamp",
  "monotonic_sequence_within_scope",
] as const;

export default function AggregateTriggerAuthoringPanel({
  step2LogicalEntityDefinitions,
  step25RelationDefinitions,
  topologySystemName,
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
  const allDeclaredFields = useMemo(
    () =>
      Array.from(new Set(targets.flatMap((target) => target.fields ?? []))),
    [targets],
  );
  const step2Fields = useMemo(
    () =>
      Array.from(
        new Set(step2LogicalEntityDefinitions.flatMap((t) => t.fields ?? [])),
      ),
    [step2LogicalEntityDefinitions],
  );
  const step25Fields = useMemo(
    () =>
      Array.from(
        new Set(step25RelationDefinitions.flatMap((t) => t.fields ?? [])),
      ),
    [step25RelationDefinitions],
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
  const [functionId, setFunctionId] = useState(
    "aggregate_trigger_authoring_function",
  );
  // operation_definition_id is not authored here: AdminRuntime.assign_screen_data_shape
  // derives it from the manifest's topologySystemName (admin/contents Step1) and overrides
  // any submitted value, per runtime-orchestration-ssot.yaml
  // aggregate_trigger_substrate_contract.processing_function_scope.operation_definition_id_authority_binding.
  const operationDefinitionId = (topologySystemName ?? "").trim();
  const [conflictKeyFields, setConflictKeyFields] = useState<string[]>([]);
  const [deltaMapRows, setDeltaMapRows] = useState<DeltaMapRow[]>([
    { name: "event_count", amount: 1 },
  ]);
  const [minimumTrialCount, setMinimumTrialCount] = useState(1);
  const [ratioNumeratorField, setRatioNumeratorField] = useState(
    "event_count",
  );
  const [ratioDenominatorField, setRatioDenominatorField] = useState(
    "event_count",
  );
  const [comparisonOperator, setComparisonOperator] = useState<
    typeof comparisonOperatorAllowedValues[number]
  >(">=");
  const [targetRatio, setTargetRatio] = useState(1);
  const [payloadMapRows, setPayloadMapRows] = useState<PayloadMapRow[]>([]);

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

  const aggregateTargetFields = aggregateTarget?.fields ?? [];
  const materializationTargetFields = materializationTarget?.fields ?? [];
  const deltaMapFieldNames = deltaMapRows
    .map((r) => r.name.trim())
    .filter((n) => n.length > 0);

  const functionIdValid = isSafeAggregateTriggerIdentifier(functionId);
  const operationDefinitionIdValid = operationDefinitionId.length > 0 &&
    isSafeAggregateTriggerIdentifier(operationDefinitionId);
  const deltaMapValid = deltaMapRows.every((r) =>
    isSafeAggregateTriggerIdentifier(r.name)
  );
  const payloadMapValid = payloadMapRows.every((r) =>
    isSafeAggregateTriggerIdentifier(r.targetField) &&
    (r.source === "constant" || isSafeAggregateTriggerIdentifier(r.sourceField))
  );
  const structuredInputsValid = functionIdValid &&
    operationDefinitionIdValid && deltaMapValid && payloadMapValid &&
    conflictKeyFields.length > 0 && payloadMapRows.length > 0;

  const deltaMap = Object.fromEntries(
    deltaMapRows
      .filter((r) => r.name.trim().length > 0)
      .map((r) => [r.name.trim(), r.amount]),
  );
  const materializationPayloadMap = payloadMapRows.map((row) => ({
    target_field: row.targetField,
    source: row.source,
    ...(row.source === "constant"
      ? { constant_value: row.constantValue }
      : { source_field: row.sourceField }),
  }));

  const payload = aggregateTarget && materializationTarget &&
      structuredInputsValid
    ? [buildAggregateTriggerDefinition({
      triggerDefinitionId: "00000000-0000-0000-0000-000000000001",
      canonicalTriggerKind,
      triggerSourceDetailKind,
      executionScope,
      transactionBoundary,
      approvalPolicy,
      aggregateTargetBinding: aggregateTarget,
      materializationTargetBinding: materializationTarget,
      operationDefinitionId,
      functionId,
      acceptedEventSchemaRef: "contents.step3.aggregate_trigger.event.v1",
      materializationPolicyRef: "backend_runtime_authority_required",
      conflictKeyFields,
      deltaMap,
      thresholdPolicy: {
        minimum_trial_count: minimumTrialCount,
        ratio_numerator_field: ratioNumeratorField,
        ratio_denominator_field: ratioDenominatorField,
        comparison_operator: comparisonOperator,
        target_ratio: targetRatio,
      },
      materializationPayloadMap,
    })]
    : [];

  useEffect(() => {
    onPayloadChange?.(payload);
  }, [JSON.stringify(payload)]);

  // Reset dependent selections when the aggregate target changes so stale
  // field references from a previously selected target are never retained.
  useEffect(() => {
    setConflictKeyFields((prev) =>
      prev.filter((f) => aggregateTargetFields.includes(f))
    );
  }, [aggregateTargetKey]);

  useEffect(() => {
    if (!deltaMapFieldNames.includes(ratioNumeratorField)) {
      setRatioNumeratorField(deltaMapFieldNames[0] ?? "");
    }
    if (!deltaMapFieldNames.includes(ratioDenominatorField)) {
      setRatioDenominatorField(deltaMapFieldNames[0] ?? "");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [JSON.stringify(deltaMapFieldNames)]);

  const toggleConflictKeyField = (field: string) => {
    setConflictKeyFields((prev) =>
      prev.includes(field)
        ? prev.filter((f) => f !== field)
        : [...prev, field]
    );
  };

  const updateDeltaMapRow = (index: number, patch: Partial<DeltaMapRow>) => {
    setDeltaMapRows((prev) =>
      prev.map((row, i) => i === index ? { ...row, ...patch } : row)
    );
  };
  const addDeltaMapRow = () =>
    setDeltaMapRows((prev) => [...prev, { name: "", amount: 1 }]);
  const removeDeltaMapRow = (index: number) =>
    setDeltaMapRows((prev) => prev.filter((_, i) => i !== index));

  const updatePayloadMapRow = (
    index: number,
    patch: Partial<PayloadMapRow>,
  ) => {
    setPayloadMapRows((prev) =>
      prev.map((row, i) => i === index ? { ...row, ...patch } : row)
    );
  };
  const addPayloadMapRow = () =>
    setPayloadMapRows((prev) => [...prev, {
      targetField: materializationTargetFields[0] ?? "",
      source: "function_input_event",
      sourceField: "",
      constantValue: "",
    }]);
  const removePayloadMapRow = (index: number) =>
    setPayloadMapRows((prev) => prev.filter((_, i) => i !== index));

  /** Field picker bound to the SSOT-declared source for the given materialization_payload_map entry's `source`. */
  const sourceFieldOptionsFor = (
    source: typeof materializationPayloadMapAllowedSources[number],
  ): string[] => {
    switch (source) {
      case "aggregate_current_row":
        return deltaMapFieldNames;
      case "selected_step2_entity_fields":
        return step2Fields;
      case "selected_step2_5_relation_fields":
        return step25Fields;
      case "generated_value":
        return [...generatedValueOptions];
      default:
        return [];
    }
  };

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
        <label class="block">
          processing_function_scope.function_id
          <input
            class={`mt-1 w-full rounded border px-2 py-1 font-mono ${
              functionIdValid ? "" : "border-red-400"
            }`}
            aria-label="aggregate processing function id"
            value={functionId}
            onInput={(e) =>
              setFunctionId((e.target as HTMLInputElement).value)}
          />
        </label>
        <label class="block">
          processing_function_scope.operation_definition_id
          <input
            class={`mt-1 w-full rounded border bg-slate-50 px-2 py-1 font-mono text-slate-600 ${
              operationDefinitionIdValid ? "" : "border-red-400"
            }`}
            aria-label="aggregate operation definition id"
            value={operationDefinitionId}
            readOnly
            disabled
          />
          <p class="mt-1 text-xs text-slate-500">
            admin/contents Step1 の topologySystemName から backend
            が導出する値です（ここでは編集できません）。
          </p>
        </label>
      </div>

      <fieldset class="mt-3 rounded border border-indigo-100 bg-white p-2">
        <legend class="px-1 font-semibold">
          conflict_key_fields (from aggregate target)
        </legend>
        {aggregateTargetFields.length === 0
          ? (
            <p class="text-slate-500">
              aggregate target に選択可能な field がありません。
            </p>
          )
          : (
            <div class="flex flex-wrap gap-2">
              {aggregateTargetFields.map((field) => (
                <label key={field} class="flex items-center gap-1">
                  <input
                    type="checkbox"
                    aria-label={`conflict key field ${field}`}
                    checked={conflictKeyFields.includes(field)}
                    onChange={() => toggleConflictKeyField(field)}
                  />
                  {field}
                </label>
              ))}
            </div>
          )}
      </fieldset>

      <fieldset class="mt-3 rounded border border-indigo-100 bg-white p-2">
        <legend class="px-1 font-semibold">
          delta_map (event-to-aggregate counters)
        </legend>
        {deltaMapRows.map((row, index) => (
          <div key={index} class="mt-1 flex items-center gap-2">
            <input
              class={`w-1/2 rounded border px-2 py-1 font-mono ${
                isSafeAggregateTriggerIdentifier(row.name)
                  ? ""
                  : "border-red-400"
              }`}
              aria-label={`delta map field name ${index}`}
              placeholder="counter name"
              value={row.name}
              onInput={(e) =>
                updateDeltaMapRow(index, {
                  name: (e.target as HTMLInputElement).value,
                })}
            />
            <input
              type="number"
              class="w-1/4 rounded border px-2 py-1 font-mono"
              aria-label={`delta map amount ${index}`}
              value={row.amount}
              onInput={(e) =>
                updateDeltaMapRow(index, {
                  amount: Number((e.target as HTMLInputElement).value),
                })}
            />
            <button
              type="button"
              class="rounded border px-2 py-1 text-slate-600"
              onClick={() => removeDeltaMapRow(index)}
            >
              削除
            </button>
          </div>
        ))}
        <button
          type="button"
          class="mt-2 rounded border px-2 py-1 text-indigo-700"
          onClick={addDeltaMapRow}
        >
          + counter を追加
        </button>
      </fieldset>

      <fieldset class="mt-3 rounded border border-indigo-100 bg-white p-2">
        <legend class="px-1 font-semibold">threshold_policy</legend>
        <div class="grid gap-2 sm:grid-cols-2">
          <label class="block">
            minimum_trial_count
            <input
              type="number"
              min={1}
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              aria-label="threshold minimum trial count"
              value={minimumTrialCount}
              onInput={(e) =>
                setMinimumTrialCount(
                  Number((e.target as HTMLInputElement).value),
                )}
            />
          </label>
          <label class="block">
            target_ratio
            <input
              type="number"
              step="0.01"
              min={0}
              max={1}
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              aria-label="threshold target ratio"
              value={targetRatio}
              onInput={(e) =>
                setTargetRatio(Number((e.target as HTMLInputElement).value))}
            />
          </label>
          <label class="block">
            ratio_numerator_field
            <select
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              aria-label="threshold ratio numerator field"
              value={ratioNumeratorField}
              onInput={(e) =>
                setRatioNumeratorField(
                  (e.target as HTMLSelectElement).value,
                )}
              onChange={(e) =>
                setRatioNumeratorField(
                  (e.target as HTMLSelectElement).value,
                )}
            >
              {deltaMapFieldNames.map((field) => (
                <option key={field} value={field}>{field}</option>
              ))}
            </select>
          </label>
          <label class="block">
            ratio_denominator_field
            <select
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              aria-label="threshold ratio denominator field"
              value={ratioDenominatorField}
              onInput={(e) =>
                setRatioDenominatorField(
                  (e.target as HTMLSelectElement).value,
                )}
              onChange={(e) =>
                setRatioDenominatorField(
                  (e.target as HTMLSelectElement).value,
                )}
            >
              {deltaMapFieldNames.map((field) => (
                <option key={field} value={field}>{field}</option>
              ))}
            </select>
          </label>
          <label class="block">
            comparison_operator
            <select
              class="mt-1 w-full rounded border px-2 py-1 font-mono"
              aria-label="threshold comparison operator"
              value={comparisonOperator}
              onInput={(e) =>
                setComparisonOperator(
                  (e.target as HTMLSelectElement)
                    .value as typeof comparisonOperatorAllowedValues[number],
                )}
              onChange={(e) =>
                setComparisonOperator(
                  (e.target as HTMLSelectElement)
                    .value as typeof comparisonOperatorAllowedValues[number],
                )}
            >
              {comparisonOperatorAllowedValues.map((op) => (
                <option key={op} value={op}>{op}</option>
              ))}
            </select>
          </label>
        </div>
      </fieldset>

      <fieldset class="mt-3 rounded border border-indigo-100 bg-white p-2">
        <legend class="px-1 font-semibold">
          materialization_payload_map (target: {materializationTargetFields
            .length > 0
            ? "select below"
            : "no fields available"})
        </legend>
        {payloadMapRows.map((row, index) => {
          const options = sourceFieldOptionsFor(row.source);
          return (
            <div
              key={index}
              class="mt-2 grid gap-2 rounded border border-indigo-50 p-2 sm:grid-cols-2"
            >
              <label class="block">
                target_field
                <select
                  class="mt-1 w-full rounded border px-2 py-1 font-mono"
                  aria-label={`materialization payload target field ${index}`}
                  value={row.targetField}
                  onInput={(e) =>
                    updatePayloadMapRow(index, {
                      targetField: (e.target as HTMLSelectElement).value,
                    })}
                  onChange={(e) =>
                    updatePayloadMapRow(index, {
                      targetField: (e.target as HTMLSelectElement).value,
                    })}
                >
                  {materializationTargetFields.map((field) => (
                    <option key={field} value={field}>{field}</option>
                  ))}
                </select>
              </label>
              <label class="block">
                source
                <select
                  class="mt-1 w-full rounded border px-2 py-1 font-mono"
                  aria-label={`materialization payload source ${index}`}
                  value={row.source}
                  onInput={(e) =>
                    updatePayloadMapRow(index, {
                      source: (e.target as HTMLSelectElement)
                        .value as typeof materializationPayloadMapAllowedSources[
                          number
                        ],
                      sourceField: "",
                    })}
                  onChange={(e) =>
                    updatePayloadMapRow(index, {
                      source: (e.target as HTMLSelectElement)
                        .value as typeof materializationPayloadMapAllowedSources[
                          number
                        ],
                      sourceField: "",
                    })}
                >
                  {materializationPayloadMapAllowedSources.map((source) => (
                    <option key={source} value={source}>{source}</option>
                  ))}
                </select>
              </label>
              {row.source === "constant"
                ? (
                  <label class="block sm:col-span-2">
                    constant_value
                    <input
                      class="mt-1 w-full rounded border px-2 py-1 font-mono"
                      aria-label={`materialization payload constant value ${index}`}
                      value={row.constantValue}
                      onInput={(e) =>
                        updatePayloadMapRow(index, {
                          constantValue: (e.target as HTMLInputElement).value,
                        })}
                    />
                  </label>
                )
                : options.length > 0
                ? (
                  <label class="block sm:col-span-2">
                    source_field
                    <select
                      class="mt-1 w-full rounded border px-2 py-1 font-mono"
                      aria-label={`materialization payload source field ${index}`}
                      value={row.sourceField}
                      onInput={(e) =>
                        updatePayloadMapRow(index, {
                          sourceField: (e.target as HTMLSelectElement).value,
                        })}
                      onChange={(e) =>
                        updatePayloadMapRow(index, {
                          sourceField: (e.target as HTMLSelectElement).value,
                        })}
                    >
                      <option value="">(select)</option>
                      {options.map((opt) => (
                        <option key={opt} value={opt}>{opt}</option>
                      ))}
                    </select>
                  </label>
                )
                : (
                  <label class="block sm:col-span-2">
                    source_field
                    <input
                      class={`mt-1 w-full rounded border px-2 py-1 font-mono ${
                        isSafeAggregateTriggerIdentifier(row.sourceField)
                          ? ""
                          : "border-red-400"
                      }`}
                      aria-label={`materialization payload source field ${index}`}
                      placeholder="declared event schema path"
                      value={row.sourceField}
                      onInput={(e) =>
                        updatePayloadMapRow(index, {
                          sourceField: (e.target as HTMLInputElement).value,
                        })}
                    />
                  </label>
                )}
              <button
                type="button"
                class="rounded border px-2 py-1 text-slate-600 sm:col-span-2"
                onClick={() => removePayloadMapRow(index)}
              >
                この行を削除
              </button>
            </div>
          );
        })}
        <button
          type="button"
          class="mt-2 rounded border px-2 py-1 text-indigo-700"
          onClick={addPayloadMapRow}
          disabled={materializationTargetFields.length === 0}
        >
          + materialization payload entry を追加
        </button>
        {payloadMapRows.length === 0 && (
          <p class="mt-1 text-amber-900">
            materialization_payload_map に最低1件のエントリが必要です。
          </p>
        )}
      </fieldset>

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
        {allDeclaredFields.length === 0 && (
          <>
            {" "}/ 警告: Step2/2.5
            対象に列定義がまだ無いため、conflict_key_fields /
            materialization_payload_map を構成できません。
          </>
        )}
      </p>
    </section>
  );
}
