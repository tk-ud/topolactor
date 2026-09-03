/**
 * Generic schema-composed structural subtree conditional visibility evaluator.
 *
 * SSOT: docs/design/runtime-orchestration-ssot.yaml
 * ui_projection_render_reachability_contract.structural_subtree_conditional_visibility_contract.
 *
 * A LayoutNode (structural_node Category/Section only — see the SSOT's
 * authored_record_type_scope) may carry a visibilityBinding {source, matchValue}.
 * `source` is a "ui-local:<nodeId>.<stateKey>" reference into the SAME
 * projection-local RuntimeGuardedStateStore already used for UI状態更新/UI監視割当
 * (frontend/runtime/uiEventEffectRunner.ts). A node is visible only when its own
 * binding (if any) AND every ancestor's own binding (if any) resolve their source
 * slot's live value to strict-equal matchValue — this is the ONE resolver both
 * frontend/components/LayoutProjectionTree.tsx (DOM mount) and
 * frontend/runtime/uiEventEffectRunner.ts (lifecycle interaction reachability)
 * call, so a hidden subtree is unreachable from both surfaces via the identical
 * semantics, never two independently-maintained checks that could drift apart.
 *
 * This module never reads or writes DOM, never dispatches, and never mutates the
 * state store — read-only evaluation over data already resolved elsewhere. Kept
 * import-free of uiEventEffectRunner.ts (which itself imports this module to gate
 * lifecycle reachability) to avoid a circular module dependency; the
 * "ui-local:<nodeId>.<stateKey>" parse below mirrors
 * uiEventEffectRunner.ts's own parseUiLocalTargetRef exactly (same regex, same
 * shape) rather than sharing an import.
 */

export type VisibilityBinding = {
  source: string;
  matchValue: unknown;
};

/** Minimal per-node shape the resolver needs — satisfied structurally by both ComponentSpec and WiringNode. */
export type VisibilityGraphNode = {
  nodeId: string;
  parentNodeId?: string | null;
  visibilityBinding?: VisibilityBinding | null;
};

export type VisibilityStateReader = {
  isDeclared(targetNodeId: string, statePath: string): boolean;
  get(targetNodeId: string, statePath: string): unknown;
};

export type VisibilityResolution =
  | { ok: true; visible: boolean }
  | { ok: false; error: string };

/** Mirrors uiEventEffectRunner.ts's UI_LOCAL_TARGET_REF_RE / parseUiLocalTargetRef exactly. */
const UI_LOCAL_TARGET_REF_RE = /^ui-local:([^.]+)\.(.+)$/;

function parseVisibilitySource(
  source: string,
): { targetNodeId: string; statePath: string } | undefined {
  const trimmed = source.trim();
  if (!trimmed) return undefined;
  const match = UI_LOCAL_TARGET_REF_RE.exec(trimmed);
  if (!match) return undefined;
  return { targetNodeId: match[1], statePath: match[2] };
}

/** Builds a nodeId -> node lookup once per specs/nodes array (cheap O(n), no memoization needed at manifest scale). */
export function buildVisibilityGraph(
  nodes: readonly VisibilityGraphNode[],
): Map<string, VisibilityGraphNode> {
  const byId = new Map<string, VisibilityGraphNode>();
  for (const n of nodes) {
    if (n.nodeId) byId.set(n.nodeId, n);
  }
  return byId;
}

/**
 * Resolves whether `nodeId` is visible: walks nodeId itself plus every ancestor
 * (via parentNodeId) up to the root, collecting each one's own visibilityBinding
 * (most nodes carry none — absent is always visible for that link in the chain,
 * per default_visible_when_unbound). Every collected binding is validated first
 * (fail-close on a malformed source shape or an undeclared source slot — never
 * silently treated as visible or hidden); only once every binding on the chain
 * validates does this evaluate the strict-equality AND across all of them.
 */
export function resolveNodeVisibility(
  nodeId: string,
  graph: ReadonlyMap<string, VisibilityGraphNode>,
  store: VisibilityStateReader | undefined,
): VisibilityResolution {
  const bindings: VisibilityBinding[] = [];
  let current = graph.get(nodeId);
  const seen = new Set<string>();
  while (current) {
    if (seen.has(current.nodeId)) break; // defensive cycle guard; malformed parentNodeId graphs are a StructureMapResolver concern, not this evaluator's
    seen.add(current.nodeId);
    if (current.visibilityBinding) bindings.push(current.visibilityBinding);
    current = current.parentNodeId ? graph.get(current.parentNodeId) : undefined;
  }

  if (bindings.length === 0) return { ok: true, visible: true };
  if (!store) {
    // A binding is authored but no state store was wired in at all (e.g. a
    // draft/inspection caller that never threads one through) — this is the
    // same "source slot cannot possibly be declared" shape as an undeclared
    // slot, so it fails close identically rather than defaulting to visible.
    return {
      ok: false,
      error: `STRUCTURAL_VISIBILITY_BINDING_SOURCE_UNDECLARED:${bindings[0].source}`,
    };
  }

  const resolved: Array<{ targetNodeId: string; statePath: string; matchValue: unknown }> = [];
  for (const binding of bindings) {
    const parsed = parseVisibilitySource(binding.source);
    if (!parsed) {
      return {
        ok: false,
        error: `STRUCTURAL_VISIBILITY_BINDING_INVALID:${binding.source}`,
      };
    }
    if (!store.isDeclared(parsed.targetNodeId, parsed.statePath)) {
      return {
        ok: false,
        error: `STRUCTURAL_VISIBILITY_BINDING_SOURCE_UNDECLARED:${binding.source}`,
      };
    }
    resolved.push({ ...parsed, matchValue: binding.matchValue });
  }

  const visible = resolved.every((r) =>
    store.get(r.targetNodeId, r.statePath) === r.matchValue
  );
  return { ok: true, visible };
}
