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
};

type Props = {
  interactions: NodeEventWiring[];
  targetNodes: OverlayTargetNode[];
  triggerOptions: readonly string[];
  selectedNodeLabel?: string;
  onCommit: (next: NodeEventWiring[], label: string) => void;
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
}: Props): JSX.Element {
  const overlayTargets = listOverlayTargetNodes(targetNodes);
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
        {!isDraft && renderPayloadFrom(w, index)}
        {!isDraft && (
          <label class="block">
            結果受け取り先（outputProp）
            <input
              class="input-mono mt-0.5 w-full px-1 py-0.5 text-xs"
              value={w.outputProp ?? ""}
              placeholder="例: result"
              onInput={(e) =>
                onUpdate({ outputProp: (e.target as HTMLInputElement).value || undefined })}
            />
          </label>
        )}
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
      <div class="space-y-2">
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
