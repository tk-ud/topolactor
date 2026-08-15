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

import { parsePayloadFromSource } from "./payloadFromResolver.ts";

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
  /**
   * Drops EVERY tracked value, bound or unbound (round 29). A settled write's
   * canonical reread makes the DB the sole authority for everything the
   * surface currently displays — not just the fields seedTrackerFromPropBindingsValue
   * happens to re-seed (propBindings-bound fields). A typed value in a field with NO
   * propBindings (e.g. a still-open create/update form's free-typed input) would
   * otherwise survive reconcile()+seed unchanged (reconcile only drops nodes ABSENT
   * from the new layout; seed only ever ADDS/overwrites propBindings-bound nodeIds) and
   * could be resent stale on a LATER, unrelated write. Call before reconcile()+seed on
   * canonical_reread; never on a passive refresh, where an in-progress edit must survive.
   */
  clear(): void;
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
    clear() {
      for (const key of Object.keys(store)) {
        delete store[key];
      }
    },
  };
}

/**
 * Selected-row-relative field prefill (admin-enum subBundle closure round,
 * .agent/tasks/todo.md admin-surface-topology-seed-conversion). Generic, surface-independent:
 * composes two pre-existing mechanisms rather than adding a new one. A node's
 * propBindings.value.source may be a `node:<id>.value.<path>` reference (the SAME grammar
 * payloadFrom already resolves at dispatch time — docs/design/ui-builder-preset-ecosystem-ssot.yaml
 * payloadFrom_resolver_contract.recognized_source_patterns.node_value_path,
 * frontend/runtime/payloadFromResolver.ts) instead of an `emission.data...` path. When the
 * REFERENCED node's own tracked value changes (e.g. a table row select), every node whose
 * propBindings.value.source points at it gets its OWN tracker slot overwritten with the
 * freshly-resolved value — reusing the tracker's existing per-nodeId storage, so
 * renderEmission.ts's pre-existing applyLiveNodeValueOverride (which already re-displays a
 * node's OWN tracked value every render) picks it up with no new rendering-layer code.
 *
 * Called ONLY in direct response to the source node's own value actually changing (see
 * ProjectionShell.tsx's handleNodeValueChange) — never on an unrelated rerender (SSE passive
 * refresh, an unrelated local-state mutation) — so an in-progress edit in a dependent field is
 * never clobbered by anything other than a genuine new selection of the node it depends on.
 * This mirrors seedTrackerFromPropBindingsValue's own forceOverwrite semantics: selecting a
 * DIFFERENT row is an explicit "load this record's values" action, so it intentionally
 * overwrites whatever the user had typed for the previously-selected row — the same
 * "explicit reselection overwrites, passive background events never do" boundary
 * seedTrackerFromPropBindingsValue's own forceOverwrite option already established.
 *
 * Returns the set of dependent nodeIds actually updated (empty when nothing depends on
 * changedNodeId) so the caller knows whether a re-render is warranted.
 */
export function cascadeNodeValueReferences(
  changedNodeId: string,
  layoutNodes: readonly LayoutNodeForValueSeeding[] | undefined,
  tracker: LiveNodeValueTracker,
  resolveNodeValueSource: (
    source: string,
    nodeValues: Record<string, unknown>,
  ) => { ok: true; value: unknown } | { ok: false },
): string[] {
  if (!changedNodeId || !layoutNodes || layoutNodes.length === 0) return [];
  const updated: string[] = [];
  // Snapshot BEFORE any writes in this pass — every dependent node resolves against the SAME
  // consistent view of the tracker (the just-changed source value plus whatever else was
  // already tracked), not a partially-updated one from an earlier node in this same loop.
  const trackerSnapshot = tracker.snapshot();
  for (const node of layoutNodes) {
    if (!node.nodeId) continue;
    const source = node.propBindings?.value?.source;
    if (typeof source !== "string" || !source) continue;
    // Only a node whose OWN source actually references changedNodeId is a dependent of it --
    // parsed via the SAME node_value grammar payloadFromResolver.ts already owns, not a
    // startsWith/substring check (which could false-positive on a nodeId that is a prefix of a
    // different one, e.g. "enum_table" vs "enum_table_2"). Without this check, EVERY
    // propBindings.value-bound node would be blindly re-resolved on EVERY node's value change --
    // including a node whose source references some OTHER node being re-resolved in reaction to
    // ITS OWN change (e.g. typing into enum_update_group_name_input itself would immediately
    // re-derive its value from enum_table again, discarding the keystroke that just landed).
    const parsedSource = parsePayloadFromSource(source);
    if (parsedSource.kind !== "node_value" || parsedSource.nodeId !== changedNodeId) {
      continue;
    }
    const resolved = resolveNodeValueSource(source, trackerSnapshot);
    if (!resolved.ok) continue;
    // Own-property identity: matches this module's existing hasOwnProperty contract.
    tracker.set(node.nodeId, resolved.value);
    updated.push(node.nodeId);
  }
  return updated;
}

/**
 * form_input/search_input propBindings.value pre-fill (admin-write-surface-selection-
 * context-and-mode-composition-gap Bundle, .agent/tasks/todo.md) resolves an initial
 * displayed value from emission.data via the SAME generic resolvePropBindings path
 * table/card_list/json_viewer already use (propBindingResolver.ts
 * COMPONENT_ARRAY_PROP_CAPABILITIES / acceptsNonArrayResolvedValue). That resolution only
 * affects what renderEmission() puts in a node's rendered `value` PROP; it does not, by
 * itself, teach liveNodeValueTracker about that value — node:<nodeId>.value payloadFrom
 * resolution (payloadFromResolver.ts, used by preview/confirm buttons) reads ONLY the
 * tracker, which otherwise only gets values from actual onChange keystrokes
 * (renderEmission.ts's onNodeValueChange wiring). Without this seeding step, a user who
 * accepts a pre-filled value without editing it would still fail dispatch with
 * PAYLOAD_FROM_NODE_NOT_FOUND on Preview/Confirm.
 *
 * By default, only seeds a nodeId that has NO existing tracked value — never overwrites
 * a value the user has already started editing during a passive background refresh
 * (initial mount / SSE refresh call sites, which pass no options and keep this
 * untouched-only behavior).
 *
 * options.forceOverwrite (PR #600 review round 12): an explicit, user-triggered
 * re-Load of a DIFFERENT record (adopting a node's own admin_runtime dispatch result,
 * see ProjectionShell.tsx's onRuntimeDispatchResult handler) is NOT a passive refresh —
 * it must overwrite a still-tracked stale value from an earlier Load, otherwise the
 * displayed pre-fill and the tracker's dispatch-payload authority diverge (the field
 * SHOWS the newly loaded record's value via propBindings, but Preview/Confirm would
 * still dispatch the PREVIOUS record's stale tracked value). Pass true only from that
 * explicit-adoption boundary; the passive-refresh call sites must keep the untouched-
 * only default so an in-progress edit during a background SSE refresh is never clobbered.
 *
 * Call once per fresh emission (alongside reconcile()), never on every unrelated
 * re-render, so it can't fight a keystroke that landed the same tick.
 */
export function seedTrackerFromPropBindingsValue(
  tracker: LiveNodeValueTracker,
  layoutNodes: readonly LayoutNodeForValueSeeding[],
  emissionData: Record<string, unknown>,
  resolveRuntimeDataPath: (
    data: Record<string, unknown>,
    source: string,
  ) => unknown,
  options?: { forceOverwrite?: boolean },
): void {
  const forceOverwrite = options?.forceOverwrite ?? false;
  const tracked = tracker.snapshot();
  for (const node of layoutNodes) {
    if (!node.nodeId || node.componentKind !== "form_input/search_input") continue;
    if (
      !forceOverwrite &&
      Object.prototype.hasOwnProperty.call(tracked, node.nodeId)
    ) continue;
    const binding = node.propBindings?.value;
    if (!binding || typeof binding.source !== "string") continue;
    const resolved = resolveRuntimeDataPath(emissionData, binding.source);
    if (typeof resolved === "string") tracker.set(node.nodeId, resolved);
  }
}

/**
 * round 37: seeds an empty-string default for every Field-family node (componentKind starting
 * with "form_input/") that owns a dispatchPayloadFromByTrigger and is not yet tracked -- closes
 * an ordering-dependency gap this round's own combined search+filter payloadFrom introduced
 * (see docs/design/admin-master-roster-management-ssot.yaml canonical_routes.admin_enums.
 * ux_contract_shrinkage_owner_decision_pending's sibling note and
 * frontend/tests/projectionShellAdminRuntimeWritePayloadCapture.test.ts's "selecting
 * enum_group_filter" DOM test): when a Field's own payloadFrom references ANOTHER Field's value
 * (e.g. enum_search's payloadFrom now also reads node:enum_group_filter.value, and vice versa, so
 * either field's own change dispatch carries both current values combined), the referenced field
 * previously had NO tracked value at all until a user had ALREADY typed/selected into it at least
 * once, so touching field B before ever touching field A failed closed
 * (PAYLOAD_FROM_NODE_NOT_FOUND) even though "field A was simply never touched" is a legitimate,
 * common state whose natural payload value is an empty string -- not a missing-record error like
 * an unselected table row (node:<table>.value.<field>, deliberately NOT seeded here or anywhere
 * else, and still correctly fails closed).
 * Scoped strictly to Field-family componentKinds with their OWN dispatchPayloadFromByTrigger --
 * never touches a Table/Action node's tracked value, and never overwrites an already-tracked
 * value (a user's real keystroke always wins over this one-time empty default).
 */
export function seedTrackerWithEmptyDefaultsForFieldDispatchParticipants(
  tracker: LiveNodeValueTracker,
  layoutNodes: readonly LayoutNodeForValueSeeding[],
): void {
  const tracked = tracker.snapshot();
  for (const node of layoutNodes) {
    if (!node.nodeId) continue;
    if (!node.componentKind?.startsWith("form_input/")) continue;
    if (!node.dispatchPayloadFromByTrigger) continue;
    if (Object.prototype.hasOwnProperty.call(tracked, node.nodeId)) continue;
    tracker.set(node.nodeId, "");
  }
}

/** Minimal shape seedTrackerFromPropBindingsValue needs from a LayoutNode (api/dispatch.ts). */
export type LayoutNodeForValueSeeding = {
  nodeId?: string;
  componentKind?: string;
  propBindings?: Record<string, { source?: string }> | null;
  dispatchPayloadFromByTrigger?: unknown;
};
