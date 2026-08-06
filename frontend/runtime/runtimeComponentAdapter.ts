import type { VNode } from "preact";
import type { ComponentDataHub } from "./projectionConstructor.ts";
import type { RuntimeTopologyComponentProps } from "../components/runtimeContract.ts";
import type { DispatchResponse } from "../api/dispatch.ts";
import {
  ensureRuntimeComponentRegistryInitialized,
  hasRuntimeComponentFactory,
} from "./runtimeComponentRegistry.ts";
import { resolveTopologyLayoutClassRefs } from "./topologyLayoutClassResolver.ts";

type NormalizedDesign = {
  classname?: string;
  className?: string;
  tailwind?: string;
  style?: string;
  state?: "default" | "loading" | "success" | "error";
  topologyLayoutClassRefs?: string[];
};

/**
 * Round 29: identity context accompanying a settled admin_runtime dispatch result —
 * carries the authored target_ref (dispatchTargetRefByTrigger / RuntimeDispatchSpec.targetRef)
 * that was ACTUALLY dispatched, so a caller can confirm a settled response really did land on
 * the manifest that was targeted, rather than inferring "this must be an ordinary cross-manifest
 * child response" merely because its manifestId differs from whatever the caller currently has
 * adopted. See ProjectionShell.tsx's handleRuntimeDispatchResult.
 *
 * dryRun (preview-gap round): whether the dispatched request's OWN resolved payload carried a
 * truthy `dryRun` field — mirrors backend AdminRuntimeMasterRoster.cs's own IsTruthyPayloadFlag
 * acceptance of both the JSON boolean `true` and the JSON string `"true"` (a data-defined
 * literal:true payloadFrom source always resolves to a string, never a JS boolean, at the wire —
 * see AdminRuntimeMasterRoster.cs's own comment on this exact point). This is the SAME generic
 * payload.dryRun / payload.confirmed mutation_confirmation_contract vocabulary
 * admin-normal-surface-projection-seed-ssot.yaml already declares for ANY surface's write preview
 * (team_markdown's saved-view-update contract uses the identical flag), never an operation name,
 * nodeId, manifest UUID, or admin-enum-specific field. A caller uses this to tell a genuine
 * mutating write's settled result apart from a non-mutating preview probe's settled result,
 * without inspecting response content or hardcoding either side's identity.
 */
export type RuntimeDispatchResultContext = {
  /** The dispatchSpec.targetRef that was actually sent, if any (e.g. "manifest:<uuid>:<wiringKey>"). */
  targetRef?: string;
  /** Whether the dispatched request's own resolved payload.dryRun was truthy. */
  dryRun: boolean;
};

export type RuntimeComponentSpec = {
  componentId: string;
  /**
   * round 38: the layout node's own stable structural identity, when known -- see
   * ComponentDataHub.nodeId's own doc comment for why this must be the identity any per-instance
   * runtime state (debounce timers, dispatch sequence guards) keys on instead of componentId.
   * Absent for specs built outside the normal ComponentDataHub->adaptComponentDataHub path (e.g.
   * some unit-test-constructed specs) -- callers needing instance identity must fall back to
   * componentId only when nodeId is genuinely unavailable, never prefer componentId when both exist.
   */
  nodeId?: string;
  packageId?: string | null;
  layoutId?: string | null;
  wiringId?: string | null;
  componentType: string;
  props: RuntimeTopologyComponentProps;
  eventBinding: Record<string, unknown>;
  className?: string;
  design?: NormalizedDesign;
  /** UI Builder canvas preview — no runtime event dispatch; relaxed binding checks. */
  previewMode?: boolean;
  /** frontend_local_derived_calculation_binding: fires on change/input/select BEFORE previewMode gate. */
  calcTriggerCallback?: (value: unknown) => void;
  /** frontend_local_derived_calculation_binding: prop overrides injected into placeholder props for target nodes. */
  calcValueOverrides?: Record<string, unknown>;
  /**
   * search_suggest candidate boundary: fires on "search" trigger BEFORE previewMode gate.
   * Island/page provides this to implement the onSearch → debounce → read-only provider → suggestions update loop.
   * No mutation / DB write during typing. SSOT: candidate_source_boundary: debounce_backend_readonly_search
   */
  searchCallback?: (componentId: string, query: string) => void;
  /**
   * Guarded projection-local UI state store used by runtime UI interactions
   * (modal/drawer/dialog open state, tabs/accordion activeKey, setState).
   * This is the ONLY mutation path for UI状態更新 — both the lifecycle path
   * (uiEventEffectRunner) and the event-triggered path (emitBoundEvent) write
   * through the same guarded dispatcher instance so mutation authority is not
   * duplicated. set() fails close for a slot that was never declared (via
   * UI監視割当 stateJson or a predeclared UI状態更新 target).
   * SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml component_runtime_state_effect_boundary
   */
  localStateStore?: RuntimeGuardedStateStore;
  /** Snapshot used by dispatchExternalPort payloadFrom node:<nodeId>.value resolution. */
  payloadFromNodeValues?: Record<string, unknown>;
  /**
   * round 37 (search_filter_input_contract debounce_policy): a Field's own declared
   * debounceMs (translator-authored, react-schema-topology-seed-translator-ssot.yaml
   * Field grammar) — when present and a valid positive integer, emitBoundEvent's
   * admin_runtime Lane 2 dispatch (binding.runtimeDispatch) on an "input" trigger delays
   * the actual network enqueue by this many ms, collapsing rapid keystrokes to the LAST
   * value only (a new keystroke cancels the previous pending timer). Absent/invalid on
   * every pre-existing node (no authored source sets it yet) — behavior for those nodes
   * is byte-for-byte unchanged (immediate dispatch), so this is additive/opt-in, not a
   * generic behavior change to every input.
   */
  debounceMs?: number;
  /** Fires with this node's own latest scalar value on a value-bearing trigger (change/input/select). */
  onNodeValueChange?: (value: unknown) => void;
  /** Fires with the settled result of this node's own admin_runtime dispatch. See ComponentDataHub. */
  onRuntimeDispatchResult?: (
    result: DispatchResponse,
    context: RuntimeDispatchResultContext,
  ) => void;
  /**
   * Round 25: the REAL nested schema children (e.g. a Modal's Confirm/Cancel Action children),
   * as already-rendered VNodes — populated by components/LayoutProjectionTree.tsx's
   * renderRuntimeComponent() call, never by adaptComponentDataHub (which has no notion of the
   * schema tree's parent/child structure). Only present when
   * RuntimeComponentFactory.acceptsAuthoredChildren is true for this componentKind and this node
   * has at least one real child in the composed layout tree.
   */
  authoredChildren?: VNode[];
};

/** Raw projection-local state store (read/write, no guard). Wrapped by RuntimeGuardedStateStore. */
export type RuntimeLocalStateStore = {
  get(targetNodeId: string, statePath: string): unknown;
  set(targetNodeId: string, statePath: string, value: unknown): void;
};

/**
 * Guarded mutation surface over a RuntimeLocalStateStore: set() fails close for
 * undeclared slots instead of writing silently. declare()/isDeclared() enforce
 * the "UI監視割当 before UI状態更新" ordering. Implemented by
 * frontend/runtime/uiEventEffectRunner.ts createRuntimeStateDispatcher.
 */
export type RuntimeGuardedStateStore = {
  get(targetNodeId: string, statePath: string): unknown;
  set(
    targetNodeId: string,
    statePath: string,
    value: unknown,
  ): { ok: true } | { ok: false; error: string };
  declare(targetNodeId: string, statePath: string, initialValue: unknown): void;
  isDeclared(targetNodeId: string, statePath: string): boolean;
};

type AdaptResult = { ok: true; value: RuntimeComponentSpec } | {
  ok: false;
  error: string;
};

function extractTopologyLayoutClassRefs(
  value: unknown,
): string[] | undefined {
  if (!Array.isArray(value)) return undefined;
  const refs = value.filter((x): x is string =>
    typeof x === "string" && x.trim().length > 0
  );
  return refs.length > 0 ? refs : undefined;
}

function normalizeDesign(
  value: unknown,
): { ok: true; value: NormalizedDesign; className?: string } | {
  ok: false;
  error: string;
} {
  if (value === undefined || value === null) return { ok: true, value: {} };
  if (typeof value !== "object" || Array.isArray(value)) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_INVALID_DESIGN" };
  }
  const raw = value as Record<string, unknown>;
  const topologyLayoutClassRefs = extractTopologyLayoutClassRefs(
    raw.topologyLayoutClassRefs,
  );
  const classname = typeof raw.classname === "string"
    ? raw.classname.trim()
    : undefined;
  const className = typeof raw.className === "string"
    ? raw.className.trim()
    : undefined;
  const tailwind = typeof raw.tailwind === "string"
    ? raw.tailwind.trim()
    : undefined;
  const style = typeof raw.style === "string" ? raw.style : undefined;
  const stateRaw = raw.state;
  const state = stateRaw === "default" || stateRaw === "loading" ||
      stateRaw === "success" || stateRaw === "error"
    ? stateRaw
    : undefined;
  if (stateRaw !== undefined && state === undefined) {
    return {
      ok: false,
      error: "RUNTIME_COMPONENT_ADAPTER_INVALID_DESIGN_STATE",
    };
  }

  if (topologyLayoutClassRefs) {
    const resolved = resolveTopologyLayoutClassRefs(topologyLayoutClassRefs);
    if (!resolved.ok) {
      return { ok: false, error: resolved.error };
    }
    return {
      ok: true,
      value: { topologyLayoutClassRefs, style, state },
      className: resolved.className,
    };
  }

  const merged = [classname, className, tailwind].filter((v): v is string =>
    Boolean(v && v.length > 0)
  ).join(" ").trim();
  return {
    ok: true,
    value: { classname, className, tailwind, style, state },
    className: merged.length > 0 ? merged : undefined,
  };
}

function normalizeTopologyProps(
  props: Record<string, unknown>,
): RuntimeTopologyComponentProps {
  return props as RuntimeTopologyComponentProps;
}

export function adaptComponentDataHub(hub: ComponentDataHub): AdaptResult {
  if (!hub.componentId || hub.componentId.trim().length === 0) {
    return {
      ok: false,
      error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_ID",
    };
  }
  if (!hub.componentKind || hub.componentKind.trim().length === 0) {
    return {
      ok: false,
      error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_KIND",
    };
  }
  ensureRuntimeComponentRegistryInitialized();
  if (!hasRuntimeComponentFactory(hub.componentKind)) {
    return {
      ok: false,
      error:
        `RUNTIME_COMPONENT_ADAPTER_UNSUPPORTED_COMPONENT_KIND: ${hub.componentKind}`,
    };
  }
  if (
    typeof hub.props !== "object" || hub.props === null ||
    Array.isArray(hub.props)
  ) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_INVALID_PROPS" };
  }
  if (
    typeof hub.eventBinding !== "object" || hub.eventBinding === null ||
    Array.isArray(hub.eventBinding)
  ) {
    return {
      ok: false,
      error: "RUNTIME_COMPONENT_ADAPTER_INVALID_EVENT_BINDING",
    };
  }
  const normalizedDesign = normalizeDesign(hub.design);
  if (!normalizedDesign.ok) return normalizedDesign;
  const design = normalizedDesign.value;
  let className = normalizedDesign.className;

  const props = hub.props as RuntimeTopologyComponentProps;
  const layoutCssRefs = props.layoutCss?.topologyLayoutClassRefs;
  if (layoutCssRefs?.length) {
    const resolved = resolveTopologyLayoutClassRefs(layoutCssRefs);
    if (!resolved.ok) return { ok: false, error: resolved.error };
    className = resolved.className;
  }

  return {
    ok: true,
    value: {
      componentId: hub.componentId,
      nodeId: hub.nodeId,
      packageId: hub.packageId,
      layoutId: hub.layoutId,
      wiringId: hub.wiringId,
      componentType: hub.componentKind,
      props: normalizeTopologyProps(hub.props),
      eventBinding: hub.eventBinding,
      localStateStore: hub.localStateStore,
      payloadFromNodeValues: hub.payloadFromNodeValues,
      onNodeValueChange: hub.onNodeValueChange,
      onRuntimeDispatchResult: hub.onRuntimeDispatchResult,
      debounceMs: hub.debounceMs,
      className,
      design,
    },
  };
}
