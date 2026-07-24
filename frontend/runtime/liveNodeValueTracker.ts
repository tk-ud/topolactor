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
  const store: Record<string, unknown> = {};
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
