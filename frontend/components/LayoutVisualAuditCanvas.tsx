import { JSX } from "preact";
import { resolveCanvasRootPreviewClassName } from "../runtime/layoutClassPreviewUtils.ts";
import type { LayoutPreviewNodeInput } from "../runtime/layoutComponentPreview.ts";
import { LayoutPreviewNodeFrame } from "./LayoutPreviewNodeFrame.tsx";

export type LayoutVisualAuditCanvasProps = {
  nodes: LayoutPreviewNodeInput[];
  layoutClassRefs?: string[];
  title?: string;
  minHeight?: number;
};

function computeCanvasBounds(nodes: LayoutPreviewNodeInput[]): {
  width: number;
  height: number;
} {
  if (nodes.length === 0) {
    return { width: 640, height: 360 };
  }
  const width = Math.max(...nodes.map((node) => node.x + node.width), 480) + 32;
  const height = Math.max(...nodes.map((node) => node.y + node.height), 280) + 32;
  return { width, height };
}

/**
 * Read-only visual audit surface — renders placed nodes with runtime primitive preview.
 * SSOT: registrar visual preview + ui-ux-primitive-catalog preview_only surfaces.
 */
export function LayoutVisualAuditCanvas({
  nodes,
  layoutClassRefs = [],
  title = "配置プレビュー",
  minHeight = 320,
}: LayoutVisualAuditCanvasProps): JSX.Element {
  const bounds = computeCanvasBounds(nodes);
  const canvasClassName = resolveCanvasRootPreviewClassName(layoutClassRefs);

  return (
    <section
      class="rounded-lg border border-slate-200 bg-slate-50 p-3"
      aria-label={title}
    >
      <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
        <h3 class="m-0 text-sm font-semibold text-slate-900">{title}</h3>
        <span class="text-xs text-slate-500">{nodes.length} ノード</span>
      </div>
      <div
        class={`relative overflow-auto rounded-md border border-dashed border-slate-300 bg-white ${canvasClassName}`}
        style={{ minHeight: `${minHeight}px`, height: `${Math.min(bounds.height, 520)}px` }}
      >
        {nodes.length === 0
          ? (
            <div class="flex h-full min-h-[280px] items-center justify-center text-sm text-slate-400">
              配置ノードがありません
            </div>
          )
          : (
            <div
              class="relative"
              style={{ width: `${bounds.width}px`, height: `${bounds.height}px` }}
            >
              {nodes.map((node) => (
                <div
                  key={node.nodeId}
                  class={`absolute overflow-hidden rounded border shadow-sm ${
                    node.isDraftOnly
                      ? "border-yellow-300 bg-yellow-50"
                      : "border-slate-200 bg-white"
                  }`}
                  style={{
                    left: `${node.x}px`,
                    top: `${node.y}px`,
                    width: `${node.width}px`,
                    height: `${node.height}px`,
                  }}
                  aria-label={`${node.componentKey} preview`}
                >
                  {node.nodeKind === "structural_html" && node.htmlTag
                    ? (
                      <div class="flex h-full flex-col justify-center p-2 text-xs text-slate-800">
                        <div class="font-mono text-[0.6rem] text-slate-400">&lt;{node.htmlTag}&gt;</div>
                        {node.htmlTag === "a" && node.linkHref
                          ? (
                            <a href={node.linkHref} class="truncate text-blue-700 underline">
                              {node.inlineText || node.linkHref}
                            </a>
                          )
                          : (
                            <span class="truncate">{node.inlineText || `(${node.htmlTag})`}</span>
                          )}
                      </div>
                    )
                    : (
                      <LayoutPreviewNodeFrame
                        componentKey={node.componentKey}
                        componentKind={node.componentKind}
                        componentId={node.componentId}
                        isDraftOnly={node.isDraftOnly}
                      />
                    )}
                </div>
              ))}
            </div>
          )}
      </div>
    </section>
  );
}
