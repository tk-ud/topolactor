import { h, type JSX } from "preact";
import {
  flowNodePresentation,
  flowRootClassName,
} from "../runtime/layoutNodeFlowProjection.ts";
import {
  isLayoutStructureContainerClassRefs,
  resolveFlowContainerPreviewClassName,
  resolveFlowNodePreviewClassName,
  FLOW_LEAF_ROLES,
} from "../runtime/layoutClassPreviewUtils.ts";
import type { LayoutNodeKind, SizingMode, StructuralHtmlTag } from "../runtime/visualLayoutUtils.ts";
import { layoutDimensionLabel } from "../runtime/visualLayoutUtils.ts";
import { LayoutPreviewNodeFrame } from "./LayoutPreviewNodeFrame.tsx";
import { UX_EMPTY_CANVAS_DRAG_GUIDANCE, UX_ROUTE_KEY_REQUIRED_FOR_CANVAS } from "../content/adminUxTerms.ts";
import { resolveInlineStyleFromCssTokenRefs } from "../runtime/cssDictionary.ts";
import { interpolateLinkHrefReadOnly } from "../runtime/linkPlaceholderInterpolation.ts";

export type FlowCanvasNode = {
  nodeId: string;
  componentKey: string;
  componentKind?: string;
  nodeKind?: LayoutNodeKind;
  htmlTag?: StructuralHtmlTag;
  layoutClassRefs?: string[];
  isDraftOnly: boolean;
  slotKey: string;
  orderIndex: number;
  parentNodeId: string | null;
  width?: number | string;
  height?: number | string;
  widthMode?: SizingMode;
  heightMode?: SizingMode;
  componentId?: string;
};

export type FlowCanvasDesignDraft = {
  inlineText?: string;
  linkHref?: string;
  linkTarget?: string;
  cssTokenRefs?: string[];
};

function isContainerNode(node: FlowCanvasNode, childCount: number): boolean {
  if (childCount > 0) return true;
  return isLayoutStructureContainerClassRefs(node.layoutClassRefs ?? []);
}

function buildFlowChildrenMap(
  nodes: FlowCanvasNode[],
): Map<string | undefined, FlowCanvasNode[]> {
  const map = new Map<string | undefined, FlowCanvasNode[]>();
  for (const node of nodes) {
    const key = node.parentNodeId ?? undefined;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(node);
  }
  for (const children of map.values()) {
    children.sort((a, b) => a.orderIndex - b.orderIndex);
  }
  return map;
}

function FlowCanvasNodeView({
  node,
  childrenMap,
  selectedNodeId,
  selectedNodeIds,
  designDraftByNodeId,
  onSelectNode,
  onDeleteNode,
  calcTriggerNodeIds,
  calcOverridesByNodeId,
  onNodeValueChange,
}: {
  node: FlowCanvasNode;
  childrenMap: Map<string | undefined, FlowCanvasNode[]>;
  selectedNodeId: string | null;
  selectedNodeIds?: ReadonlySet<string>;
  designDraftByNodeId: ReadonlyMap<string, FlowCanvasDesignDraft>;
  onSelectNode: (nodeId: string) => void;
  onDeleteNode?: (nodeId: string) => void;
  calcTriggerNodeIds?: ReadonlySet<string>;
  calcOverridesByNodeId?: ReadonlyMap<string, Record<string, unknown>>;
  onNodeValueChange?: (nodeId: string, propKey: string, value: unknown) => void;
}): JSX.Element {
  const children = childrenMap.get(node.nodeId) ?? [];
  const isSelected = node.nodeId === selectedNodeId;
  const isInSelectionSet = selectedNodeIds != null && selectedNodeIds.has(node.nodeId) && !isSelected;
  const design = designDraftByNodeId.get(node.nodeId);
  const isContainer = isContainerNode(node, children.length);
  const { style: flowStyle, className: flowClassName } = flowNodePresentation(node);
  const className = isContainer
    ? resolveFlowContainerPreviewClassName(node.layoutClassRefs ?? [], isSelected)
    : resolveFlowNodePreviewClassName(node.layoutClassRefs ?? [], {
      allowedFor: FLOW_LEAF_ROLES,
      isSelected,
    }) || flowClassName;
  const tokenResult = design?.cssTokenRefs?.length
    ? resolveInlineStyleFromCssTokenRefs(design.cssTokenRefs)
    : { ok: true as const, style: {} };
  const style = { ...flowStyle, ...(tokenResult.ok ? tokenResult.style : {}) };
  const sizeLabel = `${layoutDimensionLabel(node.width ?? "auto")}×${
    layoutDimensionLabel(node.height ?? "auto")
  }`;

  const childElements = children.map((child) => (
    <FlowCanvasNodeView
      key={child.nodeId}
      node={child}
      childrenMap={childrenMap}
      selectedNodeId={selectedNodeId}
      selectedNodeIds={selectedNodeIds}
      designDraftByNodeId={designDraftByNodeId}
      onSelectNode={onSelectNode}
      onDeleteNode={onDeleteNode}
      calcTriggerNodeIds={calcTriggerNodeIds}
      calcOverridesByNodeId={calcOverridesByNodeId}
      onNodeValueChange={onNodeValueChange}
    />
  ));

  const commonProps = {
    "data-node-id": node.nodeId,
    class: [
      className,
      isSelected ? "ring-2 ring-blue-400 ring-offset-1" : isInSelectionSet ? "ring-1 ring-indigo-300 ring-offset-1" : "",
      "rounded border border-slate-200",
    ].filter(Boolean).join(" ") || undefined,
    style: Object.keys(style).length > 0 ? style : undefined,
    onClick: (e: Event) => {
      (e as MouseEvent).stopPropagation();
      onSelectNode(node.nodeId);
    },
    onKeyDown: (e: KeyboardEvent) => {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        onSelectNode(node.nodeId);
      }
      if ((e.key === "Delete" || e.key === "Backspace") && onDeleteNode) {
        e.preventDefault();
        onDeleteNode(node.nodeId);
      }
    },
    tabIndex: 0,
    role: "button" as const,
    "aria-selected": isSelected,
  };

  if (node.nodeKind === "structural_html" && node.htmlTag) {
    const text = design?.inlineText?.trim();
    const href = design?.linkHref?.trim();
    const linkPreview = node.htmlTag === "a" ? interpolateLinkHrefReadOnly(href) : null;
    const inner = href && node.htmlTag === "a"
      ? text || (linkPreview?.ok ? linkPreview.value : href)
      : text;
    return h(
      node.htmlTag,
      {
        ...commonProps,
        ...(node.htmlTag === "a" && linkPreview?.ok && linkPreview.value ? { href: linkPreview.value } : {}),
        ...(node.htmlTag === "a" && linkPreview && !linkPreview.ok ? { "aria-invalid": true } : {}),
      },
      inner,
      node.htmlTag === "a" && linkPreview && !linkPreview.ok
        ? h("span", { role: "alert", class: "ml-1 text-[0.6rem] text-red-700" }, linkPreview.message)
        : null,
      ...childElements,
    ) as JSX.Element;
  }

  if (isContainer) {
    return (
      <div {...commonProps}>
        {!tokenResult.ok && (
          <div role="alert" class="mb-1 rounded border border-red-200 bg-red-50 p-1 text-[0.6rem] text-red-700">
            {tokenResult.message}
          </div>
        )}
        {childElements.length > 0
          ? childElements
          : (
            <span class="px-1 text-[0.55rem] italic text-slate-400">
              （空コンテナ — 子をドロップ）
            </span>
          )}
        {(node.slotKey || (sizeLabel && node.widthMode === "custom" && node.heightMode === "custom")) && (
          <div class="border-t border-slate-100 bg-slate-50 px-1 py-0.5 font-mono text-[0.48rem] text-slate-400">
            {node.slotKey && <span>slot:{node.slotKey} </span>}
            {node.widthMode === "custom" && node.heightMode === "custom" && (
              <span>{sizeLabel}</span>
            )}
          </div>
        )}
      </div>
    );
  }

  const calcTriggerCallback = calcTriggerNodeIds?.has(node.nodeId)
    ? (value: unknown) => onNodeValueChange?.(node.nodeId, "value", value)
    : undefined;
  const calcValueOverridesForNode = calcOverridesByNodeId?.get(node.nodeId);

  return (
    <div {...commonProps}>
      {!tokenResult.ok && (
        <div role="alert" class="m-1 rounded border border-red-200 bg-red-50 p-1 text-[0.6rem] text-red-700">
          {tokenResult.message}
        </div>
      )}
      <div class="min-h-0 overflow-auto p-1">
        <LayoutPreviewNodeFrame
          componentKey={node.componentKey}
          componentKind={node.componentKind}
          componentId={node.componentId}
          isDraftOnly={node.isDraftOnly}
          inlineText={design?.inlineText}
          linkHref={design?.linkHref}
          linkTarget={design?.linkTarget}
          calcTriggerCallback={calcTriggerCallback}
          calcValueOverrides={calcValueOverridesForNode}
        />
      </div>
      {(node.slotKey || (sizeLabel && node.widthMode === "custom" && node.heightMode === "custom")) && (
        <div class="border-t border-slate-100 bg-slate-50 px-1 py-0.5 font-mono text-[0.48rem] text-slate-400">
          {node.slotKey && <span>slot:{node.slotKey} </span>}
          {node.widthMode === "custom" && node.heightMode === "custom" && (
            <span>{sizeLabel}</span>
          )}
        </div>
      )}
    </div>
  );
}

export type FlowLayoutCanvasProps = {
  nodes: FlowCanvasNode[];
  selectedNodeId: string | null;
  /** Full selection set for multi-select visual highlight (ring-indigo on non-primary nodes). */
  selectedNodeIds?: ReadonlySet<string>;
  rootLayoutClassRefs?: string[];
  designDraftByNodeId?: ReadonlyMap<string, FlowCanvasDesignDraft>;
  canvasRef?: { current: HTMLDivElement | null };
  minHeight?: number;
  onSelectNode: (nodeId: string) => void;
  onDeselectAll?: () => void;
  onDragOver?: (e: Event) => void;
  onDrop?: (e: Event) => void;
  onDeleteNode?: (nodeId: string) => void;
  onAddFromEmptyState?: (templateId: string) => void;
  allowEmptyStateTemplates?: boolean;
  emptyGuidance?: string;
  /** Calc binding trigger: nodeIds whose value changes should call onNodeValueChange */
  calcTriggerNodeIds?: ReadonlySet<string>;
  /** Calc binding results: nodeId → prop overrides injected by evaluateAllCalcBindings */
  calcOverridesByNodeId?: ReadonlyMap<string, Record<string, unknown>>;
  /** Called when a calc-trigger input node changes value. Never dispatches to backend. */
  onNodeValueChange?: (nodeId: string, propKey: string, value: unknown) => void;
};

export function FlowLayoutCanvas({
  nodes,
  selectedNodeId,
  selectedNodeIds,
  rootLayoutClassRefs = [],
  designDraftByNodeId = new Map(),
  canvasRef,
  minHeight = 480,
  onSelectNode,
  onDeselectAll,
  onDragOver,
  onDrop,
  onDeleteNode,
  onAddFromEmptyState,
  allowEmptyStateTemplates = true,
  emptyGuidance = UX_EMPTY_CANVAS_DRAG_GUIDANCE,
  calcTriggerNodeIds,
  calcOverridesByNodeId,
  onNodeValueChange,
}: FlowLayoutCanvasProps): JSX.Element {
  const childrenMap = buildFlowChildrenMap(nodes);
  const roots = childrenMap.get(undefined) ?? [];
  const rootClass = flowRootClassName(rootLayoutClassRefs);

  return (
    <div
      ref={canvasRef}
      role="application"
      aria-label="レイアウトキャンバス — クリックで選択。レイアウト構造は右のレイヤーツリーで編集"
      class="h-full min-h-0 flex-1 overflow-auto rounded-lg border-2 border-dashed border-gray-300 bg-white p-3 focus-within:border-blue-300"
      style={{ minHeight: `${minHeight}px` }}
      onClick={() => onDeselectAll?.()}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      {nodes.length === 0
        ? (
          <div class="flex min-h-[280px] flex-col items-center justify-center gap-4 p-6 text-center">
            <div class="text-4xl text-gray-200" aria-hidden="true">☐</div>
            <div>
              <p class="text-base font-semibold text-gray-500">layout draft が空です</p>
              <p class="mt-1 text-sm text-gray-500">{emptyGuidance}</p>
              <p class="mt-1 text-xs text-slate-500">
                レイアウト構造は右のレイヤーツリーで編集します。
              </p>
              {!allowEmptyStateTemplates && (
                <p class="mt-1 text-xs text-amber-800">{UX_ROUTE_KEY_REQUIRED_FOR_CANVAS}</p>
              )}
            </div>
            {onAddFromEmptyState && allowEmptyStateTemplates && (
              <div class="flex flex-wrap justify-center gap-2">
                <button
                  type="button"
                  onClick={(e) => {
                    (e as MouseEvent).stopPropagation();
                    onAddFromEmptyState("starter_header_main");
                  }}
                  class="rounded-lg border border-blue-200 bg-blue-50 px-4 py-2 text-sm font-medium text-blue-700 hover:bg-blue-100"
                >
                  ヘッダー + メイン を追加
                </button>
                <button
                  type="button"
                  onClick={(e) => {
                    (e as MouseEvent).stopPropagation();
                    onAddFromEmptyState("starter_card");
                  }}
                  class="rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
                >
                  カードを追加
                </button>
              </div>
            )}
          </div>
        )
        : (
          <div class={`space-y-2 ${rootClass}`} data-flow-layout-root="true">
            {roots.map((node) => (
              <FlowCanvasNodeView
                key={node.nodeId}
                node={node}
                childrenMap={childrenMap}
                selectedNodeId={selectedNodeId}
                selectedNodeIds={selectedNodeIds}
                designDraftByNodeId={designDraftByNodeId}
                onSelectNode={onSelectNode}
                onDeleteNode={onDeleteNode}
                calcTriggerNodeIds={calcTriggerNodeIds}
                calcOverridesByNodeId={calcOverridesByNodeId}
                onNodeValueChange={onNodeValueChange}
              />
            ))}
          </div>
        )}
    </div>
  );
}
