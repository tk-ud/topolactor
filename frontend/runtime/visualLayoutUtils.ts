/**
 * Visual layout canvas utilities.
 * Pure functions extracted for testability.
 * SSOT: docs/registrar-admin-ui-specification.md §5
 */

export const RESPONSIVE_BREAKPOINTS = ["sm", "md", "lg", "xl"] as const;
export type BreakpointKey = (typeof RESPONSIVE_BREAKPOINTS)[number];
export type ResponsiveTokenRules = Partial<Record<string, string[]>>;

/**
 * Strip breakpoints with empty token lists from a responsive rule map.
 * Used before submitting to backend to avoid sending empty breakpoint entries.
 */
export function filterEmptyResponsiveRules(rules: ResponsiveTokenRules): Record<string, string[]> {
  const out: Record<string, string[]> = {};
  for (const [bp, tokens] of Object.entries(rules)) {
    if (tokens && tokens.length > 0) out[bp] = tokens;
  }
  return out;
}

// Minimal node shape for patch builder — compatible with DraftNode in UiBuilderAdmin.tsx.
export interface VisualNodePayload {
  nodeId: string;
  componentKey: string;
  isDraftOnly: boolean;
  slotKey: string;
  orderIndex: number;
  parentNodeId: string | null;
  gridCol: number;
  gridRow: number;
  x: number;
  y: number;
  width: number;
  height: number;
  componentId?: string;
  packageId?: string;
  layoutId?: string;
  wiringId?: string;
  tensorId?: string;
}

/**
 * Snap a value to the nearest grid increment.
 */
export function snapToGrid(value: number, snapSize: number): number {
  if (snapSize <= 0) return value;
  return Math.round(value / snapSize) * snapSize;
}

/**
 * Build the backend layout_patch payload JSON including visual coordinates.
 * Extends buildLayoutPatchJson with x/y/width/height per node.
 * Frontend authority: draft layout state + intent submission only.
 * Actual topology persistence authority: backend / DB (topology.components_layout_design).
 */
export function buildVisualLayoutPatchJson(
  nodes: VisualNodePayload[],
  layoutClassRefs: string[] = [],
): string {
  return JSON.stringify(
    {
      grid: { cols: 12 },
      ...(layoutClassRefs.length > 0 ? { layoutClassRefs } : {}),
      nodes: nodes.map((n) => ({
        nodeId: n.nodeId,
        componentKey: n.componentKey,
        ...(n.isDraftOnly ? { _draftOnly: true } : {}),
        ...(n.componentId ? { componentId: n.componentId } : {}),
        ...(n.packageId ? { packageId: n.packageId } : {}),
        ...(n.layoutId ? { layoutId: n.layoutId } : {}),
        ...(n.wiringId ? { wiringId: n.wiringId } : {}),
        ...(n.tensorId ? { tensorId: n.tensorId } : {}),
        slotKey: n.slotKey || null,
        orderIndex: n.orderIndex,
        parentNodeId: n.parentNodeId || null,
        gridCol: n.gridCol,
        gridRow: n.gridRow,
        x: n.x,
        y: n.y,
        width: n.width,
        height: n.height,
      })),
    },
    null,
    2,
  );
}

/** Returns nodes that are draft-only and would block apply. */
export function getDraftOnlyNodes(nodes: VisualNodePayload[]): VisualNodePayload[] {
  return nodes.filter((n) => n.isDraftOnly);
}

/** True if any node would block apply due to draft-only status. */
export function isDraftOnlyApplyBlocked(nodes: VisualNodePayload[]): boolean {
  return nodes.some((n) => n.isDraftOnly);
}

/**
 * Detect parent cycle that would form if nodeId were reparented to proposedParentId.
 */
export function wouldCreateVisualParentCycle(
  nodes: VisualNodePayload[],
  nodeId: string,
  proposedParentId: string | null,
): boolean {
  if (!proposedParentId) return false;
  let current: string | null = proposedParentId;
  while (current) {
    if (current === nodeId) return true;
    const parent = nodes.find((n) => n.nodeId === current);
    current = parent?.parentNodeId ?? null;
  }
  return false;
}
