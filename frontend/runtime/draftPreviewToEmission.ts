import type { DraftPreviewResult } from "../api/draftPreview.ts";
import type { Emission, LayoutNode } from "../api/dispatch.ts";

/** Map draft-preview API payload → Emission for renderEmission (same path as product). */
export function draftPreviewResultToEmission(
  result: DraftPreviewResult,
): Emission | null {
  if (!result.success || !result.layoutId || !result.layoutNodes?.length) {
    return null;
  }

  const layoutNodes: LayoutNode[] = result.layoutNodes.map((node) => ({
    nodeId: node.nodeId,
    nodeKind: node.nodeKind,
    htmlTag: node.htmlTag,
    componentKey: node.componentKey,
    componentId: node.componentId,
    componentKind: node.componentKind,
    parentNodeId: node.parentNodeId ?? undefined,
    slotKey: node.slotKey,
    orderIndex: node.orderIndex,
    x: node.x,
    y: node.y,
    width: node.width,
    height: node.height,
    widthMode: node.widthMode,
    heightMode: node.heightMode,
    layoutClassRefs: node.layoutClassRefs,
    propsJson: node.propsJson,
    stateJson: node.stateJson,
    runtimeInteractions: node.runtimeInteractions,
    componentDesign: node.design
      ? {
        inlineText: node.design.inlineText,
        linkHref: node.design.linkHref,
        linkTarget: node.design.linkTarget,
        cssTokenRefs: node.design.cssTokenRefs,
        classname: node.design.classname,
        tailwind: node.design.tailwind,
      }
      : undefined,
  }));

  const firstRow = result.initialDataRows?.[0];

  return {
    layoutId: result.layoutId,
    packageId: result.packageId,
    layoutNodes,
    data: firstRow && Object.keys(firstRow).length > 0 ? firstRow : undefined,
  };
}
