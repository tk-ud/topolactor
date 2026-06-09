import type { ComponentSpec } from "./renderEmission.ts";
import { buildChildrenMap } from "./renderEmission.ts";
import {
  resolveCanvasRootPreviewClassName,
  resolveLayoutClassPreviewClassName,
  resolveNodeWrapperPreviewClassName,
} from "./layoutClassPreviewUtils.ts";
import { formatLayoutDimensionCss } from "./visualLayoutUtils.ts";

/** Default root flow container when no layoutClassRefs are set. */
export const DEFAULT_FLOW_ROOT_CLASS = "topolactor-topology-layout-section-stack";

export type FlowLayoutNodeLike = {
  nodeId?: string;
  parentNodeId?: string | null;
  orderIndex?: number;
  width?: number | string;
  height?: number | string;
  layoutClassRefs?: string[];
};

/** Flow projection: width/height only — x/y are not projected (SSOT flow mode). */
export function buildFlowNodeStyle(spec: {
  width?: number | string;
  height?: number | string;
}): Record<string, string> {
  const style: Record<string, string> = {};
  const widthCss = formatLayoutDimensionCss(spec.width);
  const heightCss = formatLayoutDimensionCss(spec.height);
  if (widthCss !== undefined) style.width = widthCss;
  if (heightCss !== undefined) style.height = heightCss;
  return style;
}

export function resolveFlowNodeClassName(
  layoutClassRefs: readonly string[] | undefined,
  options: {
    isSelected?: boolean;
    allowedFor?: string | readonly string[];
    isRoot?: boolean;
  } = {},
): string {
  const refs = layoutClassRefs ?? [];
  if (options.isRoot) {
    const root = resolveCanvasRootPreviewClassName(refs);
    return root || DEFAULT_FLOW_ROOT_CLASS;
  }
  if (refs.length === 0) {
    return options.isSelected
      ? resolveLayoutClassPreviewClassName(["layout.state.selected"], "preview_state")
      : "";
  }
  if (options.allowedFor) {
    const resolved = resolveLayoutClassPreviewClassName(refs, options.allowedFor);
    if (resolved) return resolved;
  }
  const wrapper = resolveNodeWrapperPreviewClassName(refs, options.isSelected ?? false);
  return wrapper;
}

export { buildChildrenMap };

export function flowRootClassName(
  rootLayoutClassRefs: readonly string[] = [],
): string {
  return resolveCanvasRootPreviewClassName(rootLayoutClassRefs) || DEFAULT_FLOW_ROOT_CLASS;
}

/** Merge inline style object with className for flow nodes. */
export function flowNodePresentation(
  spec: Pick<ComponentSpec, "width" | "height" | "layoutClassRefs">,
  options: {
    isSelected?: boolean;
    allowedFor?: string | readonly string[];
    isRoot?: boolean;
  } = {},
): { style: Record<string, string>; className: string | undefined } {
  const style = buildFlowNodeStyle(spec);
  const className = resolveFlowNodeClassName(spec.layoutClassRefs, options);
  return {
    style,
    className: className || undefined,
  };
}
