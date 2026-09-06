/**
 * UI event effect runner — the runtime wrapper/factory-owned useEffect-like
 * execution boundary for authored runtimeInteractions.
 *
 * SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
 * component_runtime_state_effect_boundary / lifecycle_policy / side_effect_cycle_policy
 *
 * Boundary:
 * - The runner (not individual components) owns lifecycle emission and effect
 *   execution. Primitive components keep exposing event/value surfaces only;
 *   event-triggered lanes stay in runtimeComponentFactory.emitBoundEvent, which
 *   writes UI状態更新 through the SAME guarded dispatcher this module creates —
 *   mutation authority is not duplicated across the lifecycle and event paths.
 * - UI監視割当: declared state slots (from stateJson) AND predeclared built-in
 *   UI状態更新 targets (disclosure/tabs/accordion/setState) are connected to the
 *   runtime state store BEFORE any mutation or effect execution can run
 *   (declare-then-mutate order, enforced for both the lifecycle and the
 *   event-triggered path). A slot that was never declared this way fails close
 *   on write — including a stale/deleted-node target reference.
 * - UI状態更新: state writes go only through the runtime state dispatcher.
 * - 副作用設定: execution resolves the dependency graph first; a graph with a
 *   direct or detectable indirect loop fails close (debounce is not loop-safety
 *   proof) and no effect executes.
 * - initial_mount is a runtime synthetic lifecycle trigger emitted here — it is
 *   NOT DOM onLoad / resource loaded. Preview is inert. idempotencyPolicy
 *   guarantees initial_mount does not re-dispatch on rerender or projection
 *   refresh (the fired-registry persists for the runner's lifetime).
 */

import {
  computeDispatchIdempotencyKey,
  deriveUiWatchBindings,
  findRuntimeInteractionPolicyViolations,
  findSideEffectCycleErrors,
  isBackendOrExternalDispatchAction,
  isLifecycleTrigger,
  parseUiLocalTargetRef,
  type WiringInteraction,
  type WiringNode,
  wiringNodeDisplayLabel,
  wiringSettingCategoryOf,
} from "../lib/uiBuilderWiringProjection.ts";
import type {
  RuntimeGuardedStateStore,
  RuntimeLocalStateStore,
} from "./runtimeComponentAdapter.ts";
import {
  enqueueExternalPortDispatchCommand,
  enqueueInstanceOperationDispatchCommand,
} from "./frontendScheduler.ts";
import { buildVisibilityGraph, resolveNodeVisibility } from "./structuralVisibility.ts";
import {
  parsePayloadFromSource,
  resolvePayloadFromSource,
} from "./payloadFromResolver.ts";

/**
 * Re-exported here (canonical definition now in uiBuilderWiringProjection.ts) so
 * existing callers of this module (visualLayoutUtils.ts, tests) keep working
 * unchanged. The side_effect_cycle_policy dependency-graph derivation
 * (interactionWriteTargets, same module as the canonical definition) and this
 * module's own resolveUiStateUpdateMutation below resolve a localStateMutation
 * targetRef through the exact same parser — never two independently-maintained
 * copies that could drift apart.
 */
export { parseUiLocalTargetRef };

/**
 * Runtime local state store with change notification — the projection rerender
 * hook: store update -> subscribed listeners -> renderEmission re-run, so
 * UI状態更新 reflects into rendered runtimeSpec props (a Map update alone is
 * never treated as UI状態更新 completion). Listeners fire only on an actual
 * value change; renderEmission is a pure read, so no notification loop forms.
 */
export type NotifyingRuntimeLocalStateStore = RuntimeLocalStateStore & {
  subscribe(listener: () => void): () => void;
};

/** Map-backed projection-local state store (production default). */
export function createRuntimeLocalStateStore(): NotifyingRuntimeLocalStateStore {
  const store = new Map<string, unknown>();
  const listeners = new Set<() => void>();
  return {
    get: (nodeId, statePath) => store.get(`${nodeId} ${statePath}`),
    set: (nodeId, statePath, value) => {
      const key = `${nodeId} ${statePath}`;
      const changed = !store.has(key) || store.get(key) !== value;
      store.set(key, value);
      if (changed) {
        for (const listener of [...listeners]) listener();
      }
    },
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
  };
}

/**
 * Runtime state dispatcher — the ONLY mutation path for UI状態更新, shared by
 * BOTH the lifecycle path (this module's emitLifecycle) and the event-triggered
 * path (runtimeComponentFactory.emitBoundEvent). set() fails close for any
 * slot that was never declared — there is no write-time auto-declare fallback,
 * so "declared before mutated" is a real invariant, not a formality.
 */
export type RuntimeStateDispatcher = RuntimeGuardedStateStore;

export function createRuntimeStateDispatcher(
  store: RuntimeLocalStateStore,
): RuntimeStateDispatcher {
  const declared = new Set<string>();
  const key = (nodeId: string, stateKey: string) => `${nodeId} ${stateKey}`;
  return {
    declare(nodeId, stateKey, initialValue) {
      if (declared.has(key(nodeId, stateKey))) return;
      declared.add(key(nodeId, stateKey));
      if (store.get(nodeId, stateKey) === undefined) {
        store.set(nodeId, stateKey, initialValue);
      }
    },
    isDeclared(nodeId, stateKey) {
      return declared.has(key(nodeId, stateKey));
    },
    set(nodeId, statePath, value) {
      if (!declared.has(key(nodeId, statePath))) {
        return {
          ok: false,
          error:
            `RUNTIME_STATE_SLOT_NOT_DECLARED — ${nodeId}.${statePath} はUI監視割当で宣言されていません（宣言→更新の順序が必要です）`,
        };
      }
      store.set(nodeId, statePath, value);
      return { ok: true };
    },
    get(nodeId, statePath) {
      return store.get(nodeId, statePath);
    },
  };
}

/** actionType -> literal open/close value, or "toggle" for the toggle* family. */
const UI_STATE_UPDATE_OPEN_ACTIONS: Record<string, boolean | "toggle"> = {
  openModal: true,
  closeModal: false,
  toggleModal: "toggle",
  openDrawer: true,
  closeDrawer: false,
  toggleDrawer: "toggle",
  openDialog: true,
  closeDialog: false,
  toggleDialog: "toggle",
  // SSOT wiring_lane_contract.lanes.internal_instance_wiring: a localStateMutation
  // trigger writes true to its ui-local: target — a flag write, the same "set true"
  // semantic as open* — never a business-data value the seed record does not carry.
  localStateMutation: true,
};

export type ResolvedUiStateUpdateMutation = {
  targetNodeId: string;
  statePath: string;
  action: "set" | "toggle";
  value?: unknown;
  /**
   * Present only when the authored interaction declared payloadFrom.value as an
   * "event.<path>" reference (e.g. a select's onChange writing the option the
   * user just picked into UI-local state — the write side
   * credential_category_filter uses, generic to any future consumer). Deferred
   * — never resolved here, since this function may run at render-build time
   * (renderEmission.ts), before any real event exists. Mutually exclusive with
   * a static `value`: when present, `value` is ignored and the ACTUAL value is
   * resolved later, at event-fire time, by resolveUiStateUpdateMutationValue.
   */
  valueFrom?: string;
};

/**
 * Resolve a UI状態更新 interaction (any wiringSettingCategoryOf === "ui_state_update"
 * actionType) into the normalized {targetNodeId, statePath, action, value|valueFrom}
 * mutation shape. Shared by the event-binding builder (renderEmission.ts) and
 * the lifecycle path (executeStateUpdate below) so both derive the identical
 * target/statePath/value for the same authored interaction. Falls back to
 * parsing targetRef ("ui-local:<nodeId>.<stateKey>") when no separate
 * targetNodeId/statePath is present — the shape localStateMutation entries
 * carry (SSOT wiring_lane_contract.lanes.internal_instance_wiring). Returns
 * null when neither source yields a targetNodeId (nothing to mutate).
 */
export function resolveUiStateUpdateMutation(
  w: {
    actionType: string;
    targetNodeId?: string;
    statePath?: string;
    value?: unknown;
    targetRef?: string;
    payloadFrom?: Record<string, string>;
  },
): ResolvedUiStateUpdateMutation | null {
  const fromTargetRef = !w.targetNodeId?.trim() ? parseUiLocalTargetRef(w.targetRef) : undefined;
  const targetNodeId = w.targetNodeId?.trim() || fromTargetRef?.targetNodeId;
  if (!targetNodeId) return null;
  const statePath = w.statePath?.trim() || fromTargetRef?.statePath ||
    (w.actionType === "setActiveKey" ? "activeKey" : "open");
  const openAction = UI_STATE_UPDATE_OPEN_ACTIONS[w.actionType];
  if (openAction === "toggle") {
    return { targetNodeId, statePath, action: "toggle" };
  }
  // openAction fixed-boolean actionTypes (openModal/closeModal/openDrawer/...) never take a
  // dynamic value — only a generic setState/localStateMutation may declare payloadFrom.value.
  const valueFrom = openAction === undefined && typeof w.payloadFrom?.value === "string" &&
      w.payloadFrom.value.trim()
    ? w.payloadFrom.value.trim()
    : undefined;
  if (valueFrom) {
    return { targetNodeId, statePath, action: "set", valueFrom };
  }
  const value = openAction !== undefined ? openAction : w.value;
  return { targetNodeId, statePath, action: "set", value };
}

/**
 * Resolves a mutation's actual value at APPLY time — the finalize step
 * resolveUiStateUpdateMutation intentionally defers. A static `value` (the
 * overwhelming majority of authored mutations: openModal/closeModal/toggleModal/
 * a literal setState) resolves immediately, unchanged from today. A `valueFrom`
 * mutation resolves against the REAL triggering event's payload — only
 * available at event-fire time, never at render-build time — via the SAME
 * payloadFromResolver.ts event.<path> grammar dispatchExternalPort/
 * dispatchInstanceOperation/admin_runtime payloadFrom already use, so this is
 * not a second event-payload resolution grammar. A `valueFrom` mutation fired
 * with no eventContext (the lifecycle path — initial_mount/route_enter/
 * initial_display are synthetic triggers with no real user event to read a
 * value from) fails close explicitly rather than writing `undefined`.
 */
export function resolveUiStateUpdateMutationValue(
  mutation: ResolvedUiStateUpdateMutation,
  eventContext?: { eventPayload: Record<string, unknown>; nodeValues: Record<string, unknown> },
): { ok: true; value: unknown } | { ok: false; error: string } {
  if (mutation.action === "toggle") return { ok: true, value: undefined };
  if (!mutation.valueFrom) return { ok: true, value: mutation.value };
  if (!eventContext) {
    return {
      ok: false,
      error: `RUNTIME_STATE_UPDATE_VALUE_FROM_REQUIRES_EVENT: "${mutation.valueFrom}" has no triggering event to resolve against (lifecycle trigger)`,
    };
  }
  const resolved = resolvePayloadFromSource(
    parsePayloadFromSource(mutation.valueFrom),
    eventContext.nodeValues,
    eventContext.eventPayload,
  );
  if (!resolved.ok) return { ok: false, error: resolved.error };
  return { ok: true, value: resolved.value };
}

/**
 * Apply an already-resolved UI状態更新 mutation through the guarded dispatcher.
 * This is the single write path used by both the lifecycle runner and
 * runtimeComponentFactory.emitBoundEvent — there is no separate direct-write
 * branch for the event-triggered lane. `eventContext` is required to resolve a
 * `valueFrom` mutation (see resolveUiStateUpdateMutationValue); omitted by the
 * lifecycle path, which has no real event, so a valueFrom mutation reached by
 * a lifecycle trigger fails close there rather than writing `undefined`.
 */
export function applyGuardedLocalStateMutation(
  dispatcher: RuntimeStateDispatcher,
  mutation: ResolvedUiStateUpdateMutation,
  eventContext?: { eventPayload: Record<string, unknown>; nodeValues: Record<string, unknown> },
): { ok: true } | { ok: false; error: string } {
  const valueResult = resolveUiStateUpdateMutationValue(mutation, eventContext);
  if (!valueResult.ok) return { ok: false, error: valueResult.error };
  const next = mutation.action === "toggle"
    ? !dispatcher.get(mutation.targetNodeId, mutation.statePath)
    : valueResult.value;
  return dispatcher.set(mutation.targetNodeId, mutation.statePath, next);
}

/**
 * Predeclare every UI監視割当 slot (stateJson) and every UI状態更新 target
 * referenced by any node's runtimeInteractions, BEFORE any interaction can
 * execute. This makes "declare before mutate" a real temporal guarantee for
 * both paths: an authored interaction's target is always predeclared from the
 * same node list it will execute against, while a stale/deleted-node target
 * (absent from the current node list) genuinely fails close on write.
 *
 * mutation_authority_unification: a UI状態更新 interaction's targetNodeId is
 * predeclared ONLY when that id corresponds to an actual node in `nodes` — an
 * interaction whose author-time target has since been deleted (or was never a
 * real node) is never predeclared, so it fails close on write exactly like an
 * entirely undeclared slot. Predeclaring blindly from the referenced
 * targetNodeId (regardless of whether it exists in the current node list)
 * would make stale/deleted-node references silently succeed.
 */
export function predeclareProjectionState(
  nodes: readonly WiringNode[],
  dispatcher: RuntimeStateDispatcher,
): Array<{ nodeId: string; stateKey: string }> {
  const nodeIds = new Set(nodes.map((n) => n.nodeId));
  const declaredSlots: Array<{ nodeId: string; stateKey: string }> = [];
  const pushDeclared = (
    nodeId: string,
    stateKey: string,
    initialValue: unknown,
  ) => {
    if (dispatcher.isDeclared(nodeId, stateKey)) return;
    dispatcher.declare(nodeId, stateKey, initialValue);
    declaredSlots.push({ nodeId, stateKey });
  };
  for (const node of nodes) {
    for (const binding of deriveUiWatchBindings(node)) {
      pushDeclared(binding.nodeId, binding.stateKey, binding.initialValue);
    }
  }
  for (const node of nodes) {
    for (const w of node.runtimeInteractions ?? []) {
      if (wiringSettingCategoryOf(w) !== "ui_state_update") continue;
      const resolved = resolveUiStateUpdateMutation(w);
      if (!resolved) continue;
      if (!nodeIds.has(resolved.targetNodeId)) continue;
      pushDeclared(resolved.targetNodeId, resolved.statePath, undefined);
    }
  }
  return declaredSlots;
}

/** Create a guarded dispatcher for `nodes` with UI監視割当 + UI状態更新 targets predeclared. */
export function createProjectionStateDispatcher(
  nodes: readonly WiringNode[],
  store?: RuntimeLocalStateStore,
): RuntimeStateDispatcher {
  const dispatcher = createRuntimeStateDispatcher(
    store ?? createRuntimeLocalStateStore(),
  );
  predeclareProjectionState(nodes, dispatcher);
  return dispatcher;
}

export type LifecycleEmitResult = {
  ok: boolean;
  /** True when preview mode kept the runner inert (nothing executed by design). */
  inert: boolean;
  /** `${nodeId}#${interactionIndex}` entries that executed on this emit. */
  executed: string[];
  /** Explicit fail-close errors (policy violations, loop guard, dispatcher refusals). */
  errors: string[];
};

export type UiEventEffectRunner = {
  /** Declared UI監視割当 / UI状態更新-target slots connected at runner creation (before any mutation/effect). Grows on updateNodes. */
  declaredSlots: ReadonlyArray<{ nodeId: string; stateKey: string }>;
  /** Loop-guard verdict computed from the current node list (recomputed on updateNodes). */
  cycleErrors: ReadonlyArray<string>;
  emitLifecycle(
    trigger: "initial_mount" | "route_enter" | "initial_display",
  ): LifecycleEmitResult;
  stateDispatcher: RuntimeStateDispatcher;
  /**
   * Reconcile the runner's known node list (e.g. after an SSE refresh) without
   * recreating the runner/store/fired-registry. Recomputes predeclared
   * UI状態更新 targets, the loop guard, and the policy guard against `nodes`.
   * The fired-registry is keyed by nodeId + interaction-index, so an existing
   * node's already-fired lifecycle interaction is unaffected — call
   * emitLifecycle again afterward to run any newly-appeared lifecycle
   * interaction exactly once.
   */
  updateNodes(nodes: readonly WiringNode[]): void;
};

export type UiEventEffectRunnerOptions = {
  nodes: readonly WiringNode[];
  /** Reuse an existing dispatcher (e.g. one already passed to renderEmission) instead of creating a new one. */
  dispatcher?: RuntimeStateDispatcher;
  store?: RuntimeLocalStateStore;
  previewMode?: boolean;
  /** Identity context for computeDispatchIdempotencyKey (retry_safe_dispatch_idempotency). */
  layoutId?: string | null;
  packageId?: string | null;
  /** Injectable dispatch lanes (production defaults: frontendScheduler api_command_lane). */
  dispatchExternalPort?: (spec: {
    portTargetRef: string;
    payload: Record<string, unknown>;
    outputProp?: string;
    idempotencyKey?: string;
  }) => unknown;
  dispatchInstanceOperation?: (spec: {
    instanceTargetRef: string;
    payload: Record<string, unknown>;
    outputProp?: string;
    idempotencyKey?: string;
  }) => unknown;
};

function executeStateUpdate(
  dispatcher: RuntimeStateDispatcher,
  w: WiringInteraction,
): { ok: true } | { ok: false; error: string } {
  const resolved = resolveUiStateUpdateMutation(w);
  if (!resolved) {
    return { ok: false, error: "RUNTIME_STATE_UPDATE_TARGET_MISSING" };
  }
  return applyGuardedLocalStateMutation(dispatcher, resolved);
}

/**
 * Create the runtime effect runner for a rendered projection.
 * Order guarantee: UI監視割当 (stateJson) and UI状態更新 built-in targets are
 * predeclared here, before any lifecycle emission or state mutation can run —
 * and before any event-triggered mutation reaches the shared dispatcher too,
 * as long as the caller passes this same dispatcher into renderEmission.
 */
export function createUiEventEffectRunner(
  options: UiEventEffectRunnerOptions,
): UiEventEffectRunner {
  const stateDispatcher = options.dispatcher ??
    createRuntimeStateDispatcher(
      options.store ?? createRuntimeLocalStateStore(),
    );
  const previewMode = options.previewMode === true;
  const dispatchExternal = options.dispatchExternalPort ??
    ((
      spec: {
        portTargetRef: string;
        payload: Record<string, unknown>;
        outputProp?: string;
        idempotencyKey?: string;
      },
    ) => enqueueExternalPortDispatchCommand(spec));
  const dispatchInstance = options.dispatchInstanceOperation ??
    ((
      spec: {
        instanceTargetRef: string;
        payload: Record<string, unknown>;
        outputProp?: string;
        idempotencyKey?: string;
      },
    ) => enqueueInstanceOperationDispatchCommand(spec));

  // UI監視割当 + UI状態更新-target → runtime connection: declare everything
  // (idempotent — a dispatcher reused across calls skips already-declared slots).
  // currentNodes / cycleErrors / policyViolations / declaredSlots are mutable
  // so updateNodes can reconcile the runner to a refreshed node list (e.g. SSE
  // refresh) without recreating the runner, store, or fired-registry.
  let currentNodes: readonly WiringNode[] = options.nodes;
  const declaredSlots = predeclareProjectionState(
    currentNodes,
    stateDispatcher,
  );

  // Loop guard: dependency graph resolved before execution; loops fail close.
  let cycleErrors = findSideEffectCycleErrors(currentNodes);
  // Policy guard: the same fail-close gate that blocks authoring apply also
  // blocks runtime execution (defense in depth against out-of-band persisted data).
  // Structured (nodeId+interactionIndex), never findRuntimeInteractionPolicyErrors'
  // display string[] — per-interaction correlation below must never depend on
  // human-facing label text (see the fail-close filter's own comment).
  let policyViolations = findRuntimeInteractionPolicyViolations(currentNodes);

  const fired = new Set<string>();

  const updateNodes = (nodes: readonly WiringNode[]): void => {
    currentNodes = nodes;
    declaredSlots.push(...predeclareProjectionState(nodes, stateDispatcher));
    cycleErrors = findSideEffectCycleErrors(nodes);
    policyViolations = findRuntimeInteractionPolicyViolations(nodes);
  };

  const emitLifecycle = (
    trigger: "initial_mount" | "route_enter" | "initial_display",
  ): LifecycleEmitResult => {
    if (previewMode) {
      // lifecycle_policy: preview is inert — no state writes, no dispatch.
      return { ok: true, inert: true, executed: [], errors: [] };
    }
    if (cycleErrors.length > 0) {
      return {
        ok: false,
        inert: false,
        executed: [],
        errors: [...cycleErrors],
      };
    }
    const executed: string[] = [];
    const errors: string[] = [];
    // structural_subtree_conditional_visibility_contract: a hidden node's
    // lifecycle interactions (initial_mount/route_enter/initial_display) must
    // be as unreachable as a DOM-unmounted node's click/change interactions —
    // built from the SAME currentNodes list LayoutProjectionTree renders from,
    // via the SAME resolveNodeVisibility evaluator, so DOM mount and lifecycle
    // reachability can never disagree about which nodes are active. A resolver
    // error (undeclared/malformed source) is treated the same as invisible
    // here — LayoutProjectionTree already surfaces that as a visible render
    // error; this runner does not duplicate that into its own errors[].
    const visibilityGraph = buildVisibilityGraph(currentNodes);
    for (const node of currentNodes) {
      const visibility = resolveNodeVisibility(
        node.nodeId,
        visibilityGraph,
        stateDispatcher,
      );
      if (!visibility.ok || !visibility.visible) continue;
      for (const [idx, w] of (node.runtimeInteractions ?? []).entries()) {
        if (w.trigger !== trigger || !isLifecycleTrigger(w.trigger)) continue;
        const fireKey = `${node.nodeId}#${idx}`;
        if (isBackendOrExternalDispatchAction(w.actionType)) {
          // Fail-close: unconfirmed / non-idempotent lifecycle dispatch never executes.
          // Correlated structurally (nodeId+interactionIndex — the SAME identity
          // fireKey above uses), never by matching human-facing label text: two
          // nodes sharing the identical wiringNodeDisplayLabel (e.g. same
          // componentKey) would otherwise collide on an identical string prefix,
          // letting one node's violation wrongly block — or one node's clean
          // record wrongly clear — a DIFFERENT node's interaction. See
          // findRuntimeInteractionPolicyViolations' own doc for why this must be
          // the correlation key, not findRuntimeInteractionPolicyErrors' display
          // string[].
          const own = policyViolations
            .filter((v) => v.nodeId === node.nodeId && v.interactionIndex === idx)
            .map((v) => v.message);
          if (own.length > 0) {
            errors.push(...own);
            continue;
          }
          // Idempotency: once_per_mount / dedupe_key fire at most once per runner
          // lifetime (rerender/refresh safe); refetch_on_route_enter refires only
          // on route_enter emissions.
          if (fired.has(fireKey)) {
            if (
              !(w.idempotencyPolicy === "refetch_on_route_enter" &&
                trigger === "route_enter")
            ) {
              continue;
            }
          }
          fired.add(fireKey);
          // retry_safe_dispatch_idempotency: deterministic from stable interaction
          // identity (NOT a fresh nonce), so the SAME initial_mount/route_enter
          // firing of the SAME authored interaction produces the SAME key across
          // reload/reconnect/runner-recreation — the backend ledger recognizes a
          // retry of the SAME logical dispatch rather than a new one.
          if (w.actionType === "dispatchExternalPort" && w.portTargetRef) {
            dispatchExternal({
              portTargetRef: w.portTargetRef,
              payload: {},
              outputProp: w.outputProp,
              idempotencyKey: computeDispatchIdempotencyKey({
                layoutId: options.layoutId,
                packageId: options.packageId,
                nodeId: node.nodeId,
                interactionIndex: idx,
                runtimeInteractionId: w.runtimeInteractionId,
                trigger: w.trigger,
                actionType: w.actionType,
                targetRef: w.portTargetRef,
              }),
            });
            executed.push(fireKey);
          } else if (
            w.actionType === "dispatchInstanceOperation" && w.instanceTargetRef
          ) {
            dispatchInstance({
              instanceTargetRef: w.instanceTargetRef,
              payload: {},
              outputProp: w.outputProp,
              idempotencyKey: computeDispatchIdempotencyKey({
                layoutId: options.layoutId,
                packageId: options.packageId,
                nodeId: node.nodeId,
                interactionIndex: idx,
                runtimeInteractionId: w.runtimeInteractionId,
                trigger: w.trigger,
                actionType: w.actionType,
                targetRef: w.instanceTargetRef,
              }),
            });
            executed.push(fireKey);
          }
          continue;
        }
        // UI状態更新 on lifecycle trigger: fire once per mount through the dispatcher.
        if (fired.has(fireKey)) continue;
        fired.add(fireKey);
        const result = executeStateUpdate(stateDispatcher, w);
        if (!result.ok) {
          errors.push(
            `${wiringNodeDisplayLabel(node)} #${idx + 1}: ${result.error}`,
          );
          continue;
        }
        executed.push(fireKey);
      }
    }
    return { ok: errors.length === 0, inert: false, executed, errors };
  };

  return {
    declaredSlots,
    get cycleErrors() {
      return cycleErrors;
    },
    emitLifecycle,
    stateDispatcher,
    updateNodes,
  };
}
