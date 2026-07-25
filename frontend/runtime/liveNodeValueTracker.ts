/**
 * liveNodeValueTracker — surface-independent stable-node-identity value store.
 *
 * SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
 * lane_storage_boundary.known_gaps.remaining_write_payload_capture_gap.
 *
 * Production ProjectionShell.tsx never populated renderEmission's
 * payloadFromNodeValues option (grep-confirmed zero references before this
 * module existed), so payloadFrom `node:<nodeId>.value` resolution
 * (payloadFromResolver.ts) was unproven for any production surface. This
 * tracker is the register/update/deregister primitive that closes that gap —
 * mirrors the shape of UiBuilderAdmin.tsx's canvas-only nodeValues state
 * (handleNodeValueChange), but flat (one scalar per nodeId, matching
 * payloadFromNodeValues' own Record<string, unknown> shape) and reusable by
 * any production projection surface, not just UI Builder canvas preview.
 *
 * snapshot() returns the SAME backing object on every call (not a copy) so
 * that a runtimeSpec closure captured at an earlier renderEmission() call
 * still observes later set()/reconcile() mutations without requiring a new
 * render — the same "shared instance, not a copy" pattern already used by
 * RuntimeGuardedStateStore (uiEventEffectRunner.ts) elsewhere in this file's
 * lineage.
 *
 * Own-property identity: the backing store is created with Object.create(null)
 * (no Object.prototype in its chain), and existence is checked with
 * Object.prototype.hasOwnProperty.call(...), never bracket-index truthiness or
 * the `in` operator. A nodeId that happens to collide with an inherited
 * Object.prototype key (e.g. "constructor", "toString", "hasOwnProperty")
 * must resolve as genuinely missing, never as an inherited function value —
 * see payloadFromResolver.ts's matching own-property fix for the read side of
 * this same identity contract.
 *
 * Why this is a standalone primitive rather than reusing the existing local
 * state substrate (uiEventEffectRunner.ts createRuntimeLocalStateStore /
 * createRuntimeStateDispatcher — PR #599 review round 5 asked this question
 * explicitly, so the answer is recorded here instead of only in a PR comment):
 *   - createRuntimeStateDispatcher (RuntimeGuardedStateStore) requires
 *     declare(nodeId, stateKey, initialValue) before any set() — set() on an
 *     undeclared slot fails RUNTIME_STATE_SLOT_NOT_DECLARED. That "UI監視割当
 *     before UI状態更新" ordering fits a fixed, authoring-time-known set of
 *     state slots. Live node values are the opposite: any node carrying a
 *     value-bearing trigger can start reporting a value the first time it
 *     fires, with no predeclaration step, and the node set itself changes
 *     across SSE refreshes. Forcing a declare() call per node on every mount/
 *     refresh would require re-deriving the same reconcile() logic this
 *     module already provides, just to satisfy a guard this store doesn't
 *     need.
 *   - The raw store under it (RuntimeLocalStateStore: get/set only, see
 *     runtimeComponentAdapter.ts) exposes no iteration or delete — reconcile()
 *     (drop a value for a node no longer present) cannot be built on top of it
 *     without widening that shared interface, which would ripple into every
 *     other RuntimeLocalStateStore consumer (the lifecycle effect runner, the
 *     UI状態更新 dispatcher) for a need only this tracker has.
 *   - This tracker also does not fire change listeners on set() the way
 *     NotifyingRuntimeLocalStateStore does — a value-bearing keystroke updates
 *     the tracker for later dispatch/display-override use, not to trigger a
 *     reactive UI状態更新-style re-render loop.
 * These are genuine interface-shape mismatches, not convenience — reusing
 * either layer means changing what it guarantees for its existing callers.
 */

export type LiveNodeValueTracker = {
  /** Registers or updates nodeId's current value. No-op for an empty nodeId. */
  set(nodeId: string, value: unknown): void;
  /** Returns the live backing store — same reference across calls. */
  snapshot(): Record<string, unknown>;
  /**
   * Deregisters any tracked nodeId absent from currentNodeIds — keeps a
   * removed/replaced node's stale value from leaking into a later dispatch
   * (SSE refresh / node reconciliation boundary).
   */
  reconcile(currentNodeIds: readonly string[]): void;
};

export function createLiveNodeValueTracker(): LiveNodeValueTracker {
  const store: Record<string, unknown> = Object.create(null);
  return {
    set(nodeId, value) {
      if (!nodeId) return;
      store[nodeId] = value;
    },
    snapshot() {
      return store;
    },
    reconcile(currentNodeIds) {
      const keep = new Set(currentNodeIds);
      for (const key of Object.keys(store)) {
        if (!keep.has(key)) delete store[key];
      }
    },
  };
}
