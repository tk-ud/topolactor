/**
 * UI Builder wiring mode projection / policy lib.
 *
 * SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
 * Vocabulary authority: .agent/reports/frontend-ui-audit-bundle-semantic-frame.md
 * (Bundle: admin-uibuilder-ui-structure-wiring-ssot) — report / SSOT / test /
 * implementation keep the same trigger group mapping and setting category taxonomy.
 *
 * - wiring_mode: the wiring graph is a view/edit projection over
 *   draftNodes[].runtimeInteractions; it is rehydrated from runtimeInteractions
 *   on every render and is never a persistence source.
 * - trigger_vocabulary / lifecycle_policy / high_frequency_policy /
 *   side_effect_cycle_policy: authoring fail-close gates evaluated here before
 *   layout_patch save/apply.
 * - drag_drop_wiring_edit: valid drop -> typed runtimeInteraction patch;
 *   invalid drop -> explicit error and no draft mutation.
 */

import {
  isOverlayOpenableComponentKind,
  resolveDisclosureActionType,
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
  /** SSOT lifecycle_policy: idempotency policy required for backend/external dispatch on lifecycle triggers. */
  idempotencyPolicy?: string;
  /** SSOT ui_event_settings.side_effect_setting: explicit no-side-effect selection. */
  sideEffectNone?: boolean;
};

export type WiringNode = {
  nodeId: string;
  componentKey?: string;
  componentKind?: string;
  stateJson?: string;
  runtimeInteractions?: WiringInteraction[];
};

/**
 * SSOT trigger_vocabulary groups (canonical report vocabulary).
 * initial_mount is a runtime synthetic lifecycle trigger — NOT DOM onLoad /
 * resource loaded. load / loaded / DOM onLoad are prohibited vocabulary.
 * Raw values stay persistence identifiers; labels live in adminUxTerms.
 */
export const TRIGGER_VOCABULARY = {
  lifecycle: ["initial_mount", "route_enter", "initial_display"],
  pointer: ["click", "mouseon", "mouseout", "hover_start", "hover_end"],
  keyboard: ["keyon", "keydown", "keyup", "enter", "escape"],
  form: ["input", "change", "select", "submit", "focus", "blur"],
} as const;

export type TriggerGroup = keyof typeof TRIGGER_VOCABULARY;

export const ALL_WIRING_TRIGGERS: readonly string[] = [
  ...TRIGGER_VOCABULARY.pointer,
  ...TRIGGER_VOCABULARY.form,
  ...TRIGGER_VOCABULARY.keyboard,
  ...TRIGGER_VOCABULARY.lifecycle,
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

/** SSOT lifecycle_policy idempotency_policy_values. */
export const LIFECYCLE_IDEMPOTENCY_POLICIES: readonly string[] = [
  "once_per_mount",
  "refetch_on_route_enter",
  "dedupe_key",
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
 * SSOT ui_event_settings.setting_category_taxonomy — the six canonical wiring
 * inspector categories. No implementation-derived catch-all: an actionType
 * outside the taxonomy classifies as null and fails close in policy errors.
 *
 * ui_watch_binding (UI監視割当) is a declaration category (state slots), not an
 * interaction actionType — see deriveUiWatchBindings.
 * internal_api (内部API) is the package wiring lane (contents Step 3 /
 * screenReadQueryWiring candidates), projected via buildWiringGraphProjection's
 * internalApiWirings input, not a runtimeInteraction actionType.
 */
export const WIRING_SETTING_CATEGORIES = [
  "ui_watch_binding",
  "ui_state_update",
  "side_effect_setting",
  "internal_api",
  "external_api_integration",
  "external_instance_integration",
] as const;

export type WiringSettingCategory = typeof WIRING_SETTING_CATEGORIES[number];

const UI_STATE_UPDATE_ACTIONS = new Set([
  "openModal",
  "closeModal",
  "toggleModal",
  "openDrawer",
  "closeDrawer",
  "toggleDrawer",
  "openDialog",
  "closeDialog",
  "toggleDialog",
  "setActiveKey",
  "setState",
]);

/**
 * Classify one runtimeInteraction into the canonical taxonomy.
 * Returns null for actionTypes outside the taxonomy (fail-close; no catch-all).
 */
export function wiringSettingCategoryOf(
  w: Pick<WiringInteraction, "actionType">,
): WiringSettingCategory | null {
  if (w.actionType === "dispatchExternalPort") {
    return "external_api_integration";
  }
  if (w.actionType === "dispatchInstanceOperation") {
    return "external_instance_integration";
  }
  if (UI_STATE_UPDATE_ACTIONS.has(w.actionType)) return "ui_state_update";
  return null;
}

/** Effect fields present on an interaction (副作用設定 fields, not the effect authority itself). */
export function hasSideEffectFields(w: WiringInteraction): boolean {
  return Boolean(
    w.outputProp?.trim() ||
      (w.payloadFrom && Object.keys(w.payloadFrom).length > 0),
  );
}

/**
 * UI監視割当 (declaration category): derive declared state slots for a node.
 * Declarations are useState-like bindings, not mutations. The structured slot
 * list is the declaration surface; raw stateJson presence alone is NOT
 * sufficient UI監視割当 proof — this function is the single derivation point
 * the inspector and tests share.
 */
export type UiWatchBinding = {
  kind: "declaration";
  nodeId: string;
  stateKey: string;
  initialValue: unknown;
};

export function deriveUiWatchBindings(node: WiringNode): UiWatchBinding[] {
  if (!node.stateJson?.trim()) return [];
  try {
    const parsed = JSON.parse(node.stateJson) as Record<string, unknown>;
    if (
      typeof parsed !== "object" || parsed === null || Array.isArray(parsed)
    ) {
      return [];
    }
    return Object.entries(parsed).map(([stateKey, initialValue]) => ({
      kind: "declaration" as const,
      nodeId: node.nodeId,
      stateKey,
      initialValue,
    }));
  } catch {
    return [];
  }
}

/**
 * SSOT lifecycle_policy: preview is inert by default for lifecycle triggers, and
 * backend/external dispatch stays authoring-only in this bundle (no runtime handler).
 */
export function isPreviewInertInteraction(w: WiringInteraction): boolean {
  return isLifecycleTrigger(w.trigger) ||
    isBackendOrExternalDispatchAction(w.actionType);
}

/** Value-reactive triggers that can re-fire when the source node's value changes. */
const VALUE_REACTIVE_TRIGGERS = new Set(["input", "change", "select"]);

type WriteTarget = { nodeId: string; via: "outputProp" | "state" };

function interactionWriteTargets(
  w: WiringInteraction,
  sourceNodeId: string,
): WriteTarget[] {
  const targets: WriteTarget[] = [];
  if (w.outputProp?.trim()) {
    targets.push({ nodeId: sourceNodeId, via: "outputProp" });
  }
  if (w.targetNodeId?.trim() && UI_STATE_UPDATE_ACTIONS.has(w.actionType)) {
    targets.push({ nodeId: w.targetNodeId, via: "state" });
  }
  return targets;
}

/** Build value-reactive write edges: source node -> written node. */
function buildValueReactiveWriteEdges(
  nodes: readonly WiringNode[],
): Map<string, Set<string>> {
  const edges = new Map<string, Set<string>>();
  for (const node of nodes) {
    for (const w of node.runtimeInteractions ?? []) {
      if (!VALUE_REACTIVE_TRIGGERS.has(w.trigger)) continue;
      for (const target of interactionWriteTargets(w, node.nodeId)) {
        if (!edges.has(node.nodeId)) edges.set(node.nodeId, new Set());
        edges.get(node.nodeId)!.add(target.nodeId);
      }
    }
  }
  return edges;
}

/**
 * SSOT side_effect_cycle_policy:
 * selectable_write_targets = all_write_targets - dependency_closure(trigger_source).
 * dependency_closure(trigger_source) = trigger source itself plus every node from
 * which a value-reactive write chain reaches the trigger source (writing there
 * could feed back into the trigger).
 */
export function dependencyClosureOfTriggerSource(
  nodes: readonly WiringNode[],
  triggerSourceNodeId: string,
): ReadonlySet<string> {
  const edges = buildValueReactiveWriteEdges(nodes);
  const closure = new Set<string>([triggerSourceNodeId]);
  let grew = true;
  while (grew) {
    grew = false;
    for (const [from, tos] of edges) {
      if (closure.has(from)) continue;
      for (const to of tos) {
        if (closure.has(to)) {
          closure.add(from);
          grew = true;
          break;
        }
      }
    }
  }
  return closure;
}

export function selectableWriteTargets(
  nodes: readonly WiringNode[],
  triggerSourceNodeId: string,
): string[] {
  const closure = dependencyClosureOfTriggerSource(nodes, triggerSourceNodeId);
  return nodes.map((n) => n.nodeId).filter((id) => !closure.has(id));
}

/**
 * SSOT side_effect_cycle_policy fail-close checks.
 * Direct self-loop (watched A -> writes A) and detectable indirect loops
 * (A -> B and B -> A) are blocking. debounce/throttle does NOT clear these.
 */
export function findSideEffectCycleErrors(
  nodes: readonly WiringNode[],
): string[] {
  const errors: string[] = [];
  // Direct self-loop.
  for (const node of nodes) {
    const label = node.componentKey || node.nodeId;
    for (const [idx, w] of (node.runtimeInteractions ?? []).entries()) {
      if (!VALUE_REACTIVE_TRIGGERS.has(w.trigger)) continue;
      for (const target of interactionWriteTargets(w, node.nodeId)) {
        if (target.nodeId === node.nodeId) {
          errors.push(
            `${label} #${
              idx + 1
            }: SIDE_EFFECT_DIRECT_SELF_LOOP — 監視元 "${node.nodeId}" 自身への書き込み（${target.via}）は禁止です`,
          );
        }
      }
    }
  }
  // Detectable indirect loop (cycle in value-reactive write graph).
  const edges = buildValueReactiveWriteEdges(nodes);
  const visiting = new Set<string>();
  const done = new Set<string>();
  const reported = new Set<string>();
  const visit = (id: string, path: string[]): void => {
    if (done.has(id)) return;
    if (visiting.has(id)) {
      const start = path.indexOf(id);
      const cycle = [...path.slice(start), id];
      const key = [...cycle].sort().join(",");
      if (!reported.has(key) && cycle.length > 2) {
        reported.add(key);
        errors.push(
          `SIDE_EFFECT_INDIRECT_LOOP — 間接ループを検出: ${
            cycle.join(" -> ")
          }（明示的なcycle breakなしでは適用できません）`,
        );
      }
      return;
    }
    visiting.add(id);
    for (const to of edges.get(id) ?? []) visit(to, [...path, id]);
    visiting.delete(id);
    done.add(id);
  };
  for (const id of edges.keys()) visit(id, []);
  return errors;
}

/**
 * Fail-close authoring policy errors per SSOT trigger_vocabulary /
 * lifecycle_policy / high_frequency_policy / setting_category_taxonomy /
 * side_effect_cycle_policy. Returned messages carry stable codes. These are
 * blocking on the validate/apply/readiness paths — a warning display alone
 * never passes.
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
      if (wiringSettingCategoryOf(w) === null) {
        errors.push(
          `${prefix}: ACTION_OUTSIDE_VOCABULARY — actionType "${w.actionType}" は分類語彙外です`,
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
      if (group === "lifecycle") {
        if (w.lifecycleDispatchConfirmed !== true) {
          errors.push(
            `${prefix}: LIFECYCLE_DISPATCH_REQUIRES_CONFIRMATION — ライフサイクルトリガ "${w.trigger}" での外部/バックエンド送出には明示的な確認が必要です`,
          );
        }
        if (
          !w.idempotencyPolicy ||
          !LIFECYCLE_IDEMPOTENCY_POLICIES.includes(w.idempotencyPolicy)
        ) {
          errors.push(
            `${prefix}: LIFECYCLE_DISPATCH_REQUIRES_IDEMPOTENCY_POLICY — ライフサイクルトリガ "${w.trigger}" での送出には idempotency policy（${
              LIFECYCLE_IDEMPOTENCY_POLICIES.join(" / ")
            }）が必要です`,
          );
        }
      }
    }
  }
  // Side-effect cycle policy is independent of debounce/throttle (loop safety is
  // never proven by debounce) and blocks the same validate/apply paths.
  errors.push(...findSideEffectCycleErrors(nodes));
  return errors;
}

export type WiringTargetKind =
  | "node"
  | "external_port"
  | "instance_port"
  | "internal_api"
  | "unset";

export type WiringGraphEdge = {
  sourceNodeId: string;
  sourceLabel: string;
  /** Index into the source node's runtimeInteractions — the rehydration reference. -1 for package-lane edges. */
  interactionIndex: number;
  trigger: string;
  triggerGroup: TriggerGroup | null;
  /** Canonical setting category; null = outside taxonomy (fails close in policy errors). */
  category: WiringSettingCategory | null;
  targetKind: WiringTargetKind;
  targetRef: string | null;
  effect: string;
  /** 副作用設定 fields present on this interaction. */
  hasSideEffect: boolean;
  previewInert: boolean;
};

/** 内部API package wiring lane input for projection (view only). */
export type InternalApiWiringInput = {
  wiringKey: string;
  targetRef: string | null;
};

export type WiringGraphProjection = {
  edges: WiringGraphEdge[];
  /** UI監視割当 declarations derived per node (declaration category, not mutations). */
  watchBindings: UiWatchBinding[];
  /** nodeIds with no runtimeInteractions (still droppable wiring sources/targets). */
  unwiredNodeIds: string[];
};

/**
 * Pure view projection over runtimeInteractions (+ optional 内部API package lane).
 * Never caches or owns state; callers re-derive it from draft nodes per render,
 * and edits map back through (sourceNodeId, interactionIndex).
 */
export function buildWiringGraphProjection(
  nodes: readonly WiringNode[],
  internalApiWirings: readonly InternalApiWiringInput[] = [],
): WiringGraphProjection {
  const edges: WiringGraphEdge[] = [];
  const watchBindings: UiWatchBinding[] = [];
  const unwiredNodeIds: string[] = [];
  for (const node of nodes) {
    watchBindings.push(...deriveUiWatchBindings(node));
    const interactions = node.runtimeInteractions ?? [];
    if (interactions.length === 0) {
      unwiredNodeIds.push(node.nodeId);
      continue;
    }
    for (const [idx, w] of interactions.entries()) {
      const category = wiringSettingCategoryOf(w);
      let targetKind: WiringTargetKind = "unset";
      let targetRef: string | null = null;
      if (category === "external_api_integration") {
        targetKind = "external_port";
        targetRef = w.portTargetRef ?? null;
      } else if (category === "external_instance_integration") {
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
        hasSideEffect: hasSideEffectFields(w),
        previewInert: isPreviewInertInteraction(w),
      });
    }
  }
  // 内部API package wiring lane — projection/view only; persistence stays on
  // ui_topology:update_package_wiring, never on this graph.
  for (const wiring of internalApiWirings) {
    edges.push({
      sourceNodeId: "package",
      sourceLabel: "パッケージ",
      interactionIndex: -1,
      trigger: "client",
      triggerGroup: null,
      category: "internal_api",
      targetKind: "internal_api",
      targetRef: wiring.targetRef,
      effect: wiring.wiringKey,
      hasSideEffect: false,
      previewInert: true,
    });
  }
  return { edges, watchBindings, unwiredNodeIds };
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
