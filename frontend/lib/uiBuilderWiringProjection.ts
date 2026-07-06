/**
 * UI Builder wiring mode projection / policy lib.
 *
 * SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
 * - wiring_mode: the wiring graph is a view/edit projection over
 *   draftNodes[].runtimeInteractions; it is rehydrated from runtimeInteractions
 *   on every render and is never a persistence source.
 * - trigger_vocabulary / lifecycle_policy / high_frequency_policy: authoring
 *   fail-close gates evaluated here before layout_patch save/apply.
 * - drag_drop_wiring_edit: valid drop -> typed runtimeInteraction patch;
 *   invalid drop -> explicit error and no draft mutation.
 */

import {
  isOverlayOpenableComponentKind,
  resolveDisclosureActionType,
  type RuntimeInteractionCategory,
  runtimeInteractionCategory,
} from "./runtimeInteractionAuthoring.ts";

export type WiringInteraction = {
  trigger: string;
  actionType: string;
  targetNodeId?: string;
  statePath?: string;
  value?: unknown;
  payloadFrom?: Record<string, string>;
  outputProp?: string;
  portTargetRef?: string;
  instanceTargetRef?: string;
  /** SSOT high_frequency_policy: required for backend/external dispatch on high-frequency triggers. */
  debounceMs?: number;
  /** SSOT lifecycle_policy: explicit author confirmation for backend/external dispatch on lifecycle triggers. */
  lifecycleDispatchConfirmed?: boolean;
};

export type WiringNode = {
  nodeId: string;
  componentKey?: string;
  componentKind?: string;
  runtimeInteractions?: WiringInteraction[];
};

/** SSOT trigger_vocabulary groups. Raw values stay persistence identifiers; labels live in adminUxTerms. */
export const TRIGGER_VOCABULARY = {
  lifecycle: ["load", "route_enter", "initial_display"],
  pointer: ["mouseon", "mouseout", "hover_start", "hover_end"],
  keyboard: ["keyon", "keydown", "keyup", "enter", "escape"],
  form: [
    "click",
    "change",
    "submit",
    "toggle",
    "select",
    "input",
    "focus",
    "blur",
  ],
} as const;

export type TriggerGroup = keyof typeof TRIGGER_VOCABULARY;

export const ALL_WIRING_TRIGGERS: readonly string[] = [
  ...TRIGGER_VOCABULARY.form,
  ...TRIGGER_VOCABULARY.lifecycle,
  ...TRIGGER_VOCABULARY.pointer,
  ...TRIGGER_VOCABULARY.keyboard,
];

/** SSOT high_frequency_policy trigger set. */
export const HIGH_FREQUENCY_TRIGGERS: readonly string[] = [
  "mouseon",
  "mouseout",
  "hover_start",
  "hover_end",
  "keyon",
  "keydown",
  "keyup",
  "input",
];

export function classifyTrigger(trigger: string): TriggerGroup | null {
  for (const group of Object.keys(TRIGGER_VOCABULARY) as TriggerGroup[]) {
    if ((TRIGGER_VOCABULARY[group] as readonly string[]).includes(trigger)) {
      return group;
    }
  }
  return null;
}

export function isLifecycleTrigger(trigger: string): boolean {
  return classifyTrigger(trigger) === "lifecycle";
}

export function isHighFrequencyTrigger(trigger: string): boolean {
  return HIGH_FREQUENCY_TRIGGERS.includes(trigger);
}

/** Backend/external dispatch actions gated by lifecycle / high-frequency policies. */
export function isBackendOrExternalDispatchAction(actionType: string): boolean {
  return actionType === "dispatchExternalPort" ||
    actionType === "dispatchInstanceOperation";
}

/**
 * SSOT lifecycle_policy: preview is inert by default for lifecycle triggers, and
 * backend/external dispatch stays authoring-only in this bundle (no runtime handler).
 */
export function isPreviewInertInteraction(w: WiringInteraction): boolean {
  return isLifecycleTrigger(w.trigger) ||
    isBackendOrExternalDispatchAction(w.actionType);
}

/**
 * Fail-close authoring policy errors per SSOT trigger_vocabulary /
 * lifecycle_policy / high_frequency_policy. Returned messages carry stable codes.
 */
export function findRuntimeInteractionPolicyErrors(
  nodes: readonly WiringNode[],
): string[] {
  const errors: string[] = [];
  for (const node of nodes) {
    const label = node.componentKey || node.nodeId;
    for (const [idx, w] of (node.runtimeInteractions ?? []).entries()) {
      const prefix = `${label} #${idx + 1}`;
      const group = classifyTrigger(w.trigger);
      if (group === null) {
        errors.push(
          `${prefix}: TRIGGER_OUTSIDE_VOCABULARY — トリガ "${w.trigger}" はSSOT語彙外です`,
        );
        continue;
      }
      if (!isBackendOrExternalDispatchAction(w.actionType)) continue;
      if (isHighFrequencyTrigger(w.trigger)) {
        const debounce = w.debounceMs;
        if (
          typeof debounce !== "number" || !Number.isInteger(debounce) ||
          debounce <= 0
        ) {
          errors.push(
            `${prefix}: HIGH_FREQUENCY_DISPATCH_REQUIRES_DEBOUNCE — 高頻度トリガ "${w.trigger}" での外部/バックエンド送出には debounceMs（正の整数）が必要です`,
          );
        }
      }
      if (group === "lifecycle" && w.lifecycleDispatchConfirmed !== true) {
        errors.push(
          `${prefix}: LIFECYCLE_DISPATCH_REQUIRES_CONFIRMATION — ライフサイクルトリガ "${w.trigger}" での外部/バックエンド送出には明示的な確認が必要です`,
        );
      }
    }
  }
  return errors;
}

export type WiringTargetKind =
  | "node"
  | "external_port"
  | "instance_port"
  | "unset";

export type WiringGraphEdge = {
  sourceNodeId: string;
  sourceLabel: string;
  /** Index into the source node's runtimeInteractions — the rehydration reference. */
  interactionIndex: number;
  trigger: string;
  triggerGroup: TriggerGroup | null;
  category: RuntimeInteractionCategory;
  targetKind: WiringTargetKind;
  targetRef: string | null;
  effect: string;
  previewInert: boolean;
};

export type WiringGraphProjection = {
  edges: WiringGraphEdge[];
  /** nodeIds with no runtimeInteractions (still droppable wiring sources/targets). */
  unwiredNodeIds: string[];
};

/**
 * Pure view projection over runtimeInteractions.
 * Never caches or owns state; callers re-derive it from draft nodes per render,
 * and edits map back through (sourceNodeId, interactionIndex).
 */
export function buildWiringGraphProjection(
  nodes: readonly WiringNode[],
): WiringGraphProjection {
  const edges: WiringGraphEdge[] = [];
  const unwiredNodeIds: string[] = [];
  for (const node of nodes) {
    const interactions = node.runtimeInteractions ?? [];
    if (interactions.length === 0) {
      unwiredNodeIds.push(node.nodeId);
      continue;
    }
    for (const [idx, w] of interactions.entries()) {
      const category = runtimeInteractionCategory(w.actionType);
      let targetKind: WiringTargetKind = "unset";
      let targetRef: string | null = null;
      if (category === "external_port") {
        targetKind = "external_port";
        targetRef = w.portTargetRef ?? null;
      } else if (category === "instance_operation") {
        targetKind = "instance_port";
        targetRef = w.instanceTargetRef ?? null;
      } else if (w.targetNodeId) {
        targetKind = "node";
        targetRef = w.targetNodeId;
      }
      edges.push({
        sourceNodeId: node.nodeId,
        sourceLabel: node.componentKey || node.nodeId,
        interactionIndex: idx,
        trigger: w.trigger,
        triggerGroup: classifyTrigger(w.trigger),
        category,
        targetKind,
        targetRef,
        effect: w.statePath ? `${w.actionType}(${w.statePath})` : w.actionType,
        previewInert: isPreviewInertInteraction(w),
      });
    }
  }
  return { edges, unwiredNodeIds };
}

export type WiringDropEditResult<T extends WiringNode> =
  | { ok: true; nodes: T[]; added: WiringInteraction }
  | { ok: false; error: string };

/**
 * SSOT drag_drop_wiring_edit: valid drop appends a typed runtimeInteraction to
 * the source draft node; invalid drop returns an explicit error and the input
 * nodes are NOT mutated. The result stays draft-scoped (caller pushes history).
 */
export function applyWiringDropEdit<T extends WiringNode>(
  nodes: readonly T[],
  sourceNodeId: string,
  targetNodeId: string,
): WiringDropEditResult<T> {
  if (sourceNodeId === targetNodeId) {
    return {
      ok: false,
      error: "WIRING_DROP_SELF_TARGET — 自ノードへの配線はできません",
    };
  }
  const source = nodes.find((n) => n.nodeId === sourceNodeId);
  if (!source) {
    return {
      ok: false,
      error: `WIRING_DROP_SOURCE_NOT_FOUND — ${sourceNodeId}`,
    };
  }
  const target = nodes.find((n) => n.nodeId === targetNodeId);
  if (!target) {
    return {
      ok: false,
      error: `WIRING_DROP_TARGET_NOT_FOUND — ${targetNodeId}`,
    };
  }
  if (!isOverlayOpenableComponentKind(target.componentKind)) {
    return {
      ok: false,
      error: `WIRING_DROP_TARGET_NOT_WIRABLE — ${
        target.componentKey || target.nodeId
      } は開閉対象部品（モーダル／ドロワー／ダイアログ）ではありません`,
    };
  }
  const added: WiringInteraction = {
    trigger: "click",
    actionType: resolveDisclosureActionType("open", target.componentKind!),
    targetNodeId: target.nodeId,
    statePath: "open",
  };
  const next = nodes.map((n) =>
    n.nodeId === sourceNodeId
      ? { ...n, runtimeInteractions: [...(n.runtimeInteractions ?? []), added] }
      : n
  );
  return { ok: true, nodes: next, added };
}
