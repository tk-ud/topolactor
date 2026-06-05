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

export type ResponsiveTokenRulesValidationResult =
  | { ok: true; rules: ResponsiveTokenRules }
  | { ok: false; errorCode: string; message: string };

/**
 * Parse and validate raw JSON input for responsive token rules.
 * Returns ok:true with parsed rules on success, ok:false with structured error on any invalid input.
 * Empty string is treated as valid (clears the rules).
 */
export function validateResponsiveTokenRulesJson(raw: string): ResponsiveTokenRulesValidationResult {
  if (!raw.trim()) return { ok: true, rules: {} };
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return { ok: false, errorCode: "RESPONSIVE_TOKEN_RULE_JSON_INVALID", message: "JSONとして解析できません" };
  }
  if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
    return { ok: false, errorCode: "RESPONSIVE_TOKEN_RULE_JSON_INVALID", message: "JSONオブジェクトである必要があります（配列・nullは不可）" };
  }
  const record = parsed as Record<string, unknown>;
  for (const [key, value] of Object.entries(record)) {
    if (!(RESPONSIVE_BREAKPOINTS as readonly string[]).includes(key)) {
      return { ok: false, errorCode: "RESPONSIVE_TOKEN_RULE_JSON_INVALID", message: `不明なブレークポイント: "${key}"（有効値: sm, md, lg, xl）` };
    }
    if (!Array.isArray(value)) {
      return { ok: false, errorCode: "RESPONSIVE_TOKEN_RULE_JSON_INVALID", message: `"${key}" の値は文字列配列である必要があります` };
    }
    for (const item of value as unknown[]) {
      if (typeof item !== "string") {
        return { ok: false, errorCode: "RESPONSIVE_TOKEN_RULE_JSON_INVALID", message: `"${key}" の配列内に文字列以外の値があります` };
      }
    }
  }
  return { ok: true, rules: record as ResponsiveTokenRules };
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
  componentKind?: string;
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
export const DEFAULT_VISUAL_NODE_WIDTH = 140;
export const DEFAULT_VISUAL_NODE_HEIGHT = 60;

export type PaletteDraftSeedEntry = {
  componentKey: string;
  componentKind: string;
  isDraftOnly: boolean;
  componentId?: string;
  packageId?: string;
  layoutId?: string;
  wiringId?: string;
  tensorId?: string;
};

export type ParsedVisualLayoutPatch = {
  nodes: VisualNodePayload[];
  layoutClassRefs: string[];
};

export type ParseVisualLayoutPatchResult =
  | { ok: true; value: ParsedVisualLayoutPatch }
  | { ok: false; error: string };

function readPatchNode(
  raw: Record<string, unknown>,
  paletteByKey: Map<string, PaletteDraftSeedEntry>,
): VisualNodePayload | null {
  const nodeId = typeof raw.nodeId === "string" ? raw.nodeId.trim() : "";
  const componentKey = typeof raw.componentKey === "string"
    ? raw.componentKey.trim()
    : "";
  if (!nodeId || !componentKey) return null;
  const palette = paletteByKey.get(componentKey);
  const isDraftOnly = raw._draftOnly === true ||
    (palette?.isDraftOnly ?? false);
  return {
    nodeId,
    componentKey,
    isDraftOnly,
    slotKey: typeof raw.slotKey === "string" ? raw.slotKey : "",
    orderIndex: typeof raw.orderIndex === "number" ? raw.orderIndex : 0,
    parentNodeId: typeof raw.parentNodeId === "string" ? raw.parentNodeId : null,
    gridCol: typeof raw.gridCol === "number" ? raw.gridCol : 1,
    gridRow: typeof raw.gridRow === "number" ? raw.gridRow : 1,
    x: typeof raw.x === "number" ? raw.x : 0,
    y: typeof raw.y === "number" ? raw.y : 0,
    width: typeof raw.width === "number"
      ? raw.width
      : DEFAULT_VISUAL_NODE_WIDTH,
    height: typeof raw.height === "number"
      ? raw.height
      : DEFAULT_VISUAL_NODE_HEIGHT,
    componentId: typeof raw.componentId === "string"
      ? raw.componentId
      : palette?.componentId,
    packageId: typeof raw.packageId === "string"
      ? raw.packageId
      : palette?.packageId,
    layoutId: typeof raw.layoutId === "string" ? raw.layoutId : palette?.layoutId,
    wiringId: typeof raw.wiringId === "string" ? raw.wiringId : palette?.wiringId,
    tensorId: typeof raw.tensorId === "string" ? raw.tensorId : palette?.tensorId,
    componentKind: palette?.componentKind,
  };
}

/** Parse layout_patch_json / preview tensorPatchJson into canvas draft state. */
export function parseVisualLayoutPatchJson(
  rawJson: string,
  paletteEntries: PaletteDraftSeedEntry[] = [],
): ParseVisualLayoutPatchResult {
  const trimmed = rawJson.trim();
  if (!trimmed) {
    return { ok: true, value: { nodes: [], layoutClassRefs: [] } };
  }
  let parsed: unknown;
  try {
    parsed = JSON.parse(trimmed);
  } catch {
    return { ok: false, error: "TENSOR_PATCH_JSON_INVALID" };
  }
  if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
    return { ok: false, error: "TENSOR_PATCH_JSON_MUST_BE_OBJECT" };
  }
  const root = parsed as Record<string, unknown>;
  const layoutClassRefs = Array.isArray(root.layoutClassRefs)
    ? root.layoutClassRefs.filter((v): v is string =>
      typeof v === "string" && v.trim().length > 0
    )
    : [];
  const paletteByKey = new Map(
    paletteEntries.map((e) => [e.componentKey, e]),
  );
  const nodes: VisualNodePayload[] = [];
  if (Array.isArray(root.nodes)) {
    for (const item of root.nodes) {
      if (typeof item !== "object" || item === null || Array.isArray(item)) {
        continue;
      }
      const node = readPatchNode(item as Record<string, unknown>, paletteByKey);
      if (node) nodes.push(node);
    }
  }
  return { ok: true, value: { nodes, layoutClassRefs } };
}

/** Seed stacked nodes from package palette when persisted patch has no nodes yet. */
export function seedDraftNodesFromPalette(
  entries: PaletteDraftSeedEntry[],
  options: { startX?: number; startY?: number; rowGap?: number } = {},
): VisualNodePayload[] {
  const startX = options.startX ?? 20;
  const startY = options.startY ?? 20;
  const rowGap = options.rowGap ?? 72;
  const promotable = entries.filter((e) => !e.isDraftOnly);
  return promotable.map((entry, index) => ({
    nodeId: `seed_${entry.componentKey}_${index}`,
    componentKey: entry.componentKey,
    componentKind: entry.componentKind,
    isDraftOnly: false,
    slotKey: "",
    orderIndex: index,
    parentNodeId: null,
    gridCol: 1,
    gridRow: index + 1,
    x: startX,
    y: startY + index * rowGap,
    width: DEFAULT_VISUAL_NODE_WIDTH,
    height: DEFAULT_VISUAL_NODE_HEIGHT,
    componentId: entry.componentId,
    packageId: entry.packageId,
    layoutId: entry.layoutId,
    wiringId: entry.wiringId,
    tensorId: entry.tensorId,
  }));
}

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
