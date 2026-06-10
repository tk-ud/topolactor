import { JSX } from "preact";
import type { LayoutPreviewNodeInput } from "../runtime/layoutComponentPreview.ts";
import { FlowLayoutCanvas, type FlowCanvasNode } from "./FlowLayoutCanvas.tsx";
import type { StructuralHtmlTag } from "../runtime/visualLayoutUtils.ts";

export type LayoutVisualAuditCanvasProps = {
  nodes: LayoutPreviewNodeInput[];
  layoutClassRefs?: string[];
  title?: string;
  minHeight?: number;
};

function toFlowAuditNodes(nodes: LayoutPreviewNodeInput[]): FlowCanvasNode[] {
  return nodes.map((node, index) => ({
    nodeId: node.nodeId,
    componentKey: node.componentKey,
    componentKind: node.componentKind,
    nodeKind: node.nodeKind,
    htmlTag: node.htmlTag as StructuralHtmlTag | undefined,
    isDraftOnly: node.isDraftOnly ?? false,
    slotKey: node.slotKey ?? "",
    orderIndex: node.orderIndex ?? index,
    parentNodeId: node.parentNodeId ?? null,
    width: node.width,
    height: node.height,
    componentId: node.componentId,
  }));
}

/**
 * Read-only visual audit surface — flow tree projection (SSOT v0.9.0).
 */
export function LayoutVisualAuditCanvas({
  nodes,
  layoutClassRefs = [],
  title = "配置プレビュー",
  minHeight = 320,
}: LayoutVisualAuditCanvasProps): JSX.Element {
  const flowNodes = toFlowAuditNodes(nodes);

  return (
    <section
      class="rounded-lg border border-slate-200 bg-slate-50 p-3"
      aria-label={title}
    >
      <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
        <div class="flex items-center gap-2">
          <h3 class="m-0 text-sm font-semibold text-slate-900">{title}</h3>
          <span class="rounded bg-slate-200 px-1.5 py-0.5 text-[0.6rem] font-medium text-slate-600">
            読み取り専用
          </span>
        </div>
        <span class="text-xs text-slate-500">{nodes.length} ノード</span>
      </div>
      <FlowLayoutCanvas
        nodes={flowNodes}
        selectedNodeId={null}
        rootLayoutClassRefs={layoutClassRefs}
        minHeight={minHeight}
        onSelectNode={() => {}}
        allowEmptyStateTemplates={false}
        emptyGuidance="配置ノードがありません"
      />
    </section>
  );
}
