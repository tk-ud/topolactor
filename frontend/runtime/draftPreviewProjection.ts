import type { FlowCanvasDesignDraft } from "../components/FlowLayoutCanvas.tsx";
import type { DraftPreviewLayoutNode, DraftPreviewInitialDataRow } from "../api/draftPreview.ts";
import type { LayoutPreviewNodeInput } from "./layoutComponentPreview.ts";
import { enrichLayoutPreviewNodes } from "./layoutComponentPreview.ts";

export type DraftPreviewNodeDesign = {
  designId?: string;
  inlineText?: string;
  linkHref?: string;
  linkTarget?: string;
  cssTokenRefs?: string[];
  classname?: string;
  tailwind?: string;
  hasDesignTmpDraft?: boolean;
};

/** Build designDraftByNodeId map from API designByNodeId payload. */
export function buildDesignDraftMapFromPreview(
  designByNodeId?: Record<string, DraftPreviewNodeDesign>,
): Map<string, FlowCanvasDesignDraft> {
  const map = new Map<string, FlowCanvasDesignDraft>();
  if (!designByNodeId) return map;
  for (const [nodeId, design] of Object.entries(designByNodeId)) {
    if (!nodeId.trim()) continue;
    map.set(nodeId, {
      inlineText: design.inlineText,
      linkHref: design.linkHref,
      linkTarget: design.linkTarget,
      cssTokenRefs: design.cssTokenRefs,
    });
  }
  return map;
}

/** Convert draft-preview layout nodes into FlowLayoutCanvas / audit inputs. */
export function draftPreviewNodesToAuditInputs(
  nodes: DraftPreviewLayoutNode[],
): LayoutPreviewNodeInput[] {
  const enriched = enrichLayoutPreviewNodes(
    nodes.map((node) => ({
      nodeId: node.nodeId ?? `node-${node.orderIndex}`,
      componentKey: node.componentKey ?? "box.primitive",
      componentKind: node.componentKind,
      componentId: node.componentId,
      nodeKind: node.nodeKind,
      htmlTag: node.htmlTag,
      isDraftOnly: false,
      parentNodeId: node.parentNodeId ?? null,
      orderIndex: node.orderIndex,
      slotKey: node.slotKey ?? "",
      x: typeof node.x === "number" ? node.x : 0,
      y: typeof node.y === "number" ? node.y : 0,
      width: node.width ?? (node.widthMode === "preset" ? "auto" : 160),
      height: node.height ?? (node.heightMode === "preset" ? "auto" : 72),
      widthMode: node.widthMode,
      heightMode: node.heightMode,
      layoutClassRefs: node.layoutClassRefs,
      inlineText: node.design?.inlineText,
      linkHref: node.design?.linkHref,
    })),
    [],
  );
  return enriched as LayoutPreviewNodeInput[];
}

/** Parse ?layoutId= from a preview/inspection deep-link query string. */
export function parseDraftPreviewQueryParams(
  search: string,
): { layoutId: string } {
  const params = new URLSearchParams(search.startsWith("?") ? search.slice(1) : search);
  return {
    layoutId: params.get("layoutId")?.trim() ?? "",
  };
}

/** Map topology intent row fields into slot display (first row for sample projection). */
export function renderTopologyIntentInSlot(
  row: DraftPreviewInitialDataRow,
  slotKey: string | undefined,
  slotIndex: number,
  totalSlots: number,
): Array<{ key: string; value: string }> {
  const entries = Object.entries(row).filter(
    ([, v]) => v !== null && v !== undefined && v !== "",
  );

  const slotName = (slotKey ?? "").toLowerCase();
  const directMatch = entries.find(([k]) => k.toLowerCase() === slotName);
  if (directMatch) {
    return [{ key: directMatch[0], value: String(directMatch[1]) }];
  }

  const fieldsPerSlot = Math.max(1, Math.ceil(entries.length / Math.max(totalSlots, 1)));
  const start = slotIndex * fieldsPerSlot;
  return entries.slice(start, start + fieldsPerSlot).map(([key, value]) => ({
    key,
    value: String(value),
  }));
}
