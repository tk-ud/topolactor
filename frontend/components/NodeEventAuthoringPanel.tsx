import { useState } from "preact/hooks";
import { JSX } from "preact";
import {
  UX_EXTERNAL_INTEGRATION_SECTION_HINT,
  UX_EXTERNAL_INTEGRATION_SECTION_TITLE,
  UX_EXTERNAL_PORT_EMPTY_HINT,
  UX_INSTANCE_OPERATION_EMPTY_HINT,
  UX_INSTANCE_OPERATION_SECTION_HINT,
  UX_INSTANCE_OPERATION_SECTION_TITLE,
  UX_RUNTIME_INTERACTION_ADD_EXTERNAL,
  UX_RUNTIME_INTERACTION_ADD_INSTANCE,
  UX_RUNTIME_INTERACTION_ADD_INSTANCE_COMMIT,
  UX_RUNTIME_INTERACTION_ADD_EXTERNAL_COMMIT,
  UX_RUNTIME_INTERACTION_ADD_OVERLAY,
  UX_RUNTIME_INTERACTION_EXTERNAL_PORT_SELECT,
  UX_RUNTIME_INTERACTION_INSTANCE_OPERATION_SELECT,
  UX_RUNTIME_INTERACTION_NO_OVERLAY_TARGETS,
  UX_RUNTIME_INTERACTION_SECTION_HINT,
  UX_RUNTIME_INTERACTION_SECTION_TITLE,
  UX_RUNTIME_INTERACTION_STAGING_CANCEL,
  UX_RUNTIME_INTERACTION_TRIGGER_UI,
  UX_RUNTIME_INTERACTION_WHAT,
  UX_TARGET_UI_LABEL,
  UX_TRIGGER_UI_LABEL,
} from "../content/adminUxTerms.ts";
import {
  applyOverlayIntentToInteraction,
  defaultExternalPortInteraction,
  defaultInstanceOperationInteraction,
  friendlyOverlayTargetLabel,
  listOverlayTargetNodes,
  type OverlayIntent,
  type OverlayTargetNode,
  overlayIntentFromAction,
  defaultOverlayOpenInteraction,
  isOverlayDisclosureAction,
  RUNTIME_INTERACTION_INTENT_OPTIONS,
  runtimeInteractionCategory,
  runtimeInteractionTriggerOptions,
} from "../lib/runtimeInteractionAuthoring.ts";
import {
  useExternalPortAuthoringCandidates,
  useInstanceOperationAuthoringCandidates,
  validatePayloadFromEntries,
} from "../lib/uiBuilderEventAuthoringHooks.ts";
import {
  deriveUiWatchBindings,
  isHighFrequencyTrigger,
  isLifecycleTrigger,
  LIFECYCLE_IDEMPOTENCY_POLICIES,
  selectableWriteTargets,
  type WiringNode,
} from "../lib/uiBuilderWiringProjection.ts";
import {
  UX_EXTERNAL_PORT_CREDENTIAL_AUTHORITY_LABEL,
  UX_WIRING_CATEGORY_SIDE_EFFECT,
  UX_WIRING_CATEGORY_UI_STATE_UPDATE,
  UX_WIRING_CATEGORY_UI_WATCH_BINDING,
  UX_WIRING_DEBOUNCE_LABEL,
  UX_WIRING_IDEMPOTENCY_LABEL,
  UX_WIRING_IDEMPOTENCY_OPTIONS,
  UX_WIRING_POLICY_HIGH_FREQUENCY_WARNING,
  UX_WIRING_POLICY_LIFECYCLE_CONFIRM_LABEL,
  UX_WIRING_POLICY_LIFECYCLE_WARNING,
  UX_WIRING_SIDE_EFFECT_NONE_LABEL,
  UX_WIRING_SIDE_EFFECT_SECTION_HINT,
  UX_WIRING_WATCH_BINDING_ADD,
  UX_WIRING_WATCH_BINDING_EMPTY,
  UX_WIRING_WATCH_BINDING_HINT,
} from "../content/adminUxTerms.ts";
import { parsePayloadFromSource } from "../runtime/payloadFromResolver.ts";

export type NodeEventWiring = {
  trigger: string;
  actionType: string;
  targetNodeId?: string;
  statePath?: string;
  eventType?: string;
  payloadFrom?: Record<string, string>;
  outputProp?: string;
  portTargetRef?: string;
  instanceTargetRef?: string;
  /** SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml high_frequency_policy */
  debounceMs?: number;
  /** SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml lifecycle_policy */
  lifecycleDispatchConfirmed?: boolean;
  /** SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml lifecycle_policy idempotency_policy_values */
  idempotencyPolicy?: string;
  /** SSOT: side_effect_setting — explicit no-side-effect selection. */
  sideEffectNone?: boolean;
};

type Props = {
  interactions: NodeEventWiring[];
  targetNodes: OverlayTargetNode[];
  triggerOptions: readonly string[];
  selectedNodeLabel?: string;
  onCommit: (next: NodeEventWiring[], label: string) => void;
  /** UI監視割当 (declaration category): selected node's stateJson slot source. */
  stateJson?: string;
  onCommitStateJson?: (nextStateJson: string | undefined, label: string) => void;
  /** side_effect_cycle_policy: full draft node list + trigger source for write-target filtering. */
  sourceNodeId?: string;
  allNodes?: readonly WiringNode[];
};

type StagingKind = "external" | "instance";

function isCompleteInteraction(w: NodeEventWiring): boolean {
  if (w.actionType === "dispatchExternalPort") {
    return Boolean(w.portTargetRef?.trim());
  }
  if (w.actionType === "dispatchInstanceOperation") {
    return Boolean(w.instanceTargetRef?.trim());
  }
  if (isOverlayDisclosureAction(w.actionType)) {
    return Boolean(w.targetNodeId?.trim());
  }
  return Boolean(w.targetNodeId?.trim());
}

export default function NodeEventAuthoringPanel({
  interactions,
  targetNodes,
  triggerOptions,
  selectedNodeLabel,
  onCommit,
  stateJson,
  onCommitStateJson,
  sourceNodeId,
  allNodes,
}: Props): JSX.Element {
  // side_effect_cycle_policy: selectable_write_targets =
  // all_write_targets - dependency_closure(trigger_source).
  const selectableTargetIds = sourceNodeId && allNodes
    ? new Set(selectableWriteTargets(allNodes, sourceNodeId))
    : null;
  const overlayTargets = listOverlayTargetNodes(targetNodes).filter((n) =>
    selectableTargetIds === null || selectableTargetIds.has(n.nodeId)
  );
  const [staging, setStaging] = useState<
    { kind: StagingKind; draft: NodeEventWiring } | null
  >(null);

  const needsExternalCandidates = staging?.kind === "external" ||
    interactions.some((w) => w.actionType === "dispatchExternalPort");
  const needsInstanceCandidates = staging?.kind === "instance" ||
    interactions.some((w) => w.actionType === "dispatchInstanceOperation");

  const { candidates: externalPortCandidates, error: externalPortCandidateError } =
    useExternalPortAuthoringCandidates(needsExternalCandidates);
  const { candidates: instanceOperationCandidates, error: instanceOperationCandidateError } =
    useInstanceOperationAuthoringCandidates(needsInstanceCandidates);

  const updateAt = (index: number, patch: Partial<NodeEventWiring>) => {
    const next = interactions.map((w, i) => i === index ? { ...w, ...patch } : w);
    onCommit(next, "操作イベントを更新");
  };

  const removeAt = (index: number) => {
    onCommit(
      interactions.filter((_, i) => i !== index),
      "操作イベントを削除",
    );
  };

  const commitStaging = () => {
    if (!staging || !isCompleteInteraction(staging.draft)) return;
    onCommit([...interactions, staging.draft], staging.kind === "external"
      ? UX_RUNTIME_INTERACTION_ADD_EXTERNAL_COMMIT
      : UX_RUNTIME_INTERACTION_ADD_INSTANCE_COMMIT);
    setStaging(null);
  };

  const renderPayloadFrom = (w: NodeEventWiring, index: number) => {
    const payloadFromEntries = Object.entries(w.payloadFrom ?? {});
    const payloadFromErrors = validatePayloadFromEntries(
      payloadFromEntries.map(([field, source]) => ({ field, source })),
    );
    return (
      <fieldset class="flex flex-col gap-1">
        <legend class="text-[0.65rem] font-semibold text-slate-800">
          データスコープ（payloadFrom）
        </legend>
        {payloadFromErrors.length > 0 && (
          <div class="rounded border border-amber-200 bg-amber-50 p-1">
            {payloadFromErrors.map((err, ei) => (
              <p key={ei} class="text-[0.55rem] text-amber-800">{err}</p>
            ))}
          </div>
        )}
        {payloadFromEntries.map(([field, source], pi) => (
          <div key={pi} class="flex items-center gap-1">
            <input
              class="input-mono flex-1 px-1 py-0.5 text-[0.6rem]"
              value={field}
              placeholder="フィールド名"
              onBlur={(e) => {
                const newField = (e.target as HTMLInputElement).value.trim();
                if (newField === field) return;
                const next: Record<string, string> = {};
                payloadFromEntries.forEach(([f, s], idx) => {
                  next[idx === pi ? newField : f] = s;
                });
                if (newField) updateAt(index, { payloadFrom: next });
              }}
            />
            <span class="text-slate-400">→</span>
            <input
              class={`input-mono flex-1 px-1 py-0.5 text-[0.6rem] ${
                parsePayloadFromSource(source).kind === "unresolved_ref"
                  ? "border-amber-400 bg-amber-50"
                  : ""
              }`}
              value={source}
              placeholder="node:<nodeId>.value"
              onInput={(e) => {
                const newSource = (e.target as HTMLInputElement).value;
                const next: Record<string, string> = {};
                payloadFromEntries.forEach(([f, s], idx) => {
                  next[f] = idx === pi ? newSource : s;
                });
                updateAt(index, {
                  payloadFrom: Object.keys(next).length > 0 ? next : undefined,
                });
              }}
            />
            <button
              type="button"
              class="text-[0.55rem] text-red-400 hover:underline"
              onClick={() => {
                const next: Record<string, string> = {};
                payloadFromEntries.forEach(([f, s], idx) => {
                  if (idx !== pi) next[f] = s;
                });
                updateAt(index, {
                  payloadFrom: Object.keys(next).length > 0 ? next : undefined,
                });
              }}
            >
              削除
            </button>
          </div>
        ))}
        <button
          type="button"
          class="self-start text-[0.6rem] text-indigo-600 hover:underline"
          onClick={() => {
            updateAt(index, { payloadFrom: { ...(w.payloadFrom ?? {}), "": "" } });
          }}
        >
          + フィールドを追加
        </button>
      </fieldset>
    );
  };

  /**
   * Lifecycle / high-frequency dispatch policy controls.
   * SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml lifecycle_policy / high_frequency_policy
   * — backend/external dispatch on these triggers fails close until the author
   * sets debounceMs (high-frequency) or explicitly confirms (lifecycle).
   */
  const renderDispatchPolicyControls = (
    w: NodeEventWiring,
    onUpdate: (patch: Partial<NodeEventWiring>) => void,
  ) => {
    const highFrequency = isHighFrequencyTrigger(w.trigger);
    const lifecycle = isLifecycleTrigger(w.trigger);
    if (!highFrequency && !lifecycle) return null;
    return (
      <div class="space-y-1 rounded border border-amber-200 bg-amber-50 p-1.5">
        {highFrequency && (
          <>
            <p class="text-[0.58rem] text-amber-900">{UX_WIRING_POLICY_HIGH_FREQUENCY_WARNING}</p>
            <label class="block text-[0.62rem]">
              {UX_WIRING_DEBOUNCE_LABEL}
              <input
                type="number"
                min={1}
                class="input-mono mt-0.5 w-full px-1 py-0.5 text-xs"
                value={w.debounceMs ?? ""}
                placeholder="例: 300"
                onInput={(e) => {
                  const raw = (e.target as HTMLInputElement).value.trim();
                  const parsed = Number.parseInt(raw, 10);
                  onUpdate({
                    debounceMs: raw && Number.isInteger(parsed) && parsed > 0 ? parsed : undefined,
                  });
                }}
              />
            </label>
          </>
        )}
        {lifecycle && (
          <>
            <p class="text-[0.58rem] text-amber-900">{UX_WIRING_POLICY_LIFECYCLE_WARNING}</p>
            <label class="flex items-start gap-1 text-[0.62rem] text-amber-900">
              <input
                type="checkbox"
                checked={w.lifecycleDispatchConfirmed === true}
                onChange={(e) =>
                  onUpdate({
                    lifecycleDispatchConfirmed: (e.target as HTMLInputElement).checked || undefined,
                  })}
              />
              <span>{UX_WIRING_POLICY_LIFECYCLE_CONFIRM_LABEL}</span>
            </label>
            <label class="block text-[0.62rem]">
              {UX_WIRING_IDEMPOTENCY_LABEL}
              <select
                class="input mt-0.5 w-full px-1 py-0.5 text-xs"
                value={w.idempotencyPolicy ?? ""}
                onChange={(e) => {
                  const v = (e.target as HTMLSelectElement).value;
                  onUpdate({
                    idempotencyPolicy: LIFECYCLE_IDEMPOTENCY_POLICIES.includes(v) ? v : undefined,
                  });
                }}
              >
                <option value="">— 選択してください —</option>
                {UX_WIRING_IDEMPOTENCY_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </label>
          </>
        )}
      </div>
    );
  };

  /**
   * 副作用設定 (side_effect_setting) — useEffect-like reaction settings for a
   * dispatch interaction. payloadFrom / outputProp are effect fields, not the
   * effect authority itself; explicit no-side-effect is selectable.
   * SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml ui_event_settings.side_effect_setting
   */
  const renderSideEffectSettings = (
    w: NodeEventWiring,
    index: number,
    onUpdate: (patch: Partial<NodeEventWiring>) => void,
  ) => (
    <fieldset class="rounded border border-slate-200 bg-white/70 p-1.5 space-y-1.5">
      <legend class="text-[0.65rem] font-semibold text-slate-800">
        {UX_WIRING_CATEGORY_SIDE_EFFECT}
      </legend>
      <p class="text-[0.55rem] text-slate-600">{UX_WIRING_SIDE_EFFECT_SECTION_HINT}</p>
      <label class="flex items-start gap-1 text-[0.62rem] text-slate-800">
        <input
          type="checkbox"
          checked={w.sideEffectNone === true}
          onChange={(e) => {
            const none = (e.target as HTMLInputElement).checked;
            onUpdate(
              none
                ? { sideEffectNone: true, payloadFrom: undefined, outputProp: undefined }
                : { sideEffectNone: undefined },
            );
          }}
        />
        <span>{UX_WIRING_SIDE_EFFECT_NONE_LABEL}</span>
      </label>
      {w.sideEffectNone !== true && (
        <>
          {renderPayloadFrom(w, index)}
          <label class="block text-[0.65rem]">
            結果受け取り先（outputProp）
            <input
              class="input-mono mt-0.5 w-full px-1 py-0.5 text-xs"
              value={w.outputProp ?? ""}
              placeholder="例: result"
              onInput={(e) =>
                onUpdate({ outputProp: (e.target as HTMLInputElement).value || undefined })}
            />
          </label>
        </>
      )}
    </fieldset>
  );

  const renderOverlayRow = (w: NodeEventWiring, index: number) => {
    const intent = overlayIntentFromAction(w.actionType);
    return (
      <div key={index} class="rounded border border-blue-100 bg-white p-2 space-y-2">
        <p class="text-[0.6rem] font-medium text-blue-900">
          {UX_RUNTIME_INTERACTION_TRIGGER_UI}: {selectedNodeLabel ?? "（選択中）"}
        </p>
        <div class="grid gap-2 md:grid-cols-3">
          <label class="text-[0.65rem]">
            {UX_TRIGGER_UI_LABEL}
            <select
              class="input mt-0.5 w-full px-1 py-0.5 text-xs"
              value={w.trigger ?? w.eventType ?? "click"}
              onChange={(e) =>
                updateAt(index, { trigger: (e.target as HTMLSelectElement).value })}
            >
              {runtimeInteractionTriggerOptions(triggerOptions).map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </label>
          <label class="text-[0.65rem]">
            {UX_RUNTIME_INTERACTION_WHAT}
            <select
              class="input mt-0.5 w-full px-1 py-0.5 text-xs"
              value={intent}
              onChange={(e) => {
                const nextIntent = (e.target as HTMLSelectElement).value as OverlayIntent;
                const target = targetNodes.find((n) => n.nodeId === w.targetNodeId);
                updateAt(
                  index,
                  applyOverlayIntentToInteraction(w, nextIntent, target),
                );
              }}
            >
              {RUNTIME_INTERACTION_INTENT_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </label>
          <label class="text-[0.65rem]">
            {UX_TARGET_UI_LABEL}
            <select
              class="input mt-0.5 w-full px-1 py-0.5 text-xs"
              value={w.targetNodeId ?? ""}
              onChange={(e) => {
                const nodeId = (e.target as HTMLSelectElement).value || undefined;
                const target = targetNodes.find((n) => n.nodeId === nodeId);
                if (!target?.componentKind) {
                  updateAt(index, { targetNodeId: nodeId });
                  return;
                }
                updateAt(
                  index,
                  applyOverlayIntentToInteraction(
                    w,
                    overlayIntentFromAction(w.actionType),
                    target,
                  ),
                );
              }}
            >
              <option value="">選択してください</option>
              {overlayTargets.map((node) => (
                <option key={node.nodeId} value={node.nodeId}>
                  {friendlyOverlayTargetLabel(node)}
                </option>
              ))}
            </select>
          </label>
        </div>
        <div class="flex justify-end">
          <button type="button" class="text-[0.6rem] text-red-500" onClick={() => removeAt(index)}>
            削除
          </button>
        </div>
      </div>
    );
  };

  const renderExternalRow = (w: NodeEventWiring, index: number, isDraft = false) => {
    const onUpdate = isDraft
      ? (patch: Partial<NodeEventWiring>) =>
        setStaging((s) => s ? { ...s, draft: { ...s.draft, ...patch } } : s)
      : (patch: Partial<NodeEventWiring>) => updateAt(index, patch);
    const onRemove = isDraft
      ? () => setStaging(null)
      : () => removeAt(index);
    // Credential authority stays inside the port record context (read-only projection).
    // SSOT: external-port-substrate-ssot.yaml admin_setting_projection
    const selectedCandidate = w.portTargetRef
      ? externalPortCandidates.find((c) => c.targetRef === w.portTargetRef)
      : undefined;

    return (
      <div class="rounded border border-indigo-100 bg-indigo-50/40 p-2 space-y-2 text-[0.65rem]">
        <div class="font-semibold text-indigo-900">{UX_EXTERNAL_INTEGRATION_SECTION_TITLE}</div>
        <p class="text-[0.6rem] text-indigo-700">{UX_EXTERNAL_INTEGRATION_SECTION_HINT}</p>
        <label class="text-[0.65rem] block">
          {UX_TRIGGER_UI_LABEL}
          <select
            class="input mt-0.5 w-full px-1 py-0.5 text-xs"
            value={w.trigger ?? "click"}
            onChange={(e) => onUpdate({ trigger: (e.target as HTMLSelectElement).value })}
          >
            {runtimeInteractionTriggerOptions(triggerOptions).map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </label>
        <label class="block">
          {UX_RUNTIME_INTERACTION_EXTERNAL_PORT_SELECT}
          {externalPortCandidateError && (
            <p class="mt-0.5 text-[0.55rem] text-amber-700">{externalPortCandidateError}</p>
          )}
          <select
            class="input mt-0.5 w-full px-1 py-0.5 text-xs"
            value={w.portTargetRef ?? ""}
            onChange={(e) =>
              onUpdate({ portTargetRef: (e.target as HTMLSelectElement).value || undefined })}
          >
            <option value="">— 選択してください —</option>
            {externalPortCandidates.map((c) => (
              <option key={c.targetRef} value={c.targetRef}>
                {c.portKind} / {c.providerKind} / {c.requiredByBundle}
              </option>
            ))}
          </select>
          {!externalPortCandidateError && externalPortCandidates.length === 0 && (
            <p class="mt-0.5 text-[0.55rem] text-amber-700">
              {UX_EXTERNAL_PORT_EMPTY_HINT}
            </p>
          )}
          {!w.portTargetRef && (
            <p class="mt-0.5 text-[0.55rem] text-red-700">
              操作対象を選択するまで layout draft には保存されません。
            </p>
          )}
        </label>
        {selectedCandidate && (
          <p class="rounded border border-indigo-100 bg-white px-1.5 py-1 text-[0.58rem] text-indigo-800">
            {UX_EXTERNAL_PORT_CREDENTIAL_AUTHORITY_LABEL}: {selectedCandidate.credentialKind}
            {selectedCandidate.referenceKey
              ? `（参照キー: ${selectedCandidate.referenceKey}）`
              : "（参照キーなし）"}
          </p>
        )}
        {renderDispatchPolicyControls(w, onUpdate)}
        {!isDraft && renderSideEffectSettings(w, index, onUpdate)}
        <div class="flex justify-end gap-2">
          {isDraft && (
            <>
              <button type="button" class="btn-secondary text-[0.65rem]" onClick={onRemove}>
                {UX_RUNTIME_INTERACTION_STAGING_CANCEL}
              </button>
              <button
                type="button"
                class="btn-primary text-[0.65rem]"
                disabled={!isCompleteInteraction(w)}
                onClick={commitStaging}
              >
                {UX_RUNTIME_INTERACTION_ADD_EXTERNAL_COMMIT}
              </button>
            </>
          )}
          {!isDraft && (
            <button type="button" class="text-[0.6rem] text-red-500" onClick={onRemove}>
              削除
            </button>
          )}
        </div>
      </div>
    );
  };

  const renderInstanceRow = (w: NodeEventWiring, index: number, isDraft = false) => {
    const onUpdate = isDraft
      ? (patch: Partial<NodeEventWiring>) =>
        setStaging((s) => s ? { ...s, draft: { ...s.draft, ...patch } } : s)
      : (patch: Partial<NodeEventWiring>) => updateAt(index, patch);
    const onRemove = isDraft
      ? () => setStaging(null)
      : () => removeAt(index);

    return (
      <div class="rounded border border-emerald-100 bg-emerald-50/40 p-2 space-y-2 text-[0.65rem]">
        <div class="font-semibold text-emerald-900">{UX_INSTANCE_OPERATION_SECTION_TITLE}</div>
        <p class="text-[0.6rem] text-emerald-700">{UX_INSTANCE_OPERATION_SECTION_HINT}</p>
        <label class="text-[0.65rem] block">
          {UX_TRIGGER_UI_LABEL}
          <select
            class="input mt-0.5 w-full px-1 py-0.5 text-xs"
            value={w.trigger ?? "click"}
            onChange={(e) => onUpdate({ trigger: (e.target as HTMLSelectElement).value })}
          >
            {runtimeInteractionTriggerOptions(triggerOptions).map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </label>
        <label class="block">
          {UX_RUNTIME_INTERACTION_INSTANCE_OPERATION_SELECT}
          {instanceOperationCandidateError && (
            <p class="mt-0.5 text-[0.55rem] text-amber-700">{instanceOperationCandidateError}</p>
          )}
          <select
            class="input mt-0.5 w-full px-1 py-0.5 text-xs"
            value={w.instanceTargetRef ?? ""}
            onChange={(e) =>
              onUpdate({ instanceTargetRef: (e.target as HTMLSelectElement).value || undefined })}
          >
            <option value="">— 選択してください —</option>
            {instanceOperationCandidates.map((c) => (
              <option key={c.targetRef} value={c.targetRef}>
                {c.portKind} / {c.operationBindingKey} / {c.approvalStatus}
              </option>
            ))}
          </select>
          {!instanceOperationCandidateError && instanceOperationCandidates.length === 0 && (
            <p class="mt-0.5 text-[0.55rem] text-amber-700">
              {UX_INSTANCE_OPERATION_EMPTY_HINT}
            </p>
          )}
          {!w.instanceTargetRef && (
            <p class="mt-0.5 text-[0.55rem] text-red-700">
              操作対象を選択するまで layout draft には保存されません。
            </p>
          )}
        </label>
        {renderDispatchPolicyControls(w, onUpdate)}
        {!isDraft && renderSideEffectSettings(w, index, onUpdate)}
        <div class="flex justify-end gap-2">
          {isDraft && (
            <>
              <button type="button" class="btn-secondary text-[0.65rem]" onClick={onRemove}>
                {UX_RUNTIME_INTERACTION_STAGING_CANCEL}
              </button>
              <button
                type="button"
                class="btn-primary text-[0.65rem]"
                disabled={!isCompleteInteraction(w)}
                onClick={commitStaging}
              >
                {UX_RUNTIME_INTERACTION_ADD_INSTANCE_COMMIT}
              </button>
            </>
          )}
          {!isDraft && (
            <button type="button" class="text-[0.6rem] text-red-500" onClick={onRemove}>
              削除
            </button>
          )}
        </div>
      </div>
    );
  };

  return (
    <section class="rounded border border-blue-100 bg-blue-50/40 p-3">
      <div class="mb-2">
        <h4 class="text-xs font-semibold text-blue-900">
          {UX_RUNTIME_INTERACTION_SECTION_TITLE}
          {selectedNodeLabel ? ` — ${selectedNodeLabel}` : ""}
        </h4>
        <p class="mt-1 text-[0.65rem] text-blue-800">{UX_RUNTIME_INTERACTION_SECTION_HINT}</p>
      </div>
      {/* UI監視割当 — 宣言カテゴリ（stateJson由来の状態スロット割当。更新・副作用とは別軸） */}
      {onCommitStateJson && (
        <fieldset class="mb-2 rounded border border-sky-200 bg-sky-50/50 p-2 space-y-1.5">
          <legend class="text-[0.68rem] font-semibold text-sky-900">
            {UX_WIRING_CATEGORY_UI_WATCH_BINDING}
          </legend>
          <p class="text-[0.58rem] text-sky-800">{UX_WIRING_WATCH_BINDING_HINT}</p>
          {(() => {
            const bindings = sourceNodeId
              ? deriveUiWatchBindings({ nodeId: sourceNodeId, stateJson })
              : [];
            const commitSlots = (
              entries: Array<[string, unknown]>,
              label: string,
            ) => {
              const next = Object.fromEntries(entries.filter(([k]) => k.trim()));
              onCommitStateJson(
                Object.keys(next).length > 0 ? JSON.stringify(next) : undefined,
                label,
              );
            };
            return (
              <>
                {bindings.length === 0 && (
                  <p class="text-[0.58rem] text-slate-500">{UX_WIRING_WATCH_BINDING_EMPTY}</p>
                )}
                {bindings.map((b) => (
                  <div key={b.stateKey} class="flex items-center gap-1 text-[0.62rem]">
                    <span class="input-mono rounded bg-white px-1 py-0.5">{b.stateKey}</span>
                    <span class="text-slate-400">初期値:</span>
                    <span class="input-mono rounded bg-white px-1 py-0.5">
                      {JSON.stringify(b.initialValue)}
                    </span>
                    <button
                      type="button"
                      class="text-[0.55rem] text-red-400 hover:underline"
                      onClick={() =>
                        commitSlots(
                          bindings.filter((x) => x.stateKey !== b.stateKey)
                            .map((x) => [x.stateKey, x.initialValue]),
                          "状態スロット宣言を削除",
                        )}
                    >
                      削除
                    </button>
                  </div>
                ))}
                <button
                  type="button"
                  class="self-start text-[0.6rem] text-sky-700 hover:underline"
                  onClick={() => {
                    const stateKey = globalThis.prompt?.("状態スロット名（stateKey）")?.trim();
                    if (!stateKey) return;
                    commitSlots(
                      [...bindings.map(
                        (x) => [x.stateKey, x.initialValue] as [string, unknown],
                      ), [stateKey, null]],
                      "状態スロットを宣言",
                    );
                  }}
                >
                  {UX_WIRING_WATCH_BINDING_ADD}
                </button>
              </>
            );
          })()}
        </fieldset>
      )}
      <div class="space-y-2">
        {interactions.some((w) => isOverlayDisclosureAction(w.actionType)) && (
          <p class="text-[0.68rem] font-semibold text-blue-900">
            {UX_WIRING_CATEGORY_UI_STATE_UPDATE}
          </p>
        )}
        {interactions.map((w, i) => {
          const category = runtimeInteractionCategory(w.actionType);
          if (category === "external_port") return renderExternalRow(w, i);
          if (category === "instance_operation") return renderInstanceRow(w, i);
          if (isOverlayDisclosureAction(w.actionType)) return renderOverlayRow(w, i);
          return null;
        })}
        {staging?.kind === "external" && renderExternalRow(staging.draft, -1, true)}
        {staging?.kind === "instance" && renderInstanceRow(staging.draft, -1, true)}
        <div class="flex flex-wrap gap-2">
          <button
            type="button"
            class="btn-secondary text-xs"
            disabled={overlayTargets.length === 0 || staging !== null}
            title={overlayTargets.length === 0 ? UX_RUNTIME_INTERACTION_NO_OVERLAY_TARGETS : undefined}
            onClick={() => {
              const next = defaultOverlayOpenInteraction(targetNodes);
              onCommit([...interactions, next as NodeEventWiring], "モーダル操作を追加");
            }}
          >
            {UX_RUNTIME_INTERACTION_ADD_OVERLAY}
          </button>
          <button
            type="button"
            class="btn-secondary text-xs"
            disabled={staging !== null}
            onClick={() =>
              setStaging({
                kind: "external",
                draft: defaultExternalPortInteraction() as NodeEventWiring,
              })}
          >
            {UX_RUNTIME_INTERACTION_ADD_EXTERNAL}
          </button>
          <button
            type="button"
            class="btn-secondary text-xs"
            disabled={staging !== null}
            onClick={() =>
              setStaging({
                kind: "instance",
                draft: defaultInstanceOperationInteraction() as NodeEventWiring,
              })}
          >
            {UX_RUNTIME_INTERACTION_ADD_INSTANCE}
          </button>
        </div>
        {overlayTargets.length === 0 && (
          <p class="text-[0.65rem] text-amber-800">{UX_RUNTIME_INTERACTION_NO_OVERLAY_TARGETS}</p>
        )}
      </div>
    </section>
  );
}
