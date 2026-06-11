/**
 * Visual layout canvas utilities.
 * Pure functions extracted for testability.
 * SSOT: docs/registrar-admin-ui-specification.md §5
 */
import type { CalcBinding } from "./frontendLocalCalculationResolver.ts";

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

/** SSOT: admin-console-workflow-ssot v0.8.0 layout_editor structural_html allowlist — full set */
export const STRUCTURAL_HTML_TAG_ALLOWLIST = [
  // block
  "div", "section", "article", "aside", "header", "footer", "main", "nav",
  // heading
  "h1", "h2", "h3", "h4", "h5", "h6",
  // text
  "p", "span", "strong", "em", "blockquote", "pre", "code",
  // link
  "a",
  // form
  "form", "fieldset", "legend", "label", "button", "input", "textarea", "select", "option",
  // media
  "img", "picture", "figure", "figcaption", "video", "audio",
  // list
  "ul", "ol", "li", "dl", "dt", "dd",
  // table
  "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption",
] as const;
export type StructuralHtmlTag = (typeof STRUCTURAL_HTML_TAG_ALLOWLIST)[number];
export const STRUCTURAL_HTML_COMPONENT_KEY = "layout/structural_html";
export type LayoutNodeKind = "catalog_component" | "structural_html";

/** Canvas width/height: px number, CSS percent (e.g. "50%"), or "auto". */
export type LayoutDimension = number | string;

/**
 * Controls whether width/height is applied as inline style.
 *   auto   — component default size for display-only; not saved as inline style.
 *   preset — sizing layoutClassRef drives width; inline style suppressed.
 *   custom — explicit user value; saved and projected as inline style.
 * Absent (undefined) → backward-compat: behaves like "custom" (always projected).
 */
export type SizingMode = "auto" | "preset" | "custom";

/** layout.width.* classKey prefix — all sizing class refs use this namespace. */
const SIZING_WIDTH_KEY_PREFIX = "layout.width.";

/** Returns true when any classRef is a sizing width class (layout.width.*). */
export function hasSizingWidthClassRef(classRefs: readonly string[]): boolean {
  return classRefs.some((k) => k.trim().startsWith(SIZING_WIDTH_KEY_PREFIX));
}

/**
 * Derive widthMode after a layoutClassRefs toggle.
 * - sizing classRef present → "preset"
 * - no sizing classRef and current mode was "custom" → keep "custom"
 * - otherwise → "auto"
 */
export function resolveSizingModeAfterToggle(
  classRefsAfterToggle: readonly string[],
  currentMode: SizingMode | undefined,
): SizingMode {
  if (hasSizingWidthClassRef(classRefsAfterToggle)) return "preset";
  return currentMode === "custom" ? "custom" : "auto";
}

export function isPercentDimension(dim: LayoutDimension): boolean {
  return typeof dim === "string" && dim.trim().endsWith("%");
}

export function isAutoDimension(dim: LayoutDimension): boolean {
  return typeof dim === "string" && dim.trim().toLowerCase() === "auto";
}

export function formatLayoutDimensionCss(
  dim: LayoutDimension | undefined,
): string | undefined {
  if (dim === undefined) return undefined;
  if (typeof dim === "number") return `${dim}px`;
  const s = dim.trim();
  if (!s) return undefined;
  if (s.endsWith("%") || s.endsWith("px") || s.toLowerCase() === "auto") return s;
  const n = parseFloat(s);
  if (!Number.isNaN(n)) return `${n}px`;
  return s;
}

/** Parse inspector input: "120", "50%", "auto", etc. Returns null when invalid. */
export function parseLayoutDimensionInput(raw: string): LayoutDimension | null {
  const s = raw.trim();
  if (!s) return null;
  if (s.toLowerCase() === "auto") return "auto";
  if (s.endsWith("%")) {
    const pct = parseFloat(s.slice(0, -1));
    if (Number.isNaN(pct) || pct < 0) return null;
    return `${pct}%`;
  }
  const v = parseInt(s, 10);
  if (Number.isNaN(v)) return null;
  return v;
}

export function resolveLayoutDimensionPx(
  dim: LayoutDimension,
  canvasSize: number,
  fallback: number,
): number {
  if (typeof dim === "number") return dim;
  const s = dim.trim();
  if (s.toLowerCase() === "auto") return fallback;
  if (s.endsWith("%")) {
    const pct = parseFloat(s.slice(0, -1));
    if (Number.isNaN(pct)) return fallback;
    return Math.round((canvasSize * pct) / 100);
  }
  const n = parseFloat(s);
  return Number.isNaN(n) ? fallback : Math.round(n);
}

export function readLayoutDimension(
  raw: unknown,
  fallback: number,
): LayoutDimension {
  if (typeof raw === "number") return raw;
  if (typeof raw === "string" && raw.trim()) return raw.trim();
  return fallback;
}

export function layoutDimensionLabel(dim: LayoutDimension): string {
  return typeof dim === "number" ? String(dim) : dim;
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
  width: LayoutDimension;
  height: LayoutDimension;
  /**
   * Controls inline width style projection.
   * undefined = backward-compat (always project, as before).
   * "auto"   = display-only default; not saved or projected as inline style.
   * "preset" = sizing layoutClassRef active; inline style suppressed.
   * "custom" = explicit user value; saved and projected as inline style.
   */
  widthMode?: SizingMode;
  heightMode?: SizingMode;
  nodeKind?: LayoutNodeKind;
  htmlTag?: StructuralHtmlTag;
  layoutClassRefs?: string[];
  componentId?: string;
  packageId?: string;
  layoutId?: string;
  wiringId?: string;
  tensorId?: string;
  componentKind?: string;
  /** Serialized component props override for runtime wiring (JSON string). Flowed through layout_patch_json → backend → renderEmission. */
  propsJson?: string;
  /** Serialized component state override for runtime wiring (JSON string). Merged into props.data at render time (e.g. open:bool for modal/drawer). */
  stateJson?: string;
  /** Array prop bindings: runtime data path → component prop. Resolved after propsJson/stateJson in renderEmission. */
  propBindings?: Record<string, { source: string; keyPath?: string; labelPath?: string; valuePath?: string; childrenPath?: string; transform?: string }> | null;
}

export function isStructuralHtmlNode(node: Pick<VisualNodePayload, "nodeKind">): boolean {
  return node.nodeKind === "structural_html";
}

export function resolveLayoutNodeKind(
  raw: Record<string, unknown>,
): LayoutNodeKind {
  return raw.nodeKind === "structural_html" ? "structural_html" : "catalog_component";
}

export function readNodeLayoutClassRefs(raw: Record<string, unknown>): string[] {
  if (!Array.isArray(raw.layoutClassRefs)) return [];
  return raw.layoutClassRefs.filter((v): v is string =>
    typeof v === "string" && v.trim().length > 0
  );
}

/** Copy a layout node with a new id and slight offset (layout editor copy operation). */
export function cloneVisualNode(
  source: VisualNodePayload,
  newNodeId: string,
  offset: { x?: number; y?: number } = {},
): VisualNodePayload {
  const dx = offset.x ?? 20;
  const dy = offset.y ?? 20;
  return {
    ...source,
    nodeId: newNodeId,
    x: source.x + dx,
    y: source.y + dy,
    orderIndex: source.orderIndex + 1,
  };
}

export const DEFAULT_VISUAL_NODE_WIDTH = 140;
export const DEFAULT_VISUAL_NODE_HEIGHT = 60;

export function makeStructuralHtmlNode(
  htmlTag: StructuralHtmlTag,
  options: {
    nodeId: string;
    x: number;
    y: number;
    orderIndex: number;
    parentNodeId?: string | null;
    width?: number;
    height?: number;
  },
): VisualNodePayload {
  return {
    nodeId: options.nodeId,
    nodeKind: "structural_html",
    htmlTag,
    componentKey: STRUCTURAL_HTML_COMPONENT_KEY,
    componentKind: "layout/structural_html",
    isDraftOnly: false,
    slotKey: "",
    orderIndex: options.orderIndex,
    parentNodeId: options.parentNodeId ?? null,
    gridCol: Math.max(1, Math.floor(options.x / 50) + 1),
    gridRow: Math.max(1, Math.floor(options.y / 40) + 1),
    x: options.x,
    y: options.y,
    width: options.width ?? DEFAULT_VISUAL_NODE_WIDTH,
    height: options.height ?? DEFAULT_VISUAL_NODE_HEIGHT,
    widthMode: "auto" as SizingMode,
    heightMode: "auto" as SizingMode,
  };
}

/**
 * Snap a value to the nearest grid increment.
 */
export function snapToGrid(value: number, snapSize: number): number {
  if (snapSize <= 0) return value;
  return Math.round(value / snapSize) * snapSize;
}

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
  calculationBindings?: CalcBinding[];
};

export type ParseVisualLayoutPatchResult =
  | { ok: true; value: ParsedVisualLayoutPatch }
  | { ok: false; error: string };

function readPatchNode(
  raw: Record<string, unknown>,
  paletteByKey: Map<string, PaletteDraftSeedEntry>,
): VisualNodePayload | null {
  const nodeId = typeof raw.nodeId === "string" ? raw.nodeId.trim() : "";
  if (!nodeId) return null;
  const nodeKind = resolveLayoutNodeKind(raw);
  const htmlTag = typeof raw.htmlTag === "string" ? raw.htmlTag.trim() : "";
  const componentKey = typeof raw.componentKey === "string"
    ? raw.componentKey.trim()
    : "";
  if (nodeKind === "structural_html") {
    if (!htmlTag || !(STRUCTURAL_HTML_TAG_ALLOWLIST as readonly string[]).includes(htmlTag)) {
      return null;
    }
  } else if (!componentKey) {
    return null;
  }
  const palette = componentKey ? paletteByKey.get(componentKey) : undefined;
  const isDraftOnly = raw._draftOnly === true ||
    (palette?.isDraftOnly ?? false);
  const layoutClassRefs = readNodeLayoutClassRefs(raw);
  return {
    nodeId,
    nodeKind,
    ...(nodeKind === "structural_html"
      ? { htmlTag: htmlTag as StructuralHtmlTag, componentKey: STRUCTURAL_HTML_COMPONENT_KEY }
      : { componentKey }),
    isDraftOnly,
    slotKey: typeof raw.slotKey === "string" ? raw.slotKey : "",
    orderIndex: typeof raw.orderIndex === "number" ? raw.orderIndex : 0,
    parentNodeId: typeof raw.parentNodeId === "string" ? raw.parentNodeId : null,
    gridCol: typeof raw.gridCol === "number" ? raw.gridCol : 1,
    gridRow: typeof raw.gridRow === "number" ? raw.gridRow : 1,
    x: typeof raw.x === "number" ? raw.x : 0,
    y: typeof raw.y === "number" ? raw.y : 0,
    width: readLayoutDimension(raw.width, DEFAULT_VISUAL_NODE_WIDTH),
    height: readLayoutDimension(raw.height, DEFAULT_VISUAL_NODE_HEIGHT),
    ...(layoutClassRefs.length > 0 ? { layoutClassRefs } : {}),
    componentId: typeof raw.componentId === "string"
      ? raw.componentId
      : palette?.componentId,
    packageId: typeof raw.packageId === "string"
      ? raw.packageId
      : palette?.packageId,
    layoutId: typeof raw.layoutId === "string" ? raw.layoutId : palette?.layoutId,
    wiringId: typeof raw.wiringId === "string" ? raw.wiringId : palette?.wiringId,
    tensorId: typeof raw.tensorId === "string" ? raw.tensorId : palette?.tensorId,
    componentKind: nodeKind === "structural_html"
      ? "layout/structural_html"
      : palette?.componentKind,
    propsJson: typeof raw.propsJson === "string" ? raw.propsJson : undefined,
    stateJson: typeof raw.stateJson === "string" ? raw.stateJson : undefined,
    propBindings: (typeof raw.propBindings === "object" && raw.propBindings !== null && !Array.isArray(raw.propBindings))
      ? raw.propBindings as Record<string, { source: string; keyPath?: string; labelPath?: string; valuePath?: string; childrenPath?: string; transform?: string }>
      : undefined,
    widthMode: (raw.widthMode === "auto" || raw.widthMode === "preset" || raw.widthMode === "custom")
      ? raw.widthMode as SizingMode
      : undefined,
    heightMode: (raw.heightMode === "auto" || raw.heightMode === "preset" || raw.heightMode === "custom")
      ? raw.heightMode as SizingMode
      : undefined,
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
  const calculationBindings = Array.isArray(root.calculationBindings)
    ? (root.calculationBindings as unknown[]).filter(
        (b): b is CalcBinding =>
          typeof b === "object" && b !== null && !Array.isArray(b) &&
          typeof (b as Record<string, unknown>).calculationId === "string",
      )
    : undefined;
  return { ok: true, value: { nodes, layoutClassRefs, calculationBindings } };
}

/** Legacy bulk auto-place canvas: every nodeId is seed_{componentKey}_{index}. */
export function isPaletteAutoSeedCanvas(
  nodes: Array<{ nodeId?: string }>,
): boolean {
  return nodes.length >= 2 &&
    nodes.every((n) =>
      typeof n.nodeId === "string" && /^seed_.+_\d+$/.test(n.nodeId)
    );
}

/** @deprecated Authoring must start from an empty canvas; opt-in seed only for tests/tools. */
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
    widthMode: "auto" as SizingMode,
    heightMode: "auto" as SizingMode,
    componentId: entry.componentId,
    packageId: entry.packageId,
    layoutId: entry.layoutId,
    wiringId: entry.wiringId,
    tensorId: entry.tensorId,
  }));
}

/** Fill missing componentId from palette/registry before layout_patch apply. */
export function enrichDraftNodesWithPaletteComponentIds<
  T extends { componentKey: string; componentId?: string; nodeKind?: string },
>(
  nodes: readonly T[],
  palette: readonly { componentKey: string; componentId?: string }[],
): T[] {
  const idByKey = new Map<string, string>();
  for (const entry of palette) {
    if (entry.componentId?.trim()) {
      idByKey.set(entry.componentKey, entry.componentId.trim());
    }
  }
  return nodes.map((node) => {
    if (node.nodeKind === "structural_html" || node.componentId?.trim()) {
      return node;
    }
    const resolved = idByKey.get(node.componentKey);
    return resolved ? { ...node, componentId: resolved } : node;
  });
}

export function buildVisualLayoutPatchJson(
  nodes: VisualNodePayload[],
  layoutClassRefs: string[] = [],
  calculationBindings?: CalcBinding[],
): string {
  return JSON.stringify(
    {
      grid: { cols: 12 },
      ...(layoutClassRefs.length > 0 ? { layoutClassRefs } : {}),
      ...(calculationBindings && calculationBindings.length > 0 ? { calculationBindings } : {}),
      nodes: nodes.map((n) => ({
        nodeId: n.nodeId,
        ...(n.nodeKind ? { nodeKind: n.nodeKind } : {}),
        ...(n.nodeKind === "structural_html" && n.htmlTag ? { htmlTag: n.htmlTag } : {}),
        componentKey: n.componentKey,
        ...(n.isDraftOnly ? { _draftOnly: true } : {}),
        ...(n.componentId ? { componentId: n.componentId } : {}),
        ...(n.packageId ? { packageId: n.packageId } : {}),
        ...(n.layoutId ? { layoutId: n.layoutId } : {}),
        ...(n.wiringId ? { wiringId: n.wiringId } : {}),
        ...(n.tensorId ? { tensorId: n.tensorId } : {}),
        ...(n.layoutClassRefs && n.layoutClassRefs.length > 0
          ? { layoutClassRefs: n.layoutClassRefs }
          : {}),
        ...(n.propsJson ? { propsJson: n.propsJson } : {}),
        ...(n.stateJson ? { stateJson: n.stateJson } : {}),
        ...(n.propBindings && Object.keys(n.propBindings).length > 0 ? { propBindings: n.propBindings } : {}),
        slotKey: n.slotKey || null,
        orderIndex: n.orderIndex,
        parentNodeId: n.parentNodeId || null,
        gridCol: n.gridCol,
        gridRow: n.gridRow,
        // width/height are only serialized when mode is "custom" or absent (backward-compat).
        // "auto" and "preset" modes suppress inline dimensions; sizing classRef or CSS drives size.
        ...(!n.widthMode || n.widthMode === "custom" ? { width: n.width } : {}),
        ...(!n.heightMode || n.heightMode === "custom" ? { height: n.height } : {}),
        ...(n.widthMode ? { widthMode: n.widthMode } : {}),
        ...(n.heightMode ? { heightMode: n.heightMode } : {}),
      })),
    },
    null,
    2,
  );
}

/** Returns nodes that are draft-only and would block apply. */
/**
 * True when patch nodes look like legacy absolute/seed flat placement (not flow tree).
 * SSOT: do not silently reinterpret — require explicit migration in UI.
 */
export function isLegacyAbsoluteLayoutPatch(nodes: VisualNodePayload[]): boolean {
  if (nodes.length === 0) return false;
  if (nodes.every((n) => n.nodeId.startsWith("seed_"))) return true;
  const allRoot = nodes.every((n) => !n.parentNodeId);
  if (!allRoot) return false;
  const hasDistinctCoords = nodes.some((n) => n.x !== 0 || n.y !== 0);
  if (!hasDistinctCoords) return false;
  const distinctY = new Set(nodes.map((n) => n.y));
  return distinctY.size > 1 || nodes.length > 1;
}

/** User-confirmed migration: y-sorted flat stack under root, strip x/y. */
export function migrateAbsolutePatchToFlowStack(
  nodes: VisualNodePayload[],
): VisualNodePayload[] {
  const sorted = [...nodes].sort((a, b) => {
    if (a.y !== b.y) return a.y - b.y;
    if (a.x !== b.x) return a.x - b.x;
    return a.orderIndex - b.orderIndex;
  });
  return sorted.map((n, i) => ({
    ...n,
    parentNodeId: null,
    orderIndex: i,
    x: 0,
    y: 0,
    gridCol: 1,
    gridRow: i + 1,
  }));
}

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

export type LayoutStackDirection = "front" | "back";

/**
 * Reorder layout nodes for z-stack (later index = painted on top on canvas).
 * "front" moves toward higher index; "back" toward lower index.
 */
export function reorderLayoutNodeStack<T extends { nodeId: string; orderIndex: number }>(
  nodes: T[],
  nodeId: string,
  direction: LayoutStackDirection,
): T[] | null {
  const idx = nodes.findIndex((n) => n.nodeId === nodeId);
  if (idx < 0) return null;
  const swapIdx = direction === "front" ? idx + 1 : idx - 1;
  if (swapIdx < 0 || swapIdx >= nodes.length) return null;
  const next = [...nodes];
  [next[idx], next[swapIdx]] = [next[swapIdx], next[idx]];
  return next.map((n, i) => ({ ...n, orderIndex: i }));
}
